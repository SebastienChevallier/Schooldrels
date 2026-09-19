# Hold My Beer — guide de développement

Jeu multijoueur **peer-to-peer hébergé par un joueur** (pas de serveur dédié), Unity **6000.6** / URP,
**Netcode for GameObjects 2.x** + **Unity Transport**, avec **Unity Relay** en option.

Ce document est la référence pour tout développement multijoueur sur ce projet.
Lis-le avant d'ajouter la moindre fonctionnalité réseau.

---

## 1. Démarrage

```
1. Ouvrir le projet dans Unity 6000.6 (les packages se résolvent au premier import).
2. Les scènes et prefabs se génèrent automatiquement au premier lancement.
   Sinon : Tools > Hold My Beer > Generate Project Assets
3. Jouer : Tools > Hold My Beer > Play From Boot
```

**Toujours entrer en Play depuis la scène `Boot`.** Les autres scènes détectent
l'absence de bootstrap et le signalent dans la console au lieu de planter en silence.

Tester à plusieurs sur une machine : `Window > Multiplayer > Multiplayer Play Mode`
(virtual players), ou un build + l'éditeur, en mode **Direct IP** sur `127.0.0.1`.

### Tester sur deux réseaux différents

Direct IP ne passe pas d'un réseau à l'autre sans ouvrir un port. Trois options,
de la plus propre à la plus rapide :

1. **Relay** (le mode prévu pour ça). Une fois : `Project Settings > Services` →
   lier le projet à une organisation UGS, puis activer **Relay** dans le dashboard
   Unity. Ensuite, dans le menu : *Switch mode* → Relay → **CREATE** ; le lobby
   affiche un code, *COPY CODE* le met dans le presse-papier. L'autre joueur colle
   le code et fait **JOIN**.
2. **VPN maillé** (Tailscale, ZeroTier). Les deux machines se retrouvent sur un
   même réseau virtuel : on reste en **Direct IP** avec l'IP fournie par le VPN,
   sans compte UGS ni dashboard. Très pratique pour débugger, puisque c'est le
   même chemin de code qu'en LAN.
3. **Port forwarding** : le host ouvre l'UDP `7777` sur sa box et partage son IP
   publique. Ça marche, mais ça dépend de la box et ça n'est pas demandable à un
   joueur.

**Les deux machines doivent lancer le même build.** `BuildVersionPolicy` refuse
les versions différentes, et surtout la `NetworkPrefabsList` est hashée : deux
commits différents = déconnexion à l'approbation.

---

## 2. Topologie : host-client, pas de serveur dédié

Un joueur lance `StartHost()` : son process est **serveur *et* client** en même temps.
Les autres font `StartClient()`.

```
        ┌──────────────────────────────┐
        │  Process du HOST             │
        │  ┌────────┐    ┌──────────┐  │
        │  │ Server │◄──►│ Client 0 │  │   ← autorité
        │  └───┬────┘    └──────────┘  │
        └──────┼───────────────────────┘
               │ UDP direct  ou  Relay
        ┌──────┴────────┬───────────────┐
   ┌────▼─────┐   ┌─────▼────┐    ┌─────▼────┐
   │ Client 1 │   │ Client 2 │    │ Client N │
   └──────────┘   └──────────┘    └──────────┘
```

Conséquences à ne jamais oublier :

- `IsServer` est **vrai chez le host**, qui est aussi un joueur. Un code serveur qui
  suppose « pas de joueur local » est faux ici.
- Si le host quitte, **la session meurt**. C'est assumé (pas de host migration).
- Le host a 0 ms de latence : ne jamais valider un ressenti de jeu uniquement chez lui.

### Les deux modes de connexion

| Mode | Quand | Prérequis |
|---|---|---|
| **Direct IP** | LAN, dev quotidien, tests solo sur `127.0.0.1` | aucun |
| **Relay** | jouer entre amis via internet, sans ouvrir de port | projet lié à Unity Gaming Services (offre gratuite) |

Le code de jeu **ne sait pas** lequel est actif : il ne parle qu'à `INetworkSessionService`.

---

## 3. Architecture

### Scènes

| Scène | Rôle | Chargée par |
|---|---|---|
| `Boot` | composition root : construit tous les services, puis passe au menu | démarrage |
| `Menu` | menu principal **et** lobby (deux écrans, une seule scène) | `SceneLoader` (local) |
| `Game` | terrain + points de spawn | `NetworkSceneManager` (**réseau**) |

> Menu et Lobby partagent une scène : le lobby n'a aucun contenu 3D, et éviter un
> chargement de scène pendant la poignée de main réseau supprime toute une classe de
> bugs de timing.

