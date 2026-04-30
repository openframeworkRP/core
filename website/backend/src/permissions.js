// ── Permissions : catalogue des pages + helpers DB + middleware ────────────
//
// Le système de permissions est désormais piloté par les tables `roles`,
// `pages` et `role_permissions`. Ce fichier centralise :
//   - le catalogue des pages connues (seed initial)
//   - les helpers de lecture/écriture
//   - le middleware Express `requirePagePermission(pageKey, action)`
//
// Les rôles « système » (owner, admin, editor, rules_editor, viewer)
// gardent leur hiérarchie historique, mais n'ont plus de privilèges
// implicites — sauf `owner` qui reste super-admin (passe-partout).

import db from './db.js'

// ── Catalogue : toutes les pages que l'admin peut autoriser/interdire ──────
// `key`        : identifiant stable utilisé en code (frontend + backend)
// `category`   : regroupement visuel dans la matrice
// `label`      : libellé FR affiché à l'admin
// `default`    : permissions seed par rôle système
//                { view: ['owner','admin'], edit: [...], delete: [...] }
//                Si un tableau est `'*'`, tous les rôles système l'ont.
//
// IMPORTANT : ajouter ici toute nouvelle page avant de poser un check côté
// backend ou un `useCanAccess()` côté frontend.
export const PAGES = [
  // ── Admin ──
  { key: 'admin:games',       category: 'admin', label: 'Jeux',
    default: { view: '*', edit: ['owner','admin','editor'], delete: ['owner','admin'] } },
  { key: 'admin:jobs',        category: 'admin', label: 'Emplois',
    default: { view: '*', edit: ['owner','admin','editor'], delete: ['owner','admin'] } },
  { key: 'admin:bugs',        category: 'admin', label: 'Bugs',
    default: { view: '*', edit: ['owner','admin','editor'], delete: ['owner','admin'] } },
  { key: 'admin:stats',       category: 'admin', label: 'Stats',
    default: { view: '*', edit: ['owner','admin'], delete: ['owner'] } },
  { key: 'admin:users',       category: 'admin', label: 'Équipe (users)',
    default: { view: ['owner','admin'], edit: ['owner','admin'], delete: ['owner'] } },
  { key: 'admin:gameadmin',   category: 'admin', label: 'Admin Jeu',
    default: { view: ['owner','admin','editor'], edit: ['owner','admin','editor'], delete: ['owner','admin'] } },
  { key: 'admin:permissions', category: 'admin', label: 'Permissions',
    default: { view: ['owner'], edit: ['owner'], delete: ['owner'] } },

  // ── Hub ──
  { key: 'hub:dashboard',  category: 'hub', label: 'Dashboard',  default: { view: '*', edit: '*', delete: ['owner','admin'] } },
  { key: 'hub:tasks',      category: 'hub', label: 'Tâches',     default: { view: '*', edit: '*', delete: ['owner','admin','editor'] } },
  { key: 'hub:roadmap',    category: 'hub', label: 'Roadmap',    default: { view: '*', edit: ['owner','admin','editor'], delete: ['owner','admin'] } },
  { key: 'hub:whiteboard', category: 'hub', label: 'Idées',      default: { view: '*', edit: '*', delete: ['owner','admin','editor'] } },
  { key: 'hub:mapview',    category: 'hub', label: 'Map',        default: { view: '*', edit: ['owner','admin','editor'], delete: ['owner','admin'] } },
  { key: 'hub:fab',        category: 'hub', label: 'Assets Fab', default: { view: '*', edit: ['owner','admin','editor'], delete: ['owner','admin'] } },
  { key: 'hub:catalogue',  category: 'hub', label: 'Catalogue',  default: { view: '*', edit: ['owner','admin','editor'], delete: ['owner','admin'] } },
  { key: 'hub:activity',   category: 'hub', label: 'Activité',   default: { view: '*', edit: ['owner','admin'], delete: ['owner'] } },

  // ── Médias & Docs ──
  { key: 'admin:videos',   category: 'media', label: 'Vidéos',         default: { view: '*', edit: ['owner','admin','editor'], delete: ['owner','admin'] } },
  { key: 'admin:images',   category: 'media', label: 'Images',         default: { view: '*', edit: ['owner','admin','editor'], delete: ['owner','admin'] } },
  { key: 'admin:rules',    category: 'media', label: 'Règles',         default: { view: '*', edit: ['owner','admin','editor'], delete: ['owner','admin'] } },
  { key: 'admin:sl-rules', category: 'media', label: 'OpenFramework — Règles',
    default: { view: ['owner','admin','editor','rules_editor'], edit: ['owner','admin','editor','rules_editor'], delete: ['owner','admin'] } },
  { key: 'admin:docs',     category: 'media', label: 'Documentation',  default: { view: '*', edit: ['owner','admin','editor'], delete: ['owner','admin'] } },
  { key: 'admin:wiki',     category: 'media', label: 'Wiki in-game',   default: { view: '*', edit: ['owner','admin','editor'], delete: ['owner','admin'] } },
  { key: 'admin:ui',       category: 'media', label: 'UI Builder',     default: { view: '*', edit: ['owner','admin','editor'], delete: ['owner','admin'] } },
  { key: 'admin:vehicles', category: 'media', label: 'Véhicules',      default: { view: '*', edit: ['owner','admin','editor'], delete: ['owner','admin'] } },

  // ── DevBlog ──
  { key: 'devblog:list',    category: 'devblog', label: 'Liste devlogs', default: { view: '*', edit: ['owner','admin','editor'], delete: ['owner','admin'] } },
  { key: 'devblog:edit',    category: 'devblog', label: 'Éditeur',       default: { view: ['owner','admin','editor'], edit: ['owner','admin','editor'], delete: ['owner','admin'] } },
  { key: 'devblog:publish', category: 'devblog', label: 'Publier',       default: { view: ['owner','admin','editor'], edit: ['owner','admin'], delete: ['owner','admin'] } },
]

