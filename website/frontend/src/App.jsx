import './App.css'
import ParallaxHero from './components/ParallaxHero'
import AboutSection from './components/AboutSection'
import GamesSection from './components/GamesSection'
import ContactSection from './components/ContactSection'
import Footer from './components/Footer'
import SEO from './components/SEO'
import { useBranding } from './context/BrandingContext.jsx'
import layerTransition from './assets/layers/layer_transition_sombre_3.webp'
import layerTransitionBatiment from './assets/layers/transitionbatiment.webp'

function App() {
  const { branding } = useBranding()
  const siteName = branding.site_name || 'OpenFramework'
  const description = branding.description || 'Framework de roleplay open source pour s&box'

  return (
    <>
      <SEO
        title={`${siteName} — Serveur Roleplay sur s&box`}
        description={description}
        keywords="s&box, DarkRP, Roleplay, RP, OpenFramework, framework, open source"
        url="/"
        lang="fr"
      />
      
      <ParallaxHero />

      
      <section className="next-section">
        
        <div className="parallax-layer--transition">
          <img src={layerTransition} alt="nuages de transition" />
        </div>

        
        <div className="parallax-layer--transition-batiment">
          <img src={layerTransitionBatiment} alt="transition bâtiment" />
        </div>

        
        <AboutSection />
      </section>

      
      <GamesSection />

      
      <ContactSection />

      
      <Footer />
    </>
  )
}

export default App
