/**
 * server.js — Serveur de production pour le frontend Vite
 *
 * Remplace `vite preview` pour pouvoir :
 *  1. Servir les fichiers statiques buildés (dist/)
 *  2. Détecter les bots sociaux sur /devblog/:slug et renvoyer le HTML OG
 *     généré directement à partir de l'API JSON du backend
 *  3. Proxy /api, /auth, /uploads vers le backend
 *  4. SPA fallback → index.html pour toutes les autres routes
 */

import express from 'express'
import { createProxyMiddleware } from 'http-proxy-middleware'
import { fileURLToPath } from 'url'
import { dirname, join } from 'path'
import { readFileSync } from 'fs'
import http from 'http'

const __dirname  = dirname(fileURLToPath(import.meta.url))
const PORT       = process.env.PORT || 4173
const API_BASE   = process.env.API_INTERNAL_URL || 'http://api:3001'
const SITE_URL   = process.env.FRONTEND_URL || 'https://openframework.fr'
const SITE_NAME  = 'Small Box Studio'
const DEFAULT_IMG = `${SITE_URL}/banner_site.png`

// ── Détection bots sociaux ──────────────────────────────────────────────────
function isSocialBot(ua = '') {
  const lower = ua.toLowerCase()
  return (
    lower.includes('discordbot') ||
    lower.includes('twitterbot') ||
    lower.includes('facebookexternalhit') ||
    lower.includes('slackbot') ||
    lower.includes('telegrambot') ||
    lower.includes('whatsapp') ||
    lower.includes('linkedinbot') ||
    lower.includes('embedly') ||
    lower.includes('outbrain') ||
    lower.includes('pinterest') ||
    lower.includes('developers.google.com/+/web/snippet')
  )
}

// ── Fetch JSON depuis le backend ───────────────────────────────────────────
function fetchJson(path) {
  return new Promise((resolve, reject) => {
    const url = new URL(path, API_BASE)
    const req = http.get(url.toString(), (res) => {
      let data = ''
      res.on('data', chunk => { data += chunk })
      res.on('end', () => {
        try { resolve({ status: res.statusCode, body: JSON.parse(data) }) }
        catch { resolve({ status: res.statusCode, body: null }) }
      })
    })
    req.on('error', reject)
  })
}

