// ============================================================
// App — homepage one-page scrollable, style sbox.game
// ============================================================
// Layout : Header + Hero + Features + Contact + Footer.
// Sections supprimees : Games (multi-jeu retire pour le MVP),
// About (remplace par Features qui explique le gamemode).
//
// Tout est pilote par le BrandingProvider — couleurs/nom/logo/liens
// customisables via /admin/panel/branding.
// ============================================================

import './App.css'
import SimpleHeader from './components/SimpleHeader'
import SimpleHero from './components/SimpleHero'
import FeaturesSection from './components/FeaturesSection'
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

        <section id="features" className="page-section">
          <FeaturesSection />
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
