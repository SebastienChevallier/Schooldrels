# Design — index

Le design du jeu est découpé en un document par sujet, pour qu'on puisse en modifier
un sans relire le reste. La vue d'ensemble reste dans le spec :
`Documentation/specs/2026-09-19-school-day-design.md`, et le plan de dev dans
`Documentation/plans/2026-09-19-school-day-plan.md`.

## Journée

| Phase | Doc | Durée défaut |
|---|---|---|
| Arrivée | [`phases/01-arrivee.md`](phases/01-arrivee.md) | 20 s |
| Pause du matin | [`phases/02-pause-matin.md`](phases/02-pause-matin.md) | 90 s |
| Cours 1 | [`phases/03-cours-1.md`](phases/03-cours-1.md) | 300 s |
| Pause déjeuner | [`phases/04-dej.md`](phases/04-dej.md) | 420 s |
| Cours 2 | [`phases/05-cours-2.md`](phases/05-cours-2.md) | 300 s |
| Sortie | [`phases/06-sortie.md`](phases/06-sortie.md) | 90 s |
| Récap | [`phases/07-recap.md`](phases/07-recap.md) | écran |

## Systèmes

- [`courses/README.md`](courses/README.md) — le tirage des cours, ce qu'une matière apporte
  - [`courses/eps.md`](courses/eps.md) · [`courses/techno.md`](courses/techno.md) · [`courses/general.md`](courses/general.md) · [`courses/physique-chimie.md`](courses/physique-chimie.md)
- [`items.md`](items.md) — le catalogue d'objets : ce que chacun fait, ce qu'il rapporte, ce qu'il coûte au réseau
- [`rooms-and-keys.md`](rooms-and-keys.md) — les salles, leurs portes, les clés
- [`reputation-and-detention.md`](reputation-and-detention.md) — gagner de la réput', se faire prendre, la colle
- [`progression.md`](progression.md) — cycles de trois jours, quotas, paliers de déblocage

## Comment lire ces docs

Chaque doc suit la même trame : **But**, **Contenu**, **Règles serveur**, **Réseau**,
**À implémenter**, **Ouvert**. La section **Réseau** n'est jamais vide : sur ce projet,
tout est multijoueur d'abord (cf. `CLAUDE.md` §4).

Les valeurs chiffrées sont des **valeurs de départ**, à régler en jouant. Elles vivent
dans des ScriptableObjects, pas en dur dans le code.
