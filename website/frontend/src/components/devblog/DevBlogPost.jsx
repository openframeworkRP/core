import { useEffect, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { usePost, usePosts } from '../../hooks/useDevblog.js'
import { useLang } from '../../context/LanguageContext'
import { usePostTitle } from '../../context/PostTitleContext'
import BlockRenderer from './BlockRenderer'
import SEO from '../SEO'
import './DevBlogPost.css'

function formatDate(isoMonth, lang) {
  const [year, month] = isoMonth.split('-')
  const date = new Date(Number(year), Number(month) - 1, 1)
  return date.toLocaleDateString(lang === 'fr' ? 'fr-FR' : 'en-US', {
    month: 'long',
    year: 'numeric',
  })
}

// Merge block.data into block so BlockRenderer can access fields directly
function flattenBlock(b) {
  const flat = { ...b, ...(b.data ?? {}), game: b.game_slug ?? b.game ?? null }
  // Aplatir récursivement les sous-blocs des colonnes
  if (flat.left)  flat.left  = flat.left.map(flattenBlock)
  if (flat.right) flat.right = flat.right.map(flattenBlock)
  return flat
}

export default function DevBlogPost() {
  const { slug }            = useParams()
  const { lang }            = useLang()
  const { post, loading, error } = usePost(slug)
  const { posts: allPosts } = usePosts()
  const { setPostTitle }    = usePostTitle()
  const [views, setViews]   = useState(null)

  // Scroll haut au changement d'article
  useEffect(() => {
    window.scrollTo({ top: 0, behavior: 'instant' })
  }, [slug])

  // Incrémenter et récupérer le compteur de vues
  const API = import.meta.env.VITE_API_URL || ''
  useEffect(() => {
    if (!slug) return
    setViews(null)
    fetch(`${API}/api/posts/${slug}/view`, { method: 'POST' })
      .then(r => r.ok ? r.json() : null)
      .then(data => { if (data) setViews(data.views) })
      .catch(() => {})
  }, [slug])

  // Met à jour le titre du breadcrumb dans le header
  useEffect(() => {
    if (post) {
      setPostTitle(lang === 'fr' ? post.title_fr : post.title_en)
    }
    return () => setPostTitle(null)
  }, [post, lang, setPostTitle])

  if (loading) {
    return <div className="dbp__loading"><p>Chargement…</p></div>
  }

  if (error || !post) {
    return (
      <div className="dbp__not-found">
        <h2>{lang === 'fr' ? 'Article introuvable' : 'Post not found'}</h2>
        <Link to="/devblog" className="dbp__back-btn">
          ← {lang === 'fr' ? 'Retour au DevBlog' : 'Back to DevBlog'}
        </Link>
      </div>
    )
  }

  const title  = lang === 'fr' ? post.title_fr  : post.title_en
  const cover  = post.cover
  const rawBlocks = lang === 'fr' ? (post.blocksFr ?? []) : (post.blocksEn ?? [])
  const allBlocks = rawBlocks.map(flattenBlock)

  // Jeux présents dans ce devlog (badges hero)
  const presentGames = post.games ?? []

  // Articles des autres mois
  const related = allPosts
    .filter(p => p.id !== post.id)
    .sort((a, b) => b.month.localeCompare(a.month))
    .slice(0, 2)

  const excerpt = lang === 'fr' ? post.excerpt_fr : post.excerpt_en

  return (
    <article className="dbp">
      <SEO
        title={title}
        description={excerpt || title}
        image={cover || undefined}
        url={`/devblog/${post.slug}`}
        type="article"
        lang={lang}
        jsonLd={{
          '@context': 'https://schema.org',
          '@type': 'BlogPosting',
          headline: title,
          description: excerpt || title,
          image: cover || undefined,
          author: { '@type': 'Person', name: post.author },
          publisher: { '@type': 'Organization', name: 'Small Box Studio' },
          datePublished: post.month,
          url: `https://openframework.fr/devblog/${post.slug}`,
        }}
      />
      {/* ── Hero ── */}
      <header className="dbp__hero">
        {cover && <img src={cover} alt={title} className="dbp__hero-bg-img" />}
        <div className="dbp__hero-overlay" />

        <div className="dbp__hero-inner">
          <div className="dbp__hero-badges">
            <span className="dbp__month-badge">{formatDate(post.month, lang)}</span>
            {presentGames.map(g => (
              <span key={g.slug} className={`dbp__game-pill dbp__game-pill--${g.slug}`}
                style={g.color ? { background: g.color } : {}}>
                {lang === 'fr' ? g.label_fr : g.label_en}
              </span>
            ))}
          </div>

          <h1 className="dbp__title">{title}</h1>

          <div className="dbp__meta">
            <span className="dbp__author">{post.author}</span>
            <span className="dbp__sep">·</span>
            <span className="dbp__read-time">
              {post.read_time} min {lang === 'fr' ? 'de lecture' : 'read'}
            </span>
            {views !== null && (
              <>
                <span className="dbp__sep">·</span>
                <span className="dbp__views">
                  <svg className="dbp__views-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
                    <circle cx="12" cy="12" r="3"/>
                  </svg>
                  {views.toLocaleString(lang === 'fr' ? 'fr-FR' : 'en-US')}
                </span>
              </>
            )}
          </div>
        </div>
      </header>

      {/* ── Contenu ── */}
      <div className="dbp__layout">
        <div className="dbp__content">
          <BlockRenderer blocks={allBlocks} lang={lang} />
        </div>
      </div>

      {/* ── Autres devlogs ── */}
      {related.length > 0 && (
        <section className="dbp__related">
          <div className="dbp__related-inner">
            <h2 className="dbp__related-title">
              {lang === 'fr' ? 'Autres devlogs' : 'Other devlogs'}
            </h2>
            <div className="dbp__related-grid">
              {related.map(rp => {
                const rt = lang === 'fr' ? rp.title_fr : rp.title_en
                const re = lang === 'fr' ? rp.excerpt_fr : rp.excerpt_en
                return (
                  <Link key={rp.id} to={`/devblog/${rp.slug}`} className="dbp__related-card">
                    <div className="dbp__related-cover dbp__related-cover--global">
                      {rp.cover
                        ? <img src={rp.cover} alt={rt} />
                        : <span className="dbp__related-icon">📝</span>
                      }
                    </div>
                    <div className="dbp__related-body">
                      <span className="dbp__related-month">{formatDate(rp.month, lang)}</span>
                      <h3 className="dbp__related-card-title">{rt}</h3>
                      <p className="dbp__related-excerpt">{re}</p>
                      <div className="dbp__related-games">
                        {(rp.games ?? []).map(g => (
                          <span key={g.slug} className={`dbp__mini-pill dbp__mini-pill--${g.slug}`}
                            style={g.color ? { background: g.color } : {}}>
                            {lang === 'fr' ? g.label_fr : g.label_en}
                          </span>
                        ))}
                      </div>
                    </div>
                  </Link>
                )
              })}
            </div>
          </div>
        </section>
      )}

      {/* ── Retour ── */}
      <div className="dbp__back">
        <Link to="/devblog" className="dbp__back-btn">
          ← {lang === 'fr' ? 'Retour au DevBlog' : 'Back to DevBlog'}
        </Link>
      </div>
    </article>
  )
}

