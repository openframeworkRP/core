# Small Box Studio — Site Web

## ⚡ Démarrage rapide

```bash
# Copier et remplir les variables d'environnement
cp .env.example .env

# Lancer le site (prod)
docker compose up -d
```

Site dispo sur → `http://localhost:4173`  
API dispo sur → `http://localhost:3001`

---

## 🔄 Commandes du quotidien

### Lancer
```bash
docker compose up -d
```

### Arrêter (données conservées)
```bash
docker compose down
```

### Voir les logs en live
```bash
docker compose logs -f
# ou uniquement le backend :
docker compose logs -f api
```

### Redémarrer un service
```bash
docker compose restart api
docker compose restart frontend
```

---

## 🔨 Rebuild après un changement de code

> ✅ La base de données et les uploads sont dans `./data/` et `./uploads/` sur ta machine — **ils ne sont jamais touchés par un rebuild**.

### Rebuild complet (les deux services)
```bash
docker compose up -d --build
```

### Rebuild uniquement le backend
```bash
docker compose up -d --build api
```

### Rebuild uniquement le frontend
```bash
docker compose up -d --build frontend
```

---

## 🧪 Mode développement

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

---

## 💾 Sauvegarde & restauration de la DB

### Sauvegarder
```bash
./scripts/backup.sh
```
→ Crée une copie dans `./backups/`

### Restaurer
```bash
./scripts/restore.sh backups/devblog_YYYY-MM-DD.sqlite
```

### Sauvegarde manuelle rapide
```bash
cp data/devblog.sqlite data/devblog.sqlite.bak
```

---

## ⚠️ Ce qu'il ne faut PAS faire

```bash
# ❌ Supprime les volumes Docker (inutile mais risqué si quelqu'un migre)
docker compose down -v

# ❌ Supprime tout Docker sur la machine
docker system prune -a --volumes
```

> Avec la config actuelle les données sont dans `./data/` (bind mount), donc `down -v` ne les affecte pas. Mais par prudence, évite quand même.

---

## 📁 Structure des données persistantes

```
data/
  devblog.sqlite      ← base de données principale (devlogs, users, jobs…)
  sessions.db         ← sessions d'authentification

uploads/              ← images uploadées via l'admin
```

Ces dossiers sont dans `.gitignore` — ils ne sont pas versionnés.

---

## 🔑 Variables d'environnement (`.env`)

| Variable | Description |
|---|---|
| `STEAM_API_KEY` | Clé API Steam (auth) |
| `SESSION_SECRET` | Secret pour les sessions HTTP |
| `ALLOWED_STEAM_IDS` | Steam IDs autorisés à accéder à l'admin (séparés par `,`) |
| `VITE_API_URL` | URL publique de l'API (laisser vide en local) |
| `OPENAI_API_KEY` | Clé OpenAI (optionnel, pour la traduction auto) |
