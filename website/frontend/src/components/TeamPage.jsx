import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Users, ChevronDown, ChevronUp, Mail } from 'lucide-react'
import { useLang } from '../context/LanguageContext'
import SEO from './SEO'
import logo from '../assets/logo.png'
import './TeamPage.css'

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:3001'

/* ── Carte poste (accordéon) ────────────────────────────────────── */
function RoleCard({ job }) {
  const { lang } = useLang()
  const [open, setOpen] = useState(false)

  const title = lang === 'fr' ? job.title_fr : (job.title_en || job.title_fr)
  const desc  = lang === 'fr' ? job.description_fr : (job.description_en || job.description_fr)
  const gameLabel = lang === 'fr' ? job.game_label_fr : (job.game_label_en || job.game_label_fr)

  return (
    <div className="tp__card">
      <div className="tp__card-header" onClick={() => setOpen(o => !o)}>
        <div className="tp__card-left">
          <span className="tp__card-type">{job.type}</span>
          {gameLabel && (
            <span className="tp__card-game" style={{ background: job.game_color ?? '#555' }}>
              {gameLabel}
            </span>
          )}
          <h3 className="tp__card-title">{title}</h3>
        </div>
        <button className="tp__card-toggle" aria-label="toggle">
          {open ? <ChevronUp size={18} /> : <ChevronDown size={18} />}
        </button>
      </div>

      {open && (
        <div className="tp__card-body">
          {desc ? (
            <p className="tp__card-desc">{desc}</p>
          ) : (
            <p className="tp__card-desc tp__card-desc--empty">—</p>
          )}
          {job.contact_email && (
            <a
              href={`mailto:${job.contact_email}?subject=${encodeURIComponent(title)}`}
              className="tp__card-cta"
            >
              <Mail size={15} /> {lang === 'fr' ? 'Contacter' : 'Contact us'}
            </a>
          )}
        </div>
      )}
    </div>
  )
}

/* ── Page principale ─────────────────────────────────────────────── */
export default function TeamPage() {
  const { lang, setLang, t } = useLang()
  const [jobs, setJobs] = useState([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetch(`${API_BASE}/api/jobs`)
      .then(r => r.json())
      .then(data => { setJobs(Array.isArray(data) ? data : []); setLoading(false) })
      .catch(() => setLoading(false))
  }, [])

  return (
    <div className="tp">
      <SEO
        title={lang === 'fr' ? 'Rejoindre l’équipe' : 'Join the team'}
        description={lang === 'fr'
          ? 'Small Box Studio recrute des passionnés. Découvrez les postes ouverts et rejoignez notre équipe de développeurs et créateurs.'
          : 'Small Box Studio is hiring passionate people. Browse open positions and join our team of developers and creators.'}
        url="/team"
        lang={lang}
      />
      {/* ── Header minimal ── */}
      <header className="tp__header">
        <div className="tp__header-left">
          <Link to="/" className="tp__logo">
            <img src={logo} alt="Small Box Studio" />
          </Link>
          <nav className="tp__nav">
            <Link to="/" className="tp__nav-link">
              {lang === 'fr' ? 'Accueil' : 'Home'}
            </Link>
            <span className="tp__nav-sep">›</span>
            <span className="tp__nav-current">
              {lang === 'fr' ? 'Équipe' : 'Team'}
            </span>
          </nav>
        </div>
        <button
          className="tp__lang-btn"
          onClick={() => setLang(lang === 'fr' ? 'en' : 'fr')}
          title={lang === 'fr' ? 'Switch to English' : 'Passer en français'}
        >
          {lang === 'fr' ? '🇬🇧' : '🇫🇷'}
        </button>
      </header>

      {/* ── Hero ── */}
      <section className="tp__hero">
        <div className="tp__hero-bg" />
        <div className="tp__hero-content">
          <span className="tp__hero-eyebrow">
            <Users size={16} />
            {t('team.eyebrow')}
          </span>
          <h1 className="tp__hero-title">{t('team.title')}</h1>
          <p className="tp__hero-sub">{t('team.subtitle')}</p>
        </div>
      </section>

      {/* ── Postes ── */}
      <section className="tp__roles">
        <div className="tp__roles-inner">

          {loading ? (
            <div className="tp__loader">
              <div className="tp__spinner" />
            </div>
          ) : jobs.length === 0 ? (
            <div className="tp__empty">
              <Users size={40} />
              <p>{t('team.empty')}</p>
            </div>
          ) : (
            <>
              <p className="tp__roles-intro">{t('team.intro')}</p>
              <div className="tp__list">
                {jobs.map(job => <RoleCard key={job.id} job={job} />)}
              </div>
            </>
          )}

        </div>
      </section>
    </div>
  )
}
