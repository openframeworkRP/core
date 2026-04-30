import { Router } from 'express'
import db from '../db.js'
import { requireAuth } from '../auth.js'
import { broadcast } from '../socket.js'

const router = Router()

// ── Helpers ───────────────────────────────────────────────────────────────

function taskFromRow(row) {
  return {
    id:          row.id,
    projectId:   row.project_id,
    text:        row.text,
    description: row.description,
    category:    row.category,
    status:      row.status,
    priority:    row.priority,
    assignees:   JSON.parse(row.assignees),
    subtasks:    JSON.parse(row.subtasks),
    deadline:    row.deadline,
    notes:       row.notes,
    images:      JSON.parse(row.images),
    videos:      JSON.parse(row.videos || '[]'),
    createdAt:   row.created_at,
    updatedAt:   row.updated_at,
    createdBy:   row.created_by || '',
    updatedBy:   row.updated_by || '',
  }
}

function ideaFromRow(row) {
  return {
    id:          row.id,
    text:        row.text,
    description: row.description || '',
    projectId:   row.project_id,
    comments:    JSON.parse(row.comments),
    votes:       JSON.parse(row.votes),
    createdAt:   row.created_at,
  }
}

function getMisc() {
  const row = db.prepare("SELECT value FROM hub_state WHERE key = 'misc'").get()
  if (!row) return { milestones: [], mapAnnotations: {}, fabAssets: [], fabStudios: [] }
  return JSON.parse(row.value)
}

// Champs autorisés pour PATCH tasks (frontend camelCase → DB snake_case)
const TASK_FIELD_MAP = {
  projectId: 'project_id', text: 'text', description: 'description',
  category: 'category', status: 'status', priority: 'priority',
  assignees: 'assignees', subtasks: 'subtasks', deadline: 'deadline',
  notes: 'notes', images: 'images', videos: 'videos',
}
// Champs JSON à sérialiser
const TASK_JSON_FIELDS = new Set(['assignees', 'subtasks', 'images', 'videos'])

// Champs autorisés pour PATCH ideas
const IDEA_FIELD_MAP = {
  text: 'text', description: 'description', projectId: 'project_id', comments: 'comments', votes: 'votes',
}
const IDEA_JSON_FIELDS = new Set(['comments', 'votes'])

function buildTaskParams(body) {
  const sets = []
  const vals = []
  for (const [key, col] of Object.entries(TASK_FIELD_MAP)) {
    if (!(key in body)) continue
    sets.push(`${col} = ?`)
    vals.push(TASK_JSON_FIELDS.has(key) ? JSON.stringify(body[key]) : body[key])
  }
  return { sets, vals }
}

function buildIdeaParams(body) {
  const sets = []
  const vals = []
  for (const [key, col] of Object.entries(IDEA_FIELD_MAP)) {
    if (!(key in body)) continue
    sets.push(`${col} = ?`)
    vals.push(IDEA_JSON_FIELDS.has(key) ? JSON.stringify(body[key]) : body[key])
  }
  return { sets, vals }
}

// ── GET /api/hub/roadmap — public (milestones publics + tâches associées) ─
// Renvoie uniquement les milestones avec public=true ET archived=false.
// Les tâches sont filtrées au minimum nécessaire (pas d'assignees, notes, etc.)
router.get('/roadmap', (_req, res) => {
  const misc = getMisc()
  const milestones = (misc.milestones || []).filter(m => m.public === true && !m.archived)
  if (!milestones.length) return res.json({ milestones: [] })

  const allTaskIds = milestones.flatMap(m => m.taskIds || [])
  const tasksById = new Map()
  if (allTaskIds.length) {
    const placeholders = allTaskIds.map(() => '?').join(',')
    const rows = db.prepare(`SELECT id, text, status FROM hub_tasks WHERE id IN (${placeholders})`).all(...allTaskIds)
    for (const r of rows) tasksById.set(r.id, { id: r.id, text: r.text, status: r.status })
  }

  const out = milestones
    .sort((a, b) => new Date(a.date) - new Date(b.date))
    .map(m => ({
      id:          m.id,
      name:        m.name,
      date:        m.date,
      description: m.description || '',
      color:       m.color || '#e07b39',
      tasks:       (m.taskIds || []).map(id => tasksById.get(id)).filter(Boolean),
    }))

  res.json({ milestones: out })
})

