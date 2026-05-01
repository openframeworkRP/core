// ============================================================
// SimpleHero — hero minimaliste style sbox.game
// ============================================================
// Centré, sombre, 2 CTA. Aucune surcouche parallax — reste sober.
// ============================================================

import { Github, ExternalLink } from 'lucide-react'
import { useBranding } from '../context/BrandingContext.jsx'
import './SimpleHero.css'

export default function SimpleHero() {
  const { branding } = useBranding()
  const siteName = branding.site_name || 'OpenFramework'
  const description = branding.description || 'Framework open source pour s&box — clone, configure, joue.'

  return (
    <section className="hero">
      <div className="hero__inner">
        <h1 className="hero__title">
          {siteName}
          <span className="hero__title-accent">.</span>
        </h1>

        <p className="hero__description">
          {description}
        </p>

        <p className="hero__catch">
          Open source. No royalties. Clone it, ship it.
        </p>

        <div className="hero__cta">
          <a
            href="https://sbox.game/openframework"
            target="_blank"
            rel="noopener noreferrer"
            className="hero__btn hero__btn--primary"
          >
            <ExternalLink size={16} /> s&amp;box
          </a>
          <a
            href="https://github.com/openframeworkRP"
            target="_blank"
            rel="noopener noreferrer"
            className="hero__btn"
          >
            <Github size={16} /> github
          </a>
          <a href="#about" className="hero__link">
            learn more →
          </a>
        </div>
      </div>
    </section>
  )
}
