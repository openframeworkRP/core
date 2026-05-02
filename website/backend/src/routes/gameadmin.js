import { Router } from 'express'
import { requireRole } from '../auth.js'
import db from '../db.js'

const router = Router()

// ── Journal des actions admin (local SQLite) ──────────────────────────────
const insertLog = db.prepare(`
  INSERT INTO gameadmin_logs (action, target_steam_id, admin_steam_id, reason, extra)
  VALUES (@action, @targetSteamId, @adminSteamId, @reason, @extra)
`)

function logAction(req, action, targetSteamId, reason = '', extra = null) {
  try {
    insertLog.run({
      action,
      targetSteamId: targetSteamId || null,
      adminSteamId: req.user?.steamId || null,
      reason: reason || null,
      extra: extra ? JSON.stringify(extra) : null,
    })
  } catch (e) {
    // On garde le log local fonctionnel même si autre chose échoue.
    console.error('[gameadmin] insert local log failed:', e.message)
  }

  // Centralise aussi vers l'API jeu pour avoir une timeline unifiée
  // (web + in-game) côté panel. Best-effort, ne bloque jamais l'action admin.
  gameFetch('/api/events/admin-action', {
    method: 'POST',
    body: {
      AdminSteamId:  req.user?.steamId || 'web-admin',
      Action:        action,
      TargetSteamId: targetSteamId || null,
      Reason:        reason || null,
      PayloadJson:   extra ? JSON.stringify(extra) : null,
      Source:        'web',
    },
  }).catch(e => console.warn('[gameadmin] forward to API admin-action failed:', e.message))
}

// ── Configuration ─────────────────────────────────────────────────────────
// Default sur le DNS inter-container (compose) au lieu de localhost — depuis
// le container website-api, 'localhost:8443' pointe sur LUI-MEME, pas sur
// l'hote. core-api est le hostname Docker du container core.api.
const GAME_API_URL     = process.env.GAME_API_URL     || 'http://core-api:8443'
const GAME_SERVER_SECRET = process.env.GAME_SERVER_SECRET || ''
const STEAM_API_KEY    = process.env.STEAM_API_KEY || ''

// ── Cache des profils Steam (name + avatar) pour éviter de taper l'API Steam à chaque appel
const steamProfileCache = new Map() // steamId -> { name, avatar, expiresAt }
const STEAM_CACHE_TTL = 60 * 60 * 1000 // 1h

async function enrichWithSteamProfiles(steamIds) {
  if (!STEAM_API_KEY || steamIds.length === 0) return {}
  const now = Date.now()
  const out = {}
  const toFetch = []

  for (const sid of steamIds) {
    const cached = steamProfileCache.get(sid)
    if (cached && cached.expiresAt > now) {
      out[sid] = { name: cached.name, avatar: cached.avatar }
    } else {
      toFetch.push(sid)
    }
  }

  // Steam accepte jusqu'à 100 steamIds par appel
  for (let i = 0; i < toFetch.length; i += 100) {
    const batch = toFetch.slice(i, i + 100).join(',')
    try {
      const url = `https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key=${STEAM_API_KEY}&steamids=${batch}`
      const res = await fetchWithTimeout(url, {}, 5000)
      if (!res.ok) continue
      const data = await res.json()
      for (const p of (data?.response?.players || [])) {
        const entry = { name: p.personaname, avatar: p.avatarmedium || p.avatar }
        steamProfileCache.set(p.steamid, { ...entry, expiresAt: now + STEAM_CACHE_TTL })
        out[p.steamid] = entry
      }
    } catch { /* ignore, graceful fallback */ }
  }
  return out
}

// ── Cache du JWT GameServer (évite un login par requête) ──────────────────
let cachedToken     = null
let cachedTokenExp  = 0   // timestamp ms

async function fetchWithTimeout(url, opts = {}, timeoutMs = 8000) {
  const ctrl = new AbortController()
  const t = setTimeout(() => ctrl.abort(), timeoutMs)
  try {
    return await fetch(url, { ...opts, signal: ctrl.signal })
  } finally {
    clearTimeout(t)
  }
}

