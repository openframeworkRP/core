import { Router } from 'express'
import db from '../db.js'
import { requireRole } from '../auth.js'

const router = Router()

const VALID_STATUS = ['pending', 'confirmed', 'patched', 'wontfix']

// ── Helpers ───────────────────────────────────────────────────────────────────
function withComments(bug) {
  const comments = db.prepare(
    'SELECT * FROM bug_comments WHERE bug_id = ? ORDER BY created_at ASC'
  ).all(bug.id)
  return { ...bug, comments }
}

// ── GET /api/bugs?game=slug  — bugs publics (visiteurs) ──────────────────────
router.get('/', (req, res) => {
  const { game } = req.query
  const bugs = db.prepare(`
    SELECT * FROM bug_reports
    WHERE is_public = 1 ${game ? 'AND game_slug = ?' : ''}
    ORDER BY created_at DESC
  `).all(...(game ? [game] : []))

  res.json(bugs.map(withComments))
})

// ── GET /api/bugs/admin?game=slug  — tous les bugs (auth admin+) ─────────────
router.get('/admin', requireRole('editor'), (req, res) => {
  const { game } = req.query
  const bugs = db.prepare(`
    SELECT * FROM bug_reports
    ${game ? 'WHERE game_slug = ?' : ''}
    ORDER BY
      CASE status WHEN 'pending' THEN 0 WHEN 'confirmed' THEN 1 ELSE 2 END,
      created_at DESC
  `).all(...(game ? [game] : []))

  res.json(bugs.map(withComments))
})

// ── POST /api/bugs  — soumettre un signalement (public, rate-limited par IP) ──
router.post('/', (req, res) => {
  const { game_slug, title, description } = req.body
  if (!game_slug) return res.status(400).json({ error: 'game_slug requis' })
  if (!title?.trim()) return res.status(400).json({ error: 'title requis' })

  // Anti-spam simple : max 3 signalements par IP par heure
  const ip = req.ip || req.socket?.remoteAddress || 'unknown'
  const count = db.prepare(`
    SELECT COUNT(*) as c FROM bug_reports
    WHERE reporter_ip = ? AND created_at > datetime('now', '-1 hour')
  `).get(ip).c
  if (count >= 3) {
    return res.status(429).json({ error: 'Trop de signalements. Réessaie dans une heure.' })
  }

  // Dédoublonnage doux : avertit si titre très similaire existe déjà (pending/confirmed)
  const similar = db.prepare(`
    SELECT id FROM bug_reports
    WHERE game_slug = ? AND status IN ('pending','confirmed')
      AND lower(title) = lower(?)
    LIMIT 1
  `).get(game_slug, title.trim())

  const result = db.prepare(`
    INSERT INTO bug_reports (game_slug, title, description, reporter_ip)
    VALUES (?, ?, ?, ?)
  `).run(game_slug, title.trim(), description?.trim() || '', ip)

  res.status(201).json({
    id: result.lastInsertRowid,
    duplicate_warning: !!similar,
  })
})

// ── PATCH /api/bugs/:id  — modifier status / is_public (editor+) ─────────────
router.patch('/:id', requireRole('editor'), (req, res) => {
  const { status, is_public } = req.body
  const bug = db.prepare('SELECT * FROM bug_reports WHERE id = ?').get(Number(req.params.id))
  if (!bug) return res.status(404).json({ error: 'Bug introuvable' })

  if (status && !VALID_STATUS.includes(status)) {
    return res.status(400).json({ error: `Status invalide : ${VALID_STATUS.join(', ')}` })
  }

  db.prepare(`
    UPDATE bug_reports SET
      status     = COALESCE(?, status),
      is_public  = COALESCE(?, is_public),
      updated_at = datetime('now')
    WHERE id = ?
  `).run(status ?? null, is_public != null ? (is_public ? 1 : 0) : null, bug.id)

  res.json(withComments(db.prepare('SELECT * FROM bug_reports WHERE id = ?').get(bug.id)))
})

// ── DELETE /api/bugs/:id  — supprimer (admin+) ───────────────────────────────
router.delete('/:id', requireRole('admin'), (req, res) => {
  db.prepare('DELETE FROM bug_reports WHERE id = ?').run(Number(req.params.id))
  res.json({ ok: true })
})

// ── POST /api/bugs/:id/comments  — ajouter un commentaire (editor+) ──────────
router.post('/:id/comments', requireRole('editor'), (req, res) => {
  const { content, author } = req.body
  const bug = db.prepare('SELECT id FROM bug_reports WHERE id = ?').get(Number(req.params.id))
  if (!bug) return res.status(404).json({ error: 'Bug introuvable' })
  if (!content?.trim()) return res.status(400).json({ error: 'content requis' })

  const result = db.prepare(`
    INSERT INTO bug_comments (bug_id, author, content)
    VALUES (?, ?, ?)
  `).run(bug.id, author?.trim() || 'Team', content.trim())

  res.status(201).json(db.prepare('SELECT * FROM bug_comments WHERE id = ?').get(result.lastInsertRowid))
})

// ── DELETE /api/bugs/:id/comments/:cid  — supprimer commentaire (admin+) ─────
router.delete('/:id/comments/:cid', requireRole('admin'), (req, res) => {
  db.prepare('DELETE FROM bug_comments WHERE id = ? AND bug_id = ?')
    .run(Number(req.params.cid), Number(req.params.id))
  res.json({ ok: true })
})

export default router
