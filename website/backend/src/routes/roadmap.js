// ============================================================
// /api/roadmap — items de roadmap publique
// ============================================================
// GET / (public)            : retourne les items is_public=1, ordres par position
// GET /admin (auth)         : retourne TOUS les items (public + brouillons)
// POST / (admin/owner)      : cree un item
// PUT /:id (admin/owner)    : update un item
// DELETE /:id (owner)       : supprime un item
// ============================================================

import { Router } from 'express'
import { requireAuth, requireRole } from '../auth.js'
import db from '../db.js'

const router = Router()

const VALID_STATUS = ['planned', 'in_progress', 'done', 'shipped']

function sanitize(item) {
  return {
    id:          item.id,
    title:       item.title,
    description: item.description,
    status:      item.status,
    is_public:   item.is_public === 1 || item.is_public === true,
    position:    item.position,
    created_at:  item.created_at,
    updated_at:  item.updated_at,
  }
}

// ── GET /api/roadmap (public) ───────────────────────────────────────────
router.get('/', (_req, res) => {
  const rows = db.prepare(`
    SELECT * FROM roadmap_items
    WHERE is_public = 1
    ORDER BY position ASC, id ASC
  `).all()
  res.json(rows.map(sanitize))
})

// ── GET /api/roadmap/admin (admin/owner) ────────────────────────────────
router.get('/admin', requireAuth, requireRole('admin'), (_req, res) => {
  const rows = db.prepare(`
    SELECT * FROM roadmap_items
    ORDER BY position ASC, id ASC
  `).all()
  res.json(rows.map(sanitize))
})

// ── POST /api/roadmap (admin/owner) ─────────────────────────────────────
router.post('/', requireAuth, requireRole('admin'), (req, res) => {
  const { title = '', description = '', status = 'planned', is_public = false, position = 0 } = req.body || {}

  if (!title.trim()) {
    return res.status(400).json({ error: 'title-required' })
  }
  if (!VALID_STATUS.includes(status)) {
    return res.status(400).json({ error: 'invalid-status', valid: VALID_STATUS })
  }

  const result = db.prepare(`
    INSERT INTO roadmap_items (title, description, status, is_public, position)
    VALUES (?, ?, ?, ?, ?)
  `).run(title.trim(), description, status, is_public ? 1 : 0, position)

  const created = db.prepare(`SELECT * FROM roadmap_items WHERE id = ?`).get(result.lastInsertRowid)
  res.status(201).json(sanitize(created))
})

// ── PUT /api/roadmap/:id (admin/owner) ──────────────────────────────────
router.put('/:id', requireAuth, requireRole('admin'), (req, res) => {
  const id = parseInt(req.params.id)
  const existing = db.prepare(`SELECT * FROM roadmap_items WHERE id = ?`).get(id)
  if (!existing) return res.status(404).json({ error: 'not-found' })

  const {
    title       = existing.title,
    description = existing.description,
    status      = existing.status,
    is_public   = !!existing.is_public,
    position    = existing.position,
  } = req.body || {}

  if (!title.trim()) {
    return res.status(400).json({ error: 'title-required' })
  }
  if (!VALID_STATUS.includes(status)) {
    return res.status(400).json({ error: 'invalid-status', valid: VALID_STATUS })
  }

  db.prepare(`
    UPDATE roadmap_items
    SET title = ?, description = ?, status = ?, is_public = ?, position = ?,
        updated_at = datetime('now')
    WHERE id = ?
  `).run(title.trim(), description, status, is_public ? 1 : 0, position, id)

  const updated = db.prepare(`SELECT * FROM roadmap_items WHERE id = ?`).get(id)
  res.json(sanitize(updated))
})

// ── DELETE /api/roadmap/:id (owner only) ────────────────────────────────
router.delete('/:id', requireAuth, requireRole('owner'), (req, res) => {
  const id = parseInt(req.params.id)
  const result = db.prepare(`DELETE FROM roadmap_items WHERE id = ?`).run(id)
  if (result.changes === 0) return res.status(404).json({ error: 'not-found' })
  res.json({ ok: true })
})

export default router
