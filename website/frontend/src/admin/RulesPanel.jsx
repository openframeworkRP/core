import { useState, useEffect, useCallback, useRef } from 'react'
import { api } from './api.js'
import {
  ScrollText, Plus, Pencil, Trash2, X, Check, Clock,
  Bold, Italic, Strikethrough, Heading2, Heading3,
  List, ListOrdered, Quote, Code, Minus, Download, Eye, FileText,
} from 'lucide-react'
import '../components/RulesBook.css'

// ── Constants ──────────────────────────────────────────────────────────────

const CAT_COLORS = [
  '#5865f2', 'var(--brand-primary, #e07b39)', '#3e9041', '#d13b1a', '#9b59b6',
  '#00b5d8', '#f39c12', '#888888', '#e74c3c', '#1abc9c',
]

const TYPE_LABELS = { server: 'Serveur', job: 'Par métier', theme: 'Par thème' }
const TYPE_SLUGS  = { server: 'serveur', job: 'par-metier', theme: 'par-theme' }

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

// ── Markdown toolbar helper ────────────────────────────────────────────────

function applyFormat(ref, value, onChange, format) {
  const ta = ref.current
  if (!ta) return

  const s   = ta.selectionStart
  const e   = ta.selectionEnd
  const sel = value.slice(s, e)
  const pre = value.slice(0, s)
  const post = value.slice(e)

  function wrap(left, right, placeholder = 'texte') {
    const content = sel || placeholder
    const newVal  = `${pre}${left}${content}${right}${post}`
    onChange(newVal)
    const ns = s + left.length
    const ne = ns + content.length
    setTimeout(() => { ta.focus(); ta.setSelectionRange(ns, ne) }, 0)
  }

  function linePrefix(prefix) {
    const lineStart = pre.lastIndexOf('\n') + 1
    const newVal = `${pre.slice(0, lineStart)}${prefix}${pre.slice(lineStart)}${sel}${post}`
    onChange(newVal)
    const offset = prefix.length
    setTimeout(() => { ta.focus(); ta.setSelectionRange(s + offset, e + offset) }, 0)
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
    case 'code': {
      if (sel.includes('\n')) return wrap('```\n', '\n```', 'code')
      return wrap('`', '`', 'code')
    }
    case 'hr': {
      const newVal = `${pre}\n\n---\n\n${post}`
      onChange(newVal)
      const ns = s + 6
      setTimeout(() => { ta.focus(); ta.setSelectionRange(ns, ns) }, 0)
      break
    }
    case 'art': {
      const snippet = `\n:::art 1 Titre de l'article\n${sel || 'Texte de la règle.'}\n:::\n`
      const newVal = `${pre}${snippet}${post}`
      onChange(newVal)
      const ns = pre.length + snippet.length
      setTimeout(() => { ta.focus(); ta.setSelectionRange(ns, ns) }, 0)
      break
    }
    case 'note': {
      const snippet = `\n:::note\n${sel || 'Texte de la note.'}\n:::\n`
      const newVal = `${pre}${snippet}${post}`
      onChange(newVal)
      const ns = pre.length + snippet.length
      setTimeout(() => { ta.focus(); ta.setSelectionRange(ns, ns) }, 0)
      break
    }
    case 'table': {
      const snippet = `\n:::table\nColonne 1 | Colonne 2 | Colonne 3\n---\nValeur A  | Valeur B  | Valeur C\nValeur D  | Valeur E  | Valeur F\n:::\n`
      const newVal = `${pre}${snippet}${post}`
      onChange(newVal)
      const ns = pre.length + snippet.length
      setTimeout(() => { ta.focus(); ta.setSelectionRange(ns, ns) }, 0)
      break
    }
  }
}

// ── Parser Markdown custom (même logique que RulesBook) ─────────────────────

