import { Router } from 'express'
import db from '../db.js'
import { requireAuth } from '../auth.js'
import { broadcast } from '../socket.js'

const router = Router()

// ── Helpers ─────────────────────────────────────────────────────────────────

function allJobs(adminMode = false) {
  const where = adminMode ? '' : 'WHERE j.is_open = 1'
  return db.prepare(`
    SELECT j.*, g.label_fr as game_label_fr, g.label_en as game_label_en, g.color as game_color
    FROM jobs j
    LEFT JOIN games g ON g.slug = j.game_slug
    ${where}
    ORDER BY j.created_at DESC
  `).all()
}

// ── GET /api/jobs  (publique — offres ouvertes) ──────────────────────────────
router.get('/', (_req, res) => {
  res.json(allJobs(false))
})

// ── GET /api/jobs/admin  (toutes les offres) ─────────────────────────────────
router.get('/admin', (_req, res) => {
  res.json(allJobs(true))
})

// ── GET /api/jobs/:id ────────────────────────────────────────────────────────
router.get('/:id', (req, res) => {
  const job = db.prepare(`
    SELECT j.*, g.label_fr as game_label_fr, g.label_en as game_label_en, g.color as game_color
    FROM jobs j
    LEFT JOIN games g ON g.slug = j.game_slug
    WHERE j.id = ?
  `).get(Number(req.params.id))
  if (!job) return res.status(404).json({ error: 'Not found' })
  res.json(job)
})

// ── POST /api/jobs ───────────────────────────────────────────────────────────
router.post('/', requireAuth, (req, res) => {
  const { title_fr, title_en = '', description_fr = '', description_en = '',
          type = 'Bénévolat', game_slug = null, contact_email = '', is_open = 1 } = req.body

  if (!title_fr) return res.status(400).json({ error: 'title_fr requis' })

  const result = db.prepare(`
    INSERT INTO jobs (title_fr, title_en, description_fr, description_en, type, game_slug, contact_email, is_open)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?)
  `).run(title_fr, title_en, description_fr, description_en, type, game_slug || null, contact_email, is_open ? 1 : 0)

  broadcast('jobs_updated', {})
  res.status(201).json({ id: result.lastInsertRowid })
})

// ── PUT /api/jobs/:id ────────────────────────────────────────────────────────
router.put('/:id', requireAuth, (req, res) => {
  const id = Number(req.params.id)
  const existing = db.prepare('SELECT id FROM jobs WHERE id = ?').get(id)
  if (!existing) return res.status(404).json({ error: 'Not found' })

  const { title_fr, title_en, description_fr, description_en,
          type, game_slug, contact_email, is_open } = req.body

  db.prepare(`
    UPDATE jobs SET
      title_fr       = COALESCE(?, title_fr),
      title_en       = COALESCE(?, title_en),
      description_fr = COALESCE(?, description_fr),
      description_en = COALESCE(?, description_en),
      type           = COALESCE(?, type),
      game_slug      = ?,
      contact_email  = COALESCE(?, contact_email),
      is_open        = COALESCE(?, is_open),
      updated_at     = datetime('now')
    WHERE id = ?
  `).run(
    title_fr ?? null, title_en ?? null,
    description_fr ?? null, description_en ?? null,
    type ?? null,
    game_slug !== undefined ? (game_slug || null) : db.prepare('SELECT game_slug FROM jobs WHERE id = ?').get(id).game_slug,
    contact_email ?? null,
    is_open !== undefined ? (is_open ? 1 : 0) : null,
    id
  )

  broadcast('jobs_updated', {})
  res.json({ ok: true })
})

// ── POST /api/jobs/:id/toggle ────────────────────────────────────────────────
router.post('/:id/toggle', requireAuth, (req, res) => {
  const id = Number(req.params.id)
  const job = db.prepare('SELECT is_open FROM jobs WHERE id = ?').get(id)
  if (!job) return res.status(404).json({ error: 'Not found' })
  db.prepare('UPDATE jobs SET is_open = ?, updated_at = datetime(\'now\') WHERE id = ?')
    .run(job.is_open ? 0 : 1, id)
  broadcast('jobs_updated', {})
  res.json({ ok: true, is_open: !job.is_open })
})

// ── DELETE /api/jobs/:id ─────────────────────────────────────────────────────
router.delete('/:id', requireAuth, (req, res) => {
  db.prepare('DELETE FROM jobs WHERE id = ?').run(Number(req.params.id))
  broadcast('jobs_updated', {})
  res.json({ ok: true })
})

export default router