### Assemblies (`.asmdef`) — le sens des dépendances

```
                 HoldMyBeer.App          ← composition root, seul à connaître les types concrets
                  │   │   │   │
      ┌───────────┘   │   │   └────────────┐
      ▼               ▼   ▼                ▼
HoldMyBeer.UI   HoldMyBeer.Gameplay   HoldMyBeer.Player
      │               │   │                 │
      │               │   └─────────┬───────┘
      │               │             ▼
      │               │    HoldMyBeer.Interaction  ← contrats seuls, ni NGO ni MonoBehaviour
      └───────┬───────┘
              ▼
     HoldMyBeer.Networking ──► HoldMyBeer.Networking.Relay (optionnel, supprimable)
              │
              ▼
        HoldMyBeer.Core        ← aucune dépendance projet
```

Règle : **les flèches ne remontent jamais.** Si tu as besoin de l'inverse, c'est qu'il
manque une interface dans la couche du dessous.

### Composition root

`GameBootstrapper` (scène `Boot`) est le **seul** endroit qui fait `new` sur des types
concrets. Tout le reste reçoit des interfaces via `AppServices.Container`.

```csharp
// Dans un MonoBehaviour, une seule fois, dans Start/Awake — jamais dans Update :
var session = AppServices.Container.Resolve<INetworkSessionService>();
```

---

## 4. Les règles du multijoueur sur ce projet

### 4.1 Autorité

**Le serveur décide, le client demande.** Un client n'écrit jamais un état partagé.

```csharp
// ✅ le client exprime une intention
[Rpc(SendTo.Server)]
private void SetReadyRpc(bool ready, RpcParams rpcParams = default)
{
    var sender = rpcParams.Receive.SenderClientId;   // ← identité fiable, fournie par NGO
    ApplyReady(sender, ready);                        // ← le serveur applique
}
```

```csharp
// ❌ ne jamais faire confiance à un id envoyé par le client
[Rpc(SendTo.Server)]
private void SetReadyRpc(ulong clientId, bool ready) { /* n'importe qui usurpe n'importe qui */ }
```

Toujours vérifier l'autorité côté serveur, même pour une action « réservée au host » :
`if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;`

**Exception assumée : le mouvement du joueur.** `OwnerNetworkTransform` est
*owner-authoritative* — chacun simule son avatar et réplique le résultat. C'est
réactif, simple, et trichable. Acceptable entre amis ; à remplacer par un motor
serveur + réconciliation si le jeu devient compétitif. Le calcul de mouvement est
déjà isolé dans `FirstPersonMotor`, précisément pour rendre ce basculement local.

### 4.2 Choisir son outil de réplication

| Besoin | Outil | Coût |
|---|---|---|
| Valeur qui change et que les nouveaux arrivants doivent connaître (score, état) | `NetworkVariable<T>` | réplique l'état, synchro à la connexion |
| Collection répliquée (liste de joueurs) | `NetworkList<T>` | idem, `T` doit être `unmanaged` |
| Événement ponctuel (tir, son, effet) | `[Rpc(...)]` | rien n'est stocké, un retardataire ne le verra jamais |

Piège classique : envoyer un RPC pour synchroniser un état. Un client qui se connecte
après ne le recevra pas. **État → NetworkVariable. Événement → RPC.**

### 4.3 Types réplicables

`NetworkList<T>` / `NetworkVariable<T>` exigent des types **unmanaged** :
pas de `string`, pas de classe. Utiliser `FixedString32Bytes` (29 octets utiles !)
et implémenter `INetworkSerializable` **et** `IEquatable<T>` — voir `LobbyPlayer`.

### 4.4 Cycle de vie

Sur un `NetworkBehaviour`, ne jamais utiliser `Start()` pour de la logique réseau :

```csharp
public override void OnNetworkSpawn()    // ← ici l'objet est réplicable, IsOwner/IsServer sont fiables
public override void OnNetworkDespawn()  // ← se désabonner ici, systématiquement
```

Objets **spawnés dynamiquement** (`Spawn()`) : survivent au changement de scène
(NGO les place en DontDestroyOnLoad). C'est pour ça que `LobbyState` reste vivant du
menu jusqu'au jeu. Objets **placés dans une scène** : détruits avec elle.

### 4.5 Prefabs réseau

Tout prefab spawné doit :
1. porter un `NetworkObject` ;
2. être listé dans `Prefabs/HoldMyBeerNetworkPrefabs.asset` ;
3. être **identique** chez tout le monde — la liste est ordonnée et hashée.

Un prefab manquant côté client = déconnexion sèche avec un message obscur.

### 4.6 Connexion et approbation