// ── GET /api/hub — lecture unifiée (même forme qu'avant pour reload()) ────
router.get('/', requireAuth, (_req, res) => {
  const tasks = db.prepare('SELECT * FROM hub_tasks').all().map(taskFromRow)
  const ideas = db.prepare('SELECT * FROM hub_ideas').all().map(ideaFromRow)
  const misc  = getMisc()
  res.json({ tasks, ideas, ...misc })
})

// ── PUT /api/hub — legacy (conservé pour transition) ─────────────────────
router.put('/', requireAuth, (req, res) => {
  db.prepare(`
    INSERT INTO hub_state (key, value, updated_at)
    VALUES ('hub', ?, datetime('now'))
    ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at
  `).run(JSON.stringify(req.body))
  broadcast('hub_updated', { by: req.user?.id })
  res.json({ ok: true })
})

// ── PUT /api/hub/misc ─────────────────────────────────────────────────────
router.put('/misc', requireAuth, (req, res) => {
  db.prepare(`
    INSERT INTO hub_state (key, value, updated_at)
    VALUES ('misc', ?, datetime('now'))
    ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at
  `).run(JSON.stringify(req.body))
  broadcast('misc_updated', { by: req.user?.id })
  res.json({ ok: true })
})

// ── GET /api/hub/brand-kit ────────────────────────────────────────────────
router.get('/brand-kit', requireAuth, (_req, res) => {
  const row = db.prepare("SELECT value FROM hub_state WHERE key = 'brand_kit'").get()
  if (!row) return res.json({ colors: [], fonts: [] })
  res.json(JSON.parse(row.value))
})

// ── PUT /api/hub/brand-kit ────────────────────────────────────────────────
router.put('/brand-kit', requireAuth, (req, res) => {
  db.prepare(`
    INSERT INTO hub_state (key, value, updated_at)
    VALUES ('brand_kit', ?, datetime('now'))
    ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at
  `).run(JSON.stringify(req.body))
  res.json({ ok: true })
})

// ── GET /api/hub/activity ─────────────────────────────────────────────────
router.get('/activity', requireAuth, (req, res) => {
  const limit = Math.min(parseInt(req.query.limit) || 200, 500)
  const { targetType, targetId } = req.query
  const where = []
  const params = []
  if (targetType) { where.push('target_type = ?'); params.push(targetType) }
  if (targetId)   { where.push('target_id = ?');   params.push(targetId) }
  const whereSql = where.length ? `WHERE ${where.join(' AND ')}` : ''
  const rows = db.prepare(`
    SELECT id, action, detail, author, target_type, target_id, created_at
    FROM hub_activity ${whereSql} ORDER BY id DESC LIMIT ?
  `).all(...params, limit)
  res.json(rows)
})

// ── POST /api/hub/activity ────────────────────────────────────────────────
router.post('/activity', requireAuth, (req, res) => {
  const { action, detail, author, targetType, targetId } = req.body
  if (!action) return res.status(400).json({ error: 'action requis' })
  const result = db.prepare(`
    INSERT INTO hub_activity (action, detail, author, target_type, target_id)
    VALUES (?, ?, ?, ?, ?)
  `).run(action, detail || '', author || req.user?.displayName || 'system', targetType || '', targetId || '')
  broadcast('hub_activity', { by: req.user?.id })
  res.status(201).json({ id: result.lastInsertRowid })
})

// ══════════════════════════════════════════════════════════════════════════
// TASKS — déclarer /bulk AVANT /:id pour éviter le conflit de routing
// ══════════════════════════════════════════════════════════════════════════

// ── POST /api/hub/tasks/bulk ──────────────────────────────────────────────
router.post('/tasks/bulk', requireAuth, (req, res) => {
  const tasks = Array.isArray(req.body.tasks) ? req.body.tasks : []
  if (!tasks.length) return res.status(400).json({ error: 'tasks[] requis' })

  const now = Date.now()
  const author = req.user?.displayName || ''
  const insert = db.prepare(`
    INSERT OR IGNORE INTO hub_tasks
      (id, project_id, text, description, category, status, priority,
       assignees, subtasks, deadline, notes, images, videos, created_at, updated_at, created_by)
    VALUES
      (@id, @project_id, @text, @description, @category, @status, @priority,
       @assignees, @subtasks, @deadline, @notes, @images, @videos, @created_at, @updated_at, @created_by)
  `)

  const created = db.transaction(() => {
    const rows = []
    for (const t of tasks) {
      const id = t.id || `t_${now}_${Math.random().toString(36).slice(2, 6)}`
      insert.run({
        id,
        project_id:  t.projectId  ?? t.project_id  ?? '',
        text:        t.text        ?? '',
        description: t.description ?? '',
        category:    t.category    ?? '',
        status:      t.status      ?? 'todo',
        priority:    t.priority    ?? null,
        assignees:   JSON.stringify(t.assignees  ?? []),
        subtasks:    JSON.stringify(t.subtasks   ?? []),
        deadline:    t.deadline    ?? null,
        notes:       t.notes       ?? '',
        images:      JSON.stringify(t.images     ?? []),
        videos:      JSON.stringify(t.videos     ?? []),
        created_at:  t.createdAt   ?? t.created_at   ?? now,
        updated_at:  t.updatedAt   ?? t.updated_at   ?? now,
        created_by:  t.createdBy   ?? t.created_by   ?? author,
      })
      rows.push({ ...t, id })
    }
    return rows
  })()

  broadcast('tasks_bulk_created', { tasks: created })
  res.status(201).json({ tasks: created, count: created.length })
})

