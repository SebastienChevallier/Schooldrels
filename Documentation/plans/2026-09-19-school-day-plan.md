# Journée de lycée — plan d'implémentation

**Spec de référence :** `Documentation/specs/2026-09-19-school-day-design.md`

**But :** remplacer la journée plate du proto par une journée en phases, des cours
tirés au sort dans des salles fermables, un self, une colle coopérative, et une
boucle infinie à paliers.

**Vérification :** ce projet n'a aucune infrastructure de test. Chaque lot se valide
par (1) compilation sans erreur, (2) `Tools > Hold My Beer > Play From Boot`,
(3) **deux clients** (Multiplayer Play Mode, Direct IP `127.0.0.1`) dès qu'il y a du
réseau — et jamais uniquement chez le host, qui a 0 ms de latence.

---

## Lot 0 — Nettoyage : la vidéo dégage

- [ ] Supprimer `UploadSpot`, `UploadRule`, la ligne correspondante dans `GameBootstrapper.BuildContainer()`, le marqueur WC dans `SchoolBuilder`/`NetworkPropSpawner`.
- [ ] `StudentScore` : `Pending` → `Reputation` (acquise à la bêtise), garder `Posted` un temps si le HUD s'en sert, sinon le retirer aussi.
- [ ] `DayState.Bank()` disparaît ; `HandleMischief` crédite directement `Reputation` et le total d'équipe.
- [ ] `SendToDetention` : perd **la moitié** de la réput' personnelle (au lieu de tout le `Pending`).

*Pourquoi d'abord :* tout le reste s'appuie sur le nouveau modèle de score. Le faire
après, c'est le refaire deux fois.

## Lot 1 — La machine à phases (cœur, et le plus risqué)

- [ ] `DayPhase` devient `Arrival, MorningBreak, Class1, Lunch, Class2, Dismissal, Recap` (+ `Loser` conservé le temps de la transition).
- [ ] `DaySchedule` (ScriptableObject ou struct sérialisée) : durée par phase.
- [ ] `DayState` : `NetworkVariable<byte> _phase` + `NetworkVariable<double> _phaseEndsAt`, avancement **serveur seul** dans `Update`, annonce à chaque bascule.
- [ ] `event Action<DayPhase> PhaseChanged` côté client, alimenté par `OnValueChanged`.
- [ ] HUD : phase courante + temps restant, calculé depuis `NetworkManager.ServerTime`.

**Point chaud :** aucun système ne doit dupliquer le chrono. Tout le monde lit
`DayState`. Un deuxième timer, c'est une désynchro garantie.

## Lot 2 — Catalogue de cours et salles

- [ ] `CourseDefinition` (ScriptableObject) : matière, `RoomId`, prefabs d'items, multiplicateur de réput'.
- [ ] `CourseCatalog` : la liste ordonnée — **l'index est ce qui transite**.
- [ ] `SchoolLayout` s'étend : une salle par `RoomId` (ancre, marqueurs d'items, porte).
- [ ] `DayState` tire deux cours distincts au début du jour (serveur), réplique deux `byte`.
- [ ] Greybox : ajouter gymnase, labo, salle techno, self, salle de colle dans `SchoolBuilder`. Vérifier que le NavMesh runtime les couvre.
- [ ] HUD : « Cours 1 : Physique-Chimie — Labo ».

## Lot 3 — Portes et clés

- [ ] `Door` (`NetworkBehaviour`) : `NetworkVariable<bool> _open`, `NetworkVariable<bool> _locked`, RPC d'intention `SendTo.Server`, vérification de clé **serveur**.
- [ ] `KeyItem` : un `GrabbableItem` avec un `RoomId` ; le voler publie un `MischiefReport`.
- [ ] `OpenDoorRule : IInteractionRule`, enregistrée **entre `GrabRule` et `ThrowRule`**.
- [ ] Tirage 80/20 par salle et par jour, serveur.
- [ ] La salle du cours en cours est forcée ouverte pendant sa phase.

