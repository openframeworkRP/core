# Site Web — Référence détaillée

> Code source dans `website/`.  
> Architecture : **React + Vite** (frontend) + **Node.js + Express** (backend) + **SQLite** (base de données).  
> Authentification : **Steam OpenID** — les rôles sont gérés par l'admin owner.

---

## Sommaire

- [Architecture](#architecture)
- [Frontend React](#frontend-react)
- [Backend Node.js](#backend-nodejs)
- [Authentification & Rôles](#authentification--rôles)
- [Fonctionnalités publiques](#fonctionnalités-publiques)
- [Panel d'administration](#panel-dadministration)
- [Administration du jeu](#administration-du-jeu)
- [Intégrations externes](#intégrations-externes)
- [Variables d'environnement](#variables-denvironnement)

---

## Architecture

```
website/
├── frontend/               # React + Vite
│   ├── src/
│   │   ├── admin/          # Pages et guards du panel admin
│   │   ├── components/     # Composants réutilisables
│   │   ├── context/        # Contexts React (langue, auth, titre)
│   │   ├── hooks/          # Hooks custom (useDevblog, etc.)
│   │   └── assets/         # Logos, bannières, avatars équipe
│   ├── index.html
│   └── vite.config.js
├── backend/                # Node.js + Express
│   ├── src/
│   │   ├── routes/         # Tous les endpoints REST
│   │   ├── middleware/      # Auth, CORS, upload
│   │   ├── db/             # Init SQLite, schéma
│   │   └── index.js        # Point d'entrée Express
│   └── package.json
└── docker-compose.yml      # Compose standalone
```

Ports par défaut :

| Service | Port |
|---|---|
| Frontend React (Vite) | 4173 |
| Backend Express | 3001 |

---

## Frontend React

### Pages publiques

| Route | Description |
|---|---|
| `/` | Page d'accueil avec hero parallax, présentation du serveur |
| `/devblog` | Liste des devlogs publiés, tri par mois |
| `/devblog/:slug` | Article de devlog complet |
| `/wiki` | Index de la documentation wiki |
| `/wiki/:slug` | Page wiki individuelle |
| `/rules` | Règlement du serveur (versionné) |
| `/jobs` | Offres de recrutement actives |
| `/team` | Profils de l'équipe avec rôle et avatar |

### Composants clés

| Composant | Rôle |
|---|---|
| `ParallaxHero` | Section hero de la page d'accueil avec effet parallax au scroll |
| `AboutSection` | Section "À propos" avec description du serveur et captures |
| `JobsSection` | Grille des offres de recrutement en cours |

### Contexts

| Context | Données exposées |
|---|---|
| `LanguageContext` | Langue active (FR/EN), fonction de changement, traductions |
| `AuthContext` | Utilisateur Steam connecté, rôle, token de session |
| `PostTitleContext` | Titre du post actif (pour les meta og dynamiques) |

### Hooks

- `useDevblog` — fetch + cache des posts publiés depuis le backend

### Multilingue

- Deux langues supportées : **FR** et **EN**
- Traduction automatique des contenus via l'API OpenAI (option activable par post)
- Détection de la langue du navigateur au premier chargement
- Switcher visible dans la nav

---

## Backend Node.js

### Base de données SQLite

Deux bases distinctes :

| Fichier | Contenu |
|---|---|
| `data/devblog.sqlite` | Posts, blocs de contenu, wiki, vidéos, images, membres, offres, règles |
| `data/sessions.db` | Sessions HTTP (express-session) |

### Routes disponibles

#### Contenu éditorial

| Route | Fichier | Description |
|---|---|---|
| `/api/posts` | `posts.js` | CRUD des devlogs : créer, éditer, publier/dépublier, organiser par mois |
| `/api/blocks` | `blocks.js` | Blocs de contenu d'un post (texte, image, code, vidéo) — éditeur modulaire |
| `/api/wiki` | `wiki.js` | Pages wiki : créer, éditer, supprimer, arborescence |
| `/api/docs` | `docs.js` | Documentation générale (guides, FAQ) |
| `/api/videos` | `videos.js` | Références de vidéos (YouTube, hébergées) |
| `/api/images` | `images.js` | Gestion de la médiathèque |

#### Communauté

| Route | Fichier | Description |
|---|---|---|
| `/api/members` | `members.js` | Profils des membres : nom, rôle, avatar, réseaux sociaux |
| `/api/jobs` | `jobs.js` | Offres de recrutement : titre, description, statut (ouvert/fermé) |
| `/api/rules` | `rules.js` | Règlement du serveur avec historique de versions |
| `/api/users` | `users.js` | Gestion des comptes utilisateurs et attribution des rôles |
| `/api/permissions` | `permissions.js` | Définition des permissions par rôle |

#### Administration du jeu

| Route | Fichier | Description |
|---|---|---|
| `/api/gameadmin` | `gameadmin.js` | Logs d'actions admin, envoi de commandes au serveur (ban, whitelist, jail, amende) |
| `/api/control` | `control.js` | Contrôle du serveur dédié (état, restart si configuré) |
| `/api/stats` | `stats.js` | Statistiques joueurs et serveur |

#### Système & Utilitaires

| Route | Fichier | Description |
|---|---|---|
| `/api/auth` | `auth.js` | Steam OpenID OAuth, création de session, logout |
| `/api/setup` | `setup.js` | Configuration initiale (wizard first-run) |
| `/api/branding` | `branding.js` | Nom du serveur, logo, couleurs, identité visuelle |
| `/api/upload` | `upload.js` | Upload de fichiers (images, avatars) — stockage local dans `data/uploads/` |
| `/api/tokens` | `tokens.js` | Gestion des tokens d'API (pour les intégrations externes) |
| `/api/og` | `og.js` | Génération des balises Open Graph dynamiques par page |
| `/api/translate` | `translate.js` | Traduction automatique d'un contenu (FR ↔ EN) via OpenAI |
| `/api/bugs` | `bugs.js` | Remontée et suivi de bugs internes |
| `/api/games` | `games.js` | Gestion des jeux/modes du serveur |

#### Hub interne

| Route | Fichier | Description |
|---|---|---|
| `/api/hub` | `hub.js` | Projets, tâches, idées — espace collaboratif interne pour l'équipe |
| `/api/assets` | `assets.js` | Gestion des assets du serveur (hors images) |
| `/api/nextcloud` | `nextcloud.js` | Intégration Nextcloud pour le stockage de fichiers de l'équipe |

---

## Authentification & Rôles

### Flux Steam OpenID

```
Joueur clique "Connexion Steam"
  → Redirect vers steamcommunity.com/openid/login
  → Steam valide l'identité
  → Callback sur /api/auth/callback avec SteamID64
  → Backend crée/met à jour l'entrée utilisateur en DB
  → Session HTTP créée (express-session + sessions.db)
  → Redirect vers la page demandée
```

### Rôles disponibles

| Rôle | Permissions |
|---|---|
| `owner` | Accès total — gestion des rôles, configuration, tout le panel |
| `admin` | Gestion du contenu, des joueurs, des bans — pas de gestion des rôles |
| `editor` | Création et édition de posts, wiki, membres — pas d'actions sur les joueurs |
| *(aucun)* | Accès public uniquement |

### Guards frontend

`AdminGuard` — composant React qui vérifie le rôle depuis `AuthContext` avant d'afficher les pages admin. Redirige vers `/admin/login` si non authentifié ou rôle insuffisant.

---

## Fonctionnalités publiques

### Devblog

- Articles organisés par mois
- Éditeur de blocs modulaires : texte riche, images, blocs de code, vidéos embarquées
- Publication différée (planifier une date de publication)
- Traduction automatique FR ↔ EN (OpenAI API)
- Balises Open Graph dynamiques pour le partage sur les réseaux sociaux

### Wiki

- Arborescence de pages (parent → enfants)
- Éditeur markdown enrichi
- Recherche plein texte dans les titres et contenus

### Recrutement

- Offres de poste avec titre, description détaillée, compétences requises
- Statut : ouvert / fermé
- Les candidatures sont gérées hors site (Discord/email selon config)

### Équipe

- Fiche par membre : pseudo, rôle dans l'équipe, avatar, lien Discord/Steam optionnel
- Ordre d'affichage configurable

### Règlement

- Texte du règlement versionné
- Chaque version est horodatée
- La version active est affichée publiquement

---

## Panel d'administration

Accessible sur `/admin` après connexion Steam avec un rôle suffisant.

### Gestion du contenu

- **Posts** — créer, éditer, publier, dépublier, réorganiser les blocs
- **Blocs** — éditeur visuel inline (drag & drop des blocs, prévisualisation)
- **Wiki** — CRUD des pages, arborescence
- **Vidéos** — ajouter/retirer des vidéos de la médiathèque
- **Images** — uploader, organiser, supprimer les images
- **Membres** — gérer les fiches de l'équipe
- **Offres** — créer/fermer les offres de recrutement
- **Règles** — éditer et publier une nouvelle version du règlement

### Gestion des utilisateurs

- Lister les utilisateurs connectés avec Steam
- Attribuer / retirer un rôle (owner, admin, editor)
- Voir l'historique de connexion

### Panneau de permissions

`PermissionsPanel` — interface de gestion fine des permissions par rôle. Chaque action de l'admin panel peut être activée ou désactivée par rôle.

### Branding

- Nom du serveur (affiché dans le titre et le footer)
- Logo (upload)
- Couleur principale (utilisée pour l'accent UI)
- Description courte (utilisée dans les balises Open Graph)

---

## Administration du jeu

Le site web peut agir directement sur le serveur de jeu via l'API .NET.

### Logs d'actions admin

- Toutes les actions admin effectuées en jeu sont remontées à l'API (`EventsController`)
- Le panel web les affiche en temps réel avec : date, admin, action, cible, raison

### Actions disponibles depuis le web

| Action | Endpoint API appelé | Description |
|---|---|---|
| **Ban** | `POST /api/admin/ban/` | Bannir un joueur (SteamID + raison + durée) |
| **Unban** | `POST /api/admin/unban/{userId}` | Lever un ban |
| **Whitelist ajouter** | `POST /api/admin/whitelist/` | Autoriser un SteamID à rejoindre |
| **Whitelist retirer** | `POST /api/admin/whitelist/{id}/supp` | Retirer de la whitelist |
| **Jail** | Via queue de commandes | Envoyer un joueur en prison |
| **Amende** | Via queue de commandes | Débiter une somme du compte d'un joueur |
| **Commande custom** | `POST /api/admin/command/queue` | Envoyer n'importe quelle ConCmd au serveur |

### Queue de commandes

Le serveur de jeu poll régulièrement `GET /api/admin/command/pending`. Si des commandes sont en attente, il les exécute et les marque comme traitées.

Ce mécanisme permet d'envoyer des actions au serveur dédié **sans connexion directe** (pas de RCON, pas d'accès SSH requis depuis le web).

---

## Intégrations externes

### s&box Fab (`fab.js`)

- Scraper de la page Fab du gamemode
- Remonte les statistiques d'abonnement / téléchargement dans le hub interne

### Nextcloud (`nextcloud.js`)

- Intégration avec une instance Nextcloud de l'équipe
- Permet de lister et accéder aux fichiers partagés depuis le hub interne

### OpenAI (`translate.js`)

- Traduction automatique des articles de devblog FR ↔ EN
- Optionnel : activé post par post depuis l'éditeur admin

### Steam OpenID (`auth.js`)

- Authentification via le protocole OpenID de Steam
- Récupère le SteamID64 et le profil Steam (pseudo, avatar) après authentification

---

## Variables d'environnement

Renseignées dans le fichier `.env` à la racine du mono-repo :

| Variable | Description |
|---|---|
| `STEAM_API_KEY` | Clé API Steam pour valider les sessions OpenID |
| `SESSION_SECRET` | Secret de signature des sessions HTTP |
| `VITE_API_URL` | URL du backend Express depuis le frontend (ex: `http://localhost:3001`) |
| `VITE_GAME_API_URL` | URL de l'API .NET depuis le frontend (pour les stats) |
| `VITE_MAINTENANCE` | `true` pour afficher la page de maintenance |
| `ALLOWED_STEAM_IDS` | SteamIDs autorisés à se connecter au panel admin (séparés par virgule) |
| `OPENAI_API_KEY` | Clé OpenAI pour la traduction automatique (optionnel) |
| `NEXTCLOUD_URL` | URL de l'instance Nextcloud (optionnel) |
| `NEXTCLOUD_USER` | Utilisateur Nextcloud (optionnel) |
| `NEXTCLOUD_PASSWORD` | Mot de passe Nextcloud (optionnel) |
