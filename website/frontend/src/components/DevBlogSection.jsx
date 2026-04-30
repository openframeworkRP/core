import { useEffect, useRef, useState } from 'react'
import './DevBlogSection.css'
import { useLang } from '../context/LanguageContext'

/* ─── Données des articles ───────────────────────────────────────────────
   Chaque article a :
   - id          : identifiant unique
   - game        : slug du jeu ('core' | 'all' | futur jeu…)
   - month       : mois (ISO: 'YYYY-MM')
   - titleFr / titleEn
   - excerptFr / excerptEn
   - contentFr / contentEn  (supporte le markdown basique : ## titres, **gras**, \n\n paragraphes)
   - author
   - tags        : string[]
──────────────────────────────────────────────────────────────────────── */
export const BLOG_POSTS = [
  {
    id: 1,
    game: 'core',
    month: '2026-03',
    titleFr: 'OpenFramework – Devlog #1 : Premières fondations',
    titleEn: 'OpenFramework – Devlog #1: First Foundations',
    excerptFr: 'On pose les bases du serveur Roleplay : système de jobs, économie et carte initiale. Retour sur un mois intense de prototypage.',
    excerptEn: 'We lay the groundwork for the Roleplay server: job system, economy, and initial map. A look back at an intense month of prototyping.',
    contentFr: `## Premières fondations\n\nCe mois-ci nous avons posé les bases techniques de OpenFramework. La priorité était de définir une architecture solide avant d'ajouter du contenu.\n\n## Système de jobs\n\nNous avons implémenté un système de jobs dynamiques. Les joueurs peuvent choisir leur rôle en temps réel : citoyen, policier, criminel… Chaque job dispose de permissions et d'outils uniques.\n\n## Économie\n\nUn système d'économie persistante a été intégré. L'argent est sauvegardé entre les sessions et circuler dans l'économie de la ville est maintenant cohérent.\n\n## Prochaine étape\n\nLe mois prochain, on attaque la map et les bâtiments interactifs. Stay tuned !`,
    contentEn: `## First Foundations\n\nThis month we laid the technical groundwork for OpenFramework. The priority was to define a solid architecture before adding content.\n\n## Job System\n\nWe implemented a dynamic job system. Players can choose their role in real time: citizen, police officer, criminal… Each job has unique permissions and tools.\n\n## Economy\n\nA persistent economy system has been integrated. Money is saved between sessions and circulating in the city's economy is now coherent.\n\n## Next Step\n\nNext month we'll tackle the map and interactive buildings. Stay tuned!`,
    author: 'Small Box Studio',
    tags: ['Roleplay', 'Gamemode', 'Economy'],
  },
  {
    id: 2,
    game: 'core',
    month: '2026-02',
    titleFr: 'OpenFramework – Devlog #2 : La carte prend vie',
    titleEn: 'OpenFramework – Devlog #2: The Map Comes Alive',
    excerptFr: 'Ce mois-ci on a travaillé intensément sur la map : zones résidentielles, quartier d\'affaires et premier PNJ interactif.',
    excerptEn: 'This month we worked intensively on the map: residential areas, business district, and the first interactive NPC.',
    contentFr: `## La carte prend vie\n\nFévrier a été un mois très visuel. On a commencé à peupler la map avec des bâtiments authentiques et des zones distinctes.\n\n## Zones résidentielles\n\nLes appartements sont maintenant achetables. Les joueurs peuvent décorer leur intérieur et inviter d'autres joueurs.\n\n## Quartier d'affaires\n\nLe quartier business dispose de boutiques louables. Monter son entreprise devient une vraie mécanique de jeu.\n\n## PNJ interactif\n\nLe premier PNJ — le banquier — est en place. Il permet de déposer de l'argent et de contracter des prêts.`,
    contentEn: `## The Map Comes Alive\n\nFebruary was a very visual month. We started populating the map with authentic buildings and distinct zones.\n\n## Residential Areas\n\nApartments are now purchasable. Players can decorate their interiors and invite other players.\n\n## Business District\n\nThe business district has rentable shops. Building your company is now a real game mechanic.\n\n## Interactive NPC\n\nThe first NPC — the banker — is in place. He allows depositing money and taking out loans.`,
    author: 'Small Box Studio',
    tags: ['Map', 'NPC', 'Housing'],
  },
]

/* ─── Jeux disponibles (pour les filtres) ─────────────────────────────── */
const GAMES = [
  { slug: 'all',        labelFr: 'Tous les jeux',  labelEn: 'All games' },
  { slug: 'core', labelFr: 'OpenFramework',      labelEn: 'OpenFramework' },
  // Ajoute tes futurs jeux ici
]

/* ─── Utilitaires ─────────────────────────────────────────────────────── */
function formatMonth(isoMonth, lang) {
  const [year, month] = isoMonth.split('-')
  const date = new Date(Number(year), Number(month) - 1, 1)
  return date.toLocaleDateString(lang === 'fr' ? 'fr-FR' : 'en-US', {
    month: 'long',
    year: 'numeric',
  })
}

/** Rendu basique du markdown (## titres, **gras**, \n\n paragraphes) */
function renderContent(raw) {
  return raw.split('\n\n').map((block, i) => {
    if (block.startsWith('## ')) {
      return <h3 key={i} className="devblog__post-h3">{block.slice(3)}</h3>
    }
    const parts = block.split(/(\*\*[^*]+\*\*)/g).map((part, j) => {
      if (part.startsWith('**') && part.endsWith('**')) {
        return <strong key={j}>{part.slice(2, -2)}</strong>
      }
      return part
    })
    return <p key={i} className="devblog__post-p">{parts}</p>
  })
}

