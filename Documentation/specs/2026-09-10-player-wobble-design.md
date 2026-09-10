# Wobble physique du joueur — design

Date : 2026-09-10
Statut : validé, prêt à planifier

## But

Donner au joueur un corps qui ballotte, à la manière de PEAK, et qui peut
s'effondrer en ragdoll complet puis se relever. Le joueur voit son propre torse,
ses bras et ses jambes ballotter sous lui en vue FPS, et voit les corps des
autres joueurs ballotter autour de lui.

Hors périmètre : l'ivresse comme modificateur d'amplitude, la préhension physique
d'objets, l'escalade. Le design ne les empêche pas — la tension est le point
d'entrée naturel pour les brancher plus tard.

## Approche retenue : la tension unique

Le rig Mixamo est doublé de `Rigidbody` + `ConfigurableJoint`, chaque joint tiré
vers sa pose d'animation par un drive en ressort. Un scalaire unique
`tension ∈ [0,1]` module la raideur de tous les drives :

- `tension = 1` — corps presque rigide, légèrement mou : le ballottement continu.
- `tension = 0` — ragdoll total : l'effondrement.

Il n'y a donc pas de transition à écrire entre les deux modes. Descendre la
tension à 0 en `collapseDuration` *est* l'effondrement ; la remonter en
`recoverDuration` *est* le relevé. Les deux déclencheurs demandés (mouvement
continu, effondrement ponctuel) sont une seule feature, pas deux.

### Alternatives écartées

