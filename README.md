# OpenFramework Core

> Framework de roleplay open source pour [s&box](https://sbox.game). À la ESX/QBCore — un seul `git clone` et tu as tout : gamemode, API, devblog/admin web.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![s&box](https://img.shields.io/badge/s%26box-openframework.core-blue)](https://sbox.game/openframework)

## Qu'est-ce que c'est ?

OpenFramework Core est un framework complet pour héberger un serveur s&box de type roleplay :

- **`gamemode/`** — Le gamemode s&box lui-même (multijoueur dédié, économie, inventaire, jobs, NPC, véhicules, vêtements, etc.)
- **`backend/`** — API .NET 9 + SQL Server : auth Steam, persistance des characters, inventaires, économie, panel admin
- **`website/`** — Site web (Vite + Node.js) : devblog public + panel d'admin web pour la gestion des joueurs

Tout est containerisé sauf le serveur s&box dédié lui-même (qui doit tourner sur une machine avec Steam, comme un serveur FiveM).

## Quickstart (5 min)

```bash
# 1. Cloner
git clone https://github.com/openframeworkRP/core.git
cd core

# 2. Configurer
cp .env.example .env
# Edite .env et remplis MSSQL_SA_PASSWORD, JWT_KEY, SERVER_SECRET, STEAM_API_KEY, ALLOWED_STEAM_IDS

# 3. Lancer l'API + DB + website
docker compose up -d

# 4. Publier le gamemode (option a) ou monter en local (option b)
# Voir docs/SETUP.md pour les details
```

Plus de détails dans **[docs/SETUP.md](docs/SETUP.md)**.

## Structure

```
core/
├── gamemode/                   # Le gamemode s&box
│   ├── core.sbproj             # Projet s&box (publie comme openframework.core)
│   ├── Code/                   # Sources C#
│   └── Assets/                 # Models, materials, scenes, prefabs
├── backend/                    # API .NET 9 + EF Core
│   ├── OpenFramework.Api/      # Controllers, DTOs, DbContext
│   └── compose.yaml            # Compose standalone (utilise par le racine)
├── website/                    # Devblog + admin web
│   ├── frontend/               # Vite + React
│   ├── backend/                # Node.js + SQLite + Steam auth
│   └── docker-compose.yml      # Compose standalone (utilise par le racine)
├── docker-compose.yml          # Orchestration globale
├── .env.example                # Variables d'environnement
└── docs/                       # Documentation
```

## Prérequis

- **Docker** + **Docker Compose** (pour API + DB + website)
- **s&box** (Steam) avec accès dev pour publier le gamemode
- **Cle API Steam** : [https://steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey)

## Documentation

- **[docs/SETUP.md](docs/SETUP.md)** — Guide complet de setup self-hosted
- **[docs/CONFIG.md](docs/CONFIG.md)** — ConVars du gamemode et options de config
- **[docs/API.md](docs/API.md)** — Référence des endpoints de l'API .NET
- **[CONTRIBUTING.md](CONTRIBUTING.md)** — Guide de contribution

## Contraintes techniques

- **Multijoueur dédié** obligatoire — chaque feature doit fonctionner en serveur dédié, pas seulement en listen server.
- **Anti-duplication** — toutes les manipulations d'items/argent doivent être atomiques côté host.
- **Langue** — code, commentaires, commits et issues en français (par cohérence avec l'historique).

Détails dans [CONTRIBUTING.md](CONTRIBUTING.md).

## Licence

[MIT](LICENSE) — fais-en ce que tu veux, contribue si tu veux.

## Crédits

Fork de l'ancien gamemode `small_life`. Le projet est passé open source en 2026 pour devenir un framework communautaire.
