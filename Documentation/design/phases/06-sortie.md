# Phase — Sortie

`DayPhase.Dismissal` · durée par défaut **90 s** (1–2 min)

## But

La décompression, et le dernier filet de sécurité. Tout se referme, la journée est
presque scellée, et il reste juste assez de temps pour une dernière bêtise ou pour
aller délivrer un pote encore collé.

## Contenu

- Toutes les salles de cours se ferment ; leurs items sont dépawnés.
- Les adultes retournent vers l'entrée : la surveillance se concentre au portail.
- Les derniers collés sortent à la fin de la phase, quoi qu'il arrive.

## Règles serveur

- Multiplicateur de réput' **×0,5**, comme la pause du matin.
- Le despawn des items de cours est étalé, comme le spawn.
- À la fin de la phase, le score du jour est figé et la phase passe à `Recap`.

## Réseau

- Le dépawn de masse est le miroir du pic de spawn : l'étaler.
- Aucun score ne se calcule côté client : le récap lira l'état répliqué tel quel.

## À implémenter

- [ ] Fermeture + despawn étalé en fin de phase.
- [ ] Gel du score du jour côté serveur.

## Ouvert

- Une vraie « sortie » (franchir le portail) pour valider sa journée ? Tentant, mais
  ça punit le joueur déconnecté ou bloqué. Non pour le proto.
