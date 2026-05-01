import { useEffect, useRef, useState } from 'react'
import { useParams } from 'react-router-dom'
import { API_BASE, api } from '../admin/api.js'

export default function VideoPage() {
  const { slug } = useParams()
  const [video, setVideo] = useState(null)
  const [error, setError] = useState(null)
  const [pct, setPct] = useState(null)
  const videoRef = useRef(null)

  useEffect(() => {
    let cancelled = false
    let timer

    async function poll() {
      try {
        const [v, prog] = await Promise.all([
          api.getVideo(slug),
          api.getVideoProgress(slug).catch(() => null),
        ])
        if (cancelled) return
        setVideo(v)
        if (prog?.pct != null) setPct(prog.pct)
        if (v.status === 'processing') {
          timer = setTimeout(poll, 2000)
        }
      } catch {
        if (!cancelled) setError('Vidéo introuvable.')
      }
    }

    poll()
    return () => { cancelled = true; clearTimeout(timer) }
  }, [slug])

  const shareUrl = `${window.location.origin}/v/${slug}`

  function copyLink() {
    navigator.clipboard.writeText(shareUrl).catch(() => {})
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
      ) : !video ? (
        <div style={{ color: '#555', fontSize: '0.9rem' }}>Chargement…</div>
      ) : (
        <div style={{ width: '100%', maxWidth: 960, display: 'flex', flexDirection: 'column', gap: 16 }}>
          {/* Player */}
          <div style={{ position: 'relative', width: '100%', background: '#000', borderRadius: 12, overflow: 'hidden', boxShadow: '0 8px 40px rgba(0,0,0,0.6)' }}>
            {video.status === 'processing' ? (
              <div style={{ padding: '60px 24px', textAlign: 'center', color: '#888', fontSize: '0.9rem' }}>
                <div style={{ marginBottom: 10, fontSize: '1.4rem' }}>⏳</div>
                <div style={{ marginBottom: 12 }}>
                  Transcription en cours…{pct != null ? ` ${pct}%` : ''} La vidéo sera disponible dans quelques instants.
                </div>
                <div style={{ width: '100%', maxWidth: 300, height: 4, background: 'rgba(255,255,255,0.08)', borderRadius: 2, overflow: 'hidden', margin: '0 auto' }}>
                  <div style={{ height: '100%', width: `${pct ?? 0}%`, background: 'var(--brand-primary, #e07b39)', borderRadius: 2, transition: 'width 0.4s' }} />
                </div>
              </div>
            ) : video.status === 'error' ? (
              <div style={{ padding: '60px 24px', textAlign: 'center', color: '#d13b1a', fontSize: '0.9rem' }}>
                Erreur lors de la transcription de la vidéo.
              </div>
            ) : (
              <video
                ref={videoRef}
                src={`${API_BASE}/uploads/${video.filename}`}
                controls
                playsInline
                autoPlay
                style={{ width: '100%', display: 'block', maxHeight: '75vh' }}
              />
            )}
          </div>

          {/* Info bar */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ color: '#e8e8e8', fontWeight: 600, fontSize: '1rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                {video.title || 'Sans titre'}
              </div>
              <div style={{ color: '#555', fontSize: '0.75rem', marginTop: 2 }}>
                OpenFramework · {new Date(video.created_at).toLocaleDateString('fr-FR', { day: 'numeric', month: 'long', year: 'numeric' })}
              </div>
            </div>

            <button
              id="copy-btn"
              onClick={copyLink}
              style={{
                padding: '8px 18px',
                background: 'var(--brand-primary, #e07b39)',
                color: '#fff',
                border: 'none',
                borderRadius: 8,
                cursor: 'pointer',
                fontWeight: 600,
                fontSize: '0.82rem',
                fontFamily: 'inherit',
                flexShrink: 0,
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
