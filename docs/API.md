# API .NET — Référence détaillée

> Code source dans `backend/OpenFramework.Api/`.  
> Stack : **ASP.NET 9** + **Entity Framework Core** + **SQL Server (MSSQL)**.  
> Documentation interactive automatique via **Swagger** : `http://localhost:8443/swagger`

---

## Sommaire

- [Architecture](#architecture)
- [Authentification](#authentification)
- [Controllers & Endpoints](#controllers--endpoints)
  - [Auth](#auth--serverauthcontroller)
  - [Personnages](#personnages--charactercontroller)
  - [Inventaire](#inventaire--inventorycontroller)
  - [Apparence & Vêtements](#apparence--clothescontroller)
  - [Positions](#positions--positioncontroller)
  - [Banque](#banque--bankcontroller)
  - [ATM](#atm--atmcontroller)
  - [Police MDT](#police-mdt--mdtcontroller)
  - [Événements](#événements--eventscontroller)
  - [Administration](#administration)
- [Base de données](#base-de-données)
- [Variables d'environnement](#variables-denvironnement)

---

## Architecture

```
backend/
└── OpenFramework.Api/
    ├── Controllers/            # Endpoints REST (un fichier par domaine)
    ├── Data/
    │   ├── AppDbContext.cs     # DbContext EF Core
    │   └── Migrations/         # Migrations EF Core auto-générées
    ├── Models/                 # Entités EF Core (tables)
    ├── DTOs/                   # Data Transfer Objects (entrée/sortie)
    ├── Services/               # Logique métier extraite des controllers
    ├── Middleware/             # Auth JWT, gestion d'erreurs
    ├── appsettings.json        # Config (surchargée par variables d'env)
    └── Program.cs              # Bootstrap, injection de dépendances
```

### Flux de requête type

```
Client (gamemode ou website)
  → HTTPS
  → Middleware JWT (valide le token, extrait le rôle)
  → Controller
  → Service (logique métier)
  → DbContext EF Core
  → SQL Server
  → Réponse JSON
```

### Rôles JWT

| Rôle | Émis à | Accès |
|---|---|---|
| `Player` | Client s&box (joueur) | Ses propres données (personnage, inventaire, banque) |
| `GameServer` | Serveur dédié s&box | Toutes les données de tous les joueurs + actions admin |

Les endpoints sensibles ont l'attribut `[Authorize(Roles = "GameServer")]`. Les endpoints joueur ont `[Authorize(Roles = "Player,GameServer")]`.

---

## Authentification

### Login serveur dédié

```http
POST /api/auth/server
Content-Type: application/json

{
  "serverSecret": "valeur de SERVER_SECRET dans .env"
}
```

**Réponse :**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-05-02T10:00:00Z"
}
```

Le serveur dédié utilise ce token pour toutes ses requêtes suivantes. Il est renouvelé automatiquement avant expiration.

### Login joueur

```http
POST /api/auth/player
Content-Type: application/json

{
  "steamId": "76561198000000000",
  "serverToken": "token GameServer pour valider que la requête vient bien du serveur"
}
```

**Réponse :**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-05-02T10:00:00Z"
}
```

---

## Controllers & Endpoints

### Auth — `ServerAuthController`

| Méthode | Route | Rôle requis | Description |
|---|---|---|---|
| POST | `/api/auth/server` | *(aucun)* | Authentifie le serveur dédié, retourne JWT GameServer |
| POST | `/api/auth/player` | GameServer | Génère un JWT Player pour un SteamID |

---

### Personnages — `CharacterController`

Les personnages sont liés à un SteamID. Un joueur peut en avoir plusieurs.

| Méthode | Route | Rôle requis | Description |
|---|---|---|---|
| GET | `/api/character/list` | GameServer | Liste les personnages d'un SteamID |
| POST | `/api/character/create` | GameServer | Crée un nouveau personnage |
| POST | `/api/character/{id}/update` | GameServer | Met à jour les données du personnage |
| DELETE | `/api/character/{id}/delete` | GameServer | Supprime un personnage |
| PUT | `/api/character/{id}/appearance` | GameServer | Met à jour l'apparence (morphs, genre, tenue) |
| POST | `/api/character/{id}/changeActualJob` | GameServer | Change le métier actif du personnage |
| GET | `/api/character/{id}` | Player, GameServer | Récupère les données d'un personnage |

**Corps de création :**
```json
{
  "steamId": "76561198000000000",
  "firstName": "John",
  "lastName": "Doe",
  "gender": "male"
}
```

**Corps de mise à jour de l'apparence :**
```json
{
  "gender": "male",
  "skinColor": [0.8, 0.6, 0.5],
  "morphs": [0.1, 0.3, 0.0, 0.5, ...],
  "outfit": {
    "hat": "item_cap_01",
    "top": "item_shirt_02",
    "bottom": "item_jeans_01",
    "shoes": "item_sneakers_03"
  }
}
```

---

### Inventaire — `InventoryController`

L'inventaire est lié au personnage actif du joueur (`/characters/actual/`).

| Méthode | Route | Rôle requis | Description |
|---|---|---|---|
| GET | `/api/characters/actual/inventory/get` | Player, GameServer | Récupère l'inventaire complet |
| POST | `/api/characters/actual/inventory/add` | GameServer | Ajoute un item (itemId, quantité, slot) |
| POST | `/api/characters/actual/inventory/delete` | GameServer | Supprime un item (itemId ou slot) |
| POST | `/api/characters/actual/inventory/clear` | GameServer | Vide entièrement l'inventaire |

**Corps d'ajout d'item :**
```json
{
  "itemId": "item_pistol_9mm",
  "quantity": 1,
  "slot": 3,
  "metadata": {}
}
```

**Réponse inventaire :**
```json
{
  "slots": [
    {
      "slot": 0,
      "itemId": "item_burger",
      "quantity": 2,
      "metadata": {}
    },
    {
      "slot": 3,
      "itemId": "item_pistol_9mm",
      "quantity": 1,
      "metadata": { "ammo": 15 }
    }
  ]
}
```

---

### Apparence & Vêtements — `ClothesController`

| Méthode | Route | Rôle requis | Description |
|---|---|---|---|
| GET | `/api/clothes/{characterId}` | Player, GameServer | Récupère la tenue complète |
| PUT | `/api/clothes/{characterId}` | GameServer | Met à jour la tenue |
| GET | `/api/clothes/catalogue` | Player, GameServer | Liste tous les vêtements disponibles |

---

### Positions — `PositionController`

| Méthode | Route | Rôle requis | Description |
|---|---|---|---|
| GET | `/api/position/{characterId}` | GameServer | Récupère la dernière position sauvegardée |
| POST | `/api/position/{characterId}` | GameServer | Sauvegarde la position actuelle |

**Corps de sauvegarde :**
```json
{
  "x": 1234.5,
  "y": -567.8,
  "z": 42.0,
  "map": "unioncity"
}
```

---

### Banque — `BankController`

| Méthode | Route | Rôle requis | Description |
|---|---|---|---|
| POST | `/api/bank/accounts/create` | GameServer | Crée un compte bancaire |
| GET | `/api/bank/accounts/{characterId}` | Player, GameServer | Liste les comptes d'un personnage |
| GET | `/api/bank/accounts/{accountId}/details` | Player, GameServer | Détails d'un compte (solde, membres, transactions) |
| POST | `/api/bank/accounts/{accountId}/members` | GameServer | Ajoute un co-titulaire |
| DELETE | `/api/bank/accounts/{accountId}/members/{targetCharacterId}` | GameServer | Retire un co-titulaire |
| POST | `/api/bank/transfer` | GameServer | Virement entre deux comptes |
| POST | `/api/bank/deposit` | GameServer | Dépôt d'argent liquide sur un compte |
| POST | `/api/bank/withdraw` | GameServer | Retrait d'un compte vers le liquide |

**Corps de virement :**
```json
{
  "fromAccountId": 1,
  "toAccountId": 7,
  "amount": 500.00,
  "reason": "Remboursement"
}
```

**Réponse compte :**
```json
{
  "id": 1,
  "name": "Compte courant — John Doe",
  "balance": 12500.00,
  "reserve": 500.00,
  "interestRate": 0.02,
  "members": [
    { "characterId": 3, "name": "John Doe", "role": "owner" }
  ]
}
```

---

### ATM — `AtmController`

| Méthode | Route | Rôle requis | Description |
|---|---|---|---|
| POST | `/api/atm/withdraw` | Player, GameServer | Retrait depuis un ATM |
| POST | `/api/atm/deposit` | Player, GameServer | Dépôt depuis un ATM |
| GET | `/api/atm/balance/{characterId}` | Player, GameServer | Solde consultable depuis un ATM |

Les opérations ATM sont validées côté API : vérification du solde, application de la réserve intouchable avant de valider le retrait.

---

### Police MDT — `MdtController`

| Méthode | Route | Rôle requis | Description |
|---|---|---|---|
| GET | `/api/mdt/search/{query}` | GameServer | Recherche un personnage par nom |
| GET | `/api/mdt/criminalrecord/{characterId}` | GameServer | Récupère le casier judiciaire complet |
| POST | `/api/mdt/criminalrecord/{characterId}/addrecord` | GameServer | Ajoute une infraction au casier |
| GET | `/api/mdt/fines/{characterId}` | GameServer | Liste les amendes d'un personnage |
| POST | `/api/mdt/fines/{characterId}/pay` | GameServer | Marque une amende comme payée |

**Corps d'ajout d'infraction :**
```json
{
  "officerId": 12,
  "offenceType": "assault",
  "description": "Agression sur la place principale",
  "fine": 2500.00,
  "jailTime": 0
}
```

**Réponse casier judiciaire :**
```json
{
  "characterId": 5,
  "name": "Jane Smith",
  "records": [
    {
      "id": 1,
      "date": "2026-04-15T14:32:00Z",
      "officerName": "Officer Williams",
      "offenceType": "speeding",
      "description": "Excès de vitesse zone 50",
      "fine": 350.00,
      "paid": true
    }
  ]
}
```

---

### Événements — `EventsController`

Endpoint de logging utilisé par le gamemode pour remonter des événements au backend.

| Méthode | Route | Rôle requis | Description |
|---|---|---|---|
| POST | `/api/events/log` | GameServer | Logue un événement (chat, connexion, action admin, mort) |
| GET | `/api/events` | GameServer | Liste les événements avec filtres |

**Corps de log :**
```json
{
  "type": "admin_action",
  "actorSteamId": "76561198000000000",
  "targetSteamId": "76561198000000001",
  "action": "ban",
  "reason": "Triche",
  "metadata": {}
}
```

Types d'événements supportés :
- `player_join` / `player_leave`
- `chat_message`
- `inventory_change`
- `admin_action` (ban, kick, whitelist, jail, amende)
- `player_death`
- `anticheat_flag`

---

### Administration

#### Bans — `AdminController`

| Méthode | Route | Rôle requis | Description |
|---|---|---|---|
| POST | `/api/admin/ban/` | GameServer | Bannir un joueur |
| POST | `/api/admin/unban/{userId}` | GameServer | Lever un ban |
| GET | `/api/admin/ban/getList` | GameServer | Liste des bans actifs |
| GET | `/api/admin/ban/{userId}` | GameServer | Détail d'un ban |

**Corps de ban :**
```json
{
  "steamId": "76561198000000000",
  "reason": "Triche — vitesse anormale",
  "durationMinutes": 10080,
  "adminSteamId": "76561198000000001"
}
```

#### Whitelist — `AdminController`

| Méthode | Route | Rôle requis | Description |
|---|---|---|---|
| POST | `/api/admin/whitelist/` | GameServer | Ajouter à la whitelist |
| POST | `/api/admin/whitelist/{userId}/supp` | GameServer | Retirer de la whitelist |
| GET | `/api/admin/whitelist/` | GameServer | Liste de la whitelist |

#### Queue de commandes — `AdminCommandController`

| Méthode | Route | Rôle requis | Description |
|---|---|---|---|
| POST | `/api/admin/command/queue` | *(token web)* | Dépose une commande en file d'attente |
| GET | `/api/admin/command/pending` | GameServer | Récupère les commandes en attente |
| POST | `/api/admin/command/{id}/ack` | GameServer | Marque une commande comme exécutée |

**Corps de commande :**
```json
{
  "command": "jail",
  "targetSteamId": "76561198000000000",
  "params": { "duration": 600, "reason": "Trouble à l'ordre public" },
  "adminSteamId": "76561198000000001"
}
```

#### Personnages admin — `AdminCharacterController`

| Méthode | Route | Rôle requis | Description |
|---|---|---|---|
| GET | `/api/admin/characters` | GameServer | Liste tous les personnages |
| GET | `/api/admin/characters/{id}` | GameServer | Détail d'un personnage |
| POST | `/api/admin/characters/{id}/setmoney` | GameServer | Modifier l'argent liquide |
| POST | `/api/admin/characters/{id}/setjob` | GameServer | Forcer un métier |

#### Inventaire admin — `AdminInventoryController`

| Méthode | Route | Rôle requis | Description |
|---|---|---|---|
| GET | `/api/admin/inventory/{characterId}` | GameServer | Voir l'inventaire d'un joueur |
| POST | `/api/admin/inventory/{characterId}/add` | GameServer | Ajouter un item (admin) |
| POST | `/api/admin/inventory/{characterId}/clear` | GameServer | Vider l'inventaire (admin) |

#### Lecture globale — `AdminReadController`

| Méthode | Route | Rôle requis | Description |
|---|---|---|---|
| GET | `/api/admin/players/online` | GameServer | Joueurs actuellement connectés |
| GET | `/api/admin/players/history` | GameServer | Historique des connexions |
| GET | `/api/admin/stats` | GameServer | Statistiques agrégées (joueurs uniques, sessions, etc.) |

---

## Base de données

### Tables principales (EF Core)

| Table | Description |
|---|---|
| `Users` | Comptes joueurs (SteamId, date de création, ban, whitelist) |
| `Characters` | Personnages (nom, genre, métier, argent liquide, position) |
| `CharacterAppearances` | Morphs, couleurs, tenue par personnage |
| `Inventories` | Items en inventaire (characterId, itemId, slot, quantité, metadata JSON) |
| `BankAccounts` | Comptes bancaires (solde, réserve, taux d'intérêt) |
| `BankAccountMembers` | Relation compte ↔ personnage (co-titulaires) |
| `BankTransactions` | Historique des opérations bancaires |
| `CriminalRecords` | Casier judiciaire (characterId, offence, amende, date) |
| `AdminBans` | Bans actifs et historique |
| `AdminWhitelist` | SteamIDs whitelistés |
| `AdminCommands` | Queue de commandes web → serveur |
| `EventLogs` | Logs d'événements (connexions, actions admin, chat) |

### Migrations EF Core

Les migrations sont appliquées automatiquement au démarrage de l'API (`Program.cs` appelle `db.Database.Migrate()`).

Pour générer une nouvelle migration après modification d'un modèle :
```bash
cd backend/OpenFramework.Api
dotnet ef migrations add NomDeLaMigration
```

Pour appliquer manuellement :
```bash
dotnet ef database update
```

---

## Variables d'environnement

| Variable | Description |
|---|---|
| `MSSQL_SA_PASSWORD` | Mot de passe SQL Server SA |
| `ConnectionStrings__DefaultConnection` | Chaîne de connexion EF Core (générée automatiquement par Docker Compose) |
| `JWT_KEY` | Clé de signature JWT (min 32 caractères recommandé) |
| `JWT_ISSUER` | Issuer JWT (ex: `openframework`) |
| `JWT_AUDIENCE` | Audience JWT (ex: `openframework-clients`) |
| `SERVER_SECRET` | Secret partagé API ↔ gamemode pour l'auth serveur |
| `ASPNETCORE_ENVIRONMENT` | `Production` ou `Development` (active Swagger en dev) |

> En production, Swagger est désactivé. Pour le réactiver temporairement, passer `ASPNETCORE_ENVIRONMENT=Development`.
