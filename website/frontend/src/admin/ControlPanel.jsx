// ============================================================
// ControlPanel — Phase 1 du Control Center
// ============================================================
// Affiche l'etat des containers + permet start/stop/restart.
// Restreint aux owners (cf. backend /api/control/*).
// ============================================================

import { useEffect, useRef, useState } from 'react'
import { getSocket } from './useAdminSocket.js'
import './ControlPanel.css'

const REFRESH_MS = 5000
const MAX_CONSOLE_LINES = 500

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

  // Console live
  const [consoleFor, setConsoleFor]     = useState(null)
  const [consoleLines, setConsoleLines] = useState([])
  const [consoleLive, setConsoleLive]   = useState(false)
  const consoleRef = useRef(null)

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

  // ── Console streaming via Socket.io ────────────────────────────────────
  useEffect(() => {
    if (!consoleFor) return

    const s = getSocket()
    setConsoleLive(false)
    s.emit('console:subscribe', consoleFor)

    const handleLine = ({ serviceId, line }) => {
      if (serviceId !== consoleFor) return
      setConsoleLive(true)
      setConsoleLines(prev => {
        const next = [...prev, line]
        return next.length > MAX_CONSOLE_LINES ? next.slice(-MAX_CONSOLE_LINES) : next
      })
    }

    const handleEnd = ({ serviceId }) => {
      if (serviceId !== consoleFor) return
      setConsoleLive(false)
      setConsoleLines(prev => [...prev, '─── flux terminé ───'])
    }

    const handleError = ({ serviceId, message }) => {
      if (serviceId !== consoleFor) return
      setConsoleLive(false)
      setConsoleLines(prev => [...prev, `[ERREUR] ${message}`])
    }

    s.on('console:line',  handleLine)
    s.on('console:end',   handleEnd)
    s.on('console:error', handleError)

    return () => {
      s.emit('console:unsubscribe', consoleFor)
      s.off('console:line',  handleLine)
      s.off('console:end',   handleEnd)
      s.off('console:error', handleError)
    }
  }, [consoleFor])

  // Auto-scroll console vers le bas quand de nouvelles lignes arrivent
  useEffect(() => {
    if (consoleRef.current) {
      consoleRef.current.scrollTop = consoleRef.current.scrollHeight
    }
  }, [consoleLines])

  async function action(verb, serviceId) {
    setBusy(b => ({ ...b, [serviceId]: verb }))
    try {
      const r = await fetch(`/api/control/${verb}/${serviceId}`, {
        method: 'POST',
        credentials: 'include',
      })
      const data = await r.json().catch(() => ({}))
      if (!r.ok) {
        const detail = data.stderr || data.detail || ''
        setError(`${verb} ${serviceId} : ${data.error || r.status}${detail ? '\n\n' + detail : ''}`)
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

  function openConsole(serviceId) {
    setConsoleLines([])
    setConsoleFor(serviceId)
  }

  function closeConsole() {
    setConsoleFor(null)
    setConsoleLines([])
    setConsoleLive(false)
  }

  const consoleLabel = services?.find(s => s.id === consoleFor)?.label ?? consoleFor

  return (
    <div className="ctrl-page">
      <header className="ctrl-header">
        <h1>Control Center</h1>
        <p className="ctrl-subtitle">
          État des services, restart, logs. Refresh toutes les {REFRESH_MS / 1000}s.
        </p>
      </header>

      {error && <pre className="ctrl-error">⚠ {error}</pre>}

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
                    <div className="ctrl-svc-name">{svc.label}</div>
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
                        title="Restart le container (garde les env vars du create)"
                      >
                        {busy[svc.id] === 'restart' ? '...' : 'Restart'}
                      </button>
                    )}
                    <button
                      className="ctrl-btn ctrl-btn-recreate"
                      disabled={isBusy}
                      onClick={() => {
                        if (!confirm(`Recreer ${svc.label} ?\n\nLe container sera detruit et recree depuis docker-compose.yml. A faire apres avoir modifie .env. Coupure courte (~10s).`)) return
                        action('recreate', svc.id)
                      }}
                      title="Recree le container depuis docker-compose.yml (recharge les env vars du .env)"
                    >
                      {busy[svc.id] === 'recreate' ? '...' : 'Recreate'}
                    </button>
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
                    <button
                      className="ctrl-btn ctrl-btn-console"
                      onClick={() => openConsole(svc.id)}
                    >
                      Console
                    </button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      )}

      {/* ── Modal logs statiques ────────────────────────────────────────── */}
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

      {/* ── Modal console live ──────────────────────────────────────────── */}
      {consoleFor && (
        <div className="ctrl-logs-overlay" onClick={closeConsole}>
          <div className="ctrl-logs-modal ctrl-console-modal" onClick={e => e.stopPropagation()}>
            <header>
              <h2>
                {consoleLive && <span className="ctrl-console-live" title="Stream actif" />}
                Console — {consoleLabel}
              </h2>
              <span className="ctrl-console-count">{consoleLines.length} lignes</span>
              <button
                className="ctrl-btn"
                onClick={() => setConsoleLines([])}
                title="Effacer l'affichage (le stream continue)"
              >
                Clear
              </button>
              <button className="ctrl-btn ctrl-btn-stop" onClick={closeConsole}>Fermer</button>
            </header>
            <pre ref={consoleRef}>
              {consoleLines.length === 0
                ? '(en attente de données…)'
                : consoleLines.join('\n')}
            </pre>
          </div>
        </div>
      )}
    </div>
  )
}
