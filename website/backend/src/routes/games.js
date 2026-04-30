import express from 'express'
import db from '../db.js'
import slugify from 'slugify'
import { requireAuth } from '../auth.js'
import { broadcast } from '../socket.js'

const router = express.Router()

// ── GET /games ────────────────────────────────────────────────────────────
router.get('/', (_req, res) => {
  const games = db.prepare('SELECT * FROM games ORDER BY id').all()
  res.json(games)
})

// ── POST /games ───────────────────────────────────────────────────────────
router.post('/', requireAuth, (req, res) => {
  const { label_fr, label_en, color } = req.body
  if (!label_fr || !label_en) return res.status(400).json({ error: 'label_fr and label_en are required' })

  const slug = slugify(label_en, { lower: true, strict: true })
  try {
    const result = db.prepare(`
      INSERT INTO games (slug, label_fr, label_en, color) VALUES (?, ?, ?, ?)
    `).run(slug, label_fr, label_en, color ?? null)
    const game = db.prepare('SELECT * FROM games WHERE id = ?').get(result.lastInsertRowid)
    broadcast('games_updated', {})
    res.status(201).json(game)
  } catch (e) {
    res.status(409).json({ error: 'Slug already exists' })
  }
})

// ── PUT /games/:id ────────────────────────────────────────────────────────
router.put('/:id', requireAuth, (req, res) => {
  const { label_fr, label_en, color } = req.body
  db.prepare(`
    UPDATE games SET label_fr = ?, label_en = ?, color = ? WHERE id = ?
  `).run(label_fr, label_en, color ?? null, req.params.id)
  const game = db.prepare('SELECT * FROM games WHERE id = ?').get(req.params.id)
  if (!game) return res.status(404).json({ error: 'Not found' })
  broadcast('games_updated', {})
  res.json(game)
})

// ── DELETE /games/:id ─────────────────────────────────────────────────────
router.delete('/:id', requireAuth, (req, res) => {
  db.prepare('DELETE FROM games WHERE id = ?').run(req.params.id)
  broadcast('games_updated', {})
  res.json({ ok: true })
})

export default router
