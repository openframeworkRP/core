// ============================================================
// BrandingPanel — edition du logo, couleurs, nom du site
// ============================================================
// Reserve aux owners (cf. AdminApp). PUT /api/branding sauvegarde,
// puis BrandingContext.refresh() recharge l'app entiere avec le
// nouveau theme (CSS vars + favicon + title).
// ============================================================

import { useState, useEffect } from 'react'
import { useBranding } from '../context/BrandingContext.jsx'
import './BrandingPanel.css'

const FIELDS = [
  { key: 'site_name',       label: 'Nom du site',          type: 'text',  hint: 'Affiche dans le header, le title du browser et le SEO.' },
  { key: 'site_short_name', label: 'Nom court',            type: 'text',  hint: 'Pour les espaces compacts (mobile, etc.). Souvent identique au nom du site.' },
  { key: 'default_author',  label: 'Auteur par defaut',    type: 'text',  hint: 'Auteur affiche sur les nouveaux devlogs si non precise.' },
  { key: 'description',     label: 'Description',          type: 'textarea', hint: 'Meta description pour le SEO. ~150 caracteres ideal.' },
  { key: 'primary_color',   label: 'Couleur principale',   type: 'color', hint: 'Boutons, liens, accents. Hex (#RRGGBB).' },
  { key: 'accent_color',    label: 'Couleur accent',       type: 'color', hint: 'Hover, highlights, badges. Hex (#RRGGBB).' },
  { key: 'logo_url',        label: 'URL du logo',          type: 'text',  hint: 'URL absolue (https://...) ou relative (/img/logo.png). Laisse vide pour pas de logo.' },
  { key: 'favicon_url',     label: 'URL du favicon',       type: 'text',  hint: 'Idem. Format recommande : ICO/PNG 32x32.' },
]

export default function BrandingPanel() {
  const { branding, loading, save } = useBranding()
  const [form, setForm]       = useState({})
  const [saving, setSaving]   = useState(false)
  const [error, setError]     = useState(null)
  const [success, setSuccess] = useState(false)

  // Synchronise le form avec les valeurs courantes au mount / refresh.
  useEffect(() => {
    if (!loading) setForm({ ...branding })
  }, [loading, branding])

  function update(key, value) {
    setForm(prev => ({ ...prev, [key]: value }))
    setSuccess(false)
  }

  async function handleSave() {
    setSaving(true)
    setError(null)
    try {
      // N'envoie que les cles modifiees
      const updates = {}
      for (const { key } of FIELDS) {
        if (form[key] !== branding[key]) updates[key] = form[key]
      }
      if (Object.keys(updates).length === 0) {
        setSuccess(true)
        return
      }
      await save(updates)
      setSuccess(true)
    } catch (e) {
      setError(e.message)
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <div className="brand-page">Chargement…</div>

  return (
    <div className="brand-page">
      <header className="brand-header">
        <h1>Branding</h1>
        <p className="brand-subtitle">
          Personnalise le nom, le logo et les couleurs de ton instance.
          Les changements s'appliquent immediatement apres save.
        </p>
      </header>

      {error && <div className="brand-error">⚠ {error}</div>}
      {success && <div className="brand-success">✓ Branding sauvegarde — recharge la page pour voir tous les effets.</div>}

      <div className="brand-form">
        {FIELDS.map(field => (
          <div key={field.key} className="brand-field">
            <label htmlFor={`brand-${field.key}`}>{field.label}</label>
            <p className="brand-hint">{field.hint}</p>
            {field.type === 'textarea' ? (
              <textarea
                id={`brand-${field.key}`}
                value={form[field.key] || ''}
                onChange={e => update(field.key, e.target.value)}
                rows={3}
              />
            ) : field.type === 'color' ? (
              <div className="brand-color-row">
                <input
                  type="color"
                  value={form[field.key] || '#000000'}
                  onChange={e => update(field.key, e.target.value)}
                />
                <input
                  type="text"
                  value={form[field.key] || ''}
                  onChange={e => update(field.key, e.target.value)}
                  placeholder="#RRGGBB"
                  className="brand-color-text"
                />
              </div>
            ) : (
              <input
                id={`brand-${field.key}`}
                type="text"
                value={form[field.key] || ''}
                onChange={e => update(field.key, e.target.value)}
              />
            )}
          </div>
        ))}
      </div>

      {/* Preview live du logo */}
      {form.logo_url && (
        <div className="brand-preview">
          <h3>Preview du logo</h3>
          <img
            src={form.logo_url}
            alt={form.site_name}
            onError={(e) => { e.target.style.display = 'none' }}
          />
        </div>
      )}

      <div className="brand-actions">
        <button
          type="button"
          className="brand-btn brand-btn-primary"
          onClick={handleSave}
          disabled={saving}
        >
          {saving ? 'Sauvegarde…' : 'Sauvegarder'}
        </button>
        <button
          type="button"
          className="brand-btn"
          onClick={() => setForm({ ...branding })}
          disabled={saving}
        >
          Annuler les modifications
        </button>
      </div>
    </div>
  )
}
