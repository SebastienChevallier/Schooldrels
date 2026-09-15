using System.IO;
using HoldMyBeer.Gameplay.Adults;
using HoldMyBeer.Gameplay.Day;
using HoldMyBeer.Gameplay.Interaction;
using HoldMyBeer.Gameplay.Pranks;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace HoldMyBeer.Editor
{
    /// <summary>
    /// The networked prefabs of the school prototype. Every one of them is spawned,
    /// so every one of them must end up in the network prefabs list.
    /// </summary>
    public sealed class SchoolPrefabs
    {
        private const string PrefabsFolder = "Assets/HoldMyBeer/Prefabs";
        private const string MaterialsFolder = PrefabsFolder + "/Materials";

        public GameObject DayState { get; private set; }
        public GameObject Supervisor { get; private set; }
        public GameObject FireAlarm { get; private set; }
        public GameObject Blackboard { get; private set; }
        public GameObject UploadSpot { get; private set; }
        public GameObject Firecracker { get; private set; }

        public static SchoolPrefabs Create()
        {
            return new SchoolPrefabs
            {
                DayState = CreateDayState(),
                Supervisor = CreateSupervisor(),
                FireAlarm = CreatePrankTarget("FireAlarm", new Vector3(0.35f, 0.35f, 0.12f),
                    new Color(0.8f, 0.1f, 0.1f), "Déclencher l'alarme", "déclenche l'alarme incendie",
                    reputation: 30, noiseRadius: 60f, cooldownSeconds: 45f),
                Blackboard = CreatePrankTarget("Blackboard", new Vector3(3f, 1.2f, 0.08f),
                    new Color(0.1f, 0.25f, 0.15f), "Dessiner une bite", "tague le tableau",
                    reputation: 15, noiseRadius: 0f, cooldownSeconds: 40f),
                UploadSpot = CreateUploadSpot(),
                Firecracker = CreateFirecracker()
            };
        }

        /// <summary>
        /// Saved as an asset on purpose: a material created in memory and referenced
        /// from a scene or prefab does not survive serialisation (see CLAUDE.md §7).
        /// </summary>
        public static Material Material(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            {
                Directory.CreateDirectory(MaterialsFolder);
                AssetDatabase.Refresh();
            }

            var path = $"{MaterialsFolder}/M_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateDayState()
        {
            var root = new GameObject("DayState");
            root.AddComponent<NetworkObject>();
            root.AddComponent<DayState>();
            return Save(root, "DayState");
        }

        private static GameObject CreateSupervisor()
        {
            var root = new GameObject("Supervisor");

            var agent = root.AddComponent<NavMeshAgent>();
            agent.height = 1.9f;
            agent.radius = 0.35f;
            agent.acceleration = 20f;
            agent.angularSpeed = 540f;
            agent.stoppingDistance = 0.3f;
            agent.autoBraking = true;

            root.AddComponent<NetworkObject>();
            var networkTransform = root.AddComponent<NetworkTransform>();
            networkTransform.SyncScaleX = networkTransform.SyncScaleY = networkTransform.SyncScaleZ = false;
            networkTransform.InLocalSpace = false;
            networkTransform.Interpolate = true;

            var body = Visual(PrimitiveType.Capsule, root.transform, "Body",
                new Vector3(0f, 0.95f, 0f), new Vector3(0.7f, 0.95f, 0.7f), Material("Supervisor", new Color(0.2f, 0.35f, 0.75f)));

            // The eyes are the facing indicator: players need to read where it looks.
            Visual(PrimitiveType.Cube, root.transform, "Eyes",
                new Vector3(0f, 1.65f, 0.3f), new Vector3(0.45f, 0.12f, 0.1f), Material("Eyes", Color.white));

            var supervisor = root.AddComponent<Supervisor>();
            var serialized = new SerializedObject(supervisor);
            serialized.FindProperty("bodyRenderer").objectReferenceValue = body.GetComponent<Renderer>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return Save(root, "Supervisor");
        }

        private static GameObject CreatePrankTarget(string name, Vector3 size, Color color, string prompt,
                                                    string announcement, int reputation, float noiseRadius,
                                                    float cooldownSeconds)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            root.transform.localScale = size;
            root.GetComponent<Renderer>().sharedMaterial = Material(name, color);
            root.AddComponent<NetworkObject>();

            var target = root.AddComponent<PrankTarget>();
            var serialized = new SerializedObject(target);
            serialized.FindProperty("prompt").stringValue = prompt;
            serialized.FindProperty("announcement").stringValue = announcement;
            serialized.FindProperty("reputation").intValue = reputation;
            serialized.FindProperty("noiseRadius").floatValue = noiseRadius;
            serialized.FindProperty("cooldownSeconds").floatValue = cooldownSeconds;
            serialized.FindProperty("feedbackRenderer").objectReferenceValue = root.GetComponent<Renderer>();
            serialized.FindProperty("feedbackColor").colorValue =
                noiseRadius > 0f ? new Color(1f, 0.9f, 0.2f) : new Color(0.95f, 0.95f, 0.95f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return Save(root, name);
        }

        private static GameObject CreateUploadSpot()
        {
            // A bright phone-shaped slab: the one place with signal, so it has to be findable.
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "UploadSpot";
            root.transform.localScale = new Vector3(0.5f, 0.8f, 0.08f);
            root.GetComponent<Renderer>().sharedMaterial = Material("UploadSpot", new Color(0.2f, 0.8f, 0.95f));
            root.AddComponent<NetworkObject>();
            root.AddComponent<UploadSpot>();
            return Save(root, "UploadSpot");
        }

        private static GameObject CreateFirecracker()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "Firecracker";
            root.transform.localScale = new Vector3(0.06f, 0.1f, 0.06f);
            root.GetComponent<Renderer>().sharedMaterial = Material("Firecracker", new Color(0.85f, 0.1f, 0.15f));

            var body = root.AddComponent<Rigidbody>();
            body.mass = 0.2f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            root.AddComponent<NetworkObject>();
            var networkTransform = root.AddComponent<NetworkTransform>();
            networkTransform.InLocalSpace = false;
            networkTransform.Interpolate = true;

            var grip = new GameObject("GripAnchor");
            grip.transform.SetParent(root.transform, false);
            grip.transform.localPosition = new Vector3(0f, -0.6f, 0f);

            var item = root.AddComponent<GrabbableItem>();
            var itemSerialized = new SerializedObject(item);
            itemSerialized.FindProperty("gripAnchor").objectReferenceValue = grip.transform;
            itemSerialized.ApplyModifiedPropertiesWithoutUndo();

            var firecracker = root.AddComponent<Firecracker>();
            var serialized = new SerializedObject(firecracker);
            serialized.FindProperty("fuseRenderer").objectReferenceValue = root.GetComponent<Renderer>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return Save(root, "Firecracker");
        }

        private static GameObject Visual(PrimitiveType type, Transform parent, string name, Vector3 position,
                                         Vector3 scale, Material material)
        {
            var visual = GameObject.CreatePrimitive(type);
            visual.name = name;

            // Visuals only: a collider here would block the supervisor's own sight ray
            // and stand in the students' aim.
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = position;
            visual.transform.localScale = scale;
            visual.GetComponent<Renderer>().sharedMaterial = material;
            return visual;
        }

        private static GameObject Save(GameObject root, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