// ── PATCH /api/hub/tasks/bulk ─────────────────────────────────────────────
router.patch('/tasks/bulk', requireAuth, (req, res) => {
  const { ids, changes } = req.body
  if (!Array.isArray(ids) || !ids.length || !changes) {
    return res.status(400).json({ error: 'ids[] et changes requis' })
  }

  const { sets, vals } = buildTaskParams(changes)
  if (!sets.length) return res.status(400).json({ error: 'Aucun champ valide' })

  sets.push('updated_at = ?', 'updated_by = ?')
  vals.push(Date.now(), req.user?.displayName || '')

  const stmt = db.prepare(`UPDATE hub_tasks SET ${sets.join(', ')} WHERE id = ?`)
  db.transaction(() => {
    for (const id of ids) stmt.run(...vals, id)
  })()

  broadcast('tasks_bulk_updated', { ids, changes })
  res.json({ updated: ids.length })
})

// ── POST /api/hub/tasks ───────────────────────────────────────────────────
router.post('/tasks', requireAuth, (req, res) => {
  const b   = req.body
  const now = Date.now()
  const id  = b.id || `t_${now}`
  const author = req.user?.displayName || ''

  db.prepare(`
    INSERT INTO hub_tasks
      (id, project_id, text, description, category, status, priority,
       assignees, subtasks, deadline, notes, images, videos, created_at, updated_at, created_by)
    VALUES
      (@id, @project_id, @text, @description, @category, @status, @priority,
       @assignees, @subtasks, @deadline, @notes, @images, @videos, @created_at, @updated_at, @created_by)
  `).run({
    id,
    project_id:  b.projectId  ?? b.project_id  ?? '',
    text:        b.text        ?? '',
    description: b.description ?? '',
    category:    b.category    ?? '',
    status:      b.status      ?? 'todo',
    priority:    b.priority    ?? null,
    assignees:   JSON.stringify(b.assignees  ?? []),
    subtasks:    JSON.stringify(b.subtasks   ?? []),
    deadline:    b.deadline    ?? null,
    notes:       b.notes       ?? '',
    images:      JSON.stringify(b.images     ?? []),
    videos:      JSON.stringify(b.videos     ?? []),
    created_at:  b.createdAt   ?? b.created_at   ?? now,
    updated_at:  b.updatedAt   ?? b.updated_at   ?? now,
    created_by:  b.createdBy   ?? b.created_by   ?? author,
  })

  const task = taskFromRow(db.prepare('SELECT * FROM hub_tasks WHERE id = ?').get(id))
  broadcast('task_created', { task })
  res.status(201).json({ task })
})

// ── PATCH /api/hub/tasks/:id ──────────────────────────────────────────────
router.patch('/tasks/:id', requireAuth, (req, res) => {
  const row = db.prepare('SELECT id FROM hub_tasks WHERE id = ?').get(req.params.id)
  if (!row) return res.status(404).json({ error: 'Task not found' })

  const { sets, vals } = buildTaskParams(req.body)
  if (sets.length) {
    sets.push('updated_at = ?', 'updated_by = ?')
    vals.push(Date.now(), req.user?.displayName || '')
    db.prepare(`UPDATE hub_tasks SET ${sets.join(', ')} WHERE id = ?`).run(...vals, req.params.id)
  }

  const task = taskFromRow(db.prepare('SELECT * FROM hub_tasks WHERE id = ?').get(req.params.id))
  broadcast('task_updated', { id: req.params.id, changes: req.body, task })
  res.json({ task })
})

