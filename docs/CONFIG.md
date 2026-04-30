# Configuration

> ⚠️ Stub — à compléter au fil des PR.

Référence des ConVars et ConCmds du gamemode pour les hébergeurs.

## ConVars serveur

| Nom | Type | Default | Description |
|-----|------|---------|-------------|
| `core-api_server_secret` | string | `""` | Secret pour l'auth serveur API (= `SERVER_SECRET` du `.env`) |
| `whitelist_mod` | bool | `false` | Activer la whitelist : seuls les SteamIDs whitelistés peuvent rejoindre |
| `server_locked` | bool | `false` | Verrouiller le serveur : seuls les admins peuvent rejoindre |
| `core-discord_debug` | bool | `false` | Activer les logs de debug du bridge Discord |
| `core_debug_npc` | bool | `false` | Logs de debug NPC |
| `core_debug_appearance` | bool | `false` | Logs de debug RestoreAppearance/ApplyAppearance |
| `core_debug_morphs` | bool | `false` | Logs de debug pour la chaîne de morphs faciaux |
| `core_debug_hair` | bool | `false` | Logs de debug du système coiffeur |

## ConCmds serveur

| Nom | Description |
|-----|-------------|
| `core-connect_server` | Connecte le serveur a l'API (a appeler apres avoir set `core-api_server_secret`) |
| `core-save_positions` | Force la sauvegarde des positions de tous les joueurs |
| `core_dump_morph_names` | Dump la liste des morphs disponibles |

## Whitelist / admins

Pour l'instant les listes sont vides côté code (cf. `gamemode/Code/GameLoop/ServerManager.cs:WhitelistedSteamId` et `gamemode/Code/Systems/Pawn/Client.cs:AdminSteamIds`). Refacto prévue : chargement depuis un fichier de config externe.

En attendant, ajoute tes SteamID directement dans ces deux structures avant de publier ton gamemode.
