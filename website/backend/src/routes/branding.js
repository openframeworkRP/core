// ============================================================
// /api/branding — config visuelle (logo, couleurs, nom)
// ============================================================
// GET  : public (lu par le frontend au boot pour appliquer le theme)
// PUT  : owner only (depuis le panel admin ou le wizard)
// ============================================================

import { Router } from 'express'
import { requireAuth, requireRole } from '../auth.js'
import db from '../db.js'

const router = Router()

// Cles autorisees a etre ecrites via /api/branding (whitelist).
const ALLOWED_KEYS = new Set([
  'site_name',
  'site_short_name',
  'default_author',
  'description',
  'primary_color',
  'accent_color',
  'logo_url',
  'favicon_url',
  'link_github',
  'link_sbox',
  'link_discord',
  'link_steam',
])

// ── GET /api/branding (public) ──────────────────────────────────────────
router.get('/', (_req, res) => {
  const rows = db.prepare('SELECT key, value FROM branding').all()
  const branding = {}
  for (const { key, value } of rows) {
    branding[key] = value || ''
  }
  res.json(branding)
})

// ── PUT /api/branding (owner only) ──────────────────────────────────────
// Body : { site_name, primary_color, ... } — partial update accepte.
router.put('/', requireAuth, requireRole('owner'), (req, res) => {
  const updates = req.body || {}
  const upsert = db.prepare(`
    INSERT INTO branding (key, value) VALUES (?, ?)
    ON CONFLICT(key) DO UPDATE SET value = excluded.value
  `)

  const applied = {}
  const skipped = []
  const invalidColor = (v) => v && !/^#[0-9a-fA-F]{3,8}$/.test(v)
  const invalidUrl   = (v) => v && !/^(https?:)?\/\/|^\/|^data:image\//.test(v)

  for (const [key, rawValue] of Object.entries(updates)) {
    if (!ALLOWED_KEYS.has(key)) { skipped.push({ key, reason: 'not-allowed' }); continue }

    const value = String(rawValue ?? '').trim().slice(0, 1024)

    // Validation legere par cle
    if ((key === 'primary_color' || key === 'accent_color') && invalidColor(value)) {
      skipped.push({ key, reason: 'invalid-color', hint: 'format attendu : #RRGGBB ou #RGB' })
      continue
    }
    if ((key === 'logo_url' || key === 'favicon_url') && invalidUrl(value)) {
      skipped.push({ key, reason: 'invalid-url', hint: 'URL absolue (http(s)://) ou relative (/...)' })
      continue
    }
    // Pour les liens externes : doivent etre des URLs absolues (ou vide)
    if (key.startsWith('link_') && value && !/^https?:\/\//.test(value)) {
      skipped.push({ key, reason: 'invalid-link', hint: 'URL absolue obligatoire (https://...)' })
      continue
    }

    upsert.run(key, value)
    applied[key] = value
  }

  res.json({ ok: true, applied, skipped })
})

export default router
