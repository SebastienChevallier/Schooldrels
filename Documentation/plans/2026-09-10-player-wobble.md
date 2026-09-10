# Wobble physique du joueur — plan d'implémentation

> **Pour un worker agentique :** SOUS-SKILL REQUISE — utiliser `superpowers:subagent-driven-development` (recommandé) ou `superpowers:executing-plans` pour exécuter tâche par tâche. Les étapes utilisent la syntaxe checkbox (`- [ ]`).

**But :** donner au joueur un corps qui ballotte à la manière de PEAK — ragdoll passif traîné par la racine — et qui peut s'effondrer puis se relever, piloté par un scalaire de tension unique.

**Architecture :** deux squelettes. Un `AnimatedRig` kinematic piloté par l'`Animator`, enfant du `Player`, sans renderer. Un rig physique (`Rigidbody` + `ConfigurableJoint` par os) instancié à la racine de la scène, portant le `SkinnedMeshRenderer` visible, tiré vers la pose animée par des drives en ressort dont la raideur est modulée par `tension ∈ [0,1]`. Aucun os ne transite sur le réseau : seule une `NetworkVariable<float>` de tension est répliquée.

**Stack :** Unity 6000.6, URP 17.6, Netcode for GameObjects 2.13, Input System 1.20. Rig Mixamo Y Bot (`mixamorig:*`). Prefabs générés par code (`ProjectAssetGenerator`).

**Spec de référence :** `Documentation/specs/2026-09-10-player-wobble-design.md`

---

## Note sur la vérification

Ce projet n'a **aucune infrastructure de test** (décision actée dans le spec). Chaque tâche se vérifie donc par :

1. **Compilation** — la console Unity ne montre aucune erreur après recompilation.
2. **Play mode** — `Tools > Hold My Beer > Play From Boot`, puis l'observation décrite dans la tâche.
3. **Deux clients quand le réseau est en jeu** — `Window > Multiplayer > Multiplayer Play Mode` (virtual players), mode Direct IP sur `127.0.0.1`.

**Ne jamais valider un ressenti uniquement chez le host** : il a 0 ms de latence, c'est écrit dans `CLAUDE.md` et c'est particulièrement vrai pour du ballottement.

Après chaque tâche qui touche le générateur, il faut relancer
`Tools > Hold My Beer > Generate Project Assets` pour que le prefab reflète le code.

---

## Structure des fichiers

**Créés :**

| Fichier | Responsabilité |
|---|---|
| `Assets/HoldMyBeer/Runtime/Player/Wobble/WobbleSettings.cs` | valeurs de réglage sérialisables |
| `Assets/HoldMyBeer/Runtime/Player/Wobble/WobbleSolver.cs` | maths de la tension, classe pure |
| `Assets/HoldMyBeer/Runtime/Player/Wobble/WobbleBone.cs` | un os physique ; seul accès à l'API `ConfigurableJoint` |
| `Assets/HoldMyBeer/Runtime/Player/Wobble/WobbleRig.cs` | possède les os, applique la tension |
| `Assets/HoldMyBeer/Runtime/Player/PlayerRagdollState.cs` | couche réseau : `NetworkVariable` + RPC |
| `Assets/HoldMyBeer/Editor/PlayerRagdollBuilder.cs` | génère `Prefabs/PlayerRagdoll.prefab` depuis Y Bot |
| `Assets/HoldMyBeer/Editor/RagdollLayerSetup.cs` | crée la layer `PlayerRagdoll` et sa matrice de collision |

**Modifiés :**

| Fichier | Changement |
|---|---|
| `Assets/HoldMyBeer/Editor/ProjectAssetGenerator.cs:95-146` | `CreatePlayerPrefab()` : capsule → `AnimatedRig` Y Bot, ajout `WobbleRig` + `PlayerRagdollState` |
| `Assets/HoldMyBeer/Runtime/Player/PlayerController.cs` | suspension du motor pendant l'effondrement, masquage par l'os `Head` |

**Non modifiés, et c'est le point :** `FirstPersonMotor`, `OwnerNetworkTransform`, `PlayerSpawner`, `ConnectionApprovalHandler`, la `NetworkPrefabsList`. Le prefab ragdoll n'est **jamais** spawné par NGO, il ne va donc **pas** dans la `NetworkPrefabsList`.

---

## Tâche 1 : brancher le rig Mixamo à la place de la capsule

Prérequis du spec. À la fin de cette tâche, le jeu tourne comme avant mais avec un vrai personnage animé — aucune physique encore.

**Fichiers :**
- Modifier : `Assets/HoldMyBeer/Editor/ProjectAssetGenerator.cs:110-118` (bloc `Visual`)
- Modifier : `Assets/HoldMyBeer/Editor/ProjectAssetGenerator.cs:28-34` (constantes)

- [ ] **Étape 1 : ajouter les chemins d'assets art aux constantes**

Après la ligne `private const string NetworkPrefabsListPath = ...;` (ligne 34), ajouter :

```csharp
        private const string ArtFolder = Root + "/_ART/Player";
        private const string PlayerModelPath = ArtFolder + "/Models/Y Bot.fbx";
        private const string PlayerAnimatorPath = ArtFolder + "/AC_Player.controller";
        private const string RagdollPrefabPath = PrefabsFolder + "/PlayerRagdoll.prefab";
```

- [ ] **Étape 2 : remplacer le bloc capsule par l'instanciation du rig**

Remplacer intégralement ce bloc (lignes 110-118) :

```csharp
            // Visual body, hidden for the owner so it does not block the camera.
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());
```

par :

```csharp
            // The animated rig drives the pose; the physical ragdoll follows it.
            // Keeping them apart is what stops the Animator and physics from
            // fighting over the same Transforms.
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
            if (model == null)
            {
                throw new FileNotFoundException(
                    $"Player model not found at '{PlayerModelPath}'. " +
                    "Re-import the Y Bot FBX before generating assets.");
            }

            var animatedRig = (GameObject)PrefabUtility.InstantiatePrefab(model);
            animatedRig.name = "AnimatedRig";
            animatedRig.transform.SetParent(root.transform, false);

            var animator = animatedRig.GetComponent<Animator>();
            if (animator == null)
            {
                animator = animatedRig.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerAnimatorPath);
            animator.applyRootMotion = false;
```

- [ ] **Étape 3 : réparer la référence `firstPersonHiddenVisual`**

Ligne 140, `serialized.FindProperty("firstPersonHiddenVisual").objectReferenceValue = visual;` référence une variable qui n'existe plus. La remplacer par :

```csharp
            serialized.FindProperty("firstPersonHiddenVisual").objectReferenceValue = animatedRig;
```

C'est temporaire : la tâche 8 supprime ce champ au profit du masquage par l'os `Head`.

- [ ] **Étape 4 : régénérer et vérifier**

Dans Unity : `Tools > Hold My Beer > Generate Project Assets`.
Attendu : console sans erreur, message `[Hold My Beer] Scenes and prefabs generated.`
Puis `Tools > Hold My Beer > Play From Boot`, héberger une partie en Direct IP.
Attendu : le personnage Y Bot est visible à la place de la capsule pour un observateur ; l'owner ne le voit pas (masqué en entier, comportement d'avant).

- [ ] **Étape 5 : commit**

```bash
git add Assets/HoldMyBeer/Editor/ProjectAssetGenerator.cs Assets/HoldMyBeer/Prefabs/Player.prefab
git commit -m "feat(player): remplace la capsule par le rig Mixamo anime"
```

---

## Tâche 2 : `WobbleSettings`

