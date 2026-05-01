// ============================================================
// /api/control/* — Control Center : status, restart, logs des containers
// ============================================================
// Usage : panel admin web pour gerer l'infrastructure (API .NET, DB,
// website lui-meme). Restreint aux owners.
// ============================================================

import { Router } from 'express'
import { requireAuth, requireRole } from '../auth.js'
import {
  inspectContainer,
  restartContainer,
  startContainer,
  stopContainer,
  containerLogs,
  composeRecreate,
} from '../docker.js'

const router = Router()

// ── Liste des services exposes au control center ────────────────────────
// Le champ 'self' identifie le container qui execute ce code (website.api)
// — il a besoin d'un traitement special pour le restart (suicide pattern).
const SERVICES = [
  { id: 'core.api',         container: 'core-api',              label: 'API du jeu (.NET 9)',     critical: true },
  { id: 'sqlserver',        container: 'core-sqlserver',        label: 'SQL Server (DB du jeu)', critical: true },
  { id: 'adminer',          container: 'core-adminer',          label: 'Adminer (UI DB)' },
  { id: 'website.api',      container: 'core-website-api',      label: 'API du website (Node)',  self: true },
  { id: 'website.frontend', container: 'core-website-frontend', label: 'Frontend (Vite)' },
  { id: 'website.scraper',  container: 'core-website-scraper',  label: 'Scraper FAB (Python)' },
]

function findService(id) {
  return SERVICES.find(s => s.id === id)
}

// Tout le router est admin/owner only
router.use(requireAuth, requireRole('owner'))

// ── GET /api/control/status ─────────────────────────────────────────────
router.get('/status', async (_req, res) => {
  const services = await Promise.all(SERVICES.map(async svc => {
    try {
      const info = await inspectContainer(svc.container)
      if (!info) {
        return { ...svc, state: 'not-found', running: false }
      }
      return { ...svc, ...info }
    } catch (e) {
      return { ...svc, state: 'error', running: false, error: e.message }
    }
  }))
  res.json({ services })
})

// ── POST /api/control/restart/:service ──────────────────────────────────
router.post('/restart/:service', async (req, res) => {
  const svc = findService(req.params.service)
  if (!svc) return res.status(404).json({ error: 'unknown-service' })

  // Cas special : website.api (nous-memes) doit repondre AVANT de mourir
  if (svc.self) {
    res.json({
      ok: true,
      scheduled: true,
      hint: 'Restart programme dans 1s — la connexion sera coupee, refresh la page apres ~10s.',
    })
    setTimeout(() => {
      restartContainer(svc.container, 5).catch(() => { /* on est en train de mourir */ })
    }, 1000)
    return
  }

  try {
    const r = await restartContainer(svc.container, 10)
    res.json({ ok: r.status === 204, status: r.status })
  } catch (e) {
    res.status(500).json({ error: 'restart-failed', detail: e.message })
  }
})

// ── POST /api/control/start/:service ────────────────────────────────────
router.post('/start/:service', async (req, res) => {
  const svc = findService(req.params.service)
  if (!svc) return res.status(404).json({ error: 'unknown-service' })
  try {
    const r = await startContainer(svc.container)
    // 204 = started, 304 = already running
    res.json({ ok: r.status === 204 || r.status === 304, status: r.status })
  } catch (e) {
    res.status(500).json({ error: 'start-failed', detail: e.message })
  }
})

// ── POST /api/control/stop/:service ─────────────────────────────────────
router.post('/stop/:service', async (req, res) => {
  const svc = findService(req.params.service)
  if (!svc) return res.status(404).json({ error: 'unknown-service' })
  if (svc.self) {
    return res.status(400).json({
      error: 'cannot-stop-self',
      hint: 'Stopper website.api couperait ce panel. Utilise restart a la place.',
    })
  }
  try {
    const r = await stopContainer(svc.container, 10)
    res.json({ ok: r.status === 204 || r.status === 304, status: r.status })
  } catch (e) {
    res.status(500).json({ error: 'stop-failed', detail: e.message })
  }
})

// ── POST /api/control/recreate/:service ─────────────────────────────────
// Plus puissant que restart : recree le container depuis docker-compose.yml
// pour qu'il prenne les nouvelles env vars du .env. Utile apres reconfig.
router.post('/recreate/:service', async (req, res) => {
  const svc = findService(req.params.service)
  if (!svc) return res.status(404).json({ error: 'unknown-service' })

  if (svc.self) {
    res.json({
      ok: true,
      scheduled: true,
      hint: 'Recreate programme dans 1s — la connexion sera coupee, refresh la page apres ~15s.',
    })
    setTimeout(() => {
      composeRecreate([svc.id]).catch(() => { /* on est en train de mourir */ })
    }, 1000)
    return
  }

  try {
    const r = await composeRecreate([svc.id])
    if (r.code === 0) {
      res.json({ ok: true, code: r.code })
    } else {
      res.status(500).json({
        error: 'recreate-failed',
        code: r.code,
        stderr: r.stderr.split('\n').slice(-10).join('\n'),
      })
    }
  } catch (e) {
    res.status(500).json({ error: 'recreate-failed', detail: e.message })
  }
})

// ── GET /api/control/logs/:service?tail=100 ─────────────────────────────
router.get('/logs/:service', async (req, res) => {
  const svc = findService(req.params.service)
  if (!svc) return res.status(404).json({ error: 'unknown-service' })
  const tail = Math.min(parseInt(req.query.tail) || 100, 500)
  try {
    const r = await containerLogs(svc.container, tail)
    res.type('text/plain').send(r.body)
  } catch (e) {
    res.status(500).json({ error: 'logs-failed', detail: e.message })
  }
})

export default router
