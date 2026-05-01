import { useSearchParams } from 'react-router-dom'
import { Link } from 'react-router-dom'
import { usePosts, useGames } from '../../hooks/useDevblog.js'
import { useLang } from '../../context/LanguageContext'
import SEO from '../SEO'
import './DevBlogList.css'

function formatMonth(isoMonth, lang) {
  const [year, month] = isoMonth.split('-')
  const date = new Date(Number(year), Number(month) - 1, 1)
  return date.toLocaleDateString(lang === 'fr' ? 'fr-FR' : 'en-US', {
    month: 'long',
    year: 'numeric',
  })
}

function BlogCard({ post }) {
  const { lang } = useLang()
  const title   = lang === 'fr' ? post.title_fr   : post.title_en
  const excerpt = lang === 'fr' ? post.excerpt_fr : post.excerpt_en
  const cover   = post.cover

  return (
    <Link to={`/devblog/${post.slug}`} className="dbl__card">
      <div className="dbl__card-cover dbl__card-cover--global">
        {cover
          ? <img src={cover} alt={title} />
          : (
            <div className="dbl__card-cover-placeholder">
              <span className="dbl__card-cover-icon">📝</span>
            </div>
          )
        }
        <div className="dbl__card-cover-overlay" />
        <div className="dbl__card-cover-meta">
          <span className="dbl__month">{formatMonth(post.month, lang)}</span>
        </div>
      </div>

      <div className="dbl__card-body">
        <h3 className="dbl__card-title">{title}</h3>
        <p className="dbl__card-excerpt">{excerpt}</p>

        <div className="dbl__card-games">
          {(post.games ?? []).map(g => (
            <span key={g.slug} className={`dbl__game-pill dbl__game-pill--${g.slug}`}
              style={g.color ? { background: g.color } : {}}>
              {lang === 'fr' ? g.label_fr : g.label_en}
            </span>
          ))}
        </div>

        <div className="dbl__card-footer">
          <span className="dbl__read-time">
            {post.read_time} min {lang === 'fr' ? 'de lecture' : 'read'}
          </span>
        </div>
      </div>
    </Link>
  )
}

export default function DevBlogList() {
  const { lang, t }   = useLang()
  const { posts, loading, error } = usePosts()
  const { games }     = useGames()
  const [searchParams, setSearchParams] = useSearchParams()
  const activeGame = searchParams.get('s') ?? 'all'

  function setActiveGame(slug) {
    if (slug === 'all') setSearchParams({}, { replace: true })
    else setSearchParams({ s: slug }, { replace: true })
  }

  const filtered = activeGame === 'all'
    ? posts
    : posts.filter(p => (p.games ?? []).some(g => g.slug === activeGame))

  const sorted = [...filtered].sort((a, b) => b.month.localeCompare(a.month))

  return (
    <div className="dbl">
      <SEO
        title={lang === 'fr' ? 'DevBlog' : 'DevBlog'}
        description={lang === 'fr'
          ? 'Suivez le développement de OpenFramework. Chaque mois, des articles sur nos jeux S&Box, nos avancées et nos galères.'
          : 'Follow OpenFramework development. Monthly articles about our S&Box games, progress and challenges.'}
        url="/devblog"
        lang={lang}
      />
      <header className="dbl__hero">
        <div className="dbl__hero-inner">
          <span className="dbl__eyebrow">{t('devblog.subtitle')}</span>
          <h1 className="dbl__hero-title">{t('devblog.title')}</h1>
          <p className="dbl__hero-intro">{t('devblog.intro')}</p>
        </div>
        <div className="dbl__hero-bg" aria-hidden="true" />
      </header>

      <div className="dbl__filters-bar">
        <div className="dbl__filters">
          {games.map(game => (
            <button
              key={game.slug}
              className={`dbl__filter-btn${activeGame === game.slug ? ' dbl__filter-btn--active' : ''}`}
              onClick={() => setActiveGame(game.slug)}
            >
              {lang === 'fr' ? game.label_fr : game.label_en}
            </button>
          ))}
        </div>
        <span className="dbl__count">
          {sorted.length} devlog{sorted.length !== 1 ? 's' : ''}
        </span>
      </div>

      <main className="dbl__main">
        {error   && <p className="dbl__empty">API error</p>}
        {loading && <p className="dbl__empty">Chargement…</p>}
        {!loading && !error && sorted.length === 0 && (
          <p className="dbl__empty">{t('devblog.empty')}</p>
        )}
        {!loading && !error && sorted.length > 0 && (
          <div className="dbl__grid">
            {sorted.map(post => (
              <BlogCard key={post.id} post={post} />
            ))}
          </div>
        )}
      </main>
    </div>
  )
}
