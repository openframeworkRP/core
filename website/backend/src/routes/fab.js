import { Router } from 'express'
import { requireAuth } from '../auth.js'
import { execFile } from 'node:child_process'
import { promisify } from 'node:util'
import { filenameToSearchTerm } from '../dekogon_name_map.js'

// Puppeteer-core chargé dynamiquement
let _puppeteer = null
async function getBrowser() {
  if (!_puppeteer) {
    try {
      console.log('[puppeteer] import puppeteer-core...')
      _puppeteer = await import('puppeteer-core')
      console.log('[puppeteer] import OK')
    } catch (e) {
      console.error('[puppeteer] import FAILED:', e.message)
      throw new Error('puppeteer-core non installé (npm install puppeteer-core)')
    }
  }
  const executablePath = process.env.PUPPETEER_EXECUTABLE_PATH || '/usr/bin/chromium'
  console.log('[puppeteer] launch chromium:', executablePath)
  const browser = await _puppeteer.default.launch({
    headless: true,
    executablePath,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-dev-shm-usage'],
  })
  console.log('[puppeteer] browser launched OK')
  return browser
}

const execFileAsync = promisify(execFile)

const router = Router()

// Extrait le username depuis une URL Fab.com ou retourne la valeur brute
function parseUsername(input) {
  const s = decodeURIComponent(input.trim())
  // https://www.fab.com/fr/sellers/Dekogon Studios  ou  fab.com/sellers/Dekogon Studios
  const m = s.match(/\/sellers\/(.+?)(?:\/|$)/)
  return m ? m[1].trim() : s
}

const XHR_HEADERS = {
  'Accept': 'application/json, text/plain, */*',
  'Accept-Language': 'fr-FR,fr;q=0.9,en-US;q=0.8,en;q=0.7',
  'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36',
  'sec-ch-ua': '"Chromium";v="124", "Google Chrome";v="124", "Not-A.Brand";v="99"',
  'sec-ch-ua-mobile': '?0',
  'sec-ch-ua-platform': '"Windows"',
  'Sec-Fetch-Dest': 'empty',
  'Sec-Fetch-Mode': 'cors',
  'Sec-Fetch-Site': 'same-origin',
  'Referer': 'https://www.fab.com/',
  'Origin': 'https://www.fab.com',
  'Connection': 'keep-alive',
}

// ── GET /api/fab/listings?username=xxx  (auth) ────────────────────────────
router.get('/listings', requireAuth, async (req, res) => {
  const { username } = req.query
  if (!username?.trim()) return res.status(400).json({ error: 'username requis' })

  const name = parseUsername(username)

  // Essai 1 : API interne /i/listings (XHR)
  try {
    const url = `https://www.fab.com/i/listings?seller_username=${encodeURIComponent(name)}&sort_by=-published_at&first=48`
    const apiRes = await fetch(url, { headers: XHR_HEADERS })

    if (apiRes.ok) {
      const data = await apiRes.json()
      const results = data.results || data.listings || data.assets || []
      if (Array.isArray(results) && results.length >= 0) {
        return res.json({ assets: normalizeAssets(results, name), total: data.count ?? results.length })
      }
    }
  } catch (_) {}

  // Essai 2 : API de recherche /i/search
  try {
    const url = `https://www.fab.com/i/search?seller_username=${encodeURIComponent(name)}&first=48`
    const apiRes = await fetch(url, { headers: XHR_HEADERS })

    if (apiRes.ok) {
      const data = await apiRes.json()
      const results = data.results || data.listings || data.assets || []
      if (Array.isArray(results) && results.length >= 0) {
        return res.json({ assets: normalizeAssets(results, name), total: data.count ?? results.length })
      }
    }
  } catch (_) {}

  // Essai 3 : /_next/data/ — récupère d'abord le buildId
  try {
    const homeRes = await fetch('https://www.fab.com/', {
      headers: {
        ...XHR_HEADERS,
        'Accept': 'text/html,application/xhtml+xml,*/*;q=0.8',
        'Sec-Fetch-Dest': 'document',
        'Sec-Fetch-Mode': 'navigate',
        'Sec-Fetch-Site': 'none',
      },
      redirect: 'follow',
    })

    if (homeRes.ok) {
      const html = await homeRes.text()
      const m = html.match(/"buildId"\s*:\s*"([^"]+)"/)
      if (m) {
        const buildId = m[1]
        const dataUrl = `https://www.fab.com/_next/data/${buildId}/sellers/${encodeURIComponent(name)}.json`
        const dataRes = await fetch(dataUrl, { headers: XHR_HEADERS })
        if (dataRes.ok) {
          const json = await dataRes.json()
          const props = json?.pageProps ?? {}
          const rawListings =
            props.listings?.results ?? props.listings ??
            props.initialListings?.results ?? props.initialListings ??
            findListings(props)
          if (Array.isArray(rawListings) && rawListings.length >= 0) {
            return res.json({ assets: normalizeAssets(rawListings, name), total: rawListings.length })
          }
        }
      }
    }
  } catch (_) {}

  res.status(503).json({
    error: `Fab.com bloque les requêtes serveur (anti-bot). Username détecté : "${name}". Réessayez plus tard.`,
  })
})

