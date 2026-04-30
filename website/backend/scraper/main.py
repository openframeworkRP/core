"""
fab_scraper — microservice FastAPI
Scrape Fab.com en bypassant Cloudflare avec Scrapling + StealthyFetcher (Camoufox).

Endpoints :
  GET /health
  GET /seller?seller=Dekogon+Studios&limit=96&sort_by=-published_at
  GET /search?seller=Dekogon+Studios&q=Ancient+Ruins&limit=5
  GET /listing?url=https://www.fab.com/listings/...
  GET /preview?url=https://www.fab.com/listings/...   (alias /listing)
"""

import re
import json
import asyncio
import logging
from contextlib import asynccontextmanager
from concurrent.futures import ThreadPoolExecutor

from fastapi import FastAPI, Query, HTTPException
from scrapling.fetchers import StealthyFetcher

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
log = logging.getLogger("fab_scraper")

# Executor dédié pour les appels Scrapling (synchrones, bloquants)
_executor = ThreadPoolExecutor(max_workers=3)


@asynccontextmanager
async def lifespan(app: FastAPI):
    log.info("Démarrage — préchauffage Camoufox...")
    loop = asyncio.get_event_loop()
    try:
        await loop.run_in_executor(_executor, _warmup)
        log.info("Camoufox prêt.")
    except Exception as e:
        log.warning(f"Warm-up ignoré: {e}")
    yield
    _executor.shutdown(wait=False)


def _warmup():
    try:
        StealthyFetcher.fetch(
            "https://httpbin.org/get",
            headless=True,
            block_images=True,
            network_idle=False,
        )
    except Exception:
        pass


app = FastAPI(title="Fab Scraper", version="1.0.0", lifespan=lifespan)


# ── Helpers ───────────────────────────────────────────────────────────────

def _norm_price(p) -> str:
    if p is None:
        return ""
    if isinstance(p, dict):
        amount = p.get("amount", 0)
        currency = p.get("currency", "USD")
        return f"{amount / 100:.2f} {currency}"
    return str(p)


def _normalize_asset(a: dict, seller: str) -> dict:
    slug = a.get("slug") or a.get("uid") or a.get("id") or ""
    url = f"https://www.fab.com/listings/{slug}" if slug else f"https://www.fab.com/sellers/{seller}"
    price = a.get("price") or a.get("personalLicense", {}).get("price") or ""
    if isinstance(price, dict):
        price = _norm_price(price)
    elif not price:
        licenses = a.get("licenses") or []
        for lic in licenses:
            if re.search(r"personal", lic.get("type", "") + lic.get("name", ""), re.I):
                price = _norm_price(lic.get("price"))
                break
    thumbnail = a.get("thumbnailUrl") or a.get("thumbnail_url") or a.get("thumbnail") or None
    if isinstance(thumbnail, dict):
        thumbnail = thumbnail.get("url")
    if not thumbnail:
        images = a.get("images") or []
        if images:
            thumbnail = images[0] if isinstance(images[0], str) else (images[0] or {}).get("url")
    return {
        "id": a.get("uid") or a.get("id") or slug,
        "title": a.get("title") or a.get("name") or "Sans titre",
        "url": url,
        "price": price or "Gratuit",
        "thumbnail": thumbnail or "",
        "publishedAt": a.get("publishedAt") or a.get("published_at") or a.get("createdAt") or None,
    }


def _fetch_sync(url: str, network_idle: bool = True):
    log.info(f"[fetch] {url}")
    page = StealthyFetcher.fetch(
        url,
        headless=True,
        solve_cloudflare=True,
        network_idle=network_idle,
        block_images=False,
        disable_resources=False,
    )
    log.info(f"[fetch] OK — {len(page.html_content)} bytes")
    return page


async def _fetch_page(url: str, network_idle: bool = True):
    loop = asyncio.get_event_loop()
    return await loop.run_in_executor(_executor, lambda: _fetch_sync(url, network_idle))


# ── Parseurs ──────────────────────────────────────────────────────────────

