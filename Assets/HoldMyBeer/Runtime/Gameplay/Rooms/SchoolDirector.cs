using System.Collections;
using System.Collections.Generic;
using HoldMyBeer.Core;
using HoldMyBeer.Gameplay.Day;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Rooms
{
    /// <summary>
    /// The server's stage manager. It owns everything that opens, closes, appears and
    /// disappears as the day moves from phase to phase: the doors, the keys, the items
    /// of the class being taught, the food at lunch, and the adults on duty.
    ///
    /// It exists so that no single system has to know about the others: <c>DayState</c>
    /// decides what the day is, and this turns that into objects. Everything here runs
    /// on the server — clients only ever see the results replicate.
    /// </summary>
    public sealed class SchoolDirector : MonoBehaviour
    {
        [Header("Prefabs réseau")]
        [SerializeField] private GameObject doorPrefab;
        [SerializeField] private GameObject keyPrefab;
        [SerializeField] private GameObject supervisorPrefab;
        [Tooltip("Le prof : même composant, réglé pour rester dans la salle où a lieu le cours.")]
        [SerializeField] private GameObject teacherPrefab;

        [Header("Cadence")]
        [Tooltip("Objets spawnés par frame : un pic de spawn entier d'un coup fait décrocher le host.")]
        [SerializeField, Min(1)] private int spawnsPerFrame = 3;

        [Tooltip("Plafond dur d'items de cours vivants en même temps.")]
        [SerializeField, Min(1)] private int maxCourseItems = 64;

        private readonly Dictionary<RoomId, Door> _doors = new();
        private readonly List<NetworkObject> _courseItems = new();
        private readonly List<NetworkObject> _adults = new();

        private NetworkManager _networkManager;
        private IDayStateProvider _provider;
        private ISchoolLayout _layout;
        private DayState _day;
        private Coroutine _populating;
        private bool _detentionWasOpen;

        private void Start()
        {
            _networkManager = NetworkManager.Singleton;
            if (_networkManager == null || !_networkManager.IsServer)
            {
                return;
            }

            if (!AppServices.IsReady ||
                !AppServices.Container.TryResolve(out _layout) ||
                !AppServices.Container.TryResolve(out _provider))
            {
                Debug.LogError("[Hold My Beer] SchoolDirector: pas de layout ou de jour. La scène Boot a-t-elle tourné ?");
                return;
            }

            SpawnDoors();
            SpawnKeys();

            _provider.CurrentChanged += Attach;
            Attach(_provider.Current);
        }

        private void OnDestroy()
        {
            if (_provider != null)
            {
                _provider.CurrentChanged -= Attach;
            }

            Detach();
        }

        private void Attach(IDayState day)
        {
            Detach();

            _day = day as DayState;
            if (_day == null)
            {
                return;
            }

            _day.PhaseChanged += HandlePhaseChanged;
            HandlePhaseChanged(_day.Phase);
        }

        private void Detach()
        {
            if (_day != null)
            {
                _day.PhaseChanged -= HandlePhaseChanged;
                _day = null;
            }
        }

        private void Update()
        {
            if (_day == null || _networkManager == null || !_networkManager.IsServer)
            {
                return;
            }

            RecallEscapees();
            WatchDetentionDoor();
        }

        // ---------------------------------------------------------------- doors

        private void SpawnDoors()
        {
            if (doorPrefab == null)
            {
                Debug.LogError("[Hold My Beer] SchoolDirector n'a pas de prefab de porte. " +
                               "Relance Tools > Hold My Beer > Generate Project Assets.");
                return;
            }

            foreach (var room in _layout.Rooms)
            {
                if (room == null || room.DoorMarker == null || _doors.ContainsKey(room.Id))
                {
                    continue;
                }

                var instance = Instantiate(doorPrefab, room.DoorMarker.position, room.DoorMarker.rotation);
                var door = instance.GetComponent<Door>();

                // Configured before Spawn so the room id is part of the very first
                // snapshot a client receives, never a frame later.
                door.Configure(room.Id);
                instance.GetComponent<NetworkObject>().Spawn();
                _doors[room.Id] = door;
            }
        }

        private void SpawnKeys()
        {
            if (keyPrefab == null || !_layout.TryGetRoom(RoomId.Staff, out var staff))
            {
                return;
            }

            var markers = staff.ItemMarkers;
            var index = 0;
            foreach (var room in _layout.Rooms)
            {
                if (room == null || room.Id is RoomId.Corridor or RoomId.Toilets or RoomId.Staff ||
                    markers.Length == 0)
                {
                    continue;
                }

                var marker = markers[index % markers.Length];
                var instance = Instantiate(keyPrefab, marker.position + Vector3.up * 0.1f * index, marker.rotation);
                instance.GetComponent<KeyItem>().SetRoom(room.Id);
                instance.GetComponent<NetworkObject>().Spawn();
                index++;
            }
        }

        public bool TryGetDoor(RoomId room, out Door door) => _doors.TryGetValue(room, out door);

        // ---------------------------------------------------------------- phases

        private void HandlePhaseChanged(DayPhase phase)
        {
            if (_networkManager == null || !_networkManager.IsServer || _day == null)
            {
                return;
            }

            var openRoom = phase switch
            {
                DayPhase.Class1 or DayPhase.Class2 => _day.CurrentCourseRoom,
                DayPhase.Lunch => RoomId.Cafeteria,
                _ => RoomId.None
            };

            ApplyDoors(openRoom);
            RepopulateItems(phase, openRoom);
            RefreshAdults();
        }

        /// <summary>
        /// The room in use is forced open for the whole phase, whatever the morning's
        /// draw said. Everything else goes back to the draw: locked 80% of the time.
        /// </summary>
        private void ApplyDoors(RoomId openRoom)
        {
            foreach (var (room, door) in _doors)
            {
                if (door == null)
                {
                    continue;
                }

                if (room == openRoom)
                {
                    door.SetOpen(true, false);
                    continue;
                }

                // The detention door is never reset by a phase: whoever opened it
                // opened it, and the people inside walked out.
                if (room == RoomId.Detention)
                {
                    continue;
                }

                door.SetOpen(false, _day.IsRoomLocked(room));
            }
        }

        // ---------------------------------------------------------------- items

        private void RepopulateItems(DayPhase phase, RoomId openRoom)
        {
            DespawnCourseItems();

            var course = phase == DayPhase.Lunch ? _day.Menu : _day.CourseFor(phase);
            if (course == null || !_layout.TryGetRoom(openRoom, out var room) || room.ItemMarkers.Length == 0)
            {
                return;
            }

            if (_populating != null)
            {
                StopCoroutine(_populating);
            }

            _populating = StartCoroutine(Populate(course, room));
        }

        /// <summary>
        /// Spread over frames on purpose: a class opening spawns its whole kit at once,
        /// and a single-frame burst of NetworkObjects is exactly what drops a host.
        /// </summary>
        private IEnumerator Populate(CourseDefinition course, Room room)
        {
            var markers = room.ItemMarkers;
            var spawnedThisFrame = 0;

            for (var i = 0; i < markers.Length && _courseItems.Count < maxCourseItems; i++)
            {
                var prefab = course.Items.Length > 0 ? course.Items[i % course.Items.Length] : null;
                if (prefab == null || markers[i] == null)
                {
                    continue;
                }

                var instance = Instantiate(prefab, markers[i].position, markers[i].rotation);
                var networkObject = instance.GetComponent<NetworkObject>();
                networkObject.Spawn();
                _courseItems.Add(networkObject);

                if (++spawnedThisFrame >= spawnsPerFrame)
                {
                    spawnedThisFrame = 0;
                    yield return null;
                }
            }

            _populating = null;
        }

        private void DespawnCourseItems()
        {
            foreach (var item in _courseItems)
            {
                if (item != null && item.IsSpawned)
                {
                    item.Despawn();
                }
            }

            _courseItems.Clear();
        }

        // ---------------------------------------------------------------- adults

        /// <summary>
        /// One supervisor plus whatever the tier adds. Spawned once and kept: adults
        /// coming and going mid-cycle would make the school unreadable.
        /// </summary>
        private void RefreshAdults()
        {
            if (supervisorPrefab == null)
            {
                return;
            }

            var tier = _day.Tier;
            var route = _layout.PatrolRoute;

            // One teacher, then the hall supervisors this tier calls for. The teacher
            // is first so that it exists from the very first class of the very first day.
            var wanted = 1 + 1 + (tier != null ? tier.ExtraAdults : 0);

            for (var i = _adults.Count; i < wanted; i++)
            {
                var prefab = i == 0 && teacherPrefab != null ? teacherPrefab : supervisorPrefab;
                var pose = route is { Length: > 0 } ? route[i % route.Length] : null;
                var position = pose != null ? pose.position : transform.position;
                var instance = Instantiate(prefab, position, Quaternion.identity);
                var networkObject = instance.GetComponent<NetworkObject>();
                networkObject.Spawn();
                _adults.Add(networkObject);
            }
        }

        // ---------------------------------------------------------------- detention

        /// <summary>
        /// Detention is a place, not a UI: a student who walks out is walked back in.
        /// Never an input lock — the player keeps their hands and their mouth.
        /// </summary>
        private void RecallEscapees()
        {
            if (_layout.DetentionPoint == null || !_layout.TryGetRoom(RoomId.Detention, out var room))
            {
                return;
            }

            foreach (var client in _networkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null || !_day.IsDetained(client.ClientId))
                {
                    continue;
                }

                if (!room.Contains(client.PlayerObject.transform.position) &&
                    client.PlayerObject.TryGetComponent<ITeleportable>(out var teleportable))
                {
                    teleportable.TeleportTo(_layout.DetentionPoint.position, _layout.DetentionPoint.rotation);
                }
            }
        }

        private void WatchDetentionDoor()
        {
            if (!_doors.TryGetValue(RoomId.Detention, out var door) || door == null)
            {
                return;
            }

            if (door.IsOpen && !_detentionWasOpen)
            {
                _day.ReleaseDetainees();
            }

            _detentionWasOpen = door.IsOpen;
        }
    }
}
