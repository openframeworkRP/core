import { Router } from 'express'
import { requireAuth, requireRole } from '../auth.js'
import db from '../db.js'

const router = Router()

const NC_URL   = process.env.NEXTCLOUD_URL   || ''
const NC_USER  = process.env.NEXTCLOUD_USER  || ''
const NC_TOKEN = process.env.NEXTCLOUD_TOKEN || ''
const NC_ROOT  = process.env.NEXTCLOUD_ROOT  || '/'

const authHeader = () =>
  'Basic ' + Buffer.from(`${NC_USER}:${NC_TOKEN}`).toString('base64')

function davUrl(path) {
  // path comes in URL-decoded from the client — re-encode each segment
  const encoded = path.split('/').map(s => encodeURIComponent(s)).join('/')
  return `${NC_URL}/remote.php/dav/files/${NC_USER}${encoded}`
}

// Parse a WebDAV PROPFIND XML response into a flat list of entries
function parsePropfind(xml, requestedPath) {
  const entries = []
  const responseRe = /<d:response>([\s\S]*?)<\/d:response>/g
  let m
  while ((m = responseRe.exec(xml)) !== null) {
    const block = m[1]
    const href  = (block.match(/<d:href>(.*?)<\/d:href>/) || [])[1] || ''

    // Decode and strip WebDAV prefix to get the relative path
    const decoded = decodeURIComponent(href)
    const davPrefix = `/remote.php/dav/files/${NC_USER}`
    const fullPath  = decoded.startsWith(davPrefix) ? decoded.slice(davPrefix.length) : decoded

    // Skip the directory itself (depth=0 response)
    if (fullPath.replace(/\/$/, '') === requestedPath.replace(/\/$/, '')) continue

    const isDir = block.includes('<d:collection/>') || block.includes('<d:collection />')
    const name  = fullPath.replace(/\/$/, '').split('/').pop() || ''

    const sizeMatch    = block.match(/<d:getcontentlength>(.*?)<\/d:getcontentlength>/)
    const modMatch     = block.match(/<d:getlastmodified>(.*?)<\/d:getlastmodified>/)
    const mimeMatch    = block.match(/<d:getcontenttype>(.*?)<\/d:getcontenttype>/)

    entries.push({
      name,
      path: fullPath.replace(/\/$/, ''),
      isDir,
      size:     sizeMatch  ? parseInt(sizeMatch[1],  10) : 0,
      modified: modMatch   ? modMatch[1]  : '',
      mime:     mimeMatch  ? mimeMatch[1] : '',
    })
  }
  return entries
}

// ── GET /api/nextcloud/browse?path=/foo/bar  ──────────────────────────────
router.get('/browse', requireAuth, async (req, res) => {
  const path = req.query.path || NC_ROOT

  try {
    const url = davUrl(path)
    const response = await fetch(url, {
      method: 'PROPFIND',
      headers: {
        Authorization: authHeader(),
        Depth: '1',
        'Content-Type': 'application/xml',
      },
      body: `<?xml version="1.0"?>
<d:propfind xmlns:d="DAV:">
  <d:prop>
    <d:resourcetype/>
    <d:getcontentlength/>
    <d:getlastmodified/>
    <d:getcontenttype/>
  </d:prop>
</d:propfind>`,
    })

    if (!response.ok) {
      return res.status(response.status).json({ error: `Nextcloud: ${response.statusText}` })
    }

    const xml     = await response.text()
    const entries = parsePropfind(xml, path)

    // Sort: folders first, then files alphabetically
    entries.sort((a, b) => {
      if (a.isDir !== b.isDir) return a.isDir ? -1 : 1
      return a.name.localeCompare(b.name)
    })

    res.json({ path, entries })
  } catch (err) {
    console.error('[nextcloud browse]', err)
    res.status(502).json({ error: 'Impossible de contacter Nextcloud' })
  }
})

