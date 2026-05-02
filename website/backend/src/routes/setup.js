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
import crypto from 'crypto'
import { dockerRequest, composeRecreate } from '../docker.js'
import db from '../db.js'

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
    // Branding optionnel — passe des valeurs pour pre-configurer le theme
    branding = null,
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

  envContent = setEnvVar(envContent, 'POSTGRES_DB',       dbName)
  envContent = setEnvVar(envContent, 'POSTGRES_USER',     'postgres')
  envContent = setEnvVar(envContent, 'POSTGRES_PASSWORD', 'OpenFwBootstrap_Pa55!')
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

  // ── Branding initial (optionnel) ───────────────────────────────────
  // Le wizard est la seule voie publique d'ecriture sur la table
  // branding (apres ca, c'est PUT /api/branding restreint aux owners).
  if (branding && typeof branding === 'object') {
    const ALLOWED = ['site_name', 'site_short_name', 'description', 'primary_color', 'accent_color', 'logo_url', 'favicon_url', 'default_author']
    const upsert = db.prepare(`
      INSERT INTO branding (key, value) VALUES (?, ?)
      ON CONFLICT(key) DO UPDATE SET value = excluded.value
    `)
    for (const [key, rawValue] of Object.entries(branding)) {
      if (!ALLOWED.includes(key)) continue
      const value = String(rawValue ?? '').trim().slice(0, 1024)
      upsert.run(key, value)
    }
  }

  // ── Force-recreate de core.api pour qu'il prenne les nouvelles env vars ──
  // Un simple 'docker restart' garde les vars du create initial, donc on
  // utilise 'docker compose up -d --force-recreate' qui recree le container
  // depuis le compose.yml (qui re-evalue les ${VAR} du nouveau .env).
  // PostgreSQL et Redis : pas recrees car leurs donnees sont dans des volumes persistants.
  const restartResults = []
  try {
    const r = await composeRecreate(['core.api'])
    restartResults.push({
      name: 'core.api',
      ok: r.code === 0,
      code: r.code,
      stderr: r.stderr.split('\n').slice(-5).join('\n'),
    })
  } catch (e) {
    restartResults.push({ name: 'core.api', ok: false, error: e.message })
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

  // ── Reponse au browser AVANT de se suicider ─────────────────────────
  // On va aussi recreate website.api / website.frontend / website.scraper
  // pour qu'ils prennent STEAM_API_KEY, ALLOWED_STEAM_IDS, SESSION_SECRET
  // etc. du nouveau .env. Mais website.api c'est NOUS — recreate kills la
  // requete en cours. Donc on repond d'abord, puis on schedule.
  res.json({
    ok: true,
    apiReady: ready,
    restarts: restartResults,
    websiteRestartScheduled: true,
    nextStep: ready
      ? '/admin'
      : 'L\'API du jeu n\'a pas repondu apres 60s. Verifie les logs de core.api dans le panel control.',
    postSetupHint: 'Le wizard va recreer les services website pour appliquer les secrets. La page va se reconnecter automatiquement dans ~15s.',
  })

  // Dans 2s, on demande a docker compose de recreate les services website.
  // Cette commande va tuer notre propre container (suicide), donc le code
  // apres setTimeout n'a pas le temps de s'executer. Mais c'est OK : la
  // reponse a deja ete envoyee.
  setTimeout(() => {
    composeRecreate(['website.api', 'website.frontend', 'website.scraper'])
      .catch(() => { /* on meurt, normal */ })
  }, 2000)
})

export default router
