# Configuration — ConVars & ConCmds

Référence complète des variables de configuration et commandes console du gamemode OpenFramework.

---

## Comment définir une ConVar sur un serveur dédié

Dans le fichier de configuration de votre serveur s&box (ex: `addons.txt` ou équivalent), ou directement via la console serveur :

```
core-api_server_secret "mon_secret_ici"
whitelist_mod true
```

---

## Configuration serveur — à définir en production

Ces variables doivent être configurées avant de lancer le serveur en conditions réelles.

| ConVar | Type | Défaut | Description |
|--------|------|--------|-------------|
| `core-api_server_secret` | `string` | `""` | Secret partagé entre le gamemode et l'API REST. Doit correspondre à `SERVER_SECRET` dans le `.env` du backend. **Obligatoire** pour que l'authentification serveur fonctionne. |
| `whitelist_mod` | `bool` | `false` | Active le mode whitelist : seuls les SteamIDs ajoutés via le panel admin peuvent rejoindre. |
| `server_locked` | `bool` | `false` | Verrouille le serveur : les nouveaux joueurs sont bloqués à la connexion. Les joueurs déjà connectés ne sont pas affectés. |

---

## Intégrations Discord

| ConVar | Type | Défaut | Description |
|--------|------|--------|-------------|
| `core-anticheat_webhook_url` | `string` | `""` | URL du webhook Discord pour les rapports anti-cheat. À chaque déconnexion, un résumé de session est envoyé (gains, pertes, transactions). Marqué `SUSPECT` si > 5 000 $/min, `CRITIQUE` si > 20 000 $/min. Désactivé si vide. |
| `core-feedback_webhook_url` | `string` | `""` | URL du webhook Discord pour les feedbacks joueurs soumis depuis le Pause Menu. L'onglet Feedback est désactivé (bouton grisé) si cette variable est vide. |

---

## Wiki

| ConVar | Type | Défaut | Description |
|--------|------|--------|-------------|
| `core-wiki_api_url` | `string` | `http://localhost:3001/api/wiki` | URL de base de l'API wiki (website backend Node.js, port 3001). Si s&box bloque l'URL (non whitelistée), le gamemode rebascule automatiquement sur `localhost:3001`. En prod, pointer vers l'URL publique de votre website backend. |

---

## Personnalisation du Pause Menu

| ConVar | Type | Défaut | Description |
|--------|------|--------|-------------|
| `core-pausemenu_logo_url` | `string` | `""` | Chemin vers l'image du logo affiché en haut à gauche du Pause Menu (ex: `ui/mon_logo.png`). Aucun logo affiché si vide. |
| `core-map_image_url` | `string` | `""` | Chemin vers l'image de fond de la carte (bouton Map + vue complète). Si vide, utilise l'image fournie par le composant `MinimapRenderer` de la scène. |

---

## Variables client (côté joueur)

Ces variables sont définissables par chaque joueur dans sa console.

| ConVar | Type | Défaut | Description |
|--------|------|--------|-------------|
| `cl_drawhud` | `bool` | `true` | Affiche ou masque le HUD en jeu. |
| `hc1_hide_flashbang_overlay` | `bool` | `false` | Masque l'overlay de flashbang. |

---

## Debug — à ne pas activer en production

Ces variables génèrent des logs verbeux. Elles sont utiles en développement mais doivent rester à `false` en prod.

| ConVar | Type | Défaut | Description |
|--------|------|--------|-------------|
| `core-discord_debug` | `bool` | `false` | Logs de debug du bridge Discord (requêtes, réponses). |
| `core_debug_npc` | `bool` | `false` | Logs de debug du système NPC (comportements, nœuds, interactions). |
| `core_debug_appearance` | `bool` | `false` | Logs de debug pour `RestoreAppearance` / `ApplyAppearance` (tenues, sync). |
| `core_debug_morphs` | `bool` | `true`* | Logs de debug pour la chaîne de morphs faciaux (hydratation, sync, application). *Activé par défaut — à désactiver en prod. |
| `core_debug_hair` | `bool` | `false` | Logs de debug du système coiffeur (RPC, sync, application de teinte). |
| `inventory_debug` | `bool` | `false` | Logs de debug de l'inventaire (opérations, grille, drag & drop). |
| `pickup_debug` | `bool` | `false` | Logs de debug du système de ramassage d'objets. |
| `culling_debug` | `bool` | `false` | Logs de debug des zones de culling local. |
| `shopsign_debug` | `bool` | `false` | Logs de debug des enseignes de boutique (`ShopSign`). |
| `radio_debug` | `bool` | `false` | Logs de debug du système radio (fréquences, portée). |
| `creator_debug` | `bool` | `false` | Logs de debug du gestionnaire de personnages (écran de création). |
| `hc1_debug` | `bool` | `false` | Affiche l'overlay de debug texte en jeu. |
| `hc1_editor_volumes` | `bool` | `true` | Affiche les volumes dans l'éditeur s&box. Sans effet en jeu. |
| `hc1_bot_follow` | `bool` | `false` | Les bots suivent les inputs du joueur hôte. Dev uniquement. |

---

## ConCmds serveur

Commandes à exécuter dans la console serveur (flag `Server` — inaccessibles côté client).

| ConCmd | Description |
|--------|-------------|
| `core-connect_server` | Connecte le gamemode à l'API REST avec le secret configuré (`core-api_server_secret`). Appelé automatiquement au démarrage si le secret est présent. |
| `core-save_positions` | Force la sauvegarde des positions de tous les joueurs connectés vers l'API. |

## ConCmds de debug

Commandes utilitaires pour le développement. Ignorées en production.

| ConCmd | Description |
|--------|-------------|
| `core_dump_morph_names` | Affiche dans les logs la liste complète des morphs faciaux disponibles sur le modèle joueur. |
| `ui_dump` | Dump l'état des panels UI actifs dans les logs. Accepte un label optionnel. |
| `cam_info` | Affiche les informations de la caméra du gestionnaire de personnages. |
| `bag_debug [0\|1]` | Active/désactive les logs de debug des sacs de l'inventaire. |
| `test_weight_speed` | Simule la progression du poids pour tester l'impact sur la vitesse de déplacement. |
| `test_weight_speed_stop` | Arrête la simulation de poids. |
| `rfs_*` | Famille de commandes de spawn pour tester les éléments du système de cuisine (ingrédients, stations, équipements). Ex: `rfs_beef`, `rfs_fryer`, `rfs_kit`. |

---

## Whitelist et admins

Les listes de SteamIDs sont actuellement définies directement dans le code :

- **Whitelist** : `gamemode/Code/GameLoop/ServerManager.cs` → `WhitelistedSteamId`
- **Admins** : `gamemode/Code/Systems/Pawn/Client.cs` → `AdminSteamIds`

En prod, utilisez le panel admin web pour gérer la whitelist via l'API plutôt que d'éditer le code.
