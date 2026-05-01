// ============================================================
// ContactSection — refonte sober style sbox.game
// ============================================================
// Plus de gros titre 'Retrouve-nous / Liens utiles' avec fond cartoon.
// Section minimaliste avec liens lus dynamiquement depuis le branding.
// ============================================================

import { Github, MessageCircle, Gamepad2, ExternalLink } from 'lucide-react'
import { useBranding } from '../context/BrandingContext.jsx'
import './ContactSection.css'

export default function ContactSection() {
  const { branding } = useBranding()

  const LINKS = [
    branding.link_github  && {
      icon: <Github size={20} />,
      label: 'GitHub',
      href: branding.link_github,
      sub:  branding.link_github.replace(/^https?:\/\//, ''),
    },
    branding.link_sbox && {
      icon: <Gamepad2 size={20} />,
      label: 's&box',
      href: branding.link_sbox,
      sub:  branding.link_sbox.replace(/^https?:\/\//, ''),
    },
    branding.link_discord && {
      icon: <MessageCircle size={20} />,
      label: 'Discord',
      href: branding.link_discord,
      sub:  branding.link_discord.replace(/^https?:\/\//, ''),
    },
    branding.link_steam && {
      icon: <ExternalLink size={20} />,
      label: 'Steam',
      href: branding.link_steam,
      sub:  branding.link_steam.replace(/^https?:\/\//, ''),
    },
  ].filter(Boolean)

  if (LINKS.length === 0) {
    return null
  }

  return (
    <div className="contact">
      <div className="contact__inner">
        <header className="contact__header">
          <h2>Liens</h2>
          <p>Code, communauté, gamemode publié — tout ce qu'il faut pour suivre et contribuer.</p>
        </header>

        <div className="contact__grid">
          {LINKS.map(link => (
            <a
              key={link.href}
              href={link.href}
              target="_blank"
              rel="noopener noreferrer"
              className="contact__card"
            >
              <span className="contact__card-icon">{link.icon}</span>
              <div className="contact__card-text">
                <span className="contact__card-label">{link.label}</span>
                <span className="contact__card-value">{link.sub}</span>
              </div>
              <ExternalLink size={14} className="contact__card-arrow" />
            </a>
          ))}
        </div>
      </div>
    </div>
  )
}
