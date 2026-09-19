# Journée de lycée, cours aléatoires, self et boucle infinie — design

Date : 2026-09-19
Statut : validé, à planifier
Remplace : la boucle « réput' à poster aux toilettes » du proto (`UploadSpot`, `UploadRule`)

## But

Passer d'une journée plate (un chrono, un quota, poster aux WC) à une **journée
rythmée en phases**, avec des cours tirés au sort, des salles fermées à clé, un
self, et une boucle **infinie** dont la difficulté et le contenu s'ouvrent au fil
des quotas. Le jeu ne doit jamais « se terminer » : il doit donner une raison de
relancer un cycle.

---

## 1. Le déroulé d'une journée

```
Arrivée          Pause          Cours 1          Dej            Cours 2          Sortie        Récap
(entrée en jeu)  1–2 min        5 min            5–10 min       5 min            1–2 min       écran
   │                │              │                │              │                │            │
   └── spawn ───────┴── libre ─────┴── salle X ─────┴── self ──────┴── salle Y ─────┴── libre ───┘
```

| Phase | Durée (défaut, réglable) | Ce qui la caractérise |
|---|---|---|
| `Arrival` | 20 s | spawn, HUD d'accueil, rien n'est encore ouvert |
| `MorningBreak` | 90 s | couloir libre, repérage, les salles de cours sont fermées |
| `Class1` | 300 s | une matière tirée au sort, dans sa salle ; il faut la trouver et y aller |
| `Lunch` | 420 s | self ouvert, file, plateaux, bataille de nourriture |
| `Class2` | 300 s | une autre matière (jamais la même que Class1 le même jour) |
| `Dismissal` | 90 s | tout se referme, retour libre |
| `Recap` | écran | bilan du jour, réput' par élève, progression vers le quota |

Une phase est un **état serveur répliqué** : `DayPhase` devient cette machine à
états, et son horloge (`_endsAt`) est déjà répliquée — c'est le même mécanisme,
étendu. Les clients ne font que lire la phase et son temps restant.

Règle : **rien n'est jamais bloqué par une phase.** Une phase change ce qui est
ouvert, ce qui rapporte, et qui surveille quoi — jamais les entrées du joueur.

### Ce que la phase pilote

- **Ouverture des salles** : la salle du cours en cours est ouverte, le reste est
  fermé (voir §3).
- **Position des adultes** : pendant un cours, le prof est *dans* la salle du cours
  et le surveillant patrouille les couloirs — être dans le couloir pendant un cours
  est en soi suspect. Pendant le dej, tout le monde converge vers le self.
- **Multiplicateur de réput'** : une bêtise pendant un cours vaut plus que la même
  pendant une pause. C'est ce qui pousse à jouer là où c'est dangereux.

---

## 2. Les cours

Chaque cours est tiré au sort dans le catalogue, avec une contrainte : les cours
généraux partagent **une** salle assignée au début du cycle et qui ne change plus.

| Matière | Salle | Items propres | Potentiel |
|---|---|---|---|
| EPS | gymnase | balle, ballon, sifflet | lancers, chaos de volume |
| Techno | salle techno | PC, Arduino | faire bugger un poste ; **voler un PC** (objectif secondaire) |
| Français / Maths / Philo | salle générale (fixe) | craie, effaceur, sarbacane artisanale | discrétion, tirs à distance |
| Physique-Chimie | labo | matériel de chimie, pissette | réactions, fumées, dégâts en chaîne |

Modélisation : un `CourseDefinition` (ScriptableObject) = matière, salle, liste de
prefabs d'items à faire apparaître, multiplicateur de réput'. Le serveur tire, puis
réplique l'**index** du cours (un `byte`), jamais l'objet : les catalogues sont
identiques chez tout le monde puisque le build l'est.

Les items d'un cours sont **spawnés par le serveur à l'ouverture de la salle** et
dépawnés à sa fermeture. Ça borne le nombre d'objets réseau vivants, et ça donne au
cours son identité sans dupliquer des salles.

### Le vol de PC (et les objectifs secondaires)

Un objectif secondaire est une bêtise à condition : *sortir un PC de la salle techno
sans être vu*. Il ne passe pas par une nouvelle mécanique — c'est un item lourd
(ralentit, visible dans les mains) plus une zone de sortie. Il publie un
`MischiefReport` comme le reste. La forme générale : `ISideObjective`, tirée au sort
avec le cours.

---

## 3. Les salles fermées à clé

Hors cours, une salle est fermée **80 % du temps** (tirage par salle et par jour,
côté serveur, répliqué). Fermée ≠ inaccessible :

- la **clé** existe quelque part (trousseau du surveillant, loge, bureau du CPE) ;
- la voler est une bêtise en soi, et rend *recherché* ;
- une salle ouverte le reste jusqu'à la fin de la phase.

