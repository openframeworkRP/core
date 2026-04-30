// ── Routes /api/permissions ────────────────────────────────────────────────
// Gestion des rôles, pages et de la matrice de permissions.
// La page admin:permissions est strictement réservée à l'owner (ou à tout
// autre rôle qui aurait reçu la permission via la matrice).

import { Router } from 'express'
import db from '../db.js'
import { requireAuth } from '../auth.js'
import { requirePagePermission, getRolePermissions, PAGES } from '../permissions.js'
import { broadcast } from '../socket.js'

const router = Router()

// ── Permissions du user connecté (utilisé par AuthContext) ────────────────
router.get('/me', requireAuth, (req, res) => {
  const role = req.user?.role
  if (!role) return res.json({ role: null, permissions: {} })
  res.json({ role, permissions: getRolePermissions(role) })
})

// ── Liste des rôles ───────────────────────────────────────────────────────
router.get('/roles', requireAuth, (_req, res) => {
  const rows = db.prepare(`
    SELECT key, label, hierarchy, is_system, created_at
    FROM roles ORDER BY hierarchy DESC, key
  `).all()
  res.json(rows)
})

// ── Liste des pages (catalogue) ───────────────────────────────────────────
router.get('/pages', requireAuth, (_req, res) => {
  const rows = db.prepare(`
    SELECT key, label, category FROM pages ORDER BY category, key
  `).all()
  res.json(rows)
})

// ── Matrice complète : rôles × pages × {view, edit, delete} ──────────────
router.get('/matrix', requireAuth, requirePagePermission('admin:permissions', 'view'), (_req, res) => {
  const roles = db.prepare(`SELECT key, label, hierarchy, is_system FROM roles ORDER BY hierarchy DESC, key`).all()
  const pages = db.prepare(`SELECT key, label, category FROM pages ORDER BY category, key`).all()
  const perms = db.prepare(`SELECT role_key, page_key, can_view, can_edit, can_delete FROM role_permissions`).all()

  // matrix[roleKey][pageKey] = { view, edit, delete }
  const matrix = {}
  for (const r of roles) matrix[r.key] = {}
  for (const p of perms) {
    if (!matrix[p.role_key]) matrix[p.role_key] = {}
    matrix[p.role_key][p.page_key] = {
      view:   !!p.can_view,
      edit:   !!p.can_edit,
      delete: !!p.can_delete,
    }
  }
  res.json({ roles, pages, matrix })
})

// ── Update bulk de la matrice ─────────────────────────────────────────────
// Body : { changes: [{ role_key, page_key, can_view, can_edit, can_delete }, ...] }
// `owner` reste passe-partout : on ignore silencieusement les changements le
// concernant pour éviter qu'un admin ne se tire une balle dans le pied.
router.put('/matrix', requireAuth, requirePagePermission('admin:permissions', 'edit'), (req, res) => {
  const changes = Array.isArray(req.body?.changes) ? req.body.changes : []
  if (!changes.length) return res.json({ ok: true, applied: 0 })

  const upsert = db.prepare(`
    INSERT INTO role_permissions (role_key, page_key, can_view, can_edit, can_delete)
    VALUES (@role_key, @page_key, @can_view, @can_edit, @can_delete)
    ON CONFLICT(role_key, page_key) DO UPDATE SET
      can_view   = excluded.can_view,
      can_edit   = excluded.can_edit,
      can_delete = excluded.can_delete
  `)

  let applied = 0
  db.transaction(() => {
    for (const c of changes) {
      if (!c.role_key || !c.page_key) continue
      if (c.role_key === 'owner') continue   // owner intouchable
      // Vérifie que la page existe (sinon FK violation)
      const pageExists = db.prepare('SELECT 1 FROM pages WHERE key = ?').get(c.page_key)
      const roleExists = db.prepare('SELECT 1 FROM roles WHERE key = ?').get(c.role_key)
      if (!pageExists || !roleExists) continue
      upsert.run({
        role_key:   c.role_key,
        page_key:   c.page_key,
        can_view:   c.can_view   ? 1 : 0,
        can_edit:   c.can_edit   ? 1 : 0,
        can_delete: c.can_delete ? 1 : 0,
      })
      applied++
    }
  })()

  broadcast('permissions_updated', {})
  res.json({ ok: true, applied })
})

