import express from 'express'
import multer from 'multer'
import { unlinkSync, existsSync, mkdirSync, statSync } from 'fs'
import { join, extname, dirname } from 'path'
import { fileURLToPath } from 'url'
import { spawn } from 'child_process'
import db from '../db.js'
import { requireAuth } from '../auth.js'

const __dirname = dirname(fileURLToPath(import.meta.url))
const UPLOAD_DIR = join(__dirname, '../../uploads')
mkdirSync(UPLOAD_DIR, { recursive: true })

// Separate multer config for videos — 2 GB limit
const storage = multer.diskStorage({
  destination: (_req, _file, cb) => cb(null, UPLOAD_DIR),
  filename: (_req, file, cb) => {
    const ext = extname(file.originalname)
    cb(null, `${Date.now()}-${Math.random().toString(36).slice(2)}${ext}`)
  },
})
const upload = multer({
  storage,
  limits: { fileSize: 2 * 1024 * 1024 * 1024 }, // 2 GB
  fileFilter: (_req, file, cb) => {
    if (file.mimetype.startsWith('video/')) cb(null, true)
    else cb(new Error('Only video files are allowed'))
  },
})

function randomSlug(len = 8) {
  const chars = 'abcdefghijklmnopqrstuvwxyz0123456789'
  let s = ''
  for (let i = 0; i < len; i++) s += chars[Math.floor(Math.random() * chars.length)]
  return s
}

// In-memory transcode progress: slug → { pct: 0..100 }
const transcodeProgress = new Map()

function parseTimeSecs(str) {
  const [h, m, s] = str.split(':').map(parseFloat)
  return h * 3600 + m * 60 + s
}

const router = express.Router()

// GET /api/videos/:slug/progress — transcode progress (0–100)
router.get('/:slug/progress', (req, res) => {
  const { slug } = req.params
  const p = transcodeProgress.get(slug)
  if (p === undefined) {
    // Not transcoding — check DB status
    const video = db.prepare('SELECT status FROM videos WHERE slug = ?').get(slug)
    if (!video) return res.status(404).json({ error: 'Not found' })
    return res.json({ pct: video.status === 'ready' ? 100 : 0 })
  }
  res.json({ pct: p.pct })
})

// GET /api/videos — list all videos (admin)
router.get('/', requireAuth, (_req, res) => {
  const videos = db.prepare('SELECT * FROM videos ORDER BY created_at DESC').all()
  res.json(videos)
})

// GET /api/videos/:slug — get one video (public, used by video page)
router.get('/:slug', (req, res) => {
  const video = db.prepare('SELECT * FROM videos WHERE slug = ?').get(req.params.slug)
  if (!video) return res.status(404).json({ error: 'Not found' })
  res.json(video)
})

// POST /api/videos — upload video (transcodes to WebM via FFmpeg in background)
router.post('/', requireAuth, upload.single('file'), (req, res) => {
  if (!req.file) return res.status(400).json({ error: 'No file uploaded' })

  const title = req.body.title?.trim() || req.file.originalname.replace(/\.[^.]+$/, '')

  let slug
  let attempts = 0
  do {
    slug = randomSlug(8)
    attempts++
  } while (db.prepare('SELECT id FROM videos WHERE slug = ?').get(slug) && attempts < 20)

  const inputPath  = join(UPLOAD_DIR, req.file.filename)
  const webmName   = req.file.filename.replace(/\.[^.]+$/, '') + '.webm'
  const outputPath = join(UPLOAD_DIR, webmName)

  db.prepare(`
    INSERT INTO videos (slug, title, filename, size, mime, status)
    VALUES (?, ?, ?, ?, ?, ?)
  `).run(slug, title, webmName, req.file.size, 'video/webm', 'processing')

  res.json({ slug, title, filename: webmName, size: req.file.size, status: 'processing' })

  // Transcode to WebM VP9 in background
  transcodeProgress.set(slug, { pct: 0 })

  const ffmpeg = spawn('ffmpeg', [
    '-i', inputPath,
    '-c:v', 'libvpx-vp9',
    '-crf', '33',
    '-b:v', '0',
    '-c:a', 'libopus',
    '-b:a', '128k',
    '-deadline', 'good',
    '-cpu-used', '4',
    '-y', outputPath,
  ])

  let durationSecs = 0
  ffmpeg.stderr.on('data', (chunk) => {
    const text = chunk.toString()
    if (!durationSecs) {
      const m = text.match(/Duration:\s*(\d+:\d+:\d+\.\d+)/)
      if (m) durationSecs = parseTimeSecs(m[1])
    }
    if (durationSecs) {
      const m = text.match(/time=(\d+:\d+:\d+\.\d+)/)
      if (m) {
        const pct = Math.min(99, Math.round((parseTimeSecs(m[1]) / durationSecs) * 100))
        transcodeProgress.set(slug, { pct })
      }
    }
  })

  ffmpeg.on('close', (code) => {
    transcodeProgress.delete(slug)
    if (code === 0) {
      let finalSize = req.file.size
      try { finalSize = statSync(outputPath).size } catch (_) {}
      db.prepare('UPDATE videos SET status = ?, size = ? WHERE slug = ?')
        .run('ready', finalSize, slug)
    } else {
      db.prepare('UPDATE videos SET status = ? WHERE slug = ?').run('error', slug)
    }
    // Always remove the original upload
    try { unlinkSync(inputPath) } catch (_) {}
  })
})

// PATCH /api/videos/:slug — rename title
router.patch('/:slug', requireAuth, (req, res) => {
  const { title } = req.body
  if (!title?.trim()) return res.status(400).json({ error: 'Title required' })
  const result = db.prepare('UPDATE videos SET title = ? WHERE slug = ?').run(title.trim(), req.params.slug)
  if (result.changes === 0) return res.status(404).json({ error: 'Not found' })
  res.json({ ok: true })
})

// DELETE /api/videos/:slug — delete video + file
router.delete('/:slug', requireAuth, (req, res) => {
  const video = db.prepare('SELECT * FROM videos WHERE slug = ?').get(req.params.slug)
  if (!video) return res.status(404).json({ error: 'Not found' })

  const filePath = join(UPLOAD_DIR, video.filename)
  if (existsSync(filePath)) {
    try { unlinkSync(filePath) } catch (_) {}
  }

  db.prepare('DELETE FROM videos WHERE slug = ?').run(req.params.slug)
  res.json({ ok: true })
})

export default router
