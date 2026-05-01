/* =====================================================================
   blogPosts.js — Données des articles DevBlog
   =====================================================================

   MODÈLE : un devlog = une édition mensuelle GLOBALE du studio.
   Chaque bloc peut appartenir à un jeu spécifique ou être global.

   CHAMP game SUR LES BLOCS :
     game: null          → bloc global, toujours visible
     game: 'core'  → visible seulement quand ce jeu est sélectionné
                           (ou quand le filtre est sur "Tout voir")

   TYPES DE BLOCS :
   { type: 'text',    content: 'Texte **gras** *italique*' }
   { type: 'heading', level: 2|3, content: 'Titre' }
   { type: 'image',   src: '/blog/img.png', alt: '...', caption: '...' }
  { type: 'video',   src: '/blog/vid.webp', caption: '...' }
   { type: 'youtube', id: 'VIDEO_ID', caption: '...' }
   { type: 'quote',   content: '...', author: '...' }
   { type: 'divider' }
   { type: 'callout', variant: 'info'|'warning'|'success', content: '...' }
   { type: 'columns', left: [blocs...], right: [blocs...] }
   { type: 'gallery', images: [{ src, alt, caption }] }

   ===================================================================== */

export const GAMES = [
  { slug: 'all',        labelFr: 'Tout voir',  labelEn: 'All',        color: null },
  { slug: 'core', labelFr: 'OpenFramework', labelEn: 'OpenFramework', color: '#e07b39' },
  // Ajoute tes futurs jeux ici, ex :
  // { slug: 'mon_jeu', labelFr: 'Mon Jeu', labelEn: 'My Game', color: '#4a80c8' },
]

