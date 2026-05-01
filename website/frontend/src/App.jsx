// ============================================================
// App — homepage minimaliste, style sbox.game
// ============================================================
// Layout sober : Header + Hero + Footer. Les sections gameplay-RP
// (parallax, cloud layers, AboutSection narrative) ont ete retirees
// pour matcher l'esthetique dev-tool de la plateforme s&box.
//
// Le branding (logo, couleurs, nom) est lu depuis BrandingProvider
// donc tout est customisable via /admin/panel/branding.
// ============================================================

import './App.css'
import SimpleHeader from './components/SimpleHeader'
import SimpleHero from './components/SimpleHero'
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
      <SimpleHero />
      <Footer />
    </>
  )
}

export default App
