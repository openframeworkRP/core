/**
 * Données des règles OpenFramework
 * Deux livres : règles générales du serveur et règles de la police
 */

export const BOOKS = [
  {
    id: 'server',
    title: 'Règlement Général',
    subtitle: 'Les règles du serveur OpenFramework',
    cover_color: '#8B4513',
    cover_accent: '#D4A574',
    spine_color: '#6B3410',
    icon: '📜',
    chapters: [
      {
        id: 'intro',
        title: 'Introduction',
        content: [
          {
            type: 'heading',
            text: 'Bienvenue sur OpenFramework',
          },
          {
            type: 'paragraph',
            text: 'OpenFramework est un serveur DarkRP & Roleplay immersif en français sur S&Box. Ce règlement a pour but d\'assurer une expérience de jeu équitable et agréable pour tous les joueurs.',
          },
          {
            type: 'paragraph',
            text: 'En vous connectant au serveur, vous acceptez automatiquement l\'ensemble de ce règlement. L\'ignorance des règles ne constitue pas une excuse valable.',
          },
          {
            type: 'note',
            text: '⚠️ Le non-respect de ces règles peut entraîner des sanctions allant du simple avertissement jusqu\'au bannissement définitif.',
          },
        ],
      },
      {
        id: 'rp',
        title: 'Règles de Roleplay',
        content: [
          {
            type: 'heading',
            text: 'Le Roleplay (RP)',
          },
          {
            type: 'rule',
            number: '1',
            title: 'Rester en personnage (Stay IC)',
            text: 'Vous devez toujours rester dans votre rôle. Les discussions hors-jeu (OOC) doivent être minimisées et utilisées uniquement en cas d\'absolue nécessité.',
          },
          {
            type: 'rule',
            number: '2',
            title: 'Pas de RDM (Random Deathmatch)',
            text: 'Il est strictement interdit de tuer un joueur sans raison RP valable. Chaque action violente doit être justifiée par une situation en jeu cohérente.',
          },
          {
            type: 'rule',
            number: '3',
            title: 'Pas de NLR (New Life Rule)',
            text: 'Après votre mort, vous oubliez les événements de votre vie précédente. Vous ne pouvez pas retourner à l\'endroit de votre mort pendant 3 minutes.',
          },
          {
            type: 'rule',
            number: '4',
            title: 'Pas de Metagaming',
            text: 'Il est interdit d\'utiliser des informations obtenues hors du jeu (Discord, streams, etc.) pour avantager votre personnage en jeu.',
          },
          {
            type: 'rule',
            number: '5',
            title: 'Pas de Powergaming',
            text: 'Il est interdit d\'effectuer des actions irréalistes ou impossibles pour votre personnage, ou de forcer d\'autres joueurs à subir des actions sans leur donner la possibilité de réagir.',
          },
        ],
      },
      {
        id: 'comportement',
        title: 'Comportement & Respect',
        content: [
          {
            type: 'heading',
            text: 'Règles de Comportement',
          },
          {
            type: 'rule',
            number: '6',
            title: 'Respect mutuel',
            text: 'Le respect entre joueurs est obligatoire. Tout harcèlement, insulte, discrimination ou comportement toxique est strictement interdit et sanctionné immédiatement.',
          },
          {
            type: 'rule',
            number: '7',
            title: 'Pas de Cheats / Exploits',
            text: 'L\'utilisation de cheats, hacks, exploits de jeu ou tout moyen détourné pour obtenir un avantage est interdite et entraîne un bannissement définitif.',
          },
          {
            type: 'rule',
            number: '8',
            title: 'Noms de personnage',
            text: 'Votre nom de personnage doit être réaliste (prénom + nom). Les pseudos offensants, ridicules ou imitant d\'autres joueurs sont interdits.',
          },
          {
            type: 'rule',
            number: '9',
            title: 'Spam et publicité',
            text: 'Tout spam de messages, sons, ou publicité pour d\'autres serveurs est strictement interdit.',
          },
        ],
      },
      {
        id: 'construction',
        title: 'Construction & Bases',
        content: [
          {
            type: 'heading',
            text: 'Règles de Construction',
          },
          {
            type: 'rule',
            number: '10',
            title: 'Bases réalistes',
            text: 'Vos constructions doivent être réalistes et adaptées à votre rôle. Une base doit avoir une entrée accessible et ne pas bloquer les zones publiques.',
          },
          {
            type: 'rule',
            number: '11',
            title: 'Pas de Fading Doors abusifs',
            text: 'Les portes à code (fading doors) sont autorisées, mais leur utilisation abusive (spam ou porte disparaissant trop rapidement) est interdite.',
          },
          {
            type: 'rule',
            number: '12',
            title: 'Limite de props',
            text: 'Chaque joueur dispose d\'un quota de props limité. Ne placez pas de props inutiles ou décoratifs en excès au détriment de la performance du serveur.',
          },
          {
            type: 'note',
            text: '💡 En cas de doute sur une construction, consultez un admin avant de builder.',
          },
        ],
      },
      {
        id: 'sanctions',
        title: 'Sanctions',
        content: [
          {
            type: 'heading',
            text: 'Système de Sanctions',
          },
          {
            type: 'paragraph',
            text: 'Les sanctions sont appliquées progressivement selon la gravité de l\'infraction et l\'historique du joueur.',
          },
          {
            type: 'list',
            items: [
              '⚠️ Avertissement verbal — pour les infractions mineures et les premières erreurs',
              '🔇 Mute / Gag — pour les abus de communication',
              '⏱️ Kick — pour les infractions répétées',
              '🚫 Ban temporaire — 1h à 7 jours selon la gravité',
              '🔒 Ban permanent — pour les infractions graves (cheats, harcèlement grave, etc.)',
            ],
          },
          {
            type: 'note',
            text: '📩 Tout bannissement peut faire l\'objet d\'un appel sur notre Discord. Les décisions des admins sont finales sauf preuve contraire.',
          },
        ],
      },
    ],
  },
  {
    id: 'police',
    title: 'Manuel de la Police',
    subtitle: 'Procédures & règles pour les forces de l\'ordre',
    cover_color: '#1a2d4a',
    cover_accent: '#4a90d9',
    spine_color: '#0f1e33',
    icon: '🚔',
    chapters: [
      {
        id: 'intro',
        title: 'Introduction',
        content: [
          {
            type: 'heading',
            text: 'Manuel des Forces de l\'Ordre',
          },
          {
            type: 'paragraph',
            text: 'Ce manuel est destiné à tous les joueurs incarnant un rôle au sein des forces de l\'ordre sur OpenFramework. En rejoignant la police, vous vous engagez à respecter ces procédures et à faire respecter la loi de manière équitable.',
          },
          {
            type: 'paragraph',
            text: 'La police est au service de la ville et de ses citoyens. Votre rôle est crucial pour maintenir l\'ordre et assurer une expérience RP de qualité pour tous.',
          },
          {
            type: 'note',
            text: '🔵 Le non-respect de ces procédures peut entraîner la perte de votre grade ou votre exclusion des forces de l\'ordre.',
          },
        ],
      },
      {
        id: 'grades',
        title: 'Grades & Hiérarchie',
        content: [
          {
            type: 'heading',
            text: 'Structure Hiérarchique',
          },
          {
            type: 'paragraph',
            text: 'La police est organisée de manière hiérarchique. Chaque grade confère des responsabilités et des autorisations spécifiques.',
          },
          {
            type: 'list',
            items: [
              '👮 Officier — Grade de départ, patrouille basique, arrestations simples',
              '🚔 Sergent — Supervision des patrouilles, autorisé à mener des interrogatoires',
              '⭐ Lieutenant — Gestion des opérations courantes, autorisé pour les mandats',
              '🌟 Capitaine — Direction des opérations majeures, gestion des effectifs',
              '🏅 Commissaire — Chef de la police, décisions stratégiques et disciplinaires',
            ],
          },
          {
            type: 'rule',
            number: 'H1',
            title: 'Respect de la hiérarchie',
            text: 'Les ordres des supérieurs doivent être respectés, sauf s\'ils contreviennent aux lois du serveur ou au règlement général.',
          },
        ],
      },
      {
        id: 'arrestation',
        title: 'Procédures d\'Arrestation',
        content: [
          {
            type: 'heading',
            text: 'Procédures d\'Arrestation',
          },
          {
            type: 'rule',
            number: 'A1',
            title: 'Motif d\'arrestation',
            text: 'Toute arrestation doit être motivée par une infraction constatée ou signalée. Il est interdit d\'arrêter un joueur sans motif valable (arrestation abusive).',
          },
          {
            type: 'rule',
            number: 'A2',
            title: 'Lecture des droits',
            text: 'Avant toute arrestation, vous devez informer le suspect du motif de son arrestation. Utilisez la commande /arrest ou RP vocal selon le contexte.',
          },
          {
            type: 'rule',
            number: 'A3',
            title: 'Durée de détention',
            text: 'La durée maximale de détention sans jugement est de 10 minutes. Au-delà, le suspect doit être libéré ou transféré pour jugement.',
          },
          {
            type: 'rule',
            number: 'A4',
            title: 'Usage de la force',
            text: 'La force ne doit être utilisée qu\'en dernier recours. La force létale est autorisée uniquement si votre vie ou celle d\'un civil est en danger immédiat.',
          },
          {
            type: 'note',
            text: '⚖️ Toute arrestation abusive signalée fera l\'objet d\'une enquête interne.',
          },
        ],
      },
      {
        id: 'fouille',
        title: 'Fouilles & Perquisitions',
        content: [
          {
            type: 'heading',
            text: 'Fouilles & Perquisitions',
          },
          {
            type: 'rule',
            number: 'F1',
            title: 'Fouille de personne',
            text: 'La fouille d\'un individu est autorisée en cas de suspicion fondée (comportement suspect, plainte reçue, flagrant délit). Elle doit être annoncée vocalement au suspect.',
          },
          {
            type: 'rule',
            number: 'F2',
            title: 'Mandat de perquisition',
            text: 'La perquisition d\'une propriété privée nécessite un mandat signé par un Lieutenant minimum. En cas de flagrant délit visible depuis l\'extérieur, le mandat peut être court-circuité.',
          },
          {
            type: 'rule',
            number: 'F3',
            title: 'Saisie de biens',
            text: 'Les biens illégaux saisis doivent être documentés et remis au commissariat. Toute confiscation doit être justifiée et tracée.',
          },
        ],
      },
      {
        id: 'regles',
        title: 'Règles de Conduite',
        content: [
          {
            type: 'heading',
            text: 'Code de Conduite Policier',
          },
          {
            type: 'rule',
            number: 'C1',
            title: 'Neutralité et impartialité',
            text: 'La police doit traiter tous les citoyens de manière égale, sans favoritisme ni discrimination. Il est interdit d\'utiliser votre position pour favoriser vos alliés RP.',
          },
          {
            type: 'rule',
            number: 'C2',
            title: 'Corruption',
            text: 'La corruption est autorisée dans un cadre RP scénarisé et consenti, mais doit rester anecdotique. Aucun officier ne peut être "acheté" contre des items qui affectent l\'équilibre du jeu.',
          },
          {
            type: 'rule',
            number: 'C3',
            title: 'Véhicules de service',
            text: 'Les véhicules de police ne doivent être utilisés que dans l\'exercice de vos fonctions. Il est interdit de les prêter à des civils ou de les utiliser à des fins personnelles.',
          },
          {
            type: 'rule',
            number: 'C4',
            title: 'Communication radio',
            text: 'Utilisez la radio pour coordonner les patrouilles et les interventions. Les communications doivent rester professionnelles et pertinentes.',
          },
          {
            type: 'note',
            text: '🏛️ Rappelez-vous : vous représentez l\'autorité sur le serveur. Votre comportement influence l\'expérience de jeu de tous.',
          },
        ],
      },
    ],
  },
]