**Fichiers :**
- Créer : `Assets/HoldMyBeer/Runtime/Player/Wobble/WobbleSettings.cs`

- [ ] **Étape 1 : écrire le fichier**

```csharp
using UnityEngine;

namespace HoldMyBeer.Player.Wobble
{
    /// <summary>
    /// Tunable wobble values, editable per prefab variant. Mirrors the shape of
    /// <see cref="PlayerMovementSettings"/> so both read the same way in the inspector.
    /// </summary>
    [System.Serializable]
    public struct WobbleSettings
    {
        [SerializeField] private float idleTension;
        [SerializeField] private float springAtFullTension;
        [SerializeField] private float damper;
        [SerializeField] private float maxForce;
        [SerializeField] private float collapseDuration;
        [SerializeField] private float recoverDuration;
        [SerializeField] private float pelvisFollowSpring;
        [SerializeField] private float pelvisFollowDamper;
        [SerializeField] private float maxBoneSpeed;
        [SerializeField] private float maxBoneAngularSpeed;
        [SerializeField] private float watchdogDistance;
        [SerializeField] private float collapseTensionThreshold;

        public float IdleTension => idleTension;
        public float SpringAtFullTension => springAtFullTension;
        public float Damper => damper;
        public float MaxForce => maxForce;
        public float CollapseDuration => collapseDuration;
        public float RecoverDuration => recoverDuration;
        public float PelvisFollowSpring => pelvisFollowSpring;
        public float PelvisFollowDamper => pelvisFollowDamper;
        public float MaxBoneSpeed => maxBoneSpeed;
        public float MaxBoneAngularSpeed => maxBoneAngularSpeed;
        public float WatchdogDistance => watchdogDistance;

        /// <summary>Below this tension the root stops being driven by the CharacterController.</summary>
        public float CollapseTensionThreshold => collapseTensionThreshold;

        public static WobbleSettings Default => new()
        {
            idleTension = 1f,
            springAtFullTension = 800f,
            damper = 30f,
            maxForce = 1500f,
            collapseDuration = 0.2f,
            recoverDuration = 1f,
            pelvisFollowSpring = 900f,
            pelvisFollowDamper = 45f,
            maxBoneSpeed = 30f,
            maxBoneAngularSpeed = 25f,
            watchdogDistance = 6f,
            collapseTensionThreshold = 0.25f
        };
    }
}
```

Ces valeurs sont un point de départ crédible, pas un réglage final. La tâche 12 est la passe de tuning.

- [ ] **Étape 2 : vérifier la compilation**

Console Unity après recompilation automatique. Attendu : aucune erreur.

- [ ] **Étape 3 : commit**

```bash
git add Assets/HoldMyBeer/Runtime/Player/Wobble/WobbleSettings.cs
git commit -m "feat(wobble): ajoute WobbleSettings"
```

---

## Tâche 3 : `WobbleSolver`

La seule logique pure de la feature. Aucune dépendance Unity au-delà de `Mathf`.

**Fichiers :**
- Créer : `Assets/HoldMyBeer/Runtime/Player/Wobble/WobbleSolver.cs`

- [ ] **Étape 1 : écrire le fichier**

```csharp
using UnityEngine;

namespace HoldMyBeer.Player.Wobble
{
    /// <summary>
    /// Turns a target tension into a smoothed current tension and the joint drive
    /// values that follow from it. Pure maths, no Unity objects — same reason
    /// <see cref="FirstPersonMotor"/> is a plain class: a future server-authoritative
    /// rewrite must not have to touch it.
    /// </summary>
    public sealed class WobbleSolver
    {
        private readonly WobbleSettings _settings;

        public WobbleSolver(WobbleSettings settings, float initialTension)
        {
            _settings = settings;
            Tension = Mathf.Clamp01(initialTension);
        }

        /// <summary>Current tension, 1 = braced, 0 = full ragdoll.</summary>
        public float Tension { get; private set; }

        public bool IsCollapsed => Tension < _settings.CollapseTensionThreshold;

        /// <summary>
        /// Ramps the tension towards <paramref name="targetTension"/>. Collapsing is
        /// deliberately faster than recovering: falling should feel sudden, getting
        /// up should not.
        /// </summary>
        public float Tick(float targetTension, float deltaTime)
        {
            var target = Mathf.Clamp01(targetTension);
            var duration = target < Tension
                ? _settings.CollapseDuration
                : _settings.RecoverDuration;

            if (duration <= 0f)
            {
                Tension = target;
                return Tension;
            }

            Tension = Mathf.MoveTowards(Tension, target, deltaTime / duration);
            return Tension;
        }

        /// <summary>
        /// Joint spring for the current tension. Squared so the last stretch of the
        /// recovery is gentle instead of snapping the body upright.
        /// </summary>
        public float CurrentSpring => _settings.SpringAtFullTension * Tension * Tension;

        public float CurrentDamper => _settings.Damper * Tension;

        public float CurrentMaxForce => _settings.MaxForce;
    }
}
```

- [ ] **Étape 2 : vérifier la compilation**

Attendu : aucune erreur en console.

- [ ] **Étape 3 : commit**

```bash
git add Assets/HoldMyBeer/Runtime/Player/Wobble/WobbleSolver.cs
git commit -m "feat(wobble): ajoute WobbleSolver, la rampe de tension"
```

---

## Tâche 4 : layer `PlayerRagdoll` et matrice de collision

Sans ça, les os d'un même corps se percutent et le personnage convulse. À faire **avant** de créer le moindre rigidbody.

**Fichiers :**
- Créer : `Assets/HoldMyBeer/Editor/RagdollLayerSetup.cs`

- [ ] **Étape 1 : écrire le fichier**

```csharp
using UnityEditor;
using UnityEngine;

namespace HoldMyBeer.Editor
{
    /// <summary>
    /// Creates the ragdoll collision layer and unchecks it against itself.
    /// Done in code for the same reason scenes and prefabs are: so that deleting
    /// ProjectSettings and regenerating gets you back to a known-good state.
    /// </summary>
    public static class RagdollLayerSetup
    {
        public const string LayerName = "PlayerRagdoll";

        [MenuItem("Tools/Hold My Beer/Setup Ragdoll Layer", priority = 40)]
        public static void Setup()
        {
            var layer = EnsureLayer(LayerName);
            if (layer < 0)
            {
                Debug.LogError(
                    $"[Hold My Beer] No free user layer left to create '{LayerName}'. " +
                    "Free one in Project Settings > Tags and Layers.");
                return;
            }

            // Bones of one body must not hit each other, and the ragdoll must not
            // hit the CharacterController that tows it.
            Physics.IgnoreLayerCollision(layer, layer, true);
            Physics.IgnoreLayerCollision(layer, 0, false);

            Debug.Log($"[Hold My Beer] Layer '{LayerName}' ready on index {layer}.");
        }

        public static int LayerIndex => LayerMask.NameToLayer(LayerName);

        private static int EnsureLayer(string name)
        {
            var existing = LayerMask.NameToLayer(name);
            if (existing >= 0)
            {
                return existing;
            }

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");

            // 0-7 are Unity's built-in layers.
            for (var i = 8; i < layers.arraySize; i++)
            {
                var element = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = name;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            }

            return -1;
        }
    }
}
```

- [ ] **Étape 2 : exécuter et vérifier**

Dans Unity : `Tools > Hold My Beer > Setup Ragdoll Layer`.
Attendu en console : `[Hold My Beer] Layer 'PlayerRagdoll' ready on index N.`
Vérifier ensuite dans `Edit > Project Settings > Physics > Layer Collision Matrix` que la case `PlayerRagdoll` × `PlayerRagdoll` est **décochée**.

