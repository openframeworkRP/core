import { useState, useEffect, useCallback, useRef, useMemo } from 'react'
import { marked } from 'marked'
import { api } from './api.js'
import {
  BookMarked, Plus, Pencil, Trash2, X, Check, Clock, Search, ChevronRight, ChevronDown,
  FileText, Folder, FolderOpen, Download, Upload, Eye, ToggleLeft, ToggleRight, Move,
  Bold, Italic, Strikethrough, Heading2, Heading3, List, ListOrdered, Quote, Code, Minus,
} from 'lucide-react'

marked.setOptions({ breaks: true, gfm: true })

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

// ── Markdown toolbar ──────────────────────────────────────────────────────

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
    case 'bold':   return wrap('**', '**')
    case 'italic': return wrap('*', '*')
    case 'strike': return wrap('~~', '~~')
    case 'h2':     return linePrefix('## ')
    case 'h3':     return linePrefix('### ')
    case 'bullet': return linePrefix('- ')
    case 'number': return linePrefix('1. ')
    case 'quote':  return linePrefix('> ')
    case 'code':   return sel.includes('\n') ? wrap('```\n', '\n```', 'code') : wrap('`', '`', 'code')
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
      style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#888', padding: '4px 6px', display: 'flex', borderRadius: 4, lineHeight: 1 }}>
      <Icon size={13} />
    </button>
  )
  return (
    <div style={{ border: '1px solid #2e3344', borderRadius: 8, overflow: 'hidden', display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 0, padding: '4px 6px', background: '#11151f', borderBottom: '1px solid #2e3344', flexWrap: 'wrap', flexShrink: 0 }}>
        {tb('bold', Bold, 'Gras')}{tb('italic', Italic, 'Italique')}{tb('strike', Strikethrough, 'Barré')}
        <div style={{ width: 1, height: 14, background: '#333', margin: '0 4px' }} />
        {tb('h2', Heading2, 'H2')}{tb('h3', Heading3, 'H3')}
        <div style={{ width: 1, height: 14, background: '#333', margin: '0 4px' }} />
        {tb('bullet', List, 'Liste')}{tb('number', ListOrdered, 'Numéros')}{tb('quote', Quote, 'Citation')}{tb('code', Code, 'Code')}{tb('hr', Minus, 'Séparateur')}
        <div style={{ flex: 1 }} />
        <button onClick={() => setPreview(p => !p)} style={{
          ...BTN, padding: '3px 9px', fontSize: '0.73rem', gap: 4,
          background: preview ? 'rgba(88,101,242,0.15)' : 'transparent',
          color: preview ? '#5865f2' : '#888',
          border: `1px solid ${preview ? 'rgba(88,101,242,0.3)' : 'transparent'}`,
        }}>
          {preview ? <FileText size={11} /> : <Eye size={11} />}
          {preview ? 'Markdown' : 'Aperçu'}
        </button>
      </div>
      {preview ? (
        <div className="wiki-md" style={{ padding: '16px 18px', flex: 1, overflow: 'auto', background: '#0a0e18' }}
          dangerouslySetInnerHTML={{ __html: value?.trim() ? marked.parse(value) : '<span style="color:#444;font-style:italic">Rien à afficher.</span>' }} />
      ) : (
        <textarea ref={textareaRef} value={value} onChange={e => onChange(e.target.value)}
          placeholder="Écris la documentation en Markdown…" style={{
            display: 'block', width: '100%', flex: 1, background: '#0a0e18', border: 'none',
            padding: '14px 18px', color: '#c8c8c8', fontFamily: "'Consolas','Monaco',monospace",
            fontSize: '0.84rem', lineHeight: 1.7, resize: 'none', boxSizing: 'border-box', outline: 'none',
          }} />
      )}
    </div>
  )
}

// ── Tree node ─────────────────────────────────────────────────────────────

