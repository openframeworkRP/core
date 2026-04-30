import { useEffect, useState } from 'react'
import { Link, useParams, useNavigate } from 'react-router-dom'
import { useLang } from '../context/LanguageContext'
import { BookOpen, Bug, X, Send, ChevronDown, ChevronUp, MessageSquare, ScrollText } from 'lucide-react'
import SEO from './SEO'
import openFrameworkAnim from '../assets/game/small-life/anim.webm'
import logo from '../assets/logo.png'
import './GamePage.css'

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:3001'

const STATUS_COLORS = {
  pending:   { bg: '#3b3200', text: '#facc15', border: '#854d0e' },
  confirmed: { bg: '#1e1b4b', text: '#a5b4fc', border: '#4338ca' },
  patched:   { bg: '#052e16', text: '#4ade80', border: '#166534' },
  wontfix:   { bg: '#1c1c1c', text: '#71717a', border: '#3f3f46' },
}

/* ── Données par jeu ────────────────────────────────────────────── */
const GAMES = {
  'small-life': {
    slug:      'core',
    titleKey:  'games.core.title',
    genreKey:  'games.core.genre',
    descKey:   'games.core.desc',
    tags:      ['games.core.tag_rp', 'games.core.tag_multi'],
    color:     '#e07b39',
    video:     openFrameworkAnim,
    sboxUrl:   'https://sbox.game/openframework/core',
  },
}

