using HoldMyBeer.Core;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Player
{
    /// <summary>
    /// The server asks, the owner moves: the owner is the only peer whose position
    /// write survives replication.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerTeleporter : NetworkBehaviour, ITeleportable
    {
        private CharacterController _controller;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            if (IsServer && IsSpawned)
            {
                TeleportOwnerRpc(position, rotation);
            }
        }

        [Rpc(SendTo.Owner)]
        private void TeleportOwnerRpc(Vector3 position, Quaternion rotation)
        {
            // The CharacterController latches its PhysX position while enabled, so a
            // plain transform write would be silently undone on the next Move.
            var wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = wasEnabled;
        }
    }
}
