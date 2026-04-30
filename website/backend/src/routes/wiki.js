import express from 'express'
import db from '../db.js'
import { requireAuth } from '../auth.js'

const router = express.Router()

// ── Helpers ───────────────────────────────────────────────────────────────

function slugify(text) {
  return text
    .toString().normalize('NFD').replace(/[\u0300-\u036f]/g, '')
    .toLowerCase().trim()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
}

function logHistory(articleId, articleTitle, userName, action, oldData, newData) {
  db.prepare(`
    INSERT INTO wiki_article_history (article_id, article_title, user_name, action, old_data, new_data)
    VALUES (?, ?, ?, ?, ?, ?)
  `).run(
    articleId,
    articleTitle,
    userName,
    action,
    oldData ? JSON.stringify(oldData) : null,
    newData ? JSON.stringify(newData) : null,
  )
}

// ═══════════════════════════════════════════════════════════════════════════
//  PUBLIC — endpoints sans auth (pour le jeu s&box)
// ═══════════════════════════════════════════════════════════════════════════

// GET /api/wiki/public — tous les articles publiés du wiki in-game, groupés par catégorie
router.get('/public', (_req, res) => {
  const categories = db.prepare(
    "SELECT * FROM wiki_categories WHERE type = 'ingame' ORDER BY order_index, created_at"
  ).all()
  const articles = db.prepare(
    "SELECT * FROM wiki_articles WHERE published = 1 AND category_id IN (SELECT id FROM wiki_categories WHERE type = 'ingame') ORDER BY order_index, created_at"
  ).all()

  const byCategory = {}
  for (const a of articles) {
    if (!byCategory[a.category_id]) byCategory[a.category_id] = []
    byCategory[a.category_id].push(a)
  }

  res.json(categories.map(cat => ({ ...cat, articles: byCategory[cat.id] || [] })))
})

// GET /api/wiki/public/search?q=keyword — recherche dans les articles publiés in-game
router.get('/public/search', (req, res) => {
  const q = (req.query.q || '').trim()
  if (!q) return res.json([])

  const articles = db.prepare(`
    SELECT a.*, c.name AS category_name, c.color AS category_color
    FROM wiki_articles a
    JOIN wiki_categories c ON c.id = a.category_id
    WHERE a.published = 1 AND c.type = 'ingame'
      AND (a.title LIKE ? OR a.content LIKE ?)
    ORDER BY a.title LIKE ? DESC, a.updated_at DESC
    LIMIT 25
  `).all(`%${q}%`, `%${q}%`, `%${q}%`)

  res.json(articles)
})

// GET /api/wiki/public/:slug — un article publié in-game par son slug
router.get('/public/:slug', (req, res) => {
  const article = db.prepare(`
    SELECT a.*, c.name AS category_name, c.color AS category_color
    FROM wiki_articles a
    JOIN wiki_categories c ON c.id = a.category_id
    WHERE a.slug = ? AND a.published = 1 AND c.type = 'ingame'
  `).get(req.params.slug)

  if (!article) return res.status(404).json({ error: 'Article introuvable' })
  res.json(article)
})

// ═══════════════════════════════════════════════════════════════════════════
//  ADMIN — endpoints authentifiés
// ═══════════════════════════════════════════════════════════════════════════

// GET /api/wiki — tout le wiki (ingame + dev) groupé par type
router.get('/', requireAuth, (_req, res) => {
  const categories = db.prepare('SELECT * FROM wiki_categories ORDER BY order_index, created_at').all()
  const articles   = db.prepare('SELECT * FROM wiki_articles ORDER BY order_index, created_at').all()

  const byCategory = {}
  for (const a of articles) {
    if (!byCategory[a.category_id]) byCategory[a.category_id] = []
    byCategory[a.category_id].push(a)
  }

  const result = { ingame: [], dev: [] }
  for (const cat of categories) {
    if (result[cat.type]) {
      result[cat.type].push({ ...cat, articles: byCategory[cat.id] || [] })
    }
  }
  res.json(result)
})

// ── Catégories ────────────────────────────────────────────────────────────

router.post('/categories', requireAuth, (req, res) => {
  const { type, name, color } = req.body
  if (!type || !['ingame', 'dev'].includes(type))
    return res.status(400).json({ error: 'Type invalide' })
  if (!name?.trim())
    return res.status(400).json({ error: 'Nom requis' })

  const result = db.prepare(
    'INSERT INTO wiki_categories (type, name, color) VALUES (?, ?, ?)'
  ).run(type, name.trim(), color || '#5865f2')

  res.json(db.prepare('SELECT * FROM wiki_categories WHERE id = ?').get(result.lastInsertRowid))
})

