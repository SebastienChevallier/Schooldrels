# Architecture — Hold My Beer

Complément de `CLAUDE.md` : ce document explique **pourquoi** chaque brique existe et
comment le flux se déroule bout en bout.

---

## 1. Flux complet, du lancement à la partie

```
   [Boot]
      │  GameBootstrapper.Awake()
      │    ├─ ServiceContainer
      │    ├─ SceneLoader, PlayerProfile
      │    ├─ découverte des ISessionTransport (DirectIp, Relay…)
      │    ├─ NetworkSessionService
      │    ├─ ConnectionApprovalHandler + policies
      │    └─ LobbyLauncher, PlayerSpawner
      ▼
   [Menu] ── écran MainMenu ──────────────────────────────┐
      │        │                                          │
      │   CREATE│                                     JOIN │
      │        ▼                                          ▼
      │  session.HostAsync()                     session.JoinAsync()
      │  → transport.ConfigureHostAsync()        → ConfigureClientAsync()
      │  → NetworkManager.StartHost()            → StartClient()
      │  → lobbyLauncher.SpawnLobby()                     │
      │        │                                          │
      │        │                    ┌── approbation serveur (policies)
      │        │                    │   ↳ refus → DisconnectReason → retour menu
      │        ▼                    ▼
      └── écran Lobby ◄──── LobbyNetworkService répliqué
               │
               │  host : START (si tous prêts)   /   clients : READY
               ▼
      StartMatchRpc (serveur)
               ├─ _isOpen = false        ← ferme la porte aux retardataires
               └─ LoadNetworkScene(Game) ← NetworkSceneManager, tout le monde suit
                        ▼
                     [Game]
                GameSceneBootstrap (serveur uniquement)
                   ↳ attend OnLoadEventCompleted
                   ↳ PlayerSpawner.SpawnAllConnectedPlayers()
                        ↳ pose fournie par ISpawnPointProvider
                        ↳ NetworkObject.SpawnAsPlayerObject(clientId)
                             ↳ chez le propriétaire : caméra + input actifs
                             ↳ chez les autres : transform répliqué
```

---

## 2. Décisions et justifications

### Un conteneur de services plutôt que des singletons
Chaque système singleton devient à terme un nœud de dépendances impossible à tester.
Ici, un seul point statique (`AppServices`) expose un conteneur ; tout le reste
dépend d'interfaces. C'est le compromis entre un vrai framework DI (VContainer) et le
`FindObjectOfType` généralisé. Migrer vers VContainer ne toucherait que
`GameBootstrapper`.

### Le transport derrière une stratégie
`Direct IP` et `Relay` n'ont ni les mêmes prérequis, ni la même UX (adresse IP contre
code d'invitation), ni la même fiabilité. Les enfermer dans un `if` aurait contaminé
l'UI et la session. `ISessionTransport` isole la seule chose qui diffère réellement :
**configurer le transport avant `StartHost`/`StartClient`**.

La découverte par `ISessionTransportInstaller` va plus loin : l'assembly Relay peut
être supprimée du projet sans qu'aucune autre ligne ne change. C'est ce qui permet de
livrer un build LAN sans dépendance à Unity Gaming Services.

L'interface a d'abord reçu un `UnityTransport` tout résolu, ce qui était une fuite
d'abstraction : ça supposait que tous les modes tournent sur le même composant. C'est
vrai de Direct IP et de Relay, mais faux dès qu'on parle de Steam sockets, qui sont un
`NetworkTransport` différent. L'installer reçoit donc le `NetworkManager`, et chaque
mode installe et active le composant dont il a besoin via `NetworkTransportActivator`.

### L'échec réseau est une valeur, pas une exception
`SessionResult` porte `Success` / `Error`. Une connexion échoue constamment et pour
des raisons banales (mauvaise IP, port fermé, session pleine, version différente) :
ce n'est pas exceptionnel, c'est un cas nominal que l'UI doit afficher.

### Le lobby est un objet réseau, pas un manager de scène
`LobbyNetworkService` est spawné dynamiquement par le host. Deux bénéfices :
il est répliqué automatiquement à chaque nouveau client (état complet, sans RPC de
synchronisation à écrire), et il survit au changement de scène.

`ILobbyProvider` existe parce que le lobby **n'existe pas au démarrage** : l'UI ne
peut pas le résoudre une fois pour toutes, elle s'abonne à son apparition.

