import { useState, useEffect, useCallback, useRef } from 'react'
import { api } from './api.js'
import {
  BookOpen, Plus, Pencil, Trash2, X, Check, ChevronDown, ChevronUp,
  Bold, Italic, Heading2, List, Eye, FileText, Hash,
  AlertCircle, AlignLeft,
} from 'lucide-react'

// ── Styles inline ──────────────────────────────────────────────────────────

const BTN = {
  display: 'inline-flex', alignItems: 'center', gap: 5,
  padding: '6px 13px', borderRadius: 7, cursor: 'pointer',
  fontFamily: 'inherit', fontSize: '0.8rem', fontWeight: 600, border: 'none',
}

// ── Syntaxe MD custom ──────────────────────────────────────────────────────
//
//  ## Titre de section
//  Texte normal (paragraphe)
//  - item liste
//  :::art 1 Titre de l'article
//  Texte de la règle
//  :::
//  :::note
//  Texte de la note
//  :::

function parseCustomMd(raw) {
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

    // Article :::art N Titre
    if (/^:::art\s/.test(line)) {
      const match = line.match(/^:::art\s+(\S+)\s+(.+)$/)
      const number = match?.[1] || '?'
      const title  = match?.[2] || ''
      i++
      const content = collectBlock()
      blocks.push({ type: 'rule', number, title, text: content.join('\n').trim() })
      continue
    }

    // Tableau :::table
    if (/^:::table/.test(line)) {
      const rows = []
      i++
      while (i < lines.length && lines[i].trim() !== ':::') {
        if (lines[i].trim() !== '') rows.push(lines[i])
        i++
      }
      const headers = rows[0]?.split('|').map(c => c.trim()) || []
      const dataRows = rows.slice(1).filter(r => !/^[-|\s]+$/.test(r)).map(r => r.split('|').map(c => c.trim()))
      blocks.push({ type: 'table', headers, rows: dataRows })
      i++
      continue
    }

    // Note :::note
    if (/^:::note/.test(line)) {
      i++
      const content = collectBlock()
      blocks.push({ type: 'note', text: content.join('\n').trim() })
      continue
    }

    // Heading ## ou ###
    if (/^#{1,3}\s/.test(line)) {
      blocks.push({ type: 'heading', text: line.replace(/^#{1,3}\s/, '').trim() })
      i++
      continue
    }

    // Liste
    if (/^[-*]\s/.test(line)) {
      const items = []
      while (i < lines.length && /^[-*]\s/.test(lines[i])) {
        items.push(lines[i].replace(/^[-*]\s/, '').trim())
        i++
      }
      blocks.push({ type: 'list', items })
      continue
    }

    // Ligne vide → skip
    if (line.trim() === '') { i++; continue }

    // Bloc ::: non reconnu → skip pour éviter boucle infinie
    if (/^:::/.test(line)) { i++; continue }

    // Paragraphe : accumule les lignes consécutives non vides
    const para = []
    while (i < lines.length && lines[i].trim() !== '' && !/^#{1,3}\s/.test(lines[i]) && !/^[-*]\s/.test(lines[i]) && !/^:::/.test(lines[i])) {
      para.push(lines[i])
      i++
    }
    if (para.length) blocks.push({ type: 'paragraph', text: para.join('\n').trim() })
  }

  return blocks
}

// ── Rendu aperçu ───────────────────────────────────────────────────────────

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

function RenderSlBlocks({ blocks }) {
  return <>
    {blocks.map((b, i) => {
      if (b.type === 'rule') return (
        <div key={i} style={{ background: 'rgba(224,123,57,0.06)', border: '1px solid rgba(224,123,57,0.2)', borderRadius: 8, padding: '12px 16px', marginBottom: 12 }}>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 10, marginBottom: 4 }}>
            <span style={{ color: '#e07b39', fontWeight: 800, fontSize: '0.82rem', fontFamily: 'Georgia, serif' }}>Art. {b.number}</span>
            <strong style={{ color: '#ddd', fontSize: '0.9rem', fontFamily: 'Georgia, serif' }}>{renderInline(b.title)}</strong>
          </div>
          <RenderSlBlocks blocks={parseCustomMd(b.text)} />
        </div>
      )
      if (b.type === 'note') return (
        <div key={i} style={{ background: 'rgba(245,158,11,0.08)', border: '1px solid rgba(245,158,11,0.25)', borderRadius: 7, padding: '10px 14px', marginBottom: 10, color: '#d4a017', fontSize: '0.84rem' }}>
          📌 {renderInline(b.text)}
        </div>
      )
      if (b.type === 'table') return (
        <div key={i} style={{ overflowX: 'auto', marginBottom: 14, borderRadius: 8, border: '1px solid rgba(255,255,255,0.07)' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.84rem' }}>
            {b.headers?.length > 0 && (
              <thead>
                <tr style={{ background: 'rgba(56,189,248,0.06)', borderBottom: '1px solid rgba(56,189,248,0.2)' }}>
                  {b.headers.map((h, j) => (
                    <th key={j} style={{ padding: '8px 14px', textAlign: 'left', color: '#38bdf8', fontFamily: 'inherit', fontSize: '0.68rem', fontWeight: 800, letterSpacing: '0.08em', textTransform: 'uppercase', whiteSpace: 'nowrap' }}>{renderInline(h)}</th>
                  ))}
                </tr>
              </thead>
            )}
            <tbody>
              {b.rows.map((row, ri) => (
                <tr key={ri} style={{ borderBottom: '1px solid rgba(255,255,255,0.04)' }}>
                  {row.map((cell, ci) => (
                    <td key={ci} style={{ padding: '8px 14px', color: '#999', verticalAlign: 'top', lineHeight: 1.5 }}>{renderInline(cell)}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )
      if (b.type === 'heading') return (
        <h3 key={i} style={{ color: '#e8e8e8', fontSize: '1rem', fontWeight: 700, fontFamily: 'Georgia, serif', margin: '20px 0 8px', borderBottom: '1px solid rgba(255,255,255,0.07)', paddingBottom: 6 }}>
          {renderInline(b.text)}
        </h3>
      )
      if (b.type === 'list') return (
        <ul key={i} style={{ color: '#aaa', paddingLeft: 20, marginBottom: 10, fontSize: '0.85rem' }}>
          {b.items.map((it, j) => <li key={j}>{renderInline(it)}</li>)}
        </ul>
      )
      if (b.type === 'paragraph') return (
        <p key={i} style={{ color: '#aaa', fontSize: '0.85rem', margin: '0 0 10px', lineHeight: 1.7 }}>{renderInline(b.text)}</p>
      )
      return null
    })}
  </>
}

function MdPreview({ raw }) {
  const blocks = parseCustomMd(raw)
  if (!blocks.length) return (
    <div style={{ color: '#444', fontStyle: 'italic', fontSize: '0.82rem', padding: '24px 0', textAlign: 'center' }}>
      Rien à afficher.
    </div>
  )
  return (
    <div style={{ lineHeight: 1.8 }}>
      <RenderSlBlocks blocks={blocks} />
    </div>
  )
}

// ── Toolbar MD ─────────────────────────────────────────────────────────────

function MdToolbar({ taRef, value, onChange, onPreviewToggle, previewMode }) {
  function insert(before, after = '', placeholder = 'texte') {
    const ta = taRef.current
    if (!ta) return
    const s = ta.selectionStart, e = ta.selectionEnd
    const sel = value.slice(s, e) || placeholder
    const newVal = value.slice(0, s) + before + sel + after + value.slice(e)
    onChange(newVal)
    setTimeout(() => { ta.focus(); ta.setSelectionRange(s + before.length, s + before.length + sel.length) }, 0)
  }

  function insertLine(prefix) {
    const ta = taRef.current
    if (!ta) return
    const s = ta.selectionStart
    const lineStart = value.lastIndexOf('\n', s - 1) + 1
    const newVal = value.slice(0, lineStart) + prefix + value.slice(lineStart)
    onChange(newVal)
    setTimeout(() => { ta.focus(); ta.setSelectionRange(s + prefix.length, s + prefix.length) }, 0)
  }

  function insertSnippet(snippet) {
    const ta = taRef.current
    if (!ta) return
    const s = ta.selectionStart
    // Ajouter saut de ligne si nécessaire
    const prefix = s > 0 && value[s - 1] !== '\n' ? '\n\n' : ''
    const newVal = value.slice(0, s) + prefix + snippet + '\n' + value.slice(s)
    const cursor = s + prefix.length + snippet.length + 1
    onChange(newVal)
    setTimeout(() => { ta.focus(); ta.setSelectionRange(cursor, cursor) }, 0)
  }

  const iconBtn = (Icon, title, fn) => (
    <button key={title} onMouseDown={e => { e.preventDefault(); fn() }} title={title}
      style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#666', padding: '4px 6px', display: 'flex', borderRadius: 4, lineHeight: 1 }}>
      <Icon size={13} />
    </button>
  )

  const textBtn = (label, title, fn, accent) => (
    <button key={label} onMouseDown={e => { e.preventDefault(); fn() }} title={title}
      style={{ background: 'none', border: 'none', cursor: 'pointer', color: accent || '#666', padding: '3px 7px', display: 'flex', borderRadius: 4, fontSize: '0.72rem', fontWeight: 700, fontFamily: 'Georgia, serif', lineHeight: 1.2 }}>
      {label}
    </button>
  )

  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 0, padding: '4px 8px', background: '#0d0d0d', borderBottom: '1px solid #2a2a2a', flexWrap: 'wrap' }}>
      {iconBtn(Bold,     'Gras',         () => insert('**', '**'))}
      {iconBtn(Italic,   'Italique',     () => insert('*', '*'))}
      <div style={{ width: 1, height: 14, background: '#2a2a2a', margin: '0 3px' }} />
      {iconBtn(Heading2, 'Titre ##',     () => insertLine('## '))}
      {iconBtn(List,     'Liste',        () => insertLine('- '))}
      <div style={{ width: 1, height: 14, background: '#2a2a2a', margin: '0 3px' }} />
      {/* Snippets custom */}
      {textBtn('Art.',  'Insérer un article',    () => insertSnippet(':::art 1 Titre de la règle\nTexte de la règle\n:::'), '#e07b39')}
      {textBtn('Note',  'Insérer une note',      () => insertSnippet(':::note\nTexte de la note\n:::'), '#f59e0b')}
      <div style={{ flex: 1 }} />
      <button onClick={onPreviewToggle}
        style={{ ...BTN, padding: '3px 10px', fontSize: '0.72rem', gap: 4, background: previewMode ? 'rgba(224,123,57,0.15)' : 'transparent', color: previewMode ? '#e07b39' : '#555', border: `1px solid ${previewMode ? 'rgba(224,123,57,0.3)' : 'transparent'}` }}>
        {previewMode ? <FileText size={11} /> : <Eye size={11} />}
        {previewMode ? 'Éditer' : 'Aperçu'}
      </button>
    </div>
  )
}

// ── Menu slash ────────────────────────────────────────────────────────────

const SLASH_ITEMS = [
  { id: 'art',       label: 'Article',          hint: ':::art N Titre',  icon: '§',  accent: '#e07b39', snippet: `:::art 1 Titre de la règle\nTexte de la règle\n:::` },
  { id: 'note',      label: 'Note',             hint: ':::note',         icon: '📌', accent: '#f59e0b', snippet: `:::note\nTexte de la note\n:::` },
  { id: 'table',     label: 'Tableau',          hint: ':::table',        icon: '⊞',  accent: '#38bdf8', snippet: `:::table\nColonne 1 | Colonne 2 | Colonne 3\n---\nValeur A  | Valeur B  | Valeur C\nValeur D  | Valeur E  | Valeur F\n:::` },
  { id: 'heading',   label: 'Titre de section', hint: '## Titre',        icon: '#',  accent: '#a78bfa', snippet: `## Titre de section` },
  { id: 'list',      label: 'Liste',            hint: '- item',          icon: '—',  accent: '#34d399', snippet: `- Premier élément\n- Deuxième élément` },
  { id: 'paragraph', label: 'Paragraphe',       hint: 'Texte libre',     icon: '¶',  accent: '#71717a', snippet: `Votre texte ici.` },
  { id: 'bold',      label: 'Gras',             hint: '**texte**',       icon: 'B',  accent: '#e8e8e8', snippet: `**texte en gras**` },
  { id: 'italic',    label: 'Italique',         hint: '*texte*',         icon: 'I',  accent: '#e8e8e8', snippet: `*texte en italique*` },
]

function SlashMenu({ pos, filter, onSelect, onClose }) {
  const [active, setActive] = useState(0)
  const items = SLASH_ITEMS.filter(it =>
    !filter || it.label.toLowerCase().includes(filter.toLowerCase()) || it.id.includes(filter.toLowerCase())
  )

  useEffect(() => { setActive(0) }, [filter])

  useEffect(() => {
    function onKey(e) {
      if (e.key === 'ArrowDown')  { e.preventDefault(); setActive(a => (a + 1) % items.length) }
      if (e.key === 'ArrowUp')    { e.preventDefault(); setActive(a => (a - 1 + items.length) % items.length) }
      if (e.key === 'Enter' || e.key === 'Tab') { e.preventDefault(); if (items[active]) onSelect(items[active]) }
      if (e.key === 'Escape')     { e.preventDefault(); onClose() }
    }
    window.addEventListener('keydown', onKey, true)
    return () => window.removeEventListener('keydown', onKey, true)
  }, [items, active, onSelect, onClose])

  if (!items.length) return null

  return (
    <div style={{
      position: 'fixed', left: pos.x, top: pos.y, zIndex: 9999,
      background: '#1a1a1a', border: '1px solid rgba(255,255,255,0.12)',
      borderRadius: 10, boxShadow: '0 8px 32px rgba(0,0,0,0.6)',
      minWidth: 220, overflow: 'hidden',
    }}>
      <div style={{ padding: '6px 10px 4px', color: '#444', fontSize: '0.65rem', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', borderBottom: '1px solid rgba(255,255,255,0.05)' }}>
        Insérer
      </div>
      {items.map((it, i) => (
        <button key={it.id}
          onMouseDown={e => { e.preventDefault(); onSelect(it) }}
          onMouseEnter={() => setActive(i)}
          style={{
            display: 'flex', alignItems: 'center', gap: 10,
            width: '100%', padding: '8px 12px', border: 'none', cursor: 'pointer', textAlign: 'left',
            background: i === active ? 'rgba(255,255,255,0.06)' : 'transparent',
            transition: 'background 0.1s',
          }}>
          <span style={{ width: 22, height: 22, borderRadius: 5, background: 'rgba(255,255,255,0.05)', border: `1px solid rgba(255,255,255,0.08)`, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '0.75rem', fontWeight: 800, color: it.accent, flexShrink: 0, fontFamily: 'Georgia, serif' }}>
            {it.icon}
          </span>
          <span style={{ flex: 1 }}>
            <span style={{ color: '#d0d0d0', fontSize: '0.82rem', fontWeight: 600, display: 'block' }}>{it.label}</span>
            <span style={{ color: '#555', fontSize: '0.7rem', fontFamily: 'monospace' }}>{it.hint}</span>
          </span>
          {i === active && <span style={{ color: '#444', fontSize: '0.65rem' }}>↵</span>}
        </button>
      ))}
    </div>
  )
}

// ── Éditeur de chapitre ─────────────────────────────────────────────────────

function ChapterEditor({ chapter, bookId, onDelete, onRefresh }) {
  const [expanded, setExpanded]     = useState(false)
  const [editingTitle, setEditingTitle] = useState(false)
  const [title, setTitle]           = useState(chapter.title)
  // contenu brut stocké dans un ref → pas de re-render à chaque frappe
  const contentRef   = useRef(chapter.content || '')
  const [previewContent, setPreviewContent] = useState(chapter.content || '')
  const [preview, setPreview]       = useState(false)
  const [saving, setSaving]         = useState(false)
  const [dirty, setDirty]           = useState(false)
  const [slashMenu, setSlashMenu]   = useState(null)
  const taRef        = useRef(null)
  const rafRef       = useRef(null)
  const slashRef     = useRef(null)

  // Cleanup au démontage
  useEffect(() => () => {
    if (slashRef.current) { document.body.removeChild(slashRef.current); slashRef.current = null }
    if (rafRef.current)   { cancelAnimationFrame(rafRef.current); rafRef.current = null }
  }, [])

  // Sync si le chapitre change depuis l'extérieur
  useEffect(() => {
    setTitle(chapter.title)
    const c = chapter.content || ''
    contentRef.current = c
    setPreviewContent(c)
    if (taRef.current) taRef.current.value = c
    setDirty(false)
  }, [chapter.chapter_id, chapter.title, chapter.content])

  // Resize via RAF pour ne faire qu'un reflow par frame
  function scheduleResize() {
    if (rafRef.current) return
    rafRef.current = requestAnimationFrame(() => {
      rafRef.current = null
      const ta = taRef.current
      if (!ta) return
      ta.style.height = 'auto'
      ta.style.height = ta.scrollHeight + 'px'
    })
  }

  function handleChange(val) {
    contentRef.current = val
    if (!dirty) setDirty(true)
    scheduleResize()
    // Slash menu : détecter / seulement si la ligne courante commence par /
    const ta = taRef.current
    if (ta) {
      const cur = ta.selectionStart
      const textBefore = val.slice(0, cur)
      const lineStart  = textBefore.lastIndexOf('\n') + 1
      const lineText   = textBefore.slice(lineStart)
      if (lineText.startsWith('/')) {
        const filter = lineText.slice(1)
        const coords = getCaretCoords(ta, lineStart)
        setSlashMenu({ x: coords.x, y: coords.y, slashPos: lineStart, filter })
      } else {
        if (slashMenu) setSlashMenu(null)
      }
    }
  }

  function getCaretCoords(ta, pos) {
    // Réutilise un seul nœud miroir au lieu d'en créer un à chaque frappe
    let div = slashRef.current
    if (!div) {
      div = document.createElement('div')
      div.style.position = 'absolute'
      div.style.visibility = 'hidden'
      div.style.whiteSpace = 'pre-wrap'
      div.style.height = 'auto'
      div.style.pointerEvents = 'none'
      document.body.appendChild(div)
      slashRef.current = div
    }
    const style = window.getComputedStyle(ta)
    ;['fontFamily','fontSize','fontWeight','lineHeight','padding','border','boxSizing','width','wordWrap','overflowWrap'].forEach(p => div.style[p] = style[p])
    div.innerHTML = ta.value.slice(0, pos).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/\n/g,'<br>') + '<span id="_sc">|</span>'
    const span    = div.querySelector('#_sc')
    const taRect  = ta.getBoundingClientRect()
    const spanRect = span.getBoundingClientRect()
    const divRect  = div.getBoundingClientRect()
    const lineH   = parseFloat(style.lineHeight) || 18
    const x = taRect.left + (spanRect.left - divRect.left)
    const y = taRect.top  + (spanRect.top  - divRect.top) + lineH - ta.scrollTop
    const menuH = Math.min(SLASH_ITEMS.length, 7) * 44 + 32
    const menuW = 220
    return {
      x: Math.min(x, window.innerWidth  - menuW - 8),
      y: y + lineH + 4 > window.innerHeight - menuH ? y - menuH : y + lineH + 4,
    }
  }

  function handleSlashSelect(item) {
    if (!slashMenu) return
    const ta = taRef.current
    const val = contentRef.current
    const cur = ta ? ta.selectionStart : val.length
    const before  = val.slice(0, slashMenu.slashPos)
    const after   = val.slice(cur)
    const newVal  = before + item.snippet + '\n' + after
    contentRef.current = newVal
    if (ta) ta.value = newVal
    setDirty(true)
    setSlashMenu(null)
    scheduleResize()
    setTimeout(() => {
      if (ta) {
        const cur2 = before.length + item.snippet.length + 1
        ta.focus()
        ta.setSelectionRange(cur2, cur2)
      }
    }, 0)
  }

  async function save() {
    setSaving(true)
    try {
      await api.updateSlBook_Chapter(bookId, chapter.chapter_id, {
        title: title.trim() || chapter.title,
        content: contentRef.current,
      })
      setDirty(false)
      onRefresh()
    } finally { setSaving(false) }
  }

  async function saveTitle() {
    if (!title.trim()) return
    await api.updateSlBook_Chapter(bookId, chapter.chapter_id, { title: title.trim() })
    setEditingTitle(false)
    onRefresh()
  }

  const blockCount = parseCustomMd(contentRef.current).length

  return (
    <div style={{ marginBottom: 8, background: '#161616', border: '1px solid rgba(255,255,255,0.07)', borderRadius: 10, overflow: 'hidden' }}>

      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '10px 14px', cursor: 'pointer', userSelect: 'none' }}
        onClick={() => !editingTitle && setExpanded(e => !e)}>
        <div style={{ color: '#444', display: 'flex', flexShrink: 0 }}>
          {expanded ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
        </div>

        {editingTitle ? (
          <div style={{ display: 'flex', flex: 1, gap: 8, alignItems: 'center' }} onClick={e => e.stopPropagation()}>
            <input value={title} onChange={e => setTitle(e.target.value)} autoFocus
              onKeyDown={e => { if (e.key === 'Enter') saveTitle(); if (e.key === 'Escape') { setEditingTitle(false); setTitle(chapter.title) } }}
              style={{ flex: 1, background: '#111', border: '1px solid #3a3a3a', borderRadius: 6, padding: '5px 9px', color: '#e8e8e8', fontFamily: 'inherit', fontSize: '0.9rem', fontWeight: 700, outline: 'none' }} />
            <button onClick={saveTitle} style={{ ...BTN, padding: '4px 9px', background: '#3e9041', color: '#fff' }}><Check size={12} /></button>
            <button onClick={() => { setEditingTitle(false); setTitle(chapter.title) }}
              style={{ ...BTN, padding: '4px 9px', background: 'transparent', color: '#666', border: '1px solid rgba(255,255,255,0.1)' }}><X size={12} /></button>
          </div>
        ) : (
          <>
            <span style={{ flex: 1, color: '#d0d0d0', fontWeight: 700, fontSize: '0.9rem' }}>{chapter.title}</span>
            <span style={{ color: '#444', fontSize: '0.7rem', flexShrink: 0 }}>{blockCount} élément{blockCount !== 1 ? 's' : ''}</span>
            {dirty && <span style={{ color: '#e07b39', fontSize: '0.68rem', fontWeight: 700, flexShrink: 0 }}>●</span>}
            <div style={{ display: 'flex', gap: 2, flexShrink: 0 }} onClick={e => e.stopPropagation()}>
              <button onClick={() => { setEditingTitle(true); setExpanded(true) }}
                style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#555', padding: 4, display: 'flex', borderRadius: 4 }}>
                <Pencil size={12} />
              </button>
              <button onClick={() => onDelete(chapter.chapter_id)}
                style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#6b3030', padding: 4, display: 'flex', borderRadius: 4 }}>
                <Trash2 size={12} />
              </button>
            </div>
          </>
        )}
      </div>

      {/* Éditeur */}
      {expanded && (
        <div>
          <MdToolbar taRef={taRef} value={contentRef.current} onChange={handleChange} previewMode={preview} onPreviewToggle={() => { setPreviewContent(contentRef.current); setPreview(p => !p) }} />

          {preview ? (
            <div style={{ padding: '16px 20px', minHeight: 120, background: '#0d0d0d' }}>
              <MdPreview raw={previewContent} />
            </div>
          ) : (
            <>
              {slashMenu && (
                <SlashMenu
                  pos={{ x: slashMenu.x, y: slashMenu.y }}
                  filter={slashMenu.filter}
                  onSelect={handleSlashSelect}
                  onClose={() => setSlashMenu(null)}
                />
              )}
              <textarea
                ref={taRef}
                defaultValue={contentRef.current}
                onChange={e => handleChange(e.target.value)}
                onKeyDown={e => {
                  if (slashMenu && e.key === 'Backspace') {
                    const ta = taRef.current
                    const cur = ta?.selectionStart ?? 0
                    if (cur <= slashMenu.slashPos + 1) setSlashMenu(null)
                  }
                }}
                placeholder={`Syntaxe disponible :

## Titre de section
Texte d'un paragraphe normal.

:::art 1 Respect mutuel
Tout joueur doit se comporter de façon respectueuse.
:::

:::note
Point important à retenir.
:::

:::table
Colonne 1 | Colonne 2 | Colonne 3
---
Valeur A  | Valeur B  | Valeur C
:::

- Élément de liste
- Autre élément`}
                style={{
                  display: 'block', width: '100%', background: '#0d0d0d', border: 'none',
                  padding: '12px 16px', color: '#b8b8b8', fontFamily: "'Consolas','Monaco',monospace",
                  fontSize: '0.82rem', lineHeight: 1.75, resize: 'none', boxSizing: 'border-box',
                  outline: 'none', minHeight: 160, overflow: 'hidden',
                }}
              />
            </>
          )}

          <div style={{ display: 'flex', gap: 8, padding: '10px 14px', borderTop: '1px solid rgba(255,255,255,0.05)', background: '#0a0a0a' }}>
            <button onClick={save} disabled={saving || !dirty}
              style={{ ...BTN, background: dirty ? '#3e9041' : 'transparent', color: dirty ? '#fff' : '#444', border: dirty ? 'none' : '1px solid rgba(255,255,255,0.07)', opacity: saving ? 0.6 : 1 }}>
              <Check size={13} />{saving ? 'Enregistrement…' : dirty ? 'Enregistrer' : 'Enregistré'}
            </button>
            {dirty && (
              <button onClick={() => {
                const orig = chapter.content || ''
                contentRef.current = orig
                if (taRef.current) { taRef.current.value = orig; scheduleResize() }
                setDirty(false)
              }}
                style={{ ...BTN, background: 'transparent', color: '#666', border: '1px solid rgba(255,255,255,0.1)' }}>
                <X size={13} />Annuler
              </button>
            )}
            <div style={{ flex: 1 }} />
            <span style={{ color: '#333', fontSize: '0.7rem', alignSelf: 'center' }}>
              Ctrl+S pour sauvegarder
            </span>
          </div>
        </div>
      )}
    </div>
  )
}

// ── Panel principal ─────────────────────────────────────────────────────────

export default function OpenFrameworkRulesPanel() {
  const [books, setBooks]           = useState([])
  const [activeBookId, setActiveBookId] = useState(null)
  const [loading, setLoading]       = useState(true)
  const [error, setError]           = useState('')
  const [addingChapter, setAddingChapter] = useState(false)
  const [newChTitle, setNewChTitle] = useState('')
  const [newChId, setNewChId]       = useState('')
  const [editingBook, setEditingBook] = useState(false)
  const [bookForm, setBookForm]     = useState({})
  const [savingBook, setSavingBook] = useState(false)

  const load = useCallback(async () => {
    try {
      const data = await api.getSlBooks()
      setBooks(data)
      setActiveBookId(cur => cur ?? (data.length > 0 ? data[0].book_id : null))
    } catch (e) {
      setError('Impossible de charger les livres : ' + e.message)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  const book = books.find(b => b.book_id === activeBookId)

  async function createChapter() {
    const id = newChId.trim() || newChTitle.trim().toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/\s+/g, '_').replace(/[^a-z0-9_]/g, '')
    if (!id || !newChTitle.trim()) return
    try {
      await api.createSlChapter(activeBookId, { chapter_id: id, title: newChTitle.trim() })
      setAddingChapter(false); setNewChTitle(''); setNewChId('')
      load()
    } catch (e) { setError(e.message) }
  }

  async function deleteChapter(chapterId) {
    if (!confirm('Supprimer ce chapitre et tout son contenu ?')) return
    try { await api.deleteSlChapter(activeBookId, chapterId); load() }
    catch (e) { setError(e.message) }
  }

  async function saveBook() {
    setSavingBook(true)
    try { await api.updateSlBook(activeBookId, bookForm); setEditingBook(false); load() }
    catch (e) { setError(e.message) }
    finally { setSavingBook(false) }
  }

  return (
    <div style={{ display: 'flex', height: '100%', overflow: 'hidden' }}>

      {/* ── Sidebar livres ── */}
      <aside style={{ width: 190, background: '#0f0f0f', borderRight: '1px solid rgba(255,255,255,0.07)', display: 'flex', flexDirection: 'column', flexShrink: 0, overflowY: 'auto' }}>
        <div style={{ padding: '16px 14px 8px', color: '#444', fontSize: '0.68rem', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.08em' }}>Livres</div>
        {books.map(b => (
          <button key={b.book_id} onClick={() => setActiveBookId(b.book_id)}
            style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px', background: activeBookId === b.book_id ? 'rgba(224,123,57,0.1)' : 'transparent', border: 'none', borderLeft: `2px solid ${activeBookId === b.book_id ? '#e07b39' : 'transparent'}`, cursor: 'pointer', textAlign: 'left', width: '100%' }}>
            <span style={{ fontSize: '1rem' }}>{b.icon}</span>
            <span style={{ color: activeBookId === b.book_id ? '#e07b39' : '#999', fontSize: '0.82rem', fontWeight: 600, lineHeight: 1.3 }}>{b.title}</span>
          </button>
        ))}
      </aside>

      {/* ── Contenu ── */}
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
        {loading ? (
          <div style={{ color: '#555', textAlign: 'center', padding: '60px 0', fontSize: '0.85rem' }}>Chargement…</div>
        ) : !book ? (
          <div style={{ color: '#555', textAlign: 'center', padding: '60px 0', fontSize: '0.85rem' }}>Sélectionne un livre.</div>
        ) : (
          <>
            {/* Header livre */}
            <div style={{ padding: '18px 28px 14px', borderBottom: '1px solid rgba(255,255,255,0.07)', display: 'flex', alignItems: 'center', gap: 12, flexShrink: 0 }}>
              <span style={{ fontSize: '1.5rem' }}>{book.icon}</span>
              {editingBook ? (
                <div style={{ flex: 1, display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center' }}>
                  <input value={bookForm.title || ''} onChange={e => setBookForm(f => ({ ...f, title: e.target.value }))} placeholder="Titre"
                    style={{ background: '#111', border: '1px solid #3a3a3a', borderRadius: 6, padding: '5px 10px', color: '#e8e8e8', fontFamily: 'inherit', fontSize: '1rem', fontWeight: 700, outline: 'none', minWidth: 160 }} autoFocus />
                  <input value={bookForm.icon || ''} onChange={e => setBookForm(f => ({ ...f, icon: e.target.value }))} placeholder="📖"
                    style={{ background: '#111', border: '1px solid #3a3a3a', borderRadius: 6, padding: '5px 9px', color: '#e8e8e8', fontFamily: 'inherit', fontSize: '1rem', outline: 'none', width: 62 }} />
                  <label style={{ color: '#555', fontSize: '0.73rem', display: 'flex', alignItems: 'center', gap: 5 }}>
                    Couv. <input type="color" value={bookForm.cover_color || '#1a0a00'} onChange={e => setBookForm(f => ({ ...f, cover_color: e.target.value }))} style={{ width: 24, height: 24, border: 'none', borderRadius: 3, cursor: 'pointer' }} />
                  </label>
                  <label style={{ color: '#555', fontSize: '0.73rem', display: 'flex', alignItems: 'center', gap: 5 }}>
                    Accent <input type="color" value={bookForm.cover_accent || '#D4A574'} onChange={e => setBookForm(f => ({ ...f, cover_accent: e.target.value }))} style={{ width: 24, height: 24, border: 'none', borderRadius: 3, cursor: 'pointer' }} />
                  </label>
                  <button onClick={saveBook} disabled={savingBook} style={{ ...BTN, background: '#3e9041', color: '#fff' }}><Check size={13} />{savingBook ? '…' : 'OK'}</button>
                  <button onClick={() => setEditingBook(false)} style={{ ...BTN, background: 'transparent', color: '#666', border: '1px solid rgba(255,255,255,0.1)' }}><X size={13} /></button>
                </div>
              ) : (
                <>
                  <div style={{ flex: 1 }}>
                    <h2 style={{ color: '#e8e8e8', fontSize: '1.1rem', fontWeight: 700, margin: 0 }}>{book.title}</h2>
                    <div style={{ color: '#555', fontSize: '0.73rem', marginTop: 2 }}>{book.chapters.length} chapitre{book.chapters.length !== 1 ? 's' : ''}</div>
                  </div>
                  <button onClick={() => { setEditingBook(true); setBookForm({ title: book.title, icon: book.icon, cover_color: book.cover_color, cover_accent: book.cover_accent }) }}
                    style={{ ...BTN, padding: '5px 10px', background: 'rgba(255,255,255,0.04)', color: '#666', border: '1px solid rgba(255,255,255,0.08)' }}>
                    <Pencil size={12} /> Modifier
                  </button>
                </>
              )}
            </div>

            {error && (
              <div style={{ background: 'rgba(209,59,26,0.12)', color: '#d13b1a', border: '1px solid rgba(209,59,26,0.3)', borderRadius: 8, padding: '8px 14px', margin: '12px 28px 0', fontSize: '0.82rem', display: 'flex', justifyContent: 'space-between' }}>
                {error}
                <button onClick={() => setError('')} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#d13b1a', padding: 2 }}><X size={13} /></button>
              </div>
            )}

            {/* Chapitres */}
            <div style={{ flex: 1, overflowY: 'auto', padding: '20px 28px 28px' }}>

              {/* Légende syntaxe */}
              <div style={{ marginBottom: 20, padding: '10px 14px', background: 'rgba(255,255,255,0.02)', borderRadius: 8, border: '1px solid rgba(255,255,255,0.05)' }}>
                <div style={{ color: '#444', fontSize: '0.7rem', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: 8 }}>Syntaxe</div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px 20px', fontSize: '0.75rem', fontFamily: 'monospace', color: '#555' }}>
                  <span><span style={{ color: '#a78bfa' }}>## Titre</span> — section</span>
                  <span><span style={{ color: '#e07b39' }}>:::art 1 Nom</span><br /><span style={{ color: '#888' }}>Texte</span><br /><span style={{ color: '#e07b39' }}>:::</span> — article</span>
                  <span><span style={{ color: '#f59e0b' }}>:::note</span><br /><span style={{ color: '#888' }}>Texte</span><br /><span style={{ color: '#f59e0b' }}>:::</span> — note</span>
                  <span><span style={{ color: '#38bdf8' }}>:::table</span><br /><span style={{ color: '#888' }}>A | B</span><br /><span style={{ color: '#888' }}>---</span><br /><span style={{ color: '#888' }}>1 | 2</span><br /><span style={{ color: '#38bdf8' }}>:::</span> — tableau</span>
                  <span><span style={{ color: '#34d399' }}>- item</span> — liste</span>
                  <span><span style={{ color: '#71717a' }}>Texte libre</span> — paragraphe</span>
                </div>
              </div>

              {book.chapters.length === 0 && !addingChapter && (
                <div style={{ color: '#444', fontSize: '0.85rem', textAlign: 'center', padding: '40px 0' }}>
                  Aucun chapitre. Créez-en un pour commencer.
                </div>
              )}

              {book.chapters.map(ch => (
                <ChapterEditor
                  key={ch.id || ch.chapter_id}
                  chapter={ch}
                  bookId={activeBookId}
                  onDelete={deleteChapter}
                  onRefresh={load}
                />
              ))}

              {/* Ajouter chapitre */}
              {addingChapter ? (
                <div style={{ background: '#1a1a1a', border: '1px solid rgba(255,255,255,0.09)', borderRadius: 10, padding: '14px 18px', marginTop: 8 }}>
                  <div style={{ color: '#666', fontSize: '0.74rem', fontWeight: 700, marginBottom: 10, textTransform: 'uppercase', letterSpacing: '0.05em' }}>Nouveau chapitre</div>
                  <input value={newChTitle} onChange={e => setNewChTitle(e.target.value)} placeholder="Titre du chapitre…" autoFocus
                    onKeyDown={e => { if (e.key === 'Enter') createChapter(); if (e.key === 'Escape') { setAddingChapter(false); setNewChTitle(''); setNewChId('') } }}
                    style={{ width: '100%', background: '#111', border: '1px solid #3a3a3a', borderRadius: 6, padding: '8px 10px', color: '#e8e8e8', fontFamily: 'inherit', fontSize: '0.9rem', fontWeight: 700, marginBottom: 8, boxSizing: 'border-box', outline: 'none' }} />
                  <input value={newChId} onChange={e => setNewChId(e.target.value)} placeholder="ID (auto si vide)"
                    style={{ width: '100%', background: '#111', border: '1px solid #222', borderRadius: 6, padding: '5px 10px', color: '#666', fontFamily: 'monospace', fontSize: '0.77rem', marginBottom: 12, boxSizing: 'border-box', outline: 'none' }} />
                  <div style={{ display: 'flex', gap: 8 }}>
                    <button onClick={createChapter} style={{ ...BTN, background: '#3e9041', color: '#fff' }}><Check size={13} /> Créer</button>
                    <button onClick={() => { setAddingChapter(false); setNewChTitle(''); setNewChId('') }}
                      style={{ ...BTN, background: 'transparent', color: '#888', border: '1px solid rgba(255,255,255,0.1)' }}>Annuler</button>
                  </div>
                </div>
              ) : (
                <button onClick={() => setAddingChapter(true)}
                  style={{ ...BTN, background: 'rgba(255,255,255,0.02)', color: '#555', border: '1px dashed rgba(255,255,255,0.1)', width: '100%', justifyContent: 'center', padding: '11px', marginTop: 8 }}>
                  <Plus size={14} /> Ajouter un chapitre
                </button>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  )
}
