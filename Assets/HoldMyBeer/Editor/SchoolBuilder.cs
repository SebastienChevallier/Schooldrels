using HoldMyBeer.Gameplay.Day;
using HoldMyBeer.Gameplay.Adults;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine.AI;
using UnityEngine;

namespace HoldMyBeer.Editor
{
    /// <summary>
    /// Greybox of the prototype school, seen from above (x east, z north):
    ///
    ///            ┌── Salle A ──┐   ┌── Salle B ──┐        z = 12
    ///            │             │   │  pétards    │
    ///   ┌────────┴────  ───────┴───┴────  ───────┴────────┐ z = 2
    ///   │ spawn           couloir  (alarme)            ◄ S │
    ///   └──────────────┬──  ──┬──────────────┬──  ──┬─────┘ z = -2
    ///                  │ WC   │              │ CPE  │        z = -8
    ///                  └──────┘              └──────┘
    ///
    /// Every door is 2 m wide. The supervisor paths on a NavMesh built at load from
    /// everything under the School root, so walls and doors can move freely.
    /// </summary>
    public static class SchoolBuilder
    {
        private const float WallHeight = 3f;
        private const float WallThickness = 0.2f;
        private const float DoorWidth = 2f;

        public static void BuildWalls(GameObject ground)
        {
            var root = new GameObject("School").transform;
            ground.transform.SetParent(root, true);

            var surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            root.gameObject.AddComponent<RuntimeNavMeshBuilder>();
            var material = SchoolPrefabs.Material("Wall", new Color(0.85f, 0.82f, 0.72f));

            // Corridor, with a door to each room.
            WallAlongX(root, material, 2f, -20f, 20f, -8f, 8f);
            WallAlongX(root, material, -2f, -20f, 20f, -2f, 14f);
            WallAlongZ(root, material, -20f, -2f, 2f);
            WallAlongZ(root, material, 20f, -2f, 2f);

            // Classrooms A and B.
            Room(root, material, -14f, -2f, 2f, 12f);
            Room(root, material, 2f, 14f, 2f, 12f);

            // Toilets (the only signal) and the CPE's office (detention).
            Room(root, material, -6f, 2f, -8f, -2f);
            Room(root, material, 10f, 18f, -8f, -2f);

            // Desks, so the classrooms read as classrooms and there is something to hide behind.
            var desk = SchoolPrefabs.Material("Desk", new Color(0.55f, 0.38f, 0.22f));
            foreach (var x in new[] { -11f, -8f, -5f, 5f, 8f, 11f })
            {
                Block(root, desk, "Desk", new Vector3(x, 0.4f, 8.5f), new Vector3(1.6f, 0.8f, 0.8f));
                Block(root, desk, "Desk", new Vector3(x, 0.4f, 5.5f), new Vector3(1.6f, 0.8f, 0.8f));
            }
        }

        public static void BuildLayout()
        {
            var layoutObject = new GameObject("SchoolLayout");

            var detention = new GameObject("DetentionPoint").transform;
            detention.SetParent(layoutObject.transform, false);
            detention.SetPositionAndRotation(new Vector3(14f, 0.1f, -6f), Quaternion.LookRotation(Vector3.forward));

            var waypoints = new[]
            {
                new Vector3(17f, 0f, 0f), new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 7f),
                new Vector3(8f, 0f, 0f), new Vector3(-8f, 0f, 0f), new Vector3(-8f, 0f, 7f),
                new Vector3(-8f, 0f, 0f), new Vector3(-17f, 0f, 0f)
            };

            var routeRoot = new GameObject("PatrolRoute").transform;
            routeRoot.SetParent(layoutObject.transform, false);
            var route = new Transform[waypoints.Length];
            for (var i = 0; i < waypoints.Length; i++)
            {
                route[i] = new GameObject($"Waypoint_{i}").transform;
                route[i].SetParent(routeRoot, false);
                route[i].position = waypoints[i];
            }

            var layout = layoutObject.AddComponent<SchoolLayout>();
            var serialized = new SerializedObject(layout);
            serialized.FindProperty("detentionPoint").objectReferenceValue = detention;
            var routeProperty = serialized.FindProperty("patrolRoute");
            routeProperty.arraySize = route.Length;
            for (var i = 0; i < route.Length; i++)
            {
                routeProperty.GetArrayElementAtIndex(i).objectReferenceValue = route[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Room(Transform root, Material material, float xMin, float xMax, float zMin, float zMax)
        {
            // The side touching the corridor is already built, with its door.
            var farZ = Mathf.Abs(zMin) > Mathf.Abs(zMax) ? zMin : zMax;
            WallAlongX(root, material, farZ, xMin, xMax);
            WallAlongZ(root, material, xMin, zMin, zMax);
            WallAlongZ(root, material, xMax, zMin, zMax);
        }

        /// <summary>A wall running east-west at <paramref name="z"/>, split around each door centre.</summary>
        private static void WallAlongX(Transform root, Material material, float z, float xMin, float xMax,
                                       params float[] doorCentres)
        {
            var start = xMin;
            foreach (var door in doorCentres)
            {
                Segment(root, material, new Vector3(start, 0f, z), new Vector3(door - DoorWidth / 2f, 0f, z));
                start = door + DoorWidth / 2f;
            }

            Segment(root, material, new Vector3(start, 0f, z), new Vector3(xMax, 0f, z));
        }

        private static void WallAlongZ(Transform root, Material material, float x, float zMin, float zMax)
        {
            Segment(root, material, new Vector3(x, 0f, zMin), new Vector3(x, 0f, zMax));
        }

        private static void Segment(Transform root, Material material, Vector3 from, Vector3 to)
        {
            var length = Vector3.Distance(from, to);
            if (length < 0.01f)
            {
                return;
            }

            var alongX = Mathf.Abs(to.x - from.x) > Mathf.Abs(to.z - from.z);
            var size = alongX
                ? new Vector3(length + WallThickness, WallHeight, WallThickness)
                : new Vector3(WallThickness, WallHeight, length + WallThickness);

            Block(root, material, "Wall", (from + to) / 2f + Vector3.up * (WallHeight / 2f), size);
        }

        private static void Block(Transform root, Material material, string name, Vector3 centre, Vector3 size)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(root, false);
            block.transform.position = centre;
            block.transform.localScale = size;
            block.GetComponent<Renderer>().sharedMaterial = material;
            block.isStatic = true;
        }
    }
}
