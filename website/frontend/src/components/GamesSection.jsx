import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import './GamesSection.css'
import { useLang } from '../context/LanguageContext'
import openFrameworkPreview from '../assets/game/small-life/preview.webp'
import openFrameworkAnim from '../assets/game/small-life/anim.webm'

function GameCard({ titleKey, genreKey, descKey, tag1, tag2, tag3, ctaKey, preview, video, gamePage }) {
  const { t } = useLang()
  const [hovered, setHovered] = useState(false)
  const videoRef = useRef(null)

  function handleMouseEnter() {
    setHovered(true)
    videoRef.current?.play()
  }

  function handleMouseLeave() {
    setHovered(false)
    if (videoRef.current) {
      videoRef.current.pause()
      videoRef.current.currentTime = 0
    }
  }

  return (
    <Link
      to={gamePage}
      className={`game-card${hovered ? ' game-card--hovered' : ''}`}
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
    >
      {/* Média : image par défaut, vidéo au hover */}
      <div className="game-card__media">
        <img
          src={preview}
          alt={t(titleKey)}
          className={`game-card__preview${hovered ? ' game-card__preview--hidden' : ''}`}
        />
        <video
          ref={videoRef}
          src={video}
          muted
          playsInline
          loop
          preload="none"
          className={`game-card__video${hovered ? ' game-card__video--visible' : ''}`}
        />

        {/* Badge genre */}
        <span className="game-card__genre-badge">{t(genreKey)}</span>
      </div>

      {/* Contenu */}
      <div className="game-card__body">
        <h3 className="game-card__title">{t(titleKey)}</h3>

        <div className="game-card__tags">
          <span className="game-card__tag">{t(tag1)}</span>
          <span className="game-card__tag">{t(tag2)}</span>
        </div>

        <p className="game-card__desc">{t(descKey)}</p>

        <span className="game-card__cta">
          {t(ctaKey)}
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" width="16" height="16">
            <path fillRule="evenodd" d="M3 10a.75.75 0 0 1 .75-.75h10.638L10.23 5.29a.75.75 0 1 1 1.04-1.08l5.5 5.25a.75.75 0 0 1 0 1.08l-5.5 5.25a.75.75 0 1 1-1.04-1.08l4.158-3.96H3.75A.75.75 0 0 1 3 10Z" clipRule="evenodd" />
          </svg>
        </span>
      </div>
    </Link>
  )
}

export default function GamesSection() {
  const { t } = useLang()
  const sectionRef = useRef(null)

  useEffect(() => {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) entry.target.classList.add('games--visible')
        })
      },
      { threshold: 0.1 }
    )
    const el = sectionRef.current
    if (el) observer.observe(el)
    return () => { if (el) observer.unobserve(el) }
  }, [])

  return (
    <section className="games" id="games" ref={sectionRef}>
      {/* Séparateur ondulé entre About et Games */}
      <div className="games__wave" aria-hidden="true">
        <svg viewBox="0 0 1440 80" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
          <path d="M0,40 C360,80 1080,0 1440,40 L1440,0 L0,0 Z" fill="#2b2b2b" />
        </svg>
      </div>

      <div className="games__inner">
        {/* Header */}
        <div className="games__header">
          <span className="games__eyebrow">{t('games.subtitle')}</span>
          <h2 className="games__title">{t('games.title')}</h2>
          <div className="games__divider" />
        </div>

        {/* Grille de cartes */}
        <div className="games__grid">
          <GameCard
            titleKey="games.core.title"
            genreKey="games.core.genre"
            descKey="games.core.desc"
            tag1="games.core.tag_rp"
            tag2="games.core.tag_multi"
            ctaKey="games.core.cta"
            preview={openFrameworkPreview}
            video={openFrameworkAnim}
            gamePage="/game/small-life"
          />
        </div>
      </div>
    </section>
  )
}
