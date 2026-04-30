import express from 'express'
import multer from 'multer'
import db from '../db.js'
import { requireAuth } from '../auth.js'

const router = express.Router()
const upload = multer({ limits: { fileSize: 5 * 1024 * 1024 } })

// ── Helpers ───────────────────────────────────────────────────────────────

function slugify(text) {
  return String(text || '')
    .normalize('NFD').replace(/[̀-ͯ]/g, '')
    .toLowerCase().trim()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
}

function logHistory(pageId, pageTitle, userName, action, oldData, newData) {
  db.prepare(`
    INSERT INTO docs_page_history (page_id, page_title, user_name, action, old_data, new_data)
    VALUES (?, ?, ?, ?, ?, ?)
  `).run(
    pageId, pageTitle, userName, action,
    oldData ? JSON.stringify(oldData) : null,
    newData ? JSON.stringify(newData) : null,
  )
}

function getPage(id) {
  return db.prepare('SELECT * FROM docs_pages WHERE id = ?').get(id)
}

// Renvoie tous les descendants d'une page (inclus), en DFS
function collectSubtree(rootId) {
  const all = db.prepare('SELECT * FROM docs_pages ORDER BY parent_id, position, id').all()
  const byParent = new Map()
  for (const p of all) {
    const k = p.parent_id ?? 0
    if (!byParent.has(k)) byParent.set(k, [])
    byParent.get(k).push(p)
  }
  const result = []
  function walk(id, depth) {
    const page = all.find(p => p.id === id)
    if (!page) return
    result.push({ ...page, _depth: depth })
    const children = byParent.get(id) || []
    for (const c of children) walk(c.id, depth + 1)
  }
  walk(rootId, 0)
  return result
}

// Détecte si "candidateParent" est un descendant de "id" (pour empêcher les cycles)
function isDescendantOf(id, candidateParent) {
  if (candidateParent == null) return false
  const all = db.prepare('SELECT id, parent_id FROM docs_pages').all()
  const parentById = new Map(all.map(p => [p.id, p.parent_id]))
  let cur = candidateParent
  while (cur != null) {
    if (cur === id) return true
    cur = parentById.get(cur) ?? null
  }
  return false
}

function buildTree() {
  const rows = db.prepare(
    'SELECT id, parent_id, title, slug, icon, position, published, updated_at FROM docs_pages ORDER BY parent_id, position, id'
  ).all()
  const byId = new Map(rows.map(r => [r.id, { ...r, children: [] }]))
  const roots = []
  for (const r of rows) {
    const node = byId.get(r.id)
    if (r.parent_id && byId.has(r.parent_id)) {
      byId.get(r.parent_id).children.push(node)
    } else {
      roots.push(node)
    }
  }
  return roots
}

function buildBreadcrumb(id) {
  const crumb = []
  let cur = getPage(id)
  while (cur) {
    crumb.unshift({ id: cur.id, title: cur.title, slug: cur.slug })
    if (!cur.parent_id) break
    cur = getPage(cur.parent_id)
  }
  return crumb
}

// ── Lecture ───────────────────────────────────────────────────────────────

// GET /api/docs/tree — arbre complet (sans contenu)
router.get('/tree', requireAuth, (_req, res) => {
  res.json(buildTree())
})

// GET /api/docs/search?q=keyword
router.get('/search', requireAuth, (req, res) => {
  const q = (req.query.q || '').trim()
  if (!q) return res.json([])
  const like = `%${q}%`
  const rows = db.prepare(`
    SELECT id, parent_id, title, slug, icon, published, updated_at
    FROM docs_pages
    WHERE title LIKE ? OR content LIKE ?
    ORDER BY (title LIKE ?) DESC, updated_at DESC
    LIMIT 40
  `).all(like, like, like)
  res.json(rows.map(r => ({ ...r, breadcrumb: buildBreadcrumb(r.id) })))
})

// GET /api/docs/:id
router.get('/:id', requireAuth, (req, res) => {
  const page = getPage(req.params.id)
  if (!page) return res.status(404).json({ error: 'Page introuvable' })
  res.json({ ...page, breadcrumb: buildBreadcrumb(page.id) })
})

// ── Écriture ──────────────────────────────────────────────────────────────

// POST /api/docs — créer une page
router.post('/', requireAuth, (req, res) => {
  const { parent_id = null, title = '', content = '', icon = '', published = 0 } = req.body || {}
  const cleanParent = parent_id ? Number(parent_id) : null
  if (cleanParent && !getPage(cleanParent)) {
    return res.status(400).json({ error: 'parent_id invalide' })
  }
  const siblings = db.prepare(
    'SELECT COUNT(*) AS n FROM docs_pages WHERE parent_id IS ?'
  ).get(cleanParent).n
  const slug = slugify(title) || `page-${Date.now()}`

  const result = db.prepare(
    `INSERT INTO docs_pages (parent_id, title, slug, icon, content, position, published)
     VALUES (?, ?, ?, ?, ?, ?, ?)`
  ).run(cleanParent, title.trim(), slug, icon, content, siblings, published ? 1 : 0)

  const page = getPage(result.lastInsertRowid)
  logHistory(page.id, page.title, req.user?.display_name || 'Inconnu', 'created', null, {
    title: page.title, parent_id: page.parent_id, published: page.published,
  })
  res.json(page)
})

