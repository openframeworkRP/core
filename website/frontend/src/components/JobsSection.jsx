import { useEffect, useRef, useState } from 'react'
import { Briefcase, MessageCircle, ChevronDown, ChevronUp } from 'lucide-react'
import './JobsSection.css'
import { useLang } from '../context/LanguageContext'

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:3001'

function JobCard({ job }) {
  const { lang } = useLang()
  const [open, setOpen] = useState(false)

  const title = lang === 'fr' ? job.title_fr : (job.title_en || job.title_fr)
  const desc  = lang === 'fr' ? job.description_fr : (job.description_en || job.description_fr)

  return (
    <div className="jobs__card">
      <div className="jobs__card-header" onClick={() => setOpen(o => !o)}>
        <div className="jobs__card-left">
          <span className="jobs__card-type">{job.type}</span>
          {job.game_label_fr && (
            <span
              className="jobs__card-game"
              style={{ background: job.game_color ?? '#555' }}
            >
              {lang === 'fr' ? job.game_label_fr : (job.game_label_en || job.game_label_fr)}
            </span>
          )}
          <h3 className="jobs__card-title">{title}</h3>
        </div>
        <button className="jobs__card-toggle" aria-label="toggle">
          {open ? <ChevronUp size={18} /> : <ChevronDown size={18} />}
        </button>
      </div>

      {open && (
        <div className="jobs__card-body">
          {desc ? (
            <p className="jobs__card-desc">{desc}</p>
          ) : (
            <p className="jobs__card-desc jobs__card-desc--empty">—</p>
          )}
          <a
            href="https://discord.gg/sboxstudio"
            target="_blank"
            rel="noopener noreferrer"
            className="jobs__card-cta"
          >
            <MessageCircle size={15} /> Postuler sur Discord
          </a>
        </div>
      )}
    </div>
  )
}

export default function JobsSection() {
  const { t } = useLang()
  const sectionRef = useRef(null)
  const [jobs, setJobs] = useState([])

  useEffect(() => {
    fetch(`${API_BASE}/api/jobs`)
      .then(r => r.json())
      .then(data => setJobs(Array.isArray(data) ? data : []))
      .catch(() => {})
  }, [])

  useEffect(() => {
    const observer = new IntersectionObserver(
      entries => entries.forEach(e => {
        if (e.isIntersecting) e.target.classList.add('jobs--visible')
      }),
      { threshold: 0.1 }
    )
    const el = sectionRef.current
    if (el) observer.observe(el)
    return () => { if (el) observer.unobserve(el) }
  }, [])

  // Ne pas afficher la section s'il n'y a aucune offre
  if (jobs.length === 0) return null

  return (
    <section className="jobs" id="jobs" ref={sectionRef}>
      <div className="jobs__inner">
        <div className="jobs__header">
          <span className="jobs__eyebrow"><Briefcase size={14} /> {t('jobs.eyebrow')}</span>
          <h2 className="jobs__title">{t('jobs.title')}</h2>
          <p className="jobs__subtitle">{t('jobs.subtitle')}</p>
          <div className="jobs__divider" />
        </div>

        <div className="jobs__list">
          {jobs.map(job => <JobCard key={job.id} job={job} />)}
        </div>
      </div>
    </section>
  )
}
