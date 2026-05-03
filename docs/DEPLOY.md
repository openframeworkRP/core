# Déploiement en production — OpenFramework Core

Ce guide couvre le passage d'un setup local à un serveur de jeu public accessible depuis Internet.

---

## Architecture cible

```
Internet
    │
    ▼
[Nginx / reverse proxy]  ← SSL/TLS (Let's Encrypt)
    │
    ├── https://ton-site.com         → website.frontend (port 4173)
    ├── https://ton-site.com/api/ws  → website.api (port 3001)
    └── (port 8443 fermé au public)  → core.api (accès gamemode uniquement)

[Serveur dédié s&box]
    └── réseau privé ou VPN → core.api:8443
```

> L'API `.NET` (`core.api`, port 8443) **ne doit pas être exposée publiquement**. Seul le serveur s&box dédié en a besoin — idéalement sur le même réseau privé ou via VPN.

---

## 1. Préparer le serveur

### Ports à ouvrir dans le firewall

| Port | Usage |
|------|-------|
| 80 | HTTP (redirect HTTPS) |
| 443 | HTTPS (site web) |
| 27015 | Serveur s&box dédié (UDP+TCP) |
| 8443 | API .NET — **accès restreint** (IP du serveur s&box uniquement) |

### Variables d'environnement à adapter

Dans ton `.env`, ajuste :

```env
# URLs publiques
FRONTEND_URL=https://ton-site.com
BACKEND_URL=https://ton-site.com

# En prod, le frontend est buildé — pas de hot reload
NODE_ENV=production

# L'URL interne de l'API (Docker réseau interne)
GAME_API_URL=http://core-api:8443
VITE_API_URL=https://ton-site.com
```

---

## 2. Reverse proxy Nginx

Exemple de config Nginx pour un domaine `ton-site.com` avec Let's Encrypt :

```nginx
server {
    listen 80;
    server_name ton-site.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name ton-site.com;

    ssl_certificate     /etc/letsencrypt/live/ton-site.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/ton-site.com/privkey.pem;
    ssl_protocols       TLSv1.2 TLSv1.3;
    ssl_ciphers         HIGH:!aNULL:!MD5;

    # Site web (frontend statique servi via le container)
    location / {
        proxy_pass http://localhost:4173;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # API website (Node.js)
    location /api/ {
        proxy_pass http://localhost:3001;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Uploads / fichiers statiques
    location /uploads/ {
        proxy_pass http://localhost:3001;
    }
}
```

### Obtenir un certificat Let's Encrypt

```bash
sudo apt install certbot python3-certbot-nginx
sudo certbot --nginx -d ton-site.com
```

Certbot renouvelle automatiquement le certificat via un cron.

---

## 3. Lancer en prod

```bash
docker compose up -d
```

Pour vérifier que tout tourne :
```bash
docker compose ps
docker compose logs -f
```

---

## 4. Sécuriser l'API .NET

Par défaut `core.api` écoute sur `0.0.0.0:8443`. En production, restreins l'accès :

**Option A — firewall (iptables/ufw) :**
```bash
# Autoriser uniquement l'IP du serveur s&box dédié
ufw allow from <IP_SERVEUR_SBOX> to any port 8443
ufw deny 8443
```

**Option B — bind sur une IP privée uniquement (docker-compose.yml) :**
```yaml
ports:
  - "10.0.0.1:8443:8443"   # Remplace 10.0.0.1 par ton IP privée
```

---

## 5. Backups

### Base PostgreSQL

```bash
# Dump complet (à planifier en cron)
docker compose exec postgres pg_dump -U postgres OpenFrameworkDb > backup_$(date +%Y%m%d).sql

# Restaurer
docker compose exec -T postgres psql -U postgres OpenFrameworkDb < backup_20260101.sql
```

Exemple de cron quotidien (`crontab -e`) :
```
0 3 * * * cd /opt/core && docker compose exec -T postgres pg_dump -U postgres OpenFrameworkDb > /backups/db_$(date +\%Y\%m\%d).sql
```

### Données website (SQLite + uploads)

Le SQLite du site est dans `website/data/devblog.sqlite`, les uploads dans `website/uploads/`. Ces dossiers sont des volumes montés — il suffit de les copier :

```bash
cp -r website/data /backups/website_data_$(date +%Y%m%d)
cp -r website/uploads /backups/website_uploads_$(date +%Y%m%d)
```

---

## 6. Mise à jour

```bash
git pull
docker compose pull          # Récupère les nouvelles images de base
docker compose up -d --build # Rebuild et relance
```

Les migrations EF Core et SQLite s'appliquent automatiquement au démarrage. Pas besoin d'intervention manuelle.

> **Recommandation** : fais un backup de la DB avant chaque mise à jour.

---

## 7. Monitoring

### Logs en temps réel

```bash
docker compose logs -f core.api       # API .NET
docker compose logs -f website.api    # API Node.js
docker compose logs -f postgres       # Base de données
```

### Santé des containers

```bash
docker compose ps
```

Tous les services ont `restart: unless-stopped` — ils redémarrent automatiquement si le container plante ou si la machine reboot.

### Alertes (optionnel)

Si tu veux être notifié en cas de panne, tu peux brancher [Uptime Kuma](https://github.com/louislam/uptime-kuma) (auto-hébergé) sur :
- `https://ton-site.com` — site web
- `http://localhost:8443/health` — API .NET (si endpoint de santé exposé)

---

## 8. Checklist avant de rendre le serveur public

- [ ] `.env` contient des secrets robustes (pas les valeurs par défaut)
- [ ] `SESSION_SECRET`, `JWT_KEY`, `SERVER_SECRET` générés aléatoirement (`openssl rand -hex 32`)
- [ ] Port 8443 restreint à l'IP du serveur s&box dédié uniquement
- [ ] SSL actif sur le domaine public
- [ ] `ALLOWED_STEAM_IDS` contient uniquement les admins de confiance
- [ ] Backups planifiés (DB + uploads)
- [ ] `VITE_MAINTENANCE=false` dans `.env`
- [ ] Gamemode publié avec l'identité du fork (pas `openframework.core`)
