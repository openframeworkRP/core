import { useEffect, useRef } from 'react'
import './ParallaxHero.css'
import Header from './Header'

import layerFond  from '../assets/layers/layer_fond.webp'
import layerScene from '../assets/layers/layer_scene.webp'
import layerPerso from '../assets/layers/layer_perso.webp'

// Vitesse de parallaxe par layer
const SPEEDS = {
  fond:  0.15,   // ciel — bouge très peu
  scene: 0.35,   // village
  perso: 0.55,   // personnages
}

export default function ParallaxHero() {
  const fondRef  = useRef(null)
  const sceneRef = useRef(null)
  const persoRef = useRef(null)

  useEffect(() => {
    const handleScroll = () => {
      const scrollY = window.scrollY

      if (fondRef.current)
        fondRef.current.style.transform = `translateY(${scrollY * SPEEDS.fond}px)`

      if (sceneRef.current)
        sceneRef.current.style.transform = `translateY(${scrollY * SPEEDS.scene}px)`

      if (persoRef.current)
        persoRef.current.style.transform = `translateY(${scrollY * SPEEDS.perso}px)`
    }

    window.addEventListener('scroll', handleScroll, { passive: true })
    return () => window.removeEventListener('scroll', handleScroll)
  }, [])

  return (
    <section className="parallax-hero">
      {/* Header avec logo centré — par-dessus tous les layers */}
      <Header />

      {/* Layer 1 — Fond (ciel) */}
      <div className="parallax-layer parallax-layer--fond" ref={fondRef}>
        <img src={layerFond} alt="ciel" />
      </div>

      {/* Layer 2 — Scène (village) */}
      <div className="parallax-layer parallax-layer--scene" ref={sceneRef}>
        <img src={layerScene} alt="village" />
      </div>

      {/* Layer 3 — Personnages */}
      <div className="parallax-layer parallax-layer--perso" ref={persoRef}>
        <img src={layerPerso} alt="personnages" />
      </div>
    </section>
  )
}