- [ ] **Étape 3 : commit**

```bash
git add Assets/HoldMyBeer/Editor/RagdollLayerSetup.cs ProjectSettings/TagManager.asset ProjectSettings/DynamicsManager.asset
git commit -m "feat(wobble): ajoute la layer PlayerRagdoll et sa matrice de collision"
```

---

## Tâche 5 : `WobbleBone`

Le seul fichier du projet qui touche l'API `ConfigurableJoint`. Il contient le piège du spec — la conversion d'espace de `targetRotation`.

**Fichiers :**
- Créer : `Assets/HoldMyBeer/Runtime/Player/Wobble/WobbleBone.cs`

- [ ] **Étape 1 : écrire le fichier**

```csharp
using UnityEngine;

namespace HoldMyBeer.Player.Wobble
{
    /// <summary>
    /// One physical bone, sprung towards its animated counterpart. This is the only
    /// place in the project that touches the ConfigurableJoint API: swapping the
    /// technique later means rewriting this file and nothing else.
    /// </summary>
    public sealed class WobbleBone
    {
        private readonly ConfigurableJoint _joint;
        private readonly Transform _physical;
        private readonly Transform _animated;

        // targetRotation is expressed in the joint space captured at configuration
        // time, NOT in current local space. Forgetting this conversion is what
        // produces a permanently twisted character.
        private readonly Quaternion _startLocalRotation;

        public WobbleBone(Rigidbody body, ConfigurableJoint joint, Transform animated)
        {
            Body = body;
            _joint = joint;
            _physical = body.transform;
            _animated = animated;
            _startLocalRotation = _physical.localRotation;
        }

        public Rigidbody Body { get; }
        public Transform Animated => _animated;

        /// <summary>Pushes the animated pose into the joint's drive target.</summary>
        public void MatchAnimatedRotation()
        {
            _joint.targetRotation =
                Quaternion.Inverse(_animated.localRotation) * _startLocalRotation;
        }

        public void ApplyTension(float spring, float damper, float maxForce)
        {
            var drive = new JointDrive
            {
                positionSpring = spring,
                positionDamper = damper,
                maximumForce = maxForce
            };

            _joint.angularXDrive = drive;
            _joint.angularYZDrive = drive;
            _joint.slerpDrive = drive;
        }

        /// <summary>
        /// Teleports the bone onto its animated counterpart and kills its momentum.
        /// Used at spawn and by the watchdog.
        /// </summary>
        public void SnapToAnimated()
        {
            _physical.SetPositionAndRotation(_animated.position, _animated.rotation);
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// A misconfigured joint produces NaN, and one NaN contaminates the whole rig
        /// in a single frame. Clamping is cheaper than debugging that.
        /// </summary>
        public void ClampVelocity(float maxSpeed, float maxAngularSpeed)
        {
            var velocity = Body.linearVelocity;
            var angular = Body.angularVelocity;

            if (float.IsNaN(velocity.sqrMagnitude) || float.IsNaN(angular.sqrMagnitude))
            {
                SnapToAnimated();
                return;
            }

            if (velocity.sqrMagnitude > maxSpeed * maxSpeed)
            {
                Body.linearVelocity = velocity.normalized * maxSpeed;
            }

            if (angular.sqrMagnitude > maxAngularSpeed * maxAngularSpeed)
            {
                Body.angularVelocity = angular.normalized * maxAngularSpeed;
            }
        }
    }
}
```

- [ ] **Étape 2 : vérifier la compilation**

Attendu : aucune erreur.

- [ ] **Étape 3 : commit**

```bash
git add Assets/HoldMyBeer/Runtime/Player/Wobble/WobbleBone.cs
git commit -m "feat(wobble): ajoute WobbleBone, wrapper ConfigurableJoint"
```

---

## Tâche 6 : `PlayerRagdollBuilder`

Génère `Prefabs/PlayerRagdoll.prefab` depuis le Y Bot : rigidbodies, colliders, joints, layer. Le Ragdoll Wizard d'Unity ferait l'essentiel, mais à la main et sans reproductibilité — or ce projet régénère ses assets.

**Fichiers :**
- Créer : `Assets/HoldMyBeer/Editor/PlayerRagdollBuilder.cs`

- [ ] **Étape 1 : écrire le fichier**

```csharp
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HoldMyBeer.Editor
{
    /// <summary>
    /// Builds the physical ragdoll prefab from the Mixamo rig. Never spawned by NGO:
    /// each client simulates every avatar's ragdoll locally, so it must NOT be added
    /// to the NetworkPrefabsList.
    /// </summary>
    public static class PlayerRagdollBuilder
    {
        private const string ModelPath = "Assets/HoldMyBeer/_ART/Player/Models/Y Bot.fbx";
        private const string PrefabPath = "Assets/HoldMyBeer/Prefabs/PlayerRagdoll.prefab";

        private readonly struct BoneSpec
        {
            public BoneSpec(string bone, string parent, float mass, float radius, bool isBox)
            {
                Bone = bone;
                Parent = parent;
                Mass = mass;
                Radius = radius;
                IsBox = isBox;
            }

            public string Bone { get; }
            public string Parent { get; }
            public float Mass { get; }
            public float Radius { get; }
            public bool IsBox { get; }
        }

        // The classic 13-body humanoid ragdoll. Masses total roughly 70 units;
        // absolute values matter less than their ratios.
        private static readonly BoneSpec[] Bones =
        {
            new("mixamorig:Hips",         null,                      12f, 0.16f, true),
            new("mixamorig:Spine1",       "mixamorig:Hips",          16f, 0.18f, true),
            new("mixamorig:Head",         "mixamorig:Spine1",         5f, 0.13f, false),
            new("mixamorig:LeftUpLeg",    "mixamorig:Hips",           7f, 0.09f, false),
            new("mixamorig:LeftLeg",      "mixamorig:LeftUpLeg",      4f, 0.07f, false),
            new("mixamorig:LeftFoot",     "mixamorig:LeftLeg",        1f, 0.05f, true),
            new("mixamorig:RightUpLeg",   "mixamorig:Hips",           7f, 0.09f, false),
            new("mixamorig:RightLeg",     "mixamorig:RightUpLeg",     4f, 0.07f, false),
            new("mixamorig:RightFoot",    "mixamorig:RightLeg",       1f, 0.05f, true),
            new("mixamorig:LeftArm",      "mixamorig:Spine1",         2.5f, 0.06f, false),
            new("mixamorig:LeftForeArm",  "mixamorig:LeftArm",        1.5f, 0.05f, false),
            new("mixamorig:RightArm",     "mixamorig:Spine1",         2.5f, 0.06f, false),
            new("mixamorig:RightForeArm", "mixamorig:RightArm",       1.5f, 0.05f, false)
        };

        [MenuItem("Tools/Hold My Beer/Rebuild Player Ragdoll", priority = 41)]
        public static GameObject Build()
        {
            RagdollLayerSetup.Setup();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                throw new FileNotFoundException(
                    $"Player model not found at '{ModelPath}'. Re-import the Y Bot FBX.");
            }

            var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
            root.name = "PlayerRagdoll";

            var layer = RagdollLayerSetup.LayerIndex;
            SetLayerRecursively(root.transform, layer);

            var bodies = new Dictionary<string, Rigidbody>();

            foreach (var spec in Bones)
            {
                var bone = FindBone(root.transform, spec.Bone);
                if (bone == null)
                {
                    Object.DestroyImmediate(root);
                    throw new MissingReferenceException(
                        $"Bone '{spec.Bone}' is missing from '{ModelPath}'. " +
                        "The rig was renamed or re-exported differently; " +
                        "update PlayerRagdollBuilder.Bones to match.");
                }

                var body = bone.gameObject.AddComponent<Rigidbody>();
                body.mass = spec.Mass;
                // FixedUpdate physics rendered in Update judders at 50 Hz without this.
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                bodies[spec.Bone] = body;

                AddCollider(bone, spec);

                if (spec.Parent == null)
                {
                    continue;
                }

                var joint = bone.gameObject.AddComponent<ConfigurableJoint>();
                joint.connectedBody = bodies[spec.Parent];
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = Vector3.zero;
                joint.connectedAnchor =
                    bodies[spec.Parent].transform.InverseTransformPoint(bone.position);

                joint.xMotion = ConfigurableJointMotion.Locked;
                joint.yMotion = ConfigurableJointMotion.Locked;
                joint.zMotion = ConfigurableJointMotion.Locked;
                joint.angularXMotion = ConfigurableJointMotion.Free;
                joint.angularYMotion = ConfigurableJointMotion.Free;
                joint.angularZMotion = ConfigurableJointMotion.Free;

                // Slerp drive, because per-axis drives fight each other on a rig
                // whose bone axes are not aligned with the world.
                joint.rotationDriveMode = RotationDriveMode.Slerp;
                joint.enablePreprocessing = false;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[Hold My Beer] Ragdoll prefab rebuilt at '{PrefabPath}'.");
            return prefab;
        }

        private static void AddCollider(Transform bone, BoneSpec spec)
        {
            if (spec.IsBox)
            {
                var box = bone.gameObject.AddComponent<BoxCollider>();
                box.size = Vector3.one * (spec.Radius * 2f);
                return;
            }

            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = spec.Radius;
            capsule.height = spec.Radius * 5f;
            // Mixamo bones run along their local Y.
            capsule.direction = 1;
            capsule.center = new Vector3(0f, spec.Radius * 1.5f, 0f);
        }

        private static Transform FindBone(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindBone(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void SetLayerRecursively(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            for (var i = 0; i < transform.childCount; i++)
            {
                SetLayerRecursively(transform.GetChild(i), layer);
            }
        }
    }
}
```

