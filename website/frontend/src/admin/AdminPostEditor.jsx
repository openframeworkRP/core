import React, { useState, useEffect, useRef } from 'react'
import ReactDOM from 'react-dom'
import { api, API_BASE } from './api.js'
import './Admin.css'
import {
  Type, Heading, Image, Images, Video, Youtube, Quote, Megaphone, Minus,
  Languages, Check, Pencil, Rocket, Save, Trash2,
  ChevronUp, ChevronDown, ChevronRight, Upload, Calendar, Clock, User, Plus, Download, Eye, X as XIcon,
} from 'lucide-react'
import BlockRenderer, { Block } from '../components/devblog/BlockRenderer.jsx'
import '../components/devblog/DevBlogPost.css'


const BLOCK_TYPES = [
  { value: 'text',    label: 'Texte',      icon: Type },
  { value: 'heading', label: 'Titre',      icon: Heading },
  { value: 'image',   label: 'Image',      icon: Image },
  { value: 'gallery', label: 'Galerie',    icon: Images },
  { value: 'video',   label: 'Vidéo',      icon: Video },
  { value: 'youtube', label: 'YouTube',    icon: Youtube },
  { value: 'quote',   label: 'Citation',   icon: Quote },
  { value: 'callout', label: 'Callout',    icon: Megaphone },
  { value: 'divider', label: 'Séparateur', icon: Minus },
]

const MONTHS_FR = ['janvier','fevrier','mars','avril','mai','juin','juillet','aout','septembre','octobre','novembre','decembre']
function slugFromMonth(month) {
  const [year, mm] = (month ?? '').split('-')
  if (!year || !mm) return ''
  const name = MONTHS_FR[parseInt(mm, 10) - 1] ?? mm
  return `devlog-${name}-${year}-xxxx`
}

