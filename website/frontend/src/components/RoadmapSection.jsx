// ============================================================
// RoadmapSection — milestones publics sur la home
// ============================================================
// Lit /api/hub/roadmap (deja existant) qui retourne uniquement les
// milestones marques 'public: true' depuis le panel Hub > Roadmap.
// Si rien n'est publie, la section ne s'affiche pas du tout.
// ============================================================

import { useEffect, useState } from 'react'
import { Calendar, CheckCircle2, Circle } from 'lucide-react'
import './RoadmapSection.css'

function formatDate(iso) {
  if (!iso) return ''
  try {
    const d = new Date(iso)
    return d.toLocaleDateString('fr-FR', { year: 'numeric', month: 'long' })
  } catch { return iso }
}

export default function RoadmapSection() {
  const [milestones, setMilestones] = useState([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetch('/api/hub/roadmap')
      .then(r => r.json())
      .then(data => {
        setMilestones(Array.isArray(data?.milestones) ? data.milestones : [])
        setLoading(false)
      })
      .catch(() => setLoading(false))
  }, [])

  if (loading) return null
  if (milestones.length === 0) return null

  return (
    <div className="roadmap">
      <div className="roadmap__inner">
        <header className="roadmap__header">
          <h2>Roadmap</h2>
          <p>Les prochaines etapes du framework et leur avancement.</p>
        </header>

        <div className="roadmap__milestones">
          {milestones.map(m => {
            const total = m.tasks?.length || 0
            const done  = m.tasks?.filter(t => t.status === 'done').length || 0
            const pct   = total > 0 ? Math.round((done / total) * 100) : 0
            const color = m.color || 'var(--brand-primary, #3cadd9)'

            return (
              <div key={m.id} className="roadmap__milestone" style={{ '--milestone-color': color }}>
                <div className="roadmap__milestone-head">
                  <h3>{m.name}</h3>
                  {m.date && (
                    <span className="roadmap__milestone-date">
                      <Calendar size={13} /> {formatDate(m.date)}
                    </span>
                  )}
                </div>

                {m.description && (
                  <p className="roadmap__milestone-desc">{m.description}</p>
                )}

                {total > 0 && (
                  <div className="roadmap__milestone-progress">
                    <div className="roadmap__milestone-bar">
                      <div className="roadmap__milestone-bar-fill" style={{ width: `${pct}%` }} />
                    </div>
                    <span>{done}/{total}</span>
                  </div>
                )}

                {m.tasks && m.tasks.length > 0 && (
                  <ul className="roadmap__milestone-tasks">
                    {m.tasks.map(t => (
                      <li key={t.id} className={`roadmap__task roadmap__task--${t.status}`}>
                        {t.status === 'done'
                          ? <CheckCircle2 size={14} />
                          : <Circle size={14} />}
                        <span>{t.text}</span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            )
          })}
        </div>
      </div>
    </div>
  )
}
