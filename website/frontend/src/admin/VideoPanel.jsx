import { useState, useEffect, useRef, useCallback } from 'react'
import { api, API_BASE } from './api.js'
import { Upload, Trash2, Copy, Check, Film, Pencil, X } from 'lucide-react'

const btnStyle = {
  display: 'inline-flex', alignItems: 'center', gap: 6,
  padding: '7px 14px', borderRadius: 8, cursor: 'pointer',
  fontFamily: 'inherit', fontSize: '0.8rem', fontWeight: 600,
  border: 'none',
}

function formatSize(bytes) {
  if (!bytes) return ''
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} Ko`
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} Mo`
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} Go`
}

function CopyButton({ slug, filename }) {
  const [copied, setCopied] = useState(false)
  function copy() {
    const url = filename ? `${API_BASE}/uploads/${filename}` : `${window.location.origin}/v/${slug}`
    navigator.clipboard.writeText(url).catch(() => {})
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }
  return (
    <button onClick={copy} title="Copier le lien" style={{ ...btnStyle, background: copied ? '#3e9041' : '#2a2a2a', color: copied ? '#fff' : '#ccc', border: '1px solid rgba(255,255,255,0.08)' }}>
      {copied ? <Check size={14} /> : <Copy size={14} />}
      {copied ? 'Copié !' : 'Lien Discord'}
    </button>
  )
}

export default function VideoPanel() {
  const [videos,            setVideos]           = useState([])
  const [uploading,         setUploading]        = useState(false)
  const [progress,          setProgress]         = useState(0)
  const [dragOver,          setDragOver]         = useState(false)
  const [editingId,         setEditingId]        = useState(null)
  const [editTitle,         setEditTitle]        = useState('')
  const [error,             setError]            = useState('')
  const [transcodeProgress, setTranscodeProgress] = useState({}) // slug → pct
  const fileRef = useRef()

  const load = useCallback(async () => {
    try {
      const data = await api.getVideos()
      setVideos(data)
    } catch {
      setError('Impossible de charger les vidéos.')
    }
  }, [])

  useEffect(() => { load() }, [load])

  // Poll while any video is still transcoding
  useEffect(() => {
    const processing = videos.filter(v => v.status === 'processing')
    if (processing.length === 0) return

    const interval = setInterval(async () => {
      // Fetch progress for each processing video
      const results = await Promise.allSettled(
        processing.map(v => api.getVideoProgress(v.slug).then(r => ({ slug: v.slug, pct: r.pct })))
      )
      const update = {}
      results.forEach(r => { if (r.status === 'fulfilled') update[r.value.slug] = r.value.pct })
      setTranscodeProgress(prev => ({ ...prev, ...update }))
      // Also reload list to detect completion
      load()
    }, 2000)
    return () => clearInterval(interval)
  }, [videos, load])

  async function handleUpload(file) {
    if (!file || !file.type.startsWith('video/')) {
      setError('Seuls les fichiers vidéo sont acceptés.')
      return
    }
    setError('')
    setUploading(true)
    setProgress(0)
    try {
      const title = file.name.replace(/\.[^.]+$/, '')
      await api.uploadVideo(file, title, (loaded, total) => {
        setProgress(total ? Math.round((loaded / total) * 100) : 0)
      })
      await load()
    } catch (err) {
      setError('Erreur upload : ' + err.message)
    } finally {
      setUploading(false)
      setProgress(0)
    }
  }

  function onFileInput(e) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (file) handleUpload(file)
  }

  function onDrop(e) {
    e.preventDefault()
    setDragOver(false)
    const file = e.dataTransfer.files?.[0]
    if (file) handleUpload(file)
  }

  async function handleDelete(slug) {
    if (!confirm('Supprimer cette vidéo ?')) return
    await api.deleteVideo(slug)
    setVideos(v => v.filter(x => x.slug !== slug))
  }

  async function saveRename(slug) {
    if (!editTitle.trim()) return
    await api.renameVideo(slug, { title: editTitle.trim() })
    setVideos(v => v.map(x => x.slug === slug ? { ...x, title: editTitle.trim() } : x))
    setEditingId(null)
  }

  return (
    <div style={{ padding: '28px 32px', maxWidth: 900, margin: '0 auto' }}>
      <h2 style={{ color: '#e8e8e8', fontSize: '1.15rem', fontWeight: 700, marginBottom: 24, display: 'flex', alignItems: 'center', gap: 10 }}>
        <Film size={18} style={{ color: 'var(--brand-primary, #e07b39)' }} />
        Vidéos
      </h2>

      {error && (
        <div style={{ background: 'rgba(209,59,26,0.12)', color: '#d13b1a', border: '1px solid rgba(209,59,26,0.3)', borderRadius: 8, padding: '10px 14px', marginBottom: 16, fontSize: '0.82rem' }}>
          {error}
        </div>
      )}

      {/* Drop zone */}
      <div
        onClick={() => !uploading && fileRef.current?.click()}
        onDragOver={e => { e.preventDefault(); setDragOver(true) }}
        onDragLeave={() => setDragOver(false)}
        onDrop={onDrop}
        style={{
          border: `2px dashed ${dragOver ? 'var(--brand-primary, #e07b39)' : uploading ? '#555' : 'rgba(255,255,255,0.12)'}`,
          borderRadius: 12,
          padding: '36px 24px',
          textAlign: 'center',
          cursor: uploading ? 'default' : 'pointer',
          background: dragOver ? 'rgba(224,123,57,0.06)' : 'rgba(255,255,255,0.02)',
          transition: 'border-color 0.2s, background 0.2s',
          marginBottom: 28,
        }}
      >
        <input ref={fileRef} type="file" accept="video/*" hidden onChange={onFileInput} />
        {uploading ? (
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12 }}>
            <div style={{ color: '#888', fontSize: '0.85rem' }}>Upload en cours… {progress}%</div>
            <div style={{ width: '100%', maxWidth: 300, height: 6, background: '#2a2a2a', borderRadius: 3, overflow: 'hidden' }}>
              <div style={{ height: '100%', width: `${progress}%`, background: 'var(--brand-primary, #e07b39)', borderRadius: 3, transition: 'width 0.15s' }} />
            </div>
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 8 }}>
            <Upload size={28} style={{ color: '#555' }} />
            <div style={{ color: '#888', fontSize: '0.88rem', fontWeight: 600 }}>Glisser une vidéo ou cliquer pour uploader</div>
            <div style={{ color: '#444', fontSize: '0.75rem' }}>MP4, WebM, MOV… — jusqu'à 2 Go</div>
          </div>
        )}
      </div>

      {/* Video list */}
      {videos.length === 0 ? (
        <div style={{ color: '#444', fontSize: '0.85rem', textAlign: 'center', padding: '24px 0' }}>
          Aucune vidéo uploadée.
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {videos.map(v => (
            <div key={v.slug} style={{
              background: '#1e1e1e',
              border: '1px solid rgba(255,255,255,0.07)',
              borderRadius: 10,
              padding: '14px 16px',
              display: 'flex',
              alignItems: 'center',
              gap: 14,
            }}>
              {/* Thumbnail preview */}
              <div style={{ flexShrink: 0, width: 80, height: 48, background: '#111', borderRadius: 6, overflow: 'hidden', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                {v.status === 'processing' ? (
                  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4, padding: '6px 8px', width: '100%' }}>
                    <span style={{ fontSize: '0.6rem', color: 'var(--brand-primary, #e07b39)', fontWeight: 700 }}>
                      {transcodeProgress[v.slug] != null ? `${transcodeProgress[v.slug]}%` : '…'}
                    </span>
                    <div style={{ width: '100%', height: 3, background: 'rgba(255,255,255,0.08)', borderRadius: 2, overflow: 'hidden' }}>
                      <div style={{ height: '100%', width: `${transcodeProgress[v.slug] ?? 0}%`, background: 'var(--brand-primary, #e07b39)', borderRadius: 2, transition: 'width 0.4s' }} />
                    </div>
                  </div>
                ) : v.status === 'error' ? (
                  <span style={{ fontSize: '0.65rem', color: '#d13b1a', textAlign: 'center', padding: '4px' }}>Erreur</span>
                ) : (
                  <video
                    src={`${API_BASE}/uploads/${v.filename}`}
                    style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                    muted
                    preload="metadata"
                  />
                )}
              </div>

              {/* Title + meta */}
              <div style={{ flex: 1, minWidth: 0 }}>
                {editingId === v.slug ? (
                  <div style={{ display: 'flex', gap: 6 }}>
                    <input
                      value={editTitle}
                      onChange={e => setEditTitle(e.target.value)}
                      onKeyDown={e => { if (e.key === 'Enter') saveRename(v.slug); if (e.key === 'Escape') setEditingId(null) }}
                      autoFocus
                      style={{ flex: 1, background: '#111', border: '1px solid #444', borderRadius: 6, padding: '5px 10px', color: '#e8e8e8', fontFamily: 'inherit', fontSize: '0.85rem' }}
                    />
                    <button onClick={() => saveRename(v.slug)} style={{ ...btnStyle, background: '#3e9041', color: '#fff', padding: '5px 12px' }}><Check size={13} /></button>
                    <button onClick={() => setEditingId(null)} style={{ ...btnStyle, background: 'transparent', color: '#888', border: '1px solid rgba(255,255,255,0.1)', padding: '5px 10px' }}><X size={13} /></button>
                  </div>
                ) : (
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                    <span style={{ color: '#e0e0e0', fontWeight: 600, fontSize: '0.88rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{v.title || 'Sans titre'}</span>
                    <button onClick={() => { setEditingId(v.slug); setEditTitle(v.title || '') }} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#555', padding: '2px', display: 'flex' }}>
                      <Pencil size={12} />
                    </button>
                  </div>
                )}
                <div style={{ color: '#444', fontSize: '0.72rem', marginTop: 3, display: 'flex', gap: 10 }}>
                  <span>{new Date(v.created_at).toLocaleDateString('fr-FR')}</span>
                  {v.size > 0 && <span>{formatSize(v.size)}</span>}
                  <a href={`/v/${v.slug}`} target="_blank" rel="noreferrer" style={{ color: '#555', textDecoration: 'none' }}>
                    /v/{v.slug}
                  </a>
                </div>
              </div>

              {/* Actions */}
              <div style={{ display: 'flex', gap: 8, flexShrink: 0 }}>
                <CopyButton slug={v.slug} filename={v.status === 'ready' ? v.filename : null} />
                <button onClick={() => handleDelete(v.slug)} style={{ ...btnStyle, background: 'rgba(209,59,26,0.12)', color: '#d13b1a', border: '1px solid rgba(209,59,26,0.25)' }}>
                  <Trash2 size={14} />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