def _parse_listings_from_page(page, seller: str) -> list[dict]:
    results = []
    seen = set()
    html = page.html_content

    # 1. __NEXT_DATA__
    nd_match = re.search(r'<script[^>]+id="__NEXT_DATA__"[^>]*>([\s\S]*?)</script>', html)
    if nd_match:
        try:
            nd = json.loads(nd_match.group(1))
            props = nd.get("props", {}).get("pageProps", {})
            raw = (
                props.get("listings", {}).get("results")
                or props.get("listings")
                or props.get("initialListings", {}).get("results")
                or props.get("initialListings")
            )
            if isinstance(raw, list) and raw:
                for a in raw:
                    asset = _normalize_asset(a, seller)
                    if asset["id"] not in seen:
                        seen.add(asset["id"])
                        results.append(asset)
                log.info(f"__NEXT_DATA__: {len(results)} listings")
                return results
        except Exception as e:
            log.warning(f"__NEXT_DATA__ parse error: {e}")

    # 2. CSS selectors Scrapling
    try:
        links = page.css('a[href*="/listings/"]')
        imgs = [el.attrib.get("src", "") for el in page.css('img[src*="media.fab.com"]')]
        for i, link in enumerate(links):
            href = link.attrib.get("href", "")
            slug_m = re.search(r'/listings/([a-f0-9\-]{36})', href)
            if not slug_m:
                continue
            slug = slug_m.group(1)
            if slug in seen:
                continue
            title_el = link.css('[class*="ellipsisWrapper"]')
            title = title_el[0].text if title_el else link.text
            title = re.sub(r'\s+', ' ', title or "").strip()
            if not title:
                continue
            seen.add(slug)
            results.append({
                "id": slug,
                "title": title,
                "url": f"https://www.fab.com/listings/{slug}",
                "price": "",
                "thumbnail": imgs[i] if i < len(imgs) else "",
                "publishedAt": None,
            })
        if results:
            log.info(f"CSS: {len(results)} listings")
            return results
    except Exception as e:
        log.warning(f"CSS error: {e}")

    # 3. Regex fallback
    link_re = re.compile(
        r'href="/((?:[a-z]{2}/)?listings/([a-f0-9\-]{36}))"[^>]*>[\s\S]*?'
        r'<div[^>]*fabkit-Typography-ellipsisWrapper[^>]*>([\s\S]*?)</div>'
    )
    imgs_re = re.compile(r'src="(https://media\.fab\.com/[^"]+)"')
    imgs = imgs_re.findall(html)
    for i, m in enumerate(link_re.finditer(html)):
        slug = m.group(2)
        title = re.sub(r"<[^>]+>", "", m.group(3)).strip()
        if not slug or not title or slug in seen:
            continue
        seen.add(slug)
        results.append({
            "id": slug, "title": title,
            "url": f"https://www.fab.com/listings/{slug}",
            "price": "", "thumbnail": imgs[i] if i < len(imgs) else "",
            "publishedAt": None,
        })
    log.info(f"regex: {len(results)} listings")
    return results


def _parse_listing_detail(page, url: str) -> dict:
    html = page.html_content
    name = price = image_url = description = studio = ""

    # 1. CSS Scrapling (JS rendu)
    try:
        h1 = page.css("h1")
        if h1:
            name = re.sub(r'\s+', ' ', h1[0].text or "").strip()

        img = page.css('[class*="fabkit-Thumbnail"] img')
        if img:
            image_url = img[0].attrib.get("src", "")

        # Studio = div.fabkit-Badge-label à l'intérieur d'un lien /sellers/
        # Structure : <a href="/sellers/..."><div class="fabkit-Avatar..."></div><div class="fabkit-Badge-label">Leartes Studios</div></a>
        seller_links = page.css('a[href*="/sellers/"]')
        for sl in seller_links:
            href = sl.attrib.get("href", "")
            # Exclure les liens /sellers/.../about ou /sellers/.../listings
            if re.search(r'/sellers/[^/]+(?:/|$)', href) and "/about" not in href:
                label = sl.css('[class*="fabkit-Badge-label"]')
                if label:
                    txt = re.sub(r'\s+', ' ', label[0].text or "").strip()
                    if txt:
                        studio = txt
                        break

        log.info(f"CSS detail: name={name!r} studio={studio!r}")
    except Exception as e:
        log.warning(f"CSS detail error: {e}")

    # 2. JSON-LD (prioritaire pour le prix : range lowPrice/highPrice)
    for ld_match in re.finditer(
        r'<script[^>]+type="application/ld\+json"[^>]*>([\s\S]*?)</script>', html
    ):
        try:
            ld = json.loads(ld_match.group(1))
            if ld.get("@type") != "Product":
                continue
            if not name:
                name = ld.get("name", "")
            if not image_url:
                image_url = ld.get("image", "")
            if not description:
                description = ld.get("description", "")
            offers = ld.get("offers")
            if offers:
                offer = offers[0] if isinstance(offers, list) else offers
                lo = offer.get("lowPrice") or offer.get("price")
                hi = offer.get("highPrice")
                curr = offer.get("priceCurrency", "USD")
                if lo is not None and hi is not None and str(lo) != str(hi):
                    price = f"De {float(lo):.2f} € à {float(hi):.2f} €"
                elif lo is not None:
                    price = f"{float(lo):.2f} {curr}"
            break
        except Exception:
            pass

    # 2b. CSS fallback pour le prix si JSON-LD n'a rien donné
    if not price:
        try:
            for el in page.css('[class*="fabkit-Text"]'):
                txt = (el.text or "").replace("\xa0", " ").strip()
                if ("€" in txt or "$" in txt) and re.search(r'\d', txt):
                    price = txt
                    break
        except Exception:
            pass

    # 3. __NEXT_DATA__ fallback
    if not name:
        nd_match = re.search(r'<script[^>]+id="__NEXT_DATA__"[^>]*>([\s\S]*?)</script>', html)
        if nd_match:
            try:
                nd = json.loads(nd_match.group(1))
                listing = (
                    nd.get("props", {}).get("pageProps", {}).get("listing")
                    or nd.get("props", {}).get("pageProps", {}).get("initialListing")
                )
                if listing:
                    if not name:
                        name = listing.get("title") or listing.get("name") or ""
                    if not description:
                        description = listing.get("description") or listing.get("short_description") or ""
                    if not image_url:
                        image_url = listing.get("thumbnailUrl") or listing.get("thumbnail_url") or ""
                    if not price:
                        p = listing.get("price") or listing.get("personalLicense", {}).get("price")
                        if p:
                            price = _norm_price(p)
            except Exception:
                pass

    # 4. OG tags last resort
    if not name:
        m = re.search(r'<meta[^>]+property="og:title"[^>]+content="([^"]+)"', html)
        name = m.group(1).strip() if m else ""
    if not image_url:
        m = re.search(r'<meta[^>]+property="og:image"[^>]+content="([^"]+)"', html)
        image_url = m.group(1).strip() if m else ""
    if not description:
        m = re.search(r'<meta[^>]+property="og:description"[^>]+content="([^"]+)"', html)
        description = m.group(1).strip() if m else ""

    slug_m = re.search(r'/listings/([a-f0-9\-]{36})', url)
    canonical = f"https://www.fab.com/listings/{slug_m.group(1)}" if slug_m else url

    return {
        "name": name or "Sans titre",
        "description": description,
        "imageUrl": image_url,
        "price": price or "Prix non disponible",
        "studio": studio,
        "url": canonical,
    }


