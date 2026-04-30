import { Link } from 'react-router-dom'
import { Users } from 'lucide-react'
import { useLang } from '../context/LanguageContext'
import SEO from './SEO'
import logo from '../assets/logo.png'

import matthieuImg from '../assets/teams/matthieu.webp'
import twinsdImg   from '../assets/teams/twinsd.webp'
import mbkImg      from '../assets/teams/mbk.webp'
import dadaImg     from '../assets/teams/dada.webp'
import alouetteImg from '../assets/teams/alouette.webp'
import marmotteImg from '../assets/teams/mharlotte.webp'
import sampImg     from '../assets/teams/samp.webp'
import houstonImg  from '../assets/teams/houston.webp'
import benjiImg    from '../assets/teams/benji.webp'

import './MembersPage.css'

// ── Données membres (hardcodées) ────────────────────────────────────────
const MEMBERS = [
  // Fondateurs
  { id: 1, grp: 'founders', position: 1, name: 'Matthieu',  role_fr: 'Développeur, 3D',       role_en: 'Developer, 3D',        img: matthieuImg },
  { id: 2, grp: 'founders', position: 2, name: 'TwinsD',    role_fr: 'Mappeur, 3D Artist',  role_en: 'Mapper, 3D Artist',   img: twinsdImg   },
  // Équipe
  { id: 3, grp: 'team',     position: 1, name: 'MBK',       role_fr: 'Développeur',                role_en: 'Developer',                  img: mbkImg      },
  { id: 4, grp: 'team',     position: 2, name: 'Dada',      role_fr: 'Développeur',             role_en: 'Developer',             img: dadaImg     },
  { id: 5, grp: 'team',     position: 3, name: 'Alouette',  role_fr: 'Développeur',                  role_en: 'Developer',           img: alouetteImg },
  { id: 6, grp: 'team',     position: 4, name: 'Mharlotte', role_fr: 'Mappeur',                role_en: 'Mapper',                  img: marmotteImg },
  // En test
  { id: 7, grp: 'team',    position: 1, name: 'Samp',      role_fr: 'Mappeur',             role_en: 'Mapper',             img: sampImg     },
  { id: 8, grp: 'team',    position: 2, name: 'Houston',   role_fr: 'Mappeur',             role_en: 'Mapper',             img: houstonImg  },
  { id: 9, grp: 'trial',    position: 3, name: 'Benji',     role_fr: 'Développeur',         role_en: 'Developer',          img: benjiImg    },
]

/* ── Carte membre ─────────────────────────────────────────────────── */
function MemberCard({ member, founder }) {
  const { lang } = useLang()
  const role = lang === 'fr' ? member.role_fr : member.role_en

  return (
    <div className={`mp__card${founder ? ' mp__card--founder' : ''}`}>
      <div className="mp__avatar-wrap">
        {member.img
          ? <img src={member.img} alt={member.name} className="mp__avatar" />
          : <div className="mp__avatar mp__avatar--placeholder">{member.name[0]}</div>
        }
        {founder && <span className="mp__founder-badge">Fondateur</span>}
      </div>
      <div className="mp__info">
        <h3 className="mp__name">{member.name}</h3>
        <p className="mp__role">{role}</p>
      </div>
    </div>
  )
}

/* ── Page principale ────────────────────────────────────────────── */
export default function MembersPage() {
  const { lang, setLang } = useLang()

  const founders = MEMBERS.filter(m => m.grp === 'founders').sort((a, b) => a.position - b.position)
  const team     = MEMBERS.filter(m => m.grp === 'team').sort((a, b) => a.position - b.position)
  const trial    = MEMBERS.filter(m => m.grp === 'trial').sort((a, b) => a.position - b.position)

  return (
    <div className="mp">
      <SEO
        title={lang === 'fr' ? "L'équipe" : 'The team'}
        description={lang === 'fr'
          ? "Rencontrez l'équipe de Small Box Studio : développeurs, mappeurs et créateurs passionnés qui construisent des expériences multijoueur uniques sur S&Box."
          : "Meet the Small Box Studio team: developers, mappers and passionate creators building unique multiplayer experiences on S&Box."}
        url="/members"
        lang={lang}
        jsonLd={{
          '@context': 'https://schema.org',
          '@type': 'AboutPage',
          name: lang === 'fr' ? "L'équipe Small Box Studio" : 'Small Box Studio Team',
          url: 'https://openframework.com/members',
        }}
      />

      {/* ── Header ── */}
      <header className="mp__header">
        <div className="mp__header-left">
          <Link to="/" className="mp__logo">
            <img src={logo} alt="Small Box Studio" />
          </Link>
          <nav className="mp__nav">
            <Link to="/" className="mp__nav-link">
              {lang === 'fr' ? 'Accueil' : 'Home'}
            </Link>
            <span className="mp__nav-sep">›</span>
            <span className="mp__nav-current">
              {lang === 'fr' ? 'L\'équipe' : 'The team'}
            </span>
          </nav>
        </div>
        <button
          className="mp__lang-btn"
          onClick={() => setLang(lang === 'fr' ? 'en' : 'fr')}
          title={lang === 'fr' ? 'Switch to English' : 'Passer en français'}
        >
          <span className="header__lang-badge">{lang === 'fr' ? 'EN' : 'FR'}</span>
        </button>
      </header>

      {/* ── Hero ── */}
      <section className="mp__hero">
        <div className="mp__hero-bg" />
        <div className="mp__hero-content">
          <span className="mp__hero-eyebrow">
            <Users size={16} />
            {lang === 'fr' ? 'Small Box Studio' : 'Small Box Studio'}
          </span>
          <h1 className="mp__hero-title">
            {lang === 'fr' ? 'L\'équipe' : 'The team'}
          </h1>
          <p className="mp__hero-sub">
            {lang === 'fr'
              ? 'Ceux qui bâtissent les mondes développeurs, mappeurs et créatifs passionnés.'
              : 'The people building the worlds passionate developers, mappers and creatives.'}
          </p>
        </div>
      </section>

      {/* ── Contenu ── */}
      <main className="mp__main">
        <>
            {/* Équipe */}
            {(founders.length > 0 || team.length > 0) && (
              <section className="mp__section">
                <h2 className="mp__section-title">
                  <span className="mp__section-line" />
                  {lang === 'fr' ? 'Équipe' : 'Team'}
                </h2>
                <div className="mp__grid">
                  {founders.map(m => (
                    <MemberCard key={m.id} member={m} founder />
                  ))}
                  {team.map(m => (
                    <MemberCard key={m.id} member={m} />
                  ))}
                </div>
              </section>
            )}

            {/* En test */}
            {trial.length > 0 && (
              <section className="mp__section">
                <h2 className="mp__section-title">
                  <span className="mp__section-line mp__section-line--trial" />
                  {lang === 'fr' ? 'En test' : 'On trial'}
                </h2>
                <p className="mp__section-sub">
                  {lang === 'fr'
                    ? "Ces membres sont en période d'évaluation au sein du studio."
                    : 'These members are currently in their evaluation period with the studio.'}
                </p>
                <div className="mp__grid mp__grid--trial">
                  {trial.map(m => (
                    <MemberCard key={m.id} member={m} />
                  ))}
                </div>
              </section>
            )}
          </>
      </main>
    </div>
  )
}
