using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Items
{
    /// <summary>
    /// The home-made blowgun: the only way to cause trouble without being next to it.
    /// Wound up on hold, fired on release, silent.
    ///
    /// That distance is its whole point, so it is deliberately the item with the
    /// smallest payout: being safe should not also be the best paid.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class Blowgun : NetworkBehaviour, IUsableItem
    {
        [SerializeField] private GameObject pelletPrefab;
        [SerializeField, Min(1f)] private float minSpeed = 12f;
        [SerializeField, Min(1f)] private float maxSpeed = 26f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 1.2f;

        private readonly NetworkVariable<double> _readyAt = new();

        public bool IsCharged => true;

        public bool CanUse(in InteractionRequest request, out string prompt)
        {
            prompt = null;

            if (!IsSpawned || pelletPrefab == null || NetworkManager.ServerTime.Time < _readyAt.Value)
            {
                return false;
            }

            prompt = "Souffler";
            return true;
        }

        public void Use(in InteractionRequest request, IInteractionContext context)
        {
            if (!IsServer || NetworkManager.ServerTime.Time < _readyAt.Value)
            {
                return;
            }

            _readyAt.Value = NetworkManager.ServerTime.Time + cooldownSeconds;

            var speed = Mathf.Lerp(minSpeed, maxSpeed, request.Charge);
            var origin = request.HandPosition + request.AimDirection * 0.4f;
            context.SpawnItem(pelletPrefab, origin, Quaternion.LookRotation(request.AimDirection),
                request.AimDirection * speed);
        }
    }
}