Autorité : l'état d'une porte est une `NetworkVariable<bool>` sur la porte ; le
client **demande** l'ouverture, le serveur vérifie la clé et applique. Jamais
l'inverse — sinon n'importe qui ouvre tout.

---

## 4. La pause dej

Self classique : une file, un plateau qui se remplit tout seul en avançant, pas de
choix. Le **menu du jour est tiré au sort** comme le reste. Chaque élément du
plateau est un objet lançable : purée, pomme, plateau lui-même, avec la même charge
au maintien du clic que tout le reste du jeu. Les batailles de nourriture ne sont
pas une mécanique à écrire — ce sont les objets existants, en quantité, dans une
pièce fermée, avec des adultes qui regardent.

Point technique : la bataille de bouffe est le **pire cas réseau du jeu** (des
dizaines de rigidbodies répliqués). Traité en §7.

---

## 5. Se faire prendre — la colle

La vidéo à poster **disparaît**. La réput' est acquise au moment de la bêtise.

- Se faire prendre **en flagrant délit** annule la bêtise en cours, rien de plus.
- Prendre une **colle** : l'élève est enfermé en salle de colle **pour la
  demi-journée** (jusqu'à la fin de la phase de cours suivante), et perd **la moitié
  de la réput' qu'il a personnellement récoltée**.
- Les **potes peuvent venir le délivrer** : ouvrir la salle de colle (clé, ou
  diversion) libère le collé. C'est le levier coopératif du jeu, et la raison pour
  laquelle la colle n'est pas une mort sèche.

Conséquence sur le code : `UploadSpot` / `UploadRule` sont supprimés, `StudentScore.Pending`
devient simplement `Reputation` (acquise), et `SendToDetention` applique la demi-perte
plus un enfermement à durée de phase.

---

## 6. La boucle infinie

- Le quota s'évalue **tous les trois jours de cours**, pas chaque jour.
- Quota atteint → cycle suivant, quota plus haut, difficulté en hausse.
- Quota raté → retour au premier jour du cycle (pas de game over définitif).
- À **chaque cycle franchi**, le jeu ouvre quelque chose : une matière de plus au
  tirage, un item nouveau, un adulte supplémentaire, une salle. C'est la promesse de
  nouveauté qui retient les joueurs.

La progression est **par session**, pas persistante (pas de sauvegarde, cf. CLAUDE.md
§8). Un cycle = un palier de déblocage, décrit par un `ProgressionTier`
(ScriptableObject ordonné) : ce qui s'ajoute au catalogue, ce qui se durcit.

---

## 7. Points chauds réseau (à ne jamais perdre de vue)

| Sujet | Risque | Décision |
|---|---|---|
| **Machine à phases** | deux sources de vérité (client qui anticipe la sonnerie) | phase + `endsAt` en `NetworkVariable`, côté serveur seul ; le client n'affiche que du temps restant calculé depuis `ServerTime` |
| **Tirage aléatoire** | un client qui tire de son côté n'a pas la même salle | le serveur tire, réplique un index ; jamais de `Random` non seedé côté client sur du gameplay |
| **Bataille de nourriture** | des dizaines de `NetworkObject` physiques : bande passante et CPU du host | plafond dur d'objets vivants, dépawn différé des restes, et objets de nourriture **sans** `NetworkRigidbody` continu — impulsion répliquée par RPC puis simulation locale, seule la position de repos est corrigée |
| **Portes** | un client qui écrit son propre état de porte | `NetworkVariable` serveur, RPC d'intention, clé vérifiée côté serveur |
| **Spawn/despawn par phase** | pic de spawn à chaque changement de phase | spawn étalé sur quelques frames, et prefabs tous listés dans `HoldMyBeerNetworkPrefabs.asset` (un manquant = déconnexion sèche) |
| **Colle** | un client qui sort tout seul de la salle de colle | l'enfermement est une position serveur + téléport de rappel, pas une UI |
| **Rejoindre en cours de journée** | un retardataire ne voit pas l'état | tout ce qui est état (phase, cours, portes, scores, menu du jour) est `NetworkVariable`/`NetworkList` — **jamais** un RPC |
| **Host = joueur** | un code « serveur » qui suppose pas de joueur local | comme partout : `IsServer` est vrai chez un joueur |
| **Récap** | écran local divergent | le récap lit l'état répliqué, il ne recalcule rien |

---

## 8. Ce qui ne change pas

L'architecture reste celle de `CLAUDE.md` : règles d'interaction ordonnées entre
`GrabRule` et `ThrowRule`, `MischiefBus` pour le fan-out des bêtises, adultes
serveur-only sur NavMesh, UI construite par code. Aucune de ces mécaniques ne
demande une couche nouvelle — elles demandent des **données** (catalogues) et une
**machine à phases**.