Toute connexion passe par `ConnectionApprovalHandler`, qui applique des
`IConnectionApprovalPolicy`. Ajouter une règle = ajouter une classe, sans toucher au
reste (`MaxPlayersPolicy`, `BuildVersionPolicy`, `LobbyOpenPolicyAdapter`).

Le payload de connexion est **plafonné à 1024 octets** par défaut et n'est **pas de
confiance** : il est re-nettoyé côté serveur (`PlayerPrefsPlayerProfile.Sanitize`).

### 4.7 Changement de scène

En session, **jamais** `SceneManager.LoadScene`. Le serveur appelle :

```csharp
AppServices.Container.Resolve<INetworkSessionService>().LoadNetworkScene(SceneNames.Game);
```

Tous les clients suivent. Le spawn des joueurs attend `OnLoadEventCompleted`, sinon on
spawne pour des clients qui n'ont pas encore la scène.

---

## 5. Ajouter une fonctionnalité — recettes

**Un nouveau mode de transport (Steam, LAN discovery…)**
1. Ajouter la valeur dans l'enum `SessionMode`.
2. Implémenter `ISessionTransport`. Le transport **possède** son composant : il
   appelle `NetworkTransportActivator.Activate<TonTransport>(networkManager)` dans
   `ConfigureHostAsync` / `ConfigureClientAsync`, ce qui l'ajoute si besoin et le
   rend actif. Obligatoire même si tu réutilises `UnityTransport` : la tentative
   précédente a pu laisser un autre mode en place.
3. Implémenter `ISessionTransportInstaller` dans la même assembly.
4. Rien d'autre : le bootstrap le découvre par réflexion, la session et l'UI ne
   bougent pas.

> Un transport Steam n'est **pas** un `UnityTransport` configuré, c'est un autre
> composant `NetworkTransport`. C'est pour ça que l'installer reçoit le
> `NetworkManager` et non un transport déjà résolu.

**Une nouvelle règle d'entrée** → une classe `IConnectionApprovalPolicy`, ajoutée dans
`GameBootstrapper.BuildContainer()`.

**Une nouvelle interaction contextuelle** (viser une tireuse avec un verre vide le
remplit, viser un évier le vide…) → une classe `IInteractionRule`, ajoutée dans
`GameBootstrapper.BuildContainer()` **entre `GrabRule` et `ThrowRule`**.

```csharp
public sealed class FillGlassRule : IInteractionRule
{
    // Pur : tourne aussi sur le client, pour l'invite à l'écran. Aucun effet de bord.
    public bool CanApply(in InteractionRequest request, out string prompt)
    {
        prompt = "Remplir";
        return request.HeldItem is EmptyGlass && request.AimedTarget is BeerTap { IsAvailable: true };
    }

    // Serveur seul. Le contexte est la seule porte de sortie : pas de NetworkManager ici.
    public void Apply(in InteractionRequest request, IInteractionContext context) { /* … */ }
}
```

L'ordre **est** la priorité. `ThrowRule` accepte tout dès qu'on tient un objet : une
règle enregistrée après elle ne s'exécutera jamais. Il n'y a rien pour t'en avertir.

`IsCharged` décide du moment de déclenchement : `false` part au clic, `true` démarre une
charge et part au relâchement, avec `InteractionRequest.Charge` rempli entre 0 et 1.
C'est la règle qui le dit, pas le joueur — une future règle « tirer une pinte en
maintenant » hérite du comportement sans qu'on touche à `PlayerInteractor`.

L'invite affichée vient de `CanApply`, publiée dans `IInteractionPrompt` et lue par
`CrosshairHud`. L'UI ne connaît ni les joueurs, ni les règles, ni NGO.

**Un objet de jeu répliqué** → prefab + `NetworkObject`, ajouté à la
`NetworkPrefabsList`, spawné **par le serveur** avec `.Spawn()`.

