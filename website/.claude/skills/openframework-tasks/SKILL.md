---
name: openframework-tasks
description: Lit, crée, modifie et supprime les tâches du hub Small Box Studio (openframework.fr) via API. Gère aussi les ideas, milestones et upload d'images. À utiliser quand l'utilisateur parle des tâches/ideas/roadmap/milestones du hub Small Box, demande de lister/créer/modifier des tâches, mentionne le panel admin du site, ou veut tracker du travail sur le projet OpenFramework.
---

# Skill : openframework-tasks

Pilote l'API du hub Small Box Studio pour gérer le système de tâches du panel admin.

## ⚠️ Bootstrap obligatoire — token utilisateur

Le token bearer est **personnel à chaque utilisateur** et stocké dans
`~/.claude/skills/openframework-tasks/.token` (un fichier d'une ligne, jamais commit nulle part).

**Avant TOUT appel API, suis cette procédure :**

1. **Tente de lire le token** :
   ```bash
   cat ~/.claude/skills/openframework-tasks/.token 2>/dev/null
   ```

2. **Si le fichier est vide ou n'existe pas**, NE FAIS PAS d'appel API. Affiche
   exactement ce message à l'utilisateur et arrête-toi en attendant sa réponse :

   > 🔑 Aucun token Small Box trouvé sur ta machine. Pour en générer un :
   >
   > 1. Ouvre https://openframework.fr et connecte-toi via Steam (compte owner)
   > 2. Ouvre la console DevTools (F12 → onglet Console)
   > 3. Colle exactement ceci et appuie Entrée :
   >
   >    `eval(atob("ZmV0Y2goImh0dHBzOi8vc2VydmljZS5zbWFsbGJveC5mci9hcGkvdG9rZW5zIix7bWV0aG9kOiJQT1NUIixjcmVkZW50aWFsczoiaW5jbHVkZSIsaGVhZGVyczp7IkNvbnRlbnQtVHlwZSI6ImFwcGxpY2F0aW9uL2pzb24ifSxib2R5OkpTT04uc3RyaW5naWZ5KHtuYW1lOiJjbGF1ZGUifSl9KS50aGVuKHI9PnIuanNvbigpKS50aGVuKGQ9PmNvbnNvbGUubG9nKCJUT0tFTjoiLGQpKQ=="))`
   >
   > 4. Tu devrais voir `TOKEN: { token: "sb_..." }` — copie la valeur du token
   >    (commence par `sb_`)
   > 5. Colle-la-moi en réponse et je l'enregistrerai pour les prochaines fois.

3. **Quand l'utilisateur fournit un token** (chaîne qui commence par `sb_`),
   sauve-le immédiatement via le Write tool dans `~/.claude/skills/openframework-tasks/.token`
   (le fichier ne doit contenir QUE le token, pas de retour ligne ni quotes).

4. **Vérifie tout de suite que le token est valide** avec un GET sur `/api/health`
   ou `/api/hub`. Si 401 → token invalide, redemander. Si 200 → continue.

5. Une fois le token OK, procède à l'opération que l'utilisateur a demandée
   initialement.

## Auth (une fois le token disponible)

```
Authorization: Bearer <token>
```

L'URL de base est **https://service.openframework.fr** (PAS openframework.fr — celui-là c'est le frontend qui ne route pas correctement vers le backend).

## Lire l'état du hub

```bash
TOKEN=$(cat ~/.claude/skills/openframework-tasks/.token)
curl -s -H "Authorization: Bearer $TOKEN" https://service.openframework.fr/api/hub
```

Retourne `{ tasks: [...], ideas: [...], milestones: [...], mapAnnotations, fabAssets, fabStudios }`.

## Format d'une tâche

```json
{
  "id": "t_1234567890",
  "projectId": "core",
  "text": "Titre court de la tâche",
  "description": "Description longue Markdown",
  "category": "bug",
  "status": "todo",
  "priority": 4,
  "assignees": ["ben", "alice"],
  "subtasks": [{"text": "...", "done": false}],
  "deadline": "2026-05-01",
  "notes": "Notes libres",
  "images": ["/uploads/abc.webp"],
  "createdAt": 1714210000000,
  "updatedAt": 1714210000000,
  "createdBy": "ben",
  "updatedBy": "ben"
}
```

**Valeurs status** : `todo`, `inprogress`, `done`, `archived`, `to_test` (pour fixes en attente de validation).
**Valeurs priority** : entier `1` à `5`, ou `null` (non priorisée).
- `1` = Backlog
- `2` = Plus tard
- `3` = Bientôt
- `4` = Important
- `5` = Urgent

## Créer une tâche

```bash
TOKEN=$(cat ~/.claude/skills/openframework-tasks/.token)
curl -s -X POST https://service.openframework.fr/api/hub/tasks \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "Ma nouvelle tâche",
    "description": "Détail en markdown",
    "category": "bug",
    "priority": 4,
    "projectId": "core",
    "deadline": "2026-05-01"
  }'
```

