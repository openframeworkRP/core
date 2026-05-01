import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import './Header.css'
import logo from '../assets/logo.png'
import { useLang } from '../context/LanguageContext'

export default function Header() {
  const [visible, setVisible] = useState(true)
  const [menuOpen, setMenuOpen] = useState(false)
  const lastScrollY = useRef(0)
  const { lang, setLang, t } = useLang()

  useEffect(() => {
    const handleScroll = () => {
      const currentY = window.scrollY
      if (currentY < 60) {
        setVisible(true)
      } else if (currentY < lastScrollY.current) {
        setVisible(true)
      } else if (currentY > lastScrollY.current) {
        setVisible(false)
        setMenuOpen(false)
      }
      lastScrollY.current = currentY
    }

    window.addEventListener('scroll', handleScroll, { passive: true })
    return () => window.removeEventListener('scroll', handleScroll)
  }, [])

  return (
    <>
      <div
        className="header__trigger"
        onMouseEnter={() => setVisible(true)}
      />
      <header
        className={`header${visible ? '' : ' header--hidden'}`}
        onMouseLeave={() => { if (window.scrollY > 60 && !menuOpen) setVisible(false) }}
      >
        {/* ── Desktop ── */}
        <nav className="header__nav header__nav--left">
          <a href="#about">{t('nav.about')}</a>
          <a href="#games">{t('nav.games')}</a>
          <Link to="/devblog">{t('nav.devblog')}</Link>
          <Link to="/roadmap">{t('nav.roadmap')}</Link>
        </nav>

        <div className="header__logo">
          <img src={logo} alt="OpenFramework" />
        </div>

        <nav className="header__nav header__nav--right">
          <Link to="/members">{t('nav.members')}</Link>
          <Link to="/team">{t('nav.jobs')}</Link>
          <a href="#contact">{t('nav.contact')}</a>
        </nav>

        {/* ── Mobile : logo gauche + burger droite ── */}
        <div className="header__mobile">
          <div className="header__logo-mobile">
            <img src={logo} alt="OpenFramework" />
          </div>

          <div className="header__mobile-right">
            {/* Toggle langue */}
            <button
              className="header__lang-btn"
              onClick={() => setLang(lang === 'fr' ? 'en' : 'fr')}
              title={lang === 'fr' ? 'Switch to English' : 'Passer en français'}
            >
              <span className="header__lang-badge">{lang === 'fr' ? 'EN' : 'FR'}</span>
            </button>

            {/* Burger */}
            <button
              className={`header__burger${menuOpen ? ' open' : ''}`}
              onClick={() => setMenuOpen(!menuOpen)}
              aria-label="Menu"
            >
              <span /><span /><span />
            </button>
          </div>
        </div>

        {/* Toggle langue desktop */}
        <div className="header__lang">
          <button
            className="header__lang-btn"
            onClick={() => setLang(lang === 'fr' ? 'en' : 'fr')}
            title={lang === 'fr' ? 'Switch to English' : 'Passer en français'}
          >
            <span className="header__lang-badge">{lang === 'fr' ? 'EN' : 'FR'}</span>
          </button>
        </div>

        {/* ── Menu burger ouvert ── */}
        {menuOpen && (
          <nav className="header__menu-mobile">
            <a href="#about" onClick={() => setMenuOpen(false)}>{t('nav.about')}</a>
            <a href="#games" onClick={() => setMenuOpen(false)}>{t('nav.games')}</a>
            <Link to="/devblog" onClick={() => setMenuOpen(false)}>{t('nav.devblog')}</Link>
            <Link to="/roadmap" onClick={() => setMenuOpen(false)}>{t('nav.roadmap')}</Link>
            <Link to="/members" onClick={() => setMenuOpen(false)}>{t('nav.members')}</Link>
            <Link to="/team" onClick={() => setMenuOpen(false)}>{t('nav.jobs')}</Link>
            <a href="#contact" onClick={() => setMenuOpen(false)}>{t('nav.contact')}</a>
          </nav>
        )}
      </header>
    </>
  )
}
