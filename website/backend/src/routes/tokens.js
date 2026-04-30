import { Router } from 'express'
import { randomBytes } from 'crypto'
import db from '../db.js'
import { requireRole, hashToken } from '../auth.js'

const router = Router()

// ── GET /api/tokens — liste les tokens (sans la valeur) ───────────────────
router.get('/', requireRole('owner'), (_req, res) => {
  const rows = db.prepare(`
    SELECT t.id, t.name, t.last_used_at, t.created_at,
           u.steam_id, u.display_name, u.role
    FROM api_tokens t
    JOIN users u ON u.id = t.user_id
    ORDER BY t.id DESC
  `).all()
  res.json(rows)
})

// ── POST /api/tokens — crée un token (lié à l'utilisateur courant) ────────
// body: { name?: string, userId?: number }   userId optionnel : owner peut créer pour un autre user
router.post('/', requireRole('owner'), (req, res) => {
  const { name, userId } = req.body || {}

  let targetUserId = userId
  if (!targetUserId) {
    const me = db.prepare('SELECT id FROM users WHERE steam_id = ?').get(req.user.steamId)
    if (!me) return res.status(400).json({ error: 'Utilisateur courant introuvable' })
    targetUserId = me.id
  } else {
    const exists = db.prepare('SELECT 1 FROM users WHERE id = ?').get(targetUserId)
    if (!exists) return res.status(404).json({ error: 'userId inconnu' })
  }

  const token = `sb_${randomBytes(32).toString('hex')}`
  const result = db.prepare(`
    INSERT INTO api_tokens (token_hash, name, user_id)
    VALUES (?, ?, ?)
  `).run(hashToken(token), (name || '').trim(), targetUserId)

  res.status(201).json({
    id: result.lastInsertRowid,
    name: name || '',
    token,
    warning: 'Cette valeur ne sera plus jamais affichée. Copie-la maintenant.',
  })
})

// ── DELETE /api/tokens/:id — révoque un token ─────────────────────────────
router.delete('/:id', requireRole('owner'), (req, res) => {
  const info = db.prepare('DELETE FROM api_tokens WHERE id = ?').run(Number(req.params.id))
  if (!info.changes) return res.status(404).json({ error: 'Token introuvable' })
  res.json({ ok: true })
})

export default router
