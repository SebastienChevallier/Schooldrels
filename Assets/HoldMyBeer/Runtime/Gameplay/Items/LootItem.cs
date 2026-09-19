using HoldMyBeer.Core;
using HoldMyBeer.Gameplay.Day;
using HoldMyBeer.Gameplay.Interaction;
using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Items
{
    /// <summary>
    /// Something worth stealing: the school PC. Heavy enough to be seen and to slow
    /// you down, worth enough that the team will want one out of the building.
    ///
    /// It is not a new mechanic — a heavy grabbable plus an exit zone plus the
    /// suspicion that already exists. The objective is what the players make of it.
    /// </summary>
    [RequireComponent(typeof(GrabbableItem))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class LootItem : NetworkBehaviour
    {
        [SerializeField] private string label = "sort un PC du lycée";
        [SerializeField, Min(0)] private int reputation = 120;
        // Where it goes back to when the theft fails: the room it came from.
        private Vector3 _home;
        private Quaternion _homeRotation;

        private readonly NetworkVariable<bool> _delivered = new();

        private GrabbableItem _item;
        private MischiefBus _mischief;

        public string Label => label;
        public int Reputation => reputation;
        public bool Delivered => _delivered.Value;

        private void Awake() => _item = GetComponent<GrabbableItem>();

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                return;
            }

            _home = transform.position;
            _homeRotation = transform.rotation;

            if (AppServices.IsReady)
            {
                AppServices.Container.TryResolve(out _mischief);
            }
        }

        /// <summary>Server only. Credits the carrier and puts the loot back where it lives.</summary>
        public bool TryDeliver(ulong clientId)
        {
            if (!IsServer || _delivered.Value)
            {
                return false;
            }

            _delivered.Value = true;
            _mischief?.Publish(new MischiefReport(clientId, transform.position, reputation, 0f, label));
            return true;
        }

        /// <summary>Server only. Caught with it: the theft is off and the PC goes home.</summary>
        public void ReturnHome()
        {
            if (!IsServer)
            {
                return;
            }

            if (_item.IsHeld)
            {
                _item.ReleaseTo(_home, _homeRotation, Vector3.zero);
                return;
            }

            transform.SetPositionAndRotation(_home, _homeRotation);
        }
    }
}