**Une nouvelle bêtise sur un objet fixe** (alarme, tableau…) → aucun code : un prefab
`PrankTarget` réglé (réput', rayon de bruit, 0 = silencieux, cooldown) dans `SchoolPrefabs`,
ajouté à la liste réseau et placé dans `NetworkPropSpawner`. `PrankRule` les gère tous.
Une bêtise avec une logique propre (le pétard) publie un `MischiefReport` sur le `MischiefBus` :
`DayState` le crédite, les `Supervisor` l'entendent, sans se connaître.

**La boucle de journée** (cible, voir `Documentation/specs/2026-09-19-school-day-design.md`) :
la journée est une **machine à phases** répliquée — arrivée, pause, cours 1, dej, cours 2,
sortie, récap. Chaque cours est tiré au sort par le **serveur** dans un catalogue et
réplique un *index*, jamais l'objet : EPS, Techno, cours généraux (salle fixe pour le
cycle), physique-chimie, chacun avec ses items propres spawnés à l'ouverture de la salle
et dépawnés à sa fermeture. Hors cours, une salle est fermée à clé 80 % du temps — une
porte est une `NetworkVariable` serveur, le client **demande** et le serveur vérifie la clé.
Faire une bêtise donne de la réput' immédiatement et rend *recherché* quelques secondes ;
se faire voir annule la bêtise en cours, se faire **coller** enferme en salle de colle
pour la demi-journée et coûte la moitié de la réput' personnelle — les potes peuvent venir
délivrer. Le quota s'évalue **tous les trois jours** : atteint → cycle suivant, plus dur,
avec un palier de contenu débloqué ; raté → retour au jour 1. Jamais de game over.

> Il n'y a plus de vidéo à poster : `UploadSpot`/`UploadRule` sont à supprimer (lot 0 du
> plan `Documentation/plans/2026-09-19-school-day-plan.md`).

**Une politique de spawn différente** → implémenter `ISpawnPointProvider` ;
`PlayerSpawner` n'a pas à changer.

---

## 6. Conventions de code

- **SOLID**, appliqué avec discernement : une interface quand il y a une vraie raison
  de substituer (transport, spawn, input), pas par réflexe.
- `private` par défaut, `readonly` dès que possible, `sealed` sur les classes non
  destinées à l'héritage.
- Champs privés : `_camelCase`. Champs `[SerializeField]` : `camelCase`.
- Une classe = un fichier, du même nom.
- Pas de singleton, à l'exception d'`AppServices` (documentée dans le fichier) et de
  `NetworkManager.Singleton` (imposé par NGO).
- Pas de `Resolve<T>()` ni de `GetComponent` dans `Update`.
- Les commentaires expliquent **pourquoi**, jamais **quoi**.

---

## 7. Pièges déjà rencontrés (et traités)

