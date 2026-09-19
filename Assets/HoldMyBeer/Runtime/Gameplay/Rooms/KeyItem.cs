using HoldMyBeer.Core;
using HoldMyBeer.Gameplay.Day;
using HoldMyBeer.Gameplay.Interaction;
using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Rooms
{
    /// <summary>
    /// A key to one room. It is an ordinary grabbable — so it can be thrown to a friend
    /// across the corridor, which is the whole point: the key is the thing the team
    /// passes around.
    ///
    /// Taking one is itself mischief, which is what makes the staff room worth the trip.
    /// </summary>
    [RequireComponent(typeof(GrabbableItem))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class KeyItem : NetworkBehaviour
    {
        [SerializeField] private RoomId room = RoomId.General;
        [SerializeField, Min(0)] private int stealReputation = 10;

        /// <summary>Which door it opens — replicated, because a client must not guess it.</summary>
        private readonly NetworkVariable<byte> _room = new((byte)RoomId.None);

        private readonly NetworkVariable<bool> _stolen = new();

        private GrabbableItem _item;
        private MischiefBus _mischief;

        public RoomId Room => _room.Value != (byte)RoomId.None ? (RoomId)_room.Value : room;

        public int StealReputation => stealReputation;

        private void Awake() => _item = GetComponent<GrabbableItem>();

        /// <summary>
        /// Server only, before <c>Spawn()</c>. The value is kept in the plain field and
        /// pushed into the NetworkVariable on spawn: writing a NetworkVariable on an
        /// object that is not spawned yet is not a supported path.
        /// </summary>
        public void SetRoom(RoomId value) => room = value;

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                return;
            }

            _room.Value = (byte)room;

            if (AppServices.IsReady)
            {
                AppServices.Container.TryResolve(out _mischief);
            }
        }

        // Watched rather than hooked into GrabRule: taking a key is mischief whoever
        // took it and however they got it — off the rack, or off the floor where the
        // supervisor dropped it.
        private void Update()
        {
            if (!IsServer || !IsSpawned || !_item.IsHeld || _stolen.Value)
            {
                return;
            }

            _stolen.Value = true;
            _mischief?.Publish(new MischiefReport(
                _item.HolderClientId, transform.position, stealReputation, 0f, "pique une clé"));
        }
    }
}
