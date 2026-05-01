import { useState, useEffect } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { BOOKS as STATIC_BOOKS } from '../data/openFrameworkRules'
import { Library, ScrollText, Shield, BookOpen, BookMarked, BookText, Scale } from 'lucide-react'
import logo from '../assets/logo.png'
import './RulesLibrary.css'

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:3001'

// Mappe l'icon (emoji ou id livre) vers un composant Lucide
const BOOK_ICONS = {
  server:  ScrollText,
  police:  Shield,
  // fallback
  '📜': ScrollText,
  '🚔': Shield,
}
function BookIcon({ bookId, icon, size = 48, className }) {
  const Icon = BOOK_ICONS[bookId] || BOOK_ICONS[icon] || BookOpen
  return <Icon size={size} className={className} strokeWidth={1.5} />
}

export default function RulesLibrary() {
  const navigate = useNavigate()
  const [books, setBooks] = useState(STATIC_BOOKS)

  useEffect(() => {
    fetch(`${API_BASE}/api/rules/sl-books/public`)
      .then(r => r.ok ? r.json() : null)
      .then(data => { if (data && data.length > 0) setBooks(data) })
      .catch(() => {})
  }, [])

  return (
    <div className="rl">
      {/* ── Header ── */}
      <header className="rl__header">
        <div className="rl__header-left">
          <Link to="/game/small-life" className="rl__logo">
            <img src={logo} alt="OpenFramework" />
          </Link>
          <nav className="rl__breadcrumb">
            <Link to="/" className="rl__breadcrumb-link">Accueil</Link>
            <span className="rl__breadcrumb-sep">›</span>
            <Link to="/game/small-life" className="rl__breadcrumb-link">OpenFramework</Link>
            <span className="rl__breadcrumb-sep">›</span>
            <span className="rl__breadcrumb-current">Règlements</span>
          </nav>
        </div>
      </header>

      {/* ── Ambiance bibliothèque ── */}
      <div className="rl__room">
        <div className="rl__shelf-dust" />

        <div className="rl__title-area">
          <p className="rl__eyebrow"><Library size={16} style={{ display:'inline', verticalAlign:'middle', marginRight:8 }} />Bibliothèque de OpenFramework</p>
          <h1 className="rl__title">Choisissez votre livre</h1>
          <p className="rl__subtitle">Consultez les règlements du serveur.</p>
        </div>

        {/* ── Étagère ── */}
        <div className="rl__shelf">
          <div className="rl__shelf-wood rl__shelf-wood--top" />

          <div className="rl__books">
            {books.map((book, i) => (
              <button
                key={book.id}
                className="rl__book"
                style={{
                  '--cover': book.cover_color,
                  '--accent': book.cover_accent,
                  '--spine': book.spine_color,
                  '--delay': `${i * 0.12}s`,
                }}
                onClick={() => navigate(`/game/small-life/rules/${book.id}`)}
                title={book.title}
              >
                {/* Tranche (spine) */}
                <div className="rl__book-spine">
                  <BookIcon bookId={book.id} icon={book.icon} size={22} className="rl__book-spine-icon" />
                </div>

                {/* Couverture avant */}
                <div className="rl__book-cover">
                  <div className="rl__book-cover-deco" />
                  <BookIcon bookId={book.id} icon={book.icon} size={52} className="rl__book-cover-icon" />
                  <span className="rl__book-cover-title">{book.title}</span>
                  <span className="rl__book-cover-sub">{book.subtitle}</span>
                  <div className="rl__book-cover-chapters">
                    {book.chapters.length} chapitre{book.chapters.length > 1 ? 's' : ''}
                  </div>
                </div>

                {/* Pages latérales */}
                <div className="rl__book-pages">
                  {[...Array(6)].map((_, p) => (
                    <div key={p} className="rl__book-page-slice" style={{ '--pi': p }} />
                  ))}
                </div>
              </button>
            ))}

          </div>

          <div className="rl__shelf-wood rl__shelf-wood--bottom" />
        </div>
      </div>
    </div>
  )
}
