// ============================================================
// App — homepage one-page scrollable, style sbox.game
// ============================================================
// Layout : Header sticky + Hero + sections (About, Games, Contact)
// + Footer. Tout sur une page, scroll smooth via les ancres
// (#about, #games, #contact) — pareil que sbox.game.
// ============================================================

import './App.css'
import SimpleHeader from './components/SimpleHeader'
import SimpleHero from './components/SimpleHero'
import AboutSection from './components/AboutSection'
import GamesSection from './components/GamesSection'
import ContactSection from './components/ContactSection'
import Footer from './components/Footer'
import SEO from './components/SEO'
import { useBranding } from './context/BrandingContext.jsx'

function App() {
  const { branding } = useBranding()
  const siteName = branding.site_name || 'OpenFramework'
  const description = branding.description || 'Framework open source pour s&box.'

  return (
    <>
      <SEO
        title={`${siteName} — Framework open source pour s&box`}
        description={description}
        keywords="s&box, framework, open source, OpenFramework, gamemode"
        url="/"
        lang="fr"
      />

      <SimpleHeader />

      <main>
        <SimpleHero />

        <section id="about" className="page-section">
          <AboutSection />
        </section>

        <section id="games" className="page-section">
          <GamesSection />
        </section>

        <section id="contact" className="page-section">
          <ContactSection />
        </section>
      </main>

      <Footer />
    </>
  )
}

export default App