### Le mouvement est séparé du réseau
`FirstPersonMotor` ne connaît ni NGO ni l'Input System : il transforme des entrées en
déplacement de `CharacterController`. `PlayerController` fait la colle
(propriétaire ? caméra ? curseur ?). Passer un jour en autorité serveur consiste à
appeler le même motor depuis le serveur avec des inputs répliqués — sans réécrire la
physique du joueur.

### L'UI est construite par code
Le projet a été écrit sans éditeur Unity disponible : une scène ou un prefab d'UI
écrit à la main hors éditeur est un fichier cassé (GUID inventés). L'UI générée par
`UiFactory` est du placeholder fonctionnel et sans conflit de merge. Les écrans
implémentent `IScreen` et ne parlent qu'à des interfaces de service : les remplacer
par des prefabs ou de l'UI Toolkit ne touche pas la logique.

### Les scènes et prefabs sont générés
`ProjectAssetGenerator` construit `Boot`, `Menu`, `Game`, le prefab joueur, le prefab
de lobby et la `NetworkPrefabsList`, puis renseigne les Build Settings. Même raison :
c'est reproductible et vérifiable. Une fois générés, ces assets sont des assets
ordinaires, à committer et à éditer normalement.

---

### Le ragdoll est cosmétique, la tension est l'état

Le corps ballotte parce que chaque os physique est tiré vers sa pose animée par un
drive en ressort, dont la raideur suit un scalaire unique `tension ∈ [0,1]`. À 1 le
corps est presque rigide, à 0 c'est un ragdoll complet — il n'y a donc pas de
transition à écrire entre « debout » et « effondré », seulement une valeur qui descend
puis remonte.

**Aucun os ne transite sur le réseau.** Chaque client simule le ragdoll de tous les
avatars, et les rigs divergent légèrement d'une machine à l'autre. Ce qui se réplique,
c'est la tension cible, un seul float écrit par le serveur. C'est ce qui rend la
feature quasi gratuite en bande passante, et c'est le principe dont découle tout le
reste — y compris la façon dont un objet tenu est attaché.

Deux conséquences non évidentes, découvertes à l'exécution et conservées ici parce
qu'elles se re-perdent facilement :

- Le rig physique vit **à la racine de la scène**, pas sous le joueur. Enfant du
  joueur, le mouvement de la racine s'ajouterait à la simulation et le corps partirait
  à l'infini. Le lien est un ressort, et c'est ce ressort qui produit le retard, donc
  le ballottement.
- Le bassin a besoin d'un **couple de redressement** en plus du ressort de position.
  Les joints ne contraignent que les rotations relatives : un corps parfaitement posé
  mais couché à plat les satisfait tous, et l'avatar reste par terre.

### Une action, un registre de règles

Il n'y a pas de touche « attraper » ni de touche « lancer ». Il y a une action, qui
traverse une liste ordonnée d'`IInteractionRule` : `GrabRule` prend, `ThrowRule` lance,
et les règles de jeu s'insèrent entre les deux. La première qui accepte gagne.

C'est délibérément la même forme que `IConnectionApprovalPolicy` : une classe, une
ligne dans `GameBootstrapper`, et ni le joueur, ni le réseau, ni l'IK ne bougent.
L'ordre d'enregistrement **est** la priorité — une règle placée après `ThrowRule` ne
s'exécutera jamais, puisque celle-ci accepte tout dès qu'on tient un objet.

La règle est coupée en deux : `CanApply` est pur et tourne aussi bien sur le client
(pour afficher « Remplir » sans aller-retour) que sur le serveur (pour choisir) ;
`Apply` est serveur seul. Une règle n'agit qu'à travers `IInteractionContext`, qui
borne tout ce qui vient du client — elle ne voit jamais `NetworkManager`.

### Le porteur vit sur l'objet, et l'attache est locale

`GrabbableItem` porte le `holder`. Le joueur ne réplique rien sur ce qu'il tient :
deux variables décrivant le même fait finiraient par diverger, et le bug serait
« le verre est dans ma main chez moi et par terre chez toi ».

Tant que l'objet est tenu, **chaque client le colle à sa propre main locale** et son
`NetworkTransform` est désactivé. C'est la conséquence directe du ragdoll non
répliqué : attaché à un os local, l'objet paraît soudé à la main partout, alors qu'une
position répliquée le ferait flotter à côté de la main de chacun.