// Rôles système (non supprimables) avec leur libellé et hiérarchie historique
// La hiérarchie ne sert plus qu'à `requireRole` (compat) — les permissions
// effectives sont toutes dans la matrice.
export const SYSTEM_ROLES = [
  { key: 'owner',        label: 'Owner',        hierarchy: 4 },
  { key: 'admin',        label: 'Admin',        hierarchy: 3 },
  { key: 'editor',       label: 'Editor',       hierarchy: 2 },
  { key: 'rules_editor', label: 'Rules Editor', hierarchy: 1 },
  { key: 'viewer',       label: 'Viewer',       hierarchy: 1 },
]

const ACTIONS = ['view', 'edit', 'delete']

// ── Initialisation : crée les tables, seed roles + pages + permissions ────
export function initPermissions() {
  db.exec(`
    CREATE TABLE IF NOT EXISTS roles (
      key         TEXT PRIMARY KEY,
      label       TEXT NOT NULL,
      hierarchy   INTEGER NOT NULL DEFAULT 1,
      is_system   INTEGER NOT NULL DEFAULT 0,
      created_at  TEXT DEFAULT (datetime('now'))
    );

    CREATE TABLE IF NOT EXISTS pages (
      key         TEXT PRIMARY KEY,
      label       TEXT NOT NULL,
      category    TEXT NOT NULL DEFAULT 'misc',
      created_at  TEXT DEFAULT (datetime('now'))
    );

    CREATE TABLE IF NOT EXISTS role_permissions (
      role_key    TEXT NOT NULL REFERENCES roles(key) ON DELETE CASCADE,
      page_key    TEXT NOT NULL REFERENCES pages(key) ON DELETE CASCADE,
      can_view    INTEGER NOT NULL DEFAULT 0,
      can_edit    INTEGER NOT NULL DEFAULT 0,
      can_delete  INTEGER NOT NULL DEFAULT 0,
      PRIMARY KEY (role_key, page_key)
    );
  `)

  // Seed rôles système (upsert : libellé/hierarchy peuvent évoluer)
  const upsertRole = db.prepare(`
    INSERT INTO roles (key, label, hierarchy, is_system) VALUES (?, ?, ?, 1)
    ON CONFLICT(key) DO UPDATE SET label = excluded.label, hierarchy = excluded.hierarchy, is_system = 1
  `)
  for (const r of SYSTEM_ROLES) upsertRole.run(r.key, r.label, r.hierarchy)

  // Seed pages (upsert label/category, jamais drop)
  const upsertPage = db.prepare(`
    INSERT INTO pages (key, label, category) VALUES (?, ?, ?)
    ON CONFLICT(key) DO UPDATE SET label = excluded.label, category = excluded.category
  `)
  for (const p of PAGES) upsertPage.run(p.key, p.label, p.category)

  // Seed initial des permissions : on ne touche QUE les couples (role, page)
  // qui n'ont pas encore de ligne — pour ne jamais écraser une config admin.
  const hasPerm = db.prepare('SELECT 1 FROM role_permissions WHERE role_key = ? AND page_key = ?')
  const insertPerm = db.prepare(`
    INSERT INTO role_permissions (role_key, page_key, can_view, can_edit, can_delete)
    VALUES (?, ?, ?, ?, ?)
  `)
  const allRoleKeys = SYSTEM_ROLES.map(r => r.key)
  const inDefault = (def, role) => def === '*' || (Array.isArray(def) && def.includes(role))

  db.transaction(() => {
    for (const page of PAGES) {
      for (const role of allRoleKeys) {
        if (hasPerm.get(role, page.key)) continue
        // owner reste super-admin par défaut, même si oublié dans le seed
        const isOwner = role === 'owner'
        insertPerm.run(
          role, page.key,
          isOwner || inDefault(page.default.view,   role) ? 1 : 0,
          isOwner || inDefault(page.default.edit,   role) ? 1 : 0,
          isOwner || inDefault(page.default.delete, role) ? 1 : 0,
        )
      }
    }
  })()
}