// ── Créer un rôle personnalisé ────────────────────────────────────────────
router.post('/roles', requireAuth, requirePagePermission('admin:permissions', 'edit'), (req, res) => {
  const { key, label, hierarchy } = req.body || {}
  if (!key || typeof key !== 'string' || !/^[a-z0-9_-]{2,40}$/i.test(key)) {
    return res.status(400).json({ error: 'Clé invalide (a-z, 0-9, _, -, 2..40)' })
  }
  if (!label || typeof label !== 'string' || label.length > 60) {
    return res.status(400).json({ error: 'Libellé invalide' })
  }
  const safeKey = key.toLowerCase()
  if (db.prepare('SELECT 1 FROM roles WHERE key = ?').get(safeKey)) {
    return res.status(409).json({ error: 'Rôle déjà existant' })
  }
  const h = Number.isInteger(hierarchy) ? Math.max(0, Math.min(99, hierarchy)) : 1

  db.transaction(() => {
    db.prepare(`INSERT INTO roles (key, label, hierarchy, is_system) VALUES (?, ?, ?, 0)`)
      .run(safeKey, label, h)
    // Seed : aucune permission par défaut pour un nouveau rôle (à régler dans la matrice)
    const insert = db.prepare(`
      INSERT INTO role_permissions (role_key, page_key, can_view, can_edit, can_delete)
      VALUES (?, ?, 0, 0, 0)
    `)
    for (const p of PAGES) insert.run(safeKey, p.key)
  })()

  broadcast('permissions_updated', {})
  res.status(201).json({ ok: true, key: safeKey })
})

// ── Renommer un rôle / changer hiérarchie ─────────────────────────────────
router.put('/roles/:key', requireAuth, requirePagePermission('admin:permissions', 'edit'), (req, res) => {
  const { key } = req.params
  const role = db.prepare('SELECT * FROM roles WHERE key = ?').get(key)
  if (!role) return res.status(404).json({ error: 'Rôle introuvable' })

  const { label, hierarchy } = req.body || {}
  const newLabel     = typeof label === 'string' && label.trim() ? label.trim().slice(0, 60) : role.label
  const newHierarchy = Number.isInteger(hierarchy) ? Math.max(0, Math.min(99, hierarchy)) : role.hierarchy

  db.prepare(`UPDATE roles SET label = ?, hierarchy = ? WHERE key = ?`)
    .run(newLabel, newHierarchy, key)

  broadcast('permissions_updated', {})
  res.json({ ok: true })
})

// ── Supprimer un rôle (non-système uniquement) ───────────────────────────
router.delete('/roles/:key', requireAuth, requirePagePermission('admin:permissions', 'delete'), (req, res) => {
  const { key } = req.params
  const role = db.prepare('SELECT * FROM roles WHERE key = ?').get(key)
  if (!role) return res.status(404).json({ error: 'Rôle introuvable' })
  if (role.is_system) return res.status(400).json({ error: 'Impossible de supprimer un rôle système' })

  // Si des users portent ce rôle, on les rebascule sur 'viewer'
  const fallback = req.body?.reassign_to || 'viewer'
  const fallbackExists = db.prepare('SELECT 1 FROM roles WHERE key = ?').get(fallback)
  if (!fallbackExists) return res.status(400).json({ error: `Rôle de repli "${fallback}" introuvable` })

  db.transaction(() => {
    db.prepare('UPDATE users SET role = ? WHERE role = ?').run(fallback, key)
    db.prepare('DELETE FROM roles WHERE key = ?').run(key)
    // Les role_permissions partent en cascade via FK ON DELETE CASCADE
  })()

  broadcast('permissions_updated', {})
  broadcast('users_updated', {})
  res.json({ ok: true })
})

export default router
