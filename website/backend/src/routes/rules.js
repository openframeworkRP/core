import express from 'express'
import db from '../db.js'
import { requireAuth, requireRulesAccess } from '../auth.js'

const router = express.Router()

function logHistory(ruleId, ruleTitle, userName, action, oldData, newData) {
  db.prepare(`
    INSERT INTO rule_history (rule_id, rule_title, user_name, action, old_data, new_data)
    VALUES (?, ?, ?, ?, ?, ?)
  `).run(
    ruleId,
    ruleTitle,
    userName,
    action,
    oldData ? JSON.stringify(oldData) : null,
    newData ? JSON.stringify(newData) : null,
  )
}

// GET /api/rules — all categories + rules grouped by type
router.get('/', requireAuth, (_req, res) => {
  const categories = db.prepare('SELECT * FROM rule_categories ORDER BY order_index, created_at').all()
  const rules = db.prepare('SELECT * FROM rules ORDER BY order_index, created_at').all()

  const rulesByCategory = {}
  for (const rule of rules) {
    if (!rulesByCategory[rule.category_id]) rulesByCategory[rule.category_id] = []
    rulesByCategory[rule.category_id].push(rule)
  }

  const result = { server: [], job: [], theme: [] }
  for (const cat of categories) {
    if (result[cat.type]) {
      result[cat.type].push({ ...cat, rules: rulesByCategory[cat.id] || [] })
    }
  }
  res.json(result)
})

// POST /api/rules/categories — create category
router.post('/categories', requireAuth, (req, res) => {
  const { type, name, color } = req.body
  if (!type || !['server', 'job', 'theme'].includes(type))
    return res.status(400).json({ error: 'Type invalide' })
  if (!name?.trim())
    return res.status(400).json({ error: 'Nom requis' })

  const result = db.prepare(
    'INSERT INTO rule_categories (type, name, color) VALUES (?, ?, ?)'
  ).run(type, name.trim(), color || '#5865f2')

  res.json(db.prepare('SELECT * FROM rule_categories WHERE id = ?').get(result.lastInsertRowid))
})

// PUT /api/rules/categories/:id — update category
router.put('/categories/:id', requireAuth, (req, res) => {
  const { name, color } = req.body
  if (!name?.trim()) return res.status(400).json({ error: 'Nom requis' })

  const result = db.prepare(
    'UPDATE rule_categories SET name = ?, color = ? WHERE id = ?'
  ).run(name.trim(), color || '#5865f2', req.params.id)

  if (result.changes === 0) return res.status(404).json({ error: 'Not found' })
  res.json(db.prepare('SELECT * FROM rule_categories WHERE id = ?').get(req.params.id))
})

// DELETE /api/rules/categories/:id — cascade-deletes its rules
router.delete('/categories/:id', requireAuth, (req, res) => {
  const result = db.prepare('DELETE FROM rule_categories WHERE id = ?').run(req.params.id)
  if (result.changes === 0) return res.status(404).json({ error: 'Not found' })
  res.json({ ok: true })
})

// POST /api/rules/categories/:categoryId/rules — create rule
router.post('/categories/:categoryId/rules', requireAuth, (req, res) => {
  const { categoryId } = req.params
  const cat = db.prepare('SELECT id FROM rule_categories WHERE id = ?').get(categoryId)
  if (!cat) return res.status(404).json({ error: 'Catégorie introuvable' })

  const { title, content } = req.body
  const result = db.prepare(
    'INSERT INTO rules (category_id, title, content) VALUES (?, ?, ?)'
  ).run(categoryId, title?.trim() || '', content?.trim() || '')

  const rule = db.prepare('SELECT * FROM rules WHERE id = ?').get(result.lastInsertRowid)
  logHistory(rule.id, rule.title, req.user?.display_name || 'Inconnu', 'created', null, {
    title: rule.title, content: rule.content, category_id: rule.category_id,
  })
  res.json(rule)
})

// GET /api/rules/history — recent changes across all rules
router.get('/history', requireAuth, (req, res) => {
  const limit = Math.min(parseInt(req.query.limit) || 50, 200)
  res.json(db.prepare('SELECT * FROM rule_history ORDER BY changed_at DESC LIMIT ?').all(limit))
})

// GET /api/rules/:id/history — history for one rule
router.get('/:id/history', requireAuth, (req, res) => {
  res.json(
    db.prepare('SELECT * FROM rule_history WHERE rule_id = ? ORDER BY changed_at DESC').all(req.params.id)
  )
})

// PUT /api/rules/:id — update rule title/content
router.put('/:id', requireAuth, (req, res) => {
  const old = db.prepare('SELECT * FROM rules WHERE id = ?').get(req.params.id)
  if (!old) return res.status(404).json({ error: 'Not found' })

  const { title, content } = req.body
  const newTitle   = title?.trim()   ?? old.title
  const newContent = content?.trim() ?? old.content

  db.prepare("UPDATE rules SET title = ?, content = ?, updated_at = datetime('now') WHERE id = ?")
    .run(newTitle, newContent, req.params.id)

  const updated = db.prepare('SELECT * FROM rules WHERE id = ?').get(req.params.id)
  logHistory(updated.id, updated.title, req.user?.display_name || 'Inconnu', 'updated',
    { title: old.title, content: old.content },
    { title: updated.title, content: updated.content },
  )
  res.json(updated)
})