function parseMd(raw) {
  if (!raw?.trim()) return []
  const blocks = []
  const lines = raw.split('\n')
  let i = 0

  // Collecte les lignes jusqu'au ::: de fermeture correspondant (gère l'imbrication)
  function collectBlock() {
    const content = []
    let depth = 1
    while (i < lines.length) {
      const l = lines[i]
      if (/^:::/.test(l.trim()) && l.trim() !== ':::') depth++
      else if (l.trim() === ':::') { depth--; if (depth === 0) { i++; break } }
      content.push(l)
      i++
    }
    return content
  }

  while (i < lines.length) {
    const line = lines[i]
    if (/^:::art\s/.test(line)) {
      const match = line.match(/^:::art\s+(\S+)\s+(.+)$/)
      const number = match?.[1] || '?'
      const title  = match?.[2] || ''
      i++
      const content = collectBlock()
      blocks.push({ type: 'rule', number, title, text: content.join('\n').trim() })
      continue
    }
    if (/^:::table/.test(line)) {
      i++
      const rows = []
      while (i < lines.length && lines[i].trim() !== ':::') { if (lines[i].trim() !== '') rows.push(lines[i]); i++ }
      const headers = rows[0]?.split('|').map(c => c.trim()) || []
      const dataRows = rows.slice(1).filter(r => !/^[-|\s]+$/.test(r)).map(r => r.split('|').map(c => c.trim()))
      blocks.push({ type: 'table', headers, rows: dataRows })
      i++; continue
    }
    if (/^:::note/.test(line)) {
      i++
      const content = collectBlock()
      blocks.push({ type: 'note', text: content.join('\n').trim() })
      continue
    }
    if (/^#{1,3}\s/.test(line)) {
      blocks.push({ type: 'heading', text: line.replace(/^#{1,3}\s/, '').trim() })
      i++; continue
    }
    if (/^[-*]\s/.test(line)) {
      const items = []
      while (i < lines.length && /^[-*]\s/.test(lines[i])) { items.push(lines[i].replace(/^[-*]\s/, '').trim()); i++ }
      blocks.push({ type: 'list', items }); continue
    }
    if (line.trim() === '') { i++; continue }
    if (/^:::/.test(line)) { i++; continue }
    const para = []
    while (i < lines.length && lines[i].trim() !== '' && !/^#{1,3}\s/.test(lines[i]) && !/^[-*]\s/.test(lines[i]) && !/^:::/.test(lines[i])) { para.push(lines[i]); i++ }
    if (para.length) blocks.push({ type: 'paragraph', text: para.join('\n').trim() })
  }
  return blocks
}

function renderInline(text) {
  if (!text) return null
  const parts = []
  const re = /\*\*(.+?)\*\*|\*(.+?)\*|~~(.+?)~~|`([^`]+)`/g
  let last = 0, m
  while ((m = re.exec(text)) !== null) {
    if (m.index > last) parts.push(text.slice(last, m.index))
    if (m[1] !== undefined) parts.push(<strong key={m.index}>{m[1]}</strong>)
    else if (m[2] !== undefined) parts.push(<em key={m.index}>{m[2]}</em>)
    else if (m[3] !== undefined) parts.push(<s key={m.index}>{m[3]}</s>)
    else if (m[4] !== undefined) parts.push(<code key={m.index}>{m[4]}</code>)
    last = re.lastIndex
  }
  if (last < text.length) parts.push(text.slice(last))
  return parts
}

function RenderBlocks({ blocks }) {
  return <>
    {blocks.map((block, i) => {
      if (block.type === 'heading')
        return <h2 key={i} className="rb__heading">{renderInline(block.text)}</h2>
      if (block.type === 'paragraph')
        return <p key={i} className="rb__paragraph">{renderInline(block.text)}</p>
      if (block.type === 'note')
        return <div key={i} className="rb__note"><p>{renderInline(block.text)}</p></div>
      if (block.type === 'list')
        return <ul key={i} className="rb__list">{block.items.map((item, j) => <li key={j}>{renderInline(item)}</li>)}</ul>
      if (block.type === 'table')
        return (
          <div key={i} className="rb__table-wrapper">
            <table className="rb__table">
              {block.headers?.length > 0 && <thead><tr>{block.headers.map((h, j) => <th key={j}>{renderInline(h)}</th>)}</tr></thead>}
              <tbody>{block.rows.map((row, ri) => <tr key={ri}>{row.map((cell, ci) => <td key={ci}>{renderInline(cell)}</td>)}</tr>)}</tbody>
            </table>
          </div>
        )
      if (block.type === 'rule')
        return (
          <div key={i} className="rb__rule">
            <div className="rb__rule-header">
              <span className="rb__rule-num">Art. {block.number}</span>
              <strong className="rb__rule-title">{renderInline(block.title)}</strong>
            </div>
            <div className="rb__rule-text">
              <RenderBlocks blocks={parseMd(block.text)} />
            </div>
          </div>
        )
      return null
    })}
  </>
}

function MdPreview({ value }) {
  const blocks = parseMd(value)
  if (!blocks.length) return <span style={{ color: '#444', fontStyle: 'italic', fontSize: '0.82rem' }}>Rien à afficher.</span>
  return (
    <div className="rb__chapter-body">
      <RenderBlocks blocks={blocks} />
    </div>
  )
}

// ── Markdown Toolbar ───────────────────────────────────────────────────────

const SYNTAX_CHEATSHEET = [
  { label: '## Titre',            desc: 'Section / titre de chapitre' },
  { label: ':::art N Nom\n…\n:::', desc: 'Article numéroté (N = numéro)' },
  { label: ':::note\n…\n:::',     desc: 'Note / avertissement encadré' },
  { label: ':::table\nA | B\n---\n1 | 2\n:::', desc: 'Tableau (colonnes séparées par |)' },
  { label: '- item',              desc: 'Élément de liste' },
  { label: '**texte**',           desc: 'Gras' },
  { label: '*texte*',             desc: 'Italique' },
]

function MdToolbar({ textareaRef, value, onChange }) {
  const [previewMode, setPreviewMode] = useState(false)
  const [cheatOpen, setCheatOpen]     = useState(false)

  const tb = (format, Icon, title) => (
    <button
      key={format}
      onMouseDown={e => { e.preventDefault(); applyFormat(textareaRef, value, onChange, format) }}
      title={title}
      style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#666', padding: '4px 6px', display: 'flex', borderRadius: 4, lineHeight: 1 }}
    >
      <Icon size={13} />
    </button>
  )

  const tbText = (format, label, title) => (
    <button
      key={format}
      onMouseDown={e => { e.preventDefault(); applyFormat(textareaRef, value, onChange, format) }}
      title={title}
      style={{ background: 'rgba(255,255,255,0.04)', border: '1px solid rgba(255,255,255,0.08)', borderRadius: 4, cursor: 'pointer', color: '#888', padding: '2px 7px', fontSize: '0.68rem', fontFamily: 'monospace', lineHeight: 1.4, fontWeight: 600 }}
    >
      {label}
    </button>
  )

  return (
    <div style={{ border: '1px solid #2e2e2e', borderRadius: 8, overflow: 'hidden' }}>
      {/* Toolbar */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 2, padding: '4px 6px', background: '#111', borderBottom: '1px solid #2e2e2e', flexWrap: 'wrap' }}>
        {tb('bold',   Bold,         'Gras')}
        {tb('italic', Italic,       'Italique')}
        {tb('strike', Strikethrough,'Barré')}
        <div style={{ width: 1, height: 14, background: '#333', margin: '0 4px' }} />
        {tb('h2',     Heading2,     'Titre ## Titre')}
        {tb('h3',     Heading3,     'Titre ###')}
        <div style={{ width: 1, height: 14, background: '#333', margin: '0 4px' }} />
        {tb('bullet', List,         'Liste - item')}
        {tb('number', ListOrdered,  'Liste numérotée')}
        {tb('quote',  Quote,        'Citation')}
        {tb('code',   Code,         'Code')}
        {tb('hr',     Minus,        'Séparateur')}
        <div style={{ width: 1, height: 14, background: '#333', margin: '0 4px' }} />
        {tbText('art',   ':::art',   'Insérer un article :::art N Titre')}
        {tbText('note',  ':::note',  'Insérer une note :::note')}
        {tbText('table', ':::table', 'Insérer un tableau :::table')}
        <div style={{ flex: 1 }} />
        {/* Cheatsheet */}
        <div style={{ position: 'relative' }}>
          <button
            onMouseDown={e => { e.preventDefault(); setCheatOpen(p => !p) }}
            title="Aide syntaxe"
            style={{ ...BTN, padding: '3px 7px', fontSize: '0.72rem', gap: 4, background: cheatOpen ? 'rgba(255,255,255,0.07)' : 'transparent', color: cheatOpen ? '#ccc' : '#555', border: '1px solid transparent' }}
          >
            ?
          </button>
          {cheatOpen && (
            <div style={{ position: 'absolute', right: 0, top: '110%', zIndex: 50, background: '#1a1a1a', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 8, padding: '10px 14px', minWidth: 310, boxShadow: '0 8px 32px rgba(0,0,0,0.5)' }}>
              <div style={{ fontSize: '0.68rem', fontWeight: 800, letterSpacing: '0.1em', textTransform: 'uppercase', color: '#555', marginBottom: 8 }}>Syntaxe</div>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.76rem' }}>
                <tbody>
                  {SYNTAX_CHEATSHEET.map((row, i) => (
                    <tr key={i} style={{ borderBottom: '1px solid rgba(255,255,255,0.04)' }}>
                      <td style={{ padding: '5px 10px 5px 0', fontFamily: 'monospace', color: 'var(--brand-primary, #e07b39)', whiteSpace: 'pre', verticalAlign: 'top' }}>{row.label}</td>
                      <td style={{ padding: '5px 0', color: '#666', lineHeight: 1.4 }}>{row.desc}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
        {/* Preview toggle */}
        <button
          onClick={() => setPreviewMode(p => !p)}
          style={{
            ...BTN, padding: '3px 9px', fontSize: '0.73rem', gap: 4,
            background: previewMode ? 'rgba(224,123,57,0.15)' : 'transparent',
            color: previewMode ? 'var(--brand-primary, #e07b39)' : '#555',
            border: `1px solid ${previewMode ? 'rgba(224,123,57,0.3)' : 'transparent'}`,
          }}
        >
          {previewMode ? <FileText size={11} /> : <Eye size={11} />}
          {previewMode ? 'Markdown' : 'Aperçu'}
        </button>
      </div>

      {/* Editor / Preview */}
      {previewMode ? (
        <div style={{ padding: '12px 20px', minHeight: 120, background: '#0d0d0d' }}>
          <MdPreview value={value} />
        </div>
      ) : (
        <textarea
          ref={textareaRef}
          value={value}
          onChange={e => {
            onChange(e.target.value)
            const ta = e.target
            requestAnimationFrame(() => {
              ta.style.height = 'auto'
              ta.style.height = ta.scrollHeight + 'px'
            })
          }}
          placeholder="Écris en Markdown…&#10;&#10;**gras**, *italique*, ## Titre, - liste, > citation, `code`…"
          rows={6}
          style={{
            display: 'block', width: '100%', background: '#0d0d0d', border: 'none',
            padding: '10px 12px', color: '#b8b8b8', fontFamily: "'Consolas','Monaco',monospace",
            fontSize: '0.81rem', lineHeight: 1.7, resize: 'none', boxSizing: 'border-box',
            overflow: 'hidden', outline: 'none',
          }}
        />
      )}
    </div>
  )
}

// ── Export helpers ─────────────────────────────────────────────────────────

function buildMd(data, type) {
  const typeName = TYPE_LABELS[type]
  let md = `# Règles — ${typeName}\n\n`
  for (const cat of (data[type] || [])) {
    md += `## ${cat.name}\n\n`
    for (const rule of (cat.rules || [])) {
      if (rule.title)   md += `### ${rule.title}\n\n`
      if (rule.content) md += `${rule.content.trim()}\n\n`
    }
  }
  return md.trimEnd()
}

function downloadMd(content, filename) {
  const blob = new Blob([content], { type: 'text/markdown;charset=utf-8' })
  const url  = URL.createObjectURL(blob)
  const a    = document.createElement('a')
  a.href = url; a.download = filename; a.click()
  URL.revokeObjectURL(url)
}

// ── History Modal ──────────────────────────────────────────────────────────

function HistoryModal({ ruleId, ruleTitle, onClose }) {
  const [history, setHistory] = useState([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    api.getRuleItemHistory(ruleId)
      .then(h => { setHistory(h); setLoading(false) })
      .catch(() => setLoading(false))
  }, [ruleId])

  return (
    <div
      style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.72)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 24 }}
      onClick={e => e.target === e.currentTarget && onClose()}
    >
      <div style={{ background: '#1a1a1a', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 14, width: '100%', maxWidth: 640, maxHeight: '78vh', display: 'flex', flexDirection: 'column' }}>
        <div style={{ padding: '16px 22px', borderBottom: '1px solid rgba(255,255,255,0.07)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div>
            <div style={{ color: '#e8e8e8', fontWeight: 700, fontSize: '0.92rem' }}>Historique</div>
            <div style={{ color: '#555', fontSize: '0.74rem', marginTop: 2 }}>{ruleTitle || 'Sans titre'}</div>
          </div>
          <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#666', display: 'flex', padding: 4 }}>
            <X size={17} />
          </button>
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
                const label = h.action === 'created' ? 'Créée' : h.action === 'deleted' ? 'Supprimée' : 'Modifiée'
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
                            <span style={{ color: '#555', flexShrink: 0 }}>Titre :</span>
                            <span style={{ color: '#c0392b', textDecoration: 'line-through', maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{old.title || '—'}</span>
                            <span style={{ color: '#555' }}>→</span>
                            <span style={{ color: '#27ae60', maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{nw.title || '—'}</span>
                          </div>
                        )}
                        {old.content !== nw.content && (
                          <span style={{ color: '#666', fontStyle: 'italic' }}>Contenu modifié</span>
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

// ── Rule row (in document view) ────────────────────────────────────────────

function RuleRow({ rule, onUpdate, onDelete }) {
  const [editing, setEditing]         = useState(false)
  const [title, setTitle]             = useState(rule.title)
  const [content, setContent]         = useState(rule.content)
  const [historyOpen, setHistoryOpen] = useState(false)
  const [saving, setSaving]           = useState(false)
  const textareaRef = useRef(null)

  useEffect(() => {
    if (!editing) { setTitle(rule.title); setContent(rule.content) }
  }, [rule.title, rule.content, editing])

  useEffect(() => {
    if (editing && textareaRef.current) {
      const ta = textareaRef.current
      ta.style.height = 'auto'
      ta.style.height = ta.scrollHeight + 'px'
    }
  }, [editing])

  async function save() {
    setSaving(true)
    try { await onUpdate(rule.id, { title, content }); setEditing(false) }
    finally { setSaving(false) }
  }

  return (
    <div style={{ borderLeft: '2px solid rgba(255,255,255,0.06)', paddingLeft: 16, marginBottom: 16 }}>
      {editing ? (
        <div>
          <input
            value={title}
            onChange={e => setTitle(e.target.value)}
            placeholder="Titre de la règle"
            autoFocus
            onKeyDown={e => { if (e.key === 'Escape') { setEditing(false); setTitle(rule.title); setContent(rule.content) } }}
            style={{
              width: '100%', background: '#111', border: '1px solid #3a3a3a', borderRadius: 6,
              padding: '7px 10px', color: '#e8e8e8', fontFamily: 'inherit', fontSize: '0.9rem',
              fontWeight: 700, marginBottom: 10, boxSizing: 'border-box', outline: 'none',
            }}
          />
          <MdToolbar textareaRef={textareaRef} value={content} onChange={setContent} />
          <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
            <button onClick={save} disabled={saving} style={{ ...BTN, background: '#3e9041', color: '#fff', opacity: saving ? 0.6 : 1 }}>
              <Check size={13} /> {saving ? 'Enregistrement…' : 'Enregistrer'}
            </button>
            <button onClick={() => { setEditing(false); setTitle(rule.title); setContent(rule.content) }}
              style={{ ...BTN, background: 'transparent', color: '#888', border: '1px solid rgba(255,255,255,0.1)' }}>
              <X size={13} /> Annuler
            </button>
          </div>
        </div>
      ) : (
        <div>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 6 }}>
            <div style={{ color: '#d0d0d0', fontWeight: 700, fontSize: '0.9rem' }}>
              {rule.title || <span style={{ color: '#444', fontStyle: 'italic', fontWeight: 400 }}>Sans titre</span>}
            </div>
            <div style={{ display: 'flex', gap: 2 }}>
              <button onClick={() => setHistoryOpen(true)} title="Historique"
                style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#555', padding: 4, display: 'flex', borderRadius: 4 }}>
                <Clock size={12} />
              </button>
              <button onClick={() => setEditing(true)} title="Modifier"
                style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#555', padding: 4, display: 'flex', borderRadius: 4 }}>
                <Pencil size={12} />
              </button>
              <button onClick={() => onDelete(rule.id)} title="Supprimer"
                style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#6b3030', padding: 4, display: 'flex', borderRadius: 4 }}>
                <Trash2 size={12} />
              </button>
            </div>
          </div>
          {rule.content ? (
            <MdPreview value={rule.content} />
          ) : (
            <div style={{ color: '#444', fontSize: '0.78rem', fontStyle: 'italic' }}>Aucun contenu.</div>
          )}
        </div>
      )}
      {historyOpen && (
        <HistoryModal ruleId={rule.id} ruleTitle={rule.title} onClose={() => setHistoryOpen(false)} />
      )}
    </div>
  )
}

// ── New rule inline form ────────────────────────────────────────────────────

function NewRuleForm({ onSubmit, onCancel }) {
  const [title, setTitle]     = useState('')
  const [content, setContent] = useState('')
  const textareaRef = useRef(null)

  return (
    <div style={{ borderLeft: '2px dashed rgba(88,101,242,0.3)', paddingLeft: 16, marginBottom: 16 }}>
      <input
        value={title}
        onChange={e => setTitle(e.target.value)}
        placeholder="Titre de la règle"
        autoFocus
        onKeyDown={e => { if (e.key === 'Escape') onCancel() }}
        style={{
          width: '100%', background: '#111', border: '1px solid #3a3a3a', borderRadius: 6,
          padding: '7px 10px', color: '#e8e8e8', fontFamily: 'inherit', fontSize: '0.9rem',
          fontWeight: 700, marginBottom: 10, boxSizing: 'border-box', outline: 'none',
        }}
      />
      <MdToolbar textareaRef={textareaRef} value={content} onChange={setContent} />
      <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
        <button
          onClick={() => { if (title.trim() || content.trim()) onSubmit({ title: title.trim(), content: content.trim() }) }}
          style={{ ...BTN, background: '#5865f2', color: '#fff' }}
        >
          <Plus size={13} /> Ajouter
        </button>
        <button onClick={onCancel} style={{ ...BTN, background: 'transparent', color: '#888', border: '1px solid rgba(255,255,255,0.1)' }}>
          Annuler
        </button>
      </div>
    </div>
  )
}

// ── Category section ────────────────────────────────────────────────────────

function CategorySection({ cat, onUpdateCat, onDeleteCat, onCreateRule, onUpdateRule, onDeleteRule }) {
  const [editingCat, setEditingCat]   = useState(false)
  const [catName, setCatName]         = useState(cat.name)
  const [catColor, setCatColor]       = useState(cat.color)
  const [addingRule, setAddingRule]   = useState(false)

  async function saveCat() {
    if (!catName.trim()) return
    await onUpdateCat(cat.id, { name: catName.trim(), color: catColor })
    setEditingCat(false)
  }

  async function createRule(values) {
    await onCreateRule(cat.id, values)
    setAddingRule(false)
  }

  return (
    <div style={{ marginBottom: 36 }}>
      {/* Category heading */}
      {editingCat ? (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 16, flexWrap: 'wrap' }}>
          <input
            value={catName}
            onChange={e => setCatName(e.target.value)}
            autoFocus
            onKeyDown={e => { if (e.key === 'Enter') saveCat(); if (e.key === 'Escape') { setEditingCat(false); setCatName(cat.name); setCatColor(cat.color) } }}
            style={{
              background: '#111', border: '1px solid #3a3a3a', borderRadius: 6,
              padding: '6px 10px', color: '#e8e8e8', fontFamily: 'inherit',
              fontSize: '1rem', fontWeight: 700, outline: 'none', minWidth: 160,
            }}
          />
          <div style={{ display: 'flex', gap: 5 }}>
            {CAT_COLORS.map(c => (
              <button key={c} onClick={() => setCatColor(c)} style={{
                width: 18, height: 18, borderRadius: '50%', background: c, padding: 0, cursor: 'pointer',
                border: catColor === c ? '2px solid #fff' : '2px solid transparent',
              }} />
            ))}
          </div>
          <button onClick={saveCat} style={{ ...BTN, background: '#3e9041', color: '#fff', padding: '5px 10px' }}><Check size={12} /></button>
          <button onClick={() => { setEditingCat(false); setCatName(cat.name); setCatColor(cat.color) }}
            style={{ ...BTN, background: 'transparent', color: '#666', border: '1px solid rgba(255,255,255,0.1)', padding: '5px 10px' }}><X size={12} /></button>
        </div>
      ) : (
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 16 }}>
          <div style={{ flex: 1, display: 'flex', alignItems: 'center', gap: 10 }}>
            <div style={{ height: 2, width: 18, background: cat.color, borderRadius: 1, flexShrink: 0 }} />
            <span style={{ color: '#e0e0e0', fontWeight: 700, fontSize: '1.05rem', letterSpacing: '-0.01em' }}>{cat.name}</span>
            <span style={{ color: '#444', fontSize: '0.72rem' }}>{(cat.rules || []).length} règle{(cat.rules || []).length !== 1 ? 's' : ''}</span>
          </div>
          <div style={{ display: 'flex', gap: 2 }}>
            <button onClick={() => setEditingCat(true)}
              style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#555', padding: 4, display: 'flex', borderRadius: 4 }}>
              <Pencil size={12} />
            </button>
            <button onClick={() => onDeleteCat(cat.id)}
              style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#6b3030', padding: 4, display: 'flex', borderRadius: 4 }}>
              <Trash2 size={12} />
            </button>
          </div>
        </div>
      )}

      {/* Divider under heading */}
      <div style={{ height: 1, background: `linear-gradient(90deg, ${cat.color}44 0%, rgba(255,255,255,0.04) 100%)`, marginBottom: 16 }} />

      {/* Rules */}
      {(cat.rules || []).length === 0 && !addingRule && (
        <div style={{ color: '#383838', fontSize: '0.8rem', fontStyle: 'italic', marginBottom: 12, paddingLeft: 16 }}>Aucune règle dans cette catégorie.</div>
      )}
      {(cat.rules || []).map(rule => (
        <RuleRow
          key={rule.id}
          rule={rule}
          onUpdate={onUpdateRule}
          onDelete={onDeleteRule}
        />
      ))}
      {addingRule && (
        <NewRuleForm onSubmit={createRule} onCancel={() => setAddingRule(false)} />
      )}

      {!addingRule && (
        <button
          onClick={() => setAddingRule(true)}
          style={{ ...BTN, background: 'transparent', color: '#555', border: '1px dashed rgba(255,255,255,0.1)', fontSize: '0.77rem', padding: '4px 12px' }}
        >
          <Plus size={11} /> Ajouter une règle
        </button>
      )}
    </div>
  )
}

// ── Main Panel ─────────────────────────────────────────────────────────────

export default function RulesPanel() {
  const [activeType, setActiveType] = useState('server')
  const [data, setData]             = useState({ server: [], job: [], theme: [] })
  const [loading, setLoading]       = useState(true)
  const [error, setError]           = useState('')

  const [addingCat, setAddingCat]     = useState(false)
  const [newCatName, setNewCatName]   = useState('')
  const [newCatColor, setNewCatColor] = useState('#5865f2')

  const load = useCallback(async () => {
    try {
      setData(await api.getRules())
    } catch {
      setError('Impossible de charger les règles.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  // ── Category CRUD ──

  async function createCategory() {
    if (!newCatName.trim()) return
    try {
      const cat = await api.createRuleCategory({ type: activeType, name: newCatName.trim(), color: newCatColor })
      setData(d => ({ ...d, [activeType]: [...d[activeType], { ...cat, rules: [] }] }))
      setAddingCat(false); setNewCatName(''); setNewCatColor('#5865f2')
    } catch (e) { setError(e.message) }
  }

  async function updateCategory(id, values) {
    try {
      const cat = await api.updateRuleCategory(id, values)
      setData(d => ({ ...d, [activeType]: d[activeType].map(c => c.id === id ? { ...c, ...cat } : c) }))
    } catch (e) { setError(e.message) }
  }

  async function deleteCategory(id) {
    if (!confirm('Supprimer cette catégorie et toutes ses règles ?')) return
    try {
      await api.deleteRuleCategory(id)
      setData(d => ({ ...d, [activeType]: d[activeType].filter(c => c.id !== id) }))
    } catch (e) { setError(e.message) }
  }

  // ── Rule CRUD ──

  async function createRule(catId, values) {
    try {
      const rule = await api.createRule(catId, values)
      setData(d => ({
        ...d,
        [activeType]: d[activeType].map(c => c.id === catId ? { ...c, rules: [...(c.rules || []), rule] } : c),
      }))
    } catch (e) { setError(e.message) }
  }

  async function updateRule(ruleId, values) {
    try {
      const updated = await api.updateRule(ruleId, values)
      setData(d => ({
        ...d,
        [activeType]: d[activeType].map(c => ({
          ...c, rules: (c.rules || []).map(r => r.id === ruleId ? updated : r),
        })),
      }))
    } catch (e) { setError(e.message) }
  }

  async function deleteRule(ruleId) {
    if (!confirm('Supprimer cette règle définitivement ?')) return
    try {
      await api.deleteRule(ruleId)
      setData(d => ({
        ...d,
        [activeType]: d[activeType].map(c => ({
          ...c, rules: (c.rules || []).filter(r => r.id !== ruleId),
        })),
      }))
    } catch (e) { setError(e.message) }
  }

  // ── Render ──

  const cats = data[activeType] || []

  return (
    <div style={{ padding: '28px 32px', maxWidth: 860, margin: '0 auto' }}>

      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 22 }}>
        <h2 style={{ color: '#e8e8e8', fontSize: '1.15rem', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 10, margin: 0 }}>
          <ScrollText size={18} style={{ color: 'var(--brand-primary, #e07b39)' }} />
          Règles
        </h2>
        <button
          onClick={() => downloadMd(buildMd(data, activeType), `regles-${TYPE_SLUGS[activeType]}.md`)}
          style={{ ...BTN, background: 'rgba(255,255,255,0.05)', color: '#888', border: '1px solid rgba(255,255,255,0.1)', fontSize: '0.78rem' }}
        >
          <Download size={13} /> Exporter .md
        </button>
      </div>

      {error && (
        <div style={{ background: 'rgba(209,59,26,0.12)', color: '#d13b1a', border: '1px solid rgba(209,59,26,0.3)', borderRadius: 8, padding: '10px 14px', marginBottom: 16, fontSize: '0.82rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          {error}
          <button onClick={() => setError('')} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#d13b1a', padding: 2 }}><X size={14} /></button>
        </div>
      )}

      {/* Type tabs */}
      <div style={{ display: 'flex', gap: 4, marginBottom: 32, background: '#1a1a1a', padding: 4, borderRadius: 10, border: '1px solid rgba(255,255,255,0.06)', width: 'fit-content' }}>
        {Object.entries(TYPE_LABELS).map(([type, label]) => (
          <button
            key={type}
            onClick={() => { setActiveType(type); setAddingCat(false); setNewCatName('') }}
            style={{ ...BTN, background: activeType === type ? 'var(--brand-primary, #e07b39)' : 'transparent', color: activeType === type ? '#fff' : '#888', padding: '6px 18px' }}
          >
            {label}
          </button>
        ))}
      </div>

      {loading ? (
        <div style={{ color: '#555', fontSize: '0.85rem', textAlign: 'center', padding: '40px 0' }}>Chargement…</div>
      ) : (
        <>
          {/* Document body */}
          {cats.length === 0 && !addingCat && (
            <div style={{ color: '#444', fontSize: '0.85rem', textAlign: 'center', padding: '48px 0' }}>
              Aucune catégorie. Créez-en une pour commencer.
            </div>
          )}

          {cats.map(cat => (
            <CategorySection
              key={cat.id}
              cat={cat}
              onUpdateCat={updateCategory}
              onDeleteCat={deleteCategory}
              onCreateRule={createRule}
              onUpdateRule={updateRule}
              onDeleteRule={deleteRule}
            />
          ))}

          {/* Add category */}
          {addingCat ? (
            <div style={{ background: '#1a1a1a', border: '1px solid rgba(255,255,255,0.09)', borderRadius: 10, padding: '16px 18px', marginTop: 8 }}>
              <div style={{ color: '#888', fontSize: '0.78rem', fontWeight: 600, marginBottom: 10, textTransform: 'uppercase', letterSpacing: '0.05em' }}>Nouvelle catégorie</div>
              <input
                value={newCatName}
                onChange={e => setNewCatName(e.target.value)}
                placeholder="Nom de la catégorie…"
                autoFocus
                onKeyDown={e => { if (e.key === 'Enter') createCategory(); if (e.key === 'Escape') { setAddingCat(false); setNewCatName('') } }}
                style={{
                  width: '100%', background: '#111', border: '1px solid #3a3a3a', borderRadius: 6,
                  padding: '8px 10px', color: '#e8e8e8', fontFamily: 'inherit', fontSize: '0.92rem',
                  fontWeight: 700, marginBottom: 12, boxSizing: 'border-box', outline: 'none',
                }}
              />
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
            <button
              onClick={() => setAddingCat(true)}
              style={{ ...BTN, background: 'rgba(255,255,255,0.03)', color: '#666', border: '1px dashed rgba(255,255,255,0.12)', width: '100%', justifyContent: 'center', padding: '12px', marginTop: cats.length > 0 ? 8 : 0 }}
            >
              <Plus size={14} /> Ajouter une catégorie
            </button>
          )}
        </>
      )}
    </div>
  )
}