/* ─── Composant carte ─────────────────────────────────────────────────── */
function BlogCard({ post, onClick }) {
  const { lang } = useLang()
  const title   = lang === 'fr' ? post.titleFr   : post.titleEn
  const excerpt = lang === 'fr' ? post.excerptFr : post.excerptEn

  return (
    <article className="devblog__card" onClick={() => onClick(post)}>
      {/* En-tête coloré selon le jeu */}
      <div className={`devblog__card-header devblog__card-header--${post.game}`}>
        <span className="devblog__card-game">{post.game === 'core' ? 'OpenFramework' : post.game}</span>
        <span className="devblog__card-month">{formatMonth(post.month, lang)}</span>
      </div>

      <div className="devblog__card-body">
        <h3 className="devblog__card-title">{title}</h3>
        <p className="devblog__card-excerpt">{excerpt}</p>

        <div className="devblog__card-footer">
          <div className="devblog__card-tags">
            {post.tags.map(tag => (
              <span key={tag} className="devblog__tag">{tag}</span>
            ))}
          </div>
          <span className="devblog__card-read">
            {lang === 'fr' ? 'Lire →' : 'Read →'}
          </span>
        </div>
      </div>
    </article>
  )
}

/* ─── Modale article ──────────────────────────────────────────────────── */
function PostModal({ post, onClose }) {
  const { lang } = useLang()
  const title   = lang === 'fr' ? post.titleFr   : post.titleEn
  const content = lang === 'fr' ? post.contentFr : post.contentEn

  // Fermer avec Escape
  useEffect(() => {
    const handleKey = (e) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', handleKey)
    document.body.style.overflow = 'hidden'
    return () => {
      window.removeEventListener('keydown', handleKey)
      document.body.style.overflow = ''
    }
  }, [onClose])

  return (
    <div className="devblog__modal-backdrop" onClick={onClose}>
      <div className="devblog__modal" onClick={e => e.stopPropagation()}>
        <button className="devblog__modal-close" onClick={onClose} aria-label="Fermer">✕</button>

        <div className={`devblog__modal-banner devblog__card-header--${post.game}`}>
          <span className="devblog__card-game">{post.game === 'core' ? 'OpenFramework' : post.game}</span>
          <span className="devblog__card-month">{formatMonth(post.month, lang)}</span>
        </div>

        <div className="devblog__modal-inner">
          <h2 className="devblog__modal-title">{title}</h2>
          <p className="devblog__modal-author">— {post.author}</p>

          <div className="devblog__modal-tags">
            {post.tags.map(tag => (
              <span key={tag} className="devblog__tag">{tag}</span>
            ))}
          </div>

          <div className="devblog__modal-content">
            {renderContent(content)}
          </div>
        </div>
      </div>
    </div>
  )
}

/* ─── Section principale ──────────────────────────────────────────────── */
export default function DevBlogSection() {
  const { lang, t } = useLang()
  const sectionRef  = useRef(null)
  const [activeGame, setActiveGame]   = useState('all')
  const [activePost, setActivePost]   = useState(null)

  // Animation d'entrée
  useEffect(() => {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) entry.target.classList.add('devblog--visible')
        })
      },
      { threshold: 0.05 }
    )
    const el = sectionRef.current
    if (el) observer.observe(el)
    return () => { if (el) observer.unobserve(el) }
  }, [])

  const filtered = activeGame === 'all'
    ? BLOG_POSTS
    : BLOG_POSTS.filter(p => p.game === activeGame)

  // Trier du plus récent au plus ancien
  const sorted = [...filtered].sort((a, b) => b.month.localeCompare(a.month))

  return (
    <section className="devblog" id="devblog" ref={sectionRef}>
      {/* Vague de séparation */}
      <div className="devblog__wave" aria-hidden="true">
        <svg viewBox="0 0 1440 80" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
          <path d="M0,40 C360,0 1080,80 1440,40 L1440,0 L0,0 Z" fill="#1e1e1e" />
        </svg>
      </div>

      <div className="devblog__inner">
        {/* Header */}
        <div className="devblog__header">
          <span className="devblog__eyebrow">{t('devblog.subtitle')}</span>
          <h2 className="devblog__title">{t('devblog.title')}</h2>
          <div className="devblog__divider" />
          <p className="devblog__intro">{t('devblog.intro')}</p>
        </div>

        {/* Filtres par jeu */}
        <div className="devblog__filters">
          {GAMES.map(game => (
            <button
              key={game.slug}
              className={`devblog__filter-btn${activeGame === game.slug ? ' devblog__filter-btn--active' : ''}`}
              onClick={() => setActiveGame(game.slug)}
            >
              {lang === 'fr' ? game.labelFr : game.labelEn}
            </button>
          ))}
        </div>

        {/* Grille d'articles */}
        {sorted.length === 0 ? (
          <p className="devblog__empty">{t('devblog.empty')}</p>
        ) : (
          <div className="devblog__grid">
            {sorted.map(post => (
              <BlogCard key={post.id} post={post} onClick={setActivePost} />
            ))}
          </div>
        )}
      </div>

      {/* Modale */}
      {activePost && (
        <PostModal post={activePost} onClose={() => setActivePost(null)} />
      )}
    </section>
  )
}