// DELETE /api/rules/:id
router.delete('/:id', requireAuth, (req, res) => {
  const rule = db.prepare('SELECT * FROM rules WHERE id = ?').get(req.params.id)
  if (!rule) return res.status(404).json({ error: 'Not found' })

  logHistory(rule.id, rule.title, req.user?.display_name || 'Inconnu', 'deleted',
    { title: rule.title, content: rule.content }, null)

  db.prepare('DELETE FROM rules WHERE id = ?').run(req.params.id)
  res.json({ ok: true })
})

// ── OpenFramework Books API ───────────────────────────────────────────────────
// Accessible par owner, admin, editor, rules_editor

// GET /api/rules/sl-books — tous les livres avec leurs chapitres (admin)
router.get('/sl-books', requireRulesAccess, (_req, res) => {
  const books = db.prepare('SELECT * FROM sl_books ORDER BY order_index, id').all()
  const result = []
  for (const book of books) {
    const chapters = db.prepare('SELECT * FROM sl_chapters WHERE book_id = ? ORDER BY order_index, id').all(book.book_id)
    result.push({
      ...book,
      chapters: chapters.map(ch => ({ ...ch, content: ch.content || '' })),
    })
  }
  res.json(result)
})

// GET /api/rules/sl-books/public — idem mais sans auth (pour le frontend public)
router.get('/sl-books/public', (_req, res) => {
  const books = db.prepare('SELECT * FROM sl_books ORDER BY order_index, id').all()
  const result = []
  for (const book of books) {
    const chapters = db.prepare('SELECT * FROM sl_chapters WHERE book_id = ? ORDER BY order_index, id').all(book.book_id)
    result.push({
      id: book.book_id,
      title: book.title,
      icon: book.icon,
      cover_color: book.cover_color,
      cover_accent: book.cover_accent,
      chapters: chapters.map(ch => ({
        id: ch.chapter_id,
        title: ch.title,
        content: ch.content || '',  // MD brut — parsé côté client
      })),
    })
  }
  res.json(result)
})

// POST /api/rules/sl-books — créer un livre
router.post('/sl-books', requireRulesAccess, (req, res) => {
  const { book_id, title, icon, cover_color, cover_accent } = req.body
  if (!book_id?.trim() || !title?.trim()) return res.status(400).json({ error: 'book_id et title requis' })
  try {
    const maxOrder = db.prepare('SELECT MAX(order_index) as m FROM sl_books').get().m ?? -1
    db.prepare(`INSERT INTO sl_books (book_id, title, icon, cover_color, cover_accent, order_index) VALUES (?, ?, ?, ?, ?, ?)`)
      .run(book_id.trim(), title.trim(), icon || '📖', cover_color || '#1a0a00', cover_accent || '#D4A574', maxOrder + 1)
    const book = db.prepare('SELECT * FROM sl_books WHERE book_id = ?').get(book_id.trim())
    res.json({ ...book, chapters: [] })
  } catch { res.status(409).json({ error: 'book_id déjà utilisé' }) }
})

// PUT /api/rules/sl-books/:bookId — mettre à jour les métadonnées d'un livre
router.put('/sl-books/:bookId', requireRulesAccess, (req, res) => {
  const { title, icon, cover_color, cover_accent } = req.body
  const result = db.prepare(`UPDATE sl_books SET title = COALESCE(?, title), icon = COALESCE(?, icon), cover_color = COALESCE(?, cover_color), cover_accent = COALESCE(?, cover_accent), updated_at = datetime('now') WHERE book_id = ?`)
    .run(title || null, icon || null, cover_color || null, cover_accent || null, req.params.bookId)
  if (result.changes === 0) return res.status(404).json({ error: 'Livre introuvable' })
  res.json(db.prepare('SELECT * FROM sl_books WHERE book_id = ?').get(req.params.bookId))
})

// POST /api/rules/sl-books/:bookId/chapters — ajouter un chapitre
router.post('/sl-books/:bookId/chapters', requireRulesAccess, (req, res) => {
  const book = db.prepare('SELECT * FROM sl_books WHERE book_id = ?').get(req.params.bookId)
  if (!book) return res.status(404).json({ error: 'Livre introuvable' })
  const { chapter_id, title, content } = req.body
  if (!chapter_id?.trim() || !title?.trim()) return res.status(400).json({ error: 'chapter_id et title requis' })
  const maxOrder = db.prepare('SELECT MAX(order_index) as m FROM sl_chapters WHERE book_id = ?').get(req.params.bookId).m ?? -1
  try {
    const result = db.prepare(`INSERT INTO sl_chapters (book_id, chapter_id, title, content, order_index) VALUES (?, ?, ?, ?, ?)`)
      .run(req.params.bookId, chapter_id.trim(), title.trim(), content || '', maxOrder + 1)
    const ch = db.prepare('SELECT * FROM sl_chapters WHERE id = ?').get(result.lastInsertRowid)
    res.json({ ...ch, content: ch.content || '' })
  } catch { res.status(409).json({ error: 'chapter_id déjà utilisé dans ce livre' }) }
})

