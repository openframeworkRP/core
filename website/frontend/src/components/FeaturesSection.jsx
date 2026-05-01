// ============================================================
// FeaturesSection — explication du gamemode et de son contenu
// ============================================================
// Style sbox.game : grid de cards minimalistes avec icone + titre +
// description courte. Chaque card decrit un pilier du framework.
// ============================================================

import {
  Users, Package, Banknote, Briefcase, Shield, Sparkles,
  Server, Code,
} from 'lucide-react'
import './FeaturesSection.css'

const FEATURES = [
  {
    icon: <Server size={20} />,
    title: 'Multijoueur dedie',
    text: 'Architecture sync host-only avec autorite serveur stricte. Jusqu\'a 64 joueurs par instance, RPC.Host pour toutes les mutations sensibles.',
  },
  {
    icon: <Shield size={20} />,
    title: 'Anti-duplication',
    text: 'Chaque transfert d\'item ou d\'argent est atomique cote host. Audit log complet, traceback sur tout drag&drop, swap, drop/pickup.',
  },
  {
    icon: <Package size={20} />,
    title: 'Inventaire complet',
    text: 'Drag&drop, coffres, stack, equipment, vehicules. Persistence SQL + cache memoire, sync RPC vers les clients.',
  },
  {
    icon: <Banknote size={20} />,
    title: 'Economie & banque',
    text: 'Comptes bancaires, ATM, transactions logged, salaires de jobs, transferts joueur-a-joueur. Tout en transactions ACID.',
  },
  {
    icon: <Briefcase size={20} />,
    title: 'Jobs & metiers',
    text: 'Systeme de jobs avec progression, salaires, equipements specifiques. Police, EMS, mecano, chauffeur — moddable.',
  },
  {
    icon: <Users size={20} />,
    title: 'Personnages persistants',
    text: 'Character creation complete (apparence, vetements, morphs faciaux, coiffure). Sauvegarde auto, multi-characters par joueur.',
  },
  {
    icon: <Sparkles size={20} />,
    title: 'Panel admin web',
    text: 'Gestion temps reel : kick/ban, edit inventaire, give money, voir sessions live, audit logs. Permissions par role.',
  },
  {
    icon: <Code size={20} />,
    title: 'Open source MIT',
    text: 'Clone, fork, modifie, redistribue. Pas de royalties, pas de license, pas de catch. Architecture pensee pour etre etendue.',
  },
]

export default function FeaturesSection() {
  return (
    <div className="features">
      <div className="features__inner">
        <header className="features__header">
          <h2>Qu'est-ce qu'OpenFramework ?</h2>
          <p>
            Un framework de roleplay complet pour s&amp;box, pret a deployer.
            Tout le boilerplate gameplay-RP est deja la — clone, configure
            via le wizard, lance ton serveur dedie. Le code reste a toi.
          </p>
        </header>

        <div className="features__grid">
          {FEATURES.map((f, i) => (
            <div key={i} className="features__card">
              <div className="features__icon">{f.icon}</div>
              <h3>{f.title}</h3>
              <p>{f.text}</p>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
