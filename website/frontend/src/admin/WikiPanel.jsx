import { useState, useEffect, useCallback, useRef } from 'react'
import { marked } from 'marked'
import { api } from './api.js'
import {
  BookOpen, Plus, Pencil, Trash2, X, Check, Clock,
  Bold, Italic, Strikethrough, Heading2, Heading3,
  List, ListOrdered, Quote, Code, Minus, Download, Upload, Eye, FileText,
  Search, Globe, ToggleLeft, ToggleRight, ChevronDown, ChevronRight,
  SquareCheck, Square,
} from 'lucide-react'

marked.setOptions({ breaks: true, gfm: true })

// ── Constants ──────────────────────────────────────────────────────────────

const CAT_COLORS = [
  '#5865f2', 'var(--brand-primary, #e07b39)', '#3e9041', '#d13b1a', '#9b59b6',
  '#00b5d8', '#f39c12', '#888888', '#e74c3c', '#1abc9c',
]

const TYPE_LABELS = { ingame: 'In-game (public)' }
const TYPE_ICONS  = { ingame: Globe }

const BTN = {
  display: 'inline-flex', alignItems: 'center', gap: 5,
  padding: '6px 13px', borderRadius: 7, cursor: 'pointer',
  fontFamily: 'inherit', fontSize: '0.8rem', fontWeight: 600, border: 'none',
}

function fmtDate(dt) {
  if (!dt) return ''
  return new Date(dt).toLocaleDateString('fr-FR', {
    day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit',
  })
}

// ── Markdown toolbar ───────────────────────────────────────────────────────

function applyFormat(ref, value, onChange, format) {
  const ta = ref.current
  if (!ta) return
  const s = ta.selectionStart, e = ta.selectionEnd
  const sel = value.slice(s, e), pre = value.slice(0, s), post = value.slice(e)

  function wrap(left, right, ph = 'texte') {
    const c = sel || ph
    onChange(`${pre}${left}${c}${right}${post}`)
    const ns = s + left.length
    setTimeout(() => { ta.focus(); ta.setSelectionRange(ns, ns + c.length) }, 0)
  }
  function linePrefix(p) {
    const ls = pre.lastIndexOf('\n') + 1
    onChange(`${pre.slice(0, ls)}${p}${pre.slice(ls)}${sel}${post}`)
    setTimeout(() => { ta.focus(); ta.setSelectionRange(s + p.length, e + p.length) }, 0)
  }

  switch (format) {
    case 'bold':    return wrap('**', '**')
    case 'italic':  return wrap('*', '*')
    case 'strike':  return wrap('~~', '~~')
    case 'h2':      return linePrefix('## ')
    case 'h3':      return linePrefix('### ')
    case 'bullet':  return linePrefix('- ')
    case 'number':  return linePrefix('1. ')
    case 'quote':   return linePrefix('> ')
    case 'code':    return sel.includes('\n') ? wrap('```\n', '\n```', 'code') : wrap('`', '`', 'code')
    case 'hr': {
      onChange(`${pre}\n\n---\n\n${post}`)
      setTimeout(() => { ta.focus(); ta.setSelectionRange(s + 6, s + 6) }, 0)
      break
    }
  }
}

function MdToolbar({ textareaRef, value, onChange }) {
  const [preview, setPreview] = useState(false)
  const tb = (fmt, Icon, title) => (
    <button key={fmt} onMouseDown={e => { e.preventDefault(); applyFormat(textareaRef, value, onChange, fmt) }} title={title}
      style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#666', padding: '4px 6px', display: 'flex', borderRadius: 4, lineHeight: 1 }}>
      <Icon size={13} />
    </button>
  )
  return (
    <div style={{ border: '1px solid #2e2e2e', borderRadius: 8, overflow: 'hidden' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 0, padding: '4px 6px', background: '#111', borderBottom: '1px solid #2e2e2e', flexWrap: 'wrap' }}>
        {tb('bold', Bold, 'Gras')}{tb('italic', Italic, 'Italique')}{tb('strike', Strikethrough, 'Barré')}
        <div style={{ width: 1, height: 14, background: '#333', margin: '0 4px' }} />
        {tb('h2', Heading2, 'H2')}{tb('h3', Heading3, 'H3')}
        <div style={{ width: 1, height: 14, background: '#333', margin: '0 4px' }} />
        {tb('bullet', List, 'Liste')}{tb('number', ListOrdered, 'Numéros')}{tb('quote', Quote, 'Citation')}{tb('code', Code, 'Code')}{tb('hr', Minus, 'Séparateur')}
        <div style={{ flex: 1 }} />
        <button onClick={() => setPreview(p => !p)} style={{
          ...BTN, padding: '3px 9px', fontSize: '0.73rem', gap: 4,
          background: preview ? 'rgba(88,101,242,0.15)' : 'transparent',
          color: preview ? '#5865f2' : '#555',
          border: `1px solid ${preview ? 'rgba(88,101,242,0.3)' : 'transparent'}`,
        }}>
          {preview ? <FileText size={11} /> : <Eye size={11} />}
          {preview ? 'Markdown' : 'Aperçu'}
        </button>
      </div>
      {preview ? (
        <div className="wiki-md" style={{ padding: '12px 14px', minHeight: 120, background: '#0d0d0d' }}
          dangerouslySetInnerHTML={{ __html: value?.trim() ? marked.parse(value) : '<span style="color:#444;font-style:italic">Rien à afficher.</span>' }} />
      ) : (
        <textarea ref={textareaRef} value={value} onChange={e => {
          e.target.style.height = 'auto'; e.target.style.height = e.target.scrollHeight + 'px'; onChange(e.target.value)
        }} placeholder="Écris en Markdown…" rows={6} style={{
          display: 'block', width: '100%', background: '#0d0d0d', border: 'none',
          padding: '10px 12px', color: '#b8b8b8', fontFamily: "'Consolas','Monaco',monospace",
          fontSize: '0.81rem', lineHeight: 1.7, resize: 'none', boxSizing: 'border-box', overflow: 'hidden', outline: 'none',
        }} />
      )}
    </div>
  )
}

// ── Export ─────────────────────────────────────────────────────────────────

