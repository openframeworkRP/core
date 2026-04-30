# API .NET — Référence

> ⚠️ Stub — à compléter au fil des PR.

L'API est documentée via **Swagger** automatiquement. Une fois lancée :

- Swagger UI : http://localhost:8443/swagger
- Endpoint OpenAPI : http://localhost:8443/swagger/v1/swagger.json

## Authentification

Deux types de tokens JWT :

- **JWT joueur** : généré via auth Steam (1 par SteamId), endpoint `/auth/login`
- **JWT serveur** : généré au démarrage du serveur dédié via `core-api_server_secret`, endpoint `/auth/server-login`

Les endpoints sensibles (admin, économie host-only) requièrent le JWT serveur.

## Controllers exposes

| Controller | Préfixe | Description |
|-----------|---------|-------------|
| `ServerAuthController` | `/auth/*` | Login Steam joueur, login serveur |
| `CharacterController` | `/Character/*` | CRUD characters d'un joueur |
| `InventoryController` | `/inventory/*` | Sync de l'inventaire |
| `PositionController` | `/position/*` | Save/restore des positions |
| `ClothesController` | `/clothes/*` | Apparence des characters |
| `BankController` | `/bank/*` | Comptes bancaires |
| `AtmController` | `/atm/*` | ATM (retraits/dépôts) |
| `EventsController` | `/events/*` | Logs d'événements (chat, sessions, inventaire, admin actions) |
| `MdtController` | `/mdt/*` | MDT (Mobile Data Terminal) police |
| `Admin*Controller` | `/admin/*` | Endpoints du panel admin web |

Voir le code dans `backend/OpenFramework.Api/Controllers/` pour le détail.