router.put('/categories/:id', requireAuth, (req, res) => {
  const { name, color } = req.body
  if (!name?.trim()) return res.status(400).json({ error: 'Nom requis' })

  const result = db.prepare(
    'UPDATE wiki_categories SET name = ?, color = ? WHERE id = ?'
  ).run(name.trim(), color || '#5865f2', req.params.id)

  if (result.changes === 0) return res.status(404).json({ error: 'Not found' })
  res.json(db.prepare('SELECT * FROM wiki_categories WHERE id = ?').get(req.params.id))
})

router.delete('/categories/:id', requireAuth, (req, res) => {
  const result = db.prepare('DELETE FROM wiki_categories WHERE id = ?').run(req.params.id)
  if (result.changes === 0) return res.status(404).json({ error: 'Not found' })
  res.json({ ok: true })
})

// ── Articles ──────────────────────────────────────────────────────────────

router.post('/categories/:categoryId/articles', requireAuth, (req, res) => {
  const { categoryId } = req.params
  const cat = db.prepare('SELECT id FROM wiki_categories WHERE id = ?').get(categoryId)
  if (!cat) return res.status(404).json({ error: 'Catégorie introuvable' })

  const { title, content, published } = req.body
  const titleTrimmed = title?.trim() || ''
  const slug = slugify(titleTrimmed) || `article-${Date.now()}`

  const result = db.prepare(
    'INSERT INTO wiki_articles (category_id, title, slug, content, published) VALUES (?, ?, ?, ?, ?)'
  ).run(categoryId, titleTrimmed, slug, content?.trim() || '', published ? 1 : 0)

  const article = db.prepare('SELECT * FROM wiki_articles WHERE id = ?').get(result.lastInsertRowid)
  logHistory(article.id, article.title, req.user?.display_name || 'Inconnu', 'created', null, {
    title: article.title, content: article.content, published: article.published,
  })
  res.json(article)
})

router.put('/articles/:id', requireAuth, (req, res) => {
  const old = db.prepare('SELECT * FROM wiki_articles WHERE id = ?').get(req.params.id)
  if (!old) return res.status(404).json({ error: 'Not found' })

  const { title, content, published } = req.body
  const newTitle     = title?.trim()   ?? old.title
  const newContent   = content?.trim() ?? old.content
  const newPublished = published !== undefined ? (published ? 1 : 0) : old.published
  const newSlug      = title !== undefined ? (slugify(newTitle) || old.slug) : old.slug

  db.prepare(
    "UPDATE wiki_articles SET title = ?, slug = ?, content = ?, published = ?, updated_at = datetime('now') WHERE id = ?"
  ).run(newTitle, newSlug, newContent, newPublished, req.params.id)

  const updated = db.prepare('SELECT * FROM wiki_articles WHERE id = ?').get(req.params.id)
  logHistory(updated.id, updated.title, req.user?.display_name || 'Inconnu', 'updated',
    { title: old.title, content: old.content, published: old.published },
    { title: updated.title, content: updated.content, published: updated.published },
  )
  res.json(updated)
})

// POST /api/wiki/articles/:id/toggle — basculer publié/brouillon
router.post('/articles/:id/toggle', requireAuth, (req, res) => {
  const old = db.prepare('SELECT * FROM wiki_articles WHERE id = ?').get(req.params.id)
  if (!old) return res.status(404).json({ error: 'Not found' })

  const newPub = old.published ? 0 : 1
  db.prepare("UPDATE wiki_articles SET published = ?, updated_at = datetime('now') WHERE id = ?")
    .run(newPub, req.params.id)

  const updated = db.prepare('SELECT * FROM wiki_articles WHERE id = ?').get(req.params.id)
  logHistory(updated.id, updated.title, req.user?.display_name || 'Inconnu', 'updated',
    { title: old.title, published: old.published },
    { title: updated.title, published: updated.published },
  )
  res.json(updated)
})

router.delete('/articles/:id', requireAuth, (req, res) => {
  const article = db.prepare('SELECT * FROM wiki_articles WHERE id = ?').get(req.params.id)
  if (!article) return res.status(404).json({ error: 'Not found' })

  logHistory(article.id, article.title, req.user?.display_name || 'Inconnu', 'deleted',
    { title: article.title, content: article.content }, null)

  db.prepare('DELETE FROM wiki_articles WHERE id = ?').run(req.params.id)
  res.json({ ok: true })
})

// ── Historique ────────────────────────────────────────────────────────────

router.get('/history', requireAuth, (req, res) => {
  const limit = Math.min(parseInt(req.query.limit) || 50, 200)
  res.json(db.prepare('SELECT * FROM wiki_article_history ORDER BY changed_at DESC LIMIT ?').all(limit))
})

router.get('/articles/:id/history', requireAuth, (req, res) => {
  res.json(
    db.prepare('SELECT * FROM wiki_article_history WHERE article_id = ? ORDER BY changed_at DESC').all(req.params.id)
  )
})

export default router