export default function GamePage() {
  const { slug }    = useParams()
  const { lang, t, setLang } = useLang()
  const navigate    = useNavigate()
  const game        = GAMES[slug]

  const [bugs,        setBugs]        = useState([])
  const [modalOpen,   setModalOpen]   = useState(false)
  const [bugForm,     setBugForm]     = useState({ title: '', description: '' })
  const [bugStatus,   setBugStatus]   = useState(null) // null | 'success' | 'duplicate' | 'error' | 'loading'
  const [expanded,    setExpanded]    = useState({})

  useEffect(() => {
    if (!game) navigate('/')
  }, [game, navigate])

  useEffect(() => {
    if (!game) return
    fetch(`${API_BASE}/api/bugs?game=${game.slug}`)
      .then(r => r.json())
      .then(setBugs)
      .catch(() => {})
  }, [game])

  async function submitBug(e) {
    e.preventDefault()
    setBugStatus('loading')
    try {
      const res = await fetch(`${API_BASE}/api/bugs`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ game_slug: game.slug, ...bugForm }),
      })
      const data = await res.json()
      if (!res.ok) { setBugStatus('error_' + (data.error || res.statusText)); return }
      setBugStatus(data.duplicate_warning ? 'duplicate' : 'success')
      setBugForm({ title: '', description: '' })
    } catch { setBugStatus('error') }
  }

  function closeModal() { setModalOpen(false); setBugStatus(null); setBugForm({ title: '', description: '' }) }

  if (!game) return null

  const seoTitle = t(game.titleKey)
  const seoDesc  = t(game.descKey)
  const seoKeywords = slug === 'small-life'
    ? 'OpenFramework, DarkRP, Roleplay, RP, S&Box, France, français, serveur DarkRP français, Roleplay S&Box, RP France, S&Box France, DarkRP français, Small Box Studio'
    : `${seoTitle}, S&Box, DarkRP, Roleplay, France, français`

  return (
    <div className="gp">
      <SEO
        title={seoTitle}
        description={seoDesc}
        keywords={seoKeywords}
        url={`/game/${slug}`}
        type="website"
        lang={lang}
        jsonLd={{
          '@context': 'https://schema.org',
          '@type': 'VideoGame',
          name: seoTitle,
          description: seoDesc,
          genre: [t(game.genreKey), 'Roleplay', 'RP', 'DarkRP'],
          inLanguage: 'fr',
          url: `https://openframework.com/game/${slug}`,
          publisher: { '@type': 'Organization', name: 'Small Box Studio', foundingLocation: { '@type': 'Country', name: 'France' } },
          applicationCategory: 'Game',
        }}
      />
      {/* ── Header minimal ── */}
      <header className="gp__header">
        <div className="gp__header-left">
          <Link to="/" className="gp__logo">
            <img src={logo} alt="Small Box Studio" />
          </Link>
          <nav className="gp__breadcrumb">
            <Link to="/" className="gp__breadcrumb-link">
              {lang === 'fr' ? 'Accueil' : 'Home'}
            </Link>
            <span className="gp__breadcrumb-sep">›</span>
            <span className="gp__breadcrumb-current">{t(game.titleKey)}</span>
          </nav>
        </div>
        <button
          className="gp__lang-btn"
          onClick={() => setLang(lang === 'fr' ? 'en' : 'fr')}
          title={lang === 'fr' ? 'Switch to English' : 'Passer en français'}
        >
          {lang === 'fr' ? '🇬🇧' : '🇫🇷'}
        </button>
      </header>

      {/* ── Hero ── */}
      <section className="gp__hero">
        <div className="gp__hero-media">
          <video
            src={game.video}
            className="gp__hero-video"
            autoPlay
            loop
            muted
            playsInline
          />
          <div className="gp__hero-overlay" />
        </div>

        <div className="gp__hero-content">
          <span className="gp__genre" style={{ background: game.color }}>{t(game.genreKey)}</span>
          <h1 className="gp__title">{t(game.titleKey)}</h1>
          <div className="gp__tags">
            {game.tags.map(tk => (
              <span key={tk} className="gp__tag">{t(tk)}</span>
            ))}
          </div>
          <div className="gp__hero-actions">
            <Link
              to={`/devblog?s=${game.slug}`}
              className="gp__btn gp__btn--primary"
              style={{ '--game-color': game.color }}
            >
              <BookOpen size={17} /> {t('games.core.devblog_cta')}
            </Link>
            {slug === 'small-life' && (
              <Link
                to="/game/small-life/rules"
                className="gp__btn gp__btn--rules"
                style={{ '--game-color': game.color }}
              >
                <ScrollText size={17} /> {lang === 'fr' ? 'Voir les règles' : 'See the rules'}
              </Link>
            )}
            {game.sboxUrl && (
              <a
                href={game.sboxUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="gp__btn gp__btn--sbox"
              >
                <svg className="gp__sbox-icon" viewBox="0 0 24 24" fill="currentColor" width="18" height="18">
                  <path d="M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" fill="none"/>
                </svg>
                {t('games.core.sbox_cta')}
              </a>
            )}
          </div>
        </div>
      </section>

      {/* ── Description ── */}
      <section className="gp__about">
        <div className="gp__about-inner">
          <h2 className="gp__about-title">{t('games.core.about_title')}</h2>
          <p className="gp__about-desc">{t(game.descKey)}</p>
          <button
            className="gp__bug-report-btn"
            style={{ '--game-color': game.color }}
            onClick={() => setModalOpen(true)}
          >
            <Bug size={16} /> {t('bugs.report_btn')}
          </button>
        </div>
      </section>

      {/* ── Bugs publics connus ── */}
      {bugs.length > 0 && (
        <section className="gp__bugs">
          <div className="gp__bugs-inner">
            <h2 className="gp__bugs-title">{t('bugs.section_title')}</h2>
            <p className="gp__bugs-subtitle">{t('bugs.section_subtitle')}</p>
            <ul className="gp__bugs-list">
              {bugs.map(bug => {
                const sc = STATUS_COLORS[bug.status] || STATUS_COLORS.pending
                const isOpen = expanded[bug.id]
                return (
                  <li key={bug.id} className="gp__bug-item">
                    <div className="gp__bug-header" onClick={() => setExpanded(e => ({ ...e, [bug.id]: !e[bug.id] }))}>
                      <span className="gp__bug-status" style={{ background: sc.bg, color: sc.text, border: `1px solid ${sc.border}` }}>
                        {t(`bugs.status_${bug.status}`)}
                      </span>
                      <span className="gp__bug-title">{bug.title}</span>
                      <span className="gp__bug-chevron">{isOpen ? <ChevronUp size={15} /> : <ChevronDown size={15} />}</span>
                    </div>
                    {isOpen && (
                      <div className="gp__bug-body">
                        {bug.description && <p className="gp__bug-desc">{bug.description}</p>}
                        {bug.comments?.length > 0 && (
                          <div className="gp__bug-comments">
                            <div className="gp__bug-comments-title"><MessageSquare size={13} /> {t('bugs.comments_title')}</div>
                            {bug.comments.map(c => (
                              <div key={c.id} className="gp__bug-comment">
                                <span className="gp__bug-comment-author">{c.author}</span>
                                <span className="gp__bug-comment-date">{new Date(c.created_at).toLocaleDateString(lang === 'fr' ? 'fr-FR' : 'en-US')}</span>
                                <p className="gp__bug-comment-text">{c.content}</p>
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    )}
                  </li>
                )
              })}
            </ul>
          </div>
        </section>
      )}

      {/* ── Modal signalement ── */}
      {modalOpen && (
        <div className="gp__modal-overlay" onClick={e => e.target === e.currentTarget && closeModal()}>
          <div className="gp__modal">
            <div className="gp__modal-header">
              <h3><Bug size={18} /> {t('bugs.modal_title')}</h3>
              <button className="gp__modal-close" onClick={closeModal}><X size={18} /></button>
            </div>

            {(bugStatus === 'success' || bugStatus === 'duplicate') ? (
              <div className="gp__modal-success">
                <p>{t('bugs.modal_success')}</p>
                {bugStatus === 'duplicate' && <p className="gp__modal-duplicate">{t('bugs.modal_duplicate')}</p>}
                <button className="gp__bug-report-btn" style={{ '--game-color': game.color }} onClick={closeModal}>OK</button>
              </div>
            ) : (
              <form className="gp__modal-form" onSubmit={submitBug}>
                <label>
                  {t('bugs.modal_title_label')}
                  <input
                    value={bugForm.title}
                    onChange={e => setBugForm(f => ({ ...f, title: e.target.value }))}
                    placeholder={t('bugs.modal_title_placeholder')}
                    required maxLength={120}
                  />
                </label>
                <label>
                  {t('bugs.modal_desc_label')}
                  <textarea
                    value={bugForm.description}
                    onChange={e => setBugForm(f => ({ ...f, description: e.target.value }))}
                    placeholder={t('bugs.modal_desc_placeholder')}
                    rows={4} maxLength={1000}
                  />
                </label>
                {typeof bugStatus === 'string' && bugStatus.startsWith('error') && (
                  <p className="gp__modal-error">{bugStatus.replace('error_', '') || 'Erreur, réessaie.'}</p>
                )}
                <div className="gp__modal-actions">
                  <button type="button" className="gp__modal-cancel" onClick={closeModal}>{t('bugs.modal_cancel')}</button>
                  <button
                    type="submit"
                    className="gp__bug-report-btn"
                    style={{ '--game-color': game.color }}
                    disabled={bugStatus === 'loading'}
                  >
                    <Send size={15} /> {bugStatus === 'loading' ? '…' : t('bugs.modal_submit')}
                  </button>
                </div>
              </form>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
