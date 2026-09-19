# Phase — Cours 2

`DayPhase.Class2` · durée par défaut **300 s** (5 min)

## But

Identique au [cours 1](03-cours-1.md) — **seules les différences sont notées ici**.
Le cœur du jeu. Une matière tirée au sort, dans sa salle, avec ses objets à elle. Il
faut **trouver la salle, y entrer, et y foutre le bordel sous le nez d'un adulte**.
La tension vient de l'enfermement : dans une salle, on est vu.

## Contenu

- La salle du cours s'ouvre au début de la phase et se referme à la fin.
- Ses items propres sont spawnés à l'ouverture, dépawnés à la fermeture
  (cf. [`../items.md`](../items.md) et [`../courses/README.md`](../courses/README.md)).
- Un **prof** occupe la salle : il regarde la classe, se retourne vers le tableau,
  et réagit au bruit. Le **surveillant** continue sa ronde dans les couloirs.
- Être dans le couloir pendant un cours est **suspect en soi** : le surveillant
  poursuit un élève hors salle même sans bêtise.

## Règles serveur

- Multiplicateur de réput' **×1,5** dans la salle du cours, **×1** ailleurs.
- L'objectif secondaire du jour (s'il y en a un) est actif ici
  (ex. voler un PC en techno).
- Une bêtise réussie rend *recherché* quelques secondes, comme aujourd'hui.

## Réseau

- **Pic de spawn au début de la phase** : les items du cours arrivent d'un coup.
  Les étaler sur quelques frames, et plafonner le nombre total.
- Chaque prefab d'item doit être dans `HoldMyBeerNetworkPrefabs.asset`, chez tout le
  monde, dans le même ordre. Un manquant = déconnexion sèche.
- Le prof est un `Supervisor` serveur-only avec une route réduite à la salle ; les
  clients n'en voient que le transform répliqué et l'état.
- La salle ouverte est un état (`NetworkVariable` sur la porte), pas un RPC : un
  joueur qui rejoint en plein cours doit la trouver ouverte.

## À implémenter

- [ ] Ouverture/fermeture forcée de la salle du cours par la phase.
- [ ] `CourseItemSpawner` (spawn étalé, despawn à la fermeture).
- [ ] Prof : `Supervisor` paramétré « salle » plutôt que « ronde ».
- [ ] Suspicion « élève hors salle pendant un cours ».

## Ce qui diffère du cours 1

- La matière est **forcément différente** de celle du cours 1 du même jour.
- Les joueurs collés au cours 1 sortent au **début** du cours 2 : c'est la
  « demi-journée » de colle (cf. [`../reputation-and-detention.md`](../reputation-and-detention.md)).
  Un collé délivré par ses potes, lui, revient tout de suite.
- Les adultes ont vu la matinée : si l'équipe a beaucoup chauffé, la vigilance monte
  (paramètre du palier de progression, pas une IA nouvelle).

## Ouvert

- Est-ce que rater complètement le cours (ne jamais y aller) doit coûter quelque
  chose ? Proposition : non, mais le multiplicateur ×1,5 suffit à rendre la salle
  attractive.
