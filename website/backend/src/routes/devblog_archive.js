/**
 * Routes pour l'export et l'import d'un devblog au format .devblog
 *
 * Une archive .devblog est un fichier ZIP renommé contenant :
 *   manifest.json  → toutes les métadonnées du post + blocs (FR & EN)
 *   images/        → toutes les images référencées dans cover ou dans les blocs
 *
 * Export : GET  /api/posts/:id/export  → télécharge le .devblog
 * Import : POST /api/posts/import      → multipart/form-data { archive: <file.devblog> }
 */

import express from 'express'
import multer  from 'multer'
import JSZip   from 'jszip'
import { existsSync, readFileSync, writeFileSync, mkdirSync } from 'fs'
import { join, extname, basename, dirname } from 'path'
import { fileURLToPath } from 'url'
import db       from '../db.js'
import { requireAuth } from '../auth.js'

const __dirname  = dirname(fileURLToPath(import.meta.url))
const UPLOAD_DIR = join(__dirname, '../../../uploads')
mkdirSync(UPLOAD_DIR, { recursive: true })

const router = express.Router()

// Multer en mémoire pour l'import (pas de stockage intermédiaire)
const memUpload = multer({ storage: multer.memoryStorage(), limits: { fileSize: 200 * 1024 * 1024 } })

// ── helpers ────────────────────────────────────────────────────────────────

/**
 * Extrait le nom de fichier local depuis une URL /uploads/<filename>
 * Retourne null si ce n'est pas une URL locale.
 */
function localFilename(url) {
  if (!url) return null
  const m = url.match(/^\/uploads\/(.+)$/)
  return m ? m[1] : null
}

/**
 * Collecte tous les chemins d'images (/uploads/…) présents dans un post hydraté.
 * cover + tous les blocs de type image/video.
 */
function collectLocalImages(post) {
  const set = new Set()

  if (localFilename(post.cover)) set.add(post.cover)

  for (const block of [...(post.blocksFr ?? []), ...(post.blocksEn ?? [])]) {
    // bloc image/video → data.url
    if (block.data?.url && localFilename(block.data.url)) set.add(block.data.url)
    // au cas où d'autres champs contiendraient des URLs
    if (block.data?.src && localFilename(block.data.src)) set.add(block.data.src)
    // bloc gallery → data.images[].src
    if (Array.isArray(block.data?.images)) {
      for (const img of block.data.images) {
        if (img?.src && localFilename(img.src)) set.add(img.src)
      }
    }
  }

  return [...set]
}

