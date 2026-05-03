# Setup self-hosted — OpenFramework Core

Guide complet pour héberger ton propre serveur OpenFramework.

## Prérequis

- **Docker** + **Docker Compose** ([install](https://docs.docker.com/get-docker/))
- **s&box** installé avec accès dev (Steam)
- **Clé API Steam** : [steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey)
- Ton **SteamID64** : [steamid.io](https://steamid.io/)

---

## 1. Cloner et configurer

```bash
git clone https://github.com/openframeworkRP/core.git
cd core
cp .env.example .env
```

Édite `.env` et renseigne au minimum :

| Variable | Description |
|----------|-------------|
| `POSTGRES_PASSWORD` | Mot de passe PostgreSQL (choisis-en un robuste) |
| `JWT_KEY` | Clé de signature JWT — génère via `openssl rand -base64 64` |
| `SERVER_SECRET` | Secret partagé API↔gamemode — `openssl rand -hex 32` |
| `STEAM_API_KEY` | Ta clé API Steam (cf. lien ci-dessus) |
| `SESSION_SECRET` | Secret session du website — `openssl rand -hex 32` |
| `ALLOWED_STEAM_IDS` | Ton SteamID64 (séparés par virgule pour plusieurs admins) |
| `GAME_SERVER_SECRET` | **Même valeur** que `SERVER_SECRET` |

> **Conseil** : utilise le wizard browser (option 1 du README) pour que tout ça soit généré automatiquement.

---

## 2. Lancer l'infra

```bash
docker compose up -d
```

Services lancés :

| Service | Rôle | Port |
|---------|------|------|
| `core.api` | API .NET du jeu | `localhost:8443` |
| `postgres` | Base de données PostgreSQL | `localhost:5432` |
| `redis` | Cache / sessions | `localhost:6379` |
| `adminer` | Interface web PostgreSQL | `localhost:8080` |
| `website.api` | API Node.js du devblog/admin | `localhost:3001` |
| `website.frontend` | Site web React | `localhost:4173` |
| `website.scraper` | Scraper FAB (optionnel) | interne |

Vérifie que tout tourne :
```bash
docker compose ps
docker compose logs -f core.api
```

Les migrations EF Core sont appliquées **automatiquement** au premier démarrage de `core.api`. Aucune commande manuelle requise.

---

## 3. Configurer le gamemode

### Option A — Publier sur s&box (recommandé pour la prod)

1. Ouvre `gamemode/` dans l'éditeur s&box (double-clic sur `core.sbproj`)
2. Si tu forkes, configure d'abord l'identité : **Editor → OpenFramework → Configurer ce fork…** (cf. README principal)
3. **Editor → Publish** → `openframework.core` (ou ton propre ident)

Sur ton serveur dédié, dans la config :
```
+gamemode openframework.core
```

### Option B — Monter le dossier en local (dev)

Monte directement `gamemode/` dans ton install s&box. L'éditeur recharge le code à chaud sans republier.

Dans tous les cas, l'`ApiComponent` doit pointer vers ton API :
- Via la scène `Assets/scenes/maps/UnionCity/unioncity.scene` → composant `ApiComponent` → champ `BaseUrl` : `http://<ton-ip>:8443/api`
- Ou via ConVar `core-api_base_url http://<ton-ip>:8443/api` dans la console serveur

---

## 4. Lancer le serveur s&box dédié

Le serveur s&box dédié **ne tourne pas dans Docker** (il a besoin de Steam/Facepunch). Lance-le sur ta machine :

```bash
# Depuis ton install s&box
sbox.exe -dedicated +gamemode openframework.core +map unioncity
```

Au démarrage, dans la console serveur :
```
core-api_server_secret <valeur de SERVER_SECRET dans ton .env>
core-connect_server
```

Si l'auth réussit, tu verras dans `docker compose logs core.api` :
```
Server authenticated successfully
```

---

## 5. Accéder au panel admin

| URL | Usage |
|-----|-------|
| http://localhost:4173 | Site web public |
| http://localhost:4173/admin | Panel admin (login Steam) |
| http://localhost:8080 | Adminer — UI base de données |

Pour Adminer : serveur `postgres`, base `OpenFrameworkDb` (ou ta valeur `POSTGRES_DB`), utilisateur `postgres` (ou `POSTGRES_USER`), mot de passe = ta valeur `POSTGRES_PASSWORD`.

---

## Troubleshooting

### `core.api` boucle sur `Authentication failed`
Le `SERVER_SECRET` du `.env` ne correspond pas à `core-api_server_secret` dans la console s&box. Vérifie que les deux valeurs sont identiques.

### `Le wizard ne s'affiche pas`
Le wizard s'affiche uniquement si `data/config/setup-complete.flag` n'existe pas. Si tu veux le relancer : `rm data/config/setup-complete.flag` puis `docker compose restart website.api`.

### `Port already in use`
Change les ports dans `.env` (`API_PORT`, `POSTGRES_PORT`, `ADMINER_PORT`) puis relance avec `docker compose up -d`.

### `Containers en erreur au démarrage`
```bash
docker compose logs core.api        # API .NET
docker compose logs website.api     # API Node.js
docker compose logs postgres        # Base de données
```
Le plus souvent : `POSTGRES_PASSWORD` manquant ou `JWT_KEY` vide.

### `Le site affiche "Maintenance"`
La variable `VITE_MAINTENANCE` est sur `true` dans `.env`. Mets-la sur `false` et rebuild :
```bash
docker compose build website.frontend && docker compose up -d website.frontend
```

### `File not found` au runtime du gamemode
Le runtime s&box ne trouve pas une ressource — généralement il faut republier le gamemode (`Editor → Publish`).

### `Cannot connect to the database` (API .NET)
Vérifie que le container `postgres` est bien `Up` (`docker compose ps`). L'API réessaie au démarrage, mais si PostgreSQL met trop de temps à démarrer, force un restart : `docker compose restart core.api`.

---

## Mise à jour

```bash
git pull
docker compose pull
docker compose up -d --build
```

Les migrations EF Core sont appliquées automatiquement au redémarrage de `core.api`.

---

Tu bloques quelque part ? Ouvre une issue sur [github.com/openframeworkRP/core/issues](https://github.com/openframeworkRP/core/issues).