export default function AdminPostEditor({ postId, games, users = [], onSave, onBack }) {
  const [step,        setStep]        = useState(1)
  const [post,        setPost]        = useState(null)
  const [meta,        setMeta]        = useState({
    title_fr: '', title_en: '', excerpt_fr: '', excerpt_en: '',
    cover: '',
    month: '', author: 'Small Box Studio', read_time: 5, games: [],
  })
  const [blocksFr,    setBlocksFr]    = useState([])
  const [blocksEn,    setBlocksEn]    = useState([])
  const [saving,      setSaving]      = useState(false)
  const [translating, setTranslating] = useState(false)
  const [error,       setError]       = useState('')
  const [previewOpen, setPreviewOpen] = useState(false)

  useEffect(() => {
    if (!postId) return
    api.getPosts().then(all => {
      const p = all.find(x => x.id === postId)
      if (!p) return
      setPost(p)
      setMeta({
        title_fr:   p.title_fr   ?? '',
        title_en:   p.title_en   ?? '',
        excerpt_fr: p.excerpt_fr ?? '',
        excerpt_en: p.excerpt_en ?? '',
        cover:      p.cover      ?? '',
        month:      p.month      ?? '',
        author:     p.author     ?? 'Small Box Studio',
        read_time:  p.read_time  ?? 5,
        games:      (p.games ?? []).map(g => g.slug),
      })
      setBlocksFr(p.blocksFr ?? [])
      setBlocksEn(p.blocksEn ?? [])
    })
  }, [postId])

  async function saveMeta(extraMeta = {}) {
    setSaving(true); setError('')
    try {
      // À la création, title_en et excerpt_en sont requis par l'API.
      // On met provisoirement les valeurs FR — elles seront écrasées après traduction.
      const payload = {
        ...meta,
        title_en:   meta.title_en   || meta.title_fr,
        excerpt_en: meta.excerpt_en || meta.excerpt_fr,
        ...extraMeta,
      }
      let saved
      if (post) {
        saved = await api.updatePost(post.id, payload)
      } else {
        saved = await api.createPost(payload)
      }
      setPost(saved)
      return saved
    } catch (e) {
      setError(e.message)
      return null
    } finally {
      setSaving(false)
    }
  }

  async function handleGoToTranslate() {
    if (!meta.title_fr.trim()) { setError('Le titre FR est requis.'); return }
    if (blocksFr.length === 0) { setError('Ajoute au moins un bloc de contenu.'); return }
    setError('')
    const saved = await saveMeta()
    if (!saved) return
    setTranslating(true)
    try {
      const result = await api.translate({
        title_fr:   meta.title_fr,
        excerpt_fr: meta.excerpt_fr,
        blocks:     blocksFr.map(b => ({ type: b.type, data: b.data ?? {}, game_slug: b.game_slug })),
      })
      const newMeta = { ...meta, title_en: result.title_en, excerpt_en: result.excerpt_en }
      setMeta(newMeta)
      await api.updatePost(saved.id, { title_en: result.title_en, excerpt_en: result.excerpt_en })
      for (const b of blocksEn) await api.deleteBlock(saved.id, b.id)
      const newBlocksEn = []
      for (let i = 0; i < result.blocks.length; i++) {
        const rb = result.blocks[i]
        // Récupérer l'auteur du bloc FR correspondant (même position)
        const authorFr = blocksFr[i]?.author ?? null
        const created = await api.createBlock(saved.id, {
          lang: 'en', type: rb.type, game_slug: rb.game_slug ?? null, position: i, data: rb.data,
          author: authorFr,
        })
        newBlocksEn.push(created)
      }
      setBlocksEn(newBlocksEn)
      setStep(2)
    } catch (e) {
      setError('Traduction échouée : ' + e.message)
    } finally {
      setTranslating(false)
    }
  }

  async function handleFinish(publish) {
    if (!post) return
    setSaving(true); setError('')
    try {
      if (publish) { await api.publishPost(post.id) }
      else         { await api.unpublishPost(post.id) }
      onSave()
    } catch (e) {
      setError(e.message)
    } finally {
      setSaving(false)
    }
  }

  // Sauvegarde directe sans re-traduction (pour les mises à jour)
  async function handleQuickSave() {
    if (!meta.title_fr.trim()) { setError('Le titre FR est requis.'); return }
    setSaving(true); setError('')
    try {
      await saveMeta()
      onSave()
    } catch (e) {
      setError(e.message)
    } finally {
      setSaving(false)
    }
  }

  async function addBlock(type, afterIdx = null) {
    let pId = post?.id
    if (!pId) {
      const saved = await saveMeta()
      if (!saved) return
      pId = saved.id
    }
    // Position d'insertion : après afterIdx, ou à la fin
    const insertAt = afterIdx !== null ? afterIdx + 1 : blocksFr.length
    const block = await api.createBlock(pId, {
      lang: 'fr', type, position: insertAt, game_slug: null, data: defaultData(type),
    })
    // Insérer dans la liste locale à la bonne position et recalculer toutes les positions
    const next = [...blocksFr]
    next.splice(insertAt, 0, block)
    const reordered = next.map((x, i) => ({ ...x, position: i }))
    setBlocksFr(reordered)
    // Sauvegarder les nouvelles positions en base pour éviter les ex-æquo
    await api.reorderBlocks(pId, { fr: reordered.map(b => ({ id: b.id, position: b.position })), en: [] })
  }

  async function updateBlock(block, changes) {
    const pId = post?.id; if (!pId) return
    const updated = await api.updateBlock(pId, block.id, changes)
    setBlocksFr(b => b.map(x => x.id === block.id ? updated : x))

    // Si l'auteur change, propager sur le bloc EN de même position
    if ('author' in changes) {
      const idx = blocksFr.findIndex(b => b.id === block.id)
      const enBlock = blocksEn[idx]
      if (enBlock) {
        const enChanges = { author: changes.author }
        // Propager aussi l'avatar si présent dans data
        if (changes.data?.author_avatar !== undefined) {
          const enData = { ...(enBlock.data ?? {}), author_avatar: changes.data.author_avatar }
          enChanges.data = enData
        }
        const updatedEn = await api.updateBlock(pId, enBlock.id, enChanges)
        setBlocksEn(b => b.map(x => x.id === enBlock.id ? updatedEn : x))
      }
    }
  }

  async function deleteBlock(block) {
    const pId = post?.id; if (!pId) return
    await api.deleteBlock(pId, block.id)
    const reordered = blocksFr
      .filter(x => x.id !== block.id)
      .map((x, i) => ({ ...x, position: i }))
    setBlocksFr(reordered)
    // Resynchroniser les positions en base après suppression
    if (reordered.length > 0) {
      await api.reorderBlocks(pId, { fr: reordered.map(b => ({ id: b.id, position: b.position })), en: [] })
    }
  }

  async function moveBlock(block, dir) {
    // Construire les sections depuis blocksFr
    function buildSections(list) {
      const sections = []
      for (const b of list) {
        if (b.type === 'heading') {
          sections.push({ heading: b, children: [] })
        } else {
          if (sections.length === 0) sections.push({ heading: null, children: [] })
          sections[sections.length - 1].children.push(b)
        }
      }
      return sections
    }

    function flattenSections(sections) {
      const out = []
      for (const s of sections) {
        if (s.heading) out.push(s.heading)
        out.push(...s.children)
      }
      return out.map((b, i) => ({ ...b, position: i }))
    }

    // Si c'est un heading, on déplace toute la section (heading + ses enfants)
    if (block.type === 'heading') {
      const sections = buildSections(blocksFr)
      const sIdx = sections.findIndex(s => s.heading?.id === block.id)
      if (sIdx === -1) return
      const newSIdx = sIdx + dir
      if (newSIdx < 0 || newSIdx >= sections.length) return
      const newSections = [...sections]
      ;[newSections[sIdx], newSections[newSIdx]] = [newSections[newSIdx], newSections[sIdx]]
      const reordered = flattenSections(newSections)
      setBlocksFr(reordered)
      const pId = post?.id; if (!pId) return
      await api.reorderBlocks(pId, { fr: reordered.map(b => ({ id: b.id, position: b.position })), en: [] })
    } else {
      // Bloc de contenu : peut traverser les sections (heading)
      const sections = buildSections(blocksFr)

      // Trouver la section et la position du bloc dans cette section
      let sIdx = -1, cIdx = -1
      for (let i = 0; i < sections.length; i++) {
        const ci = sections[i].children.findIndex(b => b.id === block.id)
        if (ci !== -1) { sIdx = i; cIdx = ci; break }
      }
      if (sIdx === -1) return

      const newSections = sections.map(s => ({ ...s, children: [...s.children] }))
      const child = newSections[sIdx].children[cIdx]

      if (dir === -1) {
        // Monter : si pas le premier de la section, swap avec le voisin du dessus
        if (cIdx > 0) {
          ;[newSections[sIdx].children[cIdx], newSections[sIdx].children[cIdx - 1]] =
            [newSections[sIdx].children[cIdx - 1], newSections[sIdx].children[cIdx]]
        } else {
          // Premier de la section → aller à la fin de la section précédente
          if (sIdx === 0) return // déjà tout en haut
          newSections[sIdx].children.splice(cIdx, 1)
          newSections[sIdx - 1].children.push(child)
        }
      } else {
        // Descendre : si pas le dernier de la section, swap avec le voisin du dessous
        if (cIdx < newSections[sIdx].children.length - 1) {
          ;[newSections[sIdx].children[cIdx], newSections[sIdx].children[cIdx + 1]] =
            [newSections[sIdx].children[cIdx + 1], newSections[sIdx].children[cIdx]]
        } else {
          // Dernier de la section → aller au début de la section suivante
          if (sIdx === newSections.length - 1) return // déjà tout en bas
          newSections[sIdx].children.splice(cIdx, 1)
          newSections[sIdx + 1].children.unshift(child)
        }
      }

      const reordered = flattenSections(newSections)
      setBlocksFr(reordered)
      const pId = post?.id; if (!pId) return
      await api.reorderBlocks(pId, { fr: reordered.map(b => ({ id: b.id, position: b.position })), en: [] })
    }
  }

  const gameOptions = games.filter(g => g.slug !== 'all')
  const STEPS = [
    { n: 1, label: 'Rédiger en FR' },
    { n: 2, label: 'Vérifier la traduction' },
    { n: 3, label: 'Publier' },
  ]

  return (
    <div className="adm adm--editor">
      <header className="adm__header">
        <button className="adm__btn adm__btn--ghost" onClick={step === 1 ? onBack : () => setStep(s => s - 1)}>
          {step === 1 ? '← Retour' : '← Étape précédente'}
        </button>
        <div className="adm__stepper">
          {STEPS.map((s, i) => (
            <div key={s.n} className="adm__stepper-item">
              <div className={`adm__stepper-dot${step === s.n ? ' adm__stepper-dot--active' : step > s.n ? ' adm__stepper-dot--done' : ''}`}>
                {step > s.n ? <Check size={14} /> : s.n}
              </div>
              <span className={`adm__stepper-label${step === s.n ? ' adm__stepper-label--active' : ''}`}>{s.label}</span>
              {i < STEPS.length - 1 && <div className={`adm__stepper-line${step > s.n ? ' adm__stepper-line--done' : ''}`} />}
            </div>
          ))}
        </div>
        {post
          ? (
            <div style={{ display: 'flex', gap: 8 }}>
              <button
                className="adm__btn adm__btn--ghost"
                title="Aperçu du devblog"
                onClick={() => setPreviewOpen(true)}
              >
                <Eye size={15} /> Aperçu
              </button>
              <button
                className="adm__btn adm__btn--ghost"
                title="Exporter ce devblog (.devblog)"
                onClick={() => {
                  const a = document.createElement('a')
                  a.href = api.exportPost(post.id)
                  a.download = `${post.slug}.devblog`
                  a.click()
                }}
              >
                <Download size={15} /> Exporter .devblog
              </button>
            </div>
          )
          : <span style={{ minWidth: 120 }} />
        }
      </header>

      {error && <p className="adm__error adm__error--top">{error}</p>}

      {step === 1 && (
        <div className="adm__editor-layout">
          <aside className="adm__meta-panel">
            <h3>Informations</h3>
            <MonthPicker value={meta.month} onChange={v => setMeta(m => ({...m, month: v}))} />
            {meta.month && !post && (
              <p style={{ fontSize: 11, color: '#888', marginTop: -8, marginBottom: 4, wordBreak: 'break-all' }}>
                🔗 /devblog/{slugFromMonth(meta.month)}
              </p>
            )}
            {post && (
              <p style={{ fontSize: 11, color: '#888', marginTop: -8, marginBottom: 4, wordBreak: 'break-all' }}>
                🔗 /devblog/{post.slug}
              </p>
            )}
            <label>
              <span>Titre FR</span>
              <input value={meta.title_fr} placeholder="Devlog #1 — Mars 2026"
                onChange={e => setMeta(m => ({...m, title_fr: e.target.value}))} />
            </label>
            <label>
              <span>Résumé FR</span>
              <textarea rows={3} value={meta.excerpt_fr} placeholder="Courte description du devlog…"
                onChange={e => setMeta(m => ({...m, excerpt_fr: e.target.value}))} />
            </label>
            <ImageUploadField
              label="Image de présentation"
              value={meta.cover}
              onChange={v => setMeta(m => ({...m, cover: v}))}
            />
            <label>Auteur
              <input value={meta.author}
                onChange={e => setMeta(m => ({...m, author: e.target.value}))} />
            </label>
            <label>Temps de lecture (min)
              <input type="number" min={1} max={60} value={meta.read_time}
                onChange={e => setMeta(m => ({...m, read_time: Number(e.target.value)}))} />
            </label>
            <fieldset className="adm__fieldset">
              <legend>Jeux abordés</legend>
              {gameOptions.map(g => (
                <label key={g.slug} className="adm__checkbox">
                  <input type="checkbox"
                    checked={meta.games.includes(g.slug)}
                    onChange={e => setMeta(m => ({
                      ...m,
                      games: e.target.checked ? [...m.games, g.slug] : m.games.filter(s => s !== g.slug)
                    }))} />
                  <span style={{ color: g.color ?? '#aaa' }}>{g.label_fr}</span>
                </label>
              ))}
            </fieldset>
          </aside>

          <section className="adm__blocks-area">
            <div className="adm__blocks-area-header">
              <h2 className="adm__blocks-area-title">
                Contenu <span className="adm__lang-badge adm__lang-badge--lg">Français</span>
              </h2>
              <p className="adm__blocks-area-hint">
                Écris uniquement en français. La traduction EN sera générée automatiquement à l'étape suivante.
              </p>
            </div>

            <SectionsEditor
              blocks={blocksFr}
              games={gameOptions}
              users={users}
              onAdd={addBlock}
              onUpdate={updateBlock}
              onDelete={deleteBlock}
              onMove={moveBlock}
            />

            <div className="adm__step-footer">
              {post ? (
                /* ── Mode édition : sauvegarde rapide + option re-traduction ── */
                <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'center' }}>
                  <button
                    className="adm__btn adm__btn--publish"
                    onClick={handleQuickSave}
                    disabled={saving || translating}
                  >
                    {saving
                      ? <><span className="adm__spinner" /> Sauvegarde…</>
                      : <><Save size={16} /> Sauvegarder les modifications</>}
                  </button>
                  <button
                    className={`adm__btn adm__btn--ghost${translating ? ' adm__btn--translating' : ''}`}
                    onClick={handleGoToTranslate}
                    disabled={translating || saving}
                    title="Regénère la traduction EN depuis le contenu FR"
                  >
                    {translating
                      ? <><span className="adm__spinner" /> Traduction…</>
                      : <><Languages size={15} /> Retraduire &amp; prévisualiser</>}
                  </button>
                </div>
              ) : (
                /* ── Mode création : flow normal ── */
                <button
                  className={`adm__btn adm__btn--next${translating ? ' adm__btn--translating' : ''}`}
                  onClick={handleGoToTranslate}
                  disabled={translating || saving}
                >
                  {translating
                    ? <><span className="adm__spinner" /> Traduction en cours…</>
                    : <><Languages size={16} /> Traduire &amp; prévisualiser <ChevronRight size={16} /></>}
                </button>
              )}
            </div>
          </section>
        </div>
      )}

      {step === 2 && (
        <div className="adm__preview-split-wrap">
          <div className="adm__preview-split">
            <PreviewColumn lang="fr" meta={meta} blocks={blocksFr} games={gameOptions} />
            <PreviewColumn lang="en" meta={meta} blocks={blocksEn} games={gameOptions} />
          </div>
          <div className="adm__step-footer adm__step-footer--centered">
            <p className="adm__step-footer-hint">La traduction te convient ?</p>
            <div style={{ display: 'flex', gap: 12, justifyContent: 'center' }}>
              <button className="adm__btn adm__btn--ghost" onClick={() => setStep(1)}>
                <Pencil size={15} /> Modifier le contenu
              </button>
              <button className="adm__btn adm__btn--next" onClick={() => setStep(3)}>
                <Check size={15} /> Oui, continuer <ChevronRight size={15} />
              </button>
            </div>
          </div>
        </div>
      )}

      {step === 3 && (
        <div className="adm__publish-step">
          <div className="adm__publish-card">
            <div className="adm__publish-icon"><Rocket size={40} /></div>
            {meta.cover && (
              <img src={meta.cover} alt="cover" className="adm__publish-cover-preview" />
            )}
            <h2 className="adm__publish-title">{meta.title_fr || 'Devlog sans titre'}</h2>
            {meta.excerpt_fr && <p className="adm__publish-sub">{meta.excerpt_fr}</p>}
            <div className="adm__publish-meta">
              {meta.month && <span><Calendar size={13} /> {meta.month}</span>}
              <span>{meta.author}</span>
              <span><Clock size={13} /> {meta.read_time} min</span>
              <span>FR {blocksFr.length} blocs · EN {blocksEn.length} blocs</span>
            </div>
            {error && <p className="adm__error" style={{ marginTop: 12 }}>{error}</p>}
            <div className="adm__publish-actions">
              <button
                className="adm__btn adm__btn--ghost adm__btn--lg"
                onClick={() => handleFinish(false)}
                disabled={saving}
              >
                <Save size={16} /> {post?.published ? 'Dépublier' : 'Enregistrer en brouillon'}
              </button>
              <button
                className="adm__btn adm__btn--publish adm__btn--lg"
                onClick={() => handleFinish(true)}
                disabled={saving}
              >
                {saving
                  ? <><span className="adm__spinner" /> Publication…</>
                  : <><Rocket size={16} /> {post?.published ? 'Republier' : 'Publier maintenant'}</>}
              </button>
            </div>
          </div>
        </div>
      )}

      {previewOpen && (
        <PostPreviewModal
          meta={meta}
          blocks={blocksFr}
          games={gameOptions}
          onClose={() => setPreviewOpen(false)}
        />
      )}
    </div>
  )
}

