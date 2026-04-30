import { useState, useEffect, useRef, useMemo } from 'react'
import { Link, useParams, useNavigate } from 'react-router-dom'
import { BOOKS as STATIC_BOOKS } from '../data/openFrameworkRules'
import logo from '../assets/logo.png'
import './RulesBook.css'

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:3001'

/* ── Parser Markdown custom → blocs ── */
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

    // :::art N Titre
    if (/^:::art\s/.test(line)) {
      const match = line.match(/^:::art\s+(\S+)\s+(.+)$/)
      const number = match?.[1] || '?'
      const title  = match?.[2] || ''
      i++
      const content = collectBlock()
      blocks.push({ type: 'rule', number, title, text: content.join('\n').trim() })
      continue
    }

    // :::table
    if (/^:::table/.test(line)) {
      const rows = []
      i++
      while (i < lines.length && lines[i].trim() !== ':::') {
        if (lines[i].trim() !== '') rows.push(lines[i])
        i++
      }
      // Première ligne = en-têtes, ligne suivante "---" = séparateur
      const headers = rows[0]?.split('|').map(c => c.trim()) || []
      const dataRows = rows.slice(1).filter(r => !/^[-|\s]+$/.test(r)).map(r => r.split('|').map(c => c.trim()))
      blocks.push({ type: 'table', headers, rows: dataRows })
      i++; continue
    }

    // :::note
    if (/^:::note/.test(line)) {
      i++
      const content = collectBlock()
      blocks.push({ type: 'note', text: content.join('\n').trim() })
      continue
    }

    // ## Titre
    if (/^#{1,3}\s/.test(line)) {
      blocks.push({ type: 'heading', text: line.replace(/^#{1,3}\s/, '').trim() })
      i++; continue
    }

    // Liste
    if (/^[-*]\s/.test(line)) {
      const items = []
      while (i < lines.length && /^[-*]\s/.test(lines[i])) {
        items.push(lines[i].replace(/^[-*]\s/, '').trim())
        i++
      }
      blocks.push({ type: 'list', items }); continue
    }

    // Ligne vide
    if (line.trim() === '') { i++; continue }

    // Bloc ::: non reconnu → skip pour éviter boucle infinie
    if (/^:::/.test(line)) { i++; continue }

    // Paragraphe
    const para = []
    while (i < lines.length && lines[i].trim() !== '' && !/^#{1,3}\s/.test(lines[i]) && !/^[-*]\s/.test(lines[i]) && !/^:::/.test(lines[i])) {
      para.push(lines[i])
      i++
    }
    if (para.length) blocks.push({ type: 'paragraph', text: para.join('\n').trim() })
  }

  return blocks
}

/* ── Rendu inline (gras, italique, barré, code) ── */
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

/* ── Rendu d'un bloc de contenu ── */
function ContentBlock({ block }) {
  if (block.type === 'heading') {
    return <h2 className="rb__heading">{renderInline(block.text)}</h2>
  }
  if (block.type === 'paragraph') {
    return <p className="rb__paragraph">{renderInline(block.text)}</p>
  }
  if (block.type === 'note') {
    return <div className="rb__note"><p>{renderInline(block.text)}</p></div>
  }
  if (block.type === 'list') {
    return (
      <ul className="rb__list">
        {block.items.map((item, i) => <li key={i}>{renderInline(item)}</li>)}
      </ul>
    )
  }
  if (block.type === 'table') {
    return (
      <div className="rb__table-wrapper">
        <table className="rb__table">
          {block.headers?.length > 0 && (
            <thead>
              <tr>
                {block.headers.map((h, i) => <th key={i}>{renderInline(h)}</th>)}
              </tr>
            </thead>
          )}
          <tbody>
            {block.rows.map((row, ri) => (
              <tr key={ri}>
                {row.map((cell, ci) => <td key={ci}>{renderInline(cell)}</td>)}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    )
  }
  if (block.type === 'rule') {
    const innerBlocks = parseMd(block.text)
    return (
      <div className="rb__rule">
        <div className="rb__rule-header">
          <span className="rb__rule-num">Art. {block.number}</span>
          <strong className="rb__rule-title">{renderInline(block.title)}</strong>
        </div>
        <div className="rb__rule-text">
          {innerBlocks.map((inner, j) => <ContentBlock key={j} block={inner} />)}
        </div>
      </div>
    )
  }
  return null
}

/* ── Recherche ── */
function highlight(text, query) {
  if (!query) return text
  const re = new RegExp(`(${query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')})`, 'gi')
  const parts = text.split(re)
  return parts.map((p, i) =>
    re.test(p) ? <mark key={i} className="rb__search-mark">{p}</mark> : p
  )
}

function extractTextFromBlocks(blocks) {
  if (!Array.isArray(blocks)) return ''
  return blocks.map(b => {
    if (!b) return ''
    if (b.type === 'rule')      return `${b.title || ''} ${extractTextFromBlocks(parseMd(b.text || ''))}`
    if (b.type === 'note')      return extractTextFromBlocks(parseMd(b.text || ''))
    if (b.type === 'heading' || b.type === 'paragraph') return b.text || ''
    if (b.type === 'list')      return (b.items || []).join(' ')
    if (b.type === 'table')     return [...(b.headers || []), ...(b.rows || []).flat()].join(' ')
    return ''
  }).join(' ')
}

function extractText(raw) {
  // raw peut être une string (contenu API) ou un tableau de blocs (STATIC_BOOKS)
  if (Array.isArray(raw)) return extractTextFromBlocks(raw)
  if (!raw) return ''
  return raw
    .replace(/^:::art\s+\S+\s+/gm, '')
    .replace(/^:::(?:note|table|art)?/gm, '')
    .replace(/^#{1,3}\s+/gm, '')
    .replace(/\*\*(.+?)\*\*/g, '$1')
    .replace(/\*(.+?)\*/g, '$1')
    .replace(/~~(.+?)~~/g, '$1')
    .replace(/`([^`]+)`/g, '$1')
    .replace(/^[-*]\s/gm, '')
}

function SearchBar({ books, bookId, onNavigate }) {
  const [query, setQuery]       = useState('')
  const [open, setOpen]         = useState(false)
  const [dropPos, setDropPos]   = useState({ top: 0, left: 0, width: 0 })
  const inputRef                = useRef(null)
  const containerRef            = useRef(null)

  function openWithPos() {
    if (containerRef.current) {
      const r = containerRef.current.getBoundingClientRect()
      setDropPos({ top: r.bottom + 4, left: r.left, width: r.width })
    }
    setOpen(true)
  }

  // Index : [{bookId, bookTitle, chapterId, chapterTitle, excerpt, matchText}]
  const index = useMemo(() => {
    const entries = []
    for (const book of books) {
      for (const chapter of (book.chapters || [])) {
        const raw = chapter.content ?? ''
        const plain = extractText(raw)
        entries.push({
          bookId:       book.id,
          bookTitle:    book.title,
          bookIcon:     book.icon,
          chapterId:    chapter.id,
          chapterTitle: chapter.title,
          plain,
          raw,
        })
      }
    }
    return entries
  }, [books])

  const results = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (q.length < 2) return []
    return index
      .filter(e =>
        e.chapterTitle.toLowerCase().includes(q) ||
        e.plain.toLowerCase().includes(q) ||
        e.bookTitle.toLowerCase().includes(q)
      )
      .slice(0, 12)
      .map(e => {
        const pos = e.plain.toLowerCase().indexOf(q)
        let excerpt = ''
        if (pos !== -1) {
          const start = Math.max(0, pos - 40)
          const end   = Math.min(e.plain.length, pos + q.length + 80)
          excerpt = (start > 0 ? '…' : '') + e.plain.slice(start, end) + (end < e.plain.length ? '…' : '')
        }
        return { ...e, excerpt }
      })
  }, [query, index])

  // Ferme le dropdown si clic en dehors
  useEffect(() => {
    function onClick(e) {
      if (containerRef.current && !containerRef.current.contains(e.target)) setOpen(false)
    }
    document.addEventListener('mousedown', onClick)
    return () => document.removeEventListener('mousedown', onClick)
  }, [])

  return (
    <div ref={containerRef} className="rb__search">
      <div className="rb__search-input-wrap">
        <svg className="rb__search-icon" viewBox="0 0 20 20" fill="none">
          <circle cx="8.5" cy="8.5" r="5.5" stroke="currentColor" strokeWidth="1.5"/>
          <path d="M13 13l3 3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
        </svg>
        <input
          ref={inputRef}
          className="rb__search-input"
          type="text"
          placeholder="Rechercher…"
          value={query}
          onChange={e => { setQuery(e.target.value); openWithPos() }}
          onFocus={() => openWithPos()}
        />
        {query && (
          <button className="rb__search-clear" onClick={() => { setQuery(''); inputRef.current?.focus() }}>✕</button>
        )}
      </div>

      {open && query.trim().length >= 2 && (
        <div className="rb__search-results" style={{ top: dropPos.top, left: dropPos.left, width: dropPos.width }}>
          {results.length === 0 ? (
            <div className="rb__search-empty">Aucun résultat</div>
          ) : (
            results.map((r, i) => (
              <button
                key={i}
                className="rb__search-result"
                onClick={() => { onNavigate(r.bookId, r.chapterId); setOpen(false); setQuery('') }}
              >
                <div className="rb__search-result-title">{r.chapterTitle}</div>
                {r.excerpt && (
                  <div className="rb__search-result-excerpt">
                    {highlight(r.excerpt, query.trim())}
                  </div>
                )}
              </button>
            ))
          )}
        </div>
      )}
    </div>
  )
}

export default function RulesBook() {
  const { bookId, chapterId } = useParams()
  const navigate = useNavigate()
  const [books, setBooks] = useState([])
  const [loadingApi, setLoadingApi] = useState(true)

  useEffect(() => {
    fetch(`${API_BASE}/api/rules/sl-books/public`)
      .then(r => r.ok ? r.json() : null)
      .then(data => { setBooks(data && data.length > 0 ? data : STATIC_BOOKS) })
      .catch(() => { setBooks(STATIC_BOOKS) })
      .finally(() => setLoadingApi(false))
  }, [])

  const book = books.find(b => b.id === bookId)

  useEffect(() => {
    if (!loadingApi && !book) navigate('/game/small-life/rules')
  }, [book, loadingApi, navigate])

  // Redirige vers le premier chapitre si pas de chapterId dans l'URL
  useEffect(() => {
    if (!loadingApi && book && !chapterId && book.chapters.length > 0) {
      navigate(`/game/small-life/rules/${bookId}/${book.chapters[0].id}`, { replace: true })
    }
  }, [book, chapterId, loadingApi, bookId, navigate])

  if (loadingApi) return <div style={{ minHeight: '60vh', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#444', fontSize: '0.85rem' }}>Chargement…</div>

  if (!book) return null

  const chapterIdx = chapterId ? book.chapters.findIndex(c => c.id === chapterId) : 0
  const chapter    = book.chapters[chapterIdx] ?? book.chapters[0]
  const prevCh     = chapterIdx > 0 ? book.chapters[chapterIdx - 1] : null
  const nextCh     = chapterIdx < book.chapters.length - 1 ? book.chapters[chapterIdx + 1] : null

  if (!chapter) return null

  const blocks = typeof chapter.content === 'string'
    ? parseMd(chapter.content)
    : Array.isArray(chapter.content) ? chapter.content : []

  return (
    <div className="rb" style={{ '--accent': book.cover_accent, '--cover': book.cover_color }}>

      {/* ── Header ── */}
      <header className="rb__header">
        <div className="rb__header-left">
          <Link to="/game/small-life" className="rb__logo">
            <img src={logo} alt="Small Box Studio" />
          </Link>
          <nav className="rb__breadcrumb">
            <Link to="/" className="rb__breadcrumb-link">Accueil</Link>
            <span className="rb__sep">›</span>
            <Link to="/game/small-life" className="rb__breadcrumb-link">OpenFramework</Link>
            <span className="rb__sep">›</span>
            <Link to="/game/small-life/rules" className="rb__breadcrumb-link">Règlements</Link>
            <span className="rb__sep">›</span>
            <Link to={`/game/small-life/rules/${bookId}`} className="rb__breadcrumb-link">{book.title}</Link>
            <span className="rb__sep">›</span>
            <span className="rb__breadcrumb-current">{chapter.title}</span>
          </nav>
        </div>
        <div className="rb__header-right">
          <span className="rb__book-badge">{book.icon} {book.title}</span>
        </div>
      </header>

      {/* ── Layout principal ── */}
      <div className="rb__layout">

        {/* ── Sidebar chapitres ── */}
        <aside className="rb__sidebar">
          <SearchBar
            books={books}
            bookId={bookId}
            onNavigate={(bId, chId) => navigate(`/game/small-life/rules/${bId}/${chId}`)}
          />
          <p className="rb__sidebar-label">Chapitres</p>
          <nav className="rb__sidebar-nav">
            {book.chapters.map((ch, i) => (
              <Link
                key={ch.id}
                to={`/game/small-life/rules/${bookId}/${ch.id}`}
                className={`rb__sidebar-item ${ch.id === chapter.id ? 'rb__sidebar-item--active' : ''}`}
              >
                <span className="rb__sidebar-num">{String(i + 1).padStart(2, '0')}</span>
                <span className="rb__sidebar-name">{ch.title}</span>
              </Link>
            ))}
          </nav>
          <div className="rb__sidebar-back">
            <Link to="/game/small-life/rules" className="rb__back-link">
              ← Retour aux livres
            </Link>
          </div>
        </aside>

        {/* ── Contenu du chapitre ── */}
        <main className="rb__content">
          <section className="rb__chapter">
            <div className="rb__chapter-header">
              <span className="rb__chapter-eyebrow">Chapitre {chapterIdx + 1}</span>
              <h1 className="rb__chapter-title">{chapter.title}</h1>
              <div className="rb__chapter-line" />
            </div>
            <div className="rb__chapter-body">
              {blocks.map((block, j) => (
                <ContentBlock key={j} block={block} />
              ))}
              {blocks.length === 0 && (
                <p style={{ color: '#555', fontStyle: 'italic', textAlign: 'center', padding: '40px 0' }}>
                  Ce chapitre est vide.
                </p>
              )}
            </div>
          </section>

          {/* ── Navigation précédent / suivant ── */}
          <nav className="rb__chapter-nav">
            {prevCh ? (
              <Link to={`/game/small-life/rules/${bookId}/${prevCh.id}`} className="rb__chapter-nav-btn rb__chapter-nav-btn--prev">
                <span className="rb__chapter-nav-arrow">←</span>
                <span>
                  <span className="rb__chapter-nav-label">Précédent</span>
                  <span className="rb__chapter-nav-name">{prevCh.title}</span>
                </span>
              </Link>
            ) : <div />}
            {nextCh ? (
              <Link to={`/game/small-life/rules/${bookId}/${nextCh.id}`} className="rb__chapter-nav-btn rb__chapter-nav-btn--next">
                <span>
                  <span className="rb__chapter-nav-label">Suivant</span>
                  <span className="rb__chapter-nav-name">{nextCh.title}</span>
                </span>
                <span className="rb__chapter-nav-arrow">→</span>
              </Link>
            ) : (
              <Link to="/game/small-life/rules" className="rb__chapter-nav-btn rb__chapter-nav-btn--next">
                <span>
                  <span className="rb__chapter-nav-label">Terminé</span>
                  <span className="rb__chapter-nav-name">Retour aux livres</span>
                </span>
                <span className="rb__chapter-nav-arrow">↩</span>
              </Link>
            )}
          </nav>
        </main>
      </div>
    </div>
  )
}