- [ ] **Étape 2 : exécuter le builder**

Dans Unity : `Tools > Hold My Beer > Rebuild Player Ragdoll`.
Attendu en console : `[Hold My Beer] Ragdoll prefab rebuilt at 'Assets/HoldMyBeer/Prefabs/PlayerRagdoll.prefab'.`

Si la console montre à la place `Bone 'mixamorig:X' is missing`, le rig a été réexporté sous d'autres noms : lister les vrais noms avec un `Debug.Log` sur la hiérarchie et corriger le tableau `Bones`. C'est un échec voulu, bruyant, en éditeur.

- [ ] **Étape 3 : vérifier visuellement le prefab**

Ouvrir `Assets/HoldMyBeer/Prefabs/PlayerRagdoll.prefab` en mode prefab. Sélectionner `mixamorig:Hips`, `mixamorig:LeftArm`, `mixamorig:Head`.
Attendu : chacun porte un `Rigidbody`, un collider, et un `ConfigurableJoint` (sauf `Hips`) dont le `Connected Body` pointe le bon parent. Les colliders enveloppent grossièrement le membre — grossièrement suffit, on affinera en tâche 12.

- [ ] **Étape 4 : commit**

```bash
git add Assets/HoldMyBeer/Editor/PlayerRagdollBuilder.cs Assets/HoldMyBeer/Prefabs/PlayerRagdoll.prefab
git commit -m "feat(wobble): genere le prefab de ragdoll depuis le rig Mixamo"
```

---

## Tâche 7 : `WobbleRig`

Le cœur runtime. Instancie le rig physique, apparie les os, applique la tension, tient les garde-fous. Ne connaît **ni** le réseau **ni** le `CharacterController`.

**Fichiers :**
- Créer : `Assets/HoldMyBeer/Runtime/Player/Wobble/WobbleRig.cs`

