# Métier : Police

> Fichiers principaux : `gamemode/Code/Systems/Jobs/Jobs/PoliceJob.cs`  
> Apps : `gamemode/Code/World/Devices/Apps/PoliceApp/`  
> Terminal monde : `gamemode/Assets/items/furniture/electronics/policecomputer.prefab`  
> API : `backend/OpenFramework.Api/Controllers/MdtController.cs`

---

## Sommaire

- [Vue d'ensemble](#vue-densemble)
- [Actions sur un joueur](#actions-sur-un-joueur)
- [Système d'amendes](#système-damendes)
- [App Police (MDT)](#app-police-mdt)
- [Terminal fixe (policecomputer)](#terminal-fixe-policecomputer)
- [Armurerie de service (PoliceLocker)](#armurerie-de-service-policelocker)
- [Dispatch](#dispatch)
- [Endpoints API MDT](#endpoints-api-mdt)

---

## Vue d'ensemble

Le métier Police est identifié par le `JobIdentifier = "police"` dans `PoliceJob.cs`. Il est actif quand `Client.Data.Job == "police"`.

Fonctionnalités exclusives à ce métier :
- Menu d'interaction sur les joueurs (menotter, fouiller, amende, cellule)
- Accès à l'**App Police** (MDT) sur téléphone et terminal fixe
- Accès au **PoliceLocker** (armurerie de service)
- Réception des appels **Dispatch**
- Canal radio dédié

---

## Actions sur un joueur

Déclenchées via le **menu radial joueur** (long press E sur un autre joueur).  
Toutes les actions sont validées côté host via `[Rpc.Host]` dans `PoliceJob.cs`.

### Menotter / Libérer

```
RequestCuff(PlayerPawn target)
```

- Crée ou récupère un `HandcuffComponent` sur le Pawn cible
- Bascule `IsCuffed` (toggle) → synchronisé dans `client.Data.IsCuffed`
- Quand `IsCuffed = true` : le joueur ne peut plus courir, sauter ni utiliser son inventaire

### Fouiller

```
RequestSearch(PlayerPawn target)
```

- Affiche au policier (via `Notify`) l'argent liquide et chaque item de l'inventaire de la cible
- Aucun transfert d'item — uniquement lecture

### Transporter en cellule

```
RequestTpToCell(PlayerPawn target)
```

- Téléporte le personnage ciblé au point de spawn prison (défini par `Commands.RPC_RespawnInPrison`)
- Notification envoyée à la cible

### Mettre une amende

```
RequestFine(PlayerPawn target, FineReason template)
```

- Les motifs disponibles sont configurés dans **`Constants.Instance.FineReasons`** (liste `FineReason` avec `Name` et `Amount`)
- Un sous-menu "Mettre une amende" liste toutes les raisons disponibles avec leur montant
- Cooldown entre deux amendes : **3 secondes** (`FineCooldown`)
- Crée un objet `Fine` avec un `Id` unique (GUID), `IssuedAt = DateTime.Now`, `DueAt = +24h`
- Persistance double :
  1. `target.Client.Data.Fines.Add(fine)` — NetList synchronisée host → clients
  2. `ApiComponent.Instance.AddFine(...)` — persisté dans la colonne `FinesJson` du personnage en base

---

## Système d'amendes

### Modèle `Fine` (`gamemode/Code/Models/Fine.cs`)

| Champ | Type | Description |
|-------|------|-------------|
| `Id` | `string` | GUID unique de l'amende |
| `IssuedAt` | `DateTime` | Date/heure d'émission |
| `DueAt` | `DateTime` | Échéance (par défaut +24h) |
| `Amount` | `int` | Montant en dollars |
| `Reason` | `string` | Motif de l'amende |
| `Paid` | `bool` | Payée ou non |
| `PaidAt` | `DateTime?` | Date de paiement (null si impayée) |

### Persistance

- **En session** : `NetList<Fine>` dans `ClientData.Fines` — synchronisée depuis le host vers tous les clients
- **En base** : colonne `FinesJson` (JSON array) sur le modèle `Character` — chargée au choix de personnage via `PlayerApiBridge.LoadFinesAsync()`

### Paiement par le joueur

Depuis le **menu personnel** (touche menu personnel, section amendes) :
1. Le joueur clique "Payer" sur une amende impayée
2. `PlayerApiBridge.PayFine(fineId)` → RPC host `RequestPayFine`
3. Le host vérifie que le joueur a assez d'argent
4. `MoneySystem.Remove(caller, fine.Amount)` débite le compte
5. `fine.Paid = true` dans `caller.Data.Fines`
6. `ApiComponent.Instance.PayFine(characterId, fineId)` met à jour la base

### Configurer les motifs d'amendes

Dans `Constants.Instance.FineReasons` (asset `Constants`), ajouter des entrées `FineReason` :

```
Name   : "Excès de vitesse"
Amount : 500
```

---

## App Police (MDT)

L'**App Police** est une application dédiée accessible uniquement par les joueurs de métier `police`.

### Accès

- **Téléphone** : l'App Police apparaît dans le lanceur d'applications (filtré par `JobAccess = JobList.Police`)
- **Terminal fixe** : `policecomputer.prefab` placé sur la map (voir section suivante)

### Pages de l'app

| Route | Composant | Contenu |
|-------|-----------|---------|
| `/` | `HomePagePolice` | Accueil police |
| `/finance` | `FinancePagePolice` | Statistiques financières du département |
| `/amendes` | `FinesPagePolice` | Recherche d'amendes par nom RP |

### Page Amendes (`/amendes`)

- Champ de recherche (min 2 caractères) → bouton "Rechercher"
- Appelle `GET /api/mdt/search?query=X`
- Affiche les résultats sous forme de **cartes personnage** :
  - Nom complet + date de naissance
  - Badge vert "0 amende(s)" ou rouge "N amende(s)"
  - Tableau des amendes : statut (Payée / Impayée), motif, montant, date d'émission, échéance
  - Dates dépassées sur amendes impayées affichées en rouge

### Fichiers

```
gamemode/Code/World/Devices/Apps/PoliceApp/
├── PoliceApp.razor                — Déclaration de l'app (JobAccess, icône)
├── PoliceApp.splash.razor         — Splash screen au lancement
├── PoliceApp.navigator.razor      — NavigationHost (routes)
├── HomePagePolice.razor           — Page accueil
├── FinancePagePolice.razor        — Page finance
├── FinesPagePolice.razor          — Page recherche amendes
└── FinesPagePolice.razor.scss
```

---

## Terminal fixe (policecomputer)

Prefab monde statique placé directement sur la map, non déplaçable par les joueurs.

### Placement

Ouvrir la scène dans l'éditeur s&box, instancier `policecomputer.prefab` et positionner le terminal.

### Interaction

- Long press `E` dans la range → détection via `PoliceComputer` dans `PlayerPawn.HandleLongPress()`
- Si le job du joueur ≠ `"police"` → notification d'erreur, l'écran ne s'ouvre pas
- Si le job est `"police"` → `BaseDevice.PowerOn()` active l'écran et donne le focus
- L'écran affiche directement le `PoliceApp_navigator` (pas de lock screen)
- Bouton **✕** en haut à droite → `PowerOff()`, ferme l'écran

### Composants du prefab

```
policecomputer (root)
├── ModelCollider         — modèle monitor.vmdl
├── ModelRenderer         — modèle monitor.vmdl
├── HighlightOutline      — contour bleu police au survol (géré par PoliceComputer.SetHover)
├── Rigidbody             — MotionEnabled=false, toutes rotations/positions lockées
└── PoliceComputer        — hérite BaseDevice, Kind=Computer, police-only check

    └── World (child)
        ├── PoliceComputerScreen  — PanelComponent Razor, héberge PoliceApp_navigator
        ├── WorldPanel            — rendu 3D de l'UI (2208×1263px)
        └── WorldInput            — route les clics souris vers l'UI
```

### Fichiers

```
gamemode/Assets/items/furniture/electronics/policecomputer.prefab
gamemode/Code/World/PoliceComputer/PoliceComputer.cs
gamemode/Code/World/Devices/List/Computer/PoliceComputerScreen.razor
gamemode/Code/World/Devices/List/Computer/PoliceComputerScreen.razor.scss
```

---

## Armurerie de service (PoliceLocker)

Coffre physique (`PoliceLocker.cs`) placé dans le commissariat.

- Long press `E` → détection dans `HandleLongPress` → `PlayerRadialMenu.OpenForPoliceLocker()`
- `CanUse()` vérifie que le job est `"police"` côté host
- La liste des items disponibles est configurée directement sur le composant `PoliceLocker` dans l'éditeur (propriété `AvailableItems`)
- `RequestTakeItem()` (Rpc.Host) valide le job, vérifie l'inventaire et ajoute l'arme avec attribut `service_weapon`
- Un joueur ne peut pas récupérer une seconde arme de service si `service_weapon` est déjà dans son inventaire

---

## Dispatch

Système d'appels d'urgence entrants pour les policiers et médecins.

- Accessible via le **menu personnel Police** → "Dispatch" → `DispatchUI.Toggle()`
- Les appels affichent type, position et priorité
- Les blips dispatch sont visibles sur la minimap pour les joueurs Police

---

## Endpoints API MDT

Tous les endpoints sont sous `/api/mdt` avec autorisation `Roles = "GameServer"`.

### Casier judiciaire

| Méthode | Route | Description |
|---------|-------|-------------|
| `GET` | `/api/mdt/criminalrecord/{characterId}` | Lire le casier d'un personnage |
| `POST` | `/api/mdt/criminalrecord/{characterId}/addrecord` | Ajouter une infraction au casier |

### Amendes

| Méthode | Route | Description |
|---------|-------|-------------|
| `GET` | `/api/mdt/search?query=X` | Rechercher des personnages par prénom/nom (min 2 car., max 20 résultats) |
| `GET` | `/api/mdt/fines/{characterId}` | Lire toutes les amendes d'un personnage |
| `POST` | `/api/mdt/fines/{characterId}/add` | Ajouter une amende à un personnage |
| `POST` | `/api/mdt/fines/{characterId}/{fineId}/pay` | Marquer une amende comme payée |

### Corps de la requête `POST /add`

```json
{
  "id": "guid-optionnel",
  "issuedAt": "2026-05-03T20:00:00Z",
  "dueAt": "2026-05-04T20:00:00Z",
  "amount": 500,
  "reason": "Excès de vitesse",
  "issuedByCharacterId": "char-guid"
}
```