function flattenBlock(block) {
  return { ...block, ...(block.data ?? {}), game: block.game_slug ?? block.game ?? null }
}

/* ─────────────────────────────────────────────────────────
   Modal de prévisualisation plein écran
───────────────────────────────────────────────────────── */
function PostPreviewModal({ meta, blocks, games, onClose }) {
  const flat = blocks.map(flattenBlock)
  const postGames = games.filter(g => meta.games.includes(g.slug))
  const monthLabel = meta.month
    ? new Date(meta.month + '-01').toLocaleDateString('fr-FR', { month: 'long', year: 'numeric' })
    : ''

  // Bloquer le scroll de la page derrière
  useEffect(() => {
    document.body.style.overflow = 'hidden'
    return () => { document.body.style.overflow = '' }
  }, [])

  // Fermer avec Escape
  useEffect(() => {
    function onKey(e) { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [onClose])

  return ReactDOM.createPortal(
    <div className="adm__preview-modal-overlay">
      <div className="adm__preview-modal-bar">
        <span className="adm__preview-modal-badge">
          <Eye size={14} /> Aperçu — Version française
        </span>
        <button className="adm__preview-modal-close" onClick={onClose}>
          <XIcon size={18} /> Fermer
        </button>
      </div>

      <div className="adm__preview-modal-body">
        <article className="dbp">
          {/* Hero */}
          <header className="dbp__hero">
            {meta.cover && <img src={meta.cover} alt={meta.title_fr} className="dbp__hero-bg-img" />}
            <div className="dbp__hero-overlay" />
            <div className="dbp__hero-inner">
              <div className="dbp__hero-badges">
                {monthLabel && <span className="dbp__month-badge">{monthLabel}</span>}
                {postGames.map(g => (
                  <span key={g.slug} className={`dbp__game-pill dbp__game-pill--${g.slug}`}
                    style={g.color ? { background: g.color } : {}}>
                    {g.label_fr}
                  </span>
                ))}
              </div>
              <h1 className="dbp__title">{meta.title_fr || <em style={{opacity:0.4}}>Titre non renseigné</em>}</h1>
              <div className="dbp__meta">
                {meta.author && <span className="dbp__author">{meta.author}</span>}
                {meta.author && meta.read_time > 0 && <span className="dbp__sep">·</span>}
                {meta.read_time > 0 && <span className="dbp__read-time">{meta.read_time} min de lecture</span>}
              </div>
            </div>
          </header>

          {/* Contenu */}
          <div className="dbp__layout">
            <div className="dbp__content">
              {flat.length === 0
                ? <p style={{color:'#555',textAlign:'center',padding:'60px 0'}}>Aucun bloc de contenu.</p>
                : <BlockRenderer blocks={flat} lang="fr" />
              }
            </div>
          </div>
        </article>
      </div>
    </div>,
    document.body
  )
}

function PreviewColumn({ lang, meta, blocks, games }) {
  const flat    = blocks.map(flattenBlock)
  const title   = lang === 'fr' ? meta.title_fr   : meta.title_en
  const excerpt = lang === 'fr' ? meta.excerpt_fr  : meta.excerpt_en
  const monthLabel = meta.month
    ? new Date(meta.month + '-01').toLocaleDateString(lang === 'fr' ? 'fr-FR' : 'en-US', { month: 'long', year: 'numeric' })
    : ''
  const postGames = games.filter(g => meta.games.includes(g.slug))

  return (
    <div className="adm__preview-col">
      <div className="adm__preview-col-header">
        <span>{lang === 'fr' ? 'Français' : 'English'}</span>
      </div>
      <div className="adm__preview-col-body">
        {monthLabel && (
          <p style={{ color: '#4a4540', fontSize: '0.75rem', fontWeight: 800, textTransform: 'uppercase', letterSpacing: '0.12em', margin: '0 0 8px' }}>
            {monthLabel}
          </p>
        )}
        <h1 className="adm__preview-post-title">{title || <em style={{color:'#4a4540'}}>Titre non renseigné</em>}</h1>
        <div className="adm__preview-post-meta">
          {meta.author && <span>Par <strong>{meta.author}</strong></span>}
          {meta.read_time > 0 && <span><Clock size={13} /> {meta.read_time} min</span>}
          {postGames.map(g => (
            <span key={g.slug} style={{ padding:'2px 9px', borderRadius:999, fontSize:'0.65rem', fontWeight:800, background: g.color ?? '#e07b39', color:'#fff' }}>
              {g.label_fr}
            </span>
          ))}
        </div>
        {excerpt && <p className="adm__preview-excerpt">{excerpt}</p>}
        <hr className="adm__preview-divider" />
        {flat.length === 0
          ? <p style={{color:'#4a4540',textAlign:'center',padding:'40px 0'}}>Aucun bloc.</p>
          : flat.map((block, i) => <Block key={block.id ?? i} block={block} />)
        }
      </div>
    </div>
  )
}

/* ─────────────────────────────────────────────────────────
   Types de blocs de CONTENU (pas heading)
───────────────────────────────────────────────────────── */
const CONTENT_BLOCK_TYPES = [
  { value: 'text',    label: 'Texte',      icon: Type },
  { value: 'image',   label: 'Image',      icon: Image },
  { value: 'gallery', label: 'Galerie',    icon: Images },
  { value: 'video',   label: 'Vidéo',      icon: Video },
  { value: 'youtube', label: 'YouTube',    icon: Youtube },
  { value: 'quote',   label: 'Citation',   icon: Quote },
  { value: 'callout', label: 'Callout',    icon: Megaphone },
  { value: 'divider', label: 'Séparateur', icon: Minus },
]

/* ─────────────────────────────────────────────────────────
   Mini inserter inline (ligne +)
───────────────────────────────────────────────────────── */
function InlineInserter({ onAdd, types = CONTENT_BLOCK_TYPES }) {
  const [open, setOpen] = useState(false)
  const [menuPos, setMenuPos] = useState({ top: 0, left: 0 })
  const btnRef = useRef()
  const menuRef = useRef()

  function handleOpen() {
    if (!open && btnRef.current) {
      const rect = btnRef.current.getBoundingClientRect()
      setMenuPos({
        top: rect.bottom + 4,
        left: rect.left + rect.width / 2,
      })
    }
    setOpen(o => !o)
  }

  // Recalculer la position si le menu est ouvert et que la fenêtre scrolle ou est redimensionnée
  useEffect(() => {
    if (!open) return
    function reposition() {
      if (btnRef.current) {
        const rect = btnRef.current.getBoundingClientRect()
        setMenuPos({ top: rect.bottom + 4, left: rect.left + rect.width / 2 })
      }
    }
    window.addEventListener('scroll', reposition, true)
    window.addEventListener('resize', reposition)
    return () => {
      window.removeEventListener('scroll', reposition, true)
      window.removeEventListener('resize', reposition)
    }
  }, [open])

  // Fermer si clic en dehors
  useEffect(() => {
    if (!open) return
    function handleClick(e) {
      if (!btnRef.current?.contains(e.target) && !menuRef.current?.contains(e.target))
        setOpen(false)
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [open])

  const menu = open && ReactDOM.createPortal(
    <div
      ref={menuRef}
      className="adm__inline-inserter-menu"
      style={{ position: 'fixed', top: menuPos.top, left: menuPos.left, transform: 'translateX(-50%)' }}
    >
      {types.map(bt => (
        <button key={bt.value} className="adm__inserter-item"
          onClick={() => { onAdd(bt.value); setOpen(false) }}>
          <bt.icon size={13} />
          <span>{bt.label}</span>
        </button>
      ))}
    </div>,
    document.body
  )

  return (
    <div className="adm__inline-inserter">
      <button
        ref={btnRef}
        className={`adm__inline-inserter-btn${open ? ' adm__inline-inserter-btn--open' : ''}`}
        onClick={handleOpen}
        title="Insérer un bloc ici"
      >
        <span className="adm__inline-inserter-line" />
        <span className="adm__inline-inserter-plus">+</span>
        <span className="adm__inline-inserter-line" />
      </button>
      {menu}
    </div>
  )
}

/* ─────────────────────────────────────────────────────────
   Bloc de contenu (text / image / video / etc.)
───────────────────────────────────────────────────────── */
function ContentBlockCard({ block, isFirst, isLast, games, onUpdate, onDelete, onMove }) {
  const [open, setOpen] = useState(false)
  const blockType = CONTENT_BLOCK_TYPES.find(b => b.value === block.type)
  const BlockIcon = blockType?.icon
  const d = block.data ?? {}

  // Label de prévisualisation selon le type
  function preview() {
    if (block.type === 'text')    return d.content?.slice(0, 60) || <em>Texte vide</em>
    if (block.type === 'image')   return d.src ? <span className="adm__cb-preview-url">{d.src.split('/').pop()}</span> : <em>Pas d’image</em>
    if (block.type === 'gallery') return (d.images?.length) ? <span className="adm__cb-preview-url">{d.images.length} image{d.images.length > 1 ? 's' : ''}</span> : <em>Galerie vide</em>
    if (block.type === 'video')   return d.src ? <span className="adm__cb-preview-url">{d.src.split('/').pop()}</span> : <em>Pas de vidéo</em>
    if (block.type === 'youtube') return d.id  ? <span className="adm__cb-preview-url">youtu.be/{d.id}</span> : <em>Pas d’ID</em>
    if (block.type === 'quote')   return d.content?.slice(0, 50) || <em>Citation vide</em>
    if (block.type === 'callout') return d.content?.slice(0, 50) || <em>Callout vide</em>
    if (block.type === 'divider') return <em>― Séparateur</em>
    return null
  }

  return (
    <div className={`adm__cb adm__cb--${block.type}`}>
      <div className="adm__cb-header" onClick={() => setOpen(o => !o)}>
        <span className="adm__cb-toggle">{open ? <ChevronDown size={14}/> : <ChevronRight size={14}/>}</span>
        <span className="adm__cb-icon">{BlockIcon && <BlockIcon size={13} />}</span>
        <span className="adm__cb-label">{blockType?.label ?? block.type}</span>
        <span className="adm__cb-preview">{preview()}</span>
        {block.game_slug && <span className="adm__block-game-badge">{block.game_slug}</span>}
        <div className="adm__cb-actions" onClick={e => e.stopPropagation()}>
          <button disabled={isFirst} onClick={() => onMove(block, -1)} title="Monter"><ChevronUp size={14}/></button>
          <button disabled={isLast}  onClick={() => onMove(block, +1)} title="Descendre"><ChevronDown size={14}/></button>
          <button className="adm__btn--danger-icon" onClick={() => { if(confirm('Supprimer ?')) onDelete(block) }} title="Supprimer"><Trash2 size={13}/></button>
        </div>
      </div>
      {open && (
        <div className="adm__cb-body">
          <div className="adm__block-meta-row">
            <label className="adm__label">Jeu associé
              <select value={block.game_slug ?? ''}
                onChange={e => onUpdate(block, { game_slug: e.target.value || null })}>
                <option value="">Global (tous)</option>
                {games.map(g => <option key={g.slug} value={g.slug}>{g.label_fr}</option>)}
              </select>
            </label>
          </div>
          <BlockFields block={block} onUpdate={onUpdate} />
        </div>
      )}
    </div>
  )
}

/* ─────────────────────────────────────────────────────────
   SectionsEditor — vue principale
   Regroupe les blocs : heading = début de section
───────────────────────────────────────────────────────── */
function SectionsEditor({ blocks, games, users, onAdd, onUpdate, onDelete, onMove }) {
  const sections = []
  for (const block of blocks) {
    if (block.type === 'heading') {
      sections.push({ heading: block, children: [] })
    } else {
      if (sections.length === 0) sections.push({ heading: null, children: [] })
      sections[sections.length - 1].children.push(block)
    }
  }

  // Liste plate des blocs de contenu (hors headings) pour déterminer isFirst/isLast
  const contentBlocks = blocks.filter(b => b.type !== 'heading')

  function globalIdx(blockId) {
    return blocks.findIndex(b => b.id === blockId)
  }

  return (
    <div className="adm__sections">
      {blocks.length === 0 && (
        <div className="adm__sections-empty">
          <p>Commence par ajouter une section ↓</p>
          <button className="adm__btn adm__btn--ghost"
            onClick={() => onAdd('heading')}>
            <Heading size={15} /> Nouvelle section
          </button>
        </div>
      )}

      {sections.map((section, si) => {
        // Index du dernier bloc de cette section (heading ou dernier child)
        const lastBlock = section.children[section.children.length - 1] ?? section.heading
        const afterIdx = lastBlock ? globalIdx(lastBlock.id) : -1
        const headingIdx = section.heading ? globalIdx(section.heading.id) : -1

        return (
          <React.Fragment key={section.heading?.id ?? `orphan-${si}`}>
            <div className="adm__section">
              {/* En-tête */}
              {section.heading ? (
                <SectionHeader
                  block={section.heading}
                  sectionIdx={si}
                  sectionCount={sections.filter(s => s.heading).length}
                  users={users}
                  onUpdate={onUpdate}
                  onDelete={onDelete}
                  onMove={onMove}
                />
              ) : (
                <div className="adm__section-orphan-label">Blocs sans section</div>
              )}

              {/* Blocs de contenu */}
              <div className="adm__section-body">
                {section.children.length === 0 && (
                  <>
                    <p className="adm__section-empty-hint">Ajoute du contenu dans cette section</p>
                    <InlineInserter onAdd={type => onAdd(type, headingIdx)} />
                  </>
                )}
                {section.children.map((child) => {
                  const contentIdx = contentBlocks.findIndex(b => b.id === child.id)
                  return (
                  <React.Fragment key={child.id}>
                    <ContentBlockCard
                      block={child}
                      isFirst={contentIdx === 0}
                      isLast={contentIdx === contentBlocks.length - 1}
                      games={games}
                      onUpdate={onUpdate}
                      onDelete={onDelete}
                      onMove={onMove}
                    />
                    <InlineInserter
                      onAdd={type => onAdd(type, globalIdx(child.id))}
                    />
                  </React.Fragment>
                )})}
              </div>
            </div>

            {/* Inserter ENTRE sections — en dehors du div.adm__section */}
            <InlineInserter
              onAdd={type => onAdd(type, afterIdx)}
              types={[{ value: 'heading', label: 'Nouvelle section', icon: Heading }, ...CONTENT_BLOCK_TYPES]}
            />
          </React.Fragment>
        )
      })}

      <button className="adm__add-section-btn" onClick={() => onAdd('heading', blocks.length - 1)}>
        <Heading size={15} /> Nouvelle section
      </button>
    </div>
  )
}

/* ─────────────────────────────────────────────────────────
   En-tête de section (bloc heading éditable)
───────────────────────────────────────────────────────── */
function SectionHeader({ block, sectionIdx, sectionCount, users, onUpdate, onDelete, onMove }) {
  const d = block.data ?? {}
  const [expanded, setExpanded] = useState(true)
  const [localTitle, setLocalTitle] = useState(d.content ?? '')
  const prevBlockId = useRef(block.id)
  useEffect(() => {
    if (prevBlockId.current !== block.id) {
      setLocalTitle(d.content ?? '')
      prevBlockId.current = block.id
    }
  }, [block.id, d.content])

  return (
    <div className="adm__section-header">
      <div className="adm__section-header-top">
        <button className="adm__section-collapse" onClick={() => setExpanded(o => !o)}>
          {expanded ? <ChevronDown size={16}/> : <ChevronRight size={16}/>}
        </button>
        <div className="adm__section-title-preview">
          <Heading size={14} className="adm__section-title-icon" />
          <span>{d.content || <em className="adm__section-title-empty">Section sans titre</em>}</span>
          {block.author && (
            <span className="adm__block-author-badge"><User size={10} /> {block.author}</span>
          )}
        </div>
        <div className="adm__section-actions">
          <button disabled={sectionIdx === 0}                   onClick={() => onMove(block, -1)} title="Monter"><ChevronUp size={15}/></button>
          <button disabled={sectionIdx === sectionCount - 1}   onClick={() => onMove(block, +1)} title="Descendre"><ChevronDown size={15}/></button>
          <button className="adm__btn--danger-icon" onClick={() => { if(confirm('Supprimer cette section et tout son contenu ?')) onDelete(block) }}><Trash2 size={14}/></button>
        </div>
      </div>
      {expanded && (
        <div className="adm__section-header-fields">
          <label className="adm__label adm__label--full">
            <span>Titre de la section</span>
            <input
              className="adm__heading-input"
              value={localTitle}
              placeholder="Ex : Optimisations de rendu"
              onChange={e => setLocalTitle(e.target.value)}
              onBlur={() => onUpdate(block, { data: { ...d, content: localTitle } })}
            />
          </label>
          <div className="adm__section-header-row">
            <label className="adm__label">
              <span><User size={12} /> Rédigé par</span>
              <select value={block.author ?? ''}
                onChange={e => {
                  const name = e.target.value || null
                  const user = users.find(u => (u.display_name || u.steam_id) === name)
                  onUpdate(block, { author: name, data: { ...d, author_avatar: user?.avatar || null } })
                }}>
                <option value="">— Non assigné —</option>
                {users.map(u => (
                  <option key={u.id} value={u.display_name || u.steam_id}>
                    {u.display_name || u.steam_id}
                  </option>
                ))}
              </select>
            </label>
            <label className="adm__label">
              <span>Niveau</span>
              <select value={d.level ?? 2} onChange={e => onUpdate(block, { data: { ...d, level: Number(e.target.value) } })}>
                <option value={2}>H2 — Section principale</option>
                <option value={3}>H3 — Sous-section</option>
              </select>
            </label>
          </div>
        </div>
      )}
    </div>
  )
}

/* Legacy BlockInserter conservé pour compatibilité (non utilisé) */
function BlockInserter({ onAdd }) {
  return null
}

/* Legacy BlockCard conservé pour compatibilité (non utilisé) */
function BlockCard() {
  return null
}

function BlockFields({ block, onUpdate }) {
  const d = block.data ?? {}
  const set = (key, val) => onUpdate(block, { data: { ...d, [key]: val } })

  // Champ texte avec état local pour éviter le reset de curseur à chaque frappe
  function LocalTextarea({ fieldKey, rows = 5, placeholder }) {
    const [local, setLocal] = useState(d[fieldKey] ?? '')
    // Sync si le bloc change depuis l'extérieur (ex: traduction)
    const prevId = useRef(block.id)
    useEffect(() => {
      if (prevId.current !== block.id) {
        setLocal(d[fieldKey] ?? '')
        prevId.current = block.id
      }
    }, [block.id, fieldKey])
    return (
      <textarea
        rows={rows}
        value={local}
        placeholder={placeholder}
        onChange={e => setLocal(e.target.value)}
        onBlur={() => set(fieldKey, local)}
      />
    )
  }

  function LocalInput({ fieldKey, placeholder }) {
    const [local, setLocal] = useState(d[fieldKey] ?? '')
    const prevId = useRef(block.id)
    useEffect(() => {
      if (prevId.current !== block.id) {
        setLocal(d[fieldKey] ?? '')
        prevId.current = block.id
      }
    }, [block.id, fieldKey])
    return (
      <input
        value={local}
        placeholder={placeholder}
        onChange={e => setLocal(e.target.value)}
        onBlur={() => set(fieldKey, local)}
      />
    )
  }

  switch (block.type) {
    case 'text':
      return (
        <label className="adm__label adm__label--full">Contenu (markdown : **gras** *italique*)
          <LocalTextarea fieldKey="content" rows={5} />
        </label>
      )
    case 'image':
      return (<>
        <ImageUploadField label="Image" value={d.src} onChange={v => set('src', v)} />
        <label className="adm__label adm__label--full">Texte alternatif
          <LocalInput fieldKey="alt" />
        </label>
        <label className="adm__label adm__label--full">Légende
          <LocalInput fieldKey="caption" />
        </label>
      </>)
    case 'gallery': {
      const images = Array.isArray(d.images) ? d.images : []
      const updateImg = (i, patch) => set('images', images.map((img, j) => j === i ? { ...img, ...patch } : img))
      const addImg = () => set('images', [...images, { src: '', alt: '', caption: '' }])
      const removeImg = (i) => set('images', images.filter((_, j) => j !== i))
      const moveImg = (i, dir) => {
        const j = i + dir
        if (j < 0 || j >= images.length) return
        const next = [...images]
        ;[next[i], next[j]] = [next[j], next[i]]
        set('images', next)
      }
      return (
        <div className="adm__gallery-editor adm__label--full">
          <p className="adm__gallery-hint">Mosaïque jusqu'à 4 images sur 2 colonnes — au-delà, le grid wrap automatiquement.</p>
          {images.length === 0 && <p className="adm__block-divider-hint">Aucune image — clique sur « Ajouter une image ».</p>}
          {images.map((img, i) => (
            <div key={i} className="adm__gallery-row">
              <div className="adm__gallery-row-thumb">
                {img.src
                  ? <img src={img.src} alt="" />
                  : <span className="adm__gallery-row-placeholder"><Image size={16} /></span>}
              </div>
              <div className="adm__gallery-row-fields">
                <ImageUploadField label={`Image ${i + 1}`} value={img.src} onChange={v => updateImg(i, { src: v })} />
                <div className="adm__gallery-row-meta">
                  <input
                    placeholder="Texte alternatif"
                    value={img.alt ?? ''}
                    onChange={e => updateImg(i, { alt: e.target.value })}
                  />
                  <input
                    placeholder="Légende (optionnel)"
                    value={img.caption ?? ''}
                    onChange={e => updateImg(i, { caption: e.target.value })}
                  />
                </div>
              </div>
              <div className="adm__gallery-row-actions">
                <button type="button" disabled={i === 0} onClick={() => moveImg(i, -1)} title="Monter"><ChevronUp size={14} /></button>
                <button type="button" disabled={i === images.length - 1} onClick={() => moveImg(i, +1)} title="Descendre"><ChevronDown size={14} /></button>
                <button type="button" className="adm__btn--danger-icon" onClick={() => removeImg(i)} title="Supprimer"><Trash2 size={14} /></button>
              </div>
            </div>
          ))}
          <button type="button" className="adm__btn adm__btn--ghost adm__btn--sm" onClick={addImg}>
            <Plus size={14} /> Ajouter une image
          </button>
        </div>
      )
    }
    case 'video':
      return (<>
        <ImageUploadField label="Vidéo (mp4…)" value={d.src} onChange={v => set('src', v)} accept="video/*" />
        <label className="adm__label adm__label--full">Légende
          <LocalInput fieldKey="caption" />
        </label>
      </>)
    case 'youtube':
      return (<>
        <label className="adm__label adm__label--full">ID YouTube (ex: dQw4w9WgXcQ)
          <LocalInput fieldKey="id" placeholder="dQw4w9WgXcQ" />
        </label>
        <label className="adm__label adm__label--full">Légende
          <LocalInput fieldKey="caption" />
        </label>
      </>)
    case 'quote':
      return (<>
        <label className="adm__label adm__label--full">Citation
          <LocalTextarea fieldKey="content" rows={3} />
        </label>
        <label className="adm__label adm__label--full">Auteur
          <LocalInput fieldKey="author" />
        </label>
      </>)
    case 'callout':
      return (<>
        <label className="adm__label">Variante
          <select value={d.variant ?? 'info'} onChange={e => set('variant', e.target.value)}>
            <option value="info">ℹ️ Info</option>
            <option value="success">Succès</option>
            <option value="warning">Avertissement</option>
          </select>
        </label>
        <label className="adm__label adm__label--full">Contenu
          <LocalTextarea fieldKey="content" rows={3} />
        </label>
      </>)
    case 'divider':
      return <p className="adm__block-divider-hint">─ Ligne de séparation</p>
    default:
      return <p style={{color:'#888'}}>Type « {block.type} » — édition non supportée</p>
  }
}

function ImageUploadField({ label, value, onChange, accept = 'image/*' }) {
  const ref = useRef()
  const [uploading, setUploading] = useState(false)

  async function handleFile(e) {
    const file = e.target.files?.[0]
    if (!file) return
    setUploading(true)
    try {
      const { url } = await api.upload(file)
      onChange(`${API_BASE}${url}`)
    } finally {
      setUploading(false)
    }
  }

  return (
    <div className="adm__upload-field">
      <label className="adm__label">{label}
        <div className="adm__upload-row">
          <input className="adm__upload-url" value={value ?? ''} placeholder="URL ou upload →"
            onChange={e => onChange(e.target.value)} />
          <button type="button" className="adm__btn adm__btn--ghost adm__btn--sm"
            onClick={() => ref.current.click()} disabled={uploading}>
            {uploading ? '…' : <><Upload size={14} /> Upload</>}
          </button>
          <input ref={ref} type="file" accept={accept} hidden onChange={handleFile} />
        </div>
      </label>
      {value && accept.startsWith('image') && (
        <img className="adm__upload-preview" src={value} alt="preview" />
      )}
    </div>
  )
}

// ── Sélecteur Mois / Année ───────────────────────────────────────────────
const MONTHS = [
  'Janvier', 'Février', 'Mars', 'Avril', 'Mai', 'Juin',
  'Juillet', 'Août', 'Septembre', 'Octobre', 'Novembre', 'Décembre',
]
const CURRENT_YEAR = new Date().getFullYear()
const YEARS = Array.from({ length: 5 }, (_, i) => CURRENT_YEAR - 2 + i)

function MonthPicker({ value, onChange }) {
  const parts = value ? value.split('-') : ['', '']
  const curY = parts[0] ?? ''
  const curM = parts[1] ?? ''

  return (
    <div className="adm__month-picker">
      <span className="adm__label-text">Mois de publication</span>
      <div className="adm__month-picker-row">
        <select
          value={curM}
          onChange={e => {
            const newM = e.target.value
            const y = curY || String(CURRENT_YEAR)
            onChange(newM ? `${y}-${newM}` : '')
          }}
          className="adm__month-picker-select"
        >
          <option value="">— Mois —</option>
          {MONTHS.map((name, i) => {
            const val = String(i + 1).padStart(2, '0')
            return <option key={val} value={val}>{name}</option>
          })}
        </select>
        <select
          value={curY}
          onChange={e => {
            const newY = e.target.value
            onChange(curM ? `${newY}-${curM}` : `${newY}-01`)
          }}
          className="adm__month-picker-select"
        >
          <option value="">— Année —</option>
          {YEARS.map(yr => (
            <option key={yr} value={yr}>{yr}</option>
          ))}
        </select>
      </div>
    </div>
  )
}

function defaultData(type) {
  const defaults = {
    text:    { content: '' },
    heading: { level: 2, content: '' },
    image:   { src: '', alt: '', caption: '' },
    gallery: { images: [] },
    video:   { src: '', caption: '' },
    youtube: { id: '', caption: '' },
    quote:   { content: '', author: '' },
    callout: { variant: 'info', content: '' },
    divider: {},
  }
  return defaults[type] ?? {}
}
