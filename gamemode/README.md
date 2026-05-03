# OpenFramework — Gamemode

Le gamemode s&box du framework. Développé en **C# sur le moteur s&box** (Facepunch), il tourne sur un serveur dédié et synchronise tout son état via l'API centrale.

---

## Prérequis

- **s&box** installé avec accès développeur (Steam)
- L'**API backend** qui tourne (cf. `docker compose up -d` depuis la racine du repo)
- Pour publier : un package enregistré sur [asset.sandbox.game](https://asset.sandbox.game)

---

## Démarrage rapide

### 1. Ouvrir dans l'éditeur

Double-clique sur `gamemode/core.sbproj` — s&box charge le projet.

### 2. Configurer l'identité si tu forkes

> ⚠️ Indispensable si tu as forké ce repo.

Le fichier `core.sbproj` pointe sur le package `openframework.core` (compte OpenFramework). Sur ton fork tu dois le remplacer par ton propre org/ident, sinon s&box bloque le hotload.

**Editor → OpenFramework → Configurer ce fork…** → entre ton Org et ton Ident → Appliquer.

### 3. Connecter au backend

Dans la scène `Assets/scenes/maps/UnionCity/unioncity.scene`, le composant `ApiComponent` a un champ `BaseUrl`. S'il pointe sur `http://localhost:8443/api`, c'est bon pour un dev local. Sinon change-le ou utilise la ConVar dans la console serveur :

```
core-api_base_url http://<ip-du-backend>:8443/api
```

### 4. Lancer le serveur dédié

```bash
sbox.exe -dedicated +gamemode openframework.core +map unioncity
```

Dans la console serveur, après démarrage :
```
core-api_server_secret <SERVER_SECRET du .env>
core-connect_server
```

Succès → `Server authenticated successfully` dans les logs de `core.api`.

---

## Structure du code

```
Code/
├── Api/                    # Appels HTTP vers le backend (.NET API)
│   └── ApiComponent.cs     # Composant principal — token JWT, requêtes auth/perso/inventaire
│
├── Systems/                # Systèmes de jeu
│   ├── Pawn/               # Joueur : Client.cs, PlayerAppearance.cs, mouvements
│   ├── Character Manager/  # Création et sélection de personnage
│   ├── Economy_System/     # Argent liquide, transactions
│   ├── Inventory/          # Grille d'inventaire, drag & drop, items
│   ├── Jobs/               # Définitions des métiers, permissions, changement de job
│   ├── Vehicles/           # Physique, sièges, portes
│   ├── Weapons/            # Équipement, drop, recul
│   ├── Shop_System/        # Boutiques génériques (ShopCatalogueResource)
│   ├── Clothings/          # Magasin de vêtements, cabine 3D
│   ├── AtmSystem/          # Distributeurs bancaires
│   ├── CraftingSystem/     # Table d'artisanat
│   ├── Cooking/            # Stations de cuisine
│   ├── Npc/                # PNJ : spawn, comportements, traffic
│   ├── PhoneSystem/        # Téléphone portable
│   ├── Radio_System/       # Communication par fréquence
│   ├── DispatchSystem/     # Appels radio police/médecins
│   ├── TaskSystem/         # Objectifs assignés aux métiers
│   ├── MinimapSystem/      # Minimap + blips dynamiques
│   ├── Gps/                # Navigation GPS
│   ├── DayNightCycle_System/ # Horloge serveur sync
│   ├── AntiCheat/          # Validation serveur des actions sensibles
│   └── ...
│
├── World/                  # Objets interactifs dans la map
│   ├── PoliceComputer/     # Terminal MDT police
│   ├── PoliceLocker/       # Armurerie police
│   ├── Devices/            # Appareils interactifs (friteuse, grill…)
│   └── ...
│
├── UI/                     # Interfaces Razor (.razor)
│   ├── HUD/                # HUD principal
│   ├── Jobs/               # UIs spécifiques aux métiers
│   ├── Minimap/            # Affichage minimap
│   ├── NotificationSystem/ # Toast notifications
│   ├── Modals/             # Fenêtres modales génériques
│   └── ...
│
├── Database/               # DTOs et modèles de données (côté gamemode)
│   ├── DTO/                # Objets de transfert pour l'API
│   └── Tables/             # Modèles de tables
│
├── GameLoop/               # Boucle principale, règles de jeu, gestion voix
├── ChatSystem/             # Chat en jeu
├── Command/                # Commandes console admin
├── WeatherSystem/          # Météo
├── Settings/               # ConVars configurables
├── Utils/                  # Helpers, extensions, interfaces
└── Models/                 # Modèles 3D partagés
```

---

## Assets

```
Assets/
├── scenes/
│   └── maps/UnionCity/
│       └── unioncity.scene     # Scène principale — point d'entrée du jeu
├── prefabs/                    # Objets préfabriqués (PNJ, véhicules, props…)
├── models/                     # Modèles 3D importés
├── materials/                  # Matériaux
└── sounds/                     # Sons
```

---

## Points d'entrée clés

| Fichier | Rôle |
|---------|------|
| `Code/Api/ApiComponent.cs` | Authentification, token JWT, appels HTTP vers le backend |
| `Code/Systems/Pawn/Client.cs` | État principal du joueur (synced) |
| `Code/Systems/Pawn/Client.Data.cs` | Données persistantes : identité, économie, métier, licences |
| `Code/Systems/Character Manager/CharacterManager.cs` | Création de personnage (éditeur 3D in-game) |
| `Code/Systems/Jobs/` | Système de métiers et permissions |
| `Code/GameLoop/` | Boucle principale, connexion/déconnexion |
| `Code/Settings/` | ConVars (cf. [docs/CONFIG.md](../docs/CONFIG.md)) |

---

## Synchronisation réseau

Le gamemode utilise les conventions s&box :

- **`[Sync(SyncFlags.FromHost)]`** — propriété synchronisée depuis l'hôte vers tous les clients
- **`[Rpc.Host]`** — méthode exécutée uniquement sur le serveur
- **`[Rpc.Broadcast]`** — méthode exécutée sur tous les clients

> ⚠️ Toute mutation d'état (argent, inventaire, métier) doit passer par un `[Rpc.Host]` et être confirmée côté API avant d'être reflétée côté clients. Ne jamais faire confiance au client.

---

## Ajouter un système

1. Crée un dossier dans `Code/Systems/MonSysteme/`
2. Le composant principal hérite de `GameObjectSystem` ou `Component` selon le besoin
3. S'il a besoin de persister des données : ajoute un DTO dans `Code/Database/DTO/` et un endpoint dans le backend
4. L'UI va dans `Code/UI/MonSysteme/` en `.razor`
5. Teste en serveur dédié (pas seulement en listen server)

---

## ConVars utiles (console serveur)

```
core-api_base_url <url>          # URL de l'API (défaut: http://localhost:8443/api)
core-api_server_secret <secret>  # Secret pour l'auth serveur
core-connect_server              # Déclenche l'auth auprès de l'API
core-debug_inventory 1           # Active les logs détaillés inventaire
```

Référence complète : [docs/CONFIG.md](../docs/CONFIG.md)

---

## Troubleshooting

### Le hotload ne fonctionne pas / erreur de résolution de package
Le `core.sbproj` pointe encore sur `openframework.core` (org OpenFramework). Lance l'outil de fork : **Editor → OpenFramework → Configurer ce fork…**

### `Server authenticated successfully` n'apparaît pas
- Vérifie que `core.api` tourne : `docker compose ps`
- Vérifie que `SERVER_SECRET` dans `.env` = valeur passée à `core-api_server_secret`
- Vérifie que l'`ApiComponent.BaseUrl` pointe sur la bonne IP/port

### Les joueurs ne voient pas les changements d'état en temps réel
Propriété probablement non marquée `[Sync]`. Vérifie qu'elle est sur le composant `Client` (autorité host) et qu'elle utilise `SyncFlags.FromHost`.

### Crash au spawn d'un personnage
Regarde les logs `core.api` — souvent une migration EF Core manquante ou un DTO incompatible avec la version de l'API.
