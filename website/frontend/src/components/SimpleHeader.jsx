// ============================================================
// SimpleHeader — header minimaliste style sbox.game
// ============================================================

import { Link } from 'react-router-dom'
import { useBranding } from '../context/BrandingContext.jsx'
import './SimpleHeader.css'

export default function SimpleHeader() {
  const { branding } = useBranding()
  const logoUrl  = branding.logo_url
  const siteName = branding.site_short_name || branding.site_name || 'OpenFramework'

  return (
    <header className="sh">
      <Link to="/" className="sh__logo">
        {logoUrl ? (
          <img src={logoUrl} alt={siteName} />
        ) : (
          <span className="sh__logo-text">{siteName.charAt(0)}</span>
        )}
      </Link>
      <nav className="sh__nav">
        <a href="#about">about</a>
        <a href="#games">games</a>
        <a href="#contact">contact</a>
        <Link to="/devblog">blog</Link>
        <Link to="/roadmap">roadmap</Link>
        <Link to="/team">team</Link>
        <a href="https://sbox.game/openframework" target="_blank" rel="noopener noreferrer">s&amp;box</a>
        <a href="https://github.com/openframeworkRP" target="_blank" rel="noopener noreferrer">github</a>
      </nav>
    </header>
  )
}
