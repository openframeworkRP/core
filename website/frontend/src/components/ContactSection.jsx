import { useEffect, useRef } from 'react'
import { Github, MessageCircle, Gamepad2 } from 'lucide-react'
import './ContactSection.css'
import { useLang } from '../context/LanguageContext'

const LINKS = [
  {
    icon: <MessageCircle size={22} />,
    labelKey: 'contact.discord_label',
    href: 'https://discord.gg/TXwvYJaGCv',
    value: 'discord.gg/TXwvYJaGCv',
    color: '#5865f2',
  },
  {
    icon: <Github size={22} />,
    labelKey: 'contact.github_label',
    href: 'https://github.com/openframework',
    value: 'github.com/openframework',
    color: '#f0ead6',
  },
  {
    icon: <Gamepad2 size={22} />,
    labelKey: 'contact.sbox_label',
    href: 'https://sbox.game/openframework',
    value: 'sbox.game/openframework',
    color: 'var(--brand-primary, #e07b39)',
  },
]

export default function ContactSection() {
  const { t } = useLang()
  const sectionRef = useRef(null)

  useEffect(() => {
    const observer = new IntersectionObserver(
      entries => entries.forEach(e => {
        if (e.isIntersecting) e.target.classList.add('contact--visible')
      }),
      { threshold: 0.1 }
    )
    const el = sectionRef.current
    if (el) observer.observe(el)
    return () => { if (el) observer.unobserve(el) }
  }, [])

  return (
    <section className="contact" id="contact" ref={sectionRef}>
      <div className="contact__inner">
        <div className="contact__header">
          <span className="contact__eyebrow">{t('contact.eyebrow')}</span>
          <h2 className="contact__title">{t('contact.title')}</h2>
          <p className="contact__subtitle">{t('contact.subtitle')}</p>
          <div className="contact__divider" />
        </div>

        <div className="contact__grid">
          {LINKS.map(link => (
            <a
              key={link.href}
              href={link.href}
              target={link.href.startsWith('mailto') ? undefined : '_blank'}
              rel="noopener noreferrer"
              className="contact__card"
              style={{ '--card-color': link.color }}
            >
              <span className="contact__card-icon">{link.icon}</span>
              <div className="contact__card-text">
                <span className="contact__card-label">{t(link.labelKey)}</span>
                <span className="contact__card-value">{link.value}</span>
              </div>
            </a>
          ))}
        </div>
      </div>
    </section>
  )
}
