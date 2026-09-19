# Phase — Récap

`DayPhase.Recap` · écran, pas de chrono (le host enchaîne)

## But

Montrer ce qui s'est passé, et donner envie de relancer. Le récap est le seul moment
où le jeu parle de progression : réput' par élève, total d'équipe, avancement du
quota du cycle, et ce qui se débloque si on le tient.

## Contenu

- Tableau par élève : réput' gagnée, bêtises marquantes, colles prises.
- Total d'équipe du jour, et cumul sur le cycle (jour 1/3, 2/3, 3/3).
- Au **jour 3** : verdict du cycle.
  - Quota atteint → cycle suivant, quota relevé, **palier de déblocage annoncé**
    (nouvelle matière, nouvel item, adulte de plus…). C'est la carotte, elle doit être
    lisible à l'écran.
  - Quota raté → retour au jour 1 du cycle. Jamais de game over.
- Un bouton « Jour suivant », **host seul**.

## Règles serveur

- Le récap ne calcule rien : il affiche `DayState`. Toute divergence viendrait d'un
  calcul client.
- `RequestNextDay()` reste host-only, avec vérification du `SenderClientId` côté
  serveur (c'est déjà le cas aujourd'hui).

## Réseau

- Tout ce qui est affiché est déjà répliqué (`NetworkList<StudentScore>`,
  `NetworkVariable` de quota/équipe/jour).
- Un joueur qui rejoint pendant le récap voit le récap, pas un écran vide.
- Le passage au jour suivant est un changement de phase serveur ; les clients suivent.

## À implémenter

- [ ] Écran de récap (UI par code, `UiFactory`).
- [ ] Verdict de cycle tous les trois jours, pas chaque jour.
- [ ] Affichage du palier débloqué (cf. [`../progression.md`](../progression.md)).

## Ouvert

- Une « bêtise du jour » élue (la plus chère, la plus vue) ? Pur bonus de présentation,
  mais c'est exactement ce qui fait raconter la partie.
