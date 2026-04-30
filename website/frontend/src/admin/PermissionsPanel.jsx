import { useState, useEffect, useMemo, useCallback } from 'react'
import { api } from './api.js'
import { useAuth } from '../context/AuthContext.jsx'
import { useAdminSocket } from './useAdminSocket.js'
import { Shield, Plus, Save, Trash2, Eye, Pencil, X, AlertTriangle } from 'lucide-react'

const ACTIONS = [
  { key: 'view',   label: 'Voir',       icon: Eye },
  { key: 'edit',   label: 'Éditer',     icon: Pencil },
  { key: 'delete', label: 'Supprimer',  icon: Trash2 },
]

const CATEGORY_LABELS = {
  admin:   'Admin',
  hub:     'Hub',
  media:   'Médias & Docs',
  devblog: 'DevBlog',
  misc:    'Divers',
}

export default function PermissionsPanel() {
  const { refresh: refreshAuth } = useAuth()
  const [matrix, setMatrix]   = useState({})       // { roleKey: { pageKey: {view,edit,delete} } }
  const [roles, setRoles]     = useState([])
  const [pages, setPages]     = useState([])
  const [dirty, setDirty]     = useState({})       // { 'role|page': true }
  const [saving, setSaving]   = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError]     = useState('')
  const [showCreate, setShowCreate] = useState(false)

  const load = useCallback(async () => {
    try {
      setLoading(true)
      const data = await api.getPermissionsMatrix()
      setRoles(data.roles)
      setPages(data.pages)
      setMatrix(data.matrix || {})
      setDirty({})
      setError('')
    } catch (e) {
      setError(e.message)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  // Reload si un autre admin sauvegarde la matrice
  useAdminSocket(['permissions_updated'], () => load())

  // Pages groupées par catégorie
  const pagesByCategory = useMemo(() => {
    const groups = {}
    for (const p of pages) {
      const cat = p.category || 'misc'
      if (!groups[cat]) groups[cat] = []
      groups[cat].push(p)
    }
    return groups
  }, [pages])

  function toggle(roleKey, pageKey, action) {
    if (roleKey === 'owner') return     // owner intouchable
    setMatrix(m => {
      const next = { ...m }
      const row  = { ...(next[roleKey] || {}) }
      const cell = { view: false, edit: false, delete: false, ...(row[pageKey] || {}) }
      cell[action] = !cell[action]
      // Cohérence simple : edit/delete impliquent view
      if ((action === 'edit' || action === 'delete') && cell[action]) {
        cell.view = true
      }
      // Si on retire view, on retire aussi edit/delete
      if (action === 'view' && !cell.view) {
        cell.edit = false
        cell.delete = false
      }
      row[pageKey] = cell
      next[roleKey] = row
      return next
    })
    setDirty(d => ({ ...d, [`${roleKey}|${pageKey}`]: true }))
  }

  async function save() {
    if (saving) return
    const changes = []
    for (const k of Object.keys(dirty)) {
      const [roleKey, pageKey] = k.split('|')
      const cell = matrix?.[roleKey]?.[pageKey] || { view:false, edit:false, delete:false }
      changes.push({
        role_key: roleKey, page_key: pageKey,
        can_view: cell.view, can_edit: cell.edit, can_delete: cell.delete,
      })
    }
    if (!changes.length) return
    try {
      setSaving(true)
      await api.savePermissionsMatrix(changes)
      setDirty({})
      // Si on a modifié les permissions du user connecté, rafraîchit
      refreshAuth?.()
    } catch (e) {
      setError(e.message)
    } finally {
      setSaving(false)
    }
  }

  async function handleCreateRole(payload) {
    try {
      await api.createRole(payload)
      setShowCreate(false)
      await load()
    } catch (e) {
      setError(e.message)
    }
  }

  async function handleRenameRole(role) {
    const newLabel = prompt(`Nouveau libellé pour "${role.label}" :`, role.label)
    if (!newLabel || newLabel === role.label) return
    try {
      await api.updateRole(role.key, { label: newLabel })
      await load()
    } catch (e) { setError(e.message) }
  }

  async function handleDeleteRole(role) {
    if (role.is_system) return
    const fallback = prompt(
      `Supprimer le rôle "${role.label}" — vers quel rôle rebasculer les utilisateurs concernés ?`,
      'viewer',
    )
    if (!fallback) return
    if (!confirm(`Supprimer définitivement le rôle "${role.label}" ?`)) return
    try {
      await api.deleteRole(role.key, fallback)
      await load()
    } catch (e) { setError(e.message) }
  }

  if (loading) {
    return <div className="adm__panel"><div className="adm__panel-body">Chargement…</div></div>
  }

  const dirtyCount = Object.keys(dirty).length

  return (
    <div className="adm__panel">
      <header className="adm__panel-head" style={{ display: 'flex', alignItems: 'center', gap: 12, justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <Shield size={18} />
          <h2 style={{ margin: 0 }}>Permissions</h2>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="adm__btn" onClick={() => setShowCreate(true)}>
            <Plus size={14} /> Nouveau rôle
          </button>
          <button
            className="adm__btn adm__btn--primary"
            onClick={save}
            disabled={!dirtyCount || saving}
            title={dirtyCount ? `${dirtyCount} changement(s) en attente` : 'Aucun changement'}
          >
            <Save size={14} /> {saving ? 'Sauvegarde…' : `Enregistrer${dirtyCount ? ` (${dirtyCount})` : ''}`}
          </button>
        </div>
      </header>

      {error && (
        <div className="adm__panel-body" style={{ background: '#7f1d1d22', color: '#fca5a5', padding: 10, margin: '8px 16px', borderRadius: 6, display: 'flex', alignItems: 'center', gap: 8 }}>
          <AlertTriangle size={14} /> {error}
          <button onClick={() => setError('')} style={{ marginLeft: 'auto', background: 'transparent', border: 0, color: 'inherit', cursor: 'pointer' }}>
            <X size={14} />
          </button>
        </div>
      )}

      <div className="adm__panel-body" style={{ overflow: 'auto' }}>
        <p style={{ opacity: 0.7, fontSize: 13, marginTop: 0 }}>
          Pour chaque rôle, coche les pages auxquelles il a accès. Les colonnes <b>Voir / Éditer / Supprimer</b>
          sont indépendantes — éditer ou supprimer implique automatiquement de pouvoir voir.
          Le rôle <b>owner</b> a tous les droits par défaut et n'est pas modifiable.
        </p>

        {/* Liste des rôles personnalisables */}
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', margin: '8px 0 16px' }}>
          {roles.map(r => (
            <span key={r.key}
              style={{
                display: 'inline-flex', alignItems: 'center', gap: 6,
                padding: '4px 10px', borderRadius: 99,
                background: r.is_system ? '#1f293744' : '#3b82f622',
                fontSize: 12,
              }}
            >
              <b>{r.label}</b>
              <span style={{ opacity: 0.6 }}>({r.key}, h={r.hierarchy})</span>
              {!r.is_system && (
                <>
                  <button onClick={() => handleRenameRole(r)} title="Renommer"
                    style={{ background: 'transparent', border: 0, color: 'inherit', cursor: 'pointer', padding: 0, display: 'inline-flex' }}>
                    <Pencil size={12} />
                  </button>
                  <button onClick={() => handleDeleteRole(r)} title="Supprimer"
                    style={{ background: 'transparent', border: 0, color: '#fca5a5', cursor: 'pointer', padding: 0, display: 'inline-flex' }}>
                    <Trash2 size={12} />
                  </button>
                </>
              )}
            </span>
          ))}
        </div>

        {/* Matrice : pages en lignes, rôles en colonnes (chaque rôle = 3 sous-colonnes) */}
        {Object.entries(pagesByCategory).map(([cat, list]) => (
          <section key={cat} style={{ marginBottom: 18 }}>
            <h3 style={{ margin: '8px 0', fontSize: 13, opacity: 0.7, textTransform: 'uppercase', letterSpacing: 0.5 }}>
              {CATEGORY_LABELS[cat] || cat}
            </h3>
            <div style={{ overflowX: 'auto', border: '1px solid #1f2937', borderRadius: 6 }}>
              <table style={{ borderCollapse: 'collapse', width: '100%', fontSize: 12 }}>
                <thead>
                  <tr style={{ background: '#0f172a' }}>
                    <th style={{ position: 'sticky', left: 0, background: '#0f172a', padding: '8px 10px', textAlign: 'left', minWidth: 220, borderRight: '1px solid #1f2937' }}>
                      Page
                    </th>
                    {roles.map(r => (
                      <th key={r.key} colSpan={3} style={{ padding: '8px 6px', textAlign: 'center', borderRight: '1px solid #1f2937' }}>
                        {r.label}
                      </th>
                    ))}
                  </tr>
                  <tr style={{ background: '#0b1320' }}>
                    <th style={{ position: 'sticky', left: 0, background: '#0b1320', padding: '4px 10px', borderRight: '1px solid #1f2937' }} />
                    {roles.flatMap(r => ACTIONS.map(a => (
                      <th key={`${r.key}-${a.key}`} title={a.label} style={{ padding: '4px 6px', textAlign: 'center', opacity: 0.6, fontWeight: 500 }}>
                        <a.icon size={12} />
                      </th>
                    )))}
                  </tr>
                </thead>
                <tbody>
                  {list.map(page => (
                    <tr key={page.key} style={{ borderTop: '1px solid #1f2937' }}>
                      <td style={{ position: 'sticky', left: 0, background: '#020617', padding: '6px 10px', borderRight: '1px solid #1f2937', whiteSpace: 'nowrap' }}>
                        <div>{page.label}</div>
                        <div style={{ fontSize: 10, opacity: 0.4 }}>{page.key}</div>
                      </td>
                      {roles.flatMap(r => ACTIONS.map(a => {
                        const cell = matrix?.[r.key]?.[page.key] || {}
                        const checked = !!cell[a.key]
                        const isOwner = r.key === 'owner'
                        const isDirty = !!dirty[`${r.key}|${page.key}`]
                        return (
                          <td key={`${r.key}-${page.key}-${a.key}`}
                            style={{
                              padding: '4px 6px', textAlign: 'center',
                              background: isDirty ? '#3b82f622' : 'transparent',
                            }}>
                            <input
                              type="checkbox"
                              checked={isOwner ? true : checked}
                              disabled={isOwner}
                              onChange={() => toggle(r.key, page.key, a.key)}
                              title={`${r.label} — ${page.label} — ${a.label}`}
                            />
                          </td>
                        )
                      }))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        ))}
      </div>

      {showCreate && <CreateRoleModal onClose={() => setShowCreate(false)} onCreate={handleCreateRole} />}
    </div>
  )
}

function CreateRoleModal({ onClose, onCreate }) {
  const [key, setKey]             = useState('')
  const [label, setLabel]         = useState('')
  const [hierarchy, setHierarchy] = useState(1)
  const [err, setErr]             = useState('')

  async function submit(e) {
    e.preventDefault()
    if (!/^[a-z0-9_-]{2,40}$/i.test(key)) { setErr('Clé invalide'); return }
    if (!label.trim()) { setErr('Libellé requis'); return }
    try { await onCreate({ key: key.toLowerCase(), label: label.trim(), hierarchy: Number(hierarchy) || 1 }) }
    catch (e) { setErr(e.message) }
  }

  return (
    <div onClick={onClose}
      style={{ position: 'fixed', inset: 0, background: '#000a', display: 'grid', placeItems: 'center', zIndex: 9999 }}>
      <form onClick={e => e.stopPropagation()} onSubmit={submit}
        style={{ background: '#0f172a', padding: 24, borderRadius: 8, minWidth: 360, display: 'flex', flexDirection: 'column', gap: 12 }}>
        <h3 style={{ margin: 0 }}>Nouveau rôle</h3>
        {err && <div style={{ color: '#fca5a5', fontSize: 12 }}>{err}</div>}
        <label style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: 12 }}>
          Clé technique (a-z, 0-9, _, -)
          <input value={key} onChange={e => setKey(e.target.value)} placeholder="moderator" autoFocus />
        </label>
        <label style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: 12 }}>
          Libellé
          <input value={label} onChange={e => setLabel(e.target.value)} placeholder="Modérateur" />
        </label>
        <label style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: 12 }}>
          Hiérarchie (0..99, plus élevé = rang plus haut)
          <input type="number" min={0} max={99} value={hierarchy} onChange={e => setHierarchy(e.target.value)} />
        </label>
        <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
          <button type="button" className="adm__btn" onClick={onClose}>Annuler</button>
          <button type="submit" className="adm__btn adm__btn--primary">Créer</button>
        </div>
      </form>
    </div>
  )
}
