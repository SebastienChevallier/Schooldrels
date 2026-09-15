using System.Collections.Generic;
using System.IO;
using HoldMyBeer.App;
using HoldMyBeer.Core;
using HoldMyBeer.Gameplay;
using HoldMyBeer.Networking;
using HoldMyBeer.Gameplay.Interaction;
using HoldMyBeer.Player;
using HoldMyBeer.Player.Hands;
using HoldMyBeer.Player.Wobble;
using UnityEditor.Animations;
using Unity.Netcode.Components;
using HoldMyBeer.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HoldMyBeer.Editor
{
    /// <summary>
    /// Generates the scenes and prefabs the project needs, from code.
    ///
    /// Scenes and prefabs are binary-ish YAML full of GUIDs: hand-writing them
    /// outside the editor produces broken assets. Generating them keeps the repo
    /// reproducible — delete Scenes/ and Prefabs/, run this, and you are back to a
    /// known-good state. Re-running is safe: existing assets are overwritten.
    /// </summary>
    public static class ProjectAssetGenerator
    {
        private const string Root = "Assets/HoldMyBeer";
        private const string ScenesFolder = Root + "/Scenes";
        private const string PrefabsFolder = Root + "/Prefabs";

        private const string PlayerPrefabPath = PrefabsFolder + "/Player.prefab";
        private const string LobbyPrefabPath = PrefabsFolder + "/LobbyState.prefab";
        private const string NetworkPrefabsListPath = PrefabsFolder + "/HoldMyBeerNetworkPrefabs.asset";

        private const string ArtFolder = Root + "/_ART/Player";
        private const string PlayerModelPath = ArtFolder + "/Models/Y Bot.fbx";
        private const string PlayerAnimatorPath = ArtFolder + "/AC_Player.controller";
        private const string GrabbablePrefabPath = PrefabsFolder + "/Grabbable.prefab";
        private const string FallbackIdleClipPath = ArtFolder + "/Animation/IdleFallback.anim";

        private const int MaxPlayers = 8;

        [MenuItem("Tools/Hold My Beer/Generate Project Assets", priority = 0)]
        public static void GenerateAll()
        {
            EnsureFolders();

            // Built first: the player prefab holds a reference to it.
            var ragdollPrefab = PlayerRagdollBuilder.Build();

            var playerPrefab = CreatePlayerPrefab(ragdollPrefab);
            var lobbyPrefab = CreateLobbyPrefab();
            var grabbablePrefab = CreateGrabbablePrefab();

            // The ragdoll is absent on purpose: it is instantiated locally by every
            // client, never spawned by NGO, so listing it would be wrong.
            var prefabsList = CreateNetworkPrefabsList(playerPrefab, lobbyPrefab, grabbablePrefab);

            CreateBootScene(playerPrefab, lobbyPrefab, prefabsList);
            CreateMenuScene();
            CreateGameScene();

            RegisterBuildScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Hold My Beer] Scenes and prefabs generated. " +
                      "Use Tools > Hold My Beer > Play From Boot to run the game.");
        }

        [MenuItem("Tools/Hold My Beer/Play From Boot", priority = 20)]
        public static void PlayFromBoot()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(ScenePath(SceneNames.Boot));
            EditorApplication.isPlaying = true;
        }

        private static string ScenePath(string sceneName) => $"{ScenesFolder}/{sceneName}.unity";

        private static void EnsureFolders()
        {
            foreach (var folder in new[] { ScenesFolder, PrefabsFolder })
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Directory.CreateDirectory(folder);
                }
            }

            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------ prefabs

        private static GameObject CreatePlayerPrefab(GameObject ragdollPrefab)
        {
            var root = new GameObject("Player");

            var controller = root.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 1f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.35f;

            root.AddComponent<NetworkObject>();

            var networkTransform = root.AddComponent<OwnerNetworkTransform>();
            networkTransform.SyncScaleX = networkTransform.SyncScaleY = networkTransform.SyncScaleZ = false;
            networkTransform.InLocalSpace = false;
            networkTransform.Interpolate = true;

            // The animated rig drives the pose; the physical ragdoll will follow it
            // later. Keeping them apart is what stops the Animator and physics from
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

            // Mandatory here, not a nicety. The default CullUpdateTransforms disables
            // retargeting, IK and transform writes while no renderer is visible — and
            // this rig has none, by design, since the ragdoll carries the mesh. Leaving
            // the default silently kills hand IK with nothing in the console.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            EnableAnimatorIkPass();

            // OnAnimatorIK only fires on the object carrying the Animator, so the IK
            // driver has to live here rather than on the player root.
            var ikDriver = animatedRig.AddComponent<HandIkDriver>();

            // The animated rig is a pose source, not something to look at: the
            // visible mesh lives on the physical ragdoll.
            foreach (var rigRenderer in animatedRig.GetComponentsInChildren<Renderer>(true))
            {
                rigRenderer.enabled = false;
            }

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(root.transform, false);

            // Sternum height rather than eye height, so the arms stay in frame instead
            // of hanging below it. Barely pushed forward: every centimetre here is a
            // centimetre of arm reach lost, and it is the near clip plane below that
            // deals with the chest, not this offset.
            pivot.transform.localPosition = new Vector3(0f, 1.45f, 0.04f);

            var cameraObject = new GameObject("PlayerCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(pivot.transform, false);

            var camera = cameraObject.GetComponent<Camera>();
            // Tuned against the measured hand distance, not guessed: the hands settle
            // about 0.4 m from the lens, and the player's own chest sits right on it now
            // that the camera is at sternum height. 0.11 clips the chest sliver and
            // leaves a wide margin before it would start eating the hands.
            camera.nearClipPlane = 0.11f;
            camera.enabled = false;

            var audioListener = cameraObject.GetComponent<AudioListener>();
            audioListener.enabled = false;

            var playerController = root.AddComponent<PlayerController>();
            var serialized = new SerializedObject(playerController);
            serialized.FindProperty("cameraPivot").objectReferenceValue = pivot.transform;
            serialized.FindProperty("playerCamera").objectReferenceValue = camera;
            serialized.FindProperty("playerAudioListener").objectReferenceValue = audioListener;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var wobbleRig = root.AddComponent<WobbleRig>();
            var wobbleSerialized = new SerializedObject(wobbleRig);
            wobbleSerialized.FindProperty("ragdollPrefab").objectReferenceValue = ragdollPrefab;
            wobbleSerialized.FindProperty("animatedRigRoot").objectReferenceValue = animatedRig.transform;
            wobbleSerialized.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<PlayerRagdollState>();

            var hands = root.AddComponent<PlayerHands>();
            var handsSerialized = new SerializedObject(hands);
            handsSerialized.FindProperty("cameraPivot").objectReferenceValue = pivot.transform;
            handsSerialized.FindProperty("wobbleRig").objectReferenceValue = wobbleRig;
            handsSerialized.FindProperty("ikDriver").objectReferenceValue = ikDriver;
            handsSerialized.ApplyModifiedPropertiesWithoutUndo();

            var interactor = root.AddComponent<PlayerInteractor>();
            var interactorSerialized = new SerializedObject(interactor);
            interactorSerialized.FindProperty("cameraPivot").objectReferenceValue = pivot.transform;
            interactorSerialized.FindProperty("playerController").objectReferenceValue = playerController;
            interactorSerialized.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// Humanoid IK goals are ignored unless the layer runs an IK pass. Done here
        /// rather than by hand so a re-imported controller cannot silently lose it —
        /// the symptom would be hands that never move, with nothing in the console.
        /// </summary>
        private static void EnableAnimatorIkPass()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerAnimatorPath);
            if (controller == null)
            {
                Debug.LogWarning(
                    $"[Hold My Beer] No animator controller at '{PlayerAnimatorPath}'. " +
                    "Hand IK will not run.");
                return;
            }

            var layers = controller.layers;
            for (var i = 0; i < layers.Length; i++)
            {
                layers[i].iKPass = true;
            }

            controller.layers = layers;
            EnsureDefaultStateHasMotion(controller);
            EditorUtility.SetDirty(controller);
        }

        /// <summary>
        /// A humanoid Animator sitting in a state with no motion writes a zeroed pose:
        /// hips at the animator origin, character flat on the floor. That is what the
        /// ragdoll then faithfully copies. It went unnoticed while the animator was
        /// culled, because a culled animator writes nothing and the bind pose survived
        /// by accident.
        ///
        /// The generated clip is a placeholder that only fills an EMPTY default state.
        /// Drop a real idle into that state and this stops touching anything.
        /// </summary>
        private static void EnsureDefaultStateHasMotion(AnimatorController controller)
        {
            if (controller.layers.Length == 0)
            {
                return;
            }

            var defaultState = controller.layers[0].stateMachine.defaultState;
            if (defaultState == null || defaultState.motion != null)
            {
                return;
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(FallbackIdleClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "IdleFallback" };

                // Only the root is keyed. Every muscle left unkeyed stays at 0, which
                // for a humanoid is a relaxed standing pose — exactly the neutral
                // target the wobble rig wants to be pulled towards.
                var height = MeasureBindPoseHipHeight();
                clip.SetCurve("", typeof(Animator), "RootT.y", AnimationCurve.Constant(0f, 1f, height));
                clip.SetCurve("", typeof(Animator), "RootQ.w", AnimationCurve.Constant(0f, 1f, 1f));

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                AssetDatabase.CreateAsset(clip, FallbackIdleClipPath);
            }

            defaultState.motion = clip;

            Debug.LogWarning(
                $"[Hold My Beer] '{controller.name}' had no motion on its default state, so a " +
                $"placeholder idle was generated at '{FallbackIdleClipPath}'. Replace it with a " +
                "real idle animation — the ragdoll copies this pose.");
        }

        private static float MeasureBindPoseHipHeight()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
            if (model == null)
            {
                return 1f;
            }

            foreach (var bone in model.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "mixamorig:Hips")
                {
                    return bone.position.y - model.transform.position.y;
                }
            }

            return 1f;
        }

        /// <summary>
        /// A deliberately plain pick-up-able object. Spawned by NGO, so unlike the
        /// ragdoll it MUST appear in the network prefabs list.
        /// </summary>
        private static GameObject CreateGrabbablePrefab()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "Grabbable";
            root.transform.localScale = new Vector3(0.12f, 0.1f, 0.12f);

            var body = root.AddComponent<Rigidbody>();
            body.mass = 0.6f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            root.AddComponent<NetworkObject>();

            var networkTransform = root.AddComponent<NetworkTransform>();
            networkTransform.InLocalSpace = false;
            networkTransform.Interpolate = true;

            // Offset from the centre so the object hangs from a point rather than
            // being skewered through the middle by the palm.
            var grip = new GameObject("GripAnchor");
            grip.transform.SetParent(root.transform, false);
            grip.transform.localPosition = new Vector3(0f, -0.6f, 0f);

            var item = root.AddComponent<GrabbableItem>();
            var serialized = new SerializedObject(item);
            serialized.FindProperty("gripAnchor").objectReferenceValue = grip.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, GrabbablePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateLobbyPrefab()
        {
            var root = new GameObject("LobbyState");
            root.AddComponent<NetworkObject>();
            root.AddComponent<LobbyNetworkService>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, LobbyPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static NetworkPrefabsList CreateNetworkPrefabsList(params GameObject[] prefabs)
        {
            // Recreated from scratch rather than patched: the list is hashed and
            // compared between peers, so a stale leftover entry is a connection bug.
            AssetDatabase.DeleteAsset(NetworkPrefabsListPath);

            var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            AssetDatabase.CreateAsset(list, NetworkPrefabsListPath);

            foreach (var prefab in prefabs)
            {
                list.Add(new NetworkPrefab { Prefab = prefab });
            }

            EditorUtility.SetDirty(list);
            return list;
        }

        // ------------------------------------------------------------------- scenes

        private static void CreateBootScene(GameObject playerPrefab, GameObject lobbyPrefab,
                                            NetworkPrefabsList prefabsList)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // A camera so the first frame is not "no cameras rendering".
            var cameraObject = new GameObject("BootCamera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            cameraObject.GetComponent<Camera>().backgroundColor = UiFactory.Background;

            var networkObject = new GameObject("NetworkManager");
            var networkManager = networkObject.AddComponent<NetworkManager>();
            var transport = networkObject.AddComponent<UnityTransport>();

            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.NetworkConfig.EnableSceneManagement = true;
            networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabsList);
            EditorUtility.SetDirty(networkManager);

            var bootstrapObject = new GameObject("GameBootstrapper");
            var bootstrapper = bootstrapObject.AddComponent<GameBootstrapper>();

            var serialized = new SerializedObject(bootstrapper);
            serialized.FindProperty("networkManager").objectReferenceValue = networkManager;
            serialized.FindProperty("lobbyPrefab").objectReferenceValue = lobbyPrefab;
            serialized.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
            serialized.FindProperty("maxPlayers").intValue = MaxPlayers;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath(SceneNames.Boot));
        }

        private static void CreateMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var controller = new GameObject("MenuSceneController");
            controller.AddComponent<MenuSceneController>();

            EditorSceneManager.SaveScene(scene, ScenePath(SceneNames.Menu));
        }

        private static void CreateGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightObject = new GameObject("DirectionalLight", typeof(Light));
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(6f, 1f, 6f);

            var spawnRoot = new GameObject("SpawnPoints");
            for (var i = 0; i < MaxPlayers; i++)
            {
                var angle = i * (360f / MaxPlayers) * Mathf.Deg2Rad;
                var point = new GameObject($"SpawnPoint_{i}");
                point.transform.SetParent(spawnRoot.transform, false);
                point.transform.position = new Vector3(Mathf.Cos(angle) * 5f, 0.1f, Mathf.Sin(angle) * 5f);
                point.transform.rotation = Quaternion.LookRotation(
                    new Vector3(-point.transform.position.x, 0f, -point.transform.position.z).normalized,
                    Vector3.up);
            }

            spawnRoot.AddComponent<SpawnPointRegistry>();
            new GameObject("GameSceneBootstrap").AddComponent<GameSceneBootstrap>();

            new GameObject("CrosshairHud").AddComponent<CrosshairHud>();

            var grabbableSpawner = new GameObject("GrabbableSpawner").AddComponent<GrabbableSpawner>();
            var spawnerSerialized = new SerializedObject(grabbableSpawner);
            spawnerSerialized.FindProperty("grabbablePrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(GrabbablePrefabPath);
            spawnerSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath(SceneNames.Game));
        }

        private static void RegisterBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new(ScenePath(SceneNames.Boot), true),
                new(ScenePath(SceneNames.Menu), true),
                new(ScenePath(SceneNames.Game), true)
            };

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