function TreeNode({ node, depth, selectedId, onSelect, expanded, onToggle, onCreateChild }) {
  const isSel = selectedId === node.id
  const isExp = expanded.has(node.id)
  const hasChildren = node.children && node.children.length > 0
  const Icon = hasChildren ? (isExp ? FolderOpen : Folder) : FileText

  return (
    <div>
      <div
        onClick={() => onSelect(node.id)}
        style={{
          display: 'flex', alignItems: 'center', gap: 4,
          padding: '4px 8px 4px 6px',
          paddingLeft: 6 + depth * 14,
          borderRadius: 4,
          cursor: 'pointer',
          background: isSel ? 'rgba(88,101,242,0.15)' : 'transparent',
          color: isSel ? '#b8c0ff' : '#b8b8b8',
          userSelect: 'none',
          fontSize: '0.83rem',
        }}
      >
        <button
          onClick={e => { e.stopPropagation(); if (hasChildren) onToggle(node.id) }}
          style={{
            background: 'none', border: 'none', cursor: hasChildren ? 'pointer' : 'default',
            color: '#666', padding: 0, display: 'flex', width: 14,
          }}
        >
          {hasChildren ? (isExp ? <ChevronDown size={12} /> : <ChevronRight size={12} />) : null}
        </button>
        <Icon size={13} style={{ color: isSel ? '#8a94f5' : '#666', flexShrink: 0 }} />
        {node.icon && <span style={{ fontSize: '0.9em' }}>{node.icon}</span>}
        <span style={{ flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', fontWeight: depth === 0 ? 600 : 400 }}>
          {node.title || <em style={{ color: '#555' }}>Sans titre</em>}
        </span>
        {!node.published && (
          <span style={{ fontSize: '0.6rem', color: '#666', padding: '1px 5px', borderRadius: 3, background: 'rgba(255,255,255,0.04)' }}>
            brouillon
          </span>
        )}
        <button
          onClick={e => { e.stopPropagation(); onCreateChild(node.id) }}
          title="Ajouter une sous-page"
          style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#555', padding: 2, display: 'flex', borderRadius: 3, opacity: 0.6 }}
        >
          <Plus size={11} />
        </button>
      </div>
      {hasChildren && isExp && (
        <div>
          {node.children.map(ch => (
            <TreeNode key={ch.id} node={ch} depth={depth + 1} selectedId={selectedId}
              onSelect={onSelect} expanded={expanded} onToggle={onToggle}
              onCreateChild={onCreateChild} />
          ))}
        </div>
      )}
    </div>
  )
}

// ── Move modal ────────────────────────────────────────────────────────────

function flattenTree(tree, depth = 0, out = []) {
  for (const n of tree) {
    out.push({ id: n.id, title: n.title, depth, icon: n.icon })
    if (n.children?.length) flattenTree(n.children, depth + 1, out)
  }
  return out
}

// Écarte la sous-arborescence de l'item qu'on déplace (pour éviter les cycles)
function excludeSubtree(tree, excludedId) {
  return tree
    .filter(n => n.id !== excludedId)
    .map(n => ({ ...n, children: excludeSubtree(n.children || [], excludedId) }))
}

function MoveModal({ page, tree, onClose, onMove }) {
  const [targetParent, setTargetParent] = useState(page.parent_id ?? null)
  const [busy, setBusy] = useState(false)
  const options = useMemo(() => {
    const cleaned = excludeSubtree(tree, page.id)
    return flattenTree(cleaned)
  }, [tree, page.id])

  async function submit() {
    setBusy(true)
    try { await onMove(page.id, { parent_id: targetParent, position: 0 }); onClose() }
    finally { setBusy(false) }
  }

  return (
    <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.7)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 24 }}
      onClick={e => e.target === e.currentTarget && onClose()}>
      <div style={{ background: '#161a26', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 14, width: '100%', maxWidth: 480, maxHeight: '70vh', display: 'flex', flexDirection: 'column' }}>
        <div style={{ padding: '14px 20px', borderBottom: '1px solid rgba(255,255,255,0.07)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div>
            <div style={{ color: '#e8e8e8', fontWeight: 700, fontSize: '0.9rem' }}>Déplacer « {page.title || 'Sans titre'} »</div>
            <div style={{ color: '#666', fontSize: '0.73rem', marginTop: 2 }}>Choisis le nouveau parent</div>
          </div>
          <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#666', display: 'flex', padding: 4 }}><X size={17} /></button>
        </div>
        <div style={{ overflowY: 'auto', flex: 1, padding: 8 }}>
          <button onClick={() => setTargetParent(null)}
            style={{ display: 'block', width: '100%', textAlign: 'left', padding: '8px 12px', border: 'none', background: targetParent == null ? 'rgba(88,101,242,0.15)' : 'transparent', color: targetParent == null ? '#b8c0ff' : '#b8b8b8', cursor: 'pointer', borderRadius: 5, fontSize: '0.83rem' }}>
            — Racine (top-level) —
          </button>
          {options.map(o => (
            <button key={o.id} onClick={() => setTargetParent(o.id)}
              style={{ display: 'block', width: '100%', textAlign: 'left', padding: '6px 12px', paddingLeft: 12 + o.depth * 14, border: 'none', background: targetParent === o.id ? 'rgba(88,101,242,0.15)' : 'transparent', color: targetParent === o.id ? '#b8c0ff' : '#b8b8b8', cursor: 'pointer', borderRadius: 5, fontSize: '0.83rem' }}>
              {o.icon && <span style={{ marginRight: 6 }}>{o.icon}</span>}{o.title || 'Sans titre'}
            </button>
          ))}
        </div>
        <div style={{ padding: '12px 20px', borderTop: '1px solid rgba(255,255,255,0.07)', display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
          <button onClick={onClose} style={{ ...BTN, background: 'transparent', color: '#888', border: '1px solid rgba(255,255,255,0.1)' }}>Annuler</button>
          <button onClick={submit} disabled={busy} style={{ ...BTN, background: '#5865f2', color: '#fff', opacity: busy ? 0.5 : 1 }}>
            <Move size={13} /> Déplacer
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Import modal ──────────────────────────────────────────────────────────

function ImportModal({ parent, onClose, onImport }) {
  const [markdown, setMarkdown] = useState('')
  const [mode, setMode] = useState('append')
  const [busy, setBusy] = useState(false)
  const [result, setResult] = useState(null)
  const [err, setErr] = useState('')
  const fileRef = useRef(null)

  async function handleFile(e) {
    const f = e.target.files?.[0]
    if (!f) return
    setMarkdown(await f.text())
  }

  async function submit() {
    if (!markdown.trim()) { setErr('Markdown vide'); return }
    setBusy(true); setErr('')
    try {
      const r = await onImport({ parent_id: parent?.id ?? null, markdown, mode })
      setResult(r)
      if (!r.error) setTimeout(onClose, 1200)
    } catch (e) { setErr(e.message) }
    finally { setBusy(false) }
  }

  return (
    <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.7)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 24 }}
      onClick={e => e.target === e.currentTarget && onClose()}>
      <div style={{ background: '#161a26', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 14, width: '100%', maxWidth: 680, maxHeight: '85vh', display: 'flex', flexDirection: 'column' }}>
        <div style={{ padding: '14px 20px', borderBottom: '1px solid rgba(255,255,255,0.07)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div>
            <div style={{ color: '#e8e8e8', fontWeight: 700, fontSize: '0.9rem' }}>Importer du markdown</div>
            <div style={{ color: '#666', fontSize: '0.73rem', marginTop: 2 }}>
              Cible : {parent ? <strong style={{ color: '#b8c0ff' }}>{parent.title || 'Sans titre'}</strong> : <em style={{ color: '#888' }}>Racine</em>}
            </div>
          </div>
          <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#666', display: 'flex', padding: 4 }}><X size={17} /></button>
        </div>
        <div style={{ padding: '16px 20px', display: 'flex', flexDirection: 'column', gap: 12, overflow: 'auto' }}>
          <div style={{ background: 'rgba(88,101,242,0.06)', border: '1px solid rgba(88,101,242,0.15)', borderRadius: 8, padding: '10px 14px', fontSize: '0.76rem', color: '#8b98f5' }}>
            Format : chaque <code style={{ background: 'rgba(255,255,255,0.08)', padding: '1px 5px', borderRadius: 3 }}>#</code>, <code style={{ background: 'rgba(255,255,255,0.08)', padding: '1px 5px', borderRadius: 3 }}>##</code>, <code style={{ background: 'rgba(255,255,255,0.08)', padding: '1px 5px', borderRadius: 3 }}>###</code> devient une page. Le texte entre deux headings devient le contenu.
          </div>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <input ref={fileRef} type="file" accept=".md,.markdown,.txt" onChange={handleFile} style={{ display: 'none' }} />
            <button onClick={() => fileRef.current?.click()} style={{ ...BTN, background: 'rgba(255,255,255,0.05)', color: '#b0b0b0', border: '1px dashed rgba(255,255,255,0.15)' }}>
              <Upload size={13} /> Charger un fichier .md
            </button>
            <div style={{ flex: 1 }} />
            <label style={{ display: 'flex', alignItems: 'center', gap: 6, color: '#888', fontSize: '0.78rem', cursor: 'pointer' }}>
              <input type="radio" checked={mode === 'append'} onChange={() => setMode('append')} /> Ajouter
            </label>
            <label style={{ display: 'flex', alignItems: 'center', gap: 6, color: '#888', fontSize: '0.78rem', cursor: 'pointer' }}>
              <input type="radio" checked={mode === 'replace'} onChange={() => setMode('replace')} /> Remplacer les enfants
            </label>
          </div>
          <textarea value={markdown} onChange={e => setMarkdown(e.target.value)}
            placeholder="# Ma section&#10;&#10;## Sous-page&#10;Contenu…"
            style={{ width: '100%', minHeight: 260, background: '#0a0e18', border: '1px solid #2e3344', borderRadius: 8, padding: '12px 14px', color: '#c8c8c8', fontFamily: "'Consolas','Monaco',monospace", fontSize: '0.82rem', lineHeight: 1.6, resize: 'vertical', boxSizing: 'border-box', outline: 'none' }} />
          {err && <div style={{ fontSize: '0.8rem', padding: '8px 12px', borderRadius: 6, background: 'rgba(209,59,26,0.1)', color: '#d13b1a' }}>{err}</div>}
          {result && (
            <div style={{ fontSize: '0.8rem', padding: '8px 12px', borderRadius: 6, background: 'rgba(62,144,65,0.1)', color: '#3e9041' }}>
              {result.created} page{result.created > 1 ? 's' : ''} importée{result.created > 1 ? 's' : ''} en mode {result.mode}.
            </div>
          )}
        </div>
        <div style={{ padding: '12px 20px', borderTop: '1px solid rgba(255,255,255,0.07)', display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
          <button onClick={onClose} style={{ ...BTN, background: 'transparent', color: '#888', border: '1px solid rgba(255,255,255,0.1)' }}>Annuler</button>
          <button onClick={submit} disabled={busy} style={{ ...BTN, background: '#5865f2', color: '#fff', opacity: busy ? 0.5 : 1 }}>
            <Upload size={13} /> {busy ? 'Import…' : 'Importer'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Main panel ────────────────────────────────────────────────────────────

export default function DocsPanel() {
  const [tree, setTree]           = useState([])
  const [selectedId, setSelectedId] = useState(null)
  const [page, setPage]           = useState(null)
  const [draftTitle, setDraftTitle]   = useState('')
  const [draftContent, setDraftContent] = useState('')
  const [draftIcon, setDraftIcon] = useState('')
  const [dirty, setDirty]         = useState(false)
  const [saving, setSaving]       = useState(false)
  const [loading, setLoading]     = useState(true)
  const [error, setError]         = useState('')
  const [expanded, setExpanded]   = useState(new Set())

  const [searchQ, setSearchQ]     = useState('')
  const [searchResults, setSearchResults] = useState(null)
  const [movingPage, setMovingPage] = useState(null)
  const [importParent, setImportParent] = useState(undefined)  // undefined = fermé
  const textareaRef = useRef(null)

  // ── Loaders
  const loadTree = useCallback(async () => {
    try { setTree(await api.getDocsTree()) }
    catch (e) { setError(e.message) }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { loadTree() }, [loadTree])

  // Ouvre tout au premier chargement pour que l'utilisateur voie la structure
  useEffect(() => {
    if (tree.length > 0 && expanded.size === 0) {
      const all = new Set()
      function walk(nodes) { for (const n of nodes) { all.add(n.id); if (n.children) walk(n.children) } }
      walk(tree)
      setExpanded(all)
    }
  }, [tree])

  // Charge la page sélectionnée
  useEffect(() => {
    if (!selectedId) { setPage(null); return }
    let cancel = false
    api.getDocsPage(selectedId).then(p => {
      if (cancel) return
      setPage(p)
      setDraftTitle(p.title ?? '')
      setDraftContent(p.content ?? '')
      setDraftIcon(p.icon ?? '')
      setDirty(false)
    }).catch(e => setError(e.message))
    return () => { cancel = true }
  }, [selectedId])

  // Recherche (debounced)
  useEffect(() => {
    const q = searchQ.trim()
    if (!q) { setSearchResults(null); return }
    const t = setTimeout(() => {
      api.searchDocs(q).then(setSearchResults).catch(() => setSearchResults([]))
    }, 180)
    return () => clearTimeout(t)
  }, [searchQ])

  // ── Actions
  function toggleExpand(id) {
    setExpanded(prev => {
      const next = new Set(prev)
      next.has(id) ? next.delete(id) : next.add(id)
      return next
    })
  }

  async function createPage(parentId = null) {
    try {
      const p = await api.createDocsPage({ parent_id: parentId, title: 'Nouvelle page', published: 0 })
      await loadTree()
      if (parentId) setExpanded(prev => new Set(prev).add(parentId))
      setSelectedId(p.id)
    } catch (e) { setError(e.message) }
  }

  async function savePage() {
    if (!selectedId) return
    setSaving(true); setError('')
    try {
      const updated = await api.updateDocsPage(selectedId, {
        title: draftTitle, content: draftContent, icon: draftIcon,
      })
      setPage(updated)
      setDirty(false)
      await loadTree()
    } catch (e) { setError(e.message) }
    finally { setSaving(false) }
  }

  async function togglePublished() {
    if (!selectedId) return
    const updated = await api.toggleDocsPage(selectedId)
    setPage(updated)
    await loadTree()
  }

  async function deletePage() {
    if (!selectedId || !page) return
    if (!confirm(`Supprimer « ${page.title || 'Sans titre'} » et toutes ses sous-pages ?`)) return
    await api.deleteDocsPage(selectedId)
    setSelectedId(null); setPage(null)
    await loadTree()
  }

  async function handleMove(id, body) {
    await api.moveDocsPage(id, body)
    await loadTree()
    if (selectedId === id) {
      const p = await api.getDocsPage(id)
      setPage(p)
    }
  }

  function exportPage(deep) {
    if (!selectedId) return
    const a = document.createElement('a')
    a.href = api.exportDocsUrl(selectedId, deep)
    a.download = ''
    document.body.appendChild(a)
    a.click()
    a.remove()
  }

  async function handleImport(body) {
    const r = await api.importDocsMarkdown(body)
    await loadTree()
    return r
  }

  // ── Rendu
  return (
    <div style={{ display: 'flex', flex: 1, minHeight: 0, background: '#0a0a0a', color: '#d0d0d0', fontSize: '0.9rem' }}>

      {/* ── Sidebar gauche : arbre + search ── */}
      <aside style={{ width: 300, borderRight: '1px solid rgba(255,255,255,0.07)', display: 'flex', flexDirection: 'column', minHeight: 0 }}>
        <div style={{ padding: '12px 12px 8px', borderBottom: '1px solid rgba(255,255,255,0.05)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10 }}>
            <BookMarked size={16} style={{ color: '#8a94f5' }} />
            <div style={{ fontWeight: 700, fontSize: '0.95rem', color: '#e8e8e8' }}>Documentation</div>
            <div style={{ flex: 1 }} />
            <button onClick={() => createPage(null)} title="Nouvelle page racine"
              style={{ background: 'rgba(88,101,242,0.15)', color: '#b8c0ff', border: '1px solid rgba(88,101,242,0.3)', borderRadius: 5, padding: '3px 7px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 3, fontSize: '0.72rem' }}>
              <Plus size={11} /> Page
            </button>
          </div>
          <div style={{ position: 'relative' }}>
            <Search size={12} style={{ position: 'absolute', left: 9, top: '50%', transform: 'translateY(-50%)', color: '#555' }} />
            <input value={searchQ} onChange={e => setSearchQ(e.target.value)} placeholder="Rechercher…"
              style={{ width: '100%', background: '#11151f', border: '1px solid rgba(255,255,255,0.08)', borderRadius: 6, padding: '6px 10px 6px 26px', color: '#d0d0d0', fontSize: '0.8rem', outline: 'none', boxSizing: 'border-box' }} />
            {searchQ && (
              <button onClick={() => setSearchQ('')} style={{ position: 'absolute', right: 4, top: '50%', transform: 'translateY(-50%)', background: 'none', border: 'none', cursor: 'pointer', color: '#555', padding: 4, display: 'flex' }}>
                <X size={11} />
              </button>
            )}
          </div>
        </div>

        <div style={{ flex: 1, overflowY: 'auto', padding: '8px 6px' }}>
          {loading && <div style={{ color: '#555', fontSize: '0.8rem', padding: '14px', textAlign: 'center' }}>Chargement…</div>}

          {searchResults ? (
            <div>
              <div style={{ color: '#555', fontSize: '0.68rem', fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', padding: '6px 8px' }}>
                {searchResults.length} résultat{searchResults.length !== 1 ? 's' : ''}
              </div>
              {searchResults.map(r => (
                <button key={r.id} onClick={() => setSelectedId(r.id)}
                  style={{ display: 'block', width: '100%', textAlign: 'left', padding: '6px 10px', border: 'none', background: selectedId === r.id ? 'rgba(88,101,242,0.15)' : 'transparent', borderRadius: 5, cursor: 'pointer', color: '#b8b8b8' }}>
                  <div style={{ fontSize: '0.82rem', fontWeight: 600, color: '#d0d0d0' }}>
                    {r.icon && <span style={{ marginRight: 5 }}>{r.icon}</span>}{r.title || 'Sans titre'}
                  </div>
                  {r.breadcrumb?.length > 1 && (
                    <div style={{ fontSize: '0.68rem', color: '#555', marginTop: 2 }}>
                      {r.breadcrumb.slice(0, -1).map(b => b.title || '—').join(' / ')}
                    </div>
                  )}
                </button>
              ))}
              {searchResults.length === 0 && (
                <div style={{ color: '#555', fontSize: '0.78rem', padding: '14px', textAlign: 'center', fontStyle: 'italic' }}>
                  Aucun résultat
                </div>
              )}
            </div>
          ) : (
            <div>
              {tree.map(n => (
                <TreeNode key={n.id} node={n} depth={0} selectedId={selectedId}
                  onSelect={setSelectedId} expanded={expanded} onToggle={toggleExpand}
                  onCreateChild={createPage} />
              ))}
              {!loading && tree.length === 0 && (
                <div style={{ color: '#555', fontSize: '0.78rem', padding: '14px', textAlign: 'center', fontStyle: 'italic' }}>
                  Aucune page. Crée ta première page.
                </div>
              )}
            </div>
          )}
        </div>

        <div style={{ padding: '8px 10px', borderTop: '1px solid rgba(255,255,255,0.05)', display: 'flex', gap: 6 }}>
          <button onClick={() => setImportParent(null)} title="Importer à la racine"
            style={{ flex: 1, ...BTN, padding: '6px 10px', background: 'rgba(255,255,255,0.04)', color: '#aaa', border: '1px solid rgba(255,255,255,0.08)', justifyContent: 'center', fontSize: '0.74rem' }}>
            <Upload size={11} /> Importer
          </button>
        </div>
      </aside>

      {/* ── Centre : éditeur ── */}
      <section style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
        {!page ? (
          <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#555', fontSize: '0.9rem', fontStyle: 'italic' }}>
            Sélectionne une page à gauche, ou crée-en une.
          </div>
        ) : (
          <>
            {/* Breadcrumb + actions */}
            <div style={{ padding: '10px 18px', borderBottom: '1px solid rgba(255,255,255,0.05)', display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
              <div style={{ flex: 1, minWidth: 0, display: 'flex', alignItems: 'center', gap: 4, fontSize: '0.76rem', color: '#666', overflow: 'hidden' }}>
                {page.breadcrumb?.slice(0, -1).map((b, i) => (
                  <span key={b.id} style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                    <button onClick={() => setSelectedId(b.id)} style={{ background: 'none', border: 'none', color: '#888', cursor: 'pointer', padding: 0, fontSize: '0.76rem' }}>
                      {b.title || '—'}
                    </button>
                    <ChevronRight size={10} style={{ color: '#444' }} />
                  </span>
                ))}
                <span style={{ color: '#b8b8b8', fontWeight: 600 }}>{page.title || 'Sans titre'}</span>
              </div>
              <div style={{ display: 'flex', gap: 6 }}>
                <button onClick={() => setMovingPage(page)} title="Déplacer"
                  style={{ ...BTN, padding: '4px 9px', fontSize: '0.73rem', background: 'transparent', color: '#888', border: '1px solid rgba(255,255,255,0.1)' }}>
                  <Move size={11} /> Déplacer
                </button>
                <button onClick={togglePublished}
                  title={page.published ? 'Rendre brouillon' : 'Publier'}
                  style={{ ...BTN, padding: '4px 9px', fontSize: '0.73rem', background: page.published ? 'rgba(62,144,65,0.15)' : 'transparent', color: page.published ? '#3e9041' : '#888', border: `1px solid ${page.published ? 'rgba(62,144,65,0.3)' : 'rgba(255,255,255,0.1)'}` }}>
                  {page.published ? <ToggleRight size={11} /> : <ToggleLeft size={11} />}
                  {page.published ? 'Publié' : 'Brouillon'}
                </button>
                <button onClick={() => exportPage(false)} title="Exporter cette page (.md)"
                  style={{ ...BTN, padding: '4px 9px', fontSize: '0.73rem', background: 'transparent', color: '#888', border: '1px solid rgba(255,255,255,0.1)' }}>
                  <Download size={11} /> Export
                </button>
                <button onClick={() => exportPage(true)} title="Exporter avec toutes les sous-pages"
                  style={{ ...BTN, padding: '4px 9px', fontSize: '0.73rem', background: 'transparent', color: '#888', border: '1px solid rgba(255,255,255,0.1)' }}>
                  <Download size={11} /> Export + enfants
                </button>
                <button onClick={() => setImportParent(page)} title="Importer sous cette page"
                  style={{ ...BTN, padding: '4px 9px', fontSize: '0.73rem', background: 'transparent', color: '#888', border: '1px solid rgba(255,255,255,0.1)' }}>
                  <Upload size={11} /> Import ici
                </button>
                <button onClick={deletePage} title="Supprimer"
                  style={{ ...BTN, padding: '4px 9px', fontSize: '0.73rem', background: 'transparent', color: '#a33', border: '1px solid rgba(170,40,40,0.3)' }}>
                  <Trash2 size={11} />
                </button>
              </div>
            </div>

            {/* Title + icon */}
            <div style={{ padding: '16px 18px 6px', display: 'flex', alignItems: 'center', gap: 10 }}>
              <input value={draftIcon} onChange={e => { setDraftIcon(e.target.value); setDirty(true) }}
                placeholder="📄" maxLength={4}
                style={{ width: 40, height: 40, textAlign: 'center', fontSize: '1.2rem', background: '#11151f', border: '1px solid rgba(255,255,255,0.08)', borderRadius: 6, color: '#e8e8e8', outline: 'none' }} />
              <input value={draftTitle} onChange={e => { setDraftTitle(e.target.value); setDirty(true) }}
                placeholder="Titre de la page"
                style={{ flex: 1, background: 'transparent', border: 'none', color: '#f0f0f0', fontSize: '1.5rem', fontWeight: 700, outline: 'none', fontFamily: 'inherit' }} />
              {dirty && (
                <button onClick={savePage} disabled={saving}
                  style={{ ...BTN, background: '#3e9041', color: '#fff', opacity: saving ? 0.5 : 1 }}>
                  <Check size={13} /> {saving ? 'Enregistrement…' : 'Enregistrer'}
                </button>
              )}
            </div>

            {/* Content editor */}
            <div style={{ padding: '6px 18px 18px', flex: 1, display: 'flex', minHeight: 0 }}>
              <MdToolbar textareaRef={textareaRef} value={draftContent}
                onChange={v => { setDraftContent(v); setDirty(true) }} />
            </div>

            {error && (
              <div style={{ padding: '8px 18px', background: 'rgba(209,59,26,0.1)', color: '#d13b1a', fontSize: '0.8rem', borderTop: '1px solid rgba(209,59,26,0.2)' }}>
                {error}
              </div>
            )}
          </>
        )}
      </section>

      {/* ── Modals ── */}
      {movingPage && (
        <MoveModal page={movingPage} tree={tree} onClose={() => setMovingPage(null)} onMove={handleMove} />
      )}
      {importParent !== undefined && (
        <ImportModal parent={importParent} onClose={() => setImportParent(undefined)} onImport={handleImport} />
      )}
    </div>
  )
}