def _parse_global_search(page, q: str) -> list[dict]:
    """
    Parse les résultats de https://www.fab.com/search?q=...
    Structure HTML de chaque item :
      <a aria-label="TITRE par SELLER" href="/fr/listings/UUID">  ← titre + slug
      <a href="/fr/sellers/..."><div class="fabkit-Typography-ellipsisWrapper">SELLER</div></a>
      <div class="fabkit-Text--sm fabkit-Text--regular">PRIX</div>  (après le badge discount éventuel)
      <img src="https://media.fab.com/...">  ← dans .fabkit-Thumbnail-root
    """
    results = []
    seen = set()
    html = page.html_content

    # 1. __NEXT_DATA__ (plus fiable)
    nd_match = re.search(r'<script[^>]+id="__NEXT_DATA__"[^>]*>([\s\S]*?)</script>', html)
    if nd_match:
        try:
            nd = json.loads(nd_match.group(1))
            props = nd.get("props", {}).get("pageProps", {})
            raw = (
                props.get("listings", {}).get("results")
                or props.get("searchResults", {}).get("results")
                or props.get("results")
            )
            if isinstance(raw, list) and raw:
                for a in raw:
                    asset = _normalize_asset(a, "")
                    if asset["id"] not in seen:
                        seen.add(asset["id"])
                        results.append(asset)
                log.info(f"[global-search] __NEXT_DATA__: {len(results)} résultats")
                return results
        except Exception as e:
            log.warning(f"[global-search] __NEXT_DATA__ error: {e}")

    # 2. CSS Scrapling — parse chaque card .nTa5u2sc
    try:
        # Tous les liens de listing avec aria-label="TITRE par SELLER"
        listing_links = page.css('a[href*="/listings/"][aria-label]')
        # Toutes les thumbnails dans l'ordre
        thumbnails = page.css('.fabkit-Thumbnail-root img[src*="media.fab.com"]')

        for i, link in enumerate(listing_links):
            href = link.attrib.get("href", "")
            slug_m = re.search(r'/listings/([a-f0-9\-]{36})', href)
            if not slug_m:
                continue
            slug = slug_m.group(1)
            if slug in seen:
                continue

            # Titre depuis l'ellipsisWrapper ou aria-label
            title_el = link.css('[class*="ellipsisWrapper"]')
            if title_el:
                title = re.sub(r'\s+', ' ', title_el[0].text or "").strip()
            else:
                aria = link.attrib.get("aria-label", "")
                title = aria.split(" par ")[0].strip() if " par " in aria else aria.strip()

            if not title:
                continue

            # Seller : lien /sellers/ le plus proche (frère dans le parent)
            seller_name = ""
            aria = link.attrib.get("aria-label", "")
            if " par " in aria:
                seller_name = aria.split(" par ", 1)[1].strip()

            # Prix : chercher dans les siblings — texte contenant € ou chiffre
            price = ""
            # On cherche dans le HTML autour du slug
            card_re = re.search(
                rf'href="[^"]*{slug}[^"]*"[\s\S]{{0,2000}}?'
                r'(?:À partir de\s*</[^>]+>\s*)?'
                r'<div[^>]*fabkit-Text--sm[^>]*fabkit-Text--regular[^>]*>([\d\s,\.]+(?:&nbsp;)?[€$][^<]*)</div>',
                html
            )
            if card_re:
                price = re.sub(r'[\s\xa0]+', '\u00a0', card_re.group(1).replace("&nbsp;", "\u00a0")).strip()

            thumbnail = thumbnails[i].attrib.get("src", "") if i < len(thumbnails) else ""

            seen.add(slug)
            results.append({
                "id": slug,
                "title": title,
                "seller": seller_name,
                "url": f"https://www.fab.com/listings/{slug}",
                "price": price,
                "thumbnail": thumbnail,
                "publishedAt": None,
            })

        if results:
            log.info(f"[global-search] CSS: {len(results)} résultats")
            return results
    except Exception as e:
        log.warning(f"[global-search] CSS error: {e}")

    # 3. Regex fallback
    aria_re = re.compile(
        r'aria-label="([^"]+) par ([^"]+)"\s+href="[^"]*listings/([a-f0-9\-]{36})'
    )
    img_re = re.compile(r'src="(https://media\.fab\.com/[^"]+)"')
    imgs = img_re.findall(html)
    for i, m in enumerate(aria_re.finditer(html)):
        title, seller_name, slug = m.group(1).strip(), m.group(2).strip(), m.group(3)
        if slug in seen:
            continue
        seen.add(slug)
        results.append({
            "id": slug,
            "title": title,
            "seller": seller_name,
            "url": f"https://www.fab.com/listings/{slug}",
            "price": "",
            "thumbnail": imgs[i] if i < len(imgs) else "",
            "publishedAt": None,
        })
    log.info(f"[global-search] regex: {len(results)} résultats")
    return results


