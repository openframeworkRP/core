import express from 'express'
import multer from 'multer'
import sharp from 'sharp'
import { unlinkSync, mkdirSync } from 'fs'
import { join, extname, basename, dirname } from 'path'
import { fileURLToPath } from 'url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const UPLOAD_DIR = join(__dirname, '../../uploads')
mkdirSync(UPLOAD_DIR, { recursive: true })

// Stocker temporairement en mémoire pour optimiser avant écriture
const storage = multer.memoryStorage()

const upload = multer({
  storage,
  limits: { fileSize: 50 * 1024 * 1024 }, // 50 MB
  fileFilter: (_req, file, cb) => {
    if (file.mimetype.startsWith('image/') || file.mimetype.startsWith('video/')) {
      cb(null, true)
    } else {
      cb(new Error('Only images and videos are allowed'))
    }
  },
})

const ANIMATED_TYPES = ['image/gif', 'image/webp']
const SVG_TYPE = 'image/svg+xml'

const router = express.Router()

router.post('/', upload.single('file'), async (req, res) => {
  if (!req.file) return res.status(400).json({ error: 'No file uploaded' })

  const slug = `${Date.now()}-${Math.random().toString(36).slice(2)}`

  // Les vidéos passent sans traitement
  if (req.file.mimetype.startsWith('video/')) {
    const ext = extname(req.file.originalname) || '.mp4'
    const filename = `${slug}${ext}`
    const { writeFile } = await import('fs/promises')
    await writeFile(join(UPLOAD_DIR, filename), req.file.buffer)
    return res.json({ url: `/uploads/${filename}` })
  }

  // SVG : pas de traitement, écriture directe
  if (req.file.mimetype === SVG_TYPE) {
    const filename = `${slug}.svg`
    const { writeFile } = await import('fs/promises')
    await writeFile(join(UPLOAD_DIR, filename), req.file.buffer)
    return res.json({ url: `/uploads/${filename}` })
  }

  try {
    const filename = `${slug}.webp`
    const outputPath = join(UPLOAD_DIR, filename)

    const isAnimated = ANIMATED_TYPES.includes(req.file.mimetype)

    const image = sharp(req.file.buffer, { animated: isAnimated })
    const meta = await image.metadata()

    // Redimensionner si > 2400px de large
    const pipeline = meta.width > 2400
      ? image.resize({ width: 2400, withoutEnlargement: true })
      : image

    await pipeline
      .webp({
        quality: 82,
        effort: 4,      // 0=rapide … 6=lent, bon compromis
        lossless: false,
      })
      .toFile(outputPath)

    return res.json({ url: `/uploads/${filename}` })
  } catch (err) {
    console.error('[upload] sharp error:', err)
    return res.status(500).json({ error: 'Image optimisation failed' })
  }
})

export default router