// PUT /api/rules/sl-books/:bookId/chapters/:chapterId — modifier un chapitre (titre + contenu MD)
router.put('/sl-books/:bookId/chapters/:chapterId', requireRulesAccess, (req, res) => {
  const { title, content, order_index } = req.body
  const result = db.prepare(`
    UPDATE sl_chapters
    SET title       = COALESCE(?, title),
        content     = COALESCE(?, content),
        order_index = COALESCE(?, order_index),
        updated_at  = datetime('now')
    WHERE book_id = ? AND chapter_id = ?
  `).run(title ?? null, content ?? null, order_index ?? null, req.params.bookId, req.params.chapterId)
  if (result.changes === 0) return res.status(404).json({ error: 'Chapitre introuvable' })
  res.json(db.prepare('SELECT * FROM sl_chapters WHERE book_id = ? AND chapter_id = ?').get(req.params.bookId, req.params.chapterId))
})

// DELETE /api/rules/sl-books/:bookId/chapters/:chapterId
router.delete('/sl-books/:bookId/chapters/:chapterId', requireRulesAccess, (req, res) => {
  const result = db.prepare('DELETE FROM sl_chapters WHERE book_id = ? AND chapter_id = ?')
    .run(req.params.bookId, req.params.chapterId)
  if (result.changes === 0) return res.status(404).json({ error: 'Chapitre introuvable' })
  res.json({ ok: true })
})

// POST /api/rules/sl-chapters/:chId/blocks — ajouter un bloc
router.post('/sl-chapters/:chId/blocks', requireRulesAccess, (req, res) => {
  const ch = db.prepare('SELECT * FROM sl_chapters WHERE id = ?').get(req.params.chId)
  if (!ch) return res.status(404).json({ error: 'Chapitre introuvable' })
  const { type, data } = req.body
  const VALID_TYPES = ['heading', 'paragraph', 'note', 'list', 'rule']
  if (!VALID_TYPES.includes(type)) return res.status(400).json({ error: 'Type invalide' })
  const maxOrder = db.prepare('SELECT MAX(order_index) as m FROM sl_blocks WHERE chapter_id = ?').get(req.params.chId).m ?? -1
  const result = db.prepare(`INSERT INTO sl_blocks (chapter_id, type, data, order_index) VALUES (?, ?, ?, ?)`)
    .run(req.params.chId, type, JSON.stringify(data || {}), maxOrder + 1)
  const block = db.prepare('SELECT * FROM sl_blocks WHERE id = ?').get(result.lastInsertRowid)
  res.json({ ...block, data: JSON.parse(block.data) })
})

// PUT /api/rules/sl-blocks/:id — modifier un bloc
router.put('/sl-blocks/:id', requireRulesAccess, (req, res) => {
  const { type, data, order_index } = req.body
  const VALID_TYPES = ['heading', 'paragraph', 'note', 'list', 'rule']
  if (type && !VALID_TYPES.includes(type)) return res.status(400).json({ error: 'Type invalide' })
  const result = db.prepare(`UPDATE sl_blocks SET type = COALESCE(?, type), data = COALESCE(?, data), order_index = COALESCE(?, order_index), updated_at = datetime('now') WHERE id = ?`)
    .run(type || null, data ? JSON.stringify(data) : null, order_index ?? null, req.params.id)
  if (result.changes === 0) return res.status(404).json({ error: 'Bloc introuvable' })
  const block = db.prepare('SELECT * FROM sl_blocks WHERE id = ?').get(req.params.id)
  res.json({ ...block, data: JSON.parse(block.data) })
})

// DELETE /api/rules/sl-blocks/:id
router.delete('/sl-blocks/:id', requireRulesAccess, (req, res) => {
  const result = db.prepare('DELETE FROM sl_blocks WHERE id = ?').run(req.params.id)
  if (result.changes === 0) return res.status(404).json({ error: 'Bloc introuvable' })
  res.json({ ok: true })
})

// PUT /api/rules/sl-chapters/:chId/blocks/reorder — réordonner les blocs
router.put('/sl-chapters/:chId/blocks/reorder', requireRulesAccess, (req, res) => {
  const { order } = req.body // array of block ids in new order
  if (!Array.isArray(order)) return res.status(400).json({ error: 'order doit être un tableau' })
  const update = db.prepare('UPDATE sl_blocks SET order_index = ? WHERE id = ? AND chapter_id = ?')
  db.transaction(() => {
    order.forEach((id, idx) => update.run(idx, id, req.params.chId))
  })()
  res.json({ ok: true })
})

export default router
