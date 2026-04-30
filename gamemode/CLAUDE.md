# CLAUDE.md — core

Ce fichier est lu automatiquement par Claude Code quand tu travailles dans ce projet (peu importe la session, la machine ou apres redemarrage). Il contient les preferences et contraintes qui s'appliquent a TOUT developpement dans ce repo.

## Langue

- Repondre a l'utilisateur en francais.
- Code, commentaires, logs, messages de commit : francais egalement.

## Contraintes non negociables

### Multijoueur serveur dedie

Toute feature doit fonctionner en multijoueur sur serveur dedie — pas seulement en listen server.

- Proprietes synchronisees : utiliser `[Sync(SyncFlags.FromHost)]` quand l'autorite est host-only.
- Mutations d'etat serveur : toujours via `[Rpc.Host]`.
- `GameObject.Children` cote client ne retrouve pas toujours les enfants network-spawnes sur serveur dedie — preferer des refs explicites synced (ex: `SubContainerRef`) quand on a besoin d'atteindre un enfant depuis un client.
- Les bugs "invisibles" en listen server apparaissent souvent en dedie : valider mentalement que le client n'a pas d'autorite avant de proposer du code.

### Anti-duplication

Aucune manipulation d'item, d'argent ou de ressource ne doit permettre de dupliquer.

- Chaque chemin de transfert doit etre audit : drag&drop, RPC, drop/pickup, save/load, swap, death/respawn, reconnexion.
- L'autorite host doit etre stricte : detruire la source AVANT de creer la copie a destination.
- Privilegier des `[Rpc.Host]` atomiques avec logs pour tracer, plutot que des operations en plusieurs etapes client/serveur qui laissent une fenetre de duplication.

## Style de commit

Quand on push :

- NE PAS imiter le style court des commits existants (`git log`).
- Ecrire un message descriptif auto-suffisant pour les collegues qui n'ont pas participe a la session.
- Titre court mais parlant.
- Corps detaillant : fichiers/composants touches, comportement modifie, raison du changement.
- Bullet points si plusieurs modifications.

## Workflow

- Plutot que creer des fichiers .md de notes/plan temporaires, utiliser les outils de conversation (tasks/plan).
- Apres un changement UI/frontend, mentionner explicitement si la feature n'a pas ete testee en jeu (ne jamais pretendre "ca marche" sans test manuel).
