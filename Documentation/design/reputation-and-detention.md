# Réputation, se faire prendre, et la colle

## La réput'

- Une bêtise rapporte de la réput' **immédiatement**. Il n'y a **plus rien à poster** :
  le système de vidéo (`UploadSpot` / `UploadRule`) est supprimé.
- La valeur dépend de l'item, de la cible, et du **multiplicateur de la phase** :

| Phase | Multiplicateur |
|---|---|
| Arrivée | ×0 |
| Pause du matin | ×0,5 |
| Cours (dans la salle du cours) | ×1,5 |
| Cours (ailleurs) | ×1 |
| Déjeuner | ×1 |
| Sortie | ×0,5 |

Le principe : **ça rapporte là où c'est dangereux**. Une bêtise gratuite doit valoir
peu, sinon le jeu optimal est de ne jamais prendre de risque.

- La réput' est à la fois **personnelle** (ce que je risque de perdre) et **d'équipe**
  (ce qui compte pour le quota). Les deux sont répliquées (`NetworkList<StudentScore>`
  + `NetworkVariable<int>`).

## Être recherché

Faire une bêtise rend *recherché* quelques secondes (`WantedUntil`, déjà en place).
Pendant ce temps, un adulte qui vous voit poursuit. Être dans le couloir **pendant un
cours** est également suspect, sans bêtise.

## Se faire prendre en flagrant délit

Être vu **au moment** de la bêtise : la bêtise est **annulée**, rien de plus. Pas de
réput', pas de sanction. C'est le raté ordinaire, il doit rester léger.

## La colle

Se faire **attraper** par un adulte :

1. L'élève part en **salle de colle** (bureau du CPE).
2. Il y reste **la demi-journée** : jusqu'au début de la phase de cours suivante.
   Attrapé pendant le cours 1 ou le dej → sort au début du cours 2. Attrapé pendant le
   cours 2 → sort à la sortie.
3. Il perd **la moitié de la réput' qu'il a personnellement récoltée**.

### Les potes peuvent délivrer

C'est le levier coopératif du jeu, et la raison pour laquelle la colle n'est pas une
mort sèche : ouvrir la porte de la salle de colle (avec une clé, ou en attirant le CPE
ailleurs) **libère tous les collés immédiatement**, sans perte supplémentaire.

Aller chercher un pote coûte du temps et du risque à toute l'équipe. C'est le choix
intéressant : est-ce que je vais le sortir, ou est-ce que je continue à marquer ?

## Règles serveur

- L'enfermement est une **position serveur** : sortir de la salle déclenche un téléport
  de rappel. Ce n'est jamais une UI qui bloque, jamais un input désactivé.
- La demi-perte est calculée serveur, à l'instant de la capture.
- La libération est un effet de l'état de la porte, pas une action dédiée.

## Réseau

- « Collé jusqu'à » est un **état** (`NetworkVariable` / champ de `StudentScore`) avec
  une échéance en `ServerTime` : un joueur qui rejoint doit voir qui est collé.
- Le rappel par téléport passe par `ITeleportable`, comme `SendToDetention` aujourd'hui.
- **Limite connue du proto** : un joueur en ragdoll n'est pas téléporté correctement
  (la racine suit le bassin) — c'est déjà noté dans `CLAUDE.md` §8, à traiter avec la
  colle.
- Le host est un joueur : il est collable, et il ne doit avoir aucune échappatoire
  qu'un client n'a pas.

## À implémenter

- [ ] Suppression de `UploadSpot` / `UploadRule` ; `StudentScore.Pending` → `Reputation`.
- [ ] Multiplicateur de réput' par phase.
- [ ] Enfermement à durée de phase + rappel par téléport.
- [ ] Demi-perte à la capture.
- [ ] Libération par ouverture de la porte de colle.

## Ouvert

- Une deuxième colle dans la même journée coûte-t-elle plus cher ? Probablement oui
  (la moitié de ce qui reste, donc ça décroît naturellement).
- Un adulte qui vous connaît déjà vous surveille-t-il davantage ? Bonne idée de palier
  de progression, pas du proto.
