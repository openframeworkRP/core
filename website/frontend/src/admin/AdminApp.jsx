import { useState, useEffect, useCallback, useRef } from 'react'
import { useNavigate, useParams, useLocation } from 'react-router-dom'
import { api, API_BASE } from './api.js'
import { useAdminSocket } from './useAdminSocket.js'
import { useAuth } from '../context/AuthContext.jsx'
import SEO from '../components/SEO.jsx'
import './Admin.css'
import {
  Wrench, Gamepad2, Globe, Newspaper, Plus,
  FileText, CheckCircle, Pencil, Rocket, Trash2,
  ArrowDownToLine, X, ChevronDown, Clock, Briefcase,
  ToggleLeft, ToggleRight, LogOut, Users, Shield, Bug, MessageSquare, Eye, EyeOff,
  BarChart2, TrendingUp, Activity, AlertCircle, Briefcase as BriefcaseIcon,
  Download, Upload as UploadIcon,
  BarChart3, Columns3, List, Route, Lightbulb, MapPin, ShoppingBag, User, Search, Store, Film, Image as ImageIcon, SquareCheck, ScrollText, Layout,
  HardDrive, Database, BookOpen, BookMarked, Car, Server, Palette,
} from 'lucide-react'
import HubPanel, { SearchOverlay as HubSearchOverlay, DEFAULT_PROJECTS as HUB_PROJECTS, loadHubData } from './HubPanel.jsx'
import VideoPanel from './VideoPanel.jsx'
import ImagePanel from './ImagePanel.jsx'
import RulesPanel from './RulesPanel.jsx'
import OpenFrameworkRulesPanel from './OpenFrameworkRulesPanel.jsx'
import WikiPanel from './WikiPanel.jsx'
import DocsPanel from './DocsPanel.jsx'
import UIPanel from './UIPanel.jsx'
import VehiclePanel from './VehiclePanel.jsx'
import GameAdminPanel from './GameAdminPanel.jsx'
import PermissionsPanel from './PermissionsPanel.jsx'
import ControlPanel from './ControlPanel.jsx'
import BrandingPanel from './BrandingPanel.jsx'

// ── Rôles ─────────────────────────────────────────────────────────────────
const ROLE_COLORS = { owner: '#f59e0b', admin: '#a78bfa', editor: '#34d399', rules_editor: '#e07b39', viewer: '#71717a' }
const ROLE_FALLBACK_COLOR = '#6366f1'   // pour les rôles personnalisés
const colorForRole = (role) => ROLE_COLORS[role] || ROLE_FALLBACK_COLOR