Au lancer, le lanceur fournit sa pose et sa vélocité — le serveur ne peut pas les
connaître — et le serveur plafonne la magnitude. Même arbitrage que pour le mouvement.

### Une assembly de contrats pour les interactions

`HoldMyBeer.Interaction` ne contient que des interfaces et des structs, sans NGO et
sans `MonoBehaviour`. Elle existe parce que `Gameplay` et `Player` sont des frères :
le composant qui fait l'IK vit chez le joueur, les règles vivent dans le gameplay, et
aucun des deux ne doit voir l'autre. C'est le cas d'école de « il manque une interface
dans la couche du dessous ».

### L'IK pilote le rig animé, jamais le physique

`Animator.SetIKPosition` déplace la main animée ; les drives tirent le bras physique
derrière. Le ballottement du bras est donc gratuit, et il n'y a aucune animation de
prise à produire — seulement une cible qui bouge.

Deux pièges Unity, tous deux silencieux :

- L'**IK Pass** doit être activée sur la couche de l'`AnimatorController`, sinon les
  buts IK sont ignorés sans le moindre message.
- L'Animator doit être en **`AlwaysAnimate`**. Le mode par défaut,
  `CullUpdateTransforms`, désactive l'IK et l'écriture des transforms quand aucun
  renderer n'est visible — or le rig animé n'en a aucun, par construction, puisque le
  ragdoll porte le mesh.

## 3. Carte des fichiers

```
Assets/HoldMyBeer/
├── Runtime/
│   ├── Core/            aucune dépendance projet
│   │   ├── AppServices, ServiceContainer      accès aux services
│   │   ├── SceneLoader, CoroutineRunner       chargement local
│   │   ├── PlayerPrefsPlayerProfile           pseudo local + sanitisation
│   │   └── SceneNames                         constantes de scènes
│   ├── Networking/
│   │   ├── NetworkSessionService              cycle de vie de la session
│   │   ├── ISessionTransport (+ DirectIp)     stratégie de connexion
│   │   ├── TransportInstallerScanner          découverte des transports
│   │   ├── ConnectionApprovalHandler          portier serveur
│   │   ├── LobbyNetworkService, LobbyPlayer   lobby répliqué
│   │   ├── LobbyLauncher, LobbyProvider       création et exposition du lobby
│   │   └── Relay/                             assembly optionnelle
│   ├── Gameplay/
│   │   ├── PlayerSpawner                      spawn serveur
│   │   ├── SpawnPointRegistry                 marqueurs de scène
│   │   └── GameSceneBootstrap                 entrée de la scène Game
│   ├── Player/
│   │   ├── FirstPersonMotor                   maths pures, testables
│   │   ├── PlayerController                   colle réseau/caméra/input
│   │   ├── KeyboardMousePlayerInputSource     entrées
│   │   └── OwnerNetworkTransform              réplication owner-authoritative
│   ├── UI/
│   │   ├── UiFactory                          construction uGUI
│   │   ├── MainMenuScreen, LobbyScreen        écrans
│   │   └── MenuSceneController                orchestration de la scène Menu
│   └── App/
│       ├── GameBootstrapper                   composition root
│       └── LobbyOpenPolicyAdapter             règle d'entrée dépendant du lobby
└── Editor/
    ├── ProjectAssetGenerator                  génère scènes et prefabs
    └── ProjectSetupOnLoad                     exécution au premier import
```

---

## 4. Évolutions prévues et où les brancher

| Évolution | Point d'accroche |
|---|---|
| Serveur autoritaire | appeler `FirstPersonMotor` côté serveur, `OwnerNetworkTransform` → `NetworkTransform` |
| Reconnexion | clé stable dans `ConnectionPayload` + table `sessionId → clientId` dans le handler d'approbation |
| Lobby public / matchmaking | `com.unity.services.lobby` derrière un `ISessionTransport` + un service de listing |
| Chat, emotes | RPC sur `LobbyNetworkService` (événements, donc RPC et non NetworkVariable) |
| Vraie UI | remplacer `UiFactory` ; `IScreen` et les services ne bougent pas |
| Modes de jeu | un `IGameMode` résolu par `GameSceneBootstrap`, spawn délégué |
