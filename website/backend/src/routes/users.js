import { Router } from 'express'
import db from '../db.js'
import { requireAuth, requireRole } from '../auth.js'
import { broadcast } from '../socket.js'

const router = Router()

// rules_editor : rôle spécial permettant d'éditer les règles OpenFramework sans accès admin complet
const VALID_ROLES = ['owner', 'admin', 'editor', 'rules_editor', 'viewer']

function slugify(name) {
  return (name || '').toLowerCase().replace(/\s+/g, '_')
}

function migrateHubId(oldId, newId) {
  if (!oldId || !newId || oldId === newId) return

  // 1. hub_tasks assignees (JSON array)
  const tasks = db.prepare("SELECT id, assignees FROM hub_tasks").all()
  const updateTask = db.prepare("UPDATE hub_tasks SET assignees = ? WHERE id = ?")
  for (const task of tasks) {
    const arr = JSON.parse(task.assignees)
    if (!arr.includes(oldId)) continue
    updateTask.run(JSON.stringify(arr.map(a => a === oldId ? newId : a)), task.id)
  }

  // 2. hub_activity author
  db.prepare("UPDATE hub_activity SET author = ? WHERE author = ?").run(newId, oldId)

  // 3. hub_ideas votes + comments
  const ideas = db.prepare("SELECT id, votes, comments FROM hub_ideas").all()
  const updateIdea = db.prepare("UPDATE hub_ideas SET votes = ?, comments = ? WHERE id = ?")
  for (const idea of ideas) {
    const votes = JSON.parse(idea.votes)
    const comments = JSON.parse(idea.comments)
    let changed = false
    if (oldId in votes) {
      votes[newId] = (votes[newId] || 0) + votes[oldId]
      delete votes[oldId]
      changed = true
    }
    const updComments = comments.map(c => {
      if (c.author === oldId) { changed = true; return { ...c, author: newId } }
      return c
    })
    if (changed) updateIdea.run(JSON.stringify(votes), JSON.stringify(updComments), idea.id)
  }

  // 4. misc blob : mapAnnotations (item.author + comment.author)
  const miscRow = db.prepare("SELECT value FROM hub_state WHERE key = 'misc'").get()
  if (miscRow) {
    const misc = JSON.parse(miscRow.value)
    let changed = false
    if (misc.mapAnnotations) {
      for (const proj of Object.values(misc.mapAnnotations)) {
        if (!Array.isArray(proj.items)) continue
        for (const item of proj.items) {
          if (item.author === oldId) { item.author = newId; changed = true }
          if (Array.isArray(item.comments)) {
            for (const c of item.comments) {
              if (c.author === oldId) { c.author = newId; changed = true }
            }
          }
        }
      }
    }
    if (changed) {
      db.prepare("UPDATE hub_state SET value = ?, updated_at = datetime('now') WHERE key = 'misc'").run(JSON.stringify(misc))
    }
  }
}

// ── GET /api/users  (tout utilisateur connecté) — lecture seule, nécessaire pour le dropdown d'assignation ──
router.get('/', requireAuth, (_req, res) => {
  const users = db.prepare('SELECT id, steam_id, display_name, avatar, role, created_at, updated_at FROM users ORDER BY id').all()
  res.json(users)
})

// ── POST /api/users  (admin+ — owner peut tout, admin ne peut créer que editor/viewer) ──
router.post('/', requireRole('admin'), (req, res) => {
  const { steam_id, display_name, role } = req.body
  if (!steam_id) return res.status(400).json({ error: 'steam_id requis' })
  if (!VALID_ROLES.includes(role)) return res.status(400).json({ error: `Rôle invalide. Valeurs : ${VALID_ROLES.join(', ')}` })

  const callerRole = req.user.role
  if (callerRole === 'admin' && (role === 'owner' || role === 'admin')) {
    return res.status(403).json({ error: 'Un admin ne peut créer que des editor ou viewer' })
  }

  try {
    const result = db.prepare(`
      INSERT INTO users (steam_id, display_name, role)
      VALUES (?, ?, ?)
    `).run(steam_id.trim(), display_name || '', role)
    const user = db.prepare('SELECT * FROM users WHERE id = ?').get(result.lastInsertRowid)
    broadcast('users_updated', {})
    res.status(201).json(user)
  } catch (e) {
    res.status(409).json({ error: 'Ce SteamID existe déjà' })
  }
})