export default function AdminApp() {
  const [posts,   setPosts]   = useState([])
  const [games,   setGames]   = useState([])
  const [users,   setUsers]   = useState([])
  const [error,   setError]   = useState('')
  const [loading, setLoading] = useState(true)
  const [importing,    setImporting]    = useState(false)
  const [hubSearchOpen,setHubSearchOpen]= useState(false)
  const [hubData,      setHubData]      = useState(null)
  const [catalogueAssets, setCatalogueAssets] = useState([])
  const [hubOpenTaskId,setHubOpenTaskId]= useState(null)
  const [projectFilter,setProjectFilter]= useState('sl-v1')
  const [openDropdown, setOpenDropdown] = useState(null)
  const dropdownTimerRef = useRef(null)
  const importRef = useRef(null)
  const { user, logout, can } = useAuth()

  // ── Navigation URL-driven ──
  const routerNavigate = useNavigate()
  const location = useLocation()
  const params = useParams()

  const pathname = location.pathname
  const isHub     = pathname.startsWith('/admin/hub')
  const isAdmin   = pathname.startsWith('/admin/panel')
  const isDevblog = !isHub && !isAdmin
  const hubView   = isHub   ? params.hubView  : null
  const panel     = isAdmin ? params.panelId  : null
  const currentView = isHub ? `hub:${hubView}` : isAdmin ? `admin:${panel}` : 'devblog'

  // Synchronise l'ID de tâche ouverte dans le hub avec l'URL
  useEffect(() => {
    if (isHub && hubView === 'tasks' && params.taskId) {
      setHubOpenTaskId(params.taskId)
    }
  }, [isHub, hubView, params.taskId])

  function goTo(view) {
    setOpenDropdown(null)
    if (view === 'devblog')                      return routerNavigate('/admin')
    if (view === 'devblog:new')                  return routerNavigate('/admin/new')
    if (view.startsWith('devblog:edit:'))        return routerNavigate(`/admin/edit/${view.slice(13)}`)
    if (view.startsWith('hub:'))                 return routerNavigate(`/admin/hub/${view.slice(4)}`)
    if (view.startsWith('admin:'))               return routerNavigate(`/admin/panel/${view.slice(6)}`)
    return routerNavigate('/admin')
  }

  function openDd(name) {
    clearTimeout(dropdownTimerRef.current)
    setOpenDropdown(name)
  }
  function closeDd() {
    dropdownTimerRef.current = setTimeout(() => setOpenDropdown(null), 120)
  }

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const [p, g, u] = await Promise.all([api.getPosts(), api.getGames(), api.getUsers().catch(() => [])])
      setPosts(p)
      setGames(g)
      setUsers(u)
    } catch (e) {
      setError('Impossible de joindre l\'API — backend démarré ?')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { load() }, [load])

  // Reload automatique quand un autre admin modifie des données
  useAdminSocket(['posts_updated', 'games_updated', 'users_updated', 'members_updated', 'jobs_updated'], load)

  // Charge les données hub à la première ouverture de la recherche
  useEffect(() => {
    if (hubSearchOpen && !hubData) {
      loadHubData().then(data => setHubData(data))
      if (catalogueAssets.length === 0) {
        api.getAssets().then(setCatalogueAssets).catch(() => {})
      }
    }
  }, [hubSearchOpen, hubData])

  useEffect(() => {
    const handler = (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault()
        setHubSearchOpen(p => !p)
      }
      if (e.key === 'Escape') setHubSearchOpen(false)
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [])

  async function handleDelete(id) {
    if (!confirm('Supprimer définitivement ce devlog ?')) return
    await api.deletePost(id)
    load()
  }

  async function handleImport(e) {
    const file = e.target.files?.[0]
    if (!file) return
    e.target.value = ''   // reset pour pouvoir ré-importer le même fichier
    setImporting(true)
    setError('')
    try {
      const imported = await api.importPost(file)
      await load()
      routerNavigate(`/admin/edit/${imported.id}`)
    } catch (err) {
      setError('Erreur import : ' + err.message)
    } finally {
      setImporting(false)
    }
  }

  async function togglePublish(post) {
    post.published ? await api.unpublishPost(post.id) : await api.publishPost(post.id)
    load()
  }

  const drafts    = posts.filter(p => !p.published)
  const published = posts.filter(p =>  p.published)

  return (
    <>
    <SEO title="Administration" noIndex />
    <div className="adm">

      {/* ── Header ── */}
      <header className="adm__header">
        {/* Project filter dropdown */}
        {(() => {
          const current = HUB_PROJECTS.find(p => p.id === projectFilter) || HUB_PROJECTS[0]
          const isOpen = openDropdown === 'proj-filter'
          return (
            <div className="adm__proj-filter"
              onMouseEnter={() => { clearTimeout(dropdownTimerRef.current); setOpenDropdown('proj-filter') }}
              onMouseLeave={() => { dropdownTimerRef.current = setTimeout(() => setOpenDropdown(d => d === 'proj-filter' ? null : d), 120) }}
            >
              <button className="adm__proj-filter__trigger">
                <span style={{ width: 8, height: 8, borderRadius: '50%', background: current.color, flexShrink: 0, display: 'inline-block' }} />
                <span className="adm__proj-filter__name">{current.name}</span>
                <ChevronDown size={12} />
              </button>
              {isOpen && (
                <div className="adm__proj-filter__menu">
                  {HUB_PROJECTS.map(p => (
                    <button key={p.id} className={`adm__proj-filter__item${projectFilter === p.id ? ' adm__proj-filter__item--active' : ''}`} onClick={() => { setProjectFilter(p.id); setOpenDropdown(null) }}>
                      <span style={{ width: 8, height: 8, borderRadius: '50%', background: p.color, flexShrink: 0, display: 'inline-block' }} />
                      {p.name}
                    </button>
                  ))}
                </div>
              )}
            </div>
          )
        })()}
        <a href="/" target="_blank" className="adm__header-brand" style={{ textDecoration: 'none', color: 'inherit' }}>
          <span className="adm__header-icon"><Wrench size={16} /></span>
          <div>
            <div className="adm__header-title">S&amp;Box Studio</div>
            <div className="adm__header-sub">Admin</div>
          </div>
        </a>
        <div className="adm__header-search" onClick={() => setHubSearchOpen(true)}>
          <Search size={14} />
          <span>Rechercher tâches, devlogs…</span>
          <kbd>Ctrl+K</kbd>
        </div>
        {user && (
          <button className="adm__nav-btn adm__nav-btn--logout" onClick={logout} title={`Connecté : ${user.displayName} (${user.role})`}>
            {user.avatar && <img src={user.avatar} alt="" style={{ width: 22, height: 22, borderRadius: '50%' }} />}
            {user.role && (
              <span style={{ fontSize: '0.62rem', fontWeight: 700, padding: '1px 6px', borderRadius: 99, background: colorForRole(user.role) + '33', color: colorForRole(user.role) }}>
                {user.role}
              </span>
            )}
            <LogOut size={14} />
          </button>
        )}
      </header>

      {/* ── Sidebar ── */}
      <aside className="adm__sidebar">
        <span className="adm__sidebar-label">Admin</span>
        {can('admin:games') && (
          <button className={`adm__sidebar-btn${panel === 'games' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('admin:games')}>
            <Gamepad2 size={15} /> Jeux
          </button>
        )}
        {can('admin:jobs') && (
          <button className={`adm__sidebar-btn${panel === 'jobs' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('admin:jobs')}>
            <Briefcase size={15} /> Emplois
          </button>
        )}
        {can('admin:bugs') && (
          <button className={`adm__sidebar-btn${panel === 'bugs' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('admin:bugs')}>
            <Bug size={15} /> Bugs
          </button>
        )}
        {can('admin:stats') && (
          <button className={`adm__sidebar-btn${panel === 'stats' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('admin:stats')}>
            <BarChart2 size={15} /> Stats
          </button>
        )}
        {can('admin:users') && (
          <button className={`adm__sidebar-btn${panel === 'users' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('admin:users')}>
            <Users size={15} /> Équipe
          </button>
        )}
        {can('admin:gameadmin') && (
          <button className={`adm__sidebar-btn${panel === 'gameadmin' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('admin:gameadmin')}>
            <Gamepad2 size={15} /> Admin Jeu
          </button>
        )}
        {can('admin:permissions') && (
          <button className={`adm__sidebar-btn${panel === 'permissions' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('admin:permissions')}>
            <Shield size={15} /> Permissions
          </button>
        )}

        <div className="adm__sidebar-divider" />
        <span className="adm__sidebar-label">Hub</span>
        {can('hub:dashboard') && (
          <button className={`adm__sidebar-btn${hubView === 'dashboard' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('hub:dashboard')}>
            <BarChart3 size={15} /> Dashboard
          </button>
        )}
        {can('hub:tasks') && (
          <button className={`adm__sidebar-btn${hubView === 'tasks' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('hub:tasks')}>
            <SquareCheck size={15} /> Tâches
          </button>
        )}
        {can('hub:roadmap') && (
          <button className={`adm__sidebar-btn${hubView === 'roadmap' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('hub:roadmap')}>
            <Route size={15} /> Roadmap
          </button>
        )}
        {can('hub:whiteboard') && (
          <button className={`adm__sidebar-btn${hubView === 'whiteboard' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('hub:whiteboard')}>
            <Lightbulb size={15} /> Idées
          </button>
        )}
        {can('hub:mapview') && (
          <button className={`adm__sidebar-btn${hubView === 'mapview' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('hub:mapview')}>
            <MapPin size={15} /> Map
          </button>
        )}
        {can('hub:fab') && (
          <button className={`adm__sidebar-btn${hubView === 'fab' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('hub:fab')}>
            <ShoppingBag size={15} /> Assets Fab
          </button>
        )}
        {can('hub:catalogue') && (
          <button className={`adm__sidebar-btn${hubView === 'catalogue' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('hub:catalogue')}>
            <Database size={15} /> Catalogue
          </button>
        )}
        {can('hub:activity') && (
          <button className={`adm__sidebar-btn${hubView === 'activity' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('hub:activity')}>
            <Activity size={15} /> Activité
          </button>
        )}

        <div className="adm__sidebar-divider" />
        <span className="adm__sidebar-label">Médias & Docs</span>
        {can('admin:videos') && (
          <button className={`adm__sidebar-btn${panel === 'videos' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('admin:videos')}>
            <Film size={15} /> Vidéos
          </button>
        )}
        {can('admin:images') && (
          <button className={`adm__sidebar-btn${panel === 'images' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('admin:images')}>
            <ImageIcon size={15} /> Images
          </button>
        )}
        {can('admin:rules') && (
          <button className={`adm__sidebar-btn${panel === 'rules' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('admin:rules')}>
            <ScrollText size={15} /> Règles
          </button>
        )}
        {can('admin:sl-rules') && (
          <button className={`adm__sidebar-btn${panel === 'sl-rules' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('admin:sl-rules')}>
            <BookOpen size={15} /> OpenFramework — Règles
          </button>
        )}
        {can('admin:docs') && (
          <button className={`adm__sidebar-btn${panel === 'docs' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('admin:docs')}>
            <BookMarked size={15} /> Documentation
          </button>
        )}
        {can('admin:wiki') && (
          <button className={`adm__sidebar-btn${panel === 'wiki' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('admin:wiki')}>
            <BookMarked size={15} /> Wiki in-game
          </button>
        )}
        {can('admin:ui') && (
          <button className={`adm__sidebar-btn${panel === 'ui' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('admin:ui')}>
            <Layout size={15} /> UI Builder
          </button>
        )}
        {can('admin:vehicles') && (
          <button className={`adm__sidebar-btn${panel === 'vehicles' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('admin:vehicles')}>
            <Car size={15} /> Véhicules
          </button>
        )}

        {/* Control Center + Branding : reserve aux owners */}
        {user?.role === 'owner' && (
          <>
            <div className="adm__sidebar-divider" />
            <span className="adm__sidebar-label">Infrastructure</span>
            <button className={`adm__sidebar-btn${panel === 'control' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('admin:control')}>
              <Server size={15} /> Control Center
            </button>
            <button className={`adm__sidebar-btn${panel === 'branding' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('admin:branding')}>
              <Palette size={15} /> Branding
            </button>
          </>
        )}

        <div className="adm__sidebar-divider" />
        <span className="adm__sidebar-label">DevBlog</span>
        {can('devblog:list') && (
          <button className={`adm__sidebar-btn${currentView === 'devblog' ? ' adm__sidebar-btn--active' : ''}`} onClick={() => goTo('devblog')}>
            <FileText size={15} /> Devlogs
          </button>
        )}
        <a href="/devblog" target="_blank" className="adm__sidebar-btn">
          <Newspaper size={15} /> Voir le devblog
        </a>
        {can('devblog:edit') && (
          <button className="adm__sidebar-btn adm__sidebar-btn--primary" onClick={() => goTo('devblog:new')}>
            <Plus size={15} /> Nouveau devlog
          </button>
        )}
        {can('devblog:edit') && (
          <button className="adm__sidebar-btn" onClick={() => importRef.current?.click()} disabled={importing}>
            <UploadIcon size={15} /> {importing ? 'Import…' : 'Importer .devblog'}
          </button>
        )}
        <input ref={importRef} type="file" accept=".devblog" style={{ display: 'none' }} onChange={handleImport} />
      </aside>

      <div className="adm__body">
      {isAdmin ? (
        <div className="adm__panel-page">
          {panel === 'games'     && <GamesPanel games={games} onClose={() => goTo('devblog')} onRefresh={load} />}
          {panel === 'jobs'      && <JobsPanel games={games} onClose={() => goTo('devblog')} />}
          {panel === 'users'     && <UsersPanel onClose={() => goTo('devblog')} currentUser={user} />}
          {panel === 'bugs'      && <BugsPanel onClose={() => goTo('devblog')} />}
          {panel === 'stats'     && <StatsPanel onClose={() => goTo('devblog')} posts={posts} />}
          {panel === 'videos'    && <VideoPanel />}
          {panel === 'images'    && <ImagePanel />}
          {panel === 'rules'     && <RulesPanel />}
          {panel === 'sl-rules'  && <OpenFrameworkRulesPanel />}
          {panel === 'wiki'      && <WikiPanel />}
          {panel === 'docs'      && <DocsPanel />}
          {panel === 'ui'        && <UIPanel />}
          {panel === 'vehicles'  && <VehiclePanel />}
          {panel === 'gameadmin'   && <GameAdminPanel />}
          {panel === 'permissions' && <PermissionsPanel />}
          {panel === 'control'     && <ControlPanel />}
          {panel === 'branding'    && <BrandingPanel />}
        </div>
      ) : isHub ? (
        <HubPanel
          view={hubView}
          onChangeView={(v) => goTo(`hub:${v}`)}
          currentUser={user}
          projectFilter={projectFilter}
          openTaskId={hubOpenTaskId}
          onTaskOpened={() => {
            setHubOpenTaskId(null)
            if (params.taskId) routerNavigate(`/admin/hub/${hubView}`, { replace: true })
          }}
        />
      ) : (
        <>

          <main className="adm__main">
            {error && <div className="adm__banner adm__banner--error">{error}</div>}

            {loading ? (
              <div className="adm__loader">
                <div className="adm__spinner" />
                <span>Chargement…</span>
              </div>
            ) : (
              <>
                {/* Stats */}
                <div className="adm__stats">
                  <div className="adm__stat">
                    <span className="adm__stat-num">{posts.length}</span>
                    <span className="adm__stat-label">Total</span>
                  </div>
                  <div className="adm__stat">
                    <span className="adm__stat-num adm__stat-num--green">{published.length}</span>
                    <span className="adm__stat-label">Publiés</span>
                  </div>
                  <div className="adm__stat">
                    <span className="adm__stat-num adm__stat-num--orange">{drafts.length}</span>
                    <span className="adm__stat-label">Brouillons</span>
                  </div>
                </div>

                {/* Brouillons */}
                {drafts.length > 0 && (
                  <section className="adm__section">
                    <h2 className="adm__section-title"><FileText size={16} /> Brouillons</h2>
                    <div className="adm__cards">
                      {drafts.map(post => (
                        <PostCard
                          key={post.id} post={post}
                          onEdit={() => goTo(`devblog:edit:${post.id}`)}
                          onPublish={() => togglePublish(post)}
                          onDelete={() => handleDelete(post.id)}
                        />
                      ))}
                    </div>
                  </section>
                )}

                {/* Publiés */}
                <section className="adm__section">
                  <h2 className="adm__section-title"><CheckCircle size={16} /> Publiés</h2>
                  {published.length === 0 ? (
                    <div className="adm__empty-state">
                      <span>Aucun devlog publié pour l'instant.</span>
                      <button className="adm__btn adm__btn--ghost" onClick={() => goTo('devblog:new')}>
                        Créer le premier →
                      </button>
                    </div>
                  ) : (
                    <div className="adm__cards">
                      {published.map(post => (
                        <PostCard
                          key={post.id} post={post}
                          onEdit={() => goTo(`devblog:edit:${post.id}`)}
                          onPublish={() => togglePublish(post)}
                          onDelete={() => handleDelete(post.id)}
                        />
                      ))}
                    </div>
                  )}
                </section>
              </>
            )}
          </main>
        </>
      )}
    </div>
      </div>{/* adm__body */}

    {hubSearchOpen && (
      <HubSearchOverlay
        tasks={hubData?.tasks || []}
        ideas={hubData?.ideas || []}
        milestones={hubData?.milestones || []}
        fabAssets={hubData?.fabAssets || []}
        catalogueAssets={catalogueAssets}
        projects={HUB_PROJECTS}
        members={[]}
        onClose={() => setHubSearchOpen(false)}
        onEditTask={(task) => { setHubOpenTaskId(task.id); setHubSearchOpen(false); routerNavigate(`/admin/hub/tasks/${task.id}`); }}
        onNavigate={(v) => { setHubSearchOpen(false); goTo(`hub:${v}`); }}
      />
    )}
    </>
  )
}

/* ── Carte post ─────────────────────────────────────────────────────────── */
function PostCard({ post, onEdit, onPublish, onDelete }) {
  const date = post.month
    ? new Date(post.month + '-01').toLocaleDateString('fr-FR', { month: 'long', year: 'numeric' })
    : '—'

  function handleExport() {
    const url = api.exportPost(post.id)
    const a = document.createElement('a')
    a.href = url
    a.download = `${post.slug}.devblog`
    a.click()
  }

  return (
    <div className="adm__card">
      <a href={`/devblog/${post.slug}`} target="_blank" rel="noreferrer" className="adm__card-cover adm__card-cover--link">
        {post.cover
          ? <img src={post.cover} alt={post.title_fr} />
          : <span className="adm__card-cover-icon"><FileText size={32} /></span>
        }
        <span className={`adm__card-badge ${post.published ? 'adm__card-badge--pub' : 'adm__card-badge--draft'}`}>
          {post.published ? 'Publié' : 'Brouillon'}
        </span>
        <span className="adm__card-cover-view"><Eye size={18} /> Voir</span>
      </a>
      <div className="adm__card-body">
        <div className="adm__card-month">{date}</div>
        <h3 className="adm__card-title">{post.title_fr}</h3>
        <p className="adm__card-excerpt">{post.excerpt_fr}</p>
        <div className="adm__card-meta">
          <div className="adm__card-games">
            {(post.games ?? []).map(g => (
              <span key={g.slug} className="adm__pill" style={{ background: g.color ?? '#555' }}>
                {g.label_fr}
              </span>
            ))}
          </div>
          <span className="adm__card-readtime"><Clock size={13} /> {post.read_time} min</span>
        </div>
      </div>
      <div className="adm__card-actions">
        <button className="adm__card-btn" onClick={onEdit} title="Éditer">
          <Pencil size={14} /> Éditer
        </button>
        <button className="adm__card-btn" onClick={onPublish} title={post.published ? 'Dépublier' : 'Publier'}>
          {post.published ? <><ArrowDownToLine size={14} /> Dépublier</> : <><Rocket size={14} /> Publier</>}
        </button>
        <button className="adm__card-btn" onClick={handleExport} title="Exporter en .devblog">
          <Download size={14} /> Exporter
        </button>
        <button className="adm__card-btn adm__card-btn--danger" onClick={onDelete} title="Supprimer">
          <Trash2 size={14} />
        </button>
      </div>
    </div>
  )
}

/* ── Panel jeux ─────────────────────────────────────────────────────────── */
function GamesPanel({ games, onClose, onRefresh }) {
  const [form, setForm] = useState({ label_fr: '', label_en: '', color: '#e07b39' })
  const [err,  setErr]  = useState('')

  async function handleCreate(e) {
    e.preventDefault()
    setErr('')
    try {
      await api.createGame(form)
      setForm({ label_fr: '', label_en: '', color: '#e07b39' })
      onRefresh()
    } catch (e) { setErr(e.message) }
  }

  return (
    <div className="adm__games-panel">
      <div className="adm__games-panel-inner">
        <div className="adm__games-panel-header">
          <h2><Gamepad2 size={18} /> Gérer les jeux</h2>
          <button className="adm__icon-btn" onClick={onClose} title="Fermer"><X size={18} /></button>
        </div>

        <form className="adm__games-form" onSubmit={handleCreate}>
          <div className="adm__games-form-fields">
            <input placeholder="Nom FR (ex: OpenFramework)" value={form.label_fr}
              onChange={e => setForm(f => ({...f, label_fr: e.target.value}))} required />
            <input placeholder="Nom EN (ex: OpenFramework)" value={form.label_en}
              onChange={e => setForm(f => ({...f, label_en: e.target.value}))} required />
            <label className="adm__color-field">
              <input type="color" value={form.color}
                onChange={e => setForm(f => ({...f, color: e.target.value}))} />
              <span>Couleur</span>
            </label>
          </div>
          {err && <p className="adm__field-error">{err}</p>}
          <button className="adm__btn adm__btn--primary" type="submit">Ajouter le jeu</button>
        </form>

        <ul className="adm__games-list">
          {games.length === 0 && <li className="adm__games-empty">Aucun jeu.</li>}
          {games.map(g => (
            <li key={g.id} className="adm__games-item">
              <span className="adm__games-dot" style={{ background: g.color ?? '#555' }} />
              <span className="adm__games-name">{g.label_fr}</span>
              <span className="adm__games-slug">/{g.slug}</span>
              <button className="adm__icon-btn adm__icon-btn--danger"
                onClick={async () => { if(confirm(`Supprimer ${g.label_fr} ?`)) { await api.deleteGame(g.id); onRefresh() } }}>
                <Trash2 size={15} />
              </button>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}

/* ── Panel équipe / rôles ──────────────────────────────────────────────────── */
const ROLE_EMOJIS = { owner: '👑', admin: '🛡', editor: '✏️', rules_editor: '📜', viewer: '👁' }
const labelForRole = (role, allRoles) => {
  const r = allRoles?.find(x => x.key === role)
  const emoji = ROLE_EMOJIS[role] || '🔹'
  return r ? `${emoji} ${r.label}` : `${emoji} ${role}`
}
const EMPTY_USER  = { steam_id: '', display_name: '', role: 'editor' }

function UsersPanel({ onClose, currentUser }) {
  const isOwner = currentUser?.role === 'owner'
  const isAdmin = currentUser?.role === 'admin'
  // admin peut ajouter/modifier les editor et viewer, mais pas changer les rôles vers owner/admin
  const canWrite = isOwner || isAdmin

  const [users,      setUsers]      = useState([])
  const [allRoles,   setAllRoles]   = useState([])  // depuis /api/permissions/roles
  const [form,       setForm]       = useState(EMPTY_USER)
  const [editId,     setEditId]     = useState(null)
  const [err,        setErr]        = useState('')
  const [loading,    setLoading]    = useState(true)
  const [repairId,   setRepairId]   = useState(null)   // id user en cours de réparation
  const [repairOld,  setRepairOld]  = useState('')
  const [repairMsg,  setRepairMsg]  = useState('')

  async function load() {
    setLoading(true)
    try {
      const [u, r] = await Promise.all([api.getUsers(), api.getRoles().catch(() => [])])
      setUsers(u)
      setAllRoles(r)
    } catch { setErr('Impossible de charger les utilisateurs') }
    finally { setLoading(false) }
  }

  useEffect(() => { load() }, [])

  function startEdit(u) {
    setEditId(u.id)
    setForm({ steam_id: u.steam_id, display_name: u.display_name, role: u.role })
    setErr('')
  }
  function cancelEdit() { setEditId(null); setForm(EMPTY_USER); setErr('') }

  async function handleSubmit(e) {
    e.preventDefault(); setErr('')
    try {
      if (editId) await api.updateUser(editId, { role: form.role, display_name: form.display_name })
      else        await api.createUser(form)
      cancelEdit(); load()
    } catch (e) { setErr(e.message) }
  }

  async function handleDelete(u) {
    if (!confirm(`Retirer ${u.display_name || u.steam_id} de l'équipe ?`)) return
    try { await api.deleteUser(u.id); load() }
    catch (e) { setErr(e.message) }
  }

  function slugify(name) { return (name || '').toLowerCase().replace(/\s+/g, '_') }

  async function handleRepair(e) {
    e.preventDefault()
    const u = users.find(x => x.id === repairId)
    if (!u) return
    const newId = slugify(u.display_name)
    const oldId = repairOld.trim()
    if (!oldId) return
    try {
      await api.migrateHubId({ oldId, newId })
      setRepairMsg(`✓ Assignations migrées de "${oldId}" → "${newId}"`)
      setRepairOld('')
      setTimeout(() => { setRepairId(null); setRepairMsg('') }, 3000)
    } catch (e) { setRepairMsg(`Erreur : ${e.message}`) }
  }

  // rôles que l'utilisateur courant peut attribuer
  // owner : tous sauf owner. admin : tout sauf owner et admin (donc editor/viewer/rules_editor/customs).
  const assignableRoles = (() => {
    const all = allRoles.map(r => r.key).filter(k => k !== 'owner')
    if (isOwner) return all
    return all.filter(k => k !== 'admin')
  })()

  // peut-on modifier cet user ?
  function canEditUser(u) {
    if (u.steam_id === currentUser?.steamId) return true  // toujours s'auto-éditer
    if (u.role === 'owner') return false
    if (isOwner) return true
    if (isAdmin && u.role !== 'admin') return true
    return false
  }

  const editingUser = users.find(u => u.id === editId)
  const isSelfEdit = editingUser?.steam_id === currentUser?.steamId

  const f = (k, v) => setForm(p => ({ ...p, [k]: v }))

  return (
    <div className="adm__games-panel">
      <div className="adm__games-panel-inner adm__jobs-panel-inner">
        <div className="adm__games-panel-header">
          <h2><Users size={18} /> Gérer l'équipe &amp; les rôles</h2>
          <button className="adm__icon-btn" onClick={onClose} title="Fermer"><X size={18} /></button>
        </div>

        {/* Formulaire (visible si canWrite) */}
        {canWrite && (
          <form className="adm__jobs-form" onSubmit={handleSubmit}>
            <h3 className="adm__jobs-form-title">
              {editId ? <><Pencil size={14} /> Modifier le membre</> : <><Plus size={14} /> Ajouter un membre</>}
            </h3>
            <div className="adm__jobs-form-grid">
              {!editId && (
                <input
                  placeholder="SteamID64 (ex: 76561198314922998)" value={form.steam_id}
                  onChange={e => f('steam_id', e.target.value)} required
                  className="adm__jobs-form-full"
                />
              )}
              <input
                placeholder="Nom d'affichage" value={form.display_name}
                onChange={e => f('display_name', e.target.value)}
              />
              {!isSelfEdit && (
                <select value={form.role} onChange={e => f('role', e.target.value)}>
                  {assignableRoles.map(r => (
                    <option key={r} value={r}>{labelForRole(r, allRoles)}</option>
                  ))}
                </select>
              )}
            </div>
            {err && <p className="adm__field-error">{err}</p>}
            <div className="adm__jobs-form-actions">
              <button className="adm__btn adm__btn--primary" type="submit">
                {editId ? <><Pencil size={14} /> Enregistrer</> : <><Plus size={14} /> Ajouter</>}
              </button>
              {editId && (
                <button type="button" className="adm__btn adm__btn--ghost" onClick={cancelEdit}>
                  <X size={14} /> Annuler
                </button>
              )}
            </div>
          </form>
        )}

        {/* Liste */}
        <ul className="adm__jobs-list">
          {loading && <li className="adm__games-empty">Chargement…</li>}
          {!loading && users.length === 0 && <li className="adm__games-empty">Aucun membre.</li>}
          {users.map(u => (
            <li key={u.id} className="adm__jobs-item">
              <div className="adm__jobs-item-info" style={{ gap: 10 }}>
                {u.avatar && <img src={u.avatar} alt="" style={{ width: 28, height: 28, borderRadius: '50%' }} />}
                <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                  <span className="adm__jobs-item-title">{u.display_name || u.steam_id}</span>
                  <span style={{ fontSize: '0.72rem', color: '#71717a' }}>{u.steam_id}</span>
                </div>
                <span style={{
                  marginLeft: 'auto', padding: '2px 8px', borderRadius: 99,
                  fontSize: '0.7rem', fontWeight: 700, letterSpacing: '0.05em',
                  background: colorForRole(u.role) + '22', color: colorForRole(u.role),
                  border: `1px solid ${colorForRole(u.role)}44`,
                }}>
                  {labelForRole(u.role, allRoles)}
                </span>
              </div>
              <div className="adm__jobs-item-actions">
                {u.steam_id === currentUser?.steamId && (
                  <Shield size={15} style={{ color: colorForRole(u.role), opacity: 0.7 }} title="C'est vous" />
                )}
                {canEditUser(u) && (
                  <>
                    <button className="adm__icon-btn" title="Modifier" onClick={() => startEdit(u)}>
                      <Pencil size={15} />
                    </button>
                    <button className="adm__icon-btn" title="Réparer les assignations (après changement de nom)" onClick={() => { setRepairId(u.id); setRepairOld(''); setRepairMsg('') }}>
                      <Wrench size={15} />
                    </button>
                    {isOwner && (
                      <button className="adm__icon-btn adm__icon-btn--danger" title="Retirer"
                        onClick={() => handleDelete(u)}>
                        <Trash2 size={15} />
                      </button>
                    )}
                  </>
                )}
              </div>
              {repairId === u.id && (
                <form onSubmit={handleRepair} style={{ marginTop: 8, padding: '10px 12px', background: 'rgba(224,123,57,0.08)', borderRadius: 8, border: '1px solid rgba(224,123,57,0.3)', display: 'flex', flexDirection: 'column', gap: 6 }}>
                  <span style={{ fontSize: '0.75rem', color: '#e07b39', fontWeight: 600 }}>Réparer les assignations</span>
                  <span style={{ fontSize: '0.7rem', color: '#999' }}>
                    Hub ID actuel : <code style={{ background: '#333', padding: '1px 5px', borderRadius: 3 }}>{slugify(u.display_name)}</code>
                  </span>
                  <div style={{ display: 'flex', gap: 6 }}>
                    <input value={repairOld} onChange={e => setRepairOld(e.target.value)} placeholder="Ancien ID (ex: ben_10)" required
                      style={{ flex: 1, background: '#2a2a2a', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 6, padding: '5px 8px', color: '#e8e0d0', fontSize: '0.78rem', fontFamily: 'inherit', outline: 'none' }} />
                    <button type="submit" className="adm__btn adm__btn--primary" style={{ padding: '5px 12px', fontSize: '0.78rem' }}>Migrer</button>
                    <button type="button" className="adm__btn adm__btn--ghost" style={{ padding: '5px 10px', fontSize: '0.78rem' }} onClick={() => { setRepairId(null); setRepairMsg('') }}>✕</button>
                  </div>
                  {repairMsg && <span style={{ fontSize: '0.72rem', color: repairMsg.startsWith('✓') ? '#3e9041' : '#d13b1a' }}>{repairMsg}</span>}
                </form>
              )}
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}

/* ── Panel emplois ──────────────────────────────────────────────────────────── */
const JOB_TYPES = ['Bénévolat', 'Stage', 'CDD', 'CDI', 'Alternance', 'Freelance']
const EMPTY_JOB = { title_fr: '', title_en: '', description_fr: '', description_en: '',
                    type: 'Bénévolat', game_slug: '', is_open: true }

function JobsPanel({ games, onClose }) {
  const [jobs,    setJobs]    = useState([])
  const [form,    setForm]    = useState(EMPTY_JOB)
  const [editId,  setEditId]  = useState(null)
  const [err,     setErr]     = useState('')
  const [loading, setLoading] = useState(true)

  async function loadJobs() {
    setLoading(true)
    try { setJobs(await api.getAdminJobs()) }
    catch { setErr('Impossible de charger les offres') }
    finally { setLoading(false) }
  }

  useEffect(() => { loadJobs() }, [])

  function startEdit(job) {
    setEditId(job.id)
    setForm({
      title_fr: job.title_fr, title_en: job.title_en,
      description_fr: job.description_fr, description_en: job.description_en,
      type: job.type, game_slug: job.game_slug ?? '',
      is_open: !!job.is_open,
    })
    setErr('')
  }

  function cancelEdit() { setEditId(null); setForm(EMPTY_JOB); setErr('') }

  async function handleSubmit(e) {
    e.preventDefault(); setErr('')
    const body = { ...form, game_slug: form.game_slug || null }
    try {
      if (editId) await api.updateJob(editId, body)
      else        await api.createJob(body)
      cancelEdit()
      loadJobs()
    } catch (e) { setErr(e.message) }
  }

  async function handleDelete(id) {
    if (!confirm('Supprimer cette offre ?')) return
    await api.deleteJob(id)
    loadJobs()
  }

  async function handleToggle(id) {
    await api.toggleJob(id)
    loadJobs()
  }

  const f = (k, v) => setForm(prev => ({ ...prev, [k]: v }))

  return (
    <div className="adm__games-panel">
      <div className="adm__games-panel-inner adm__jobs-panel-inner">
        <div className="adm__games-panel-header">
          <h2><Briefcase size={18} /> Gérer les emplois</h2>
          <button className="adm__icon-btn" onClick={onClose} title="Fermer"><X size={18} /></button>
        </div>

        {/* Formulaire */}
        <form className="adm__jobs-form" onSubmit={handleSubmit}>
          <h3 className="adm__jobs-form-title">
            {editId ? <><Pencil size={14} /> Modifier l'offre</> : <><Plus size={14} /> Nouvelle offre</>}
          </h3>
          <div className="adm__jobs-form-grid">
            <input placeholder="Titre FR *" value={form.title_fr}
              onChange={e => f('title_fr', e.target.value)} required />
            <input placeholder="Titre EN" value={form.title_en}
              onChange={e => f('title_en', e.target.value)} />
            <select value={form.type} onChange={e => f('type', e.target.value)}>
              {JOB_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
            </select>
            <select value={form.game_slug} onChange={e => f('game_slug', e.target.value)}>
              <option value="">— Aucun jeu —</option>
              {games.map(g => <option key={g.id} value={g.slug}>{g.label_fr}</option>)}
            </select>
            <textarea placeholder="Description FR" value={form.description_fr} rows={4}
              onChange={e => f('description_fr', e.target.value)}
              className="adm__jobs-form-full" />
            <textarea placeholder="Description EN" value={form.description_en} rows={4}
              onChange={e => f('description_en', e.target.value)}
              className="adm__jobs-form-full" />
            <label className="adm__jobs-open-toggle adm__jobs-form-full">
              <input type="checkbox" checked={form.is_open}
                onChange={e => f('is_open', e.target.checked)} />
              Offre ouverte (visible sur le site)
            </label>
          </div>
          {err && <p className="adm__field-error">{err}</p>}
          <div className="adm__jobs-form-actions">
            <button className="adm__btn adm__btn--primary" type="submit">
              {editId ? <><Pencil size={14} /> Enregistrer</> : <><Plus size={14} /> Créer l'offre</>}
            </button>
            {editId && (
              <button type="button" className="adm__btn adm__btn--ghost" onClick={cancelEdit}>
                <X size={14} /> Annuler
              </button>
            )}
          </div>
        </form>

        {/* Liste */}
        <ul className="adm__jobs-list">
          {loading && <li className="adm__games-empty">Chargement…</li>}
          {!loading && jobs.length === 0 && <li className="adm__games-empty">Aucune offre.</li>}
          {jobs.map(job => (
            <li key={job.id} className={`adm__jobs-item${job.is_open ? '' : ' adm__jobs-item--closed'}`}>
              <div className="adm__jobs-item-info">
                <span className="adm__jobs-item-type">{job.type}</span>
                <span className="adm__jobs-item-title">{job.title_fr}</span>
              </div>
              <div className="adm__jobs-item-actions">
                <button
                  className={`adm__icon-btn${job.is_open ? ' adm__icon-btn--success' : ''}`}
                  title={job.is_open ? "Fermer l'offre" : "Ouvrir l'offre"}
                  onClick={() => handleToggle(job.id)}
                >
                  {job.is_open ? <ToggleRight size={17} /> : <ToggleLeft size={17} />}
                </button>
                <button className="adm__icon-btn" title="Modifier" onClick={() => startEdit(job)}>
                  <Pencil size={15} />
                </button>
                <button className="adm__icon-btn adm__icon-btn--danger" title="Supprimer"
                  onClick={() => handleDelete(job.id)}>
                  <Trash2 size={15} />
                </button>
              </div>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}

/* ── Panel Bugs ─────────────────────────────────────────────────────────────── */
const BUG_STATUS_LIST = ['pending', 'confirmed', 'patched', 'wontfix']
const BUG_STATUS_LABELS = { pending: '⏳ En attente', confirmed: '🔵 Confirmé', patched: '✅ Corrigé', wontfix: '⛔ Non corrigé' }
const BUG_STATUS_COLORS = { pending: '#facc15', confirmed: '#a5b4fc', patched: '#4ade80', wontfix: '#71717a' }

function BugsPanel({ onClose }) {
  const [bugs,       setBugs]       = useState([])
  const [loading,    setLoading]    = useState(true)
  const [expanded,   setExpanded]   = useState({})
  const [commenting, setCommenting] = useState({})
  const [err,        setErr]        = useState('')

  async function load() {
    setLoading(true)
    try { setBugs(await api.getAdminBugs()) }
    catch { setErr('Impossible de charger les bugs') }
    finally { setLoading(false) }
  }
  useEffect(() => { load() }, [])

  async function patch(id, body) {
    try { await api.patchBug(id, body); load() }
    catch (e) { setErr(e.message) }
  }

  async function deleteBug(id) {
    if (!confirm('Supprimer ce signalement ?')) return
    try { await api.deleteBug(id); load() }
    catch (e) { setErr(e.message) }
  }

  async function addComment(bug) {
    const content = commenting[bug.id]?.trim()
    if (!content) return
    try {
      await api.addBugComment(bug.id, { content })
      setCommenting(c => ({ ...c, [bug.id]: '' }))
      load()
    } catch (e) { setErr(e.message) }
  }

  async function deleteComment(bugId, cid) {
    try { await api.deleteBugComment(bugId, cid); load() }
    catch (e) { setErr(e.message) }
  }

  const grouped = BUG_STATUS_LIST
    .map(s => ({ status: s, items: bugs.filter(b => b.status === s) }))
    .filter(g => g.items.length > 0)

  return (
    <div className="adm__games-panel">
      <div className="adm__games-panel-inner adm__jobs-panel-inner" style={{ maxWidth: 700 }}>
        <div className="adm__games-panel-header">
          <h2><Bug size={18} /> Signalements de bugs</h2>
          <button className="adm__icon-btn" onClick={onClose}><X size={18} /></button>
        </div>

        {err && <p className="adm__field-error">{err}</p>}
        {loading && <p className="adm__games-empty">Chargement…</p>}
        {!loading && bugs.length === 0 && <p className="adm__games-empty">Aucun signalement pour l'instant.</p>}

        {grouped.map(({ status, items }) => (
          <div key={status} style={{ marginBottom: 16 }}>
            <div style={{
              display: 'flex', alignItems: 'center', gap: 8,
              padding: '6px 0', borderBottom: '1px solid rgba(255,255,255,0.07)',
              marginBottom: 8, fontSize: '0.78rem', fontWeight: 800,
              color: BUG_STATUS_COLORS[status], letterSpacing: '0.06em',
            }}>
              {BUG_STATUS_LABELS[status]} <span style={{ color: '#52525b' }}>({items.length})</span>
            </div>
            <ul className="adm__jobs-list" style={{ gap: 6 }}>
              {items.map(bug => {
                const isOpen = expanded[bug.id]
                return (
                  <li key={bug.id} style={{
                    flexDirection: 'column', alignItems: 'stretch', padding: 0,
                    background: '#1a1a1a', borderRadius: 10,
                    border: '1px solid rgba(255,255,255,0.07)',
                  }} className="adm__jobs-item">
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px' }}>
                      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
                        <span style={{ fontWeight: 700, fontSize: '0.9rem', color: '#e2e8f0' }}>{bug.title}</span>
                        <span style={{ fontSize: '0.7rem', color: '#52525b' }}>
                          {bug.game_slug} · {new Date(bug.created_at).toLocaleDateString('fr-FR')}
                          {bug.is_public
                            ? <span style={{ color: '#4ade80', marginLeft: 6 }}>● Public</span>
                            : <span style={{ color: '#f87171', marginLeft: 6 }}>● Privé</span>}
                        </span>
                      </div>
                      <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', alignItems: 'center' }}>
                        <select
                          value={bug.status}
                          onChange={e => patch(bug.id, { status: e.target.value })}
                          style={{
                            background: '#111', border: '1px solid rgba(255,255,255,0.12)',
                            borderRadius: 6, color: BUG_STATUS_COLORS[bug.status],
                            fontSize: '0.75rem', padding: '3px 6px', cursor: 'pointer',
                          }}
                        >
                          {BUG_STATUS_LIST.map(s => (
                            <option key={s} value={s}>{BUG_STATUS_LABELS[s]}</option>
                          ))}
                        </select>
                        <button
                          className={`adm__icon-btn${bug.is_public ? ' adm__icon-btn--success' : ''}`}
                          title={bug.is_public ? 'Rendre privé' : 'Rendre public'}
                          onClick={() => patch(bug.id, { is_public: !bug.is_public })}
                        >
                          {bug.is_public ? <Eye size={15} /> : <EyeOff size={15} />}
                        </button>
                        <button
                          className="adm__icon-btn"
                          onClick={() => setExpanded(e => ({ ...e, [bug.id]: !e[bug.id] }))}
                          title="Voir détails / commentaires"
                        >
                          <MessageSquare size={15} />
                          {bug.comments?.length > 0 && (
                            <span style={{ fontSize: '0.65rem', marginLeft: 2 }}>{bug.comments.length}</span>
                          )}
                        </button>
                        <button className="adm__icon-btn adm__icon-btn--danger" onClick={() => deleteBug(bug.id)}>
                          <Trash2 size={15} />
                        </button>
                      </div>
                    </div>

                    {isOpen && (
                      <div style={{ padding: '0 14px 14px', display: 'flex', flexDirection: 'column', gap: 10 }}>
                        {bug.description && (
                          <p style={{ fontSize: '0.85rem', color: '#a1a1aa', margin: 0, lineHeight: 1.6 }}>{bug.description}</p>
                        )}
                        {bug.comments?.length > 0 && (
                          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                            <div style={{ fontSize: '0.72rem', fontWeight: 800, color: '#52525b', letterSpacing: '0.05em', textTransform: 'uppercase' }}>
                              <MessageSquare size={12} style={{ verticalAlign: 'middle', marginRight: 4 }} />Commentaires
                            </div>
                            {bug.comments.map(c => (
                              <div key={c.id} style={{
                                background: 'rgba(255,255,255,0.04)', borderLeft: '3px solid #a78bfa',
                                borderRadius: 4, padding: '8px 12px', display: 'flex', flexDirection: 'column', gap: 4,
                              }}>
                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                  <span style={{ fontSize: '0.75rem', fontWeight: 700, color: '#a78bfa' }}>{c.author}</span>
                                  <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                                    <span style={{ fontSize: '0.68rem', color: '#52525b' }}>
                                      {new Date(c.created_at).toLocaleDateString('fr-FR')}
                                    </span>
                                    <button className="adm__icon-btn adm__icon-btn--danger" style={{ padding: '2px 4px' }}
                                      onClick={() => deleteComment(bug.id, c.id)}>
                                      <Trash2 size={12} />
                                    </button>
                                  </div>
                                </div>
                                <p style={{ fontSize: '0.83rem', color: '#c5b9a8', margin: 0 }}>{c.content}</p>
                              </div>
                            ))}
                          </div>
                        )}
                        <div style={{ display: 'flex', gap: 8 }}>
                          <input
                            value={commenting[bug.id] || ''}
                            onChange={e => setCommenting(c => ({ ...c, [bug.id]: e.target.value }))}
                            onKeyDown={e => e.key === 'Enter' && addComment(bug)}
                            placeholder="Ajouter un commentaire (patch note, précision…)"
                            style={{
                              flex: 1, background: '#111',
                              border: '1px solid rgba(255,255,255,0.1)', borderRadius: 6,
                              color: '#f0ead6', fontSize: '0.83rem', padding: '7px 10px',
                            }}
                          />
                          <button
                            className="adm__btn adm__btn--primary"
                            style={{ padding: '7px 14px', fontSize: '0.8rem' }}
                            onClick={() => addComment(bug)}
                          >
                            <Plus size={13} />
                          </button>
                        </div>
                      </div>
                    )}
                  </li>
                )
              })}
            </ul>
          </div>
        ))}
      </div>
    </div>
  )
}

/* ── Stats Panel ────────────────────────────────────────────────────────── */
const ACTIVITY_ICONS = {
  post: <FileText size={13} />,
  bug:  <Bug size={13} />,
  job:  <BriefcaseIcon size={13} />,
}
const ACTIVITY_COLORS = {
  post: { published: '#4ade80', draft: '#facc15' },
  bug:  { pending: '#facc15', confirmed: '#a5b4fc', patched: '#4ade80', wontfix: '#71717a' },
  job:  { open: '#4ade80', closed: '#71717a' },
}
const ACTIVITY_LABELS = {
  post: { published: 'Publié', draft: 'Brouillon' },
  bug:  { pending: 'En attente', confirmed: 'Confirmé', patched: 'Corrigé', wontfix: 'Non corrigé' },
  job:  { open: 'Ouvert', closed: 'Fermé' },
}

function MiniBar({ value, max, color }) {
  const pct = max > 0 ? Math.round((value / max) * 100) : 0
  return (
    <div className="adm__stats-bar-track">
      <div className="adm__stats-bar-fill" style={{ width: `${pct}%`, background: color }} />
    </div>
  )
}

function StatsPanel({ onClose, posts }) {
  const [data,    setData]    = useState(null)
  const [loading, setLoading] = useState(true)
  const [err,     setErr]     = useState('')

  useEffect(() => {
    setLoading(true)
    api.getStats()
      .then(setData)
      .catch(e => setErr(e.message))
      .finally(() => setLoading(false))
  }, [])

  const BUG_STATUS_COLORS = { pending: '#facc15', confirmed: '#a5b4fc', patched: '#4ade80', wontfix: '#71717a' }
  const BUG_STATUS_LABELS = { pending: 'En attente', confirmed: 'Confirmé', patched: 'Corrigé', wontfix: 'Non corrigé' }

  // Calcule la hauteur max pour le bar-chart des mois
  const maxMonthTotal = data ? Math.max(...data.postsByMonth.map(m => m.total), 1) : 1

  return (
    <div className="adm__games-panel adm__stats-panel">
      <div className="adm__games-panel-inner adm__stats-panel-inner">
        <div className="adm__games-panel-header">
          <h2><BarChart2 size={18} /> Dashboard — Statistiques</h2>
          <button className="adm__icon-btn" onClick={onClose} title="Fermer"><X size={18} /></button>
        </div>

        {loading && <p className="adm__games-empty">Chargement…</p>}
        {err     && <p className="adm__field-error">{err}</p>}

        {data && (
          <div className="adm__stats-dashboard">

            {/* ── KPIs ── */}
            <div className="adm__stats-kpis">
              <div className="adm__stats-kpi">
                <span className="adm__stats-kpi-icon" style={{ background: '#4ade8022', color: '#4ade80' }}><FileText size={18} /></span>
                <div>
                  <div className="adm__stats-kpi-num">{data.totals.publishedPosts}</div>
                  <div className="adm__stats-kpi-label">Posts publiés</div>
                </div>
              </div>
              <div className="adm__stats-kpi">
                <span className="adm__stats-kpi-icon" style={{ background: '#facc1522', color: '#facc15' }}><FileText size={18} /></span>
                <div>
                  <div className="adm__stats-kpi-num">{data.totals.draftPosts}</div>
                  <div className="adm__stats-kpi-label">Brouillons</div>
                </div>
              </div>
              <div className="adm__stats-kpi">
                <span className="adm__stats-kpi-icon" style={{ background: '#f87171' + '22', color: '#f87171' }}><Bug size={18} /></span>
                <div>
                  <div className="adm__stats-kpi-num">{data.totals.pendingBugs}</div>
                  <div className="adm__stats-kpi-label">Bugs en attente</div>
                </div>
              </div>
              <div className="adm__stats-kpi">
                <span className="adm__stats-kpi-icon" style={{ background: '#60a5fa22', color: '#60a5fa' }}><BriefcaseIcon size={18} /></span>
                <div>
                  <div className="adm__stats-kpi-num">{data.totals.openJobs}</div>
                  <div className="adm__stats-kpi-label">Offres ouvertes</div>
                </div>
              </div>
              <div className="adm__stats-kpi">
                <span className="adm__stats-kpi-icon" style={{ background: '#a78bfa22', color: '#a78bfa' }}><Users size={18} /></span>
                <div>
                  <div className="adm__stats-kpi-num">{data.totals.users}</div>
                  <div className="adm__stats-kpi-label">Membres</div>
                </div>
              </div>
              <div className="adm__stats-kpi">
                <span className="adm__stats-kpi-icon" style={{ background: '#e07b3922', color: '#e07b39' }}><Gamepad2 size={18} /></span>
                <div>
                  <div className="adm__stats-kpi-num">{data.totals.games}</div>
                  <div className="adm__stats-kpi-label">Jeux</div>
                </div>
              </div>
            </div>

            <div className="adm__stats-grid">

              {/* ── Posts par mois ── */}
              <div className="adm__stats-card adm__stats-card--wide">
                <h3 className="adm__stats-card-title"><TrendingUp size={14} /> Posts par mois</h3>
                {data.postsByMonth.length === 0 ? (
                  <p className="adm__games-empty" style={{ padding: 0 }}>Aucun post.</p>
                ) : (
                  <div className="adm__stats-chart">
                    {data.postsByMonth.map(m => {
                      const label = new Date(m.month + '-01').toLocaleDateString('fr-FR', { month: 'short', year: '2-digit' })
                      const pct   = Math.round((m.total / maxMonthTotal) * 100)
                      const pubPct = m.total > 0 ? Math.round((m.published / m.total) * 100) : 0
                      return (
                        <div key={m.month} className="adm__stats-chart-col" title={`${m.total} post(s) — ${m.published} publié(s)`}>
                          <div className="adm__stats-chart-bar-wrap">
                            <div className="adm__stats-chart-bar" style={{ height: `${pct}%` }}>
                              <div className="adm__stats-chart-bar-pub" style={{ height: `${pubPct}%` }} />
                            </div>
                          </div>
                          <span className="adm__stats-chart-label">{label}</span>
                          <span className="adm__stats-chart-num">{m.total}</span>
                        </div>
                      )
                    })}
                  </div>
                )}
                <div className="adm__stats-legend">
                  <span><span className="adm__stats-legend-dot" style={{ background: '#4ade80' }} /> Publié</span>
                  <span><span className="adm__stats-legend-dot" style={{ background: '#334155' }} /> Brouillon</span>
                </div>
              </div>

              {/* ── Posts par jeu ── */}
              <div className="adm__stats-card">
                <h3 className="adm__stats-card-title"><Gamepad2 size={14} /> Posts par jeu</h3>
                <ul className="adm__stats-list">
                  {data.postsByGame.map(g => (
                    <li key={g.game} className="adm__stats-list-item">
                      <span className="adm__stats-dot" style={{ background: g.color ?? '#555' }} />
                      <span className="adm__stats-list-label">{g.game}</span>
                      <span className="adm__stats-list-num">{g.count}</span>
                      <MiniBar value={g.count} max={data.totals.posts || 1} color={g.color ?? '#555'} />
                    </li>
                  ))}
                  {data.postsByGame.length === 0 && <li className="adm__games-empty" style={{ padding: 0 }}>—</li>}
                </ul>
              </div>

              {/* ── Bugs par statut ── */}
              <div className="adm__stats-card">
                <h3 className="adm__stats-card-title"><Bug size={14} /> Bugs par statut</h3>
                <ul className="adm__stats-list">
                  {data.bugsByStatus.map(b => (
                    <li key={b.status} className="adm__stats-list-item">
                      <span className="adm__stats-dot" style={{ background: BUG_STATUS_COLORS[b.status] ?? '#888' }} />
                      <span className="adm__stats-list-label">{BUG_STATUS_LABELS[b.status] ?? b.status}</span>
                      <span className="adm__stats-list-num">{b.count}</span>
                      <MiniBar value={b.count} max={data.totals.bugs || 1} color={BUG_STATUS_COLORS[b.status] ?? '#888'} />
                    </li>
                  ))}
                  {data.bugsByStatus.length === 0 && <li className="adm__games-empty" style={{ padding: 0 }}>Aucun bug.</li>}
                </ul>
              </div>

              {/* ── Bugs par jeu ── */}
              <div className="adm__stats-card">
                <h3 className="adm__stats-card-title"><AlertCircle size={14} /> Top bugs par jeu</h3>
                <ul className="adm__stats-list">
                  {data.bugsByGame.map(b => (
                    <li key={b.game_slug} className="adm__stats-list-item">
                      <span className="adm__stats-dot" style={{ background: '#f87171' }} />
                      <span className="adm__stats-list-label">{b.game_slug}</span>
                      <span className="adm__stats-list-num">{b.count}</span>
                      <MiniBar value={b.count} max={data.totals.bugs || 1} color="#f87171" />
                    </li>
                  ))}
                  {data.bugsByGame.length === 0 && <li className="adm__games-empty" style={{ padding: 0 }}>Aucun bug.</li>}
                </ul>
              </div>

              {/* ── Membres par rôle ── */}
              <div className="adm__stats-card">
                <h3 className="adm__stats-card-title"><Users size={14} /> Membres par rôle</h3>
                <ul className="adm__stats-list">
                  {data.usersByRole.map(u => (
                    <li key={u.role} className="adm__stats-list-item">
                      <span className="adm__stats-dot" style={{ background: colorForRole(u.role) }} />
                      <span className="adm__stats-list-label">{(ROLE_EMOJIS[u.role] ? ROLE_EMOJIS[u.role] + ' ' : '') + u.role}</span>
                      <span className="adm__stats-list-num">{u.count}</span>
                      <MiniBar value={u.count} max={data.totals.users || 1} color={colorForRole(u.role)} />
                    </li>
                  ))}
                </ul>
              </div>

              {/* ── Activité récente ── */}
              <div className="adm__stats-card adm__stats-card--wide">
                <h3 className="adm__stats-card-title"><Activity size={14} /> Activité récente</h3>
                <ul className="adm__stats-activity">
                  {data.recentActivity.map((ev, i) => {
                    const color = (ACTIVITY_COLORS[ev.type] ?? {})[ev.status] ?? '#888'
                    const statusLabel = (ACTIVITY_LABELS[ev.type] ?? {})[ev.status] ?? ev.status
                    const date = new Date(ev.at).toLocaleDateString('fr-FR', { day: '2-digit', month: 'short', year: '2-digit' })
                    return (
                      <li key={i} className="adm__stats-activity-item">
                        <span className="adm__stats-activity-icon" style={{ color }}>{ACTIVITY_ICONS[ev.type]}</span>
                        <span className="adm__stats-activity-label">{ev.label}</span>
                        <span className="adm__stats-activity-badge" style={{ background: color + '22', color }}>{statusLabel}</span>
                        <span className="adm__stats-activity-date">{date}</span>
                      </li>
                    )
                  })}
                  {data.recentActivity.length === 0 && <li className="adm__games-empty" style={{ padding: 0 }}>Aucune activité.</li>}
                </ul>
              </div>

            </div>
          </div>
        )}
      </div>
    </div>
  )
}

