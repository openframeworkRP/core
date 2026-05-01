// ============================================================
// RoadmapAdminPanel — gestion de la roadmap publique
// ============================================================
// Liste tous les items (public + brouillons), permet l'edition
// inline du status + toggle is_public, ajout/suppression.
// ============================================================

import { useEffect, useState } from 'react'
import { Plus, Trash2, Eye, EyeOff } from 'lucide-react'
import './RoadmapAdminPanel.css'

const STATUS_OPTIONS = [
  { value: 'planned',     label: 'Prevu' },
  { value: 'in_progress', label: 'En cours' },
  { value: 'done',        label: 'Fini' },
  { value: 'shipped',     label: 'Live' },
]

export default function RoadmapAdminPanel() {
  const [items, setItems] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [creating, setCreating] = useState(false)
  const [draft, setDraft] = useState({ title: '', description: '', status: 'planned', is_public: false })

  async function load() {
    setLoading(true)
    try {
      const r = await fetch('/api/roadmap/admin', { credentials: 'include' })
      if (!r.ok) throw new Error(`HTTP ${r.status}`)
      setItems(await r.json())
      setError(null)
    } catch (e) {
      setError(e.message)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load() }, [])

  async function createItem() {
    if (!draft.title.trim()) return
    try {
      const r = await fetch('/api/roadmap', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(draft),
      })
      if (!r.ok) throw new Error(`HTTP ${r.status}`)
      setDraft({ title: '', description: '', status: 'planned', is_public: false })
      setCreating(false)
      load()
    } catch (e) {
      setError(e.message)
    }
  }

  async function patchItem(id, updates) {
    try {
      const item = items.find(i => i.id === id)
      const r = await fetch(`/api/roadmap/${id}`, {
        method: 'PUT',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ...item, ...updates }),
      })
      if (!r.ok) throw new Error(`HTTP ${r.status}`)
      setItems(prev => prev.map(i => i.id === id ? { ...i, ...updates } : i))
    } catch (e) {
      setError(e.message)
    }
  }

  async function deleteItem(id) {
    if (!confirm('Supprimer cet item de la roadmap ?')) return
    try {
      const r = await fetch(`/api/roadmap/${id}`, {
        method: 'DELETE',
        credentials: 'include',
      })
      if (!r.ok) throw new Error(`HTTP ${r.status}`)
      setItems(prev => prev.filter(i => i.id !== id))
    } catch (e) {
      setError(e.message)
    }
  }

  return (
    <div className="rmadm-page">
      <header className="rmadm-header">
        <h1>Roadmap</h1>
        <p className="rmadm-subtitle">
          Items affiches sur la home dans la section #roadmap.
          Coche le bouton oeil pour les rendre visibles aux visiteurs.
        </p>
      </header>

      {error && <div className="rmadm-error">⚠ {error}</div>}

      {!creating ? (
        <button className="rmadm-btn rmadm-btn-primary rmadm-btn-add" onClick={() => setCreating(true)}>
          <Plus size={16} /> Nouvel item
        </button>
      ) : (
        <div className="rmadm-create">
          <input
            placeholder="Titre"
            value={draft.title}
            onChange={e => setDraft(d => ({ ...d, title: e.target.value }))}
            autoFocus
          />
          <textarea
            placeholder="Description (optionnelle)"
            value={draft.description}
            onChange={e => setDraft(d => ({ ...d, description: e.target.value }))}
            rows={2}
          />
          <div className="rmadm-create-row">
            <select value={draft.status} onChange={e => setDraft(d => ({ ...d, status: e.target.value }))}>
              {STATUS_OPTIONS.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
            </select>
            <label className="rmadm-checkbox">
              <input
                type="checkbox"
                checked={draft.is_public}
                onChange={e => setDraft(d => ({ ...d, is_public: e.target.checked }))}
              />
              Public
            </label>
            <div style={{ flex: 1 }} />
            <button className="rmadm-btn" onClick={() => setCreating(false)}>Annuler</button>
            <button className="rmadm-btn rmadm-btn-primary" onClick={createItem} disabled={!draft.title.trim()}>
              Creer
            </button>
          </div>
        </div>
      )}

      {loading ? (
        <p>Chargement…</p>
      ) : items.length === 0 ? (
        <p className="rmadm-empty">Aucun item. Click 'Nouvel item' pour commencer.</p>
      ) : (
        <div className="rmadm-list">
          {items.map(item => (
            <div key={item.id} className={`rmadm-item rmadm-item--${item.status}${item.is_public ? '' : ' rmadm-item--draft'}`}>
              <button
                className="rmadm-icon-btn"
                onClick={() => patchItem(item.id, { is_public: !item.is_public })}
                title={item.is_public ? 'Public — clique pour masquer' : 'Masque — clique pour publier'}
              >
                {item.is_public ? <Eye size={16} /> : <EyeOff size={16} />}
              </button>

              <div className="rmadm-item-body">
                <input
                  className="rmadm-item-title"
                  value={item.title}
                  onChange={e => setItems(prev => prev.map(i => i.id === item.id ? { ...i, title: e.target.value } : i))}
                  onBlur={e => patchItem(item.id, { title: e.target.value })}
                />
                <textarea
                  className="rmadm-item-desc"
                  value={item.description || ''}
                  onChange={e => setItems(prev => prev.map(i => i.id === item.id ? { ...i, description: e.target.value } : i))}
                  onBlur={e => patchItem(item.id, { description: e.target.value })}
                  placeholder="Description…"
                  rows={1}
                />
              </div>

              <select
                className="rmadm-item-status"
                value={item.status}
                onChange={e => patchItem(item.id, { status: e.target.value })}
              >
                {STATUS_OPTIONS.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
              </select>

              <button className="rmadm-icon-btn rmadm-icon-btn-danger" onClick={() => deleteItem(item.id)} title="Supprimer">
                <Trash2 size={16} />
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