export const BLOG_POSTS = [
  /* ──────────────────────────────────────────────────────────────────
     DEVLOG #1 — Mars 2026
  ────────────────────────────────────────────────────────────────── */
  {
    id: 1,
    slug: 'devlog-mars-2026',
    // games : liste des jeux abordés dans ce devlog (pour le filtre de la liste)
    games: ['core'],
    month: '2026-03',
    titleFr: 'Devlog #1 — Mars 2026',
    titleEn: 'Devlog #1 — March 2026',
    excerptFr: 'Premier devlog mensuel du studio. Au programme : les fondations de OpenFramework et les grandes décisions d\'architecture.',
    excerptEn: 'First monthly devlog from the studio. On the agenda: OpenFramework foundations and key architecture decisions.',
    cover: null, // '/blog/mars-2026/cover.jpg'
    author: 'OpenFramework',
    readTime: 5,

    blocksFr: [
      /* ── Intro globale (game: null → toujours visible) ── */
      {
        game: null,
        type: 'callout',
        variant: 'info',
        content: '👋 Bienvenue dans le premier devlog mensuel ! Ce journal regroupe toutes nos avancées. Utilisez les filtres ci-dessus pour ne voir que ce qui vous intéresse.',
      },

      /* ── Section OpenFramework ── */
      {
        game: 'core',
        type: 'heading',
        level: 2,
        content: '🏙️ OpenFramework',
      },
      {
        game: 'core',
        type: 'text',
        content: 'Ce mois-ci nous avons posé les bases techniques de **OpenFramework**. La priorité était de définir une architecture solide avant d\'ajouter du contenu.',
      },
      {
        game: 'core',
        type: 'heading',
        level: 3,
        content: 'Système de jobs',
      },
      {
        game: 'core',
        type: 'text',
        content: 'Nous avons implémenté un système de jobs **entièrement dynamiques**. Les joueurs peuvent choisir leur rôle en temps réel : citoyen, policier, criminel… Chaque job dispose de *permissions* et d\'outils uniques.',
      },
      {
        game: 'core',
        type: 'callout',
        variant: 'success',
        content: '✅ Jobs disponibles au lancement : Citoyen, Policier, Criminel, Médecin, Dealer.',
      },
      {
        game: 'core',
        type: 'heading',
        level: 3,
        content: 'Économie persistante',
      },
      {
        game: 'core',
        type: 'text',
        content: 'Un système d\'économie **persistante** a été intégré. L\'argent est sauvegardé entre les sessions et l\'économie de la ville est cohérente.',
      },

      /* ── Conclusion globale ── */
      {
        game: null,
        type: 'divider',
      },
      {
        game: null,
        type: 'text',
        content: 'C\'est tout pour ce mois-ci. Le mois prochain on attaque la **map** et les bâtiments interactifs. Stay tuned !',
      },
    ],

    blocksEn: [
      {
        game: null,
        type: 'callout',
        variant: 'info',
        content: '👋 Welcome to the first monthly devlog! This journal covers all our progress. Use the filters above to focus on what interests you.',
      },

      {
        game: 'core',
        type: 'heading',
        level: 2,
        content: '🏙️ OpenFramework',
      },
      {
        game: 'core',
        type: 'text',
        content: 'This month we laid the technical groundwork for **OpenFramework**. The priority was a solid architecture before adding content.',
      },
      {
        game: 'core',
        type: 'heading',
        level: 3,
        content: 'Job System',
      },
      {
        game: 'core',
        type: 'text',
        content: 'We implemented a fully **dynamic job system**. Players can choose their role in real time: citizen, police officer, criminal… Each job has unique *permissions* and tools.',
      },
      {
        game: 'core',
        type: 'callout',
        variant: 'success',
        content: '✅ Jobs available at launch: Citizen, Police, Criminal, Doctor, Dealer.',
      },
      {
        game: 'core',
        type: 'heading',
        level: 3,
        content: 'Persistent Economy',
      },
      {
        game: 'core',
        type: 'text',
        content: 'A **persistent economy** system has been integrated. Money is saved between sessions.',
      },

      {
        game: null,
        type: 'divider',
      },
      {
        game: null,
        type: 'text',
        content: 'That\'s it for this month. Next month we\'ll tackle the **map** and interactive buildings. Stay tuned!',
      },
    ],
  },

  /* ──────────────────────────────────────────────────────────────────
     DEVLOG #2 — Février 2026
  ────────────────────────────────────────────────────────────────── */
  {
    id: 2,
    slug: 'devlog-fevrier-2026',
    games: ['core'],
    month: '2026-02',
    titleFr: 'Devlog #2 — Février 2026',
    titleEn: 'Devlog #2 — February 2026',
    excerptFr: 'La map de OpenFramework prend forme : zones résidentielles, quartier d\'affaires et premier PNJ interactif.',
    excerptEn: 'OpenFramework\'s map takes shape: residential areas, business district and the first interactive NPC.',
    cover: null,
    author: 'OpenFramework',
    readTime: 4,

    blocksFr: [
      {
        game: null,
        type: 'callout',
        variant: 'info',
        content: '📅 Devlog de février — utilisez les filtres pour ne voir que le jeu qui vous intéresse.',
      },

      {
        game: 'core',
        type: 'heading',
        level: 2,
        content: '🏙️ OpenFramework',
      },
      {
        game: 'core',
        type: 'text',
        content: 'Février a été un mois très **visuel**. On a commencé à peupler la map avec des bâtiments authentiques et des zones distinctes.',
      },
      {
        game: 'core',
        type: 'heading',
        level: 3,
        content: 'Zones résidentielles',
      },
      {
        game: 'core',
        type: 'text',
        content: 'Les appartements sont maintenant **achetables**. Les joueurs peuvent décorer leur intérieur et inviter d\'autres joueurs.',
      },
      {
        game: 'core',
        type: 'quote',
        content: 'L\'appartement, c\'est le point de départ de toute vie dans la ville.',
        author: 'Lead Designer',
      },
      {
        game: 'core',
        type: 'heading',
        level: 3,
        content: 'PNJ interactif',
      },
      {
        game: 'core',
        type: 'text',
        content: 'Le premier PNJ — le **banquier** — est en place. Il permet de déposer de l\'argent et de contracter des prêts.',
      },
      {
        game: 'core',
        type: 'callout',
        variant: 'warning',
        content: '⚠️ Le système de prêts est encore en beta. Les intérêts seront équilibrés avant le lancement.',
      },

      {
        game: null,
        type: 'divider',
      },
      {
        game: null,
        type: 'text',
        content: 'Rendez-vous en mars pour le prochain devlog !',
      },
    ],

    blocksEn: [
      {
        game: null,
        type: 'callout',
        variant: 'info',
        content: '📅 February devlog — use the filters to focus on the game you\'re interested in.',
      },

      {
        game: 'core',
        type: 'heading',
        level: 2,
        content: '🏙️ OpenFramework',
      },
      {
        game: 'core',
        type: 'text',
        content: 'February was a very **visual** month. We started populating the map with authentic buildings and distinct zones.',
      },
      {
        game: 'core',
        type: 'heading',
        level: 3,
        content: 'Residential Areas',
      },
      {
        game: 'core',
        type: 'text',
        content: 'Apartments are now **purchasable**. Players can decorate their interiors and invite other players.',
      },
      {
        game: 'core',
        type: 'quote',
        content: 'The apartment is the starting point of every life in the city.',
        author: 'Lead Designer',
      },
      {
        game: 'core',
        type: 'heading',
        level: 3,
        content: 'Interactive NPC',
      },
      {
        game: 'core',
        type: 'text',
        content: 'The first NPC — the **banker** — is in place. He allows depositing money and taking out loans.',
      },
      {
        game: 'core',
        type: 'callout',
        variant: 'warning',
        content: '⚠️ The loan system is still in beta. Interest rates will be balanced before official launch.',
      },

      {
        game: null,
        type: 'divider',
      },
      {
        game: null,
        type: 'text',
        content: 'See you in March for the next devlog!',
      },
    ],
  },
]