- [ ] **Étape 1 : écrire le fichier**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace HoldMyBeer.Player.Wobble
{
    /// <summary>
    /// Owns the physical ragdoll and drives it towards the animated pose.
    /// Deliberately network-agnostic: it receives a tension and applies it, so it
    /// runs identically on the local avatar and on every remote one.
    /// </summary>
    public sealed class WobbleRig : MonoBehaviour
    {
        [SerializeField] private GameObject ragdollPrefab;
        [SerializeField] private Transform animatedRigRoot;
        [SerializeField] private WobbleSettings settings = WobbleSettings.Default;

        private readonly List<WobbleBone> _bones = new();

        private GameObject _ragdollInstance;
        private WobbleSolver _solver;
        private Rigidbody _pelvis;
        private Transform _animatedPelvis;
        private float _targetTension = 1f;

        public bool IsBuilt => _ragdollInstance != null;
        public float Tension => _solver?.Tension ?? 1f;
        public bool IsCollapsed => _solver != null && _solver.IsCollapsed;
        public Vector3 PelvisPosition => _pelvis != null ? _pelvis.position : transform.position;

        public void SetTargetTension(float tension)
        {
            _targetTension = Mathf.Clamp01(tension);
        }

        /// <summary>
        /// Instantiates the ragdoll at the current pose. Must be called AFTER the
        /// root has been placed, otherwise the ragdoll is born at the world origin
        /// and gets catapulted at the first FixedUpdate.
        /// </summary>
        public void Build()
        {
            if (IsBuilt)
            {
                return;
            }

            if (ragdollPrefab == null || animatedRigRoot == null)
            {
                Debug.LogError(
                    $"[Hold My Beer] WobbleRig on '{name}' is missing its ragdoll prefab " +
                    "or animated rig reference. Re-run Tools > Hold My Beer > Generate Project Assets.");
                return;
            }

            _solver = new WobbleSolver(settings, settings.IdleTension);
            _targetTension = settings.IdleTension;

            _ragdollInstance = Instantiate(ragdollPrefab, transform.position, transform.rotation);
            _ragdollInstance.name = $"PlayerRagdoll_{GetInstanceID()}";

            // The Player is spawned dynamically, so NGO keeps it across the
            // Menu -> Game load. The ragdoll must survive with it or we are left
            // holding a null reference.
            DontDestroyOnLoad(_ragdollInstance);

            PairBones();
            IgnoreOwnCharacterController();
            SnapToAnimatedPose();
        }

        /// <summary>
        /// The ragdoll must hit the ground but not the CharacterController towing it.
        /// Both sit on the Default layer, so the collision matrix cannot tell them
        /// apart — it has to be done per collider pair, per instance.
        /// </summary>
        private void IgnoreOwnCharacterController()
        {
            var controller = GetComponent<CharacterController>();
            if (controller == null)
            {
                return;
            }

            foreach (var collider in _ragdollInstance.GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(collider, controller, true);
            }
        }

        public void Teardown()
        {
            if (_ragdollInstance != null)
            {
                Destroy(_ragdollInstance);
                _ragdollInstance = null;
            }

            _bones.Clear();
            _pelvis = null;
            _solver = null;
        }

        public void SnapToAnimatedPose()
        {
            foreach (var bone in _bones)
            {
                bone.SnapToAnimated();
            }
        }

        private void PairBones()
        {
            _bones.Clear();

            foreach (var joint in _ragdollInstance.GetComponentsInChildren<ConfigurableJoint>())
            {
                var animated = FindByName(animatedRigRoot, joint.name);
                if (animated == null)
                {
                    Debug.LogError(
                        $"[Hold My Beer] Ragdoll bone '{joint.name}' has no match in the " +
                        "animated rig. The two rigs come from different models.");
                    continue;
                }

                _bones.Add(new WobbleBone(joint.GetComponent<Rigidbody>(), joint, animated));
            }

            // The pelvis has no joint, so it is not in the loop above. It is the bone
            // towed by the root, and the spring towing it is what makes the body lag
            // behind — the wobble itself.
            var pelvisTransform = FindByName(_ragdollInstance.transform, "mixamorig:Hips");
            _pelvis = pelvisTransform != null ? pelvisTransform.GetComponent<Rigidbody>() : null;

            if (_pelvis == null)
            {
                Debug.LogError("[Hold My Beer] Ragdoll has no 'mixamorig:Hips' rigidbody.");
            }

            // Cached once: FixedUpdate runs 50 times a second per player, and
            // CLAUDE.md forbids lookups in the per-frame path for exactly this reason.
            _animatedPelvis = FindByName(animatedRigRoot, "mixamorig:Hips");
            if (_animatedPelvis == null)
            {
                Debug.LogError("[Hold My Beer] Animated rig has no 'mixamorig:Hips' bone.");
            }
        }

        private void FixedUpdate()
        {
            if (!IsBuilt || _pelvis == null)
            {
                return;
            }

            var tension = _solver.Tick(_targetTension, Time.fixedDeltaTime);
            var spring = _solver.CurrentSpring;
            var damper = _solver.CurrentDamper;
            var maxForce = _solver.CurrentMaxForce;

            foreach (var bone in _bones)
            {
                bone.MatchAnimatedRotation();
                bone.ApplyTension(spring, damper, maxForce);
                bone.ClampVelocity(settings.MaxBoneSpeed, settings.MaxBoneAngularSpeed);
            }

            TowPelvis(tension);
            RunWatchdog();
        }

        /// <summary>
        /// The spring that tows the pelvis towards the animated pose. This lag IS
        /// the wobble: too stiff and the body is rigid, too soft and it drags.
        /// </summary>
        private void TowPelvis(float tension)
        {
            if (tension <= 0f || _animatedPelvis == null)
            {
                return;
            }

            var offset = _animatedPelvis.position - _pelvis.position;
            var force = offset * (settings.PelvisFollowSpring * tension)
                        - _pelvis.linearVelocity * (settings.PelvisFollowDamper * tension);

            _pelvis.AddForce(force, ForceMode.Acceleration);
        }

        private void RunWatchdog()
        {
            // This will happen: a scene load, a timeScale of 0, a paused debugger.
            // Without the resnap the only recourse is restarting the match.
            var drift = (_pelvis.position - transform.position).sqrMagnitude;
            if (drift > settings.WatchdogDistance * settings.WatchdogDistance)
            {
                SnapToAnimatedPose();
            }
        }

        private void OnDestroy()
        {
            Teardown();
        }

        private static Transform FindByName(Transform root, string boneName)
        {
            if (root.name == boneName)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindByName(root.GetChild(i), boneName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
```

- [ ] **Étape 2 : vérifier la compilation**

Attendu : aucune erreur.

- [ ] **Étape 3 : commit**

```bash
git add Assets/HoldMyBeer/Runtime/Player/Wobble/WobbleRig.cs
git commit -m "feat(wobble): ajoute WobbleRig, le pilote du ragdoll passif"
```

---

## Tâche 8 : câbler `WobbleRig` sur le prefab et voir le premier ballottement

Première tâche qui produit quelque chose de visible. Encore sans réseau : le rig est construit dans `Start`, temporairement.

**Fichiers :**
- Modifier : `Assets/HoldMyBeer/Editor/ProjectAssetGenerator.cs` (`CreatePlayerPrefab`)

- [ ] **Étape 1 : ajouter le `using` du namespace wobble**

En tête de `ProjectAssetGenerator.cs`, après `using HoldMyBeer.Player;` (ligne 7) :

```csharp
using HoldMyBeer.Player.Wobble;
```

- [ ] **Étape 2 : générer le ragdoll dans `GenerateAll`**

Dans `GenerateAll()`, juste après `EnsureFolders();` (ligne 41), ajouter :

```csharp
            var ragdollPrefab = PlayerRagdollBuilder.Build();
```

et changer la signature de l'appel suivant :

```csharp
            var playerPrefab = CreatePlayerPrefab(ragdollPrefab);
```

- [ ] **Étape 3 : recevoir le prefab et ajouter le composant**

Changer la signature de la méthode :

```csharp
        private static GameObject CreatePlayerPrefab(GameObject ragdollPrefab)
```

Puis, juste avant `var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);`, ajouter :

```csharp
            var wobbleRig = root.AddComponent<WobbleRig>();
            var wobbleSerialized = new SerializedObject(wobbleRig);
            wobbleSerialized.FindProperty("ragdollPrefab").objectReferenceValue = ragdollPrefab;
            wobbleSerialized.FindProperty("animatedRigRoot").objectReferenceValue = animatedRig.transform;
            wobbleSerialized.ApplyModifiedPropertiesWithoutUndo();
```

- [ ] **Étape 4 : construire le rig temporairement au démarrage**

Dans `WobbleRig.cs`, ajouter cette méthode pour pouvoir observer avant que le réseau ne soit branché. Elle sera **supprimée en tâche 10**.

```csharp
        [SerializeField] private bool buildOnStart;

        private void Start()
        {
            // Temporary: replaced by PlayerRagdollState in task 10.
            if (buildOnStart)
            {
                Build();
            }
        }
```

- [ ] **Étape 5 : régénérer et observer**

`Tools > Hold My Beer > Generate Project Assets`, puis ouvrir `Prefabs/Player.prefab` et cocher **Build On Start** sur le composant `WobbleRig`.
Lancer `Tools > Hold My Beer > Play From Boot`, héberger, marcher.

Attendu : un second personnage (le ragdoll) apparaît et suit le joueur en ballottant. Le personnage de l'`AnimatedRig` est encore visible aussi — les deux se superposent. C'est normal, la tâche 9 supprime le doublon.

Si le ragdoll part à l'infini : vérifier que le prefab ragdoll n'a pas été mis enfant du `Player`.
Si le personnage est tordu en permanence : la conversion `targetRotation` de `WobbleBone.MatchAnimatedRotation` est en cause — c'est le bug attendu à cet endroit.

- [ ] **Étape 6 : commit**

```bash
git add Assets/HoldMyBeer/Editor/ProjectAssetGenerator.cs Assets/HoldMyBeer/Runtime/Player/Wobble/WobbleRig.cs Assets/HoldMyBeer/Prefabs/Player.prefab
git commit -m "feat(wobble): cable WobbleRig sur le prefab joueur"
```

---

## Tâche 9 : rendre l'`AnimatedRig` invisible

Le mesh visible doit être celui du ragdoll, pas celui du rig animé.

**Fichiers :**
- Modifier : `Assets/HoldMyBeer/Editor/ProjectAssetGenerator.cs` (`CreatePlayerPrefab`)

- [ ] **Étape 1 : désactiver les renderers du rig animé**

Juste après le bloc qui configure l'`Animator`, ajouter :

```csharp
            // The animated rig is a pose source, not something to look at: the
            // visible mesh lives on the physical ragdoll.
            foreach (var renderer in animatedRig.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }
```

- [ ] **Étape 2 : régénérer et observer**

`Tools > Hold My Beer > Generate Project Assets`, puis Play From Boot.
Attendu : un seul personnage visible, celui qui ballotte. Plus de superposition.

- [ ] **Étape 3 : commit**

```bash
git add Assets/HoldMyBeer/Editor/ProjectAssetGenerator.cs Assets/HoldMyBeer/Prefabs/Player.prefab
git commit -m "feat(wobble): masque le rig anime, seul le ragdoll est visible"
```

---

## Tâche 10 : `PlayerRagdollState`, la couche réseau

**Fichiers :**
- Créer : `Assets/HoldMyBeer/Runtime/Player/PlayerRagdollState.cs`
- Modifier : `Assets/HoldMyBeer/Runtime/Player/Wobble/WobbleRig.cs` (retirer `buildOnStart`)
- Modifier : `Assets/HoldMyBeer/Editor/ProjectAssetGenerator.cs`

- [ ] **Étape 1 : écrire `PlayerRagdollState`**

```csharp
using HoldMyBeer.Player.Wobble;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Player
{
    /// <summary>
    /// The only file in the wobble feature that knows about NGO. No bone is ever
    /// replicated: every client simulates every avatar's ragdoll locally, and only
    /// the target tension crosses the wire.
    /// </summary>
    [RequireComponent(typeof(WobbleRig))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerRagdollState : NetworkBehaviour
    {
        private readonly NetworkVariable<float> _targetTension = new(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private WobbleRig _rig;

        public bool IsCollapsed => _rig != null && _rig.IsCollapsed;
        public Vector3 PelvisPosition => _rig != null ? _rig.PelvisPosition : transform.position;

        private void Awake()
        {
            _rig = GetComponent<WobbleRig>();
        }

        public override void OnNetworkSpawn()
        {
            // PlayerSpawner positions the instance before calling SpawnAsPlayerObject,
            // so the root is already at its spawn pose here — which is exactly what
            // the ragdoll needs, or it is born at the origin and catapulted.
            _rig.Build();
            _rig.SetTargetTension(_targetTension.Value);
            _targetTension.OnValueChanged += OnTensionChanged;
        }

        public override void OnNetworkDespawn()
        {
            _targetTension.OnValueChanged -= OnTensionChanged;

            // Without this, every player who leaves strands a physics corpse that
            // keeps simulating.
            _rig.Teardown();
        }

        /// <summary>Asks the server to drop this avatar into a full ragdoll.</summary>
        [Rpc(SendTo.Server)]
        public void RequestCollapseRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            _targetTension.Value = 0f;
        }

        /// <summary>Asks the server to brace this avatar back up.</summary>
        [Rpc(SendTo.Server)]
        public void RequestRecoverRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            _targetTension.Value = 1f;
        }

        private void OnTensionChanged(float previous, float current)
        {
            _rig.SetTargetTension(current);
        }
    }
}
```

Le contrôle d'autorité `SenderClientId != OwnerClientId` applique la règle du projet : on n'accepte pas qu'un joueur fasse tomber quelqu'un d'autre. L'owner reste libre de mentir sur sa propre chute — même arbitrage que pour sa position, assumé dans le spec.

- [ ] **Étape 2 : retirer le `buildOnStart` temporaire**

Dans `WobbleRig.cs`, supprimer intégralement :

```csharp
        [SerializeField] private bool buildOnStart;

        private void Start()
        {
            // Temporary: replaced by PlayerRagdollState in task 10.
            if (buildOnStart)
            {
                Build();
            }
        }
```

- [ ] **Étape 3 : ajouter le composant au prefab**

Dans `ProjectAssetGenerator.CreatePlayerPrefab`, après le bloc `wobbleSerialized.ApplyModifiedPropertiesWithoutUndo();` :

```csharp
            root.AddComponent<PlayerRagdollState>();
```

- [ ] **Étape 4 : régénérer et vérifier à deux clients**

`Tools > Hold My Beer > Generate Project Assets`.
Ouvrir `Window > Multiplayer > Multiplayer Play Mode`, activer un virtual player, lancer Play From Boot, héberger, puis rejoindre en Direct IP sur `127.0.0.1`.

Attendu : les deux avatars ballottent, chez le host **et** chez le client. Quand un joueur se déconnecte, aucun ragdoll orphelin ne reste dans la hiérarchie (vérifier dans la fenêtre Hierarchy, section DontDestroyOnLoad).

- [ ] **Étape 5 : commit**

```bash
git add Assets/HoldMyBeer/Runtime/Player/PlayerRagdollState.cs Assets/HoldMyBeer/Runtime/Player/Wobble/WobbleRig.cs Assets/HoldMyBeer/Editor/ProjectAssetGenerator.cs Assets/HoldMyBeer/Prefabs/Player.prefab
git commit -m "feat(wobble): replique la tension par NetworkVariable"
```

---

## Tâche 11 : passage de relais de la racine à l'effondrement

Sous le seuil de tension, le `CharacterController` s'éteint et la racine suit le bassin physique. `OwnerNetworkTransform` continue de répliquer, donc les autres voient la chute au bon endroit.

**Fichiers :**
- Modifier : `Assets/HoldMyBeer/Runtime/Player/PlayerController.cs`
- Modifier : `Assets/HoldMyBeer/Runtime/Player/PlayerRagdollState.cs`

- [ ] **Étape 1 : exposer la suspension du motor dans `PlayerController`**

Ajouter le champ, avec les autres champs privés :

```csharp
        private bool _motorSuspended;
```

Ajouter la méthode publique, juste après `OnNetworkDespawn()` :

```csharp
        /// <summary>
        /// Hands control of the root over to the ragdoll (or takes it back). Called
        /// by <see cref="PlayerRagdollState"/> when the tension crosses the collapse
        /// threshold; the CharacterController and the physics body must never drive
        /// the same transform at once.
        /// </summary>
        public void SetMotorSuspended(bool suspended)
        {
            if (_motorSuspended == suspended)
            {
                return;
            }

            _motorSuspended = suspended;
            _controller.enabled = !suspended;
        }
```

Puis, dans `Update()`, remplacer la ligne :

```csharp
            _motor.Tick(_input.Move, _input.SprintHeld, _input.JumpPressedThisFrame, Time.deltaTime);
```

par :

```csharp
            if (!_motorSuspended)
            {
                _motor.Tick(_input.Move, _input.SprintHeld, _input.JumpPressedThisFrame, Time.deltaTime);
            }
```

Le regard reste actif pendant l'effondrement : on garde le contrôle de la caméra en tombant, c'est nettement moins désagréable.

- [ ] **Étape 2 : piloter le relais depuis `PlayerRagdollState`**

Ajouter le champ :

```csharp
        private PlayerController _controller;
        private bool _rootFollowsRagdoll;
```

Dans `Awake()`, après `_rig = GetComponent<WobbleRig>();` :

```csharp
            _controller = GetComponent<PlayerController>();
```

Ajouter la méthode `LateUpdate`, qui ne tourne que chez l'owner :

```csharp
        private void LateUpdate()
        {
            // Only the owner may move the root: OwnerNetworkTransform replicates it
            // from here, so remote clients get the fall for free.
            if (!IsOwner || !IsSpawned || !_rig.IsBuilt)
            {
                return;
            }

            var shouldFollow = _rig.IsCollapsed;
            if (shouldFollow != _rootFollowsRagdoll)
            {
                _rootFollowsRagdoll = shouldFollow;
                _controller.SetMotorSuspended(shouldFollow);

                if (!shouldFollow)
                {
                    // Recovering: drop the root onto the ground under the pelvis
                    // before the CharacterController takes over again.
                    transform.position = ProjectToGround(_rig.PelvisPosition);
                    _rig.SnapToAnimatedPose();
                }
            }

            if (_rootFollowsRagdoll)
            {
                transform.position = _rig.PelvisPosition;
            }
        }

        private static Vector3 ProjectToGround(Vector3 origin)
        {
            return Physics.Raycast(origin + Vector3.up, Vector3.down, out var hit, 5f)
                ? hit.point
                : origin;
        }
```

- [ ] **Étape 3 : ajouter un raccourci de test temporaire**

Dans `PlayerRagdollState`, ajouter un `Update` pour pouvoir déclencher l'effondrement à la main. Il sera **supprimé en tâche 12**.

```csharp
        private void Update()
        {
            // Temporary manual trigger, replaced by hard-landing detection in task 12.
            if (!IsOwner || !IsSpawned)
            {
                return;
            }

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                RequestCollapseRpc();
            }

            if (keyboard.tKey.wasPressedThisFrame)
            {
                RequestRecoverRpc();
            }
        }
```

- [ ] **Étape 4 : vérifier à deux clients**

Play From Boot avec un virtual player, rejoindre en Direct IP.
Appuyer sur **R** chez le client (pas chez le host — le host a 0 ms de latence et ne prouve rien), puis sur **T**.

Attendu : le joueur s'effondre en ~0,2 s, s'affale au sol, et se relève en ~1 s. Chez l'autre machine, la chute se voit au même endroit, sans téléportation ni glissade.

Si le joueur passe à travers le sol pendant la chute : les colliders du ragdoll sont sur la layer `PlayerRagdoll`, vérifier que cette layer **collisionne bien** avec la layer du sol dans la matrice.
Si le joueur remonte brutalement au relevé : `ProjectToGround` ne trouve pas de sol, augmenter la distance du raycast.

- [ ] **Étape 5 : commit**

```bash
git add Assets/HoldMyBeer/Runtime/Player/PlayerController.cs Assets/HoldMyBeer/Runtime/Player/PlayerRagdollState.cs
git commit -m "feat(wobble): passage de relais de la racine pendant l effondrement"
```

---

## Tâche 12 : détection de l'atterrissage violent

Remplace le raccourci clavier par le vrai déclencheur.

**Fichiers :**
- Modifier : `Assets/HoldMyBeer/Runtime/Player/FirstPersonMotor.cs`
- Modifier : `Assets/HoldMyBeer/Runtime/Player/PlayerMovementSettings.cs`
- Modifier : `Assets/HoldMyBeer/Runtime/Player/PlayerController.cs`
- Modifier : `Assets/HoldMyBeer/Runtime/Player/PlayerRagdollState.cs`

- [ ] **Étape 1 : exposer la vitesse d'impact dans `FirstPersonMotor`**

Ajouter le champ privé, sous `private Vector3 _velocity;` :

```csharp
        private bool _wasGrounded = true;
```

Ajouter la propriété publique, sous `public bool IsGrounded => ...` :

```csharp
        /// <summary>
        /// Downward speed at the frame the character touched the ground again, or 0.
        /// Read once per Tick: it is only valid on the landing frame.
        /// </summary>
        public float LandingImpactSpeed { get; private set; }
```

Dans `Tick`, remplacer le début du bloc `if (_controller.isGrounded)` par :

```csharp
            LandingImpactSpeed = 0f;

            if (_controller.isGrounded)
            {
                if (!_wasGrounded)
                {
                    LandingImpactSpeed = -_velocity.y;
                }

                _wasGrounded = true;

                // A small downward bias keeps isGrounded stable on slopes and steps.
                _velocity.y = -2f;
```

et, dans le `else`, ajouter en première ligne :

```csharp
                _wasGrounded = false;
```

- [ ] **Étape 2 : ajouter le seuil aux réglages de mouvement**

Dans `PlayerMovementSettings`, ajouter le champ avec les autres :

```csharp
        [SerializeField] private float collapseImpactSpeed;
```

la propriété :

```csharp
        /// <summary>Landing faster than this drops the player into a ragdoll.</summary>
        public float CollapseImpactSpeed => collapseImpactSpeed;
```

et la valeur dans `Default` :

```csharp
            collapseImpactSpeed = 12f
```

Attention à la virgule : `maxPitch = 85f,` doit désormais se terminer par une virgule.

- [ ] **Étape 3 : exposer l'événement depuis `PlayerController`**

Ajouter le champ :

```csharp
        private PlayerRagdollState _ragdollState;
```

Dans `Awake()`, après `_input = new KeyboardMousePlayerInputSource(settings.LookSensitivity);` :

```csharp
            _ragdollState = GetComponent<PlayerRagdollState>();
```

Dans `Update()`, juste après l'appel à `_motor.Tick(...)` (à l'intérieur du `if (!_motorSuspended)`) :

```csharp
                if (_ragdollState != null &&
                    _motor.LandingImpactSpeed > settings.CollapseImpactSpeed)
                {
                    _ragdollState.RequestCollapseRpc();
                }
```

- [ ] **Étape 4 : relevé automatique après un délai**

Dans `PlayerRagdollState`, **supprimer** l'`Update` temporaire de la tâche 11 et le remplacer par :

```csharp
        [SerializeField] private float automaticRecoveryDelay = 2f;

        private float _recoveryAt;

        private void Update()
        {
            if (!IsOwner || !IsSpawned || !_rig.IsBuilt)
            {
                return;
            }

            if (!_rig.IsCollapsed)
            {
                _recoveryAt = 0f;
                return;
            }

            if (_recoveryAt <= 0f)
            {
                _recoveryAt = Time.time + automaticRecoveryDelay;
                return;
            }

            if (Time.time >= _recoveryAt)
            {
                _recoveryAt = 0f;
                RequestRecoverRpc();
            }
        }
```

- [ ] **Étape 5 : vérifier à deux clients**

Play From Boot avec un virtual player. Chez le **client**, sauter d'une hauteur suffisante pour dépasser 12 m/s à l'impact (une chute d'environ 8 mètres avec la gravité de `-19.62`).

Attendu : le joueur s'effondre à l'atterrissage, reste au sol ~2 s, puis se relève. Une marche ou un saut normal ne déclenche rien.

Si tout saut déclenche l'effondrement, `collapseImpactSpeed` est trop bas — c'est le premier réglage à monter en tâche 13.

- [ ] **Étape 6 : commit**

```bash
git add Assets/HoldMyBeer/Runtime/Player/FirstPersonMotor.cs Assets/HoldMyBeer/Runtime/Player/PlayerMovementSettings.cs Assets/HoldMyBeer/Runtime/Player/PlayerController.cs Assets/HoldMyBeer/Runtime/Player/PlayerRagdollState.cs
git commit -m "feat(wobble): effondrement declenche par l atterrissage violent"
```

---

## Tâche 13 : vue première personne par l'os `Head`

Remplace le masquage du corps entier. Le joueur voit son torse, ses bras et ses jambes ballotter sous lui.

**Fichiers :**
- Modifier : `Assets/HoldMyBeer/Runtime/Player/PlayerController.cs`
- Modifier : `Assets/HoldMyBeer/Runtime/Player/Wobble/WobbleRig.cs`
- Modifier : `Assets/HoldMyBeer/Editor/ProjectAssetGenerator.cs`

- [ ] **Étape 1 : exposer l'os de tête du ragdoll**

Dans `WobbleRig`, ajouter le champ :

```csharp
        private Transform _ragdollHead;
```

Dans `PairBones()`, après le cache de `_animatedPelvis` :

```csharp
            _ragdollHead = FindByName(_ragdollInstance.transform, "mixamorig:Head");
```

Ajouter la méthode publique :

```csharp
        /// <summary>
        /// Shrinks the head bone away for the owner. Y Bot is a single mesh, so there
        /// is no separate first-person arm rig to maintain: the player simply looks
        /// out from a body whose head is scaled to nothing.
        /// </summary>
        public void SetHeadVisible(bool visible)
        {
            if (_ragdollHead != null)
            {
                _ragdollHead.localScale = visible ? Vector3.one : Vector3.one * 0.0001f;
            }
        }
```

Une échelle de `0.0001` plutôt que `0` : une échelle nulle produit une matrice non inversible et fait râler le skinning.

- [ ] **Étape 2 : appeler depuis `PlayerRagdollState`**

Dans `OnNetworkSpawn()`, après `_rig.SetTargetTension(_targetTension.Value);` :

```csharp
            _rig.SetHeadVisible(!IsOwner);
```

- [ ] **Étape 3 : supprimer `firstPersonHiddenVisual`**

Dans `PlayerController.cs`, supprimer le champ :

```csharp
        [SerializeField] private GameObject firstPersonHiddenVisual;
```

et le bloc qui l'utilise dans `OnNetworkSpawn()` :

```csharp
            // Only the owner sees through this camera; hide the body so it does not
            // fill the near plane.
            if (firstPersonHiddenVisual != null)
            {
                firstPersonHiddenVisual.SetActive(false);
            }
```

Dans `ProjectAssetGenerator.CreatePlayerPrefab`, supprimer la ligne :

```csharp
            serialized.FindProperty("firstPersonHiddenVisual").objectReferenceValue = animatedRig;
```

- [ ] **Étape 4 : régénérer et vérifier**

`Tools > Hold My Beer > Generate Project Assets`, puis Play From Boot à deux clients.

Attendu : en vue FPS, le joueur voit son torse, ses bras et ses jambes ballotter sous lui, sans tête qui bouche l'écran. L'autre joueur le voit avec sa tête.

Si la caméra est à l'intérieur du torse : remonter le `CameraPivot` (actuellement à `y = 1.7`) ou augmenter le `nearClipPlane` (actuellement `0.05`).

- [ ] **Étape 5 : commit**

```bash
git add Assets/HoldMyBeer/Runtime/Player/PlayerController.cs Assets/HoldMyBeer/Runtime/Player/Wobble/WobbleRig.cs Assets/HoldMyBeer/Editor/ProjectAssetGenerator.cs Assets/HoldMyBeer/Prefabs/Player.prefab
git commit -m "feat(wobble): vue FPS par masquage de l os de tete"
```

---

## Tâche 14 : passe de réglage

La feature est complète ; il reste à la rendre drôle. Cette tâche n'a pas de critère d'acceptation objectif — c'est du ressenti, et c'est le tien.

- [ ] **Étape 1 : régler à deux clients, jamais chez le host seul**

Play From Boot avec un virtual player. Régler sur le prefab `Player`, composant `WobbleRig`, en Play mode (les valeurs se perdent à la sortie — les reporter dans `WobbleSettings.Default` une fois trouvées).

| Symptôme | Paramètre | Sens |
|---|---|---|
| corps rigide, aucun ballottement | `springAtFullTension` | baisser |
| corps mou qui traîne au sol | `springAtFullTension` | monter |
| tremblote, vibre sur place | `damper` | monter |
| oscille longtemps après un arrêt | `damper` | monter |
| le corps traîne trop loin derrière | `pelvisFollowSpring` | monter |
| le corps colle, pas de retard | `pelvisFollowSpring` | baisser |
| l'effondrement est mou | `collapseDuration` | baisser |
| le relevé est brutal | `recoverDuration` | monter |
| tout saut déclenche la chute | `collapseImpactSpeed` (dans `PlayerMovementSettings`) | monter |

- [ ] **Étape 2 : reporter les valeurs trouvées**

Éditer `WobbleSettings.Default` et `PlayerMovementSettings.Default` avec les valeurs retenues.

- [ ] **Étape 3 : vérifier la stabilité**

Jouer cinq minutes à deux, en incluant : un changement de scène `Menu → Game`, une déconnexion et une reconnexion, plusieurs chutes.

Attendu : aucun ragdoll orphelin dans la hiérarchie, aucun personnage tordu, aucun `NaN` en console, aucun resnap visible du watchdog en jeu normal.

- [ ] **Étape 4 : commit**

```bash
git add Assets/HoldMyBeer/Runtime/Player/Wobble/WobbleSettings.cs Assets/HoldMyBeer/Runtime/Player/PlayerMovementSettings.cs
git commit -m "tune(wobble): valeurs de reglage apres passe a deux clients"
```

---

## Ce que ce plan ne fait pas

Volontairement hors périmètre, conformément au spec :

- **L'ivresse** comme modificateur d'amplitude. Le point d'entrée existe : appeler `WobbleRig.SetTargetTension` avec une valeur intermédiaire.
- **La préhension physique** d'objets, l'escalade.
- **Les tests.** `WobbleSolver` reste une classe pure pour que ce soit possible sans refactoring le jour venu.
- **L'anti-triche.** Un owner peut refuser de tomber — même arbitrage que pour sa position, déjà assumé dans `CLAUDE.md`.

---

## Corrections apportées pendant l'exécution

Consignées ici parce qu'elles contredisent le texte des tâches déjà exécutées.

**Tâche 4 — `Physics.IgnoreLayerCollision` ne persiste pas.** C'est une API *runtime* :
elle modifie la matrice de la session Éditeur en cours et rien d'autre. Vérifié en
comparant les `md5` de `ProjectSettings/` avant et après : inchangés. Il fallait
écrire `m_LayerCollisionMatrix` via `SerializedObject`, puis appeler
`AssetDatabase.SaveAssets()` — sans quoi même la layer créée dans `TagManager.asset`
restait en mémoire.

**Tâche 4 — les lignes de la matrice sont des `UInt32`.** Premier correctif écrit avec
`row.intValue &= ~(1 << layer)` : la valeur `-257` a été **clampée à 0**, ce qui faisait
ignorer *toutes* les layers à `PlayerRagdoll` — le ragdoll serait passé à travers le sol.
Il faut `row.uintValue = uint.MaxValue & ~(1u << layer)`. Vérifié sur disque :
`row8 = fffeffff`, et en vivant `GetIgnoreLayerCollision(8,8)=True`, `(8,0)=False`.

**Tâche 4 — la ligne `Physics.IgnoreLayerCollision(layer, 0, false)` était fausse.**
Son commentaire annonçait d'empêcher la collision avec le `CharacterController`, mais
`false` l'*active*. Supprimée : ni `true` ni `false` n'est correct ici, parce que le
sol et le `CharacterController` sont tous deux sur `Default`. Voir la correction de la
tâche 7 ci-dessous.

**Tâche 7 — collision ragdoll ↔ `CharacterController`.** Impossible à exprimer par
layer, puisqu'il faut heurter le sol (`Default`) sans heurter le contrôleur (`Default`
aussi). Réglé par paire de colliders dans `WobbleRig.Build()`, via
`IgnoreOwnCharacterController()` — ajouté au code de la tâche 7.

**Tâche 5 — `ClampVelocity` utilise `float.IsFinite` et non `float.IsNaN`.** Un solveur
qui diverge atteint l'infini aussi facilement que le NaN, et un infini empoisonne le rig
de la même manière.

**Outillage.** Le CLI Unity (`com.unity.pipeline`) a été ajouté au projet : il permet de
recompiler, lancer les `MenuItem` et lire la console sans passer par l'interface. C'est
ce qui a permis de détecter les deux bugs de la tâche 4 par la mesure plutôt que par la
lecture.