function findListings(obj, depth = 0) {
  if (depth > 8 || !obj || typeof obj !== 'object') return null
  if (Array.isArray(obj)) {
    if (obj.length > 0 && (obj[0]?.slug || obj[0]?.uid || obj[0]?.title)) return obj
    return null
  }
  for (const key of Object.keys(obj)) {
    const found = findListings(obj[key], depth + 1)
    if (found) return found
  }
  return null
}

function normalizeAssets(list, username) {
  return list.map(a => {
    // Si déjà normalisé par scrapeFabPage (a des champs url + title directs)
    if (a.url && a.title && !a.uid) {
      return {
        id: a.slug || a.url,
        title: a.title,
        url: a.url,
        price: a.price || '',
        thumbnail: a.thumbnail || null,
        publishedAt: a.publishedAt || null,
      }
    }
    // Format API Fab brut
    return {
      id: a.uid || a.id || a.slug,
      title: a.title || a.name || 'Sans titre',
      url: a.slug
        ? `https://www.fab.com/listings/${a.slug}`
        : `https://www.fab.com/sellers/${username}`,
      price: a.price
        ? typeof a.price === 'object'
          ? `${(a.price.amount / 100).toFixed(2)} ${a.price.currency}`
          : String(a.price)
        : 'Gratuit',
      thumbnail:
        a.thumbnailUrl || a.thumbnail_url || a.thumbnail ||
        (Array.isArray(a.images) ? a.images[0]?.url ?? a.images[0] : null) || null,
      publishedAt: a.publishedAt || a.published_at || a.createdAt || a.created_at || null,
    }
  })
}

// ── GET /api/fab/search?q=xxx&first=5  (auth) ────────────────────────────
router.get('/search', requireAuth, async (req, res) => {
  const { q, first = '6' } = req.query
  if (!q?.trim()) return res.status(400).json({ error: 'q requis' })

  const query = q.trim()

  // Essai 1 : API interne /i/listings avec paramètre de recherche
  try {
    const url = `https://www.fab.com/i/listings?q=${encodeURIComponent(query)}&first=${first}`
    const apiRes = await fetch(url, { headers: XHR_HEADERS })
    if (apiRes.ok) {
      const data = await apiRes.json()
      const results = data.results || data.listings || data.assets || []
      if (Array.isArray(results)) {
        return res.json({ results: normalizeAssets(results, '') })
      }
    }
  } catch (_) {}

  // Essai 2 : API de recherche /i/search
  try {
    const url = `https://www.fab.com/i/search?q=${encodeURIComponent(query)}&first=${first}`
    const apiRes = await fetch(url, { headers: XHR_HEADERS })
    if (apiRes.ok) {
      const data = await apiRes.json()
      const results = data.results || data.listings || data.assets || []
      if (Array.isArray(results)) {
        return res.json({ results: normalizeAssets(results, '') })
      }
    }
  } catch (_) {}

  res.json({ results: [] })
})

