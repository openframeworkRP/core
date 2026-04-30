# Contribuer à OpenFramework Core

Merci de l'intérêt ! Voici les conventions du projet.

## Langue

- **Français** pour le code, les commentaires, les commits, les issues et les PR.
- Garde la cohérence avec l'existant.

## Contraintes techniques non négociables

### Multijoueur serveur dédié

Toute feature **doit fonctionner en multijoueur sur serveur dédié** — pas seulement en listen server.

- Propriétés synchronisées : utiliser `[Sync(SyncFlags.FromHost)]` quand l'autorité est host-only.
- Mutations d'état serveur : toujours via `[Rpc.Host]`.
- `GameObject.Children` côté client ne retrouve pas toujours les enfants network-spawnés sur serveur dédié — préférer des refs explicites synced quand on a besoin d'atteindre un enfant depuis un client.
- Les bugs "invisibles" en listen server apparaissent souvent en dédié : valider mentalement que le client n'a pas d'autorité avant de proposer du code.

### Anti-duplication

Aucune manipulation d'item, d'argent ou de ressource ne doit permettre de dupliquer.

- Chaque chemin de transfert doit être audité : drag&drop, RPC, drop/pickup, save/load, swap, death/respawn, reconnexion.
- L'autorité host doit être stricte : détruire la source AVANT de créer la copie à destination.
- Privilégier des `[Rpc.Host]` atomiques avec logs pour tracer.

## Workflow

1. Fork le repo
2. Crée une branche depuis `master` : `git checkout -b feat/ma-feature`
3. Code (en respectant les contraintes ci-dessus)
4. Teste en serveur dédié si possible (au moins le golden path)
5. Commit avec un message descriptif (cf. ci-dessous)
6. Pousse et ouvre une PR contre `master`

## Style de commit

- Titre court mais parlant
- Corps détaillant : fichiers/composants touchés, comportement modifié, raison du changement
- Bullet points si plusieurs modifications
- Le message doit être auto-suffisant pour quelqu'un qui n'a pas participé à la session

Exemple :
```
fix(inventory): empecher la duplication lors du drag entre coffres

- Inventory.TransferRpc verifie maintenant la source avant de creer
  la copie cote destination (anciennement: copie creee puis source
  detruite -> fenetre de duplication possible si l'host crashait)
- Ajoute des logs sur le chemin critique pour debug en prod
```

## Issues

- Bug → titre `[BUG]`, étapes pour reproduire, comportement attendu vs réel, version de s&box
- Feature → titre `[FEAT]`, problème résolu, proposition de design

## Code review

- Au moins 1 review avant merge
- Tester localement si la PR touche au gameplay critique (inventaire, économie, auth)

## Questions ?

Ouvre une discussion sur le repo GitHub.
