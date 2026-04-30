import { Router } from 'express'
import db from '../db.js'
import { requireAuth } from '../auth.js'
import { broadcast } from '../socket.js'

const router = Router()

const VALID_GROUPS = ['founders', 'team', 'trial']

// ── helpers ───────────────────────────────────────────────────────────────
function getMemberTags(memberId) {
  return db.prepare(`
    SELECT mt.id, mt.name, mt.color
    FROM member_tag_links mtl
    JOIN member_tags mt ON mt.id = mtl.tag_id
    WHERE mtl.member_id = ?
    ORDER BY mt.name
  `).all(memberId)
}

function withTags(members) {
  const links = db.prepare(`
    SELECT mtl.member_id, mt.id, mt.name, mt.color
    FROM member_tag_links mtl
    JOIN member_tags mt ON mt.id = mtl.tag_id
  `).all()
  const map = {}
  for (const l of links) {
    if (!map[l.member_id]) map[l.member_id] = []
    map[l.member_id].push({ id: l.id, name: l.name, color: l.color })
  }
  return members.map(m => ({ ...m, tags: map[m.id] || [] }))
}

// ── GET /api/members  (public) ────────────────────────────────────────────
router.get('/', (_req, res) => {
  const members = db.prepare('SELECT * FROM members ORDER BY grp, position, id').all()
  res.json(withTags(members))
})

// ── GET /api/members/tags  (public) ──────────────────────────────────────
router.get('/tags', (_req, res) => {
  const tags = db.prepare('SELECT * FROM member_tags ORDER BY name').all()
  res.json(tags)
})

// ── POST /api/members/tags  (auth) ────────────────────────────────────────
router.post('/tags', requireAuth, (req, res) => {
  const { name, color } = req.body
  if (!name?.trim()) return res.status(400).json({ error: 'name requis' })
  try {
    const result = db.prepare(`
      INSERT INTO member_tags (name, color) VALUES (?, ?)
    `).run(name.trim(), color || '#888888')
    res.status(201).json(db.prepare('SELECT * FROM member_tags WHERE id = ?').get(result.lastInsertRowid))
  } catch {
    res.status(409).json({ error: 'Ce tag existe déjà' })
  }
})

// ── PUT /api/members/tags/:tagId  (auth) ─────────────────────────────────
router.put('/tags/:tagId', requireAuth, (req, res) => {
  const tag = db.prepare('SELECT * FROM member_tags WHERE id = ?').get(req.params.tagId)
  if (!tag) return res.status(404).json({ error: 'Tag introuvable' })
  const { name, color } = req.body
  try {
    db.prepare(`
      UPDATE member_tags SET
        name  = COALESCE(?, name),
        color = COALESCE(?, color)
      WHERE id = ?
    `).run(name?.trim() || null, color || null, req.params.tagId)
    res.json(db.prepare('SELECT * FROM member_tags WHERE id = ?').get(req.params.tagId))
  } catch {
    res.status(409).json({ error: 'Ce nom de tag existe déjà' })
  }
})

// ── DELETE /api/members/tags/:tagId  (auth) ───────────────────────────────
router.delete('/tags/:tagId', requireAuth, (req, res) => {
  const tag = db.prepare('SELECT * FROM member_tags WHERE id = ?').get(req.params.tagId)
  if (!tag) return res.status(404).json({ error: 'Tag introuvable' })
  db.prepare('DELETE FROM member_tags WHERE id = ?').run(req.params.tagId)
  res.json({ ok: true })
})

// ── POST /api/members  (auth) ─────────────────────────────────────────────
router.post('/', requireAuth, (req, res) => {
  const { name, role_fr, role_en, grp, position, img_key, steam_id64 } = req.body
  if (!name) return res.status(400).json({ error: 'name requis' })
  if (!VALID_GROUPS.includes(grp)) return res.status(400).json({ error: `grp invalide. Valeurs : ${VALID_GROUPS.join(', ')}` })

  const result = db.prepare(`
    INSERT INTO members (name, role_fr, role_en, grp, position, img_key, steam_id64)
    VALUES (?, ?, ?, ?, ?, ?, ?)
  `).run(name, role_fr ?? '', role_en ?? '', grp, position ?? 0, img_key ?? '', steam_id64 ?? '')

  const member = db.prepare('SELECT * FROM members WHERE id = ?').get(result.lastInsertRowid)
  broadcast('members_updated', {})
  res.status(201).json({ ...member, tags: [] })
})

// ── PUT /api/members/:id  (auth) ──────────────────────────────────────────
router.put('/:id', requireAuth, (req, res) => {
  const member = db.prepare('SELECT * FROM members WHERE id = ?').get(req.params.id)
  if (!member) return res.status(404).json({ error: 'Membre introuvable' })

  const { name, role_fr, role_en, grp, position, img_key, steam_id64 } = req.body
  if (grp && !VALID_GROUPS.includes(grp)) return res.status(400).json({ error: `grp invalide` })

  db.prepare(`
    UPDATE members SET
      name       = ?,
      role_fr    = ?,
      role_en    = ?,
      grp        = ?,
      position   = ?,
      img_key    = ?,
      steam_id64 = ?,
      updated_at = datetime('now')
    WHERE id = ?
  `).run(
    name       ?? member.name,
    role_fr    ?? member.role_fr,
    role_en    ?? member.role_en,
    grp        ?? member.grp,
    position   ?? member.position,
    img_key    ?? member.img_key,
    steam_id64 ?? member.steam_id64 ?? '',
    req.params.id
  )

  const updated = db.prepare('SELECT * FROM members WHERE id = ?').get(req.params.id)
  broadcast('members_updated', {})
  res.json({ ...updated, tags: getMemberTags(req.params.id) })
})

// ── POST /api/members/:id/tags/:tagId  (auth) ─────────────────────────────
router.post('/:id/tags/:tagId', requireAuth, (req, res) => {
  const member = db.prepare('SELECT id FROM members WHERE id = ?').get(req.params.id)
  if (!member) return res.status(404).json({ error: 'Membre introuvable' })
  const tag = db.prepare('SELECT id FROM member_tags WHERE id = ?').get(req.params.tagId)
  if (!tag) return res.status(404).json({ error: 'Tag introuvable' })

  db.prepare(`
    INSERT OR IGNORE INTO member_tag_links (member_id, tag_id) VALUES (?, ?)
  `).run(req.params.id, req.params.tagId)
  res.json({ ok: true })
})

// ── DELETE /api/members/:id/tags/:tagId  (auth) ───────────────────────────
router.delete('/:id/tags/:tagId', requireAuth, (req, res) => {
  db.prepare('DELETE FROM member_tag_links WHERE member_id = ? AND tag_id = ?')
    .run(req.params.id, req.params.tagId)
  res.json({ ok: true })
})

// ── DELETE /api/members/:id  (auth) ──────────────────────────────────────
router.delete('/:id', requireAuth, (req, res) => {
  const member = db.prepare('SELECT * FROM members WHERE id = ?').get(req.params.id)
  if (!member) return res.status(404).json({ error: 'Membre introuvable' })
  db.prepare('DELETE FROM members WHERE id = ?').run(req.params.id)
  broadcast('members_updated', {})
  res.json({ ok: true })
})

export default router