function buildMd(data, type) {
  let md = `# Wiki — ${TYPE_LABELS[type]}\n\n`
  for (const cat of (data[type] || [])) {
    md += `## ${cat.name}\n\n`
    for (const a of (cat.articles || [])) {
      if (a.title)   md += `### ${a.title}\n\n`
      if (a.content) md += `${a.content.trim()}\n\n`
    }
  }
  return md.trimEnd()
}

function downloadMd(content, filename) {
  const blob = new Blob([content], { type: 'text/markdown;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url; a.download = filename; a.click()
  URL.revokeObjectURL(url)
}

// ── History Modal ──────────────────────────────────────────────────────────

function HistoryModal({ articleId, articleTitle, onClose }) {
  const [history, setHistory] = useState([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    api.getWikiArticleHistory(articleId).then(h => { setHistory(h); setLoading(false) }).catch(() => setLoading(false))
  }, [articleId])

  return (
    <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.72)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 24 }}
      onClick={e => e.target === e.currentTarget && onClose()}>
      <div style={{ background: '#1a1a1a', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 14, width: '100%', maxWidth: 640, maxHeight: '78vh', display: 'flex', flexDirection: 'column' }}>
        <div style={{ padding: '16px 22px', borderBottom: '1px solid rgba(255,255,255,0.07)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div>
            <div style={{ color: '#e8e8e8', fontWeight: 700, fontSize: '0.92rem' }}>Historique</div>
            <div style={{ color: '#555', fontSize: '0.74rem', marginTop: 2 }}>{articleTitle || 'Sans titre'}</div>
          </div>
          <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#666', display: 'flex', padding: 4 }}><X size={17} /></button>
        </div>
        <div style={{ overflowY: 'auto', flex: 1, padding: '14px 22px' }}>
          {loading ? (
            <div style={{ color: '#555', fontSize: '0.83rem', padding: '24px 0', textAlign: 'center' }}>Chargement…</div>
          ) : history.length === 0 ? (
            <div style={{ color: '#555', fontSize: '0.83rem', padding: '24px 0', textAlign: 'center' }}>Aucun historique.</div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {history.map(h => {
                const old = h.old_data ? JSON.parse(h.old_data) : null
                const nw  = h.new_data ? JSON.parse(h.new_data) : null
                const color = h.action === 'created' ? '#3e9041' : h.action === 'deleted' ? '#d13b1a' : 'var(--brand-primary, #e07b39)'
                const label = h.action === 'created' ? 'Créé' : h.action === 'deleted' ? 'Supprimé' : 'Modifié'
                return (
                  <div key={h.id} style={{ padding: '10px 14px', background: '#111', borderRadius: 8, border: '1px solid rgba(255,255,255,0.05)' }}>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: h.action === 'updated' ? 7 : 0 }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                        <span style={{ fontWeight: 700, fontSize: '0.79rem', color }}>{label}</span>
                        <span style={{ color: '#888', fontSize: '0.76rem' }}>par {h.user_name}</span>
                      </div>
                      <span style={{ color: '#555', fontSize: '0.7rem' }}>{fmtDate(h.changed_at)}</span>
                    </div>
                    {h.action === 'updated' && old && nw && (
                      <div style={{ fontSize: '0.76rem', display: 'flex', flexDirection: 'column', gap: 4 }}>
                        {old.title !== nw.title && (
                          <div style={{ display: 'flex', alignItems: 'baseline', gap: 6, flexWrap: 'wrap' }}>
                            <span style={{ color: '#555' }}>Titre :</span>
                            <span style={{ color: '#c0392b', textDecoration: 'line-through' }}>{old.title || '—'}</span>
                            <span style={{ color: '#555' }}>→</span>
                            <span style={{ color: '#27ae60' }}>{nw.title || '—'}</span>
                          </div>
                        )}
                        {old.content !== nw.content && <span style={{ color: '#666', fontStyle: 'italic' }}>Contenu modifié</span>}
                        {old.published !== nw.published && (
                          <span style={{ color: nw.published ? '#3e9041' : '#d13b1a' }}>{nw.published ? '→ Publié' : '→ Brouillon'}</span>
                        )}
                      </div>
                    )}
                  </div>
                )
              })}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

// ── Import — parse exported .md syntax ────────────────────────────────────
// Supports both simple format (## Cat / ### Art) and rich format
// (## CATÉGORIE : Name, ### Article : Title, metadata lines, ## subtitles in content)

function parseMd(text) {
  const categories = []
  let currentCat = null
  let currentArticle = null

  function pushArticle() {
    if (currentArticle && currentCat) {
      currentCat.articles.push({ ...currentArticle, content: currentArticle.content.trim() })
    }
    currentArticle = null
  }

  const lines = text.split('\n')
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]

    // # Header line — skip
    if (/^# [^#]/.test(line)) continue

    // ## CATÉGORIE : Name  or  ## Name  (category delimiter)
    // Only match as category if prefixed with "CATÉGORIE :" or if not inside an article
    const catMatch = line.match(/^## (?:CAT[ÉE]GORIE\s*:\s*)(.+)/)
    if (catMatch) {
      pushArticle()
      currentCat = { name: catMatch[1].trim(), articles: [], color: null }
      categories.push(currentCat)
      continue
    }

    // ### Article : Title  or  ### Title
    const artMatch = line.match(/^### (?:Article\s*:\s*)?(.+)/)
    if (artMatch) {
      pushArticle()
      currentArticle = { title: artMatch[1].trim(), content: '' }
      continue
    }

    // Metadata lines between category header and first article (or after article header)
    // Skip: "Couleur suggérée : #xxx", "Ordre : N", separator lines "---"
    if (!currentArticle && currentCat) {
      if (/^---\s*$/.test(line)) continue
      const colorMatch = line.match(/^Couleur\s+sugg[ée]r[ée]e\s*:\s*(#[0-9a-fA-F]{3,8})/)
      if (colorMatch) { currentCat.color = colorMatch[1]; continue }
      if (/^Ordre\s*:/.test(line)) continue
      continue // skip any other line before first article in category
    }

    // Inside an article — skip metadata right after article header
    if (currentArticle && currentArticle.content === '') {
      if (/^---\s*$/.test(line)) continue
      if (/^Ordre\s*:\s*\d+/.test(line)) continue
    }

    // ## inside article content — keep as content (subtitle), not a new category
    // This is the key difference: bare ## without CATÉGORIE prefix stays as content
    if (currentArticle) {
      currentArticle.content += line + '\n'
      continue
    }

    // Simple format fallback: bare ## as category when no CATÉGORIE prefix is used anywhere
    // Only triggers when we're not inside an article
    const simpleCatMatch = line.match(/^## (.+)/)
    if (simpleCatMatch && !currentArticle) {
      pushArticle()
      currentCat = { name: simpleCatMatch[1].trim(), articles: [], color: null }
      categories.push(currentCat)
      continue
    }
  }
  pushArticle()

  // Fallback: if no categories found with CATÉGORIE prefix, re-parse with simple format
  if (categories.length === 0) return parseMdSimple(text)

  return categories
}

// Original simple parser as fallback (for files exported by buildMd)
function parseMdSimple(text) {
  const categories = []
  let currentCat = null
  let currentArticle = null

  for (const line of text.split('\n')) {
    const catMatch = line.match(/^## (.+)/)
    if (catMatch) {
      if (currentArticle && currentCat) currentCat.articles.push({ ...currentArticle, content: currentArticle.content.trim() })
      currentArticle = null
      currentCat = { name: catMatch[1].trim(), articles: [], color: null }
      categories.push(currentCat)
      continue
    }
    const artMatch = line.match(/^### (.+)/)
    if (artMatch) {
      if (currentArticle && currentCat) currentCat.articles.push({ ...currentArticle, content: currentArticle.content.trim() })
      currentArticle = { title: artMatch[1].trim(), content: '' }
      continue
    }
    if (/^# /.test(line)) continue
    if (currentArticle) currentArticle.content += line + '\n'
  }
  if (currentArticle && currentCat) currentCat.articles.push({ ...currentArticle, content: currentArticle.content.trim() })

  return categories
}

function ImportModal({ activeType, existingCategories, onCreateCategory, onCreateArticle, onClose, onReload }) {
  const [files, setFiles]         = useState([])
  const [parsed, setParsed]       = useState(null)  // { categories: [...] }
  const [importing, setImporting] = useState(false)
  const [result, setResult]       = useState(null)
  const [parseError, setParseError] = useState('')
  const fileRef = useRef(null)

  async function handleFiles(e) {
    const selected = Array.from(e.target.files).filter(f => f.name.endsWith('.md'))
    setFiles(selected)
    setResult(null)
    setParseError('')

    if (!selected.length) { setParsed(null); return }

    const allCats = []
    for (const file of selected) {
      const text = await file.text()
      const cats = parseMd(text)
      for (const cat of cats) allCats.push(cat)
    }

    if (allCats.length === 0) {
      setParseError('Aucune catégorie trouvée. Formats acceptés : « ## Catégorie » ou « ## CATÉGORIE : Nom » → « ### Article » → contenu.')
      setParsed(null)
      return
    }

    const totalArticles = allCats.reduce((s, c) => s + c.articles.length, 0)
    if (totalArticles === 0) {
      setParseError('Des catégories ont été trouvées mais aucun article (### Titre). Vérifiez le format.')
      setParsed(null)
      return
    }

    setParsed({ categories: allCats })
  }

  async function doImport() {
    if (!parsed) return
    setImporting(true)
    let catCreated = 0, artCreated = 0, errors = 0

    const existingNames = new Map(existingCategories.map(c => [c.name.toLowerCase(), c.id]))

    for (const cat of parsed.categories) {
      let catId = existingNames.get(cat.name.toLowerCase())

      // Create category if it doesn't exist
      if (!catId) {
        try {
          const body = { type: activeType, name: cat.name }
          if (cat.color) body.color = cat.color
          const created = await onCreateCategory(body)
          catId = created.id
          existingNames.set(cat.name.toLowerCase(), catId)
          catCreated++
        } catch { errors++; continue }
      }

      // Create articles
      for (const art of cat.articles) {
        try {
          await onCreateArticle(catId, { title: art.title, content: art.content })
          artCreated++
        } catch { errors++ }
      }
    }

    setImporting(false)
    setResult({ catCreated, artCreated, errors })
    await onReload()
    if (errors === 0) setTimeout(onClose, 1200)
  }

  return (
    <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.72)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 24 }}
      onClick={e => e.target === e.currentTarget && onClose()}>
      <div style={{ background: '#1a1a1a', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 14, width: '100%', maxWidth: 520, display: 'flex', flexDirection: 'column' }}>
        <div style={{ padding: '16px 22px', borderBottom: '1px solid rgba(255,255,255,0.07)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ color: '#e8e8e8', fontWeight: 700, fontSize: '0.92rem' }}>Importer .md</div>
          <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#666', display: 'flex', padding: 4 }}><X size={17} /></button>
        </div>
        <div style={{ padding: '18px 22px', display: 'flex', flexDirection: 'column', gap: 16 }}>

          {/* Format hint */}
          <div style={{ background: 'rgba(88,101,242,0.06)', border: '1px solid rgba(88,101,242,0.15)', borderRadius: 8, padding: '10px 14px', fontSize: '0.76rem', color: '#8b98f5' }}>
            Format : <code style={{ background: 'rgba(255,255,255,0.08)', padding: '1px 5px', borderRadius: 3, fontSize: '0.73rem' }}>## Catégorie</code> puis <code style={{ background: 'rgba(255,255,255,0.08)', padding: '1px 5px', borderRadius: 3, fontSize: '0.73rem' }}>### Article</code> puis le contenu. Supporte aussi le format enrichi avec <code style={{ background: 'rgba(255,255,255,0.08)', padding: '1px 5px', borderRadius: 3, fontSize: '0.73rem' }}>## CATÉGORIE : Nom</code> et métadonnées (couleur, ordre).
          </div>

          {/* File picker */}
          <div>
            <input ref={fileRef} type="file" accept=".md" multiple onChange={handleFiles} style={{ display: 'none' }} />
            <button onClick={() => fileRef.current?.click()}
              style={{ ...BTN, background: 'rgba(255,255,255,0.05)', color: '#b0b0b0', border: '1px dashed rgba(255,255,255,0.15)', width: '100%', justifyContent: 'center', padding: '12px' }}>
              <Upload size={14} /> {files.length ? `${files.length} fichier${files.length > 1 ? 's' : ''} sélectionné${files.length > 1 ? 's' : ''}` : 'Choisir des fichiers .md'}
            </button>
          </div>

          {/* Parse error */}
          {parseError && (
            <div style={{ fontSize: '0.8rem', padding: '8px 12px', borderRadius: 6, background: 'rgba(209,59,26,0.1)', color: '#d13b1a' }}>
              {parseError}
            </div>
          )}

          {/* Preview */}
          {parsed && (
            <div style={{ maxHeight: 220, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 8 }}>
              {parsed.categories.map((cat, i) => {
                const exists = existingCategories.some(c => c.name.toLowerCase() === cat.name.toLowerCase())
                return (
                  <div key={i} style={{ background: '#111', borderRadius: 8, padding: '10px 14px', border: '1px solid rgba(255,255,255,0.05)' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: cat.articles.length ? 6 : 0 }}>
                      <span style={{ color: '#d0d0d0', fontWeight: 700, fontSize: '0.83rem' }}>{cat.name}</span>
                      <span style={{
                        fontSize: '0.65rem', fontWeight: 600, padding: '1px 7px', borderRadius: 4,
                        background: exists ? 'rgba(255,255,255,0.05)' : 'rgba(88,101,242,0.12)',
                        color: exists ? '#666' : '#7c8af5',
                        border: `1px solid ${exists ? 'rgba(255,255,255,0.08)' : 'rgba(88,101,242,0.25)'}`,
                      }}>
                        {exists ? 'existante' : 'nouvelle'}
                      </span>
                      <span style={{ color: '#555', fontSize: '0.72rem' }}>{cat.articles.length} article{cat.articles.length > 1 ? 's' : ''}</span>
                    </div>
                    {cat.articles.map((a, j) => (
                      <div key={j} style={{ color: '#888', fontSize: '0.75rem', paddingLeft: 12, display: 'flex', alignItems: 'center', gap: 6, marginTop: 3 }}>
                        <FileText size={10} /> {a.title}
                      </div>
                    ))}
                  </div>
                )
              })}
            </div>
          )}

          {/* Result */}
          {result && (
            <div style={{ fontSize: '0.8rem', padding: '8px 12px', borderRadius: 6, background: result.errors ? 'rgba(209,59,26,0.1)' : 'rgba(62,144,65,0.1)', color: result.errors ? '#d13b1a' : '#3e9041' }}>
              {result.catCreated > 0 && <>{result.catCreated} catégorie{result.catCreated > 1 ? 's' : ''} créée{result.catCreated > 1 ? 's' : ''} · </>}
              {result.artCreated} article{result.artCreated > 1 ? 's' : ''} importé{result.artCreated > 1 ? 's' : ''}
              {result.errors > 0 && ` · ${result.errors} erreur${result.errors > 1 ? 's' : ''}`}
            </div>
          )}

          {/* Actions */}
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
            <button onClick={onClose} style={{ ...BTN, background: 'transparent', color: '#888', border: '1px solid rgba(255,255,255,0.1)' }}>Annuler</button>
            <button onClick={doImport} disabled={!parsed || importing}
              style={{ ...BTN, background: '#5865f2', color: '#fff', opacity: (!parsed || importing) ? 0.5 : 1 }}>
              <Upload size={13} /> {importing ? 'Import en cours…' : 'Importer'}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}

// ── Article row ────────────────────────────────────────────────────────────

function ArticleRow({ article, onUpdate, onToggle, onDelete, expanded, onToggleExpand, selected, onToggleSelect }) {
  const [editing, setEditing]   = useState(false)
  const [title, setTitle]       = useState(article.title)
  const [content, setContent]   = useState(article.content)
  const [historyOpen, setHistoryOpen] = useState(false)
  const [saving, setSaving]     = useState(false)
  const textareaRef = useRef(null)

  useEffect(() => { if (!editing) { setTitle(article.title); setContent(article.content) } }, [article.title, article.content, editing])
  useEffect(() => { if (editing && textareaRef.current) { const ta = textareaRef.current; ta.style.height = 'auto'; ta.style.height = ta.scrollHeight + 'px' } }, [editing])

  async function save() {
    setSaving(true)
    try { await onUpdate(article.id, { title, content }); setEditing(false) }
    finally { setSaving(false) }
  }

  return (
    <div style={{ borderLeft: `2px solid ${article.published ? 'rgba(62,144,65,0.4)' : 'rgba(255,255,255,0.06)'}`, paddingLeft: 16, marginBottom: 16 }}>
      {editing ? (
        <div>
          <input value={title} onChange={e => setTitle(e.target.value)} placeholder="Titre de l'article" autoFocus
            onKeyDown={e => { if (e.key === 'Escape') { setEditing(false); setTitle(article.title); setContent(article.content) } }}
            style={{ width: '100%', background: '#111', border: '1px solid #3a3a3a', borderRadius: 6, padding: '7px 10px', color: '#e8e8e8', fontFamily: 'inherit', fontSize: '0.9rem', fontWeight: 700, marginBottom: 10, boxSizing: 'border-box', outline: 'none' }} />
          <MdToolbar textareaRef={textareaRef} value={content} onChange={setContent} />
          <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
            <button onClick={save} disabled={saving} style={{ ...BTN, background: '#3e9041', color: '#fff', opacity: saving ? 0.6 : 1 }}>
              <Check size={13} /> {saving ? 'Enregistrement…' : 'Enregistrer'}
            </button>
            <button onClick={() => { setEditing(false); setTitle(article.title); setContent(article.content) }}
              style={{ ...BTN, background: 'transparent', color: '#888', border: '1px solid rgba(255,255,255,0.1)' }}>
              <X size={13} /> Annuler
            </button>
          </div>
        </div>
      ) : (
        <div>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: expanded ? 6 : 0, cursor: 'pointer', userSelect: 'none' }}
            onClick={onToggleExpand}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <button onClick={e => { e.stopPropagation(); onToggleSelect(article.id) }}
                style={{ background: 'none', border: 'none', cursor: 'pointer', padding: 0, display: 'flex', color: selected ? '#5865f2' : '#333', flexShrink: 0 }}>
                {selected ? <SquareCheck size={15} /> : <Square size={15} />}
              </button>
              {expanded ? <ChevronDown size={14} style={{ color: '#666', flexShrink: 0 }} /> : <ChevronRight size={14} style={{ color: '#444', flexShrink: 0 }} />}
              <div style={{ color: '#d0d0d0', fontWeight: 700, fontSize: '0.9rem' }}>
                {article.title || <span style={{ color: '#444', fontStyle: 'italic', fontWeight: 400 }}>Sans titre</span>}
              </div>
              {article.slug && <span style={{ color: '#444', fontSize: '0.7rem', fontFamily: 'monospace' }}>/{article.slug}</span>}
              <span style={{
                fontSize: '0.65rem', fontWeight: 600, padding: '1px 7px', borderRadius: 4,
                background: article.published ? 'rgba(62,144,65,0.15)' : 'rgba(255,255,255,0.05)',
                color: article.published ? '#3e9041' : '#555',
                border: `1px solid ${article.published ? 'rgba(62,144,65,0.3)' : 'rgba(255,255,255,0.08)'}`,
              }}>
                {article.published ? 'Publié' : 'Brouillon'}
              </span>
            </div>
            <div style={{ display: 'flex', gap: 2 }} onClick={e => e.stopPropagation()}>
              <button onClick={() => onToggle(article.id)} title={article.published ? 'Dépublier' : 'Publier'}
                style={{ background: 'none', border: 'none', cursor: 'pointer', color: article.published ? '#3e9041' : '#555', padding: 4, display: 'flex', borderRadius: 4 }}>
                {article.published ? <ToggleRight size={14} /> : <ToggleLeft size={14} />}
              </button>
              <button onClick={() => setHistoryOpen(true)} title="Historique"
                style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#555', padding: 4, display: 'flex', borderRadius: 4 }}><Clock size={12} /></button>
              <button onClick={() => setEditing(true)} title="Modifier"
                style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#555', padding: 4, display: 'flex', borderRadius: 4 }}><Pencil size={12} /></button>
              <button onClick={() => onDelete(article.id)} title="Supprimer"
                style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#6b3030', padding: 4, display: 'flex', borderRadius: 4 }}><Trash2 size={12} /></button>
            </div>
          </div>
          {expanded && (
            article.content ? (
              <div className="wiki-md" style={{ paddingLeft: 22 }} dangerouslySetInnerHTML={{ __html: marked.parse(article.content) }} />
            ) : (
              <div style={{ color: '#444', fontSize: '0.78rem', fontStyle: 'italic', paddingLeft: 22 }}>Aucun contenu.</div>
            )
          )}
        </div>
      )}
      {historyOpen && <HistoryModal articleId={article.id} articleTitle={article.title} onClose={() => setHistoryOpen(false)} />}
    </div>
  )
}

// ── New article form ───────────────────────────────────────────────────────

function NewArticleForm({ onSubmit, onCancel }) {
  const [title, setTitle] = useState('')
  const [content, setContent] = useState('')
  const textareaRef = useRef(null)
  return (
    <div style={{ borderLeft: '2px dashed rgba(88,101,242,0.3)', paddingLeft: 16, marginBottom: 16 }}>
      <input value={title} onChange={e => setTitle(e.target.value)} placeholder="Titre de l'article" autoFocus
        onKeyDown={e => { if (e.key === 'Escape') onCancel() }}
        style={{ width: '100%', background: '#111', border: '1px solid #3a3a3a', borderRadius: 6, padding: '7px 10px', color: '#e8e8e8', fontFamily: 'inherit', fontSize: '0.9rem', fontWeight: 700, marginBottom: 10, boxSizing: 'border-box', outline: 'none' }} />
      <MdToolbar textareaRef={textareaRef} value={content} onChange={setContent} />
      <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
        <button onClick={() => { if (title.trim() || content.trim()) onSubmit({ title: title.trim(), content: content.trim() }) }}
          style={{ ...BTN, background: '#5865f2', color: '#fff' }}><Plus size={13} /> Ajouter</button>
        <button onClick={onCancel} style={{ ...BTN, background: 'transparent', color: '#888', border: '1px solid rgba(255,255,255,0.1)' }}>Annuler</button>
      </div>
    </div>
  )
}

// ── Category section ───────────────────────────────────────────────────────

function CategorySection({ cat, onUpdateCat, onDeleteCat, onCreateArticle, onUpdateArticle, onToggleArticle, onDeleteArticle, collapsed, onToggleCollapse, selected, onToggleSelect, onToggleSelectAll }) {
  const [editingCat, setEditingCat] = useState(false)
  const [catName, setCatName] = useState(cat.name)
  const [catColor, setCatColor] = useState(cat.color)
  const [adding, setAdding] = useState(false)
  const [expandedArticle, setExpandedArticle] = useState(null)

  async function saveCat() {
    if (!catName.trim()) return
    await onUpdateCat(cat.id, { name: catName.trim(), color: catColor })
    setEditingCat(false)
  }

  const published = (cat.articles || []).filter(a => a.published).length
  const total = (cat.articles || []).length

  return (
    <div style={{ marginBottom: 36 }}>
      {editingCat ? (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 16, flexWrap: 'wrap' }}>
          <input value={catName} onChange={e => setCatName(e.target.value)} autoFocus
            onKeyDown={e => { if (e.key === 'Enter') saveCat(); if (e.key === 'Escape') { setEditingCat(false); setCatName(cat.name); setCatColor(cat.color) } }}
            style={{ background: '#111', border: '1px solid #3a3a3a', borderRadius: 6, padding: '6px 10px', color: '#e8e8e8', fontFamily: 'inherit', fontSize: '1rem', fontWeight: 700, outline: 'none', minWidth: 160 }} />
          <div style={{ display: 'flex', gap: 5 }}>
            {CAT_COLORS.map(c => (
              <button key={c} onClick={() => setCatColor(c)} style={{ width: 18, height: 18, borderRadius: '50%', background: c, padding: 0, cursor: 'pointer', border: catColor === c ? '2px solid #fff' : '2px solid transparent' }} />
            ))}
          </div>
          <button onClick={saveCat} style={{ ...BTN, background: '#3e9041', color: '#fff', padding: '5px 10px' }}><Check size={12} /></button>
          <button onClick={() => { setEditingCat(false); setCatName(cat.name); setCatColor(cat.color) }}
            style={{ ...BTN, background: 'transparent', color: '#666', border: '1px solid rgba(255,255,255,0.1)', padding: '5px 10px' }}><X size={12} /></button>
        </div>
      ) : (
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: collapsed ? 0 : 16, cursor: 'pointer', userSelect: 'none' }}
          onClick={onToggleCollapse}>
          <div style={{ flex: 1, display: 'flex', alignItems: 'center', gap: 10 }}>
            {(() => {
              const articleIds = (cat.articles || []).map(a => a.id)
              const allSelected = articleIds.length > 0 && articleIds.every(id => selected.has(id))
              const someSelected = !allSelected && articleIds.some(id => selected.has(id))
              return (
                <button onClick={e => { e.stopPropagation(); onToggleSelectAll(cat.id) }}
                  style={{ background: 'none', border: 'none', cursor: 'pointer', padding: 0, display: 'flex', color: allSelected ? '#5865f2' : someSelected ? '#5865f288' : '#333', flexShrink: 0 }}>
                  {allSelected || someSelected ? <SquareCheck size={16} /> : <Square size={16} />}
                </button>
              )
            })()}
            {collapsed ? <ChevronRight size={16} style={{ color: '#555', flexShrink: 0 }} /> : <ChevronDown size={16} style={{ color: '#888', flexShrink: 0 }} />}
            <div style={{ height: 2, width: 18, background: cat.color, borderRadius: 1, flexShrink: 0 }} />
            <span style={{ color: '#e0e0e0', fontWeight: 700, fontSize: '1.05rem' }}>{cat.name}</span>
            <span style={{ color: '#444', fontSize: '0.72rem' }}>{published}/{total} publié{published !== 1 ? 's' : ''}</span>
          </div>
          <div style={{ display: 'flex', gap: 2 }} onClick={e => e.stopPropagation()}>
            <button onClick={() => setEditingCat(true)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#555', padding: 4, display: 'flex', borderRadius: 4 }}><Pencil size={12} /></button>
            <button onClick={() => onDeleteCat(cat.id)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#6b3030', padding: 4, display: 'flex', borderRadius: 4 }}><Trash2 size={12} /></button>
          </div>
        </div>
      )}

      {!collapsed && (
        <>
          <div style={{ height: 1, background: `linear-gradient(90deg, ${cat.color}44 0%, rgba(255,255,255,0.04) 100%)`, marginBottom: 16 }} />

          {total === 0 && !adding && (
            <div style={{ color: '#383838', fontSize: '0.8rem', fontStyle: 'italic', marginBottom: 12, paddingLeft: 16 }}>Aucun article dans cette catégorie.</div>
          )}
          {(cat.articles || []).map(a => (
            <ArticleRow key={a.id} article={a} onUpdate={onUpdateArticle} onToggle={onToggleArticle} onDelete={onDeleteArticle}
              expanded={expandedArticle === a.id} onToggleExpand={() => setExpandedArticle(prev => prev === a.id ? null : a.id)}
              selected={selected.has(a.id)} onToggleSelect={onToggleSelect} />
          ))}
          {adding && <NewArticleForm onSubmit={async v => { await onCreateArticle(cat.id, v); setAdding(false) }} onCancel={() => setAdding(false)} />}
          {!adding && (
            <button onClick={() => setAdding(true)}
              style={{ ...BTN, background: 'transparent', color: '#555', border: '1px dashed rgba(255,255,255,0.1)', fontSize: '0.77rem', padding: '4px 12px' }}>
              <Plus size={11} /> Ajouter un article
            </button>
          )}
        </>
      )}
    </div>
  )
}

// ── Main Panel ─────────────────────────────────────────────────────────────

export default function WikiPanel() {
  const [activeType, setActiveType] = useState('ingame')
  const [data, setData]   = useState({ ingame: [], dev: [] })
  const [loading, setLoading] = useState(true)
  const [error, setError]     = useState('')
  const [search, setSearch]   = useState('')

  const [addingCat, setAddingCat]       = useState(false)
  const [newCatName, setNewCatName]     = useState('')
  const [newCatColor, setNewCatColor]   = useState('#5865f2')
  const [importOpen, setImportOpen]     = useState(false)
  const [collapsedCats, setCollapsedCats] = useState({})
  const [selected, setSelected]         = useState(new Set())  // article IDs
  const [bulkBusy, setBulkBusy]         = useState(false)

  const load = useCallback(async () => {
    try { setData(await api.getWiki()) }
    catch { setError('Impossible de charger le wiki.') }
    finally { setLoading(false) }
  }, [])
  useEffect(() => { load() }, [load])

  // ── Category CRUD
  async function createCategory() {
    if (!newCatName.trim()) return
    try {
      const cat = await api.createWikiCategory({ type: activeType, name: newCatName.trim(), color: newCatColor })
      setData(d => ({ ...d, [activeType]: [...d[activeType], { ...cat, articles: [] }] }))
      setAddingCat(false); setNewCatName(''); setNewCatColor('#5865f2')
    } catch (e) { setError(e.message) }
  }
  async function updateCategory(id, values) {
    try {
      const cat = await api.updateWikiCategory(id, values)
      setData(d => ({ ...d, [activeType]: d[activeType].map(c => c.id === id ? { ...c, ...cat } : c) }))
    } catch (e) { setError(e.message) }
  }
  async function deleteCategory(id) {
    if (!confirm('Supprimer cette catégorie et tous ses articles ?')) return
    try {
      await api.deleteWikiCategory(id)
      setData(d => ({ ...d, [activeType]: d[activeType].filter(c => c.id !== id) }))
    } catch (e) { setError(e.message) }
  }

  // ── Article CRUD
  async function createArticle(catId, values) {
    try {
      const article = await api.createWikiArticle(catId, values)
      setData(d => ({ ...d, [activeType]: d[activeType].map(c => c.id === catId ? { ...c, articles: [...(c.articles || []), article] } : c) }))
    } catch (e) { setError(e.message) }
  }
  async function updateArticle(id, values) {
    try {
      const updated = await api.updateWikiArticle(id, values)
      setData(d => ({ ...d, [activeType]: d[activeType].map(c => ({ ...c, articles: (c.articles || []).map(a => a.id === id ? updated : a) })) }))
    } catch (e) { setError(e.message) }
  }
  async function toggleArticle(id) {
    try {
      const updated = await api.toggleWikiArticle(id)
      setData(d => ({ ...d, [activeType]: d[activeType].map(c => ({ ...c, articles: (c.articles || []).map(a => a.id === id ? updated : a) })) }))
    } catch (e) { setError(e.message) }
  }
  async function deleteArticle(id) {
    if (!confirm('Supprimer cet article définitivement ?')) return
    try {
      await api.deleteWikiArticle(id)
      setData(d => ({ ...d, [activeType]: d[activeType].map(c => ({ ...c, articles: (c.articles || []).filter(a => a.id !== id) })) }))
    } catch (e) { setError(e.message) }
  }

  // ── Selection helpers
  function toggleSelect(articleId) {
    setSelected(prev => {
      const next = new Set(prev)
      next.has(articleId) ? next.delete(articleId) : next.add(articleId)
      return next
    })
  }
  function toggleSelectAll(catId) {
    const cat = (data[activeType] || []).find(c => c.id === catId)
    if (!cat) return
    const articleIds = (cat.articles || []).map(a => a.id)
    setSelected(prev => {
      const next = new Set(prev)
      const allSelected = articleIds.every(id => next.has(id))
      articleIds.forEach(id => allSelected ? next.delete(id) : next.add(id))
      return next
    })
  }

  // ── Bulk actions
  async function bulkPublish(publish) {
    if (!selected.size) return
    const label = publish ? 'publier' : 'dépublier'
    if (!confirm(`${publish ? 'Publier' : 'Dépublier'} ${selected.size} article${selected.size > 1 ? 's' : ''} ?`)) return
    setBulkBusy(true)
    let errors = 0
    for (const id of selected) {
      try {
        // Only toggle if current state differs from target
        const article = (data[activeType] || []).flatMap(c => c.articles || []).find(a => a.id === id)
        if (article && article.published !== (publish ? 1 : 0)) {
          const updated = await api.toggleWikiArticle(id)
          setData(d => ({ ...d, [activeType]: d[activeType].map(c => ({ ...c, articles: (c.articles || []).map(a => a.id === id ? updated : a) })) }))
        }
      } catch { errors++ }
    }
    setBulkBusy(false)
    setSelected(new Set())
    if (errors) setError(`${errors} erreur${errors > 1 ? 's' : ''} lors de l'opération.`)
  }

  async function bulkDelete() {
    if (!selected.size) return
    if (!confirm(`Supprimer définitivement ${selected.size} article${selected.size > 1 ? 's' : ''} ?`)) return
    setBulkBusy(true)
    let errors = 0
    for (const id of selected) {
      try {
        await api.deleteWikiArticle(id)
        setData(d => ({ ...d, [activeType]: d[activeType].map(c => ({ ...c, articles: (c.articles || []).filter(a => a.id !== id) })) }))
      } catch { errors++ }
    }
    setBulkBusy(false)
    setSelected(new Set())
    if (errors) setError(`${errors} erreur${errors > 1 ? 's' : ''} lors de la suppression.`)
  }

  // ── Search filter
  const filteredCats = (data[activeType] || []).map(cat => {
    if (!search.trim()) return cat
    const q = search.toLowerCase()
    if (cat.name.toLowerCase().includes(q)) return cat
    const filtered = (cat.articles || []).filter(a => a.title.toLowerCase().includes(q) || a.content.toLowerCase().includes(q))
    return filtered.length ? { ...cat, articles: filtered } : null
  }).filter(Boolean)

  return (
    <div style={{ padding: '28px 32px', maxWidth: 860, margin: '0 auto' }}>

      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 22 }}>
        <h2 style={{ color: '#e8e8e8', fontSize: '1.15rem', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 10, margin: 0 }}>
          <BookOpen size={18} style={{ color: '#5865f2' }} /> Wiki
        </h2>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <div style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
            <Search size={13} style={{ position: 'absolute', left: 10, color: '#555', pointerEvents: 'none' }} />
            <input value={search} onChange={e => setSearch(e.target.value)} placeholder="Rechercher…"
              style={{ background: 'rgba(255,255,255,0.04)', border: '1px solid rgba(255,255,255,0.08)', borderRadius: 7, padding: '6px 10px 6px 30px', color: '#b0b0b0', fontFamily: 'inherit', fontSize: '0.78rem', outline: 'none', width: 180 }} />
          </div>
          <button onClick={() => setImportOpen(true)}
            style={{ ...BTN, background: 'rgba(255,255,255,0.05)', color: '#888', border: '1px solid rgba(255,255,255,0.1)', fontSize: '0.78rem' }}>
            <Upload size={13} /> Importer .md
          </button>
          <button onClick={() => downloadMd(buildMd(data, activeType), `wiki-${activeType}.md`)}
            style={{ ...BTN, background: 'rgba(255,255,255,0.05)', color: '#888', border: '1px solid rgba(255,255,255,0.1)', fontSize: '0.78rem' }}>
            <Download size={13} /> Exporter .md
          </button>
        </div>
      </div>

      {error && (
        <div style={{ background: 'rgba(209,59,26,0.12)', color: '#d13b1a', border: '1px solid rgba(209,59,26,0.3)', borderRadius: 8, padding: '10px 14px', marginBottom: 16, fontSize: '0.82rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          {error}
          <button onClick={() => setError('')} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#d13b1a', padding: 2 }}><X size={14} /></button>
        </div>
      )}

      {/* Tabs — masqués tant qu'il n'y a qu'un type (dev sorti dans DocsPanel) */}
      {Object.keys(TYPE_LABELS).length > 1 && (
        <div style={{ display: 'flex', gap: 4, marginBottom: 32, background: '#1a1a1a', padding: 4, borderRadius: 10, border: '1px solid rgba(255,255,255,0.06)', width: 'fit-content' }}>
          {Object.entries(TYPE_LABELS).map(([type, label]) => {
            const Icon = TYPE_ICONS[type]
            return (
              <button key={type} onClick={() => { setActiveType(type); setAddingCat(false); setSearch(''); setSelected(new Set()) }}
                style={{ ...BTN, background: activeType === type ? '#5865f2' : 'transparent', color: activeType === type ? '#fff' : '#888', padding: '6px 18px' }}>
                <Icon size={13} /> {label}
              </button>
            )
          })}
        </div>
      )}

      {/* Info banner */}
      {activeType === 'ingame' && (
        <div style={{ background: 'rgba(88,101,242,0.06)', border: '1px solid rgba(88,101,242,0.15)', borderRadius: 8, padding: '10px 14px', marginBottom: 20, fontSize: '0.78rem', color: '#8b98f5', display: 'flex', alignItems: 'center', gap: 8 }}>
          <Globe size={14} />
          Les articles publiés sont accessibles in-game via <code style={{ background: 'rgba(255,255,255,0.08)', padding: '1px 6px', borderRadius: 3, fontFamily: 'monospace', fontSize: '0.75rem' }}>/api/wiki/public</code> et recherchables via <code style={{ background: 'rgba(255,255,255,0.08)', padding: '1px 6px', borderRadius: 3, fontFamily: 'monospace', fontSize: '0.75rem' }}>/api/wiki/public/search?q=…</code>
        </div>
      )}

      {/* Bulk action bar */}
      {selected.size > 0 && (
        <div style={{
          background: 'rgba(88,101,242,0.08)', border: '1px solid rgba(88,101,242,0.25)', borderRadius: 10,
          padding: '10px 16px', marginBottom: 16, display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap',
        }}>
          <span style={{ color: '#8b98f5', fontSize: '0.82rem', fontWeight: 600 }}>
            {selected.size} article{selected.size > 1 ? 's' : ''} sélectionné{selected.size > 1 ? 's' : ''}
          </span>
          <div style={{ flex: 1 }} />
          <button onClick={() => bulkPublish(true)} disabled={bulkBusy}
            style={{ ...BTN, background: 'rgba(62,144,65,0.15)', color: '#3e9041', border: '1px solid rgba(62,144,65,0.3)', fontSize: '0.77rem', opacity: bulkBusy ? 0.5 : 1 }}>
            <ToggleRight size={13} /> Publier
          </button>
          <button onClick={() => bulkPublish(false)} disabled={bulkBusy}
            style={{ ...BTN, background: 'rgba(255,255,255,0.05)', color: '#888', border: '1px solid rgba(255,255,255,0.1)', fontSize: '0.77rem', opacity: bulkBusy ? 0.5 : 1 }}>
            <ToggleLeft size={13} /> Dépublier
          </button>
          <button onClick={bulkDelete} disabled={bulkBusy}
            style={{ ...BTN, background: 'rgba(209,59,26,0.12)', color: '#d13b1a', border: '1px solid rgba(209,59,26,0.3)', fontSize: '0.77rem', opacity: bulkBusy ? 0.5 : 1 }}>
            <Trash2 size={13} /> Supprimer
          </button>
          <button onClick={() => setSelected(new Set())}
            style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#555', padding: 4, display: 'flex' }}>
            <X size={14} />
          </button>
        </div>
      )}

      {loading ? (
        <div style={{ color: '#555', fontSize: '0.85rem', textAlign: 'center', padding: '40px 0' }}>Chargement…</div>
      ) : (
        <>
          {filteredCats.length === 0 && !addingCat && (
            <div style={{ color: '#444', fontSize: '0.85rem', textAlign: 'center', padding: '48px 0' }}>
              {search ? 'Aucun résultat.' : 'Aucune catégorie. Créez-en une pour commencer.'}
            </div>
          )}

          {filteredCats.map(cat => (
            <CategorySection key={cat.id} cat={cat}
              collapsed={!!collapsedCats[cat.id]}
              onToggleCollapse={() => setCollapsedCats(prev => ({ ...prev, [cat.id]: !prev[cat.id] }))}
              onUpdateCat={updateCategory} onDeleteCat={deleteCategory}
              onCreateArticle={createArticle} onUpdateArticle={updateArticle}
              onToggleArticle={toggleArticle} onDeleteArticle={deleteArticle}
              selected={selected} onToggleSelect={toggleSelect} onToggleSelectAll={toggleSelectAll} />
          ))}

          {addingCat ? (
            <div style={{ background: '#1a1a1a', border: '1px solid rgba(255,255,255,0.09)', borderRadius: 10, padding: '16px 18px', marginTop: 8 }}>
              <div style={{ color: '#888', fontSize: '0.78rem', fontWeight: 600, marginBottom: 10, textTransform: 'uppercase', letterSpacing: '0.05em' }}>Nouvelle catégorie</div>
              <input value={newCatName} onChange={e => setNewCatName(e.target.value)} placeholder="Nom de la catégorie…" autoFocus
                onKeyDown={e => { if (e.key === 'Enter') createCategory(); if (e.key === 'Escape') { setAddingCat(false); setNewCatName('') } }}
                style={{ width: '100%', background: '#111', border: '1px solid #3a3a3a', borderRadius: 6, padding: '8px 10px', color: '#e8e8e8', fontFamily: 'inherit', fontSize: '0.92rem', fontWeight: 700, marginBottom: 12, boxSizing: 'border-box', outline: 'none' }} />
              <div style={{ display: 'flex', gap: 6, marginBottom: 14 }}>
                {CAT_COLORS.map(c => (
                  <button key={c} onClick={() => setNewCatColor(c)} style={{
                    width: 22, height: 22, borderRadius: '50%', background: c, padding: 0, cursor: 'pointer',
                    border: newCatColor === c ? '2px solid #fff' : '2px solid transparent',
                    outline: newCatColor === c ? `2px solid ${c}` : 'none',
                  }} />
                ))}
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
                <button onClick={createCategory} style={{ ...BTN, background: '#3e9041', color: '#fff' }}><Check size={13} /> Créer</button>
                <button onClick={() => { setAddingCat(false); setNewCatName('') }}
                  style={{ ...BTN, background: 'transparent', color: '#888', border: '1px solid rgba(255,255,255,0.1)' }}>Annuler</button>
              </div>
            </div>
          ) : (
            <button onClick={() => setAddingCat(true)}
              style={{ ...BTN, background: 'rgba(255,255,255,0.03)', color: '#666', border: '1px dashed rgba(255,255,255,0.12)', width: '100%', justifyContent: 'center', padding: '12px', marginTop: filteredCats.length > 0 ? 8 : 0 }}>
              <Plus size={14} /> Ajouter une catégorie
            </button>
          )}
        </>
      )}

      {importOpen && (
        <ImportModal
          activeType={activeType}
          existingCategories={data[activeType] || []}
          onCreateCategory={body => api.createWikiCategory(body)}
          onCreateArticle={(catId, body) => api.createWikiArticle(catId, body)}
          onReload={load}
          onClose={() => setImportOpen(false)}
        />
      )}
    </div>
  )
}
