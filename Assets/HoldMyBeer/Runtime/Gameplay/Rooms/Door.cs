using HoldMyBeer.Gameplay.Day;
using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Rooms
{
    /// <summary>
    /// The door of one room. Whether it is open, and whether it is locked, is shared
    /// truth: both live in NetworkVariables written by the server only. A client that
    /// could write its own door state would be opening every room for itself and for
    /// nobody else — the kind of bug that only shows up in a real match.
    ///
    /// The client asks through <see cref="OpenDoorRule"/>; the server checks the key.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class Door : NetworkBehaviour, IInteractionTarget
    {
        [SerializeField] private Transform leaf;
        [Tooltip("De combien la porte coulisse pour s'ouvrir.")]
        [SerializeField] private Vector3 openOffset = new(0f, 0f, -1.9f);
        [SerializeField, Min(0.1f)] private float slideSeconds = 0.35f;
        [SerializeField] private Renderer lockRenderer;

        private static readonly Color LockedColor = new(0.85f, 0.2f, 0.15f);
        private static readonly Color UnlockedColor = new(0.35f, 0.75f, 0.4f);

        private readonly NetworkVariable<byte> _room = new((byte)RoomId.None);
        private readonly NetworkVariable<bool> _open = new();
        private readonly NetworkVariable<bool> _locked = new(true);

        private Vector3 _closedPosition;
        private RoomId _pendingRoom = RoomId.None;

        public RoomId Room => (RoomId)_room.Value;
        public bool IsOpen => _open.Value;
        public bool IsLocked => _locked.Value;

        public Transform Body => transform;

        /// <summary>A door is always worth aiming at: even locked, it tells you it is locked.</summary>
        public bool IsAvailable => IsSpawned;

        private void Awake()
        {
            if (leaf == null)
            {
                leaf = transform;
            }

            _closedPosition = leaf.localPosition;
        }

        /// <summary>
        /// Server only, before <c>Spawn()</c>. Held in a plain field until the object
        /// is spawned: a NetworkVariable written on an unspawned object is not a
        /// supported path, and the value must be in the very first snapshot.
        /// </summary>
        public void Configure(RoomId room) => _pendingRoom = room;

        public override void OnNetworkSpawn()
        {
            if (IsServer && _pendingRoom != RoomId.None)
            {
                _room.Value = (byte)_pendingRoom;
            }

            _locked.OnValueChanged += HandleLockChanged;
            RefreshLock();
        }

        public override void OnNetworkDespawn() => _locked.OnValueChanged -= HandleLockChanged;

        private void HandleLockChanged(bool previous, bool current) => RefreshLock();

        /// <summary>Server only. Forced open (or shut) by the day's phase.</summary>
        public void SetOpen(bool open, bool locked)
        {
            if (!IsServer)
            {
                return;
            }

            _open.Value = open;
            _locked.Value = locked;
        }

        /// <summary>Server only. The result of a player asking, key in hand.</summary>
        public bool TryOpen(bool hasKey)
        {
            if (!IsServer || _open.Value || (_locked.Value && !hasKey))
            {
                return false;
            }

            _locked.Value = false;
            _open.Value = true;
            return true;
        }

        private void Update()
        {
            if (!IsSpawned || leaf == null)
            {
                return;
            }

            // Cosmetic only, and therefore local: the shared truth is the bool, not
            // where the leaf happens to be this frame.
            var target = _closedPosition + (_open.Value ? openOffset : Vector3.zero);
            leaf.localPosition = Vector3.MoveTowards(
                leaf.localPosition, target, openOffset.magnitude / slideSeconds * Time.deltaTime);
        }

        private void RefreshLock()
        {
            if (lockRenderer != null)
            {
                lockRenderer.material.color = _locked.Value ? LockedColor : UnlockedColor;
            }
        }
    }
}
