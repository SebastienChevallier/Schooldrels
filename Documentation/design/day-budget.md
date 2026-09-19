# Budget de temps d'une journée

**Décision : une journée de cours dure 15 à 20 minutes.** C'est l'unité que le joueur
ressent — on dit « on fait une journée », pas « on fait un cycle ». Le cycle de trois
jours fait donc 45 min à 1 h : c'est l'engagement d'une soirée entre potes, assumé.

## Les deux réglages

| Phase | Nerveux (~16 min) | Confort (~20 min) |
|---|---|---|
| Arrivée | 20 s | 20 s |
| Pause du matin | 60 s | 90 s |
| Cours 1 | 270 s | 300 s |
| Déjeuner | 300 s | 420 s |
| Cours 2 | 270 s | 300 s |
| Sortie | 60 s | 90 s |
| **Total** | **16 min 20** | **20 min 20** |

Le réglage « confort » est celui des durées d'origine du design ; le réglage
« nerveux » est le plancher en dessous duquel une phase n'a plus le temps d'exister
(trouver la salle, entrer, agir, sortir).

## Ce qui est intouchable

- **Un cours ne descend pas sous 4 min.** En dessous, trouver la salle mange la phase
  entière et il ne reste plus de jeu.
- **Le déjeuner ne descend pas sous 5 min.** La bataille de bouffe a besoin d'un temps
  de montée : file, plateaux, premier jet, chaos.
- **Les pauses ne montent pas.** Ce sont des respirations, pas des phases de jeu ; si
  elles s'allongent, le jeu optimal devient d'attendre.

Si le rythme d'une partie doit changer, on **règle les phases**, on ne touche pas au
nombre de jours du cycle : trois jours est ce qui donne au quota sa marge de rattrapage
(cf. [`progression.md`](progression.md)).

## Implémentation

Les durées vivent dans une `DaySchedule` (ScriptableObject), pas en dur dans
`DayState`. Deux assets — `Schedule_Nervous`, `Schedule_Comfort` — permettent de
basculer sans recompiler et de comparer en playtest.

## Réseau

La durée n'est **jamais** lue côté client comme une vérité : le client affiche
`endsAt - ServerTime.Time`, et `endsAt` est une `NetworkVariable` posée par le serveur
au début de chaque phase. Changer de `DaySchedule` ne change donc rien côté client —
c'est exactement la propriété qu'on veut pour pouvoir régler en jouant.