// PUT /api/docs/:id — mettre à jour titre/contenu/icône/published
router.put('/:id', requireAuth, (req, res) => {
  const old = getPage(req.params.id)
  if (!old) return res.status(404).json({ error: 'Not found' })

  const { title, content, icon, published } = req.body || {}
  const newTitle     = title   !== undefined ? String(title).trim()   : old.title
  const newContent   = content !== undefined ? String(content)         : old.content
  const newIcon      = icon    !== undefined ? String(icon)            : old.icon
  const newPublished = published !== undefined ? (published ? 1 : 0)    : old.published
  const newSlug      = title   !== undefined ? (slugify(newTitle) || old.slug) : old.slug

  db.prepare(
    `UPDATE docs_pages
     SET title = ?, slug = ?, icon = ?, content = ?, published = ?, updated_at = datetime('now')
     WHERE id = ?`
  ).run(newTitle, newSlug, newIcon, newContent, newPublished, req.params.id)

  const updated = getPage(req.params.id)
  logHistory(updated.id, updated.title, req.user?.display_name || 'Inconnu', 'updated',
    { title: old.title, content: old.content, icon: old.icon, published: old.published },
    { title: updated.title, content: updated.content, icon: updated.icon, published: updated.published },
  )
  res.json(updated)
})

// POST /api/docs/:id/move — changer de parent et/ou position
router.post('/:id/move', requireAuth, (req, res) => {
  const id = Number(req.params.id)
  const page = getPage(id)
  if (!page) return res.status(404).json({ error: 'Not found' })

  const { parent_id = null, position = 0 } = req.body || {}
  const newParent = parent_id == null ? null : Number(parent_id)

  if (newParent != null && !getPage(newParent)) {
    return res.status(400).json({ error: 'parent_id invalide' })
  }
  if (newParent === id) {
    return res.status(400).json({ error: 'Une page ne peut pas être son propre parent' })
  }
  if (isDescendantOf(id, newParent)) {
    return res.status(400).json({ error: 'Déplacement interdit (cycle dans l\'arbre)' })
  }

  const siblings = db.prepare(
    'SELECT id FROM docs_pages WHERE parent_id IS ? AND id != ? ORDER BY position, id'
  ).all(newParent, id)
  const pos = Math.max(0, Math.min(Number(position) || 0, siblings.length))

  db.transaction(() => {
    db.prepare('UPDATE docs_pages SET parent_id = ?, position = ? WHERE id = ?')
      .run(newParent, pos, id)
    // Renumérote les frères (y compris la page déplacée insérée à `pos`)
    const ordered = [...siblings.slice(0, pos), { id }, ...siblings.slice(pos)]
    const upd = db.prepare('UPDATE docs_pages SET position = ? WHERE id = ?')
    ordered.forEach((s, i) => upd.run(i, s.id))
  })()

  const updated = getPage(id)
  logHistory(updated.id, updated.title, req.user?.display_name || 'Inconnu', 'moved',
    { parent_id: page.parent_id, position: page.position },
    { parent_id: updated.parent_id, position: updated.position },
  )
  res.json(updated)
})

// POST /api/docs/:id/toggle — publié / brouillon
router.post('/:id/toggle', requireAuth, (req, res) => {
  const old = getPage(req.params.id)
  if (!old) return res.status(404).json({ error: 'Not found' })
  const newPub = old.published ? 0 : 1
  db.prepare("UPDATE docs_pages SET published = ?, updated_at = datetime('now') WHERE id = ?")
    .run(newPub, req.params.id)
  const updated = getPage(req.params.id)
  logHistory(updated.id, updated.title, req.user?.display_name || 'Inconnu', 'updated',
    { published: old.published }, { published: updated.published })
  res.json(updated)
})

// DELETE /api/docs/:id — supprime la page + descendants (ON DELETE CASCADE)
router.delete('/:id', requireAuth, (req, res) => {
  const page = getPage(req.params.id)
  if (!page) return res.status(404).json({ error: 'Not found' })
  logHistory(page.id, page.title, req.user?.display_name || 'Inconnu', 'deleted',
    { title: page.title, content: page.content }, null)
  db.prepare('DELETE FROM docs_pages WHERE id = ?').run(req.params.id)
  res.json({ ok: true })
})

// ── Export markdown ────────────────────────────────────────────────────────

