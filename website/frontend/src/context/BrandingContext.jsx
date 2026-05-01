// ============================================================
// BrandingContext — config visuelle (logo, couleurs, nom)
// ============================================================
// Au mount : fetch /api/branding et expose la config dans un Context
// React. Applique aussi automatiquement les CSS variables au :root
// pour que les composants puissent utiliser var(--brand-primary) etc.
// ============================================================

import { createContext, useContext, useEffect, useState, useCallback } from 'react'

const DEFAULT_BRANDING = {
  site_name:       'OpenFramework',
  site_short_name: 'OpenFramework',
  default_author:  'OpenFramework',
  description:     'Framework open source pour s&box — clone, configure, joue.',
  primary_color:   '#3cadd9',
  accent_color:    '#88e1ff',
  logo_url:        '',
  favicon_url:     '',
  link_github:     'https://github.com/openframeworkRP/core',
  link_sbox:       'https://sbox.game/openframework',
  link_discord:    '',
  link_steam:      '',
}

const BrandingContext = createContext({
  branding: DEFAULT_BRANDING,
  loading:  true,
  refresh:  async () => {},
  save:     async () => {},
})

export function useBranding() {
  return useContext(BrandingContext)
}

function applyToDocument(branding) {
  const root = document.documentElement

  // CSS variables — les composants peuvent les utiliser directement.
  if (branding.primary_color) {
    root.style.setProperty('--brand-primary', branding.primary_color)
  }
  if (branding.accent_color) {
    root.style.setProperty('--brand-accent', branding.accent_color)
  }

  // Title initial — chaque page peut l'ecraser via Helmet/SEO.jsx
  if (branding.site_name) {
    document.title = branding.site_name
  }

  // Favicon dynamique
  if (branding.favicon_url) {
    let link = document.querySelector('link[rel="icon"]')
    if (!link) {
      link = document.createElement('link')
      link.rel = 'icon'
      document.head.appendChild(link)
    }
    link.href = branding.favicon_url
  }
}

export function BrandingProvider({ children }) {
  const [branding, setBranding] = useState(DEFAULT_BRANDING)
  const [loading, setLoading]   = useState(true)

  const refresh = useCallback(async () => {
    try {
      const r = await fetch('/api/branding')
      if (!r.ok) return
      const data = await r.json()
      // Merge avec les defaults pour combler les cles manquantes
      const merged = { ...DEFAULT_BRANDING, ...data }
      setBranding(merged)
      applyToDocument(merged)
    } catch {
      // Backend down ? On garde les defaults, pas critique au boot.
    } finally {
      setLoading(false)
    }
  }, [])

  const save = useCallback(async (updates) => {
    const r = await fetch('/api/branding', {
      method: 'PUT',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(updates),
    })
    if (!r.ok) {
      const err = await r.json().catch(() => ({}))
      throw new Error(err.error || `save-failed (HTTP ${r.status})`)
    }
    await refresh()
    return r.json()
  }, [refresh])

  useEffect(() => { refresh() }, [refresh])

  return (
    <BrandingContext.Provider value={{ branding, loading, refresh, save }}>
      {children}
    </BrandingContext.Provider>
  )
}