# ── Routes ────────────────────────────────────────────────────────────────

@app.get("/health")
async def health():
    return {"status": "ok"}


@app.get("/seller")
async def get_seller(
    seller: str = Query(...),
    limit: int = Query(96, ge=1, le=200),
    sort_by: str = Query("-published_at"),
):
    seller_enc = seller.strip().replace(" ", "%20")
    url = f"https://www.fab.com/sellers/{seller_enc}?sort_by={sort_by}"
    try:
        page = await _fetch_page(url)
        listings = _parse_listings_from_page(page, seller.strip())
        return {"seller": seller.strip(), "listings": listings[:limit], "total": len(listings[:limit])}
    except Exception as e:
        log.error(f"[/seller] {e}")
        raise HTTPException(status_code=503, detail=str(e))


@app.get("/search")
async def search_seller(
    seller: str = Query(...),
    q: str = Query(...),
    limit: int = Query(5, ge=1, le=50),
):
    seller_enc = seller.strip().replace(" ", "%20")
    url = f"https://www.fab.com/sellers/{seller_enc}?q={q.strip().replace(' ', '%20')}"
    try:
        page = await _fetch_page(url)
        results = _parse_listings_from_page(page, seller.strip())
        return {"results": results[:limit], "total": len(results[:limit]), "url": url}
    except Exception as e:
        log.error(f"[/search] {e}")
        raise HTTPException(status_code=503, detail=str(e))


@app.get("/listing")
async def get_listing(url: str = Query(...)):
    if "/listings/" not in url:
        raise HTTPException(status_code=400, detail="URL invalide (doit contenir /listings/)")
    try:
        page = await _fetch_page(url, network_idle=True)
        return _parse_listing_detail(page, url)
    except Exception as e:
        log.error(f"[/listing] {e}")
        raise HTTPException(status_code=503, detail=str(e))


@app.get("/preview")
async def preview(url: str = Query(...)):
    return await get_listing(url=url)


@app.get("/global-search")
async def global_search(
    q: str = Query(...),
    limit: int = Query(24, ge=1, le=100),
):
    url = f"https://www.fab.com/search?q={q.strip().replace(' ', '%20')}"
    try:
        page = await _fetch_page(url, network_idle=True)
        results = _parse_global_search(page, q.strip())
        return {"results": results[:limit], "total": len(results[:limit]), "q": q.strip(), "url": url}
    except Exception as e:
        log.error(f"[/global-search] {e}")
        raise HTTPException(status_code=503, detail=str(e))
