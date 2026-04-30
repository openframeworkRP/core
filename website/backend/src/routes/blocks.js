import express from 'express'
import db from '../db.js'
import { requireAuth } from '../auth.js'

const router = express.Router({ mergeParams: true })   // accès à :postId

// ── GET /posts/:postId/blocks ─────────────────────────────────────────────
router.get('/', (req, res) => {
  const { postId } = req.params
  const blocksFr = db.prepare(`SELECT * FROM blocks WHERE post_id = ? AND lang = 'fr' ORDER BY position`).all(postId)
  const blocksEn = db.prepare(`SELECT * FROM blocks WHERE post_id = ? AND lang = 'en' ORDER BY position`).all(postId)
  const parse = b => ({ ...b, data: JSON.parse(b.data) })
  res.json({ fr: blocksFr.map(parse), en: blocksEn.map(parse) })
})

// ── POST /posts/:postId/blocks ────────────────────────────────────────────────────
// body: { lang, type, game_slug, position, data, author }
router.post('/', requireAuth, (req, res) => {
  const { postId } = req.params
  const { lang, type, game_slug = null, position = 0, data = {}, author = null } = req.body
  if (!lang || !type) return res.status(400).json({ error: 'lang and type are required' })

  const result = db.prepare(`
    INSERT INTO blocks (post_id, lang, type, game_slug, position, author, data)
    VALUES (?, ?, ?, ?, ?, ?, ?)
  `).run(postId, lang, type, game_slug, position, author, JSON.stringify(data))

  const block = db.prepare('SELECT * FROM blocks WHERE id = ?').get(result.lastInsertRowid)
  res.status(201).json({ ...block, data: JSON.parse(block.data) })
})

// ── PUT /posts/:postId/blocks/reorder ────────────────────────────────────
// body: { fr: [{id, position}, ...], en: [{id, position}, ...] }
router.put('/reorder', requireAuth, (req, res) => {
  const { fr = [], en = [] } = req.body
  const update = db.prepare('UPDATE blocks SET position = ? WHERE id = ?')
  const tx = db.transaction(() => {
    for (const { id, position } of [...fr, ...en]) update.run(position, id)
  })
  tx()
  res.json({ ok: true })
})

// ── PUT /posts/:postId/blocks/:blockId ────────────────────────────────────
router.put('/:blockId', requireAuth, (req, res) => {
  const { blockId } = req.params
  const existing = db.prepare('SELECT * FROM blocks WHERE id = ?').get(blockId)
  if (!existing) return res.status(404).json({ error: 'Not found' })

  const { type, game_slug, position, data, author } = req.body
  db.prepare(`
    UPDATE blocks SET
      type = ?, game_slug = ?, position = ?, author = ?, data = ?
    WHERE id = ?
  `).run(
    type ?? existing.type,
    game_slug !== undefined ? game_slug : existing.game_slug,
    position ?? existing.position,
    author !== undefined ? author : existing.author,
    data !== undefined ? JSON.stringify(data) : existing.data,
    blockId
  )

  const block = db.prepare('SELECT * FROM blocks WHERE id = ?').get(blockId)
  res.json({ ...block, data: JSON.parse(block.data) })
})

// ── DELETE /posts/:postId/blocks/:blockId ─────────────────────────────────
router.delete('/:blockId', requireAuth, (req, res) => {
  db.prepare('DELETE FROM blocks WHERE id = ?').run(req.params.blockId)
  res.json({ ok: true })
})

export default router