| Symptôme | Cause | Où c'est géré |
|---|---|---|
| « No cameras rendering » | scènes générées vides | caméras créées dans `Boot`/`Menu` |
| Le client rejoint mais reste bloqué | `StartClient()` ne signale pas l'échec d'approbation | `OnClientStopped` + `DisconnectReason` |
| Le lobby disparaît en lançant la partie | objet de scène détruit au load | lobby **spawné dynamiquement** |
| Un joueur n'a pas d'avatar | spawn avant que sa scène soit prête | attente d'`OnLoadEventCompleted` |
| La souris tourne 10× trop vite | `Mouse.delta` multiplié par `deltaTime` | non multiplié dans `KeyboardMousePlayerInputSource` |
| Le host démarre mais personne ne rejoint | pare-feu / port 7777 fermé | passer en mode **Relay** |
| `FixedString32Bytes` non résolu | l'assembly qui lit un champ répliqué doit référencer `Unity.Collections` | ajouté aux `.asmdef` concernés |
| API Relay introuvable (`AllocationUtils`, `RelayServerData`) | ces helpers bougent d'une version de transport à l'autre | `SetRelayServerData` en octets bruts, stable depuis NGO 1.x |
| `manifest.json` : *Duplicate key found* | le fichier a été réordonné à la main, puis le Package Manager a réécrit **son** bloc sans reconnaître l'ancien | ne jamais retrier `manifest.json` : éditer une valeur sur place, laisser l'ordre d'Unity |
| L'avatar reste couché alors que la tension vaut 1 | les joints ne contraignent que les rotations *relatives* : un corps posé mais à plat les satisfait tous | couple de redressement du bassin dans `WobbleRig.UprightPelvis` |
| Le personnage est tordu en permanence | `ConfigurableJoint.targetRotation` s'exprime dans l'espace du joint, pas en local ; au repos la conversion vaut l'identité, donc le bug n'apparaît qu'une fois une animation jouée | conjugaison complète dans `WobbleBone` |
| Un réglage de `Physics` ou de layer disparaît au redémarrage | `Physics.IgnoreLayerCollision` est une API *runtime* et n'écrit pas dans `ProjectSettings` | `RagdollLayerSetup` écrit `m_LayerCollisionMatrix` via `SerializedObject` + `AssetDatabase.SaveAssets()` |
| Une ligne de matrice de collision passe à 0 | ses cases sont des `UInt32` ; écrire une valeur négative via `intValue` la clampe à 0 | utiliser `uintValue` |
| L'IK des mains ne bouge rien, sans aucun message | `AnimatorCullingMode.CullUpdateTransforms` coupe l'IK quand aucun renderer n'est visible — or le rig animé n'en a aucun | `cullingMode = AlwaysAnimate` dans le générateur |
| Les buts IK sont ignorés | l'IK Pass n'est pas activée sur la couche de l'`AnimatorController` | activée par code dans `ProjectAssetGenerator` |
| Le personnage s'écrase au sol dès qu'on réveille l'Animator | l'état par défaut n'a **pas** de motion, et un Animator humanoïde y écrit une pose à zéro | `EnsureDefaultStateHasMotion` génère un idle de repli et le signale en warning |
| Un os part en vrille (156° d'écart) alors que sa raideur est haute | on l'avait mis à l'échelle 1e-4 pour le masquer, ce qui dégénère son tenseur d'inertie — il portait un `Rigidbody`, un collider et un joint | ne jamais mettre à l'échelle un os physique ; la caméra au sternum a rendu le masquage inutile |
| Un fin liseré traverse la vue FPS | la caméra est dans le maillage du joueur, dont le back-face culling ne laisse qu'un éclat rasant | `nearClipPlane` réglé sur la distance des mains mesurée, pas au jugé |
| Les mains sortent du cadre en FPS | près de l'objectif un petit fléchissement fait un grand angle : à 25 cm, 25 cm de chute font 45° | éloigner les buts IK (≈0.6 m) plutôt que de les baisser |
| Les bras traînent derrière le joueur quand il marche | un ressort retarde par définition ; le ballottement était accroché à la translation | tous les os épinglés en `isKinematic` debout, le retard vient de la rotation de la vue dans `PlayerHands` |
| Un maillage généré en éditeur sort `null` dans le prefab | créé et référencé dans la même frame, il ne survit pas à la sérialisation, et l'échec est muet | construire le maillage au runtime (`ArmsOnlyMesh`), pas comme asset |
| Une main tenue ne transmet rien au lancer | un `Rigidbody` kinematic ne rapporte aucune vélocité | vélocité dérivée de deux positions successives dans `PlayerHands` |
| L'invite reste vide alors qu'on vise bien un objet | l'objet test tombe entre deux appels de mesure | le figer en kinematic le temps du test, ce n'est pas un bug du jeu |

---

## 8. État actuel et limites assumées

Fait : boot, menu, lobby répliqué avec ready/start, chargement réseau de la scène de
jeu, spawn des joueurs, contrôleur FPS, Direct IP + Relay + Steam, ragdoll passif à
tension unique pour l'effondrement et le relevé, mains en IK dont le ballottement est
piloté par la rotation de la vue, vue première personne bras seuls et visibles
uniquement quand on porte quelque chose, objets attrapables et lançables avec une charge
au maintien du clic, HUD de visée avec invite et jauge de charge, registre
d'interactions contextuelles. Proto « lycée » : greybox (couloir, 2 salles, WC, bureau CPE),
journée chronométrée avec quota, réput' à poster, surveillant (ronde, vue, ouïe, poursuite,
colle), alarme incendie, tableau à taguer, pétards, HUD de journée et écran LOOSER.

Le surveillant se déplace sur un NavMesh construit au chargement de la scène
(`RuntimeNavMeshBuilder` sur la racine `School`, via AI Navigation) : tout collider
enfant de `School` compte comme obstacle ou sol. Limite du proto : un joueur collé pendant qu'il est en ragdoll
n'est pas téléporté (la racine suit le bassin).

Manque de contenu, pas de code : `AC_Player` n'a ni idle ni locomotion. Un idle de
repli est généré (`_ART/Player/Animation/IdleFallback.anim`) pour que le rig ne
s'écrase pas ; il est à remplacer par de vraies animations, et le ragdoll copiera
fidèlement ce qu'on lui donnera.

En chantier (spec validée, code à écrire) : phases de journée, cours aléatoires et
salles fermées à clé, self et bataille de nourriture, colle coopérative, cycles de trois
jours avec paliers de déblocage. Spec : `Documentation/specs/2026-09-19-school-day-design.md`,
plan : `Documentation/plans/2026-09-19-school-day-plan.md`. Points chauds réseau listés
dans les deux : machine à phases (une seule source de vérité, `ServerTime`), volume de
`NetworkObject` pendant une bataille de nourriture, et **état → `NetworkVariable`,
jamais RPC**, pour qu'un joueur qui rejoint en milieu de journée voie la journée telle
qu'elle est.

Non fait (volontairement) : host migration, reconnexion, voix, anti-triche,
persistance, interpolation avancée, UI en prefabs (l'UI est construite par code, voir
`UiFactory` — le remplacement est localisé dans ce seul fichier).