// ── Helpers de lecture ─────────────────────────────────────────────────────
// Prepared statements lazy : la table n'existe pas avant initPermissions()
let _getPermStmt = null
function getPermStmt() {
  if (!_getPermStmt) {
    _getPermStmt = db.prepare(
      'SELECT can_view, can_edit, can_delete FROM role_permissions WHERE role_key = ? AND page_key = ?'
    )
  }
  return _getPermStmt
}

export function getRolePermissions(roleKey) {
  // Renvoie un objet { 'admin:users': { view, edit, delete }, ... }
  const rows = db.prepare(`
    SELECT page_key, can_view, can_edit, can_delete
    FROM role_permissions WHERE role_key = ?
  `).all(roleKey)
  const out = {}
  for (const r of rows) {
    out[r.page_key] = { view: !!r.can_view, edit: !!r.can_edit, delete: !!r.can_delete }
  }
  return out
}

export function userHasPermission(role, pageKey, action = 'view') {
  if (!role || !pageKey || !ACTIONS.includes(action)) return false
  if (role === 'owner') return true   // owner = passe-partout
  const row = getPermStmt().get(role, pageKey)
  if (!row) return false
  if (action === 'view')   return !!row.can_view
  if (action === 'edit')   return !!row.can_edit
  if (action === 'delete') return !!row.can_delete
  return false
}

// ── Middleware Express ────────────────────────────────────────────────────
// Usage : router.put('/x', requirePagePermission('admin:users', 'edit'), handler)
//
// Suppose que le middleware d'authentification a déjà tourné en amont et
// posé `req.user.role`. Sinon → 401.
export function requirePagePermission(pageKey, action = 'view') {
  if (!ACTIONS.includes(action)) {
    throw new Error(`requirePagePermission: action invalide "${action}"`)
  }
  return (req, res, next) => {
    const role = req.user?.role
    if (!role) return res.status(401).json({ error: 'Non authentifié' })
    if (!userHasPermission(role, pageKey, action)) {
      return res.status(403).json({ error: `Accès refusé : ${pageKey} (${action})` })
    }
    next()
  }
}