// ── GET /api/fab/asset?url=xxx  (auth) ───────────────────────────────────
// Scrappe les infos d'un asset Fab.com : nom, prix, image
router.get('/asset', requireAuth, async (req, res) => {
  const { url } = req.query
  if (!url?.trim()) return res.status(400).json({ error: 'url requise' })

  const slugMatch = url.match(/\/listings\/([^/?#]+)/)
  if (!slugMatch) return res.status(400).json({ error: 'URL Fab.com invalide (doit contenir /listings/...)' })
  const slug = slugMatch[1]
  const targetUrl = `https://www.fab.com/listings/${slug}`

  // 1. Essayer le microservice Scrapling (Camoufox — exécution JS, bypass Cloudflare)
  try {
    const data = await callScraperListing(targetUrl)
    return res.json({
      name: data.name || 'Sans titre',
      price: data.price || 'Prix non disponible',
      imageUrl: data.imageUrl || '',
      url: targetUrl,
    })
  } catch (scraperErr) {
    console.log('[asset] fab-scraper indisponible:', scraperErr.message, '— fallback curl')
  }

  // 2. Fallback curl
  try {
    const { stdout } = await execFileAsync('curl', [
      '-s', '-L', '--compressed', '--max-time', '15',
      '-H', 'Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8',
      '-H', 'Accept-Language: en-US,en;q=0.9',
      '-H', 'User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36',
      '-H', 'Sec-Fetch-Dest: document', '-H', 'Sec-Fetch-Mode: navigate',
      '-H', 'Sec-Fetch-Site: none', '-H', 'Cache-Control: max-age=0',
      targetUrl,
    ], { maxBuffer: 5 * 1024 * 1024 })

    let name = '', imageUrl = '', price = ''
    const ldRe = /<script[^>]+type="application\/ld\+json"[^>]*>([\s\S]*?)<\/script>/g
    let ldMatch
    while ((ldMatch = ldRe.exec(stdout)) !== null) {
      try {
        const ld = JSON.parse(ldMatch[1])
        if (ld['@type'] !== 'Product') continue
        name = ld.name || ''
        imageUrl = ld.image || ''
        const offer = Array.isArray(ld.offers) ? ld.offers[0] : ld.offers
        if (offer?.price != null) {
          const currency = offer.priceCurrency || 'USD'
          price = `${Number(offer.price).toFixed(2)} ${currency}`
        }
        break
      } catch (_) {}
    }
    if (!name) { const m = stdout.match(/<meta[^>]+property="og:title"[^>]+content="([^"]+)"/); name = m ? m[1].trim() : 'Sans titre' }
    if (!imageUrl) { const m = stdout.match(/<meta[^>]+property="og:image"[^>]+content="([^"]+)"/); imageUrl = m ? m[1].trim() : '' }

    return res.json({ name: name || 'Sans titre', price: price || 'Prix non disponible', imageUrl, url: targetUrl })
  } catch (error) {
    console.error('[asset] erreur scraping:', error)
    res.status(503).json({ error: 'Erreur lors du scraping de Fab.com' })
  }
})

// ── GET /api/fab/preview?url=xxx  (auth) ─────────────────────────────────
// Retourne { name, description, imageUrl, price } pour un listing Fab.com
router.get('/preview', requireAuth, async (req, res) => {
  const { url } = req.query
  if (!url?.trim()) return res.status(400).json({ error: 'url requise' })

  const slugMatch = url.match(/\/listings\/([^/?#]+)/)
  if (!slugMatch) return res.status(400).json({ error: 'URL Fab.com invalide (doit contenir /listings/...)' })
  const slug = slugMatch[1]
  const targetUrl = `https://www.fab.com/listings/${slug}`

  // 1. Essayer le microservice Scrapling (Camoufox — exécution JS, bypass Cloudflare)
  try {
    const data = await callScraperListing(targetUrl)
    return res.json({ ...data, url: targetUrl })
  } catch (scraperErr) {
    console.log('[preview] fab-scraper indisponible:', scraperErr.message, '— fallback curl')
  }

  // 2. Fallback curl
  try {
    const { stdout } = await execFileAsync('curl', [
      '-s', '-L', '--compressed', '--max-time', '15',
      '-H', 'Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8',
      '-H', 'Accept-Language: en-US,en;q=0.9',
      '-H', 'User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36',
      '-H', 'Sec-Fetch-Dest: document', '-H', 'Sec-Fetch-Mode: navigate',
      '-H', 'Sec-Fetch-Site: none', '-H', 'Cache-Control: max-age=0',
      targetUrl,
    ], { maxBuffer: 5 * 1024 * 1024 })

    const meta = parseHtmlMeta(stdout)
    return res.json({ ...meta, url: targetUrl })
  } catch (err) {
    console.error('[preview] erreur:', err.message)
    res.status(503).json({ error: 'Impossible de récupérer les données depuis Fab.com' })
  }
})

function extractMeta(listing) {
  const name = listing.title || listing.name || ''
  const description = listing.description || listing.short_description || ''
  const imageUrl =
    listing.thumbnailUrl || listing.thumbnail_url || listing.thumbnail ||
    (Array.isArray(listing.images) ? listing.images[0]?.url ?? listing.images[0] : null) || ''

  // Prix licence personnelle
  let price = ''
  if (listing.price) {
    if (typeof listing.price === 'object') {
      price = `${(listing.price.amount / 100).toFixed(2)} ${listing.price.currency}`
    } else {
      price = String(listing.price)
    }
  } else if (listing.personalLicense?.price) {
    const p = listing.personalLicense.price
    price = typeof p === 'object' ? `${(p.amount / 100).toFixed(2)} ${p.currency}` : String(p)
  } else if (listing.licenses) {
    const personal = listing.licenses.find(l => /personal/i.test(l.type || l.name || ''))
    if (personal?.price) {
      const p = personal.price
      price = typeof p === 'object' ? `${(p.amount / 100).toFixed(2)} ${p.currency}` : String(p)
    }
  }

  return { name, description, imageUrl, price }
}

function parseHtmlMeta(html) {
  const get = (pattern) => { const m = html.match(pattern); return m ? m[1] : '' }

  const name = get(/<meta[^>]+property="og:title"[^>]+content="([^"]+)"/)
    || get(/<title>([^<]+)<\/title>/)
  const description = get(/<meta[^>]+property="og:description"[^>]+content="([^"]+)"/)
    || get(/<meta[^>]+name="description"[^>]+content="([^"]+)"/)
  const imageUrl = get(/<meta[^>]+property="og:image"[^>]+content="([^"]+)"/)

  // Prix depuis JSON-LD
  let price = ''
  const ldMatch = html.match(/<script[^>]+type="application\/ld\+json"[^>]*>([\s\S]*?)<\/script>/)
  if (ldMatch) {
    try {
      const ld = JSON.parse(ldMatch[1])
      const offers = ld.offers || (Array.isArray(ld) ? ld.find(o => o.offers)?.offers : null)
      if (offers) {
        const personal = Array.isArray(offers)
          ? offers.find(o => /personal/i.test(o.name || o.description || '')) || offers[0]
          : offers
        if (personal?.price != null) {
          price = `${personal.price} ${personal.priceCurrency || ''}`.trim()
        }
      }
    } catch (_) {}
  }

  // Prix depuis window.__NEXT_DATA__
  if (!price) {
    const ndMatch = html.match(/<script[^>]+id="__NEXT_DATA__"[^>]*>([\s\S]*?)<\/script>/)
    if (ndMatch) {
      try {
        const nd = JSON.parse(ndMatch[1])
        const listing = nd?.props?.pageProps?.listing || nd?.props?.pageProps?.initialListing
        if (listing) {
          const { price: p, licenses, personalLicense } = listing
          if (p) price = typeof p === 'object' ? `${(p.amount / 100).toFixed(2)} ${p.currency}` : String(p)
          else if (personalLicense?.price) {
            const pp = personalLicense.price
            price = typeof pp === 'object' ? `${(pp.amount / 100).toFixed(2)} ${pp.currency}` : String(pp)
          } else if (Array.isArray(licenses)) {
            const per = licenses.find(l => /personal/i.test(l.type || l.name || ''))
            if (per?.price) {
              const pp = per.price
              price = typeof pp === 'object' ? `${(pp.amount / 100).toFixed(2)} ${pp.currency}` : String(pp)
            }
          }
        }
      } catch (_) {}
    }
  }

  return { name: name?.trim() || '', description: description?.trim() || '', imageUrl: imageUrl?.trim() || '', price: price?.trim() || '' }
}

// ── Helpers Nextcloud (réplique locale légère de nextcloud.js) ─────────────
const NC_URL   = () => process.env.NEXTCLOUD_URL   || ''
const NC_USER  = () => process.env.NEXTCLOUD_USER  || ''
const NC_TOKEN = () => process.env.NEXTCLOUD_TOKEN || ''

function ncAuthHeader() {
  return 'Basic ' + Buffer.from(`${NC_USER()}:${NC_TOKEN()}`).toString('base64')
}
function ncDavUrl(path) {
  const encoded = path.split('/').map(s => encodeURIComponent(s)).join('/')
  return `${NC_URL()}/remote.php/dav/files/${NC_USER()}${encoded}`
}
function ncParsePropfind(xml, requestedPath) {
  const entries = []
  const responseRe = /<d:response>([\s\S]*?)<\/d:response>/g
  let m
  while ((m = responseRe.exec(xml)) !== null) {
    const block = m[1]
    const href  = (block.match(/<d:href>(.*?)<\/d:href>/) || [])[1] || ''
    const decoded  = decodeURIComponent(href)
    const davPrefix = `/remote.php/dav/files/${NC_USER()}`
    const fullPath  = decoded.startsWith(davPrefix) ? decoded.slice(davPrefix.length) : decoded
    if (fullPath.replace(/\/$/, '') === requestedPath.replace(/\/$/, '')) continue
    const isDir = block.includes('<d:collection/>') || block.includes('<d:collection />')
    const name  = fullPath.replace(/\/$/, '').split('/').pop() || ''
    const sizeMatch = block.match(/<d:getcontentlength>(.*?)<\/d:getcontentlength>/)
    entries.push({ name, path: fullPath.replace(/\/$/, ''), isDir, size: sizeMatch ? parseInt(sizeMatch[1], 10) : 0 })
  }
  return entries
}
async function ncListDir(path) {
  const r = await fetch(ncDavUrl(path), {
    method: 'PROPFIND',
    headers: { Authorization: ncAuthHeader(), Depth: '1', 'Content-Type': 'application/xml' },
    body: `<?xml version="1.0"?><d:propfind xmlns:d="DAV:"><d:prop><d:resourcetype/><d:getcontentlength/></d:prop></d:propfind>`,
  })
  if (!r.ok) throw new Error(`Nextcloud ${r.status}: ${r.statusText}`)
  return ncParsePropfind(await r.text(), path)
}

/**
 * Appelle le microservice fab-scraper (Scrapling + Camoufox) pour charger
 * une page Fab.com avec exécution JS et bypass Cloudflare.
 * Fallback sur curl si le scraper est indisponible.
 */
const FAB_SCRAPER_URL = () => process.env.FAB_SCRAPER_URL || 'http://localhost:8000'

async function callScraperSeller(seller, options = {}) {
  const { limit = 96, sort_by = '-published_at' } = options
  const base = FAB_SCRAPER_URL()
  const url = `${base}/seller?seller=${encodeURIComponent(seller)}&limit=${limit}&sort_by=${sort_by}`
  const res = await fetch(url, { signal: AbortSignal.timeout(90_000) })
  if (!res.ok) throw new Error(`fab-scraper /seller HTTP ${res.status}`)
  const data = await res.json()
  return data.listings || []
}

async function callScraperSearch(seller, q, limit = 5) {
  const base = FAB_SCRAPER_URL()
  const url = `${base}/search?seller=${encodeURIComponent(seller)}&q=${encodeURIComponent(q)}&limit=${limit}`
  const res = await fetch(url, { signal: AbortSignal.timeout(90_000) })
  if (!res.ok) throw new Error(`fab-scraper /search HTTP ${res.status}`)
  const data = await res.json()
  return data.results || []
}

async function callScraperListing(listingUrl) {
  const base = FAB_SCRAPER_URL()
  const url = `${base}/listing?url=${encodeURIComponent(listingUrl)}`
  const res = await fetch(url, { signal: AbortSignal.timeout(90_000) })
  if (!res.ok) throw new Error(`fab-scraper /listing HTTP ${res.status}`)
  return res.json()
}

async function callScraperGlobalSearch(q, limit = 24) {
  const base = FAB_SCRAPER_URL()
  const url = `${base}/global-search?q=${encodeURIComponent(q)}&limit=${limit}`
  const res = await fetch(url, { signal: AbortSignal.timeout(90_000) })
  if (!res.ok) throw new Error(`fab-scraper /global-search HTTP ${res.status}`)
  return res.json()
}

async function scrapeFabPage(pageUrl, tag = 'fab') {
  // Extraire seller et query depuis l'URL
  const sellerMatch = pageUrl.match(/\/sellers\/([^?]+)/)
  const seller = sellerMatch ? decodeURIComponent(sellerMatch[1]) : ''
  const qMatch  = pageUrl.match(/[?&]q=([^&]+)/)
  const q = qMatch ? decodeURIComponent(qMatch[1]) : null

  // 1. Essayer le microservice Scrapling
  try {
    console.log(`[${tag}] → fab-scraper (Camoufox)...`)
    let results
    if (q && seller) {
      results = await callScraperSearch(seller, q, 96)
    } else if (seller) {
      results = await callScraperSeller(seller, { limit: 96 })
    } else {
      throw new Error('seller non détectable depuis URL')
    }
    if (Array.isArray(results) && results.length > 0) {
      console.log(`[${tag}] fab-scraper OK: ${results.length} listings`)
      return results
    }
    throw new Error('fab-scraper: résultats vides')
  } catch (scraperErr) {
    console.log(`[${tag}] fab-scraper indisponible (${scraperErr.message}), fallback curl...`)
  }

  // 2. Fallback curl (SSR HTML — peut être bloqué par Cloudflare)
  console.log(`[${tag}] curl URL:`, pageUrl)
  const { stdout } = await execFileAsync('curl', [
    '-s', '-L', '--compressed',
    '--max-time', '20',
    '-H', 'Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8',
    '-H', 'Accept-Language: fr-FR,fr;q=0.9,en-US;q=0.8,en;q=0.7',
    '-H', 'User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36',
    '-H', 'Sec-Fetch-Dest: document',
    '-H', 'Sec-Fetch-Mode: navigate',
    '-H', 'Sec-Fetch-Site: none',
    '-H', 'Cache-Control: max-age=0',
    pageUrl,
  ], { maxBuffer: 10 * 1024 * 1024 })

  const html = stdout
  console.log(`[${tag}] HTML reçu: ${html.length} octets`)

  // Pattern : href="/[lang/]listings/{uuid}">...<div class="fabkit-Typography-ellipsisWrapper">Titre</div>
  // Les images src sont dans des <img> dans le même bloc mais pas directement liées au href par nesting simple
  // On extrait d'abord tous les couples (slug, titre) puis les images séparément
  const results = []
  const seen = new Set()

  // Regex pour extraire href + titre dans le même tag <a>
  const linkRe = /href="(\/(?:[a-z]{2}\/)?listings\/([a-f0-9-]{36}))"[^>]*>[\s\S]*?<div[^>]*fabkit-Typography-ellipsisWrapper[^>]*>([\s\S]*?)<\/div>/g
  let m
  while ((m = linkRe.exec(html)) !== null) {
    const href = m[1]
    const slug = m[2]
    const title = m[3].replace(/<[^>]+>/g, '').trim()
    if (!slug || !title || seen.has(slug)) continue
    seen.add(slug)
    results.push({ slug, title, url: 'https://www.fab.com/listings/' + slug, thumbnail: '', price: '' })
  }

  // Extraire les images dans l'ordre des blocs de listing (heuristique)
  const imgRe = /src="(https:\/\/media\.fab\.com\/[^"]+)"/g
  const imgs = []
  let im
  while ((im = imgRe.exec(html)) !== null) {
    imgs.push(im[1])
  }
  // Associer une image par listing (les images apparaissent dans l'ordre des cards)
  results.forEach((r, i) => { r.thumbnail = imgs[i] || '' })

  // Extraire les prix (À partir de XX,XX €) dans l'ordre
  const priceRe = /À partir de(?:<!-- -->)?[\s]*([\d,.\s€]+)/g
  const prices = []
  let pm
  while ((pm = priceRe.exec(html)) !== null) {
    prices.push(pm[1].trim())
  }
  results.forEach((r, i) => { r.price = prices[i] || '' })

  console.log(`[${tag}] résultats parsés: ${results.length}`)
  return results
}

