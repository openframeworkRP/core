import { Outlet, Link, useMatch } from 'react-router-dom'
import { useLang } from '../../context/LanguageContext'
import { usePostTitle } from '../../context/PostTitleContext'
import { useBranding } from '../../context/BrandingContext.jsx'
import './DevBlogLayout.css'

export default function DevBlogLayout() {
  const { lang, setLang } = useLang()
  const { postTitle } = usePostTitle()
  const { branding } = useBranding()
  const postMatch = useMatch('/devblog/:slug')
  const slug = postMatch?.params?.slug ?? null
  const siteName = branding.site_name || 'OpenFramework'

  return (
    <div className="dblayout">
      {/* ── Header minimal ── */}
      <header className="dblayout__header">
        <div className="dblayout__left">
          <Link to="/" className="dblayout__logo">
            {branding.logo_url
              ? <img src={branding.logo_url} alt={siteName} />
              : <span className="dblayout__sitename">{siteName}</span>
            }
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
