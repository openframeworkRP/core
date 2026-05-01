// ============================================================
// ControlPanel — Phase 1 du Control Center
// ============================================================
// Affiche l'etat des containers + permet start/stop/restart.
// Restreint aux owners (cf. backend /api/control/*).
// ============================================================

import { useEffect, useState } from 'react'
import './ControlPanel.css'

const REFRESH_MS = 5000

function StatusBadge({ state, running, health }) {
  let cls = 'badge-unknown'
  let label = state || 'unknown'

  if (state === 'running' && running) {
    cls = 'badge-running'
    label = health === 'healthy' ? 'healthy' : 'running'
    if (health === 'unhealthy') { cls = 'badge-warning'; label = 'unhealthy' }
  } else if (state === 'restarting') {
    cls = 'badge-warning'; label = 'restarting'
  } else if (state === 'exited') {
    cls = 'badge-stopped'; label = 'stopped'
  } else if (state === 'not-found') {
    cls = 'badge-error'; label = 'not found'
  } else if (state === 'error') {
    cls = 'badge-error'; label = 'error'
  }

  return <span className={`ctrl-badge ${cls}`}>{label}</span>
}

function formatUptime(startedAt, running) {
  if (!running || !startedAt) return '—'
  const ms = Date.now() - new Date(startedAt).getTime()
  if (ms < 0) return '—'
  const sec = Math.floor(ms / 1000)
  if (sec < 60)        return `${sec}s`
  const min = Math.floor(sec / 60)
  if (min < 60)        return `${min}m ${sec % 60}s`
  const hrs = Math.floor(min / 60)
  if (hrs < 24)        return `${hrs}h ${min % 60}m`
  const days = Math.floor(hrs / 24)
  return `${days}j ${hrs % 24}h`
}

export default function ControlPanel() {
  const [services, setServices] = useState(null)
  const [error, setError]       = useState(null)
  const [busy, setBusy]         = useState({}) // { 'core.api': 'restarting' }
  const [logsFor, setLogsFor]   = useState(null)
  const [logsContent, setLogsContent] = useState('')
  const [logsLoading, setLogsLoading] = useState(false)

  async function fetchStatus() {
    try {
      const r = await fetch('/api/control/status', { credentials: 'include' })
      if (r.status === 401 || r.status === 403) {
        setError('Acces refuse — il faut etre owner.')
        return
      }
      const data = await r.json()
      setServices(data.services)
      setError(null)
    } catch (e) {
      setError(e.message)
    }
  }

  useEffect(() => {
    fetchStatus()
    const t = setInterval(fetchStatus, REFRESH_MS)
    return () => clearInterval(t)
  }, [])

  async function action(verb, serviceId) {
    setBusy(b => ({ ...b, [serviceId]: verb }))
    try {
      const r = await fetch(`/api/control/${verb}/${serviceId}`, {
        method: 'POST',
        credentials: 'include',
      })
      const data = await r.json().catch(() => ({}))
      if (!r.ok) {
        setError(`${verb} ${serviceId} : ${data.error || r.status}`)
      } else if (data.scheduled) {
        // Suicide pattern (website.api) — on previent
        setError(null)
        alert(data.hint || 'Restart programme. Refresh la page dans 10s.')
      }
    } catch (e) {
      setError(`${verb} ${serviceId} : ${e.message}`)
    } finally {
      setTimeout(() => {
        setBusy(b => { const c = { ...b }; delete c[serviceId]; return c })
        fetchStatus()
      }, 1500)
    }
  }

  async function viewLogs(serviceId) {
    setLogsFor(serviceId)
    setLogsLoading(true)
    setLogsContent('')
    try {
      const r = await fetch(`/api/control/logs/${serviceId}?tail=200`, { credentials: 'include' })
      const text = await r.text()
      setLogsContent(text)
    } catch (e) {
      setLogsContent(`Erreur : ${e.message}`)
    } finally {
      setLogsLoading(false)
    }
  }

  return (
    <div className="ctrl-page">
      <header className="ctrl-header">
        <h1>Control Center</h1>
        <p className="ctrl-subtitle">
          État des services, restart, logs. Refresh toutes les {REFRESH_MS / 1000}s.
        </p>
      </header>

      {error && <div className="ctrl-error">⚠ {error}</div>}

      {!services && !error && <p>Chargement…</p>}

      {services && (
        <table className="ctrl-table">
          <thead>
            <tr>
              <th>Service</th>
              <th>État</th>
              <th>Uptime</th>
              <th>Restarts</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {services.map(svc => {
              const isBusy = !!busy[svc.id]
              return (
                <tr key={svc.id}>
                  <td>
                    <div className="ctrl-svc-name">
                      {svc.label}
                      {svc.critical && <span className="ctrl-tag critical">critique</span>}
                      {svc.self && <span className="ctrl-tag self">moi</span>}
                    </div>
                    <div className="ctrl-svc-meta">{svc.container}</div>
                  </td>
                  <td>
                    <StatusBadge state={svc.state} running={svc.running} health={svc.health} />
                  </td>
                  <td>{formatUptime(svc.startedAt, svc.running)}</td>
                  <td>{svc.restartCount ?? 0}</td>
                  <td className="ctrl-actions">
                    {!svc.running && (
                      <button
                        className="ctrl-btn ctrl-btn-start"
                        disabled={isBusy}
                        onClick={() => action('start', svc.id)}
                      >
                        {busy[svc.id] === 'start' ? '...' : 'Start'}
                      </button>
                    )}
                    {svc.running && (
                      <button
                        className="ctrl-btn ctrl-btn-restart"
                        disabled={isBusy}
                        onClick={() => action('restart', svc.id)}
                      >
                        {busy[svc.id] === 'restart' ? '...' : 'Restart'}
                      </button>
                    )}
                    {svc.running && !svc.self && (
                      <button
                        className="ctrl-btn ctrl-btn-stop"
                        disabled={isBusy}
                        onClick={() => {
                          if (svc.critical && !confirm(`Arreter ${svc.label} ? Le jeu va se couper.`)) return
                          action('stop', svc.id)
                        }}
                      >
                        Stop
                      </button>
                    )}
                    <button
                      className="ctrl-btn ctrl-btn-logs"
                      onClick={() => viewLogs(svc.id)}
                    >
                      Logs
                    </button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      )}

      {logsFor && (
        <div className="ctrl-logs-overlay" onClick={() => setLogsFor(null)}>
          <div className="ctrl-logs-modal" onClick={e => e.stopPropagation()}>
            <header>
              <h2>Logs — {logsFor}</h2>
              <button className="ctrl-btn" onClick={() => viewLogs(logsFor)}>Refresh</button>
              <button className="ctrl-btn ctrl-btn-stop" onClick={() => setLogsFor(null)}>Fermer</button>
            </header>
            <pre>{logsLoading ? 'Chargement…' : (logsContent || '(vide)')}</pre>
          </div>
        </div>
      )}
    </div>
  )
}
