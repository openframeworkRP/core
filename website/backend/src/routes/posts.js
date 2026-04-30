import express from 'express'
import db from '../db.js'
import { requireAuth } from '../auth.js'
import { broadcast } from '../socket.js'

const router = express.Router()

// ── Génération de slug depuis le mois (YYYY-MM → devlog-mars-2026-a3f2) ──
const MONTHS_FR = ['janvier','fevrier','mars','avril','mai','juin','juillet','aout','septembre','octobre','novembre','decembre']

function randomSuffix(len = 4) {
  return Math.random().toString(36).slice(2, 2 + len)
}

function slugFromMonth(month) {
  // month = "YYYY-MM"
  const [year, mm] = month.split('-')
  const monthName = MONTHS_FR[parseInt(mm, 10) - 1] ?? mm
  return `devlog-${monthName}-${year}-${randomSuffix()}`
}

// ── helpers ───────────────────────────────────────────────────────────────

function hydratePost(post) {
  if (!post) return null
  const games = db.prepare(`
    SELECT g.* FROM games g
    JOIN post_games pg ON pg.game_id = g.id
    WHERE pg.post_id = ?
    ORDER BY g.id
  `).all(post.id)

  const blocksFr = db.prepare(`
    SELECT * FROM blocks WHERE post_id = ? AND lang = 'fr' ORDER BY position
  `).all(post.id).map(b => ({ ...b, data: JSON.parse(b.data) }))

  const blocksEn = db.prepare(`
    SELECT * FROM blocks WHERE post_id = ? AND lang = 'en' ORDER BY position
  `).all(post.id).map(b => ({ ...b, data: JSON.parse(b.data) }))

  return { ...post, games, blocksFr, blocksEn }
}

// ── GET /posts  (liste publiés) ──────────────────────────────────────────
router.get('/', (req, res) => {
  const { all } = req.query           // ?all=1 → admin, voit tout
  const posts = all
    ? db.prepare('SELECT * FROM posts ORDER BY month DESC').all()
    : db.prepare('SELECT * FROM posts WHERE published = 1 ORDER BY month DESC').all()
  res.json(posts.map(hydratePost))
})

// ── GET /posts/:slug ─────────────────────────────────────────────────────
router.get('/:slug', (req, res) => {
  const post = db.prepare('SELECT * FROM posts WHERE slug = ?').get(req.params.slug)
  if (!post) return res.status(404).json({ error: 'Not found' })
  res.json(hydratePost(post))
})

// ── POST /posts ───────────────────────────────────────────────────────────
router.post('/', requireAuth, (req, res) => {
  const { title_fr, title_en, excerpt_fr, excerpt_en, cover, author, read_time, month, games = [], published = 0, slug: customSlug } = req.body
  if (!title_fr || !title_en || !month) return res.status(400).json({ error: 'title_fr, title_en and month are required' })

  const slug = customSlug || slugFromMonth(month)

  const upsertPost = db.transaction(() => {
    const result = db.prepare(`
      INSERT INTO posts (slug, month, title_fr, title_en, excerpt_fr, excerpt_en, cover, author, read_time, published)
      VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    `).run(slug, month, title_fr, title_en, excerpt_fr ?? '', excerpt_en ?? '', cover ?? null, author ?? 'Small Box Studio', read_time ?? 5, published ? 1 : 0)

    const postId = result.lastInsertRowid
    syncGames(postId, games)
    return postId
  })

  try {
    const postId = upsertPost()
    const post = db.prepare('SELECT * FROM posts WHERE id = ?').get(postId)
    broadcast('posts_updated', {})
    res.status(201).json(hydratePost(post))
  } catch (e) {
    res.status(409).json({ error: 'Slug already exists: ' + slug })
  }
})

