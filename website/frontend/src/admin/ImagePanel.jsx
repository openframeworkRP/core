import { useState, useEffect, useRef, useCallback } from 'react'
import { api, API_BASE } from './api.js'
import { Upload, Trash2, Copy, Check, ImageIcon, Pencil, X } from 'lucide-react'

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
    const url = filename ? `${API_BASE}/uploads/${filename}` : `${window.location.origin}/i/${slug}`
    navigator.clipboard.writeText(url).catch(() => {})
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }
  return (
    <button onClick={copy} title="Copier le lien" style={{ ...btnStyle, background: copied ? '#3e9041' : '#2a2f3e', color: copied ? '#fff' : '#ccc', border: '1px solid rgba(255,255,255,0.08)' }}>
      {copied ? <Check size={14} /> : <Copy size={14} />}
      {copied ? 'Copié !' : 'Lien Discord'}
    </button>
  )
}

export default function ImagePanel() {
  const [images,    setImages]    = useState([])
  const [uploading, setUploading] = useState(false)
  const [progress,  setProgress]  = useState(0)
  const [dragOver,  setDragOver]  = useState(false)
  const [editingId, setEditingId] = useState(null)
  const [editTitle, setEditTitle] = useState('')
  const [error,     setError]     = useState('')
  const fileRef = useRef()

  const load = useCallback(async () => {
    try {
      const data = await api.getImages()
      setImages(data)
    } catch {
      setError('Impossible de charger les images.')
    }
  }, [])

  useEffect(() => { load() }, [load])

  async function handleUpload(file) {
    if (!file || !file.type.startsWith('image/')) {
      setError('Seuls les fichiers image sont acceptés.')
      return
    }
    setError('')
    setUploading(true)
    setProgress(0)
    try {
      const title = file.name.replace(/\.[^.]+$/, '')
      await api.uploadImage(file, title, (loaded, total) => {
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
    if (!confirm('Supprimer cette image ?')) return
    await api.deleteImage(slug)
    setImages(v => v.filter(x => x.slug !== slug))
  }

  async function saveRename(slug) {
    if (!editTitle.trim()) return
    await api.renameImage(slug, { title: editTitle.trim() })
    setImages(v => v.map(x => x.slug === slug ? { ...x, title: editTitle.trim() } : x))
    setEditingId(null)
  }

  return (
    <div style={{ padding: '28px 32px', maxWidth: 900, margin: '0 auto' }}>
      <h2 style={{ color: '#e8e8e8', fontSize: '1.15rem', fontWeight: 700, marginBottom: 24, display: 'flex', alignItems: 'center', gap: 10 }}>
        <ImageIcon size={18} style={{ color: 'var(--brand-primary, #e07b39)' }} />
        Images
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
          background: dragOver ? 'rgba(60, 173, 217,0.06)' : 'rgba(255,255,255,0.02)',
          transition: 'border-color 0.2s, background 0.2s',
          marginBottom: 28,
        }}
      >
        <input ref={fileRef} type="file" accept="image/*" hidden onChange={onFileInput} />
        {uploading ? (
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12 }}>
            <div style={{ color: '#888', fontSize: '0.85rem' }}>Upload en cours… {progress}%</div>
            <div style={{ width: '100%', maxWidth: 300, height: 6, background: '#2a2f3e', borderRadius: 3, overflow: 'hidden' }}>
              <div style={{ height: '100%', width: `${progress}%`, background: 'var(--brand-primary, #e07b39)', borderRadius: 3, transition: 'width 0.15s' }} />
            </div>
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 8 }}>
            <Upload size={28} style={{ color: '#555' }} />
            <div style={{ color: '#888', fontSize: '0.88rem', fontWeight: 600 }}>Glisser une image ou cliquer pour uploader</div>
            <div style={{ color: '#444', fontSize: '0.75rem' }}>PNG, JPG, GIF, WebP, SVG… — jusqu'à 50 Mo (converti en WebP)</div>
          </div>
        )}
      </div>

      {/* Image list */}
      {images.length === 0 ? (
        <div style={{ color: '#444', fontSize: '0.85rem', textAlign: 'center', padding: '24px 0' }}>
          Aucune image uploadée.
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {images.map(v => (
            <div key={v.slug} style={{
              background: '#1a1f2c',
              border: '1px solid rgba(255,255,255,0.07)',
              borderRadius: 10,
              padding: '14px 16px',
              display: 'flex',
              alignItems: 'center',
              gap: 14,
            }}>
              {/* Thumbnail preview */}
              <div style={{ flexShrink: 0, width: 80, height: 48, background: '#11151f', borderRadius: 6, overflow: 'hidden', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <img
                  src={`${API_BASE}/uploads/${v.filename}`}
                  alt={v.title || ''}
                  style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                  loading="lazy"
                />
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
                      style={{ flex: 1, background: '#11151f', border: '1px solid #444', borderRadius: 6, padding: '5px 10px', color: '#e8e8e8', fontFamily: 'inherit', fontSize: '0.85rem' }}
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
                  {v.width > 0 && v.height > 0 && <span>{v.width}×{v.height}</span>}
                  <a href={`/i/${v.slug}`} target="_blank" rel="noreferrer" style={{ color: '#555', textDecoration: 'none' }}>
                    /i/{v.slug}
                  </a>
                </div>
              </div>

              {/* Actions */}
              <div style={{ display: 'flex', gap: 8, flexShrink: 0 }}>
                <CopyButton slug={v.slug} filename={v.filename} />
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
