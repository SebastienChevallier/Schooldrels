using System.Collections.Generic;
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
    ///   ┌ Loge ┐  ┌── Salle générale ──┐  ┌── Techno ──┐      ┌── Gymnase ──┐   z = 12/16
    ///   │      │  │                    │  │            │      │             │
    /// ──┴──  ──┴──┴─────  ────────  ───┴──┴──  ────────┴──  ──┴─────────────┘   z = 2
    ///    couloir                      (alarme)                                  z = -2
    /// ──┬──  ─────────┬──  ──┬────────┬──  ──────┬────────────┬──
    ///   │   Labo      │  WC  │        │  Self    │   CPE      │                 z = -12
    ///   └─────────────┘──────┘        └──────────┘────────────┘
    ///
    /// Every door is 2 m wide and every room is described once, in <see cref="Rooms"/>:
    /// walls, door marker, item markers and the <c>Room</c> component all come from
    /// the same table, so moving a room is changing four numbers.
    ///
    /// The supervisor paths on a NavMesh built at load from everything under the
    /// School root, so walls and doors can move freely.
    /// </summary>
    public static class SchoolBuilder
    {
        private const float WallHeight = 3f;
        private const float WallThickness = 0.2f;
        private const float DoorWidth = 2f;

        /// <summary>One room of the greybox: its footprint, and where its door sits.</summary>
        private readonly struct RoomSpec
        {
            public RoomSpec(RoomId id, string name, float xMin, float xMax, float zMin, float zMax,
                            float doorX, bool doorOnNorthWall, int itemMarkers)
            {
                Id = id;
                Name = name;
                XMin = xMin;
                XMax = xMax;
                ZMin = zMin;
                ZMax = zMax;
                DoorX = doorX;
                DoorOnNorthWall = doorOnNorthWall;
                ItemMarkers = itemMarkers;
            }

            public RoomId Id { get; }
            public string Name { get; }
            public float XMin { get; }
            public float XMax { get; }
            public float ZMin { get; }
            public float ZMax { get; }

            /// <summary>Where the door pierces the corridor wall.</summary>
            public float DoorX { get; }

            /// <summary>North of the corridor (z = 2) or south of it (z = -2).</summary>
            public bool DoorOnNorthWall { get; }

            public int ItemMarkers { get; }

            public Vector3 Centre => new((XMin + XMax) / 2f, 0f, (ZMin + ZMax) / 2f);
            public Vector3 Size => new(XMax - XMin, 4f, ZMax - ZMin);
            public Vector3 DoorPosition => new(DoorX, 0f, DoorOnNorthWall ? 2f : -2f);
        }

        private static readonly RoomSpec[] Rooms =
        {
            new(RoomId.Staff, "Loge", -21f, -16f, 2f, 8f, -18.5f, true, 3),
            new(RoomId.General, "SalleGenerale", -14f, -2f, 2f, 12f, -8f, true, 6),
            new(RoomId.Techno, "Techno", 2f, 14f, 2f, 12f, 8f, true, 6),
            new(RoomId.Gym, "Gymnase", 16f, 30f, 2f, 16f, 18f, true, 6),
            new(RoomId.Lab, "Labo", -21f, -10f, -12f, -2f, -16f, false, 6),
            new(RoomId.Toilets, "WC", -6f, 2f, -8f, -2f, -2f, false, 0),
            new(RoomId.Cafeteria, "Self", 3f, 10f, -14f, -2f, 6f, false, 8),
            new(RoomId.Detention, "BureauCPE", 12f, 18f, -8f, -2f, 14f, false, 0)
        };

        public static void BuildWalls(GameObject ground)
        {
            var root = new GameObject("School").transform;
            ground.transform.SetParent(root, true);

            var surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            root.gameObject.AddComponent<RuntimeNavMeshBuilder>();
            var material = SchoolPrefabs.Material("Wall", new Color(0.85f, 0.82f, 0.72f));

            // The corridor, pierced once per room that opens onto it.
            WallAlongX(root, material, 2f, -30f, 30f, DoorCentres(northWall: true));
            WallAlongX(root, material, -2f, -30f, 30f, DoorCentres(northWall: false));
            WallAlongZ(root, material, -30f, -2f, 2f);
            WallAlongZ(root, material, 30f, -2f, 2f);

            foreach (var room in Rooms)
            {
                Room(root, material, room);
            }

            // Desks, so the classrooms read as classrooms and there is something to hide behind.
            var desk = SchoolPrefabs.Material("Desk", new Color(0.55f, 0.38f, 0.22f));
            foreach (var x in new[] { -11f, -8f, -5f, 5f, 8f, 11f })
            {
                Block(root, desk, "Desk", new Vector3(x, 0.4f, 8.5f), new Vector3(1.6f, 0.8f, 0.8f));
                Block(root, desk, "Desk", new Vector3(x, 0.4f, 5.5f), new Vector3(1.6f, 0.8f, 0.8f));
            }

            // The canteen counter: the queue runs along it, which is the whole ritual.
            Block(root, desk, "Counter", new Vector3(6.5f, 0.5f, -5f), new Vector3(6f, 1f, 0.8f));
        }

        public static void BuildLayout()
        {
            var layoutObject = new GameObject("SchoolLayout");

            var detention = new GameObject("DetentionPoint").transform;
            detention.SetParent(layoutObject.transform, false);
            detention.SetPositionAndRotation(new Vector3(15f, 0.1f, -6f), Quaternion.LookRotation(Vector3.forward));

            var waypoints = new[]
            {
                new Vector3(24f, 0f, 0f), new Vector3(18f, 0f, 0f), new Vector3(8f, 0f, 0f),
                new Vector3(8f, 0f, 6f), new Vector3(8f, 0f, 0f), new Vector3(-2f, 0f, 0f),
                new Vector3(-8f, 0f, 0f), new Vector3(-8f, 0f, 6f), new Vector3(-8f, 0f, 0f),
                new Vector3(-16f, 0f, 0f), new Vector3(-24f, 0f, 0f)
            };

            var routeRoot = new GameObject("PatrolRoute").transform;
            routeRoot.SetParent(layoutObject.transform, false);
            var route = new Transform[waypoints.Length];
            for (var i = 0; i < route.Length; i++)
            {
                route[i] = new GameObject($"Waypoint_{i}").transform;
                route[i].SetParent(routeRoot, false);
                route[i].position = waypoints[i];
            }

            var rooms = new List<UnityEngine.Object>();
            foreach (var spec in Rooms)
            {
                rooms.Add(BuildRoom(layoutObject.transform, spec));
            }

            var layout = layoutObject.AddComponent<SchoolLayout>();
            var serialized = new SerializedObject(layout);
            serialized.FindProperty("detentionPoint").objectReferenceValue = detention;
            Fill(serialized.FindProperty("patrolRoute"), route);
            Fill(serialized.FindProperty("rooms"), rooms.ToArray());
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// One <c>Room</c> plus its markers. The door marker sits in the corridor wall
        /// and faces the room, because that is where <c>SchoolDirector</c> spawns the
        /// networked door at runtime — doors are spawned prefabs like every other
        /// networked object, never scene objects.
        /// </summary>
        private static HoldMyBeer.Gameplay.Day.Room BuildRoom(Transform parent, RoomSpec spec)
        {
            var roomObject = new GameObject($"Room_{spec.Name}");
            roomObject.transform.SetParent(parent, false);
            roomObject.transform.position = spec.Centre;

            var doorMarker = new GameObject("Door").transform;
            doorMarker.SetParent(roomObject.transform, false);
            doorMarker.position = spec.DoorPosition;
            doorMarker.rotation = Quaternion.LookRotation(spec.DoorOnNorthWall ? Vector3.forward : Vector3.back);

            var markersRoot = new GameObject("ItemMarkers").transform;
            markersRoot.SetParent(roomObject.transform, false);

            var markers = new UnityEngine.Object[spec.ItemMarkers];
            for (var i = 0; i < spec.ItemMarkers; i++)
            {
                var marker = new GameObject($"Item_{i}").transform;
                marker.SetParent(markersRoot, false);

                // Spread along the room, a metre off the floor: items drop onto desks
                // and counters rather than clipping through them.
                var t = (i + 0.5f) / spec.ItemMarkers;
                marker.position = new Vector3(
                    Mathf.Lerp(spec.XMin + 1.5f, spec.XMax - 1.5f, t),
                    1f,
                    Mathf.Lerp(spec.ZMin + 1.5f, spec.ZMax - 1.5f, i % 2 == 0 ? 0.35f : 0.75f));
                markers[i] = marker;
            }

            var teacherRoot = new GameObject("TeacherRoute").transform;
            teacherRoot.SetParent(roomObject.transform, false);
            var teacherRoute = new UnityEngine.Object[3];
            for (var i = 0; i < teacherRoute.Length; i++)
            {
                var point = new GameObject($"Teacher_{i}").transform;
                point.SetParent(teacherRoot, false);
                point.position = new Vector3(
                    Mathf.Lerp(spec.XMin + 2f, spec.XMax - 2f, i / 2f),
                    0f,
                    Mathf.Lerp(spec.ZMin + 2f, spec.ZMax - 2f, 0.15f));
                teacherRoute[i] = point;
            }

            var room = roomObject.AddComponent<HoldMyBeer.Gameplay.Day.Room>();
            var serialized = new SerializedObject(room);
            serialized.FindProperty("id").enumValueIndex = (int)spec.Id;
            serialized.FindProperty("size").vector3Value = spec.Size;
            serialized.FindProperty("doorMarker").objectReferenceValue = doorMarker;
            Fill(serialized.FindProperty("itemMarkers"), markers);
            Fill(serialized.FindProperty("teacherRoute"), teacherRoute);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return room;
        }

        private static void Fill(SerializedProperty array, UnityEngine.Object[] values)
        {
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static float[] DoorCentres(bool northWall)
        {
            var centres = new List<float>();
            foreach (var room in Rooms)
            {
                if (room.DoorOnNorthWall == northWall)
                {
                    centres.Add(room.DoorX);
                }
            }

            centres.Sort();
            return centres.ToArray();
        }

        private static void Room(Transform root, Material material, RoomSpec spec)
        {
            // The side touching the corridor is already built, with its door.
            var farZ = spec.DoorOnNorthWall ? spec.ZMax : spec.ZMin;
            WallAlongX(root, material, farZ, spec.XMin, spec.XMax);
            WallAlongZ(root, material, spec.XMin, spec.ZMin, spec.ZMax);
            WallAlongZ(root, material, spec.XMax, spec.ZMin, spec.ZMax);
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
