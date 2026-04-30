import { useEffect, useRef } from 'react'
import './AboutSection.css'
import { useLang } from '../context/LanguageContext'

export default function AboutSection() {
  const { t } = useLang()
  const sectionRef = useRef(null)

  useEffect(() => {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('about--visible')
          }
        })
      },
      { threshold: 0.15 }
    )

    const el = sectionRef.current
    if (el) observer.observe(el)
    return () => { if (el) observer.unobserve(el) }
  }, [])

  return (
    <section className="about" id="about" ref={sectionRef}>
      <div className="about__inner">
        {/* Titre */}
        <div className="about__header">
          <span className="about__eyebrow">S&amp;Box Studio</span>
          <h2 className="about__title">{t('about.title')}</h2>
          <div className="about__divider" />
        </div>

        {/* Valeurs */}
        <div className="about__text about__text--values">
          <p>{t('about.values_p1')}</p>
          <p>{t('about.values_p2')}</p>
        </div>
      </div>
    </section>
  )
}
