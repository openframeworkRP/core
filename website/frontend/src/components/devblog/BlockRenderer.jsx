/* =====================================================================
   BlockRenderer — Rendu des blocs d'articles DevBlog
   ===================================================================== */
import { useState, useEffect, useCallback } from 'react'
import { createPortal } from 'react-dom'
import { ZoomIn, X } from 'lucide-react'
import sboxLogo from '../../assets/favicon_sbox-3.png'
import './BlockRenderer.css'

/* ── Lightbox ──────────────────────────────────────────────────────── */
function Lightbox({ src, alt, caption, onClose }) {
  useEffect(() => {
    function onKey(e) { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', onKey)
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', onKey)
      document.body.style.overflow = ''
    }
  }, [onClose])

  return createPortal(
    <div className="lightbox" onClick={onClose}>
      <button className="lightbox__close" onClick={onClose} aria-label="Fermer"><X size={18} /></button>
      <div className="lightbox__inner" onClick={e => e.stopPropagation()}>
        <img src={src} alt={alt ?? ''} className="lightbox__img" />
        {caption && <p className="lightbox__caption">{caption}</p>}
      </div>
    </div>,
    document.body
  )
}

const DISCORD_RE = /https:\/\/discord\.com\/channels\/[\d/]+/g
const SBOX_RE = /https:\/\/sbox\.game\/[\w\-/]+/g
const BADGE_RE = new RegExp(`${DISCORD_RE.source}|${SBOX_RE.source}`, 'g')

function DiscordBadge({ url }) {
  return (
    <a href={url} target="_blank" rel="noopener noreferrer" className="discord-badge">
      <svg className="discord-badge__icon" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
        <path d="M20.317 4.37a19.791 19.791 0 0 0-4.885-1.515.074.074 0 0 0-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 0 0-5.487 0 12.64 12.64 0 0 0-.617-1.25.077.077 0 0 0-.079-.037A19.736 19.736 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057a.082.082 0 0 0 .031.057 19.9 19.9 0 0 0 5.993 3.03.078.078 0 0 0 .084-.028 14.09 14.09 0 0 0 1.226-1.994.076.076 0 0 0-.041-.106 13.107 13.107 0 0 1-1.872-.892.077.077 0 0 1-.008-.128 10.2 10.2 0 0 0 .372-.292.074.074 0 0 1 .077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 0 1 .078.01c.12.098.246.198.373.292a.077.077 0 0 1-.006.127 12.299 12.299 0 0 1-1.873.892.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028 19.839 19.839 0 0 0 6.002-3.03.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.03zM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.955-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.946 2.418-2.157 2.418z"/>
      </svg>
      Voir sur Discord
    </a>
  )
}

function SboxBadge({ url }) {
  return (
    <a href={url} target="_blank" rel="noopener noreferrer" className="sbox-badge">
      <img className="sbox-badge__icon" src={sboxLogo} alt="" aria-hidden="true" />
      Voir sur s&amp;box
    </a>
  )
}

/** Inline markdown : **gras**, *italique*, liens Discord/sbox.game → badge */
function renderInline(text) {
  if (typeof text !== 'string') return text

  // Découper d'abord sur les URLs Discord et sbox.game
  const segments = []
  let last = 0
  let m
  BADGE_RE.lastIndex = 0
  while ((m = BADGE_RE.exec(text)) !== null) {
    if (m.index > last) segments.push({ type: 'text', value: text.slice(last, m.index) })
    const kind = m[0].includes('discord.com') ? 'discord' : 'sbox'
    segments.push({ type: kind, value: m[0] })
    last = m.index + m[0].length
  }
  if (last < text.length) segments.push({ type: 'text', value: text.slice(last) })

  return segments.flatMap((seg, si) => {
    if (seg.type === 'discord') return [<DiscordBadge key={`d${si}`} url={seg.value} />]
    if (seg.type === 'sbox')    return [<SboxBadge    key={`s${si}`} url={seg.value} />]
    // Appliquer le markdown inline sur les segments texte
    const parts = seg.value.split(/(\*\*[^*]+\*\*|\*[^*]+\*)/g)
    return parts.map((part, i) => {
      if (part.startsWith('**') && part.endsWith('**')) return <strong key={`${si}-${i}`}>{part.slice(2, -2)}</strong>
      if (part.startsWith('*')  && part.endsWith('*'))  return <em key={`${si}-${i}`}>{part.slice(1, -1)}</em>
      return part
    })
  })
}

function BlockText({ block }) {
  // Sépare les paragraphes sur les lignes vides, et les sauts simples en <br>
  const paragraphs = (block.content ?? '').split(/\n{2,}/)
  return (
    <div className="block-text">
      {paragraphs.map((para, pi) => {
        const lines = para.split('\n')
        return (
          <p key={pi}>
            {lines.map((line, li) => (
              <span key={li}>
                {renderInline(line)}
                {li < lines.length - 1 && <br />}
              </span>
            ))}
          </p>
        )
      })}
    </div>
  )
}

