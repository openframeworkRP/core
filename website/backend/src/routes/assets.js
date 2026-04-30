import { Router } from 'express'
import db from '../db.js'
import { requireAuth, requireRole } from '../auth.js'

const router = Router()

function newId() {
  return `ast_${Date.now()}_${Math.random().toString(36).slice(2, 7)}`
}

// ── POST /api/assets/bulk-import  (editor+) ──────────────────────────────
router.post('/bulk-import', requireRole('editor'), (req, res) => {
  const { assets } = req.body
  if (!Array.isArray(assets) || assets.length === 0) return res.status(400).json({ error: 'assets requis' })

  const insert = db.prepare(`
    INSERT OR IGNORE INTO asset_catalogue (id, name, vendor, description, store_url, download_url, price, tags, thumbnail)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
  `)
  let created = 0
  const rows = []
  for (const a of assets) {
    if (!a.name?.trim()) continue
    const id = newId()
    insert.run(
      id, a.name.trim(), a.vendor?.trim() || '',
      a.description?.trim() || '', a.store_url?.trim() || '',
      a.download_url?.trim() || '', a.price?.trim() || '',
      JSON.stringify(Array.isArray(a.tags) ? a.tags : []),
      a.thumbnail?.trim() || ''
    )
    const row = db.prepare('SELECT * FROM asset_catalogue WHERE id = ?').get(id)
    if (row) { rows.push({ ...row, tags: JSON.parse(row.tags) }); created++ }
  }
  res.status(201).json({ created, assets: rows })
})

// ── GET /api/assets  (auth) ───────────────────────────────────────────────
router.get('/', requireAuth, (_req, res) => {
  const rows = db.prepare('SELECT * FROM asset_catalogue ORDER BY created_at DESC').all()
  res.json(rows.map(r => ({ ...r, tags: JSON.parse(r.tags || '[]') })))
})

// ── POST /api/assets  (editor+) ──────────────────────────────────────────
router.post('/', requireRole('editor'), (req, res) => {
  const { name, vendor, description, store_url, download_url, price, tags, thumbnail } = req.body
  if (!name?.trim()) return res.status(400).json({ error: 'name requis' })

  const id = newId()
  db.prepare(`
    INSERT INTO asset_catalogue (id, name, vendor, description, store_url, download_url, price, tags, thumbnail)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
  `).run(
    id,
    name.trim(),
    vendor?.trim() || '',
    description?.trim() || '',
    store_url?.trim() || '',
    download_url?.trim() || '',
    price?.trim() || '',
    JSON.stringify(Array.isArray(tags) ? tags : []),
    thumbnail?.trim() || '',
  )

  const row = db.prepare('SELECT * FROM asset_catalogue WHERE id = ?').get(id)
  res.status(201).json({ ...row, tags: JSON.parse(row.tags) })
})

// ── PUT /api/assets/:id  (editor+) ───────────────────────────────────────
router.put('/:id', requireRole('editor'), (req, res) => {
  const row = db.prepare('SELECT * FROM asset_catalogue WHERE id = ?').get(req.params.id)
  if (!row) return res.status(404).json({ error: 'Asset introuvable' })

  const { name, vendor, description, store_url, download_url, price, tags, thumbnail } = req.body

  db.prepare(`
    UPDATE asset_catalogue SET
      name         = COALESCE(?, name),
      vendor       = COALESCE(?, vendor),
      description  = COALESCE(?, description),
      store_url    = COALESCE(?, store_url),
      download_url = COALESCE(?, download_url),
      price        = COALESCE(?, price),
      tags         = COALESCE(?, tags),
      thumbnail    = COALESCE(?, thumbnail),
      updated_at   = datetime('now')
    WHERE id = ?
  `).run(
    name?.trim() ?? null,
    vendor?.trim() ?? null,
    description?.trim() ?? null,
    store_url?.trim() ?? null,
    download_url?.trim() ?? null,
    price?.trim() ?? null,
    tags !== undefined ? JSON.stringify(Array.isArray(tags) ? tags : []) : null,
    thumbnail?.trim() ?? null,
    req.params.id,
  )

  const updated = db.prepare('SELECT * FROM asset_catalogue WHERE id = ?').get(req.params.id)
  res.json({ ...updated, tags: JSON.parse(updated.tags) })
})

// ── DELETE /api/assets/:id  (editor+) ────────────────────────────────────
router.delete('/:id', requireRole('editor'), (req, res) => {
  const row = db.prepare('SELECT * FROM asset_catalogue WHERE id = ?').get(req.params.id)
  if (!row) return res.status(404).json({ error: 'Asset introuvable' })
  db.prepare('DELETE FROM asset_catalogue WHERE id = ?').run(req.params.id)
  res.json({ ok: true })
})

export default router
