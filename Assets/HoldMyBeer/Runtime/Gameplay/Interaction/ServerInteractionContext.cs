using HoldMyBeer.Gameplay.Day;
using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Interaction
{
    /// <summary>
    /// The server-side implementation of what a rule may do. Every value that came
    /// from a client is bounded here rather than in the rules, so a new rule cannot
    /// accidentally forget to sanitise a throw.
    /// </summary>
    public sealed class ServerInteractionContext : IInteractionContext
    {
        private readonly NetworkManager _networkManager;
        private readonly float _maxThrowSpeed;
        private readonly MischiefBus _mischief;
        private readonly IDayStateProvider _day;

        public ServerInteractionContext(NetworkManager networkManager, float maxThrowSpeed, MischiefBus mischief,
                                        IDayStateProvider day)
        {
            _networkManager = networkManager;
            _maxThrowSpeed = maxThrowSpeed;
            _mischief = mischief;
            _day = day;
        }

        public void ReportMischief(in MischiefReport report)
        {
            if (_networkManager.IsServer)
            {
                _mischief.Publish(in report);
            }
        }

        public bool BankReputation(ulong clientId)
        {
            return _networkManager.IsServer && _day.Current is DayState day && day.Bank(clientId);
        }

        public bool Hold(IGrabbable item, ulong clientId, HandSide hand)
        {
            return _networkManager.IsServer
                   && item is GrabbableItem concrete
                   && concrete.TryHold(clientId, hand);
        }

        public bool Release(IGrabbable item, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (!_networkManager.IsServer || item is not GrabbableItem concrete)
            {
                return false;
            }

            concrete.ReleaseTo(position, rotation, Clamp(velocity));
            return true;
        }

        public void Despawn(IGrabbable item)
        {
            if (_networkManager.IsServer && item is GrabbableItem concrete && concrete.IsSpawned)
            {
                concrete.NetworkObject.Despawn();
            }
        }

        /// <summary>
        /// The throw velocity is reported by the thrower, because the server cannot
        /// know where an unreplicated ragdoll hand was. Capping the magnitude removes
        /// the only case that actually spoils a match: the item fired across the map.
        /// </summary>
        private Vector3 Clamp(Vector3 velocity)
        {
            if (!float.IsFinite(velocity.sqrMagnitude))
            {
                return Vector3.zero;
            }

            return velocity.sqrMagnitude > _maxThrowSpeed * _maxThrowSpeed
                ? velocity.normalized * _maxThrowSpeed
                : velocity;
        }
    }
}
