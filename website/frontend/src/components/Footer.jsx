import { Link } from 'react-router-dom'
import { Github, MessageCircle, Gamepad2 } from 'lucide-react'
import logo from '../assets/logo.png'
import { useLang } from '../context/LanguageContext'
import './Footer.css'

const SOCIALS = [
  { icon: <Github size={18} />,       href: 'https://github.com/openframework', label: 'GitHub' },
  { icon: <Gamepad2 size={18} />,     href: 'https://sbox.game/openframework', label: 'S&Box' },
  { icon: <MessageCircle size={18} />,href: 'https://discord.gg/TXwvYJaGCv', label: 'Discord' },
]

export default function Footer() {
  const { t } = useLang()
  const year = new Date().getFullYear()

  return (
    <footer className="footer">
      <div className="footer__inner">
        {/* Logo + baseline */}
        <div className="footer__brand">
          <img src={logo} alt="Small Box Studio" className="footer__logo" />
          <p className="footer__tagline">{t('hero.tagline')}</p>
        </div>

        {/* Liens de navigation */}
        <nav className="footer__nav">
          <a href="#about">{t('nav.about')}</a>
          <a href="#games">{t('nav.games')}</a>
          <Link to="/devblog">{t('nav.devblog')}</Link>
          <Link to="/members">{t('nav.members')}</Link>
          <Link to="/team">{t('nav.jobs')}</Link>
          <a href="#contact">{t('nav.contact')}</a>
        </nav>

        <div className="footer__divider" />

        {/* Bas de footer */}
        <div className="footer__bottom">
          <span className="footer__copy">
            © {year} S&amp;Box Studio — {t('footer.rights')}
          </span>
          <div className="footer__socials">
            {SOCIALS.map(s => (
              <a
                key={s.label}
                href={s.href}
                target={s.href.startsWith('mailto') ? undefined : '_blank'}
                rel="noopener noreferrer"
                className="footer__social-btn"
                aria-label={s.label}
                title={s.label}
              >
                {s.icon}
              </a>
            ))}
          </div>
        </div>
      </div>
    </footer>
  )
}
