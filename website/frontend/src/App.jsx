import './App.css'
import ParallaxHero from './components/ParallaxHero'
import AboutSection from './components/AboutSection'
import GamesSection from './components/GamesSection'
import ContactSection from './components/ContactSection'
import Footer from './components/Footer'
import SEO from './components/SEO'
import layerTransition from './assets/layers/layer_transition_sombre_3.webp'
import layerTransitionBatiment from './assets/layers/transitionbatiment.webp'

function App() {
  return (
    <>
      <SEO
        title="Serveur Roleplay français sur S&Box"
        description="OpenFramework est le premier studio français de jeux RP sur S&Box. Rejoignez OpenFramework, notre serveur DarkRP & Roleplay immersif en français. Communauté active, mises à jour régulières."
        keywords="S&Box, DarkRP, Roleplay, RP, France, français, serveur RP français, OpenFramework, DarkRP français, Roleplay S&Box, S&Box France, OpenFramework, jeux RP français"
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
