using HoldMyBeer.Core;
using HoldMyBeer.Gameplay.Day;
using HoldMyBeer.Gameplay.Interaction;
using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Items
{
    /// <summary>
    /// Something you plant and walk away from — the Arduino on a school PC. Armed when
    /// it is put down, it goes off much later, somewhere else, while its owner is in
    /// another room with an alibi.
    ///
    /// It is the only prank in the game that rewards planning rather than nerve, which
    /// is why the delay is long enough to cross a phase.
    /// </summary>
    [RequireComponent(typeof(GrabbableItem))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DelayedPrank : NetworkBehaviour
    {
        [SerializeField] private string label = "fait planter un poste";
        [SerializeField, Min(0)] private int reputation = 40;
        [SerializeField, Min(0f)] private float delaySeconds = 45f;
        [SerializeField, Min(0f)] private float noiseRadius = 18f;
        [SerializeField] private Renderer statusRenderer;

        /// <summary>Armed and its deadline: state, not an event — a latecomer must see it ticking.</summary>
        private readonly NetworkVariable<double> _firesAt = new();

        private GrabbableItem _item;
        private MischiefBus _mischief;
        private ulong _planter;
        private bool _wasHeld;
        private Color _restColor;

        private void Awake()
        {
            _item = GetComponent<GrabbableItem>();
            if (statusRenderer != null)
            {
                _restColor = statusRenderer.sharedMaterial.color;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && AppServices.IsReady)
            {
                AppServices.Container.TryResolve(out _mischief);
            }
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (statusRenderer != null)
            {
                var armed = _firesAt.Value > 0d;
                statusRenderer.material.color =
                    armed && Mathf.Repeat(Time.time * 2f, 1f) < 0.5f ? Color.green : _restColor;
            }

            if (!IsServer)
            {
                return;
            }

            var held = _item.IsHeld;
            if (held)
            {
                _planter = _item.HolderClientId;
            }
            else if (_wasHeld && _firesAt.Value <= 0d)
            {
                _firesAt.Value = NetworkManager.ServerTime.Time + delaySeconds;
            }

            _wasHeld = held;

            if (_firesAt.Value > 0d && NetworkManager.ServerTime.Time >= _firesAt.Value)
            {
                _firesAt.Value = 0d;
                _mischief?.Publish(new MischiefReport(
                    _planter, transform.position, reputation, noiseRadius, label));
            }
        }
    }
}
