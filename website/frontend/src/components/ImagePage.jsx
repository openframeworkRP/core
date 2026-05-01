import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { API_BASE, api } from '../admin/api.js'

export default function ImagePage() {
  const { slug } = useParams()
  const [image, setImage] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    let cancelled = false
    api.getImage(slug)
      .then(img => { if (!cancelled) setImage(img) })
      .catch(() => { if (!cancelled) setError('Image introuvable.') })
    return () => { cancelled = true }
  }, [slug])

  function copyLink() {
    navigator.clipboard.writeText(`${window.location.origin}/i/${slug}`).catch(() => {})
    const el = document.getElementById('copy-btn')
    if (el) { el.textContent = 'Copié !'; setTimeout(() => { el.textContent = 'Copier le lien' }, 2000) }
  }

  return (
    <div style={{
      minHeight: '100vh',
      background: '#111',
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center',
      padding: '24px 16px',
      fontFamily: 'system-ui, sans-serif',
    }}>
      {error ? (
        <div style={{ color: '#888', fontSize: '1rem' }}>{error}</div>
      ) : !image ? (
        <div style={{ color: '#555', fontSize: '0.9rem' }}>Chargement…</div>
      ) : (
        <div style={{ width: '100%', maxWidth: 1200, display: 'flex', flexDirection: 'column', gap: 16 }}>
          {/* Image */}
          <div style={{ position: 'relative', width: '100%', background: '#000', borderRadius: 12, overflow: 'hidden', boxShadow: '0 8px 40px rgba(0,0,0,0.6)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <img
              src={`${API_BASE}/uploads/${image.filename}`}
              alt={image.title || ''}
              style={{ width: '100%', height: 'auto', display: 'block', maxHeight: '85vh', objectFit: 'contain' }}
            />
          </div>

          {/* Info bar */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ color: '#e8e8e8', fontWeight: 600, fontSize: '1rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                {image.title || 'Sans titre'}
              </div>
              <div style={{ color: '#555', fontSize: '0.75rem', marginTop: 2 }}>
                OpenFramework · {new Date(image.created_at).toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' })}
                {image.width > 0 && image.height > 0 && ` · ${image.width}×${image.height}`}
              </div>
            </div>

            <a
              href={`${API_BASE}/uploads/${image.filename}`}
              target="_blank"
              rel="noreferrer"
              style={{
                padding: '8px 18px', background: '#2a2a2a', color: '#ccc',
                border: '1px solid rgba(255,255,255,0.08)', borderRadius: 8,
                cursor: 'pointer', fontWeight: 600, fontSize: '0.82rem',
                textDecoration: 'none', flexShrink: 0,
              }}
            >
              Original
            </a>

            <button
              id="copy-btn"
              onClick={copyLink}
              style={{
                padding: '8px 18px', background: 'var(--brand-primary, #e07b39)', color: '#fff',
                border: 'none', borderRadius: 8, cursor: 'pointer',
                fontWeight: 600, fontSize: '0.82rem', fontFamily: 'inherit', flexShrink: 0,
              }}
            >
              Copier le lien
            </button>
          </div>

          {/* Branding */}
          <div style={{ textAlign: 'center', marginTop: 8 }}>
            <a href="/" style={{ color: '#333', fontSize: '0.72rem', textDecoration: 'none' }}>
              OpenFramework
            </a>
          </div>
        </div>
      )}
    </div>
  )
}
