using System.Collections.Generic;
using System.IO;
using HoldMyBeer.App;
using HoldMyBeer.Core;
using HoldMyBeer.Gameplay;
using HoldMyBeer.Networking;
using HoldMyBeer.Player;
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

        private const int MaxPlayers = 8;

        [MenuItem("Tools/Hold My Beer/Generate Project Assets", priority = 0)]
        public static void GenerateAll()
        {
            EnsureFolders();

            var playerPrefab = CreatePlayerPrefab();
            var lobbyPrefab = CreateLobbyPrefab();
            var prefabsList = CreateNetworkPrefabsList(playerPrefab, lobbyPrefab);

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

        private static GameObject CreatePlayerPrefab()
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

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.7f, 0f);

            var cameraObject = new GameObject("PlayerCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(pivot.transform, false);

            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.enabled = false;

            var audioListener = cameraObject.GetComponent<AudioListener>();
            audioListener.enabled = false;

            var playerController = root.AddComponent<PlayerController>();
            var serialized = new SerializedObject(playerController);
            serialized.FindProperty("cameraPivot").objectReferenceValue = pivot.transform;
            serialized.FindProperty("playerCamera").objectReferenceValue = camera;
            serialized.FindProperty("playerAudioListener").objectReferenceValue = audioListener;
            serialized.FindProperty("firstPersonHiddenVisual").objectReferenceValue = animatedRig;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
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
