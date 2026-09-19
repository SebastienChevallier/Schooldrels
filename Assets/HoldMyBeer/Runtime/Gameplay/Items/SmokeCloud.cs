using HoldMyBeer.Core;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Items
{
    /// <summary>
    /// The one thing in the game that takes an adult's senses away, so it is bounded
    /// in space and in time, and both bounds are server truth: the deadline is a
    /// replicated server timestamp, not a local timer that would drift per machine.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class SmokeCloud : NetworkBehaviour
    {
        [SerializeField, Min(0.5f)] private float radius = 4f;
        [SerializeField, Min(1f)] private float lifetimeSeconds = 12f;
        [SerializeField] private Transform visual;

        private readonly NetworkVariable<double> _endsAt = new();

        private SmokeField _field;

        public float Radius => radius;

        public override void OnNetworkSpawn()
        {
            if (AppServices.IsReady && AppServices.Container.TryResolve(out _field))
            {
                _field.Add(this);
            }

            if (IsServer)
            {
                _endsAt.Value = NetworkManager.ServerTime.Time + lifetimeSeconds;
            }
        }

        public override void OnNetworkDespawn() => _field?.Remove(this);

        private void Update()
        {
            if (!IsSpawned || _endsAt.Value <= 0d)
            {
                return;
            }

            if (visual != null)
            {
                // Puffs up fast, then thins out: readable from across a room, which is
                // what a player needs to decide whether to run through it.
                var left = (float)(_endsAt.Value - NetworkManager.ServerTime.Time);
                var fade = Mathf.Clamp01(left / lifetimeSeconds);
                visual.localScale = Vector3.one * radius * 2f * Mathf.Lerp(0.6f, 1f, fade);
            }

            if (IsServer && NetworkManager.ServerTime.Time >= _endsAt.Value)
            {
                NetworkObject.Despawn();
            }
        }
    }
}