// ── DELETE /api/hub/tasks/:id ─────────────────────────────────────────────
router.delete('/tasks/:id', requireAuth, (req, res) => {
  const taskId = req.params.id
  const info = db.prepare('DELETE FROM hub_tasks WHERE id = ?').run(taskId)
  if (!info.changes) return res.status(404).json({ error: 'Task not found' })
  // Nettoyer le taskId des milestones dans le misc blob
  const misc = getMisc()
  const milestones = misc.milestones || []
  const hadRef = milestones.some(m => (m.taskIds || []).includes(taskId))
  if (hadRef) {
    misc.milestones = milestones.map(m => ({
      ...m,
      taskIds: (m.taskIds || []).filter(id => id !== taskId)
    }))
    db.prepare(`
      INSERT INTO hub_state (key, value, updated_at)
      VALUES ('misc', ?, datetime('now'))
      ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at
    `).run(JSON.stringify(misc))
  }
  broadcast('task_deleted', { id: taskId })
  res.json({ ok: true })
})

// ══════════════════════════════════════════════════════════════════════════
// IDEAS — déclarer /bulk AVANT /:id
// ══════════════════════════════════════════════════════════════════════════

// ── POST /api/hub/ideas/bulk ──────────────────────────────────────────────
router.post('/ideas/bulk', requireAuth, (req, res) => {
  const ideas = Array.isArray(req.body.ideas) ? req.body.ideas : []
  if (!ideas.length) return res.status(400).json({ error: 'ideas[] requis' })

  const now = Date.now()
  const insert = db.prepare(`
    INSERT OR IGNORE INTO hub_ideas (id, text, description, project_id, comments, votes, created_at)
    VALUES (@id, @text, @description, @project_id, @comments, @votes, @created_at)
  `)

  const created = db.transaction(() => {
    const rows = []
    for (const i of ideas) {
      const id = i.id || `i_${now}_${Math.random().toString(36).slice(2, 6)}`
      insert.run({
        id,
        text:        i.text        ?? '',
        description: i.description ?? '',
        project_id:  i.projectId   ?? i.project_id ?? '',
        comments:    JSON.stringify(i.comments ?? []),
        votes:       JSON.stringify(i.votes    ?? {}),
        created_at:  i.createdAt   ?? i.created_at  ?? now,
      })
      rows.push({ ...i, id })
    }
    return rows
  })()

  broadcast('ideas_bulk_created', { ideas: created })
  res.status(201).json({ ideas: created, count: created.length })
})

// ── POST /api/hub/ideas ───────────────────────────────────────────────────
router.post('/ideas', requireAuth, (req, res) => {
  const b   = req.body
  const now = Date.now()
  const id  = b.id || `i_${now}`

  db.prepare(`
    INSERT INTO hub_ideas (id, text, description, project_id, comments, votes, created_at)
    VALUES (@id, @text, @description, @project_id, @comments, @votes, @created_at)
  `).run({
    id,
    text:        b.text        ?? '',
    description: b.description ?? '',
    project_id:  b.projectId   ?? b.project_id ?? '',
    comments:    JSON.stringify(b.comments ?? []),
    votes:       JSON.stringify(b.votes    ?? {}),
    created_at:  b.createdAt   ?? b.created_at  ?? now,
  })

  const idea = ideaFromRow(db.prepare('SELECT * FROM hub_ideas WHERE id = ?').get(id))
  broadcast('idea_created', { idea })
  res.status(201).json({ idea })
})

// ── PATCH /api/hub/ideas/:id ──────────────────────────────────────────────
router.patch('/ideas/:id', requireAuth, (req, res) => {
  const row = db.prepare('SELECT id FROM hub_ideas WHERE id = ?').get(req.params.id)
  if (!row) return res.status(404).json({ error: 'Idea not found' })

  const { sets, vals } = buildIdeaParams(req.body)
  if (sets.length) {
    db.prepare(`UPDATE hub_ideas SET ${sets.join(', ')} WHERE id = ?`).run(...vals, req.params.id)
  }

  const idea = ideaFromRow(db.prepare('SELECT * FROM hub_ideas WHERE id = ?').get(req.params.id))
  broadcast('idea_updated', { id: req.params.id, changes: req.body, idea })
  res.json({ idea })
})

// ── DELETE /api/hub/ideas/:id ─────────────────────────────────────────────
router.delete('/ideas/:id', requireAuth, (req, res) => {
  const info = db.prepare('DELETE FROM hub_ideas WHERE id = ?').run(req.params.id)
  if (!info.changes) return res.status(404).json({ error: 'Idea not found' })
  broadcast('idea_deleted', { id: req.params.id })
  res.json({ ok: true })
})

export default router
