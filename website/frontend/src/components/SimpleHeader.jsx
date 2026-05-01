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
        <a href="#features">features</a>
        <a href="#roadmap">roadmap</a>
        <a href="#contact">contact</a>
        {branding.link_sbox && (
          <a href={branding.link_sbox} target="_blank" rel="noopener noreferrer">s&amp;box</a>
        )}
        {branding.link_github && (
          <a href={branding.link_github} target="_blank" rel="noopener noreferrer">github</a>
        )}
        {branding.link_discord && (
          <a href={branding.link_discord} target="_blank" rel="noopener noreferrer">discord</a>
        )}
      </nav>
    </header>
  )
}
