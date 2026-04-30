import express from 'express'
import multer from 'multer'
import sharp from 'sharp'
import { unlinkSync, existsSync, mkdirSync, statSync } from 'fs'
import { join, dirname } from 'path'
import { fileURLToPath } from 'url'
import db from '../db.js'
import { requireAuth } from '../auth.js'

const __dirname = dirname(fileURLToPath(import.meta.url))
const UPLOAD_DIR = join(__dirname, '../../uploads')
mkdirSync(UPLOAD_DIR, { recursive: true })

// 50 MB max — encode in WebP via sharp before persisting
const ANIMATED_TYPES = new Set(['image/gif', 'image/webp'])
const SVG_TYPE = 'image/svg+xml'
const PASSTHROUGH_TYPES = new Set([SVG_TYPE])

const storage = multer.memoryStorage()
const upload = multer({
  storage,
  limits: { fileSize: 50 * 1024 * 1024 },
  fileFilter: (_req, file, cb) => {
    if (file.mimetype.startsWith('image/')) cb(null, true)
    else cb(new Error('Only image files are allowed'))
  },
})

function randomSlug(len = 8) {
  const chars = 'abcdefghijklmnopqrstuvwxyz0123456789'
  let s = ''
  for (let i = 0; i < len; i++) s += chars[Math.floor(Math.random() * chars.length)]
  return s
}

const router = express.Router()

// GET /api/images — list all images (admin)
router.get('/', requireAuth, (_req, res) => {
  const images = db.prepare('SELECT * FROM images ORDER BY created_at DESC').all()
  res.json(images)
})

// GET /api/images/:slug — get one image (public, used by share page)
router.get('/:slug', (req, res) => {
  const image = db.prepare('SELECT * FROM images WHERE slug = ?').get(req.params.slug)
  if (!image) return res.status(404).json({ error: 'Not found' })
  res.json(image)
})

// POST /api/images — upload image (re-encodes to WebP via sharp)
router.post('/', requireAuth, upload.single('file'), async (req, res) => {
  if (!req.file) return res.status(400).json({ error: 'No file uploaded' })

  const title = req.body.title?.trim() || req.file.originalname.replace(/\.[^.]+$/, '')

  let slug
  let attempts = 0
  do {
    slug = randomSlug(8)
    attempts++
  } while (db.prepare('SELECT id FROM images WHERE slug = ?').get(slug) && attempts < 20)

  try {
    let filename, mime, size, width = 0, height = 0

    if (PASSTHROUGH_TYPES.has(req.file.mimetype)) {
      // SVG : pas de ré-encodage, on garde tel quel
      filename = `img_${Date.now()}_${slug}.svg`
      mime = SVG_TYPE
      const { writeFile } = await import('fs/promises')
      await writeFile(join(UPLOAD_DIR, filename), req.file.buffer)
      size = req.file.size
    } else {
      filename = `img_${Date.now()}_${slug}.webp`
      mime = 'image/webp'
      const outputPath = join(UPLOAD_DIR, filename)
      const isAnimated = ANIMATED_TYPES.has(req.file.mimetype)

      const image = sharp(req.file.buffer, { animated: isAnimated })
      const meta = await image.metadata()

      const pipeline = meta.width > 2400
        ? image.resize({ width: 2400, withoutEnlargement: true })
        : image

      await pipeline
        .webp({ quality: 82, effort: 4, lossless: false })
        .toFile(outputPath)

      try {
        size = statSync(outputPath).size
        const outMeta = await sharp(outputPath).metadata()
        width = outMeta.width || 0
        height = outMeta.height || 0
      } catch (_) { size = req.file.size }
    }

    db.prepare(`
      INSERT INTO images (slug, title, filename, size, mime, width, height)
      VALUES (?, ?, ?, ?, ?, ?, ?)
    `).run(slug, title, filename, size, mime, width, height)

    res.json({ slug, title, filename, size, mime, width, height })
  } catch (err) {
    console.error('[images] sharp error:', err)
    res.status(500).json({ error: 'Image optimisation failed' })
  }
})

// PATCH /api/images/:slug — rename title
router.patch('/:slug', requireAuth, (req, res) => {
  const { title } = req.body
  if (!title?.trim()) return res.status(400).json({ error: 'Title required' })
  const result = db.prepare('UPDATE images SET title = ? WHERE slug = ?').run(title.trim(), req.params.slug)
  if (result.changes === 0) return res.status(404).json({ error: 'Not found' })
  res.json({ ok: true })
})

// DELETE /api/images/:slug — delete image + file
router.delete('/:slug', requireAuth, (req, res) => {
  const image = db.prepare('SELECT * FROM images WHERE slug = ?').get(req.params.slug)
  if (!image) return res.status(404).json({ error: 'Not found' })

  const filePath = join(UPLOAD_DIR, image.filename)
  if (existsSync(filePath)) {
    try { unlinkSync(filePath) } catch (_) {}
  }

  db.prepare('DELETE FROM images WHERE slug = ?').run(req.params.slug)
  res.json({ ok: true })
})

export default router
