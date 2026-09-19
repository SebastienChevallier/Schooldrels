# Cours — Physique-Chimie

**Salle :** labo · **Multiplicateur :** ×1,5 (le plus haut potentiel de chaos du jeu)

## Ce que la matière apporte

Les **réactions** : des bêtises qui ne se contentent pas de faire du bruit, mais qui
produisent un effet qui dure et qui se propage. C'est le cours où une seule action
bien placée peut renverser toute la phase.

## Items

| Item | Ce qu'il fait |
|---|---|
| **Fiole / bécher** | contient un réactif. Se lance, se verse, se mélange. |
| **Réactifs (A, B, C)** | pris séparément : rien. Mélangés : fumée, mousse, odeur, petite explosion. |
| **Pissette à eau** | jet à distance : éteint, mouille, déclenche une réaction sur un réactif posé. Rechargeable à l'évier. |
| **Bec / plaque chauffante** | source de chaleur fixe : chauffer une fiole = réaction. |

Détail des valeurs : [`../items.md`](../items.md).

## Les réactions

Le principe : **une réaction est un couple (contenu, cible)**, donc exactement une
`IInteractionRule` de plus — la forme documentée dans `CLAUDE.md` §5 avec l'exemple du
verre et de la tireuse. Aucune couche nouvelle.

| Couple | Effet |
|---|---|
| Réactif A + réactif B | nuage de fumée : coupe la vue des adultes dans un rayon |
| Réactif + chaleur | petite explosion : gros bruit, attire tout le monde |
| Pissette + réactif posé | déclenche à distance ce qu'on a préparé avant |
| Fiole lancée sur un adulte | le met hors-jeu quelques secondes (aveuglé, pas blessé) |

La fumée est le seul effet du jeu qui **modifie les sens d'un adulte** : c'est
puissant, donc c'est borné dans le temps et dans l'espace, et c'est côté serveur.

## Contrainte spatiale

Labo étroit, paillasses hautes, une seule porte. Facile de préparer quelque chose,
difficile de sortir. C'est la salle qui punit le plus l'improvisation.

## Réseau

- **Toute réaction est serveur.** Un client qui déciderait qu'il y a de la fumée chez
  lui verrait un jeu différent de celui des autres.
- La fumée est un **état à durée** (`NetworkVariable` avec un `endsAt` en `ServerTime`),
  pas un RPC : un retardataire doit voir le nuage encore actif.
- L'effet visuel est local, l'effet sur la vue des adultes est serveur : ne jamais
  dériver la logique du particle system.
- Attention au volume : une réaction en chaîne peut spawner beaucoup d'objets.
  Mêmes règles que la bataille de bouffe — plafond et despawn.

## Ouvert

- Combien de réactifs ? Trois au départ, c'est déjà six couples ; au-delà, personne
  ne retient.
- Les réactions doivent-elles pouvoir blesser un joueur ? Non : pas de dégâts dans ce
  jeu, uniquement du chaos et de la gêne.
