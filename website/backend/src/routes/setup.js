// ============================================================
// /api/setup/* — wizard de configuration au premier lancement
// ============================================================
// GET  /api/setup/status   : public, indique si le wizard doit etre
//                            affiche (setup-complete.flag absent ?)
// POST /api/setup/apply    : public (1er run, pas encore de user),
//                            ecrit le .env, restart core.api via Docker
//                            socket, polle /health/ready, pose le flag.
// ============================================================

import { Router } from 'express'
import fs from 'fs/promises'
import http from 'http'
import crypto from 'crypto'

const router = Router()

const REPO_ENV_PATH         = process.env.REPO_ENV_PATH         || '/app/host-repo/.env'
const REPO_ENV_EXAMPLE_PATH = process.env.REPO_ENV_EXAMPLE_PATH || '/app/host-repo/.env.example'
const SETUP_COMPLETE_FLAG   = process.env.SETUP_COMPLETE_FLAG   || '/app/host-repo/data/config/setup-complete.flag'

// En reseau bridge : on cible le service core-api via son nom DNS docker.
// En reseau host (Linux natif) : on cible localhost:8443.
// Override via env var CORE_API_INTERNAL_URL si besoin.
const CORE_API_BASE = process.env.CORE_API_INTERNAL_URL || 'http://core-api:8443'

// ── Helpers ──────────────────────────────────────────────────────────────

async function fileExists(p) {
  try { await fs.access(p); return true } catch { return false }
}

/**
 * Requete au daemon Docker via le socket Unix (monte par docker-compose).
 * Pas de dependance externe : on utilise le module http natif de Node.
 */
function dockerRequest(path, method = 'GET', body = null) {
  return new Promise((resolve, reject) => {
    const options = {
      socketPath: '/var/run/docker.sock',
      path,
      method,
      headers: { 'Content-Type': 'application/json' },
    }
    const req = http.request(options, (res) => {
      let data = ''
      res.on('data', (chunk) => (data += chunk))
      res.on('end', () => resolve({ status: res.statusCode, body: data }))
    })
    req.on('error', reject)
    if (body) req.write(JSON.stringify(body))
    req.end()
  })
}

async function checkApiHealth() {
  try {
    const r = await fetch(`${CORE_API_BASE}/health`, { signal: AbortSignal.timeout(2000) })
    if (!r.ok) return { reachable: true, configured: false, ready: false }
    const data = await r.json()
    return { reachable: true, configured: !!data.configured, ready: false }
  } catch {
    return { reachable: false, configured: false, ready: false }
  }
}

async function checkApiReady() {
  try {
    const r = await fetch(`${CORE_API_BASE}/health/ready`, { signal: AbortSignal.timeout(2000) })
    return r.ok
  } catch {
    return false
  }
}

/**
 * Met a jour ou ajoute KEY=VALUE dans le contenu d'un .env.
 * Echappe les caracteres problematiques pour eviter d'injecter du shell.
 */
function setEnvVar(content, key, value) {
  const safeValue = String(value ?? '').replace(/\r?\n/g, ' ')
  const line = `${key}=${safeValue}`
  const re = new RegExp(`^${key}=.*$`, 'm')
  if (re.test(content)) return content.replace(re, line)
  return content + (content.endsWith('\n') ? '' : '\n') + line + '\n'
}

// ── GET /api/setup/status ────────────────────────────────────────────────
router.get('/status', async (_req, res) => {
  try {
    const [setupComplete, envExists, apiHealth] = await Promise.all([
      fileExists(SETUP_COMPLETE_FLAG),
      fileExists(REPO_ENV_PATH),
      checkApiHealth(),
    ])
    const ready = await checkApiReady()
    res.json({
      needsSetup: !setupComplete,
      setupComplete,
      envExists,
      apiHealth: { ...apiHealth, ready },
    })
  } catch (e) {
    res.status(500).json({ error: 'status-failed', detail: e.message })
  }
})

