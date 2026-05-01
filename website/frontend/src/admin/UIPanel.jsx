import { useState, useEffect } from 'react'
import { ExternalLink, Settings, X } from 'lucide-react'

const LS_KEY = 'hub_figma_url'

export default function UIPanel() {
  const [url,        setUrl]        = useState(() => localStorage.getItem(LS_KEY) || '')
  const [editUrl,    setEditUrl]    = useState('')
  const [showConfig, setShowConfig] = useState(false)

  useEffect(() => {
    if (!url) setShowConfig(true)
  }, [])

  const openConfig = () => {
    setEditUrl(url)
    setShowConfig(true)
  }

  const save = () => {
    const trimmed = editUrl.trim()
    setUrl(trimmed)
    localStorage.setItem(LS_KEY, trimmed)
    setShowConfig(false)
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', background: '#1a1a1a' }}>
      {/* Toolbar */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 8,
        padding: '8px 12px', background: '#242424', borderBottom: '1px solid #333', flexShrink: 0,
      }}>
        <span style={{ fontWeight: 600, color: '#e8e0d0', fontSize: 14, flex: 1 }}>Figma</span>
        {url && (
          <a href={url} target="_blank" rel="noreferrer"
            style={{ display: 'flex', alignItems: 'center', gap: 4, color: '#888', fontSize: 12, textDecoration: 'none' }}
            title="Ouvrir dans Figma">
            <ExternalLink size={14} /> Ouvrir
          </a>
        )}
        <button onClick={openConfig}
          style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#888', display: 'flex', alignItems: 'center', padding: 4 }}
          title="Configurer l'URL Figma">
          <Settings size={15} />
        </button>
      </div>

      {/* Iframe */}
      {url ? (
        <iframe
          src={url}
          style={{ flex: 1, border: 'none', width: '100%' }}
          allowFullScreen
        />
      ) : (
        <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#666', fontSize: 14 }}>
          Colle une URL d'embed Figma pour commencer
        </div>
      )}

      {/* Modal config */}
      {showConfig && (
        <div onClick={() => url && setShowConfig(false)}
          style={{ position: 'fixed', inset: 0, zIndex: 9999, background: 'rgba(0,0,0,0.7)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
          <div onClick={e => e.stopPropagation()}
            style={{ background: '#242424', border: '1px solid #444', borderRadius: 8, padding: 24, width: 520, maxWidth: '90vw' }}>
            <div style={{ display: 'flex', alignItems: 'center', marginBottom: 16 }}>
              <span style={{ fontWeight: 600, color: '#e8e0d0', flex: 1 }}>URL d'embed Figma</span>
              {url && <button onClick={() => setShowConfig(false)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#888' }}><X size={16} /></button>}
            </div>
            <p style={{ color: '#888', fontSize: 12, marginBottom: 12 }}>
              Dans Figma : <strong style={{ color: '#aaa' }}>Share → Copy embed link</strong>
            </p>
            <input
              autoFocus
              value={editUrl}
              onChange={e => setEditUrl(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && save()}
              placeholder="https://www.figma.com/embed?embed_host=share&url=..."
              style={{
                width: '100%', padding: '8px 10px', borderRadius: 6,
                border: '1px solid #555', background: '#1a1a1a', color: '#e8e0d0',
                fontSize: 13, boxSizing: 'border-box', marginBottom: 16,
              }}
            />
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
              {url && <button onClick={() => setShowConfig(false)}
                style={{ padding: '6px 14px', borderRadius: 6, border: '1px solid #555', background: 'none', color: '#aaa', cursor: 'pointer', fontSize: 13 }}>
                Annuler
              </button>}
              <button onClick={save}
                style={{ padding: '6px 14px', borderRadius: 6, border: 'none', background: 'var(--brand-primary, #e07b39)', color: '#fff', cursor: 'pointer', fontSize: 13, fontWeight: 600 }}>
                Enregistrer
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