// ── GET /api/fab/scrape-seller?seller=Dekogon+Studios&limit=96  (auth) ───
router.get('/scrape-seller', requireAuth, async (req, res) => {
  const { seller, limit = '96' } = req.query
  if (!seller?.trim()) return res.status(400).json({ error: 'seller requis' })

  const sellerEncoded = encodeURIComponent(seller.trim())
  const pageUrl = `https://www.fab.com/sellers/${sellerEncoded}?sort_by=-published_at`

  try {
    const raw = await scrapeFabPage(pageUrl, 'scrape-seller')
    const maxLimit = parseInt(limit, 10) || 96
    const listings = normalizeAssets(raw.slice(0, maxLimit), seller.trim())
    return res.json({ seller: seller.trim(), listings, total: listings.length })
  } catch (err) {
    res.status(503).json({ error: 'Scraping indisponible : ' + err.message })
  }
})

// ── GET /api/fab/search-seller?seller=Dekogon+Studios&q=Ancient+Ruins+VOL.1&first=5  (auth) ──
router.get('/search-seller', requireAuth, async (req, res) => {
  const { seller, q, first = '5' } = req.query
  if (!seller?.trim()) return res.status(400).json({ error: 'seller requis' })
  if (!q?.trim())      return res.status(400).json({ error: 'q requis' })

  const sellerEncoded = encodeURIComponent(seller.trim())
  const pageUrl = `https://www.fab.com/sellers/${sellerEncoded}?q=${encodeURIComponent(q.trim())}`
  const maxResults = parseInt(first, 10) || 5

  try {
    const raw = await scrapeFabPage(pageUrl, 'search-seller')
    const results = normalizeAssets(raw.slice(0, maxResults), seller.trim())
    console.log('[search-seller] résultats finaux:', results.map(r => r.title))
    return res.json({ results, total: results.length, url: pageUrl })
  } catch (err) {
    res.status(503).json({ error: 'Scraping indisponible : ' + err.message })
  }
})