## Lot 4 — Items de cours

- [ ] `CourseItemSpawner` serveur : spawn à l'ouverture de la phase, despawn à la fermeture, **étalé sur quelques frames**.
- [ ] Ajouter tous les prefabs à `HoldMyBeerNetworkPrefabs.asset` (un manquant = déconnexion sèche et message obscur).
- [ ] Une bêtise par famille d'items, via `PrankTarget` quand c'est possible (aucun code).

## Lot 5 — Le self

- [ ] Zone de file + `TrayItem` qui se remplit en avançant (serveur).
- [ ] `MenuOfTheDay` : tirage serveur, répliqué en index.
- [ ] **Nourriture lançable sans `NetworkRigidbody` continu** : impulsion répliquée par RPC, simulation locale, position de repos corrigée. Plafond dur d'objets vivants + despawn différé des restes.
- [ ] Mesurer : 4 joueurs, bataille pleine, regarder la bande passante et la frame du host.

## Lot 6 — La colle coopérative

- [ ] `DetentionRoom` : enfermement jusqu'à la fin de la phase de cours suivante, rappel par téléport serveur si on sort.
- [ ] Délivrance : ouvrir la porte de colle (clé/diversion) libère les collés.
- [ ] Le prof en salle pendant un cours : réutiliser `Supervisor` avec une route réduite à la salle.

## Lot 7 — Cycles et paliers

- [ ] Quota évalué **tous les trois jours**, pas chaque jour.
- [ ] `ProgressionTier` (liste ordonnée) : ce que chaque cycle franchi ajoute au catalogue / durcit.
- [ ] Écran de récap de journée et de fin de cycle, lisant l'état répliqué.
- [ ] Échec → retour au jour 1 du cycle, jamais de game over.

## Lot 8 — Objectifs secondaires

- [ ] `ISideObjective`, tiré avec le cours.
- [ ] Premier exemplaire : voler un PC en salle techno (item lourd + zone de sortie + discrétion).

---

## Points chauds, classés par risque

1. **La machine à phases** — tout en dépend, et une désynchro s'y voit partout.
   À faire en premier, à deux clients, avec une latence simulée.
2. **La bataille de nourriture** — le seul endroit du jeu qui peut faire tomber le
   host. Décider tôt du modèle de réplication des objets jetables ; ne pas découvrir
   le problème au Lot 5.
3. **Le volume de `NetworkObject`** — items de cours + nourriture + clés. Plafond et
   despawn sont des fonctionnalités, pas de l'optimisation tardive.
4. **La `NetworkPrefabsList`** — chaque nouveau prefab doit y être, chez tout le
   monde, dans le même ordre. C'est la première cause de « le client se déconnecte
   sans raison ».
5. **État vs événement** — phase, cours, portes, menu, scores, colle : **tout est
   `NetworkVariable`**. Un joueur qui rejoint à 14h doit voir la journée telle
   qu'elle est. Un RPC pour ça est un bug qui ne se voit qu'en partie réelle.
6. **Autorité** — porte, plateau, clé, sortie de colle : le client demande, le
   serveur décide, l'identité vient de `rpcParams.Receive.SenderClientId`.
7. **Le host est un joueur** — il est collable, il est dans la file du self, il voit
   sa propre bêtise avec 0 ms. Ne valider aucun ressenti chez lui.
8. **NavMesh** — les nouvelles salles doivent être sous la racine `School` pour que
   `RuntimeNavMeshBuilder` les prenne, sinon les adultes traversent ou se bloquent.

## Ordre conseillé

`Lot 0 → 1 → 2 → 3 → 4` donne déjà une journée jouable et testable à plusieurs.
`5` (self) et `6` (colle) sont les deux gros morceaux de fun, `7` referme la boucle,
`8` est du contenu.