function BlockHeading({ block }) {
  const Tag = `h${block.level ?? 2}`
  const cls = `block-heading block-heading--h${block.level ?? 2}`
  return (
    <div className="block-heading-wrap">
      <Tag className={cls}>{block.content}</Tag>
      {block.author && (
        <span className="block-heading__by">
          <span className="block-heading__by-label">By</span>
          {block.author_avatar
            ? <img className="block-heading__by-avatar" src={block.author_avatar} alt={block.author} />
            : <span className="block-heading__by-initials">{block.author.charAt(0).toUpperCase()}</span>
          }
          <span className="block-heading__by-name">{block.author}</span>
        </span>
      )}
    </div>
  )
}

function BlockImage({ block }) {
  const [open, setOpen] = useState(false)
  const close = useCallback(() => setOpen(false), [])
  return (
    <>
      <figure className="block-image block-image--zoomable" onClick={() => setOpen(true)}>
        <img src={block.src} alt={block.alt ?? ''} loading="lazy" />
        <span className="block-image__zoom-hint"><ZoomIn size={16} /></span>
        {block.caption && <figcaption className="block-caption">{block.caption}</figcaption>}
      </figure>
      {open && <Lightbox src={block.src} alt={block.alt} caption={block.caption} onClose={close} />}
    </>
  )
}

function BlockVideo({ block }) {
  return (
    <figure className="block-video">
      <video
        src={block.src}
        controls
        playsInline
        preload="metadata"
      />
      {block.caption && <figcaption className="block-caption">{block.caption}</figcaption>}
    </figure>
  )
}

function BlockYoutube({ block }) {
  return (
    <figure className="block-youtube">
      <div className="block-youtube__wrapper">
        <iframe
          src={`https://www.youtube.com/embed/${block.id}`}
          title={block.caption ?? 'YouTube video'}
          allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
          allowFullScreen
        />
      </div>
      {block.caption && <figcaption className="block-caption">{block.caption}</figcaption>}
    </figure>
  )
}

function BlockQuote({ block }) {
  return (
    <blockquote className="block-quote">
      <p>"{renderInline(block.content)}"</p>
      {block.author && <cite>— {block.author}</cite>}
    </blockquote>
  )
}

function BlockDivider() {
  return <hr className="block-divider" />
}

function BlockCallout({ block }) {
  return (
    <div className={`block-callout block-callout--${block.variant ?? 'info'}`}>
      {renderInline(block.content)}
    </div>
  )
}

function BlockColumns({ block, lang }) {
  return (
    <div className="block-columns">
      <div className="block-columns__col">
        {(block.left ?? []).map((b, i) => <Block key={i} block={b} lang={lang} />)}
      </div>
      <div className="block-columns__col">
        {(block.right ?? []).map((b, i) => <Block key={i} block={b} lang={lang} />)}
      </div>
    </div>
  )
}

function BlockGallery({ block }) {
  const [lightbox, setLightbox] = useState(null)
  const close = useCallback(() => setLightbox(null), [])
  return (
    <>
      <figure className="block-gallery">
        <div className="block-gallery__grid">
          {(block.images ?? []).map((img, i) => (
            <div key={i} className="block-gallery__item block-image--zoomable" onClick={() => setLightbox(img)}>
              <img src={img.src} alt={img.alt ?? ''} loading="lazy" />
              <span className="block-image__zoom-hint"><ZoomIn size={16} /></span>
              {img.caption && <span className="block-gallery__caption">{img.caption}</span>}
            </div>
          ))}
        </div>
      </figure>
      {lightbox && <Lightbox src={lightbox.src} alt={lightbox.alt} caption={lightbox.caption} onClose={close} />}
    </>
  )
}

/* Routeur de blocs */
export function Block({ block, lang }) {
  switch (block.type) {
    case 'text':     return <BlockText    block={block} />
    case 'heading':  return <BlockHeading block={block} />
    case 'image':    return <BlockImage   block={block} />
    case 'video':    return <BlockVideo   block={block} />
    case 'youtube':  return <BlockYoutube block={block} />
    case 'quote':    return <BlockQuote   block={block} />
    case 'divider':  return <BlockDivider />
    case 'callout':  return <BlockCallout block={block} />
    case 'columns':  return <BlockColumns block={block} lang={lang} />
    case 'gallery':  return <BlockGallery block={block} />
    default:         return null
  }
}

export default function BlockRenderer({ blocks, lang }) {
  if (!blocks?.length) return null
  return (
    <div className="block-renderer">
      {blocks.map((block, i) => (
        <Block key={block.id ?? i} block={block} lang={lang} />
      ))}
    </div>
  )
}
