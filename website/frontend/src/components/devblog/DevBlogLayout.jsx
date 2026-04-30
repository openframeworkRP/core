import { Outlet, Link, useMatch } from 'react-router-dom'
import { useLang } from '../../context/LanguageContext'
import { usePostTitle } from '../../context/PostTitleContext'
import logo from '../../assets/logo.png'
import './DevBlogLayout.css'

export default function DevBlogLayout() {
  const { lang, setLang } = useLang()
  const { postTitle } = usePostTitle()
  const postMatch = useMatch('/devblog/:slug')
  const slug = postMatch?.params?.slug ?? null

  return (
    <div className="dblayout">
      {/* ── Header minimal ── */}
      <header className="dblayout__header">
        <div className="dblayout__left">
          <Link to="/" className="dblayout__logo">
            <img src={logo} alt="Small Box Studio" />
          </Link>

          <nav className="dblayout__nav">
            <Link to="/" className="dblayout__nav-link">
              {lang === 'fr' ? 'Accueil' : 'Home'}
            </Link>
            <span className="dblayout__nav-sep">›</span>
            <Link
              to="/devblog"
              className={`dblayout__nav-link${!slug ? ' dblayout__nav-link--active' : ''}`}
            >
              Devlogs
            </Link>
            {slug && postTitle && (
              <>
                <span className="dblayout__nav-sep">›</span>
                <span className="dblayout__nav-current">{postTitle}</span>
              </>
            )}
          </nav>
        </div>

        <button
          className="dblayout__lang-btn"
          onClick={() => setLang(lang === 'fr' ? 'en' : 'fr')}
          title={lang === 'fr' ? 'Switch to English' : 'Passer en français'}
        >
          {lang === 'fr' ? '🇬🇧' : '🇫🇷'}
        </button>
      </header>

      {/* ── Page ── */}
      <Outlet />
    </div>
  )
}