// ── PUT /api/users/:id  (admin+ pour editor/viewer, owner pour tout) ───────
router.put('/:id', requireRole('admin'), (req, res) => {
  const { role, display_name } = req.body
  const target = db.prepare('SELECT * FROM users WHERE id = ?').get(req.params.id)
  if (!target) return res.status(404).json({ error: 'Utilisateur introuvable' })

  const callerRole = req.user.role
  const isSelf = req.user.steamId === target.steam_id

  // Auto-édition : un admin peut modifier son propre display_name, mais pas son rôle
  if (isSelf) {
    if (role && role !== target.role) {
      return res.status(403).json({ error: 'Vous ne pouvez pas modifier votre propre rôle' })
    }
  } else {
    // Un admin ne peut modifier que les editor/viewer, pas les owner/admin
    if (callerRole === 'admin' && (target.role === 'owner' || target.role === 'admin')) {
      return res.status(403).json({ error: 'Un admin ne peut pas modifier un owner ou un autre admin' })
    }
    // Empêcher de modifier un autre owner (seul l'owner peut se modifier lui-même)
    if (target.role === 'owner') {
      return res.status(403).json({ error: 'Impossible de modifier un autre owner' })
    }
  }
  // Un admin ne peut pas élever un rôle vers admin ou owner
  if (callerRole === 'admin' && role && (role === 'owner' || role === 'admin')) {
    return res.status(403).json({ error: 'Un admin ne peut pas attribuer le rôle owner ou admin' })
  }
  if (role && !VALID_ROLES.includes(role)) {
    return res.status(400).json({ error: `Rôle invalide. Valeurs : ${VALID_ROLES.join(', ')}` })
  }

  // Si le display_name change, migrer les hub IDs dans toutes les données
  if (display_name && display_name !== target.display_name) {
    const oldHubId = slugify(target.display_name)
    const newHubId = slugify(display_name)
    migrateHubId(oldHubId, newHubId)
  }

  db.prepare(`
    UPDATE users SET
      role         = COALESCE(?, role),
      display_name = COALESCE(?, display_name),
      updated_at   = datetime('now')
    WHERE id = ?
  `).run(role || null, display_name || null, req.params.id)

  const updated = db.prepare('SELECT id, steam_id, display_name, avatar, role, created_at, updated_at FROM users WHERE id = ?').get(req.params.id)
  broadcast('users_updated', {})
  res.json(updated)
})

// ── POST /api/users/migrate-hub-id  (admin+ — réparer les assignations après un changement de nom) ──
router.post('/migrate-hub-id', requireRole('admin'), (req, res) => {
  const { oldId, newId } = req.body
  if (!oldId || !newId) return res.status(400).json({ error: 'oldId et newId requis' })
  migrateHubId(oldId, newId)
  broadcast('hub_updated', {})
  broadcast('users_updated', {})
  res.json({ ok: true, migrated: { from: oldId, to: newId } })
})

// ── DELETE /api/users/:id  (owner seulement) ─────────────────────────────
router.delete('/:id', requireRole('owner'), (req, res) => {
  const target = db.prepare('SELECT * FROM users WHERE id = ?').get(req.params.id)
  if (!target) return res.status(404).json({ error: 'Utilisateur introuvable' })
  if (target.role === 'owner') return res.status(403).json({ error: 'Impossible de supprimer un owner' })

  db.prepare('DELETE FROM users WHERE id = ?').run(req.params.id)
  broadcast('users_updated', {})
  res.json({ ok: true })
})

export default router