async function getServerToken() {
  const now = Date.now()
  // Token valable 30 jours côté API — on rafraîchit quand il reste < 1 jour
  if (cachedToken && (cachedTokenExp - now) > 24 * 60 * 60 * 1000) {
    return cachedToken
  }

  let res
  try {
    res = await fetchWithTimeout(`${GAME_API_URL}/api/auth/server-login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ ServerSecret: GAME_SERVER_SECRET }),
    })
  } catch (e) {
    throw new Error(`API de jeu injoignable (${GAME_API_URL}): ${e.message}`)
  }
  if (!res.ok) {
    const txt = await res.text().catch(() => '')
    throw new Error(`Auth serveur API jeu échouée (${res.status}): ${txt}`)
  }
  const data = await res.json()
  cachedToken    = data.access_token
  // Rafraîchit 29j plus tard — l'API émet un token 30j
  cachedTokenExp = now + 29 * 24 * 60 * 60 * 1000
  return cachedToken
}

// ── Helper d'appel vers l'API de jeu avec le token GameServer ─────────────
async function gameFetch(path, { method = 'GET', body = null } = {}) {
  const token = await getServerToken()
  const opts = {
    method,
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
  }
  if (body !== null) opts.body = JSON.stringify(body)

  let res
  try {
    res = await fetchWithTimeout(`${GAME_API_URL}${path}`, opts)
  } catch (e) {
    const err = new Error(`API de jeu injoignable (${GAME_API_URL}): ${e.message}`)
    err.status = 502
    throw err
  }
  const text = await res.text()
  let data = null
  try { data = text ? JSON.parse(text) : null } catch { data = text }

  if (!res.ok) {
    const err = new Error(typeof data === 'string' ? data : (data?.error || res.statusText))
    err.status = res.status
    err.body   = data
    throw err
  }
  return data
}

// ── Sync de la liste des admins vers core-api ────────────────────────────
async function pushAdminListToCoreApi() {
  try {
    const rows = db.prepare('SELECT steam_id FROM gamemode_admins').all()
    await gameFetch('/api/admin/game-admins/sync', {
      method: 'POST',
      body: { steamIds: rows.map(r => r.steam_id) },
    })
  } catch (e) {
    console.warn('[gameadmin] push admin list to core-api failed:', e.message)
  }
}

// ── Endpoint gamemode-only : liste des admins en jeu ─────────────────────
// Pas de session requise — authentifié par X-Server-Secret (même secret que
// le gamemode utilise pour s'authentifier sur core-api). Mis avant le
// middleware requireRole pour ne pas bloquer le poller côté gamemode.
router.get('/game-admins/list', (req, res) => {
  const secret = req.headers['x-server-secret'] || req.query.secret
  if (!GAME_SERVER_SECRET || secret !== GAME_SERVER_SECRET) {
    return res.status(401).json({ error: 'Non autorisé' })
  }
  try {
    const rows = db.prepare('SELECT steam_id FROM gamemode_admins').all()
    res.json({ steamIds: rows.map(r => r.steam_id) })
  } catch (e) {
    res.status(500).json({ error: e.message })
  }
})

// ── Middleware : editor+ du site (editor, admin, owner) ───────────────────
router.use(requireRole('editor'))

// ─────────────────────────────────────────────────────────────────────────
//  ADMINS GAMEMODE — gestion de la liste des SteamIDs admin en jeu
// ─────────────────────────────────────────────────────────────────────────
router.get('/game-admins', (_req, res) => {
  try {
    const rows = db.prepare('SELECT * FROM gamemode_admins ORDER BY added_at DESC').all()
    res.json(rows)
  } catch (e) { res.status(500).json({ error: e.message }) }
})

router.post('/game-admins', requireRole('admin'), async (req, res) => {
  const { steam_id, label } = req.body || {}
  if (!steam_id || !/^\d{17}$/.test(steam_id.trim())) {
    return res.status(400).json({ error: 'SteamID64 invalide (17 chiffres requis)' })
  }
  try {
    db.prepare('INSERT OR REPLACE INTO gamemode_admins (steam_id, label, added_by) VALUES (?, ?, ?)')
      .run(steam_id.trim(), (label || '').trim(), req.user?.steamId || 'web-admin')
    logAction(req, 'gameadmin_add', steam_id.trim(), label || '')
    await pushAdminListToCoreApi()
    res.status(201).json({ ok: true })
  } catch (e) { res.status(500).json({ error: e.message }) }
})

router.delete('/game-admins/:steamId', requireRole('admin'), async (req, res) => {
  const result = db.prepare('DELETE FROM gamemode_admins WHERE steam_id = ?').run(req.params.steamId)
  if (result.changes === 0) return res.status(404).json({ error: 'Admin introuvable' })
  logAction(req, 'gameadmin_remove', req.params.steamId)
  await pushAdminListToCoreApi()
  res.json({ ok: true })
})

// ─────────────────────────────────────────────────────────────────────────
//  DASHBOARD
// ─────────────────────────────────────────────────────────────────────────
router.get('/stats', async (_req, res) => {
  try { res.json(await gameFetch('/api/admin/read/stats')) }
  catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

// ─────────────────────────────────────────────────────────────────────────
//  USERS (joueurs)
// ─────────────────────────────────────────────────────────────────────────
router.get('/users', async (_req, res) => {
  try {
    const users = await gameFetch('/api/admin/read/users')
    const profiles = await enrichWithSteamProfiles(users.map(u => u.steamId))
    res.json(users.map(u => ({
      ...u,
      displayName: profiles[u.steamId]?.name || null,
      avatar:      profiles[u.steamId]?.avatar || null,
    })))
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.get('/users/:steamId', async (req, res) => {
  try {
    const data = await gameFetch(`/api/admin/read/users/${encodeURIComponent(req.params.steamId)}`)
    const profiles = await enrichWithSteamProfiles([req.params.steamId])
    res.json({
      ...data,
      displayName: profiles[req.params.steamId]?.name || null,
      avatar:      profiles[req.params.steamId]?.avatar || null,
    })
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

// ─────────────────────────────────────────────────────────────────────────
//  CHARACTERS
// ─────────────────────────────────────────────────────────────────────────
router.get('/characters', async (_req, res) => {
  try { res.json(await gameFetch('/api/admin/read/characters')) }
  catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.get('/positions', async (_req, res) => {
  try { res.json(await gameFetch('/api/admin/read/positions')) }
  catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.get('/items', async (req, res) => {
  const { page = 1, pageSize = 200 } = req.query
  try { res.json(await gameFetch(`/api/admin/read/items?page=${page}&pageSize=${pageSize}`)) }
  catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

// Cache des mappings character/account pour enrichir les transactions
let txLookupCache = null
let txLookupCacheExp = 0
const TX_LOOKUP_TTL = 60 * 1000

async function getTxLookups() {
  const now = Date.now()
  if (txLookupCache && txLookupCacheExp > now) return txLookupCache

  const charById = new Map()
  const acctById = new Map()
  try {
    const characters = await gameFetch('/api/admin/read/characters')
    for (const c of (characters || [])) {
      charById.set(c.id, c)
    }
    // Fetch des comptes par character (parallèle, best-effort)
    await Promise.all((characters || []).map(async c => {
      try {
        const accounts = await gameFetch(`/api/admin/read/characters/${encodeURIComponent(c.id)}/accounts`)
        for (const a of (accounts || [])) {
          acctById.set(a.id, {
            accountName:   a.accountName,
            accountNumber: a.accountNumber,
            ownerId:       c.id,
            ownerName:     `${c.firstName} ${c.lastName}`.trim(),
          })
        }
      } catch { /* skip ce character */ }
    }))
  } catch { /* charById restera vide */ }

  txLookupCache    = { charById, acctById }
  txLookupCacheExp = now + TX_LOOKUP_TTL
  return txLookupCache
}

function enrichTx(t, lookups) {
  const ini = lookups.charById.get(t.initiatorCharacterId)
  const from = lookups.acctById.get(t.fromAccountId)
  const to   = lookups.acctById.get(t.toAccountId)
  return {
    ...t,
    initiatorName:   ini ? `${ini.firstName} ${ini.lastName}`.trim() : null,
    fromAccountName: from?.accountName  || null,
    fromAccountOwner: from?.ownerName   || null,
    toAccountName:   to?.accountName    || null,
    toAccountOwner:  to?.ownerName      || null,
  }
}

router.get('/transactions', async (req, res) => {
  const { page = 1, pageSize = 200 } = req.query
  try {
    const data = await gameFetch(`/api/admin/read/transactions?page=${page}&pageSize=${pageSize}`)
    const lookups = await getTxLookups()
    const transactions = (data?.transactions || []).map(t => enrichTx(t, lookups))
    res.json({ ...data, transactions })
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.get('/characters/:id', async (req, res) => {
  try { res.json(await gameFetch(`/api/admin/read/characters/${encodeURIComponent(req.params.id)}`)) }
  catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.get('/characters/:id/inventory', async (req, res) => {
  try { res.json(await gameFetch(`/api/admin/read/characters/${encodeURIComponent(req.params.id)}/inventory`)) }
  catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

// Enqueue une commande admin in-game pour confirmer côté gamemode qu'une action
// DB directe (rename/delete character, etc.) a bien été propagée. Best-effort :
// si la queue échoue, on retourne null pour ne pas bloquer l'action principale.
async function enqueueGamemodeAck(req, command, targetSteamId, args) {
  try {
    const queued = await gameFetch('/api/admin/command/queue', {
      method: 'POST',
      body: {
        AdminSteamId:  req.user?.steamId || 'web-admin',
        Command:       String(command).toLowerCase(),
        TargetSteamId: targetSteamId || null,
        ArgsJson:      args ? JSON.stringify(args) : null,
      },
    })
    return queued?.id || queued?.Id || null
  } catch (e) {
    console.warn(`[gameadmin] enqueueGamemodeAck(${command}) failed:`, e.message)
    return null
  }
}

// Édition d'un personnage (renommage prénom/nom RP, etc.)
router.patch('/characters/:id', async (req, res) => {
  // Whitelist : on n'accepte que des champs sûrs depuis le panel web
  const ALLOWED = ['firstName', 'lastName']
  const changes = {}
  for (const k of ALLOWED) {
    if (req.body && Object.prototype.hasOwnProperty.call(req.body, k)) {
      const v = String(req.body[k] ?? '').trim()
      if (v.length > 0 && v.length <= 64) changes[k] = v
    }
  }
  if (Object.keys(changes).length === 0) {
    return res.status(400).json({ error: 'Aucun champ valide à modifier (firstName/lastName, 1-64 chars).' })
  }
  try {
    const data = await gameFetch(`/api/admin/character/${encodeURIComponent(req.params.id)}`, {
      method: 'PATCH',
      body: changes,
    })
    logAction(req, 'character_update', null, req.body?.reason || null, { characterId: req.params.id, changes })

    // Confirme côté gamemode : si le proprio est connecté avec ce perso actif,
    // on lui notifie le changement de nom RP. Sinon, simple ack.
    const ownerSteamId = data?.character?.ownerId || data?.character?.OwnerId || null
    const commandId = await enqueueGamemodeAck(req, 'character_update', ownerSteamId, {
      characterId: req.params.id,
    })

    res.json({ ...(data ?? { ok: true, ...changes }), commandId })
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

// Suppression d'un personnage (cas des noms RP troll, etc.)
router.delete('/characters/:id', async (req, res) => {
  const reason = (req.query?.reason || req.body?.reason || '').toString().slice(0, 500)
  try {
    // On lit l'ownerId AVANT la suppression — sinon il sera plus récupérable
    let ownerSteamId = null
    try {
      const before = await gameFetch(`/api/admin/read/characters/${encodeURIComponent(req.params.id)}`)
      ownerSteamId = before?.character?.ownerId || before?.ownerId || null
    } catch { /* best-effort */ }

    const data = await gameFetch(`/api/admin/character/${encodeURIComponent(req.params.id)}`, {
      method: 'DELETE',
    })
    logAction(req, 'character_delete', null, reason || null, { characterId: req.params.id })

    // Confirme côté gamemode : si le proprio joue ce perso, kick propre.
    const commandId = await enqueueGamemodeAck(req, 'character_delete', ownerSteamId, {
      characterId: req.params.id,
    })

    res.json({ ...(data ?? { ok: true }), commandId })
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.get('/characters/:id/accounts', async (req, res) => {
  try { res.json(await gameFetch(`/api/admin/read/characters/${encodeURIComponent(req.params.id)}/accounts`)) }
  catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.get('/accounts/:accountId/transactions', async (req, res) => {
  const { page = 1, pageSize = 50 } = req.query
  try {
    const data = await gameFetch(
      `/api/admin/read/accounts/${encodeURIComponent(req.params.accountId)}/transactions?page=${page}&pageSize=${pageSize}`
    )
    const lookups = await getTxLookups()
    if (Array.isArray(data)) {
      res.json(data.map(t => enrichTx(t, lookups)))
    } else if (Array.isArray(data?.transactions)) {
      res.json({ ...data, transactions: data.transactions.map(t => enrichTx(t, lookups)) })
    } else {
      res.json(data)
    }
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

// ─────────────────────────────────────────────────────────────────────────
//  BANS (lecture + écriture, mappe vers /api/admin/ban/ existant)
// ─────────────────────────────────────────────────────────────────────────
router.get('/bans', async (_req, res) => {
  try { res.json(await gameFetch('/api/admin/ban/getList')) }
  catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.post('/bans', async (req, res) => {
  const { UserSteamId, Reason } = req.body || {}
  if (!UserSteamId) return res.status(400).json({ error: 'UserSteamId requis' })
  try {
    const data = await gameFetch('/api/admin/ban/', {
      method: 'POST',
      body: {
        UserSteamId,
        Reason: Reason || '',
        AdminSteamId: req.user?.steamId || 'web-admin',
      },
    })
    logAction(req, 'ban', UserSteamId, Reason)
    res.json(data)
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.delete('/bans/:steamId', async (req, res) => {
  try {
    const data = await gameFetch(`/api/admin/unban/${encodeURIComponent(req.params.steamId)}`, {
      method: 'POST',
      body: {
        Reason: req.body?.Reason || '',
        AdminSteamId: req.user?.steamId || 'web-admin',
      },
    })
    logAction(req, 'unban', req.params.steamId, req.body?.Reason)
    res.json(data)
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

// ─────────────────────────────────────────────────────────────────────────
//  WHITELIST (mappe vers /api/admin/whitelist/ existant)
// ─────────────────────────────────────────────────────────────────────────
router.get('/whitelist', async (_req, res) => {
  try { res.json(await gameFetch('/api/admin/whitelist/getList')) }
  catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.post('/whitelist', async (req, res) => {
  const { UserSteamId } = req.body || {}
  if (!UserSteamId) return res.status(400).json({ error: 'UserSteamId requis' })
  try {
    const data = await gameFetch('/api/admin/whitelist/', {
      method: 'POST',
      body: {
        UserSteamId,
        AdminSteamId: req.user?.steamId || 'web-admin',
      },
    })
    logAction(req, 'whitelist_add', UserSteamId)
    res.json(data)
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.delete('/whitelist/:steamId', async (req, res) => {
  try {
    const data = await gameFetch(`/api/admin/whitelist/${encodeURIComponent(req.params.steamId)}/supp`, {
      method: 'POST',
    })
    logAction(req, 'whitelist_remove', req.params.steamId)
    res.json(data)
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

// ─────────────────────────────────────────────────────────────────────────
//  WARNINGS (lecture seule — l'API actuelle n'expose pas encore le write)
// ─────────────────────────────────────────────────────────────────────────
router.get('/warns', async (_req, res) => {
  try { res.json(await gameFetch('/api/admin/read/warns')) }
  catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

// ─────────────────────────────────────────────────────────────────────────
//  LOGS LOCAUX — journal SQLite des actions web (legacy, conservé en backup)
// ─────────────────────────────────────────────────────────────────────────
router.get('/logs', (req, res) => {
  try {
    const limit = Math.min(parseInt(req.query.limit) || 200, 500)
    const rows = db.prepare(
      'SELECT * FROM gameadmin_logs ORDER BY created_at DESC LIMIT ?'
    ).all(limit)
    res.json(rows)
  } catch (e) { res.status(500).json({ error: e.message }) }
})

// ─────────────────────────────────────────────────────────────────────────
//  AUDIT — proxie vers l'API jeu (sessions, chat, actions admin centralisées)
// ─────────────────────────────────────────────────────────────────────────

// Petit helper pour reconduire les query params vers l'API jeu sans les copier à la main
function passthroughQuery(req, allowed) {
  const out = []
  for (const key of allowed) {
    const v = req.query[key]
    if (v !== undefined && v !== '') out.push(`${key}=${encodeURIComponent(v)}`)
  }
  return out.length ? `?${out.join('&')}` : ''
}

router.get('/sessions', async (req, res) => {
  const qs = passthroughQuery(req, ['from', 'to', 'steamId', 'activeOnly', 'page', 'pageSize'])
  try {
    const data = await gameFetch(`/api/admin/read/sessions${qs}`)
    // Enrichir avec les profils Steam pour avoir nom + avatar dans la timeline
    const ids = [...new Set((data?.sessions || []).map(s => s.steamId))]
    const profiles = await enrichWithSteamProfiles(ids)
    res.json({
      ...data,
      sessions: (data?.sessions || []).map(s => ({
        ...s,
        steamProfile: profiles[s.steamId] || null,
      })),
    })
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.get('/sessions/active', async (_req, res) => {
  try {
    const sessions = await gameFetch('/api/admin/read/sessions/active')
    const ids = [...new Set((sessions || []).map(s => s.steamId))]
    const profiles = await enrichWithSteamProfiles(ids)
    res.json((sessions || []).map(s => ({ ...s, steamProfile: profiles[s.steamId] || null })))
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.get('/sessions/playtime', async (req, res) => {
  const qs = passthroughQuery(req, ['from', 'to', 'steamId'])
  try {
    const rows = await gameFetch(`/api/admin/read/sessions/playtime${qs}`)
    const ids = [...new Set((rows || []).map(r => r.steamId))]
    const profiles = await enrichWithSteamProfiles(ids)
    res.json((rows || []).map(r => ({ ...r, steamProfile: profiles[r.steamId] || null })))
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.get('/chat', async (req, res) => {
  const qs = passthroughQuery(req, ['from', 'to', 'steamId', 'channel', 'search', 'excludeCommands', 'page', 'pageSize'])
  try {
    const data = await gameFetch(`/api/admin/read/chat${qs}`)
    const ids = [...new Set((data?.messages || []).map(m => m.steamId))]
    const profiles = await enrichWithSteamProfiles(ids)
    res.json({
      ...data,
      messages: (data?.messages || []).map(m => ({
        ...m,
        steamProfile: profiles[m.steamId] || null,
      })),
    })
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

// ─────────────────────────────────────────────────────────────────────────
//  ADMIN COMMAND QUEUE — déposer une commande pour exécution in-game
// ─────────────────────────────────────────────────────────────────────────
router.post('/commands', async (req, res) => {
  const { command, targetSteamId, args } = req.body || {}
  if (!command) return res.status(400).json({ error: 'command requis' })

  try {
    const data = await gameFetch('/api/admin/command/queue', {
      method: 'POST',
      body: {
        AdminSteamId:  req.user?.steamId || 'web-admin',
        Command:       String(command).toLowerCase(),
        TargetSteamId: targetSteamId || null,
        ArgsJson:      args ? JSON.stringify(args) : null,
      },
    })
    // Mirror dans gameadmin_logs local + AdminActionLogs API (audit unifié)
    logAction(req, `webcmd:${command}`, targetSteamId || null, JSON.stringify(args || {}))
    res.status(201).json(data)
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.get('/commands', async (req, res) => {
  const qs = passthroughQuery(req, ['status', 'limit'])
  try {
    res.json(await gameFetch(`/api/admin/command${qs}`))
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

// Statut d'une commande précise — utilisé par le panel pour polling efficace
// après une action admin (DB directe ou queue) : permet d'afficher
// "exécuté in-game" ou "échec" sans tirer toute la liste à chaque tick.
router.get('/commands/:id', async (req, res) => {
  try {
    res.json(await gameFetch(`/api/admin/command/${encodeURIComponent(req.params.id)}`))
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

// ─────────────────────────────────────────────────────────────────────────
//  INVENTORY WRITE — mappe vers /api/admin/inventory/ de l'API jeu
// ─────────────────────────────────────────────────────────────────────────
router.post('/inventory/give', async (req, res) => {
  const { characterId, itemGameId, mass, count, line, collum, metadata } = req.body || {}
  if (!characterId || !itemGameId) return res.status(400).json({ error: 'characterId et itemGameId requis' })
  try {
    const data = await gameFetch('/api/admin/inventory/give', {
      method: 'POST',
      body: { characterId, itemGameId, mass, count, line, collum, metadata },
    })
    logAction(req, 'inventory_give', null, `${itemGameId}×${count ?? 1}`, { characterId, itemGameId, count })
    res.status(201).json(data)
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.patch('/inventory/item/:itemId', async (req, res) => {
  const { characterId, ...itemChanges } = req.body || {}
  try {
    const data = await gameFetch(`/api/admin/inventory/item/${encodeURIComponent(req.params.itemId)}`, {
      method: 'PATCH',
      body: itemChanges,
    })
    logAction(req, 'inventory_modify', null, null, { characterId: characterId || null, itemId: req.params.itemId, changes: itemChanges })
    res.json(data)
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.delete('/inventory/item/:itemId', async (req, res) => {
  const characterId = req.query.characterId || null
  try {
    const data = await gameFetch(`/api/admin/inventory/item/${encodeURIComponent(req.params.itemId)}`, {
      method: 'DELETE',
    })
    logAction(req, 'inventory_delete', null, null, { characterId, itemId: req.params.itemId })
    res.json(data ?? { ok: true })
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.get('/inventory-logs', async (req, res) => {
  const qs = passthroughQuery(req, ['from', 'to', 'steamId', 'characterId', 'itemGameId', 'action', 'page', 'pageSize'])
  try {
    const data = await gameFetch(`/api/admin/read/inventory${qs}`)
    const ids = [...new Set((data?.logs || []).map(l => l.actorSteamId))]
    const profiles = await enrichWithSteamProfiles(ids)
    res.json({
      ...data,
      logs: (data?.logs || []).map(l => ({
        ...l,
        actorProfile: profiles[l.actorSteamId] || null,
      })),
    })
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

router.get('/admin-actions', async (req, res) => {
  const qs = passthroughQuery(req, ['from', 'to', 'adminSteamId', 'targetSteamId', 'action', 'source', 'page', 'pageSize'])
  try {
    const data = await gameFetch(`/api/admin/read/admin-actions${qs}`)
    const ids = [...new Set([
      ...(data?.actions || []).map(a => a.adminSteamId),
      ...(data?.actions || []).map(a => a.targetSteamId).filter(Boolean),
    ])]
    const profiles = await enrichWithSteamProfiles(ids)
    res.json({
      ...data,
      actions: (data?.actions || []).map(a => ({
        ...a,
        adminProfile:  profiles[a.adminSteamId]  || null,
        targetProfile: a.targetSteamId ? (profiles[a.targetSteamId] || null) : null,
      })),
    })
  } catch (e) { res.status(e.status || 502).json({ error: e.message }) }
})

export default router