// GET /api/docs/:id/export?deep=1 — renvoie le MD d'une page ou du sous-arbre
router.get('/:id/export', requireAuth, (req, res) => {
  const id = Number(req.params.id)
  const page = getPage(id)
  if (!page) return res.status(404).json({ error: 'Not found' })
  const deep = req.query.deep === '1' || req.query.deep === 'true'

  let md
  if (!deep) {
    md = `# ${page.title || 'Sans titre'}\n\n${(page.content || '').trim()}\n`
  } else {
    const nodes = collectSubtree(id)
    md = nodes.map(n => {
      const level = Math.min(n._depth + 1, 6) // #..######
      const h = '#'.repeat(level)
      const body = (n.content || '').trim()
      return `${h} ${n.title || 'Sans titre'}\n\n${body}${body ? '\n' : ''}`
    }).join('\n')
  }

  const filename = `${page.slug || 'doc'}${deep ? '-tree' : ''}.md`
  res.setHeader('Content-Type', 'text/markdown; charset=utf-8')
  res.setHeader('Content-Disposition', `attachment; filename="${filename}"`)
  res.send(md)
})

// ── Import markdown ───────────────────────────────────────────────────────

// Parse un markdown en arbre de pages à partir des headings #..######.
// Les lignes entre deux headings deviennent le contenu du dernier heading ouvert.
function parseMdToTree(text) {
  const lines = text.split('\n')
  const root = { title: null, level: 0, content: '', children: [] }
  const stack = [root]

  for (const line of lines) {
    const m = line.match(/^(#{1,6})\s+(.+?)\s*$/)
    if (m) {
      const level = m[1].length
      const title = m[2].trim()
      while (stack.length > 0 && stack[stack.length - 1].level >= level) stack.pop()
      const parent = stack[stack.length - 1] || root
      const node = { title, level, content: '', children: [] }
      parent.children.push(node)
      stack.push(node)
    } else {
      const target = stack[stack.length - 1]
      if (target) target.content += line + '\n'
    }
  }
  // Normalise le contenu (trim)
  function trimContent(n) {
    n.content = (n.content || '').replace(/^\s+|\s+$/g, '')
    for (const c of n.children) trimContent(c)
  }
  trimContent(root)
  return root
}

// POST /api/docs/import — body: { parent_id?, mode?: 'append'|'replace', markdown? } ou multipart file
// Crée les pages correspondantes sous `parent_id` (ou à la racine si null).
router.post('/import', requireAuth, upload.single('file'), async (req, res) => {
  try {
    let markdown = ''
    if (req.file) {
      markdown = req.file.buffer.toString('utf-8')
    } else if (req.body?.markdown) {
      markdown = String(req.body.markdown)
    }
    if (!markdown.trim()) return res.status(400).json({ error: 'Markdown vide' })

    const parentId = req.body?.parent_id ? Number(req.body.parent_id) : null
    const mode = (req.body?.mode === 'replace') ? 'replace' : 'append'

    if (parentId && !getPage(parentId)) {
      return res.status(400).json({ error: 'parent_id invalide' })
    }

    const tree = parseMdToTree(markdown)
    if (tree.children.length === 0) {
      return res.status(400).json({ error: 'Aucun heading # trouvé dans le markdown' })
    }

    const insertPage = db.prepare(
      `INSERT INTO docs_pages (parent_id, title, slug, content, position, published)
       VALUES (?, ?, ?, ?, ?, 1)`
    )

    let created = 0
    db.transaction(() => {
      // Mode replace : supprime tous les enfants directs (cascade sur descendants)
      if (mode === 'replace') {
        const kids = db.prepare('SELECT id FROM docs_pages WHERE parent_id IS ?').all(parentId)
        const del = db.prepare('DELETE FROM docs_pages WHERE id = ?')
        for (const k of kids) del.run(k.id)
      }

      const startPos = db.prepare(
        'SELECT COUNT(*) AS n FROM docs_pages WHERE parent_id IS ?'
      ).get(parentId).n

      function insertSubtree(nodes, parent, basePos) {
        for (let i = 0; i < nodes.length; i++) {
          const node = nodes[i]
          const slug = slugify(node.title) || `page-${Date.now()}-${i}`
          const result = insertPage.run(parent, node.title, slug, node.content || '', basePos + i)
          const pid = result.lastInsertRowid
          created++
          if (node.children.length > 0) insertSubtree(node.children, pid, 0)
        }
      }
      insertSubtree(tree.children, parentId, startPos)
    })()

    res.json({ ok: true, created, mode, parent_id: parentId })
  } catch (e) {
    res.status(500).json({ error: e.message })
  }
})

// ── Historique ────────────────────────────────────────────────────────────

router.get('/:id/history', requireAuth, (req, res) => {
  res.json(
    db.prepare('SELECT * FROM docs_page_history WHERE page_id = ? ORDER BY changed_at DESC')
      .all(req.params.id)
  )
})

export default router