**Deux systèmes séparés** (jiggle analytique pour le continu + ragdoll Unity
classique pour l'effondrement). Chaque moitié est plus simple, mais la couture
entre les deux est un *switch* visible, et le relevé demande un blend
ragdoll → animation qui reste du code fragile à maintenir.

**Asset Store** (Active Ragdoll Physics-Based Character Controller et
équivalents). Rapide à voir tourner, mais boîte noire qui se cogne au découpage
en asmdef, à la DI et à NGO — précisément ce qui rend ce projet maintenable.

## Structure de scène

Il y a **deux squelettes**, et le squelette physique **n'est pas enfant du joueur**.

```
Player (root, NetworkObject, OwnerNetworkTransform, CharacterController)
├── CameraPivot / PlayerCamera
├── AnimatedRig      ← squelette Mixamo, kinematic, piloté par l'Animator, aucun renderer
└── PlayerRagdollState / WobbleRig

PlayerRagdoll_<clientId>   ← racine de la scène, DontDestroyOnLoad, PAS enfant de Player
└── mixamorig:Hips ... (Rigidbody + Collider + ConfigurableJoint par os)
    └── SkinnedMeshRenderer d'Y Bot bindé sur CE squelette
```

**Pourquoi deux squelettes.** Un seul ne peut pas être écrit à la fois par
l'Animator et par la physique : ils se battraient sur les mêmes `Transform`.
L'`AnimatedRig` ne coûte que des transforms, aucun mesh.

**Pourquoi le rig physique n'est pas enfant du joueur.** S'il l'était, le
mouvement de la racine s'ajouterait à la simulation physique et le corps
partirait à l'infini au premier pas. Le lien entre les deux est un ressort, pas
une parenté : le bassin physique est tiré vers la position de la racine.
**Ce ressort est ce qui produit le retard, donc le wobble.** Trop raide, le
personnage est rigide ; trop mou, il traîne par terre.

## Composants

Tout vit dans `HoldMyBeer.Player`, sous `Runtime/Player/Wobble/`. Aucun asmdef à
modifier : `HoldMyBeer.Editor` référence déjà `HoldMyBeer.Player`.

| Type | Nature | Rôle |
|---|---|---|
| `WobbleSettings` | struct sérialisable | valeurs à régler, calqué sur `PlayerMovementSettings` |
| `WobbleSolver` | classe pure | maths de la tension, aucune dépendance Unity au-delà de `Vector3`/`Quaternion` |
| `WobbleBone` | classe | un os physique ; **seul** endroit qui touche l'API `ConfigurableJoint` |
| `WobbleRig` | `MonoBehaviour` | possède les os, applique la tension reçue ; ne décide de rien, ignore le réseau |
| `PlayerRagdollState` | `NetworkBehaviour` | `NetworkVariable<float>` de tension, `CollapseRpc` ; seul fichier de la feature qui connaît NGO |
| `PlayerRagdollBuilder` | éditeur | génère rigidbodies, colliders et joints depuis les noms d'os Mixamo |

`WobbleSettings` expose : `idleTension`, `springAtFullTension`, `damper`,
`maxForce`, `collapseDuration`, `recoverDuration`, `pelvisFollowSpring`,
`pelvisFollowDamper`, `maxBoneSpeed`, `maxBoneAngularSpeed`, `watchdogDistance`,
`collapseTensionThreshold`, avec un `Default` statique éditable par variante de prefab.

Sens de dépendance : `PlayerRagdollState` → `WobbleRig` → `WobbleBone` → Unity
Physics, avec `WobbleSolver` isolé sur le côté. Rien ne remonte. `WobbleRig` est
utilisable sans réseau, donc jouable dans une scène vide.

`WobbleSolver` est une classe pure pour la même raison que `FirstPersonMotor` :
le jour où le mouvement passe en autorité serveur, elle ne bouge pas.

`PlayerRagdollBuilder` s'ajoute à côté de `ProjectAssetGenerator`, sous une entrée
`Tools > Hold My Beer > Rebuild Player Ragdoll`. Le Ragdoll Wizard d'Unity fait
l'essentiel du travail, mais à la main et sans reproductibilité — or ce projet a
pour règle que ses assets se régénèrent.

## Vue première personne

Y Bot est un mesh unique : il n'y a **pas** de rig de bras FPS séparé à gérer.
Chez l'owner, on met l'échelle de l'os `mixamorig:Head` à ~0 au lieu de masquer
le corps entier. La caméra étant déjà sur `CameraPivot` à hauteur de tête, le
joueur voit son propre torse, ses bras et ses jambes ballotter sous lui.

Cela **remplace** le `firstPersonHiddenVisual` actuel de `PlayerController`, qui
éteint tout le corps.

## Réseau

**Aucun os ne transite sur le réseau.** Le ragdoll est simulé localement chez
chaque client, pour tous les avatars. Les rigs divergeront légèrement d'une
machine à l'autre : sans importance, c'est cosmétique.

Ce qui se réplique :

- la racine, déjà répliquée par `OwnerNetworkTransform` — inchangé ;
- un `NetworkVariable<float>` de tension cible, **écriture serveur seule**.

**Tension haute.** `FirstPersonMotor` bouge le `CharacterController` exactement
comme aujourd'hui. Le rig physique court derrière en ballottant. Rien de
l'existant ne change.

**Effondrement.** Le gameplay appelle `CollapseRpc(SendTo.Server)` → le serveur
écrit `targetTension = 0` → tous les clients interpolent leur tension locale sur
`collapseDuration`. Chez l'owner uniquement, sous un seuil, le
`CharacterController` se désactive et la racine suit la position du bassin
physique ; `OwnerNetworkTransform` continue de répliquer, donc les autres voient
la chute au bon endroit. Au relevé, la tension remonte, la racine se recale sur
le bassin projeté au sol, le `CharacterController` se rallume.

**Autorité — arbitrage assumé.** Le serveur est seul à écrire la tension,
conformément à la règle du projet. Mais c'est l'owner qui *détecte* l'atterrissage
violent et demande l'effondrement par RPC. C'est cohérent avec l'exception déjà
assumée dans `CLAUDE.md` pour le mouvement owner-authoritative : un client
malveillant peut refuser de tomber, exactement comme il peut déjà mentir sur sa
position. Il n'y a pas de raison d'être plus strict ici que sur la position.

## Cycle de vie

- `PlayerRagdollState.OnNetworkSpawn` instancie le rig physique ;
  `OnNetworkDespawn` le détruit. Sans le second, chaque joueur qui quitte laisse
  un cadavre physique qui continue de simuler.
- Le rig est mis en `DontDestroyOnLoad`, symétriquement à son propriétaire : le
  `Player` est spawné dynamiquement et survit au passage `Menu → Game`, un rig
  créé dans la scène `Game` ne survivrait pas et laisserait une référence nulle.
- Le rig est bâti **après** que `PlayerSpawner` ait posé la racine, et tous les os
  téléportés sur la pose animée (`SnapToAnimatedPose()`) avant activation de la
  physique. Sinon le ragdoll naît à l'origine du monde et se fait catapulter au
  premier `FixedUpdate`. Concrètement : `PlayerSpawner` doit positionner le
  `Player` **avant** d'appeler `Spawn()`, pour qu'`OnNetworkSpawn` voie déjà la
  bonne position. **Vérifié : c'est déjà le cas** — `PlayerSpawner` fait
  `Instantiate(prefab, pose.position, pose.rotation)` avant `SpawnAsPlayerObject()`.
  Rien à corriger, mais la contrainte est à préserver si ce code évolue.

À `tension = 0`, l'`AnimatedRig` continue de tourner dans le vide : la physique
l'ignore, puisque les drives sont à zéro. C'est voulu — c'est ce qui rend le
relevé gratuit, la pose cible est déjà là quand la tension remonte.

## Garde-fous

- **Clamp de vitesse** angulaire et linéaire par os. Un `ConfigurableJoint` mal
  réglé produit des NaN, et un NaN contamine tout le rig en un frame.
- **Watchdog** : si la distance bassin ↔ racine dépasse un seuil, resnap sur la
  pose animée. Ça arrivera — changement de scène, `timeScale` à 0, pause
  debugger. Sans watchdog, le seul recours est de relancer la partie.
- **Échec bruyant en éditeur** : si `PlayerRagdollBuilder` ne trouve pas un os attendu
  (rig renommé, Mixamo réexporté), il s'arrête en nommant l'os manquant, au build
  du prefab et non au runtime. Même logique que les scènes qui signalent
  l'absence de bootstrap dans la console.
- **Rigidbodies en `Interpolate`** : les joints tournent en `FixedUpdate`, le
  rendu en `Update` ; sans interpolation le personnage saccade à 50 Hz et on
  croira à un problème de réseau.

## Layers de collision

Une layer `PlayerRagdoll`, décochée contre elle-même dans la matrice de collision
— sinon les os d'un même corps se percutent et le personnage convulse. Décochée
aussi contre la layer du joueur, sinon le rig physique se cogne au
`CharacterController` qui le tracte.

## Piège d'implémentation à connaître

`ConfigurableJoint.targetRotation` s'exprime dans l'espace du joint **au moment
de sa configuration**, pas dans l'espace local courant. La conversion est
`Quaternion.Inverse(cible) * rotationDeReposCapturée`. L'oublier donne un
personnage tordu en permanence — c'est invariablement le premier bug de ce genre
de système.

## Prérequis

Le prefab réseau `Prefabs/Player.prefab` porte encore un `Visual` en capsule
(`MeshFilter` + `MeshRenderer` nus). Le rig Mixamo vit dans
`_ART/Player/Player.prefab` et **n'est pas encore branché** sur le prefab réseau.
Ce branchement est un préalable au wobble : c'est ce squelette qui va ballotter.

## Tests

**Décision : pas de tests pour l'instant.** Le projet n'a aujourd'hui aucune
infrastructure de test — `com.unity.test-framework` est dans le manifest, mais il
n'existe ni asmdef ni fichier de test. Introduire cette convention n'est pas le
sujet de cette feature.

Ce qui serait testable le jour venu, c'est `WobbleSolver` et lui seul : rampes de
tension, bornes 0 et 1, absence de dépassement, stabilité à `deltaTime` variable.
Il reste une classe pure précisément pour que ce soit possible sans rien
refactorer.

Le reste — ressorts, joints, ballottement — ne se valide pas en assert. Ça se
valide à l'œil, en Play mode, à deux clients, et jamais uniquement chez le host
qui a 0 ms de latence.

## Références

- [How does character controller work in PEAK game?](https://discussions.unity.com/t/how-does-character-controller-work-in-peak-game/1683399) — le fil qui établit que PEAK est un ragdoll *passif* traîné, pas un active ragdoll, et que l'active ragdoll serait coûteux à synchroniser en réseau.
- [Wobbly Physics and Ragdoll for Character](https://discussions.unity.com/t/wobbly-physics-and-ragdoll-for-character/1517960) — réglage du « trop mou » par `positionSpring` et masse des rigidbodies.
- [Active Ragdolls?](https://discussions.unity.com/t/active-ragdolls/871034) — `ConfigurableJoint` + `targetRotation` + drives en ressort, et la layer de collision décochée contre elle-même.
