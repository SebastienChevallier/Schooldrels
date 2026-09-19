using System.Collections.Generic;
using System.IO;
using HoldMyBeer.Gameplay.Adults;
using HoldMyBeer.Gameplay.Day;
using HoldMyBeer.Gameplay.Interaction;
using HoldMyBeer.Gameplay.Items;
using HoldMyBeer.Gameplay.Lunch;
using HoldMyBeer.Gameplay.Pranks;
using HoldMyBeer.Gameplay.Rooms;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace HoldMyBeer.Editor
{
    /// <summary>
    /// The networked prefabs of the school prototype. Every one of them is spawned, so
    /// every one of them must end up in the network prefabs list — a prefab missing on
    /// a client is a blunt disconnect with an obscure message (CLAUDE.md §4.5).
    ///
    /// <see cref="All"/> exists precisely so that adding a prefab here cannot be
    /// forgotten in the list: the generator asks for the whole set, never for names.
    /// </summary>
    public sealed class SchoolPrefabs
    {
        private const string PrefabsFolder = "Assets/HoldMyBeer/Prefabs";
        private const string MaterialsFolder = PrefabsFolder + "/Materials";

        private readonly List<GameObject> _all = new();

        public GameObject DayState { get; private set; }
        public GameObject Supervisor { get; private set; }
        public GameObject Teacher { get; private set; }
        public GameObject Door { get; private set; }
        public GameObject Key { get; private set; }
        public GameObject FireAlarm { get; private set; }
        public GameObject Blackboard { get; private set; }
        public GameObject Firecracker { get; private set; }
        public GameObject Tray { get; private set; }

        /// <summary>Everything that can be spawned, in creation order.</summary>
        public IReadOnlyList<GameObject> All => _all;

        public static SchoolPrefabs Create()
        {
            var prefabs = new SchoolPrefabs();
            prefabs.Build();
            return prefabs;
        }

        private void Build()
        {
            // ----------------------------------------------------------- items
            var ball = Track(Grabbable("Ballon", PrimitiveType.Sphere, Vector3.one * 0.45f,
                new Color(0.9f, 0.5f, 0.15f), mass: 0.6f, projectile: true));
            var smallBall = Track(Grabbable("Balle", PrimitiveType.Sphere, Vector3.one * 0.12f,
                new Color(0.95f, 0.95f, 0.3f), mass: 0.15f, projectile: true));
            var whistle = Track(Grabbable("Sifflet", PrimitiveType.Cylinder, new Vector3(0.06f, 0.07f, 0.06f),
                new Color(0.8f, 0.8f, 0.85f), mass: 0.1f, projectile: false, root => root.AddComponent<Whistle>()));

            var loot = Track(Grabbable("PC", PrimitiveType.Cube, new Vector3(0.5f, 0.35f, 0.45f),
                new Color(0.25f, 0.27f, 0.3f), mass: 9f, projectile: false,
                root => root.AddComponent<LootItem>()));
            var arduino = Track(Grabbable("Arduino", PrimitiveType.Cube, new Vector3(0.18f, 0.04f, 0.12f),
                new Color(0.1f, 0.5f, 0.7f), mass: 0.12f, projectile: false, root =>
                {
                    var prank = root.AddComponent<DelayedPrank>();
                    Set(prank, s => s.FindProperty("statusRenderer").objectReferenceValue =
                        root.GetComponent<Renderer>());
                }));

            var chalk = Track(Grabbable("Craie", PrimitiveType.Cylinder, new Vector3(0.03f, 0.05f, 0.03f),
                Color.white, mass: 0.05f, projectile: true));
            var eraser = Track(Grabbable("Effaceur", PrimitiveType.Cube, new Vector3(0.16f, 0.07f, 0.09f),
                new Color(0.35f, 0.3f, 0.28f), mass: 0.25f, projectile: true));
            var pellet = Track(Grabbable("Projectile", PrimitiveType.Sphere, Vector3.one * 0.05f,
                new Color(0.9f, 0.9f, 0.75f), mass: 0.02f, projectile: true));
            var blowgun = Track(Grabbable("Sarbacane", PrimitiveType.Cylinder, new Vector3(0.04f, 0.4f, 0.04f),
                new Color(0.6f, 0.55f, 0.4f), mass: 0.2f, projectile: false, root =>
                {
                    var gun = root.AddComponent<Blowgun>();
                    Set(gun, s => s.FindProperty("pelletPrefab").objectReferenceValue = pellet);
                }));

            var smoke = Track(CreateSmoke());
            var flaskA = Track(Flask("FioleA", ChemicalItem.Reagent.A, new Color(0.3f, 0.85f, 0.4f), smoke));
            var flaskB = Track(Flask("FioleB", ChemicalItem.Reagent.B, new Color(0.9f, 0.4f, 0.8f), smoke));
            var flaskC = Track(Flask("FioleC", ChemicalItem.Reagent.C, new Color(0.35f, 0.6f, 0.95f), smoke));
            var hotPlate = Track(CreateHotPlate());

            var mash = Track(Food("Puree", new Color(0.95f, 0.85f, 0.5f)));
            var fruit = Track(Food("Pomme", new Color(0.85f, 0.2f, 0.2f)));
            var dessert = Track(Food("Dessert", new Color(0.8f, 0.7f, 0.55f)));

            Tray = Track(CreateTray());

            // ----------------------------------------------------------- data
            var data = SchoolData.Create(
                eps: new[] { ball, smallBall, whistle },
                techno: new[] { loot, arduino },
                general: new[] { chalk, eraser, blowgun },
                science: new[] { flaskA, flaskB, flaskC, hotPlate },
                menus: new[]
                {
                    new[] { mash, fruit, dessert },
                    new[] { mash, mash, fruit },
                    new[] { fruit, dessert, dessert }
                });

            // --------------------------------------------------------- fixtures
            DayState = Track(CreateDayState(data));
            Supervisor = Track(CreateSupervisor("Supervisor", teaches: false));
            Teacher = Track(CreateSupervisor("Teacher", teaches: true));
            Door = Track(CreateDoor());
            Key = Track(CreateKey());

            FireAlarm = Track(CreatePrankTarget("FireAlarm", new Vector3(0.35f, 0.35f, 0.12f),
                new Color(0.8f, 0.1f, 0.1f), "Déclencher l'alarme", "déclenche l'alarme incendie",
                reputation: 30, noiseRadius: 60f, cooldownSeconds: 45f));
            Blackboard = Track(CreatePrankTarget("Blackboard", new Vector3(3f, 1.2f, 0.08f),
                new Color(0.1f, 0.25f, 0.15f), "Dessiner une bite", "tague le tableau",
                reputation: 15, noiseRadius: 0f, cooldownSeconds: 40f));
            Firecracker = Track(CreateFirecracker());
        }

        private GameObject Track(GameObject prefab)
        {
            _all.Add(prefab);
            return prefab;
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

        /// <summary>
        /// The shape every item shares: a body, a grip, replication, and — for the
        /// cheap ones — the impulse-replicated projectile treatment rather than a
        /// streamed transform. See <c>LightProjectile</c> for why.
        /// </summary>
        private static GameObject Grabbable(string name, PrimitiveType shape, Vector3 size, Color color,
                                            float mass, bool projectile,
                                            System.Action<GameObject> decorate = null)
        {
            var root = GameObject.CreatePrimitive(shape);
            root.name = name;
            root.transform.localScale = size;
            root.GetComponent<Renderer>().sharedMaterial = Material(name, color);

            var body = root.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            root.AddComponent<NetworkObject>();
            var networkTransform = root.AddComponent<NetworkTransform>();
            networkTransform.InLocalSpace = false;
            networkTransform.Interpolate = true;

            var grip = new GameObject("GripAnchor");
            grip.transform.SetParent(root.transform, false);

            var item = root.AddComponent<GrabbableItem>();
            Set(item, s => s.FindProperty("gripAnchor").objectReferenceValue = grip.transform);

            if (projectile)
            {
                root.AddComponent<LightProjectile>();
            }

            decorate?.Invoke(root);
            return Save(root, name);
        }

        private static GameObject Food(string name, Color color)
        {
            return Grabbable(name, PrimitiveType.Sphere, Vector3.one * 0.14f, color, mass: 0.2f, projectile: true,
                root => root.AddComponent<MessyImpact>());
        }

        private static GameObject Flask(string name, ChemicalItem.Reagent reagent, Color color, GameObject smoke)
        {
            return Grabbable(name, PrimitiveType.Cylinder, new Vector3(0.09f, 0.14f, 0.09f), color,
                mass: 0.3f, projectile: true, root =>
                {
                    var chemical = root.AddComponent<ChemicalItem>();
                    Set(chemical, s =>
                    {
                        s.FindProperty("reagent").enumValueIndex = (int)reagent;
                        s.FindProperty("smokePrefab").objectReferenceValue = smoke;
                    });
                });
        }

        private static GameObject CreateSmoke()
        {
            var root = new GameObject("Fumee");
            root.AddComponent<NetworkObject>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.GetComponent<Renderer>().sharedMaterial = Material("Smoke", new Color(0.75f, 0.78f, 0.8f, 0.6f));

            var cloud = root.AddComponent<SmokeCloud>();
            Set(cloud, s => s.FindProperty("visual").objectReferenceValue = visual.transform);

            return Save(root, "Fumee");
        }

        private static GameObject CreateHotPlate()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "PlaqueChauffante";
            root.transform.localScale = new Vector3(0.4f, 0.12f, 0.4f);
            root.GetComponent<Renderer>().sharedMaterial = Material("HotPlate", new Color(0.8f, 0.3f, 0.15f));
            root.AddComponent<NetworkObject>();
            root.AddComponent<HeatSource>();
            return Save(root, "PlaqueChauffante");
        }

        private static GameObject CreateTray()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Plateau";
            root.transform.localScale = new Vector3(0.5f, 0.04f, 0.35f);
            root.GetComponent<Renderer>().sharedMaterial = Material("Tray", new Color(0.55f, 0.6f, 0.65f));

            var body = root.AddComponent<Rigidbody>();
            body.mass = 1.2f;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            root.AddComponent<NetworkObject>();
            var networkTransform = root.AddComponent<NetworkTransform>();
            networkTransform.InLocalSpace = false;
            networkTransform.Interpolate = true;

            var grip = new GameObject("GripAnchor");
            grip.transform.SetParent(root.transform, false);

            var item = root.AddComponent<GrabbableItem>();
            Set(item, s => s.FindProperty("gripAnchor").objectReferenceValue = grip.transform);

            // Four portion blocks: the tray has to *look* like it is emptying, or
            // taking something off it reads as nothing happening.
            var portions = new Object[4];
            for (var i = 0; i < portions.Length; i++)
            {
                var portion = GameObject.CreatePrimitive(PrimitiveType.Cube);
                portion.name = $"Portion_{i}";
                Object.DestroyImmediate(portion.GetComponent<Collider>());
                portion.transform.SetParent(root.transform, false);
                portion.transform.localPosition = new Vector3(-0.3f + i * 0.2f, 0.6f, 0f);
                portion.transform.localScale = new Vector3(0.3f, 2f, 0.6f);
                portion.GetComponent<Renderer>().sharedMaterial =
                    Material("Portion", new Color(0.9f, 0.82f, 0.55f));
                portions[i] = portion;
            }

            var tray = root.AddComponent<Tray>();
            Set(tray, s =>
            {
                var array = s.FindProperty("portionVisuals");
                array.arraySize = portions.Length;
                for (var i = 0; i < portions.Length; i++)
                {
                    array.GetArrayElementAtIndex(i).objectReferenceValue = portions[i];
                }
            });

            return Save(root, "Plateau");
        }

        private static GameObject CreateDoor()
        {
            var root = new GameObject("Door");
            root.AddComponent<NetworkObject>();

            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "Leaf";
            leaf.transform.SetParent(root.transform, false);
            leaf.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            leaf.transform.localScale = new Vector3(2f, 2.1f, 0.12f);
            leaf.GetComponent<Renderer>().sharedMaterial = Material("Door", new Color(0.45f, 0.35f, 0.25f));

            var lockLight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lockLight.name = "Lock";
            Object.DestroyImmediate(lockLight.GetComponent<Collider>());
            lockLight.transform.SetParent(leaf.transform, false);
            lockLight.transform.localPosition = new Vector3(0.35f, 0f, 0.6f);
            lockLight.transform.localScale = new Vector3(0.06f, 0.06f, 0.3f);
            lockLight.GetComponent<Renderer>().sharedMaterial = Material("Lock", new Color(0.85f, 0.2f, 0.15f));

            var door = root.AddComponent<Door>();
            Set(door, s =>
            {
                s.FindProperty("leaf").objectReferenceValue = leaf.transform;
                s.FindProperty("lockRenderer").objectReferenceValue = lockLight.GetComponent<Renderer>();
                // Slides sideways into the wall: a swinging leaf would need a hinge and
                // would shove players around, which nothing here is worth.
                s.FindProperty("openOffset").vector3Value = new Vector3(2f, 0f, 0f);
            });

            return Save(root, "Door");
        }

        private static GameObject CreateKey()
        {
            return Grabbable("Cle", PrimitiveType.Cube, new Vector3(0.04f, 0.02f, 0.14f),
                new Color(0.85f, 0.75f, 0.25f), mass: 0.05f, projectile: true,
                root => root.AddComponent<KeyItem>());
        }

        private static GameObject CreateDayState(SchoolData data)
        {
            var root = new GameObject("DayState");
            root.AddComponent<NetworkObject>();

            var day = root.AddComponent<DayState>();
            Set(day, s =>
            {
                s.FindProperty("schedule").objectReferenceValue = data.ComfortSchedule;
                s.FindProperty("courses").objectReferenceValue = data.Courses;
                s.FindProperty("menus").objectReferenceValue = data.Menus;
                s.FindProperty("progression").objectReferenceValue = data.Progression;
            });

            return Save(root, "DayState");
        }

        private static GameObject CreateSupervisor(string name, bool teaches)
        {
            var root = new GameObject(name);

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

            var colour = teaches ? new Color(0.45f, 0.25f, 0.6f) : new Color(0.2f, 0.35f, 0.75f);
            var body = Visual(PrimitiveType.Capsule, root.transform, "Body",
                new Vector3(0f, 0.95f, 0f), new Vector3(0.7f, 0.95f, 0.7f), Material(name, colour));

            // The eyes are the facing indicator: players need to read where it looks.
            Visual(PrimitiveType.Cube, root.transform, "Eyes",
                new Vector3(0f, 1.65f, 0.3f), new Vector3(0.45f, 0.12f, 0.1f), Material("Eyes", Color.white));

            var supervisor = root.AddComponent<Supervisor>();
            Set(supervisor, s =>
            {
                s.FindProperty("bodyRenderer").objectReferenceValue = body.GetComponent<Renderer>();
                s.FindProperty("teachesTheClass").boolValue = teaches;
            });

            return Save(root, name);
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
            Set(target, s =>
            {
                s.FindProperty("prompt").stringValue = prompt;
                s.FindProperty("announcement").stringValue = announcement;
                s.FindProperty("reputation").intValue = reputation;
                s.FindProperty("noiseRadius").floatValue = noiseRadius;
                s.FindProperty("cooldownSeconds").floatValue = cooldownSeconds;
                s.FindProperty("feedbackRenderer").objectReferenceValue = root.GetComponent<Renderer>();
                s.FindProperty("feedbackColor").colorValue =
                    noiseRadius > 0f ? new Color(1f, 0.9f, 0.2f) : new Color(0.95f, 0.95f, 0.95f);
            });

            return Save(root, name);
        }

        private static GameObject CreateFirecracker()
        {
            return Grabbable("Firecracker", PrimitiveType.Cylinder, new Vector3(0.06f, 0.1f, 0.06f),
                new Color(0.85f, 0.1f, 0.15f), mass: 0.2f, projectile: false, root =>
                {
                    var firecracker = root.AddComponent<Firecracker>();
                    Set(firecracker, s => s.FindProperty("fuseRenderer").objectReferenceValue =
                        root.GetComponent<Renderer>());
                });
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

        private static void Set(Object target, System.Action<SerializedObject> edit)
        {
            var serialized = new SerializedObject(target);
            edit(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Save(GameObject root, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
