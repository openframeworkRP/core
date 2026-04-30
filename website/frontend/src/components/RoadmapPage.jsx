import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Map, Clock, CheckCircle2, Circle, Loader2, FlaskConical } from 'lucide-react'
import { useLang } from '../context/LanguageContext'
import SEO from './SEO'
import logo from '../assets/logo.png'
import './RoadmapPage.css'

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:3001'

// Mapping statut interne → libellé public + icône + couleur
function getStatusMeta(status, lang) {
  switch (status) {
    case 'done':
      return { label: lang === 'fr' ? 'Terminé' : 'Done', icon: CheckCircle2, color: '#3e9041' }
    case 'in_progress':
      return { label: lang === 'fr' ? 'En cours' : 'In progress', icon: Loader2, color: '#e07b39' }
    case 'to_test':
      return { label: lang === 'fr' ? 'En test' : 'In testing', icon: FlaskConical, color: '#6ea8fe' }
    default:
      // todo, bug, v2 → "À venir" côté public
      return { label: lang === 'fr' ? 'À venir' : 'Planned', icon: Circle, color: '#888888' }
  }
}

function formatDate(iso, lang) {
  if (!iso) return ''
  const d = new Date(iso)
  return d.toLocaleDateString(lang === 'fr' ? 'fr-FR' : 'en-US', {
    day: 'numeric', month: 'long', year: 'numeric',
  })
}

function MilestoneCard({ ms, lang }) {
  const total = ms.tasks.length
  const done = ms.tasks.filter(t => t.status === 'done').length
  const pct = total ? Math.round((done / total) * 100) : 0
  const isComplete = total > 0 && done === total

  return (
    <article className="rm__card" style={{ '--ms-color': ms.color }}>
      <div className="rm__card-strip" />

      <header className="rm__card-head">
        <div className="rm__card-titles">
          <h2 className="rm__card-title">{ms.name}</h2>
          <div className="rm__card-meta">
            <span className="rm__card-date">
              <Clock size={13} />
              {formatDate(ms.date, lang)}
            </span>
            {isComplete && (
              <span className="rm__card-badge rm__card-badge--done">
                <CheckCircle2 size={12} />
                {lang === 'fr' ? 'Terminé' : 'Completed'}
              </span>
            )}
          </div>
        </div>
      </header>

      {ms.description && (
        <p className="rm__card-desc">{ms.description}</p>
      )}

      {total > 0 && (
        <div className="rm__progress">
          <div className="rm__progress-head">
            <span className="rm__progress-label">
              {lang === 'fr' ? 'Progression' : 'Progress'}
            </span>
            <span className="rm__progress-pct">{done}/{total} · {pct}%</span>
          </div>
          <div className="rm__progress-bar">
            <div className="rm__progress-fill" style={{ width: `${pct}%` }} />
          </div>
        </div>
      )}

      {total > 0 && (
        <ul className="rm__tasks">
          {ms.tasks.map(t => {
            const meta = getStatusMeta(t.status, lang)
            const Icon = meta.icon
            return (
              <li key={t.id} className="rm__task">
                <span className="rm__task-icon" style={{ color: meta.color }}>
                  <Icon size={14} />
                </span>
                <span className={`rm__task-text${t.status === 'done' ? ' rm__task-text--done' : ''}`}>
                  {t.text}
                </span>
                <span className="rm__task-status" style={{ color: meta.color, borderColor: `${meta.color}40` }}>
                  {meta.label}
                </span>
              </li>
            )
          })}
        </ul>
      )}
    </article>
  )
}

export default function RoadmapPage() {
  const { lang, setLang } = useLang()
  const [milestones, setMilestones] = useState([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetch(`${API_BASE}/api/hub/roadmap`)
      .then(r => r.json())
      .then(data => {
        setMilestones(Array.isArray(data.milestones) ? data.milestones : [])
        setLoading(false)
      })
      .catch(() => setLoading(false))
  }, [])

  return (
    <div className="rm">
      <SEO
        title={lang === 'fr' ? 'Roadmap publique' : 'Public roadmap'}
        description={lang === 'fr'
          ? 'Découvrez la roadmap publique de Small Box Studio : milestones, fonctionnalités prévues et avancement en temps réel.'
          : 'Discover Small Box Studio public roadmap: milestones, planned features and live progress.'}
        url="/roadmap"
        lang={lang}
      />

      <header className="rm__header">
        <div className="rm__header-left">
          <Link to="/" className="rm__logo">
            <img src={logo} alt="Small Box Studio" />
          </Link>
          <nav className="rm__nav">
            <Link to="/" className="rm__nav-link">
              {lang === 'fr' ? 'Accueil' : 'Home'}
            </Link>
            <span className="rm__nav-sep">›</span>
            <span className="rm__nav-current">
              {lang === 'fr' ? 'Roadmap' : 'Roadmap'}
            </span>
          </nav>
        </div>
        <button
          className="rm__lang-btn"
          onClick={() => setLang(lang === 'fr' ? 'en' : 'fr')}
          title={lang === 'fr' ? 'Switch to English' : 'Passer en français'}
        >
          {lang === 'fr' ? '🇬🇧' : '🇫🇷'}
        </button>
      </header>

      <section className="rm__hero">
        <div className="rm__hero-bg" />
        <div className="rm__hero-content">
          <span className="rm__hero-eyebrow">
            <Map size={16} />
            {lang === 'fr' ? 'Notre route' : 'Our path'}
          </span>
          <h1 className="rm__hero-title">
            {lang === 'fr' ? 'Roadmap' : 'Roadmap'}
          </h1>
          <p className="rm__hero-sub">
            {lang === 'fr'
              ? 'Suivez en temps réel les grandes étapes du studio et l\'avancement des fonctionnalités.'
              : 'Follow studio milestones and feature progress in real time.'}
          </p>
        </div>
      </section>

      <section className="rm__list-section">
        <div className="rm__list-inner">
          {loading ? (
            <div className="rm__loader">
              <div className="rm__spinner" />
            </div>
          ) : milestones.length === 0 ? (
            <div className="rm__empty">
              <Map size={40} />
              <p>
                {lang === 'fr'
                  ? 'Aucun milestone public pour le moment. Revenez bientôt !'
                  : 'No public milestone yet. Come back soon!'}
              </p>
            </div>
          ) : (
            <div className="rm__timeline">
              {milestones.map(ms => (
                <MilestoneCard key={ms.id} ms={ms} lang={lang} />
              ))}
            </div>
          )}
        </div>
      </section>
    </div>
  )
}