// ── PUT /posts/:id ────────────────────────────────────────────────────────
router.put('/:id', requireAuth, (req, res) => {
  const { title_fr, title_en, excerpt_fr, excerpt_en, cover, author, read_time, month, games, published } = req.body
  const existing = db.prepare('SELECT * FROM posts WHERE id = ?').get(req.params.id)
  if (!existing) return res.status(404).json({ error: 'Not found' })

  db.prepare(`
    UPDATE posts SET
      title_fr = ?, title_en = ?, excerpt_fr = ?, excerpt_en = ?,
      cover = ?, author = ?, read_time = ?, month = ?, published = ?,
      updated_at = datetime('now')
    WHERE id = ?
  `).run(
    title_fr ?? existing.title_fr,
    title_en ?? existing.title_en,
    excerpt_fr ?? existing.excerpt_fr,
    excerpt_en ?? existing.excerpt_en,
    cover !== undefined ? cover : existing.cover,
    author ?? existing.author,
    read_time ?? existing.read_time,
    month ?? existing.month,
    published !== undefined ? (published ? 1 : 0) : existing.published,
    req.params.id
  )

  if (games !== undefined) syncGames(req.params.id, games)

  const post = db.prepare('SELECT * FROM posts WHERE id = ?').get(req.params.id)
  broadcast('posts_updated', {})
  res.json(hydratePost(post))
})

// ── DELETE /posts/:id ─────────────────────────────────────────────────────
router.delete('/:id', requireAuth, (req, res) => {
  db.prepare('DELETE FROM posts WHERE id = ?').run(req.params.id)
  broadcast('posts_updated', {})
  res.json({ ok: true })
})

// ── POST /posts/:id/publish  ──────────────────────────────────────────────
router.post('/:id/publish', requireAuth, (req, res) => {
  db.prepare("UPDATE posts SET published = 1, updated_at = datetime('now') WHERE id = ?").run(req.params.id)
  const post = db.prepare('SELECT * FROM posts WHERE id = ?').get(req.params.id)
  broadcast('posts_updated', {})
  res.json(hydratePost(post))
})

// ── POST /posts/:id/unpublish ─────────────────────────────────────────────
router.post('/:id/unpublish', requireAuth, (req, res) => {
  db.prepare("UPDATE posts SET published = 0, updated_at = datetime('now') WHERE id = ?").run(req.params.id)
  const post = db.prepare('SELECT * FROM posts WHERE id = ?').get(req.params.id)
  broadcast('posts_updated', {})
  res.json(hydratePost(post))
})

// ── POST /posts/:slug/view  (incrémente le compteur de vues, 1 fois par IP+UA) ──
router.post('/:slug/view', (req, res) => {
  const post = db.prepare('SELECT id FROM posts WHERE slug = ? AND published = 1').get(req.params.slug)
  if (!post) return res.status(404).json({ error: 'Not found' })

  // IP réelle (derrière un proxy/nginx)
  const ip = (req.headers['x-forwarded-for'] || req.socket.remoteAddress || '').split(',')[0].trim()
  // User-Agent tronqué à 300 chars pour éviter les abus
  const ua = (req.headers['user-agent'] || '').slice(0, 300)

  // Doublon = même IP ET même User-Agent sur le même post
  const result = db.prepare(
    'INSERT OR IGNORE INTO post_views (post_id, ip, user_agent) VALUES (?, ?, ?)'
  ).run(post.id, ip, ua)

  // N'incrémente que si la combinaison est nouvelle
  if (result.changes > 0) {
    db.prepare('UPDATE posts SET views = views + 1 WHERE id = ?').run(post.id)
  }

  const updated = db.prepare('SELECT views FROM posts WHERE id = ?').get(post.id)
  res.json({ views: updated.views })
})

// ── helper : sync jeux liés ───────────────────────────────────────────────
function syncGames(postId, gameSlugs) {
  db.prepare('DELETE FROM post_games WHERE post_id = ?').run(postId)
  for (const slug of gameSlugs) {
    const game = db.prepare('SELECT id FROM games WHERE slug = ?').get(slug)
    if (game) db.prepare('INSERT INTO post_games (post_id, game_id) VALUES (?, ?)').run(postId, game.id)
  }
}

export default router
