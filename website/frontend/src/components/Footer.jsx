import { Link } from 'react-router-dom'
import { Github, MessageCircle, Gamepad2 } from 'lucide-react'
import { useLang } from '../context/LanguageContext'
import { useBranding } from '../context/BrandingContext.jsx'
import './Footer.css'

export default function Footer() {
  const { t } = useLang()
  const { branding } = useBranding()
  const year = new Date().getFullYear()
  const siteName = branding.site_name || 'OpenFramework'

  // Liens lus dynamiquement depuis le branding — masques si vides.
  const socials = [
    branding.link_github  && { icon: <Github size={18} />,        href: branding.link_github,  label: 'GitHub' },
    branding.link_sbox    && { icon: <Gamepad2 size={18} />,      href: branding.link_sbox,    label: 's&box' },
    branding.link_discord && { icon: <MessageCircle size={18} />, href: branding.link_discord, label: 'Discord' },
  ].filter(Boolean)

  return (
    <footer className="footer">
      <div className="footer__inner">
        {/* Logo (si fourni) ou nom en texte + baseline */}
        <div className="footer__brand">
          {branding.logo_url ? (
            <img src={branding.logo_url} alt={siteName} className="footer__logo" />
          ) : (
            <span className="footer__sitename">{siteName}</span>
          )}
          <p className="footer__tagline">{branding.description || t('hero.tagline')}</p>
        </div>

        {/* Liens de navigation */}
        <nav className="footer__nav">
          <a href="#features">features</a>
          <a href="#roadmap">roadmap</a>
          <a href="#contact">contact</a>
          <Link to="/members">{t('nav.members')}</Link>
        </nav>

        <div className="footer__divider" />

        {/* Bas de footer */}
        <div className="footer__bottom">
          <span className="footer__copy">
            © {year} {siteName} — {t('footer.rights')}
          </span>
          {socials.length > 0 && (
            <div className="footer__socials">
              {socials.map(s => (
                <a
                  key={s.label}
                  href={s.href}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="footer__social-btn"
                  aria-label={s.label}
                  title={s.label}
                >
                  {s.icon}
                </a>
              ))}
            </div>
          )}
        </div>
      </div>
    </footer>
  )
}