// ── GET /api/nextcloud/scan-assets  (auth) ────────────────────────────────
// Descend récursivement sous /unreal_asset (BFS, max 6 niveaux).
// Retourne chaque fichier ZIP trouvé comme un pack :
//   name   = nom du ZIP sans extension
//   path   = chemin complet du ZIP (pour le download_url)
//   vendor = dossier de niveau 1 sous /unreal_asset (nom du studio)
router.get('/scan-assets', requireAuth, async (req, res) => {
  const base = process.env.NEXTCLOUD_ASSET_ROOT || '/unreal_asset'

  async function listDir(path) {
    const r = await fetch(davUrl(path), {
      method: 'PROPFIND',
      headers: { Authorization: authHeader(), Depth: '1', 'Content-Type': 'application/xml' },
      body: `<?xml version="1.0"?><d:propfind xmlns:d="DAV:"><d:prop><d:resourcetype/><d:getcontentlength/></d:prop></d:propfind>`,
    })
    if (!r.ok) throw new Error(`Nextcloud ${r.status}: ${r.statusText}`)
    return parsePropfind(await r.text(), path)
  }

  try {
    const zips = []
    const queue = [base]

    while (queue.length) {
      const dir = queue.shift()
      const entries = await listDir(dir).catch(() => [])
      for (const e of entries) {
        if (e.isDir) {
          queue.push(e.path)
        } else if (/\.(zip|rar)$/i.test(e.name)) {
          // Vendor = premier segment sous /unreal_asset
          const relative = e.path.slice(base.length).replace(/^\//, '')
          const parts    = relative.split('/')
          const vendor   = parts.length > 1 ? parts[0] : null
          zips.push({
            name:   e.name.replace(/\.(zip|rar)$/i, ''),
            path:   e.path,
            vendor,
            size:   e.size,
          })
        }
      }
    }

    zips.sort((a, b) => (a.vendor || '').localeCompare(b.vendor || '') || a.name.localeCompare(b.name))
    res.json({ packs: zips, total: zips.length })
  } catch (err) {
    console.error('[nextcloud scan-assets]', err)
    res.status(502).json({ error: err.message })
  }
})

// ── GET /api/nextcloud/download?path=/foo/file.zip  ───────────────────────
// Proxifie le téléchargement pour éviter d'exposer le token au client
router.get('/download', requireAuth, async (req, res) => {
  const path = req.query.path
  if (!path) return res.status(400).json({ error: 'path requis' })

  try {
    const url = davUrl(path)
    const upstream = await fetch(url, {
      headers: { Authorization: authHeader() },
    })

    if (!upstream.ok) {
      return res.status(upstream.status).json({ error: `Nextcloud: ${upstream.statusText}` })
    }

    const filename    = path.split('/').pop() || 'file'
    const contentType = upstream.headers.get('content-type') || 'application/octet-stream'
    const contentLen  = upstream.headers.get('content-length')

    res.setHeader('Content-Disposition', `attachment; filename="${encodeURIComponent(filename)}"`)
    res.setHeader('Content-Type', contentType)
    if (contentLen) res.setHeader('Content-Length', contentLen)

    // Stream directement
    const reader = upstream.body.getReader()
    const pump = async () => {
      while (true) {
        const { done, value } = await reader.read()
        if (done) { res.end(); break }
        res.write(Buffer.from(value))
      }
    }
    await pump()
  } catch (err) {
    console.error('[nextcloud download]', err)
    res.status(502).json({ error: 'Erreur lors du téléchargement depuis Nextcloud' })
  }
})

// ── Demandes de téléchargement ────────────────────────────────────────────

// POST /api/nextcloud/requests — l'utilisateur demande la permission de DL
router.post('/requests', requireAuth, (req, res) => {
  const { filePath, fileName } = req.body || {}
  if (!filePath || !fileName) return res.status(400).json({ error: 'filePath et fileName requis' })
  const requesterId   = req.user?.steamId || req.user?.id || 'unknown'
  const requesterName = req.user?.displayName || req.user?.display_name || requesterId

  // Empêche les doublons (pending)
  const existing = db.prepare(
    "SELECT id FROM nc_dl_requests WHERE file_path = ? AND requester_id = ? AND status = 'pending'"
  ).get(filePath, requesterId)
  if (existing) return res.status(409).json({ error: 'Demande déjà en attente' })

  const result = db.prepare(
    'INSERT INTO nc_dl_requests (file_path, file_name, requester_id, requester_name) VALUES (?, ?, ?, ?)'
  ).run(filePath, fileName, requesterId, requesterName)

  res.json({ id: result.lastInsertRowid, status: 'pending' })
})

// GET /api/nextcloud/requests — liste les demandes (admin/owner uniquement)
router.get('/requests', requireRole('admin'), (req, res) => {
  const status = req.query.status || null
  const rows = status
    ? db.prepare('SELECT * FROM nc_dl_requests WHERE status = ? ORDER BY created_at DESC').all(status)
    : db.prepare('SELECT * FROM nc_dl_requests ORDER BY created_at DESC LIMIT 200').all()
  res.json(rows)
})

// GET /api/nextcloud/my-requests — demandes de l'utilisateur courant
router.get('/my-requests', requireAuth, (req, res) => {
  const requesterId = req.user?.steamId || req.user?.id || 'unknown'
  const rows = db.prepare(
    'SELECT * FROM nc_dl_requests WHERE requester_id = ? ORDER BY created_at DESC'
  ).all(requesterId)
  res.json(rows)
})

// PATCH /api/nextcloud/requests/:id — approuver ou refuser (admin/owner uniquement)
router.patch('/requests/:id', requireRole('admin'), (req, res) => {
  const { status } = req.body || {}
  if (!['approved', 'denied'].includes(status)) return res.status(400).json({ error: 'status doit être approved ou denied' })
  const reviewedBy = req.user?.displayName || req.user?.display_name || req.user?.steamId || 'admin'
  const result = db.prepare(
    "UPDATE nc_dl_requests SET status = ?, reviewed_by = ?, reviewed_at = datetime('now') WHERE id = ?"
  ).run(status, reviewedBy, req.params.id)
  if (result.changes === 0) return res.status(404).json({ error: 'Demande introuvable' })
  res.json({ ok: true })
})

export default router
