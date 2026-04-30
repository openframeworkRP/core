import { Router } from 'express'
import passport, { attachMember } from '../auth.js'
import { getRolePermissions } from '../permissions.js'
import db from '../db.js'

const router = Router()
const IS_DEV = process.env.NODE_ENV !== 'production'

// ── [DEV ONLY] Login direct sans Steam ───────────────────────────────────
if (IS_DEV) {
  router.get('/dev-login', (req, res) => {
    // Prend le premier owner/admin en base, ou le premier user disponible
    const devUser = db.prepare(`
      SELECT * FROM users ORDER BY
        CASE role WHEN 'owner' THEN 0 WHEN 'admin' THEN 1 ELSE 2 END
      LIMIT 1
    `).get()

    if (!devUser) {
      return res.status(404).json({ error: 'Aucun utilisateur en base. Ajoute-en un d\'abord.' })
    }

    const user = {
      steamId:     devUser.steam_id,
      displayName: devUser.display_name || 'Dev User',
      avatar:      devUser.avatar || null,
      role:        devUser.role,
    }

    req.login(user, err => {
      if (err) return res.status(500).json({ error: err.message })
      res.redirect(`${process.env.FRONTEND_URL || 'http://localhost:5173'}/admin`)
    })
  })
}

// ── Déclenche l'auth Steam (redirige vers Steam) ──────────────────────────
router.get('/steam', passport.authenticate('steam', { failureRedirect: '/' }))

// ── Callback Steam après login ────────────────────────────────────────────
router.get('/steam/return',
  passport.authenticate('steam', { failureRedirect: `${process.env.FRONTEND_URL || 'http://localhost:5173'}/admin/login?error=unauthorized` }),
  (_req, res) => {
    res.redirect(`${process.env.FRONTEND_URL || 'http://localhost:5173'}/admin`)
  }
)

// ── Déconnexion ───────────────────────────────────────────────────────────
router.post('/logout', (req, res) => {
  req.logout(() => {
    req.session.destroy(() => {
      res.json({ ok: true })
    })
  })
})

// ── Vérifie si connecté ─────────────────────────────────────────────────
router.get('/me', attachMember, (req, res) => {
  if (req.isAuthenticated && req.isAuthenticated()) {
    const permissions = req.user?.role ? getRolePermissions(req.user.role) : {}
    res.json({ authenticated: true, user: req.user, member: req.member, permissions })
  } else {
    res.json({ authenticated: false })
  }
})

export default router
