# Gamemode — Référence détaillée

> Code source dans `gamemode/Code/`. Tous les systèmes sont écrits en C# sur le moteur **s&box** (Facepunch).  
> Chaque fonctionnalité est conçue pour fonctionner en **serveur dédié**. Les données persistantes transitent par l'API .NET.

---

## Sommaire

- [Architecture générale](#architecture-générale)
- [Joueur & Personnage](#joueur--personnage)
- [Économie](#économie)
- [Métiers](#métiers)
- [Inventaire & Items](#inventaire--items)
- [Véhicules](#véhicules)
- [PNJ & IA](#pnj--ia)
- [Environnement & Monde](#environnement--monde)
- [Police & Justice](#police--justice)
- [Communication](#communication)
- [Administration](#administration)
- [Anti-triche](#anti-triche)

---

## Architecture générale

```
gamemode/
├── Code/
│   ├── Api/                    # Client HTTP vers l'API backend
│   ├── Database/               # DTOs et mappers (tables locales)
│   ├── Discord/                # Bridge Discord (logs serveur)
│   ├── GameLoop/               # Boucle de jeu, règles, spawning, timer
│   ├── Systems/
│   │   ├── AntiCheat/
│   │   ├── Armury_System/
│   │   ├── AtmSystem/
│   │   ├── BankEconomy/
│   │   ├── Chair_System/
│   │   ├── ChatSystem/
│   │   ├── Character_Manager/
│   │   ├── Clothings/
│   │   ├── Command/
│   │   ├── Cooking/
│   │   ├── CraftingSystem/
│   │   ├── DayNightCycle_System/
│   │   ├── DispatchSystem/
│   │   ├── Drug_System/
│   │   ├── GpsSystem/
│   │   ├── Grab_System/
│   │   ├── HandcuffComponent/
│   │   ├── JobSystem/
│   │   ├── MinimapSystem/
│   │   ├── MoneySystem/
│   │   ├── Npc/
│   │   ├── Pawn/
│   │   ├── PhoneSystem/
│   │   ├── Radio_System/
│   │   ├── RadialMenu/
│   │   ├── Shop_System/
│   │   ├── TaskSystem/
│   │   ├── Tools/
│   │   ├── UI_Player_Manager/
│   │   ├── VehicleSystem/
│   │   ├── Vehicles/
│   │   ├── WeaponSystem/
│   │   ├── WeatherSystem/
│   │   └── Window_System/
│   └── UI/                     # Interfaces Razor (HUD, menus, overlays)
└── Assets/
    ├── items/                  # Définitions d'items (.item)
    ├── models/                 # Modèles 3D et matériaux
    ├── prefabs/                # Prefabs de GameObjects
    ├── scenes/                 # Scènes (maps)
    └── shaders/                # Shaders custom
```

Le flux de données principal :

```
Joueur (Client) ──→ RPC Host ──→ Gamemode (Serveur) ──→ API .NET ──→ SQL Server
                                        │
                              [Sync] ←──┘  (propriétés [Sync(SyncFlags.FromHost)])
```

---

## Joueur & Personnage

### Pawn (`Systems/Pawn/`)

Le `Pawn` est le personnage jouable. Il est composé de plusieurs `Component` indépendants :

| Composant | Rôle |
|---|---|
| `CharacterController` | Déplacement, saut, collision, gravité |
| `AnimationHelper` | Sélection et blend des animations selon l'état |
| `EmotesAnimations` | Émotes jouables en jeu via commande |
| `HealthComponent` | Points de vie, mort, respawn |
| `ArmorComponent` | Points d'armure, absorption des dégâts |
| `PlayerFootsteps` | Sons de pas selon le sol |
| `CameraController` | Caméra FPS/TPS, zoom, transitions |
| `UI_Player_Manager` | HUD (santé, argent, métier, minimap) |

#### Synchronisation réseau

- Les propriétés d'état (santé, argent, métier) utilisent `[Sync(SyncFlags.FromHost)]`
- Les mutations (prendre des dégâts, dépenser de l'argent) passent par `[Rpc.Host]`
- Le client n'a jamais d'autorité sur son propre état

#### Personnage (`Systems/Character_Manager/`)

- `CharacterManager` — charge le personnage depuis l'API au login, maintient la ref active
- `CharacterCreationState` — state machine de création (nom, genre, morphs, tenue initiale)
- Données persistées côté API : nom, genre, morphs faciaux, tenue, métier, position

### Santé & Mort

- `HealthComponent` gère HP, seuil de mort et appel au respawn
- À la mort : le joueur passe en mode spectateur (`SpectateSystem`) et peut être réanimé par un médecin avec le défibrillateur
- Respawn classique si aucune réanimation dans le délai configuré

### Apparence

- Corps géré via morphs (données `float[]` stockées en API)
- Tenue composée de slots (chapeau, haut, bas, chaussures, etc.)
- Synchronisation FirstPerson / ThirdPerson : le `FirstPersonBody` (shadow) suit le personnage avec le même skin pour éviter les décalages visuels en dédi

---

## Économie

### Argent liquide (`Systems/MoneySystem/`)

- Chaque joueur a un solde liquide (`Cash`) local sur le personnage
- Les transactions liquide-to-liquide se font directement entre joueurs (RPC Host)
- Non persisté séparément : inclus dans les données du personnage

### Banque (`Systems/BankEconomy/`)

`BankEconomyComponent` modélise un compte bancaire :

| Concept | Description |
|---|---|
| **Fonds de roulement** | Solde utilisable librement |
| **Réserve intouchable** | Plancher minimum configuré par le maire |
| **Taux d'intérêt** | Appliqué automatiquement selon la décision du maire |
| **Co-titulaires** | Plusieurs personnages peuvent accéder au même compte |

Toutes les opérations bancaires (virement, retrait, dépôt) passent par l'API — jamais traitées localement.

### ATM (`Systems/AtmSystem/`)

- `AtmComponent` placé sur la map (prefab)
- Ouvre un panneau de retrait/dépôt lié au compte bancaire du personnage
- L'opération est validée côté API avant d'être appliquée

### Boutiques (`Systems/Shop_System/`)

- `ShopCatalogueResource` — ScriptableObject définissant la liste d'items, prix, stock
- `ShopSign` — Component 3D placé sur la map, ouvre le panneau boutique au `Use`
- `ShopCatalogueManager` — singleton gérant les instances actives

### Armurerie (`Systems/Armury_System/`)

- Catalogue séparé `ArmuryCatalogueResource` (armes de poing, armes longues, munitions)
- L'accès à certaines armes est conditionné au métier Police ou Armurier
- Vérification du métier côté host avant tout transfert d'arme

### Vêtements (`Systems/Clothings/`)

- `ClothShopManager` — gestion de la boutique de vêtements
- `ShopDressingRoom` — cabine d'essayage : prévisualise la tenue en temps réel avant achat
- `ClothExpo` — mannequin 3D exposant des tenues
- `ClothingsCatalogueResource` — définition du catalogue (slots, modèles, prix)

---

## Métiers

### Système de base (`Systems/JobSystem/`)

`JobSystem` est le singleton central. Il maintient la liste des métiers disponibles et l'attribution par personnage.

`JobComponent` est attaché à chaque `Pawn`. Il expose :
- Le métier actuel (`CurrentJob`)
- Les permissions associées (accès armurerie, MDT, dispatch, etc.)
- Le salaire et les conditions d'obtention

### Métiers disponibles

#### Citizen
- Accès aux activités civiles, boutiques, ATM
- Peut voter pour changer de métier via `JobVoteSystem`

#### Police

> Documentation détaillée : [docs/jobs/POLICE.md](jobs/POLICE.md)

- Accès au MDT via l'**App Police** (téléphone ou terminal fixe `policecomputer`)
- `HandcuffComponent` — menotter / libérer un joueur
- Fouiller un joueur (inventaire + argent liquide)
- Système d'**amendes** : émission, persistance API, paiement par le joueur
- Accès à l'armurerie de service (`PoliceLocker`)
- Reçoit les appels dispatch (`DispatchSystem`)

#### Médecin
- `Defibrillator` — item permettant de réanimer un joueur mort avant le délai de respawn
- Reçoit les appels urgence du `DispatchSystem`
- Peut soigner (restaurer des HP) via l'item soin

#### Maire
- `JobTask` de type `mayor` — tâches administratives assignées : fixer le taux d'intérêt, la réserve bancaire
- Peut verrouiller le serveur ou activer la whitelist via les ConVars

#### Armurier
- Gère le stock de son armurerie via le `ShopCatalogueResource`
- Seul à pouvoir vendre légalement des armes aux civils

#### Cuisinier
- Accès aux stations de cuisine (grill, friteuse, planche, soda)
- Fabrique des plats vendables à d'autres joueurs

#### Maintenance
- `JobTask` de type `maintenance` — réparations programmées sur la map
- Accès aux outils (`ToolsGunComponent`)

#### Éboueur
- `JobTask` de type itinéraires — points de collecte sur la map à valider dans l'ordre

#### Intérimaire
- Missions courtes multi-secteurs
- `JobTask` varié selon la tâche assignée

### Vote de métier (`JobVoteSystem`)

- Un joueur peut soumettre une demande de changement de métier
- Les autres joueurs votent (système de majorité ou vote unique selon config)
- Le changement est appliqué via l'API si le vote aboutit

### Tâches métier (`Systems/TaskSystem/`)

- `TaskDefinition` — définit une tâche (type, zone, durée, récompense)
- `TaskTrigger` — zone physique sur la map qui valide ou déclenche une tâche
- `TaskManager` — assigne et suit les tâches actives par joueur

---

## Inventaire & Items

### Grille d'inventaire (`Systems/Inventory/`)

- Inventaire en grille (N×M slots configurables)
- Items occupent 1 ou plusieurs cases selon leur taille
- Opérations supportées :
  - **Drag & drop** entre slots
  - **Split** d'un stack
  - **Inspection** d'un item (description, stats)
  - **Drop** sur le sol → crée un `DroppedItem` GameObject dans la scène
  - **Pickup** d'un item au sol → retrait du GameObject, ajout en inventaire
- Toutes les mutations passent par `[Rpc.Host]` pour éviter la duplication

### Items

Les items sont définis via des fichiers `.item` (asset custom s&box) dans `Assets/items/`.

Catégories d'items :

| Catégorie | Exemples |
|---|---|
| Armes | Pistolet, fusil, couteau |
| Munitions | Chargeur 9mm, 5.56 |
| Nourriture | Burger, frites, soda |
| Ingrédients | Steak cru, pain, légumes |
| Vêtements | Chaque pièce de tenue est un item |
| Outils | Clé à molette, défibrillateur, menottes |
| Divers | Téléphone, radio, drogue |

### Crafting (`Systems/CraftingSystem/`)

- `CraftingTable` — prefab placé sur la map (workbench)
- `CraftingSystem` — singleton gérant les recettes
- Recettes configurées en code ou via ressource : liste d'ingrédients → résultat
- La validation de la recette et le transfert d'items sont atomiques côté host

### Cuisine (`Systems/Cooking/`)

Système de cuisine avancé avec plusieurs stations :

| Station | Rôle |
|---|---|
| `CuttingStation` | Découpe les ingrédients (couper un steak, émincer des légumes) |
| `GrillStation` | Cuisson à la plancha avec timer |
| `FryerStation` | Friture avec timer et état (cru / cuit / brûlé) |
| `SodaFountain` | Distributeur de boissons |
| `AssemblyPlank` | Assemblage final des plats (ex: burger = pain + steak + salade) |

L'état de cuisson (cru/cuit/brûlé) est synchronisé côté serveur.

### Armes (`Systems/WeaponSystem/`)

- `Equipment` — component attaché au `Pawn`, gère l'arme équipée
- `DroppedWeapon` — GameObject créé au sol quand une arme est lâchée
- `RecoilPattern` — ScriptableObject définissant le pattern de recul par arme
- `Defibrillator` — item médical avec logique de réanimation dédiée

### Radio (`Systems/Radio_System/`)

- `RadioComponent` attaché au Pawn quand l'item radio est équipé
- Communication par canal de fréquence (ex: canal 1 = général, canal 2 = police)
- La transmission est serveur-authoritative : seuls les joueurs sur la même fréquence reçoivent

### Téléphone (`Systems/PhoneSystem/`)

- `PhoneItemSystem` — item portable ouvrant une interface UI dédiée
- Interface avec contacts, appels, SMS (fonctionnalités extensibles)

---

## Véhicules

### Physique avancée (`Systems/Vehicles/`)

Le système de véhicule simule les composants mécaniques :

| Composant | Rôle |
|---|---|
| `Axle` | Essieu avant/arrière, calcul de la force de traction |
| `Clutch` | Embrayage, transition entre rapports |
| `Differential` | Différentiel, répartition du couple entre les roues |
| `DrivetrainType` | Enum : FWD / RWD / AWD |
| `WheelAssembly` | Assembly physique de chaque roue (suspension, friction, collision) |

### Contrôleur principal (`Systems/VehicleSystem/`)

- `VehicleController` — entry point du véhicule, gère entrée/sortie joueur, transmission des inputs
- `Vehicle` — état du véhicule (vitesse, rapport, carburant éventuel)
- `VehicleInformation` — ScriptableObject de config (masse, puissance, nb places)
- `DoorVehicleComponent` — portes animées, ouverture/fermeture 3D synchronisée
- `SeatComponent` — gestion des passagers (N sièges configurables), animations par siège

### Réseau

- Le conducteur est authoritative sur les inputs
- La position/rotation du véhicule est synchronisée via `[Sync]`
- Les passagers reçoivent la position par la sync du véhicule, pas en calculant localement

---

## PNJ & IA

### Gestion des PNJ (`Systems/Npc/`)

#### NpcManager

Singleton gérant le cycle de vie :
- Spawn de PNJ à des points définis sur la map
- Pool de PNJ : limite le nombre d'instances actives
- Respawn automatique si un PNJ est détruit

#### NpcPawnController

Contrôleur de déplacement et d'animation des PNJ, analogue au `CharacterController` joueur.

#### Comportements modulaires

Chaque PNJ a un `BaseNpcBehavior` extensible. Les comportements disponibles :

| Comportement | Description |
|---|---|
| `PedestrianBehavior` | Se déplace entre waypoints, s'arrête aux feux, évite les obstacles |
| `RoamBehavior` | Déambule dans un rayon autour d'un point d'ancrage |
| `CombatBehavior` | Cible un ennemi, se positionne, tire |

#### Arbre de nœuds (Behavior Tree)

Les comportements complexes sont construits via des nœuds composables :

| Nœud | Action |
|---|---|
| `SelectTargetNode` | Choisit la cible la plus proche dans un rayon |
| `AimAtTargetNode` | Oriente le PNJ vers la cible |
| `MoveToNode` | Déplace le PNJ vers un point ou une cible |
| `ShootTargetNode` | Tire sur la cible si en ligne de mire |
| `PlayRandomEmoteNode` | Joue une animation aléatoire (idle varié) |

#### Traffic (`Systems/Npc/Traffic/`)

- Véhicules PNJ spawné sur la map et suivant des routes définies
- Respectent les intersections et s'arrêtent derrière les joueurs/véhicules
- Comportement simpliste (pas de pathfinding complet) pour rester léger serveur

#### Dialogues (`NpcSystem/`)

- Chaque PNJ peut avoir un `UsePanel` configurable
- Déclenché au `Use` (touche E) par le joueur
- Contenu : texte, liste d'options, déclenchement d'actions (ouvrir une boutique, lancer une quête)

---

## Environnement & Monde

### Cycle jour/nuit (`Systems/DayNightCycle_System/`)

- Horloge serveur avec heure in-game configurable (ratio temps réel / temps jeu)
- Synchronisée sur tous les clients via `[Sync(SyncFlags.FromHost)]`
- Pilote le `SkyAtmosphere` et l'éclairage directionnel de la scène
- Événements exposés : `OnSunrise`, `OnSunset` pour déclencher des actions

### Météo (`Systems/WeatherSystem/`)

- Transitions progressives entre états : ensoleillé, nuageux, pluie, brouillard
- Synchronisé serveur → clients
- Affecte visuellement la scène (particules, opacité du ciel)

### Minimap (`Systems/MinimapSystem/`)

- Rendu en temps réel depuis une caméra orthographique zénithale
- Blips dynamiques : joueurs, PNJ marqués, points d'intérêt
- Blips métier-spécifiques (ex: blip dispatch pour la police)

### GPS (`Systems/GpsSystem/`)

- Navigation point-à-point sur la map
- Affiche un itinéraire sur la minimap et un marqueur de destination

### Audio zonalisé (`Systems/Blocker_Audio_System/`)

- `AudioRoomBlocker` — volume de collision définissant une pièce
- Les sons générés hors de la pièce sont atténués pour les joueurs à l'intérieur
- Simule l'isolation phonique (ex: dans un bâtiment, on entend moins la rue)

### Meubles interactifs

| Système | Composant | Comportement |
|---|---|---|
| Chaises | `ChairComponent` | S'asseoir / se lever, support N places (`Seats` list) |
| Fenêtres | `WindowComponent` | Ouvrir / fermer avec animation |
| Lampes | `LampComponent` | Allumer / éteindre |
| Placement libre | `PropPlacer` | Poser/déplacer un objet dans la scène |
| Visuel | `FurnitureVisual` | Composant de rendu pour les meubles placés |

---

## Police & Justice

> Documentation complète du métier Police : [docs/jobs/POLICE.md](jobs/POLICE.md)

### MDT — App Police (`World/Devices/Apps/PoliceApp/`)

Application dédiée accessible uniquement au métier Police, disponible sur :
- **Téléphone** (filtrée par `JobAccess`)
- **Terminal fixe** `policecomputer.prefab` — prop monde non-déplaçable, long press E pour ouvrir

Pages disponibles : Accueil, Finance, **Amendes** (recherche par nom/prénom RP avec tableau d'amendes par personnage).

### Menottes (`Systems/HandcuffComponent/`)

- `HandcuffComponent` attaché au Pawn cible
- Empêche le joueur de courir, sauter, utiliser son inventaire
- Déclenché par l'item menottes, validé côté host
- Relâché par la police ou auto-libération après délai si non relevé

### Dispatch (`Systems/DispatchSystem/`)

- `DispatchSystem` — singleton gérant la file d'appels
- `DispatchCall` — objet représentant un appel (type, position, priorité, heure)
- `DispatchType` — enum : appel urgent, assistance, signalement, infraction

Les appels sont visibles sur le HUD des joueurs Police et Médecin, avec localisation sur la minimap.

---

## Communication

### Chat (`Systems/ChatSystem/`)

- Canal local : portée de voix (rayon configuré)
- Canal global : visible par tous les joueurs connectés
- Canal métier : visible uniquement par les membres du même métier
- Les messages sont loggés côté API (EventsController)

### Radio (`Systems/Radio_System/`)

- Item équipable, communication par fréquence numérique
- Utilisé principalement par la Police pour les communications internes
- La fréquence est configurable en jeu

### Menu radial (`Systems/RadialMenu/`)

- Roue d'actions contextuelle (touche configurée, ex: `Alt`)
- Les options changent selon le contexte : près d'un PNJ, d'un véhicule, d'un meuble
- Actions rapides : s'asseoir, ramasser, interagir, lever les mains

---

## Administration

### Commandes (`Systems/Command/`)

- Système de commandes console joueur et admin
- Commandes admin : kick, ban, goto, bring, setjob, setmoney, etc.
- Permission vérifiée côté host (liste `AdminSteamIds`)

### Queue de commandes web

Le site web peut envoyer des commandes au serveur dédié via l'API :
- `POST /api/admin/command/queue` — le web dépose une commande (ban, whitelist, jail, amende)
- `GET /api/admin/command/pending` — le gamemode poll cette route régulièrement et exécute les commandes en attente

### Whitelist & Verrouillage

- `whitelist_mod` (ConVar bool) — seuls les SteamIDs whitelistés peuvent rejoindre
- `server_locked` (ConVar bool) — seuls les admins peuvent rejoindre
- La liste est gérée en API (`/api/admin/whitelist/`)

### Bridge Discord (`Discord/DiscordBridge`)

- Envoie des messages sur un webhook Discord configuré
- Événements loggés : connexion/déconnexion joueur, actions admin, mort, etc.
- Activé via ConVar `core-discord_debug`

---

## Anti-triche

`Systems/AntiCheat/` — validation serveur-side des actions sensibles :

- Toute modification d'inventaire est revalidée côté host avant application
- Les mouvements hors norme (vitesse, téléportation) sont détectés et loggés
- Les RPC suspects (appelé depuis un client sans permission) sont rejetés silencieusement
- Les logs anti-triche sont remontés à l'API via `EventsController`

---

## ConVars & ConCmds

Voir [docs/CONFIG.md](CONFIG.md) pour la référence complète.

| ConVar | Default | Description |
|---|---|---|
| `core-api_server_secret` | `""` | Secret partagé API ↔ gamemode |
| `whitelist_mod` | `false` | Active la whitelist |
| `server_locked` | `false` | Verrouille le serveur aux admins uniquement |
| `core-discord_debug` | `false` | Logs Discord |
| `core_debug_npc` | `false` | Logs NPC |
| `core_debug_appearance` | `false` | Logs apparence |
| `core_debug_morphs` | `false` | Logs morphs faciaux |

| ConCmd | Description |
|---|---|
| `core-connect_server` | Connecte le serveur à l'API |
| `core-save_positions` | Force la sauvegarde des positions |
| `core_dump_morph_names` | Dump la liste des morphs disponibles |
