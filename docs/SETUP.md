# Setup self-hosted — OpenFramework Core

Guide complet pour héberger ton propre serveur OpenFramework.

## Prérequis

- **Docker** + **Docker Compose** ([install](https://docs.docker.com/get-docker/))
- **s&box** installé avec accès dev (Steam)
- **Clé API Steam** : [steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey)
- Ton **SteamID64** : [steamid.io](https://steamid.io/)

## 1. Cloner et configurer

```bash
git clone https://github.com/openframeworkRP/core.git
cd core
cp .env.example .env
```

Édite `.env` et renseigne au minimum :

| Variable | Description |
|----------|-------------|
| `MSSQL_SA_PASSWORD` | Mot de passe SQL Server (>= 8 char, maj + chiffre + spécial) |
| `JWT_KEY` | Clé de signature JWT — génère via `openssl rand -base64 64` |
| `SERVER_SECRET` | Secret partagé API↔gamemode — `openssl rand -hex 32` |
| `STEAM_API_KEY` | Ta clé API Steam (cf. lien ci-dessus) |
| `SESSION_SECRET` | Secret session du website — `openssl rand -hex 32` |
| `ALLOWED_STEAM_IDS` | Ton SteamID64 (séparés par virgule pour plusieurs admins) |
| `GAME_SERVER_SECRET` | **Même valeur** que `SERVER_SECRET` |

## 2. Lancer l'infra

```bash
docker compose up -d
```

Services lancés :
- `core-api` — API .NET du jeu sur `localhost:8443`
- `core-sqlserver` — SQL Server sur `localhost:1433`
- `core-adminer` — UI DB sur `localhost:8080`
- `core-website-api` — API du devblog sur `localhost:3001`
- `core-website-frontend` — site web sur `localhost:4173`
- `core-website-scraper` — scraper FAB

Vérifie que tout tourne :
```bash
docker compose ps
docker compose logs -f core.api
```

## 3. Migrer la base de données

Au premier lancement, l'API .NET applique les migrations EF Core automatiquement. Si elle ne le fait pas, force avec :

```bash
docker compose exec core.api dotnet ef database update
```

## 4. Configurer le gamemode

### Option A — Publier sur s&box (recommandé)

```bash
cd gamemode
# Ouvrir le projet dans s&box editor (double-click core.sbproj)
# Puis Editor > Publish > openframework.core
```

Sur ton serveur dédié, dans la config :
```
+gamemode openframework.core
```

### Option B — Local (dev)

Monte directement `gamemode/` dans ton install s&box ou édite la scene `scenes/maps/unioncity/unioncity.scene` pour pointer le composant `ApiComponent.BaseUrl` vers `http://<ton-ip>:8443/api`.

## 5. Lancer le serveur s&box dédié

Le serveur s&box dédié **ne tourne pas dans Docker** (besoin de Steam/Facepunch). Lance-le sur ta machine :

```bash
# Depuis l'install s&box
sbox.exe -dedicated +gamemode openframework.core +map unioncity
```

Au démarrage, dans la console serveur :
```
core-api_server_secret <ton SERVER_SECRET du .env>
core-connect_server
```

Si l'auth réussit, tu verras dans les logs `core-api` :
```
Server authenticated successfully
```

## 6. Accéder au panel admin

- Site : http://localhost:4173
- Admin : http://localhost:4173/admin (login Steam)
- Adminer (DB) : http://localhost:8080 (server: `sqlserver`, user: `sa`, mot de passe: `${MSSQL_SA_PASSWORD}`)

## Troubleshooting

### `core.api` boucle sur `Authentication failed`
Le `SERVER_SECRET` du `.env` ne correspond pas à `core-api_server_secret` dans la console s&box. Vérifie que les deux valeurs sont identiques.

### `Cannot connect to SQL Server`
Le mot de passe SA ne respecte pas la policy MSSQL : minimum 8 caractères, au moins une majuscule, un chiffre, un caractère spécial.

### Le site web affiche "Maintenance"
La variable `VITE_MAINTENANCE` est sur `true` dans `.env`. Mets-la sur `false` et rebuild :
```bash
docker compose build website.frontend && docker compose up -d website.frontend
```

### "File not found" au runtime du gamemode
Le runtime s&box ne trouve pas une ressource — généralement il faut republier le gamemode (`Editor > Publish`).

## Mise à jour

```bash
git pull
docker compose pull
docker compose up -d --build
```

---

Tu bloques quelque part ? Ouvre une issue sur [github.com/openframeworkRP/core/issues](https://github.com/openframeworkRP/core/issues).