function hydratePost(post) {
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

// ── GET /api/posts/:id/export ──────────────────────────────────────────────
router.get('/:id/export', requireAuth, async (req, res) => {
  const post = db.prepare('SELECT * FROM posts WHERE id = ?').get(req.params.id)
  if (!post) return res.status(404).json({ error: 'Post introuvable' })

  const hydrated   = hydratePost(post)
  const imageUrls  = collectLocalImages(hydrated)

  const zip = new JSZip()
  const imgFolder = zip.folder('images')

  // Remplacement des URLs locales par un chemin relatif dans l'archive
  const urlRemap = {}
  for (const url of imageUrls) {
    const filename = localFilename(url)
    if (!filename) continue
    const filePath = join(UPLOAD_DIR, filename)
    if (existsSync(filePath)) {
      imgFolder.file(filename, readFileSync(filePath))
      urlRemap[url] = `images/${filename}`
    }
  }

  // Réécriture des URLs dans les blocs pour qu'elles pointent vers l'archive
  function remapBlocks(blocks) {
    return blocks.map(b => {
      const data = { ...b.data }
      if (data.url && urlRemap[data.url]) data.url = urlRemap[data.url]
      if (data.src && urlRemap[data.src]) data.src = urlRemap[data.src]
      if (Array.isArray(data.images)) {
        data.images = data.images.map(img => (img?.src && urlRemap[img.src]) ? { ...img, src: urlRemap[img.src] } : img)
      }
      return { ...b, data }
    })
  }

  const manifest = {
    version:    1,
    exportedAt: new Date().toISOString(),
    post: {
      slug:       hydrated.slug,
      month:      hydrated.month,
      title_fr:   hydrated.title_fr,
      title_en:   hydrated.title_en,
      excerpt_fr: hydrated.excerpt_fr,
      excerpt_en: hydrated.excerpt_en,
      cover:      urlRemap[hydrated.cover] ?? hydrated.cover ?? null,
      author:     hydrated.author,
      read_time:  hydrated.read_time,
      published:  hydrated.published,
      games:      hydrated.games.map(g => g.slug),
    },
    blocksFr: remapBlocks(hydrated.blocksFr),
    blocksEn: remapBlocks(hydrated.blocksEn),
  }

  zip.file('manifest.json', JSON.stringify(manifest, null, 2))

  const buffer = await zip.generateAsync({ type: 'nodebuffer', compression: 'DEFLATE' })
  const archiveName = `${hydrated.slug}.devblog`

  res.setHeader('Content-Type',        'application/octet-stream')
  res.setHeader('Content-Disposition', `attachment; filename="${archiveName}"`)
  res.send(buffer)
})

// ── POST /api/posts/import ─────────────────────────────────────────────────
router.post('/import', requireAuth, memUpload.single('archive'), async (req, res) => {
  if (!req.file) return res.status(400).json({ error: 'Aucun fichier fourni' })

  let zip
  try {
    zip = await JSZip.loadAsync(req.file.buffer)
  } catch {
    return res.status(400).json({ error: 'Archive corrompue ou format invalide' })
  }

  // Lire le manifest
  const manifestFile = zip.file('manifest.json')
  if (!manifestFile) return res.status(400).json({ error: 'manifest.json introuvable dans l\'archive' })

  let manifest
  try {
    manifest = JSON.parse(await manifestFile.async('string'))
  } catch {
    return res.status(400).json({ error: 'manifest.json invalide (JSON malformé)' })
  }

  if (!manifest.post) return res.status(400).json({ error: 'Manifest incomplet (champ post manquant)' })

  // Extraire les images vers uploads/
  const urlRemap = {}   // "images/<filename>" → "/uploads/<newFilename>"
  const imageFiles = Object.values(zip.files).filter(f => !f.dir && f.name.startsWith('images/'))

  for (const imgFile of imageFiles) {
    const originalFilename = basename(imgFile.name)
    const ext        = extname(originalFilename)
    const newFilename = `${Date.now()}-${Math.random().toString(36).slice(2)}${ext}`
    const destPath   = join(UPLOAD_DIR, newFilename)
    const buffer     = await imgFile.async('nodebuffer')
    writeFileSync(destPath, buffer)
    urlRemap[imgFile.name] = `/uploads/${newFilename}`
  }

  // Fonction de réécriture des URLs dans les blocs
  function remapBlocks(blocks = []) {
    return blocks.map(b => {
      const data = { ...b.data }
      if (data.url && urlRemap[data.url]) data.url = urlRemap[data.url]
      if (data.src && urlRemap[data.src]) data.src = urlRemap[data.src]
      if (Array.isArray(data.images)) {
        data.images = data.images.map(img => (img?.src && urlRemap[img.src]) ? { ...img, src: urlRemap[img.src] } : img)
      }
      // Supprimer les propriétés de DB pour la réinsertion
      const { id, post_id, created_at, ...cleanBlock } = b
      return { ...cleanBlock, data }
    })
  }

  const p = manifest.post

  // Générer un slug depuis le mois (devlog-mars-2026-a3f2), ou garder le slug de l'archive
  const MONTHS_FR_ARCH = ['janvier','fevrier','mars','avril','mai','juin','juillet','aout','septembre','octobre','novembre','decembre']
  function randomSuffixArch(len = 4) { return Math.random().toString(36).slice(2, 2 + len) }
  function slugFromMonthArch(month) {
    const [year, mm] = (month ?? '').split('-')
    if (!year || !mm) return null
    const name = MONTHS_FR_ARCH[parseInt(mm, 10) - 1] ?? mm
    return `devlog-${name}-${year}-${randomSuffixArch()}`
  }
  // Générer un slug unique si collision
  let slug = p.slug || slugFromMonthArch(p.month) || `devlog-import-${Date.now()}`
  if (db.prepare('SELECT id FROM posts WHERE slug = ?').get(slug)) {
    slug = `${slug}-${randomSuffixArch()}`
  }

  // Réécrire la cover
  const cover = (p.cover && urlRemap[p.cover]) ? urlRemap[p.cover] : p.cover ?? null

  // Transaction d'import
  const importTx = db.transaction(() => {
    const result = db.prepare(`
      INSERT INTO posts (slug, month, title_fr, title_en, excerpt_fr, excerpt_en, cover, author, read_time, published)
      VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, 0)
    `).run(
      slug,
      p.month      ?? '',
      p.title_fr   ?? '',
      p.title_en   ?? p.title_fr ?? '',
      p.excerpt_fr ?? '',
      p.excerpt_en ?? p.excerpt_fr ?? '',
      cover,
      p.author     ?? 'OpenFramework',
      p.read_time  ?? 5,
    )

    const postId = result.lastInsertRowid

    // Sync jeux
    for (const gameSlug of (p.games ?? [])) {
      const game = db.prepare('SELECT id FROM games WHERE slug = ?').get(gameSlug)
      if (game) db.prepare('INSERT OR IGNORE INTO post_games (post_id, game_id) VALUES (?, ?)').run(postId, game.id)
    }

    // Insérer les blocs FR
    for (const block of remapBlocks(manifest.blocksFr ?? [])) {
      db.prepare(`
        INSERT INTO blocks (post_id, lang, type, game_slug, position, author, data)
        VALUES (?, 'fr', ?, ?, ?, ?, ?)
      `).run(postId, block.type, block.game_slug ?? null, block.position ?? 0, block.author ?? null, JSON.stringify(block.data ?? {}))
    }

    // Insérer les blocs EN
    for (const block of remapBlocks(manifest.blocksEn ?? [])) {
      db.prepare(`
        INSERT INTO blocks (post_id, lang, type, game_slug, position, author, data)
        VALUES (?, 'en', ?, ?, ?, ?, ?)
      `).run(postId, block.type, block.game_slug ?? null, block.position ?? 0, block.author ?? null, JSON.stringify(block.data ?? {}))
    }

    return postId
  })

  try {
    const postId = importTx()
    const post   = db.prepare('SELECT * FROM posts WHERE id = ?').get(postId)
    res.status(201).json(hydratePost(post))
  } catch (e) {
    res.status(500).json({ error: 'Erreur lors de l\'import : ' + e.message })
  }
})

export default router
