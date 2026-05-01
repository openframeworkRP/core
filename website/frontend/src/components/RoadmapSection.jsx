// ============================================================
// RoadmapSection — items de roadmap publique sur la home
// ============================================================
// Lit /api/roadmap (only is_public=1) et affiche les items en
// liste minimaliste regroupes par status.
// ============================================================

import { useEffect, useState } from 'react'
import { Circle, CircleDot, CheckCircle2, Rocket } from 'lucide-react'
import './RoadmapSection.css'

const STATUS_META = {
  planned:     { label: 'Prevu',     icon: <Circle      size={16} /> },
  in_progress: { label: 'En cours',  icon: <CircleDot   size={16} /> },
  done:        { label: 'Fini',      icon: <CheckCircle2 size={16} /> },
  shipped:     { label: 'Live',      icon: <Rocket      size={16} /> },
}

const STATUS_ORDER = ['in_progress', 'planned', 'done', 'shipped']

export default function RoadmapSection() {
  const [items, setItems] = useState([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetch('/api/roadmap')
      .then(r => r.json())
      .then(data => {
        setItems(Array.isArray(data) ? data : [])
        setLoading(false)
      })
      .catch(() => setLoading(false))
  }, [])

  if (loading) return null
  if (items.length === 0) return null

  // Group par status, dans l'ordre defini
  const grouped = STATUS_ORDER
    .map(status => ({
      status,
      items: items.filter(i => i.status === status),
    }))
    .filter(g => g.items.length > 0)

  return (
    <div className="roadmap">
      <div className="roadmap__inner">
        <header className="roadmap__header">
          <h2>Roadmap</h2>
          <p>Ce qui est en cours, ce qui arrive, ce qui est deja en place.</p>
        </header>

        <div className="roadmap__groups">
          {grouped.map(group => (
            <div key={group.status} className={`roadmap__group roadmap__group--${group.status}`}>
              <div className="roadmap__group-header">
                {STATUS_META[group.status].icon}
                <span>{STATUS_META[group.status].label}</span>
                <span className="roadmap__group-count">{group.items.length}</span>
              </div>
              <ul className="roadmap__items">
                {group.items.map(item => (
                  <li key={item.id} className="roadmap__item">
                    <h3>{item.title}</h3>
                    {item.description && <p>{item.description}</p>}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