// ── GET /api/fab/match-nextcloud?vendor=Dekogon+Studios  (auth) ───────────
// 1. Scanne Nextcloud → liste des ZIPs du vendor
// 2. Pour chaque ZIP : cherche sur Fab via curl (API search avec seller_username + q)
//    → retourne les 5 premiers candidats Fab pour chaque pack local
router.get('/match-nextcloud', requireAuth, async (req, res) => {
  const { vendor } = req.query
  if (!vendor?.trim()) return res.status(400).json({ error: 'vendor requis' })

  const base = process.env.NEXTCLOUD_ASSET_ROOT || '/unreal_asset'

  // ── Étape 1 : scanner Nextcloud ──────────────────────────────────────
  let zips = []
  try {
    const queue = [base]
    while (queue.length) {
      const dir = queue.shift()
      const entries = await ncListDir(dir).catch(() => [])
      for (const e of entries) {
        if (e.isDir) {
          const segment  = e.path.replace(/\/$/, '').split('/').pop() || ''
          const rootDepth = base.split('/').filter(Boolean).length
          const pathDepth = e.path.split('/').filter(Boolean).length
          if (pathDepth === rootDepth + 1) {
            if (segment.toLowerCase() !== vendor.trim().toLowerCase()) continue
          }
          queue.push(e.path)
        } else if (e.name.toLowerCase().endsWith('.zip')) {
          const relative  = e.path.slice(base.length).replace(/^\//, '')
          const parts     = relative.split('/')
          const fileVendor = parts.length > 1 ? parts[0] : null
          if (fileVendor?.toLowerCase() !== vendor.trim().toLowerCase()) continue
          zips.push({ filename: e.name.replace(/\.zip$/i, ''), path: e.path, size: e.size })
        }
      }
    }
  } catch (err) {
    return res.status(502).json({ error: `Nextcloud inaccessible : ${err.message}` })
  }

  if (zips.length === 0) {
    return res.json({ vendor: vendor.trim(), matches: [], total: 0, message: `Aucun ZIP trouvé pour "${vendor}"` })
  }

  // ── Étape 2 : pour chaque ZIP, chercher les candidats Fab via curl ───
  // URL : https://www.fab.com/i/search?seller_username=Dekogon+Studios&q=Ancient+Ruins+VOL.1&first=5
  const sellerParam = encodeURIComponent(vendor.trim())

  async function searchFab(term) {
    const pageUrl = `https://www.fab.com/sellers/${sellerParam}?q=${encodeURIComponent(term)}`
    try {
      const raw = await scrapeFabPage(pageUrl, `searchFab:${term}`)
      return normalizeAssets(raw.slice(0, 5), vendor.trim())
    } catch (err) {
      console.error(`[searchFab] ERREUR pour "${term}":`, err.message)
      return []
    }
  }

  // Traitement séquentiel avec petite pause pour éviter le rate-limit
  const matches = []
  for (const zip of zips) {
    const searchTerm = filenameToSearchTerm(zip.filename)
    const candidates = await searchFab(searchTerm)

    // Petite pause anti-rate-limit
    await new Promise(r => setTimeout(r, 300))

    matches.push({
      local: {
        filename: zip.filename,
        path: zip.path,
        size: zip.size,
        searchTerm,
      },
      // Les 5 premiers candidats Fab, le premier étant le plus probable
      candidates: candidates.map((c, i) => ({
        rank: i + 1,
        title: c.title,
        url: c.url,
        price: c.price,
        thumbnail: c.thumbnail,
      })),
      // Commodité : premier résultat direct si disponible
      best: candidates[0] ?? null,
      matched: candidates.length > 0,
    })
  }

  matches.sort((a, b) => {
    if (a.matched !== b.matched) return a.matched ? -1 : 1
    return a.local.filename.localeCompare(b.local.filename)
  })

  res.json({
    vendor: vendor.trim(),
    matches,
    total: matches.length,
    matched: matches.filter(m => m.matched).length,
    unmatched: matches.filter(m => !m.matched).length,
  })
})

// ── GET /api/fab/global-search?q=roman+bath&limit=24  (auth) ────────────
// Recherche globale sur https://www.fab.com/search?q=...
// Retourne : [{ id, title, seller, url, price, thumbnail }]
router.get('/global-search', requireAuth, async (req, res) => {
  const { q, limit = '24' } = req.query
  if (!q?.trim()) return res.status(400).json({ error: 'q requis' })

  const maxLimit = Math.min(parseInt(limit, 10) || 24, 100)

  // 1. Microservice Scrapling (Camoufox)
  try {
    const data = await callScraperGlobalSearch(q.trim(), maxLimit)
    return res.json({
      results: data.results || [],
      total: data.total || 0,
      q: q.trim(),
    })
  } catch (scraperErr) {
    console.log('[global-search] fab-scraper indisponible:', scraperErr.message, '— fallback curl')
  }

  // 2. Fallback : API interne Fab /i/search
  try {
    const url = `https://www.fab.com/i/search?q=${encodeURIComponent(q.trim())}&first=${maxLimit}`
    const apiRes = await fetch(url, { headers: XHR_HEADERS })
    if (apiRes.ok) {
      const data = await apiRes.json()
      const raw = data.results || data.listings || []
      const results = raw.map(a => ({
        id: a.uid || a.id || a.slug || '',
        title: a.title || a.name || 'Sans titre',
        seller: a.seller?.name || a.sellerName || '',
        url: `https://www.fab.com/listings/${a.uid || a.id || a.slug}`,
        price: '',
        thumbnail: a.thumbnailUrl || a.thumbnail_url || '',
        publishedAt: a.publishedAt || null,
      }))
      return res.json({ results, total: results.length, q: q.trim() })
    }
  } catch (_) {}

  res.status(503).json({ error: 'Recherche globale indisponible' })
})

export default router
