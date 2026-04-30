import passport from 'passport'
import { Strategy as SteamStrategy } from 'passport-steam'
import { createHash } from 'crypto'
import db from './db.js'

// ── Rôles hiérarchiques ────────────────────────────────────────────────────
// rules_editor : peut éditer les livres de règles OpenFramework, niveau entre editor et viewer
const ROLE_HIERARCHY = { owner: 4, admin: 3, editor: 2, rules_editor: 1, viewer: 1 }

// ── SteamID autorisés : toute personne présente dans la table users ────────
function isAllowedSteamId(steamId) {
  const row = db.prepare('SELECT role FROM users WHERE steam_id = ?').get(steamId)
  return !!row
}

function getUserFromDb(steamId) {
  return db.prepare('SELECT * FROM users WHERE steam_id = ?').get(steamId)
}

// ── Serialization ──────────────────────────────────────────────────────────
passport.serializeUser((user, done) => {
  done(null, user)
})

passport.deserializeUser((obj, done) => {
  // Re-fetch role from DB on each request so role changes take effect
  const dbUser = getUserFromDb(obj.steamId)
  if (!dbUser) return done(null, false)
  done(null, { ...obj, role: dbUser.role })
})

// ── Steam Strategy ─────────────────────────────────────────────────────────
passport.use(new SteamStrategy(
  {
    returnURL: `${process.env.BACKEND_URL || 'http://localhost:3001'}/auth/steam/return`,
    realm:     `${process.env.BACKEND_URL || 'http://localhost:3001'}/`,
    apiKey:    process.env.STEAM_API_KEY || 'NO_KEY',
  },
  (_identifier, profile, done) => {
    const steamId     = profile.id
    const displayName = profile.displayName
    const avatar      = profile.photos?.[2]?.value || profile.photos?.[0]?.value || null

    if (!isAllowedSteamId(steamId)) {
      return done(null, false, { message: 'SteamID non autorisé' })
    }

    // Upsert display name & avatar dans users
    db.prepare(`
      INSERT INTO users (steam_id, display_name, avatar)
      VALUES (?, ?, ?)
      ON CONFLICT(steam_id) DO UPDATE SET
        display_name = excluded.display_name,
        avatar       = excluded.avatar,
        updated_at   = datetime('now')
    `).run(steamId, displayName, avatar)

    // Auto-link : si un membre a le même nom (Steam displayName) et pas encore de steam_id64,
    // on le lie automatiquement — plus besoin de saisie manuelle
    const unlinkedMember = db.prepare(`
      SELECT id FROM members
      WHERE LOWER(name) = LOWER(?)
        AND (steam_id64 IS NULL OR steam_id64 = '')
    `).get(displayName)
    if (unlinkedMember) {
      db.prepare(`UPDATE members SET steam_id64 = ?, updated_at = datetime('now') WHERE id = ?`)
        .run(steamId, unlinkedMember.id)
    }

    const dbUser = getUserFromDb(steamId)
    return done(null, { steamId, displayName, avatar, role: dbUser.role })
  }
))

export default passport

// ── Auth par token Bearer (pour Claude / scripts / MCP) ───────────────────
export function hashToken(token) {
  return createHash('sha256').update(token).digest('hex')
}

// Si Authorization: Bearer xxx présent et valide, peuple req.user comme passport
function tryBearerAuth(req) {
  if (req.isAuthenticated && req.isAuthenticated()) return true
  const header = req.headers.authorization || ''
  if (!header.startsWith('Bearer ')) return false
  const token = header.slice(7).trim()
  if (!token) return false
  const row = db.prepare(`
    SELECT t.id AS token_id, u.steam_id, u.display_name, u.avatar, u.role
    FROM api_tokens t
    JOIN users u ON u.id = t.user_id
    WHERE t.token_hash = ?
  `).get(hashToken(token))
  if (!row) return false
  db.prepare(`UPDATE api_tokens SET last_used_at = datetime('now') WHERE id = ?`).run(row.token_id)
  req.user = {
    steamId:     row.steam_id,
    displayName: row.display_name,
    avatar:      row.avatar,
    role:        row.role,
  }
  req.authVia = 'bearer'
  return true
}

// ── Middleware : vérifie que l'utilisateur est connecté ───────────────────
export function requireAuth(req, res, next) {
  if (tryBearerAuth(req)) return next()
  res.status(401).json({ error: 'Non authentifié' })
}

// ── Middleware : vérifie le rôle minimum requis ────────────────────────────
// Usage : requireRole('admin')  → autorise admin + owner
export function requireRole(minRole) {
  return (req, res, next) => {
    if (!tryBearerAuth(req)) {
      return res.status(401).json({ error: 'Non authentifié' })
    }
    const userLevel = ROLE_HIERARCHY[req.user?.role] ?? 0
    const minLevel  = ROLE_HIERARCHY[minRole] ?? 99
    if (userLevel < minLevel) {
      return res.status(403).json({ error: `Rôle requis : ${minRole}` })
    }
    next()
  }
}

// ── Middleware : vérifie que l'utilisateur peut éditer les règles ──────────
// Autorise : owner, admin, editor, rules_editor
export function requireRulesAccess(req, res, next) {
  if (!tryBearerAuth(req)) {
    return res.status(401).json({ error: 'Non authentifié' })
  }
  const role = req.user?.role
  if (!['owner', 'admin', 'editor', 'rules_editor'].includes(role)) {
    return res.status(403).json({ error: 'Accès refusé : rôle rules_editor requis' })
  }
  next()
}

// ── Middleware : injecte req.member depuis la session ───────────────────────
// Résout le membre lié à l'utilisateur connecté (par steam_id64 puis par name)
// Place le résultat dans req.member (null si pas trouvé ou non connecté)
export function attachMember(req, _res, next) {
  req.member = null
  if (req.isAuthenticated && req.isAuthenticated() && req.user) {
    const u = req.user
    req.member =
      db.prepare('SELECT * FROM members WHERE steam_id64 = ?').get(u.steamId) ||
      db.prepare('SELECT * FROM members WHERE LOWER(name) = LOWER(?)').get(u.displayName) ||
      null
  }
  next()
}