// ── Échapper les attributs HTML ────────────────────────────────────────────
function esc(str) {
  return String(str ?? '').replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

// ── Générer le HTML OG pour un post devblog ────────────────────────────────
function buildDevblogOgHtml(post) {
  const title       = post.title_fr || post.title_en || SITE_NAME
  const description = (post.excerpt_fr || post.excerpt_en || title).substring(0, 200)
  const image       = post.cover
    ? (post.cover.startsWith('http') ? post.cover : `${SITE_URL}${post.cover}`)
    : DEFAULT_IMG
  const canonical   = `${SITE_URL}/devblog/${post.slug}`
  const fullTitle   = `${title} | ${SITE_NAME}`

  return `<!DOCTYPE html>
<html lang="fr">
<head>
  <meta charset="UTF-8" />
  <title>${esc(fullTitle)}</title>
  <meta name="description" content="${esc(description)}" />
  <link rel="canonical" href="${esc(canonical)}" />

  <!-- Open Graph -->
  <meta property="og:type"              content="article" />
  <meta property="og:url"               content="${esc(canonical)}" />
  <meta property="og:title"             content="${esc(fullTitle)}" />
  <meta property="og:description"       content="${esc(description)}" />
  <meta property="og:image"             content="${esc(image)}" />
  <meta property="og:image:url"         content="${esc(image)}" />
  <meta property="og:image:secure_url"  content="${esc(image)}" />
  <meta property="og:image:width"       content="1200" />
  <meta property="og:image:height"      content="630" />
  <meta property="og:image:alt"         content="${esc(title)}" />
  <meta property="og:site_name"         content="${esc(SITE_NAME)}" />
  <meta property="og:locale"            content="fr_FR" />

  <!-- Twitter Card -->
  <meta name="twitter:card"        content="summary_large_image" />
  <meta name="twitter:title"       content="${esc(fullTitle)}" />
  <meta name="twitter:description" content="${esc(description)}" />
  <meta name="twitter:image"       content="${esc(image)}" />
  <meta name="twitter:image:alt"   content="${esc(title)}" />

  <meta http-equiv="refresh" content="0; url=${esc(canonical)}" />
</head>
<body>
  <p>Redirecting to <a href="${esc(canonical)}">${esc(canonical)}</a>…</p>
</body>
</html>`
}

const app = express()

// ── Proxy routes backend ────────────────────────────────────────────────────
// pathRewrite preserve l'URL originale (req.originalUrl) — sinon Express
// strip le prefixe lors du `app.use('/api', proxy)` et le backend recoit
// `/setup/status` au lieu de `/api/setup/status`, ce qui casse toutes les
// routes API.
const backendProxy = createProxyMiddleware({
  target: API_BASE,
  changeOrigin: true,
  pathRewrite: (_path, req) => req.originalUrl,
  on: {
    proxyReq: (proxyReq) => {
      proxyReq.setHeader('X-Forwarded-Proto', 'https')
    },
  },
})

// ── Proxy Socket.io (HTTP polling + WebSocket upgrade) ──────────────────────
const socketProxy = createProxyMiddleware({
  target: API_BASE,
  changeOrigin: true,
  pathRewrite: (_path, req) => req.originalUrl,
  on: {
    proxyReq: (proxyReq) => {
      proxyReq.setHeader('X-Forwarded-Proto', 'https')
    },
  },
})

app.use('/api',       backendProxy)
app.use('/auth',      backendProxy)
app.use('/uploads',   backendProxy)
app.use('/og',        backendProxy)
app.use('/socket.io', socketProxy)

// ── Interception bots sur /devblog/:slug ────────────────────────────────────
app.get('/devblog/:slug', async (req, res, next) => {
  const ua = req.headers['user-agent'] || ''
  if (!isSocialBot(ua)) return next() // humain → SPA

  try {
    const { status, body } = await fetchJson(`/api/posts/${encodeURIComponent(req.params.slug)}`)
    if (status !== 200 || !body) return res.status(404).send('Not found')
    res.setHeader('Content-Type', 'text/html; charset=utf-8')
    res.setHeader('Cache-Control', 'no-cache')
    res.send(buildDevblogOgHtml(body))
  } catch (err) {
    console.error('[OG devblog] fetch error:', err.message)
    next() // fallback SPA
  }
})

// ── Interception bots sur /v/:slug (vidéos) ─────────────────────────────────
app.get('/v/:slug', async (req, res, next) => {
  const ua = req.headers['user-agent'] || ''
  if (!isSocialBot(ua)) return next() // humain → SPA

  try {
    const { status, body } = await fetchJson(`/api/videos/${encodeURIComponent(req.params.slug)}`)
    if (status !== 200 || !body) return res.status(404).send('Not found')

    const title    = esc(body.title || 'Video')
    const fullTitle = esc(`${body.title || 'Video'} | ${SITE_NAME}`)
    const canonical = esc(`${SITE_URL}/v/${body.slug}`)
    const SERVICE_URL = API_BASE
    const absVideo  = body.filename
      ? `${SERVICE_URL}/uploads/${body.filename}`
      : ''

    const html = `<!DOCTYPE html>
<html lang="fr">
<head>
  <meta charset="UTF-8" />
  <title>${fullTitle}</title>
  <meta property="og:type"              content="video.other" />
  <meta property="og:url"               content="${canonical}" />
  <meta property="og:title"             content="${fullTitle}" />
  <meta property="og:site_name"         content="${esc(SITE_NAME)}" />
  <meta property="og:video"             content="${esc(absVideo)}" />
  <meta property="og:video:secure_url"  content="${esc(absVideo)}" />
  <meta property="og:video:type"        content="${esc(body.mime || 'video/webm')}" />
  <meta property="og:video:width"       content="1280" />
  <meta property="og:video:height"      content="720" />
  <meta name="twitter:card"             content="player" />
  <meta name="twitter:title"            content="${fullTitle}" />
  <meta name="twitter:player"           content="${canonical}" />
  <meta name="twitter:player:width"     content="1280" />
  <meta name="twitter:player:height"    content="720" />
  <link rel="canonical"                 href="${canonical}" />
  <meta http-equiv="refresh" content="0; url=${canonical}" />
</head>
<body><p>Redirecting…</p></body>
</html>`

    res.setHeader('Content-Type', 'text/html; charset=utf-8')
    res.setHeader('Cache-Control', 'no-cache')
    res.send(html)
  } catch (err) {
    console.error('[OG video] fetch error:', err.message)
    next() // fallback SPA
  }
})

// ── Fichiers statiques Vite (dist/) ─────────────────────────────────────────
const DIST = join(__dirname, 'dist')
app.use(express.static(DIST, { index: false }))

// ── SPA fallback ────────────────────────────────────────────────────────────
const indexHtml = readFileSync(join(DIST, 'index.html'), 'utf-8')
app.get('*', (_req, res) => {
  res.setHeader('Content-Type', 'text/html; charset=utf-8')
  res.send(indexHtml)
})

// ── Serveur HTTP — nécessaire pour proxifier les WebSocket (Socket.io) ─────
const server = http.createServer(app)

// Délègue les upgrades WebSocket (/socket.io) au proxy
server.on('upgrade', socketProxy.upgrade)

server.listen(PORT, '0.0.0.0', () => {
  console.log(`🌐  Frontend server running on http://0.0.0.0:${PORT}`)
})