// ── POST /api/setup/apply ────────────────────────────────────────────────
router.post('/apply', async (req, res) => {
  // Empeche de relancer si deja fait (sauf force=true)
  if (await fileExists(SETUP_COMPLETE_FLAG) && !req.body?.force) {
    return res.status(403).json({
      error: 'setup-already-complete',
      hint: 'Pour reconfigurer, supprime data/config/setup-complete.flag puis relance.',
    })
  }

  const {
    jwtKey,
    serverSecret,
    sessionSecret,
    steamApiKey = '',
    allowedSteamIds = '',
    apiPort = 8443,
    dbName = 'OpenFrameworkDb',
  } = req.body || {}

  // ── Validation des inputs ──────────────────────────────────────────
  if (!jwtKey || jwtKey.length < 32) {
    return res.status(400).json({ error: 'jwt-key-too-short', minLength: 32 })
  }
  if (!serverSecret || serverSecret.length < 16) {
    return res.status(400).json({ error: 'server-secret-too-short', minLength: 16 })
  }

  // ── Construction du .env depuis .env.example comme template ────────
  let envContent = ''
  try {
    envContent = await fs.readFile(REPO_ENV_EXAMPLE_PATH, 'utf-8')
  } catch {
    envContent = '# Genere par le wizard OpenFramework Core\n'
  }

  // Note : on ne change PAS MSSQL_SA_PASSWORD ici. Au 1er boot SQL
  // Server ecrit le mdp bootstrap dans son volume persistent — le
  // changer cote .env ne suffit pas, il faudrait ALTER LOGIN sa.
  // Pour l'instant on garde la valeur bootstrap (le user peut la
  // changer manuellement via T-SQL plus tard, cf. docs/SETUP.md).
  envContent = setEnvVar(envContent, 'MSSQL_SA_PASSWORD',  'OpenFwBootstrap_Pa55!')
  envContent = setEnvVar(envContent, 'DB_NAME',            dbName)
  envContent = setEnvVar(envContent, 'API_PORT',           String(apiPort))
  envContent = setEnvVar(envContent, 'JWT_KEY',            jwtKey)
  envContent = setEnvVar(envContent, 'SERVER_SECRET',      serverSecret)
  envContent = setEnvVar(envContent, 'GAME_SERVER_SECRET', serverSecret)
  envContent = setEnvVar(envContent, 'SESSION_SECRET',     sessionSecret || crypto.randomBytes(32).toString('hex'))
  envContent = setEnvVar(envContent, 'STEAM_API_KEY',      steamApiKey)
  envContent = setEnvVar(envContent, 'ALLOWED_STEAM_IDS',  allowedSteamIds)

  // ── Ecriture du .env (mode 0600 : lecture proprietaire seulement) ──
  try {
    await fs.writeFile(REPO_ENV_PATH, envContent, { encoding: 'utf-8', mode: 0o600 })
  } catch (e) {
    return res.status(500).json({ error: 'env-write-failed', detail: e.message })
  }

  // ── Restart core-api pour qu'il recharge la config ──────────────────
  // SQL Server n'a pas besoin d'etre restart : son mdp SA reste celui
  // du 1er boot, qu'on a remis dans le .env.
  const restartResults = []
  try {
    const r = await dockerRequest('/containers/core-api/restart?t=10', 'POST')
    restartResults.push({ name: 'core-api', status: r.status, ok: r.status === 204 })
  } catch (e) {
    restartResults.push({ name: 'core-api', ok: false, error: e.message })
  }

  // ── Polling /health/ready (max 60s) ─────────────────────────────────
  const start = Date.now()
  let ready = false
  while (Date.now() - start < 60_000) {
    if (await checkApiReady()) { ready = true; break }
    await new Promise(r => setTimeout(r, 2000))
  }

  // ── Pose du flag setup-complete ─────────────────────────────────────
  try {
    const meta = JSON.stringify({
      completedAt: new Date().toISOString(),
      apiReady: ready,
      restarts: restartResults,
    }, null, 2)
    await fs.writeFile(SETUP_COMPLETE_FLAG, meta, 'utf-8')
  } catch {
    // pas critique : la config est ecrite, juste le flag qui rate
  }

  res.json({
    ok: true,
    apiReady: ready,
    restarts: restartResults,
    nextStep: ready
      ? '/admin'
      : 'L\'API n\'a pas repondu apres 60s. Verifie docker logs core-api.',
  })
})

export default router