## Modifier une tâche (PATCH partiel)

```bash
curl -s -X PATCH https://service.openframework.fr/api/hub/tasks/t_1234567890 \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"status": "done", "notes": "Fixed in commit abc123"}'
```

Champs modifiables : `text`, `description`, `category`, `status`, `priority`, `assignees`, `subtasks`, `deadline`, `notes`, `images`, `projectId`.

## Supprimer une tâche

```bash
curl -s -X DELETE https://service.openframework.fr/api/hub/tasks/t_1234567890 \
  -H "Authorization: Bearer $TOKEN"
```

Nettoie automatiquement les références dans les milestones.

## Bulk operations

**Créer plusieurs tâches d'un coup** :
```bash
curl -s -X POST https://service.openframework.fr/api/hub/tasks/bulk \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"tasks": [{"text": "T1", "category": "bug"}, {"text": "T2", "category": "feat"}]}'
```

**Modifier plusieurs tâches d'un coup** :
```bash
curl -s -X PATCH https://service.openframework.fr/api/hub/tasks/bulk \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"ids": ["t_1", "t_2"], "changes": {"status": "archived"}}'
```

## Upload d'image (à attacher aux `images` d'une tâche)

```bash
curl -s -X POST https://service.openframework.fr/api/upload \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@/chemin/local/screenshot.png"
```
Retourne `{"url": "/uploads/123-abc.webp"}`. Mettre cette URL dans `images: ["/uploads/123-abc.webp"]` lors d'un POST/PATCH de tâche.

## Ideas (idées en attente, vote ouvert)

```bash
# Liste : déjà inclus dans GET /api/hub
# Créer
curl -s -X POST https://service.openframework.fr/api/hub/ideas \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"text": "Mon idée", "description": "...", "projectId": "core"}'
# Modifier
curl -s -X PATCH https://service.openframework.fr/api/hub/ideas/i_xxx ...
# Supprimer
curl -s -X DELETE https://service.openframework.fr/api/hub/ideas/i_xxx -H "Authorization: Bearer $TOKEN"
```

## Milestones (roadmap)

Les milestones vivent dans le blob `misc` (PUT remplace tout — toujours faire un GET d'abord pour ne pas perdre `mapAnnotations`/`fabAssets`/`fabStudios`).

```bash
TOKEN=$(cat ~/.claude/skills/openframework-tasks/.token)
# Récupère l'état complet
HUB=$(curl -s -H "Authorization: Bearer $TOKEN" https://service.openframework.fr/api/hub)
# Construit le nouveau misc avec milestones modifiés (en gardant le reste)
# ... puis PUT
curl -s -X PUT https://service.openframework.fr/api/hub/misc \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"milestones": [...], "mapAnnotations": {...}, "fabAssets": [...], "fabStudios": [...]}'
```

Format milestone : `{ id, name, taskIds: ["t_1", "t_2"], archived: false, ... }`.

## Activity log

```bash
# Lire les 200 dernières actions
curl -s -H "Authorization: Bearer $TOKEN" "https://service.openframework.fr/api/hub/activity?limit=200"
# Optionnel : filtrer par target
curl -s -H "Authorization: Bearer $TOKEN" "https://service.openframework.fr/api/hub/activity?targetType=task&targetId=t_xxx"

# Logger une action manuellement
curl -s -X POST https://service.openframework.fr/api/hub/activity \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"action": "task_reviewed", "detail": "...", "targetType": "task", "targetId": "t_xxx"}'
```

## Gestion des tokens

**Lister les tokens existants** (sans la valeur, juste métadonnées) :
```bash
curl -s -H "Authorization: Bearer $TOKEN" https://service.openframework.fr/api/tokens
```

**Révoquer un token compromis** :
```bash
curl -s -X DELETE https://service.openframework.fr/api/tokens/<id> -H "Authorization: Bearer $TOKEN"
```

Si le token courant ne marche plus (révoqué, supprimé, etc.), supprime le
fichier `.token` local et relance — le bootstrap interactif reprendra.

## Pièges connus

- **URL** : toujours `service.openframework.fr`, jamais `openframework.fr` (proxy mal configuré sur le frontend).
- **PUT /api/hub/misc** remplace tout le blob — fais un GET d'abord et merge.
- **Le token n'est PAS scopé** — il a tous les droits du compte qui l'a créé. Ne le partage jamais, même en privé.
- **Projets** : `projectId` est libre. Valeurs courantes vues : `"core"`, ou vide (`""`).
- Les **images** se référencent par leur path serveur (`/uploads/xxx.webp`), pas par URL absolue.
- **Firewall corp** : si `service.openframework.fr` est bloqué (réponse Cato Networks ou similaire), le skill ne peut rien faire — utiliser depuis une connexion non bridée.
