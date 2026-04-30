import { Router } from 'express'
import db from '../db.js'

const router = Router()

const BASE_URL    = process.env.FRONTEND_URL || 'https://openframework.fr'
const SERVICE_URL = process.env.SERVICE_URL  || process.env.API_URL || 'http://localhost:3001'
const SITE_NAME   = 'Small Box Studio'
const DEFAULT_IMAGE = `${BASE_URL}/banner_site.png`

function absImage(img) {
  if (!img) return DEFAULT_IMAGE
  if (img.startsWith('http')) return img
  return `${BASE_URL}${img}`
}

function escAttr(str) {
  return String(str ?? '').replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

function buildHtml({ title, description, image, url, type = 'website' }) {
  const fullTitle = `${title} | ${SITE_NAME}`
  const canonical = `${BASE_URL}${url}`
  const img = absImage(image)

  return `<!DOCTYPE html>
<html lang="fr">
<head>
  <meta charset="UTF-8" />
  <title>${escAttr(fullTitle)}</title>
  <meta name="description" content="${escAttr(description)}" />

  <!-- Open Graph -->
  <meta property="og:type"        content="${escAttr(type)}" />
  <meta property="og:url"         content="${escAttr(canonical)}" />
  <meta property="og:title"       content="${escAttr(fullTitle)}" />
  <meta property="og:description" content="${escAttr(description)}" />
  <meta property="og:image"       content="${escAttr(img)}" />
  <meta property="og:image:url"   content="${escAttr(img)}" />
  <meta property="og:image:secure_url" content="${escAttr(img)}" />
  <meta property="og:image:alt"   content="${escAttr(title)}" />
  <meta property="og:site_name"   content="${escAttr(SITE_NAME)}" />
  <meta property="og:locale"      content="fr_FR" />

  <!-- Twitter Card -->
  <meta name="twitter:card"        content="summary_large_image" />
  <meta name="twitter:title"       content="${escAttr(fullTitle)}" />
  <meta name="twitter:description" content="${escAttr(description)}" />
  <meta name="twitter:image"       content="${escAttr(img)}" />
  <meta name="twitter:image:alt"   content="${escAttr(title)}" />

  <link rel="canonical" href="${escAttr(canonical)}" />
  <meta http-equiv="refresh" content="0; url=${escAttr(canonical)}" />
</head>
<body>
  <p>Redirecting to <a href="${escAttr(canonical)}">${escAttr(canonical)}</a>…</p>
</body>
</html>`
}

function buildVideoHtml({ title, videoUrl, url, mime = 'video/webm' }) {
  const fullTitle = `${title} | ${SITE_NAME}`
  const canonical = `${BASE_URL}${url}`
  const absVideo  = videoUrl.startsWith('http') ? videoUrl : `${BASE_URL}${videoUrl}`

  return `<!DOCTYPE html>
<html lang="fr">
<head>
  <meta charset="UTF-8" />
  <title>${fullTitle}</title>
  <meta property="og:type"              content="video.other" />
  <meta property="og:url"               content="${canonical}" />
  <meta property="og:title"             content="${fullTitle}" />
  <meta property="og:site_name"         content="${SITE_NAME}" />
  <meta property="og:video"             content="${absVideo}" />
  <meta property="og:video:secure_url"  content="${absVideo}" />
  <meta property="og:video:type"        content="${mime}" />
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
<body>
  <p>Redirecting to <a href="${canonical}">${canonical}</a>…</p>
</body>
</html>`
}

export function isSocialBot(userAgent = '') {
  const ua = userAgent.toLowerCase()
  return (
    ua.includes('discordbot') ||
    ua.includes('twitterbot') ||
    ua.includes('facebookexternalhit') ||
    ua.includes('slackbot') ||
    ua.includes('telegrambot') ||
    ua.includes('whatsapp') ||
    ua.includes('linkedinbot') ||
    ua.includes('embedly') ||
    ua.includes('outbrain') ||
    ua.includes('pinterest') ||
    ua.includes('developers.google.com/+/web/snippet')
  )
}

// ── GET /og/devblog/:slug — OG HTML for devblog posts ────────────────────
router.get('/devblog/:slug', (req, res) => {
  const post = db.prepare('SELECT * FROM posts WHERE slug = ? AND published = 1').get(req.params.slug)
  if (!post) return res.status(404).send('Not found')

  const title       = post.title_fr || post.title_en
  const description = (post.excerpt_fr || post.excerpt_en || title).substring(0, 200)
  const image       = post.cover || null
  const url         = `/devblog/${post.slug}`

  res.setHeader('Content-Type', 'text/html; charset=utf-8')
  res.setHeader('Cache-Control', 'public, max-age=300')
  res.send(buildHtml({ title, description, image, url, type: 'article' }))
})

// ── GET /og/v/:slug — OG HTML for video pages (Discord video embed) ──────
router.get('/v/:slug', (req, res) => {
  const video = db.prepare('SELECT * FROM videos WHERE slug = ?').get(req.params.slug)
  if (!video) return res.status(404).send('Not found')

  res.setHeader('Content-Type', 'text/html; charset=utf-8')
  res.setHeader('Cache-Control', video.status === 'ready' ? 'public, max-age=300' : 'no-cache')
  res.send(buildVideoHtml({
    title:    video.title || 'Video',
    videoUrl: `${SERVICE_URL}/uploads/${video.filename}`,
    url:      `/v/${video.slug}`,
    mime:     video.mime || 'video/webm',
  }))
})

// ── Middleware à monter sur /devblog/:slug dans index.js ──────────────────
export function ogMiddleware(req, res, next) {
  const ua = req.headers['user-agent'] || ''
  if (!isSocialBot(ua)) return next()

  const slug = req.params.slug
  const post = db.prepare('SELECT * FROM posts WHERE slug = ? AND published = 1').get(slug)
  if (!post) return next()

  const title       = post.title_fr || post.title_en
  const description = (post.excerpt_fr || post.excerpt_en || title).substring(0, 200)
  const image       = post.cover || null
  const url         = `/devblog/${post.slug}`

  res.setHeader('Content-Type', 'text/html; charset=utf-8')
  res.setHeader('Cache-Control', 'public, max-age=300')
  res.send(buildHtml({ title, description, image, url, type: 'article' }))
}

export default router
