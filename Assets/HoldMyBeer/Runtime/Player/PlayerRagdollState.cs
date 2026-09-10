using HoldMyBeer.Player.Wobble;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Player
{
    /// <summary>
    /// The only file in the wobble feature that knows about NGO. No bone is ever
    /// replicated: every client simulates every avatar's ragdoll locally, and only
    /// the target tension crosses the wire.
    /// </summary>
    [RequireComponent(typeof(WobbleRig))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerRagdollState : NetworkBehaviour
    {
        [SerializeField] private float automaticRecoveryDelay = 2f;

        private readonly NetworkVariable<float> _targetTension = new(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private WobbleRig _rig;
        private PlayerController _controller;
        private bool _rootFollowsRagdoll;
        private float _recoveryAt;

        public bool IsCollapsed => _rig != null && _rig.IsCollapsed;

        private void Awake()
        {
            _rig = GetComponent<WobbleRig>();
            _controller = GetComponent<PlayerController>();
        }

        public override void OnNetworkSpawn()
        {
            // PlayerSpawner positions the instance before calling SpawnAsPlayerObject,
            // so the root is already at its spawn pose here — which is exactly what
            // the ragdoll needs, or it is born at the origin and catapulted.
            _rig.Build();
            _rig.SetTargetTension(_targetTension.Value);
            _rig.SetHeadVisible(!IsOwner);

            _targetTension.OnValueChanged += OnTensionChanged;
        }

        public override void OnNetworkDespawn()
        {
            _targetTension.OnValueChanged -= OnTensionChanged;

            // Without this, every player who leaves strands a physics corpse that
            // keeps simulating.
            _rig.Teardown();
        }

        /// <summary>Asks the server to drop this avatar into a full ragdoll.</summary>
        [Rpc(SendTo.Server)]
        public void RequestCollapseRpc(RpcParams rpcParams = default)
        {
            // A player may fell themselves, never someone else. The owner staying free
            // to lie about their own fall is the same trade already accepted for their
            // position under owner-authoritative movement.
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            _targetTension.Value = 0f;
        }

        /// <summary>Asks the server to brace this avatar back up.</summary>
        [Rpc(SendTo.Server)]
        public void RequestRecoverRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            _targetTension.Value = 1f;
        }

        private void Update()
        {
            if (!IsOwner || !IsSpawned || !_rig.IsBuilt)
            {
                return;
            }

            if (!_rig.IsCollapsed)
            {
                _recoveryAt = 0f;
                return;
            }

            if (_recoveryAt <= 0f)
            {
                _recoveryAt = Time.time + automaticRecoveryDelay;
                return;
            }

            if (Time.time >= _recoveryAt)
            {
                _recoveryAt = 0f;
                RequestRecoverRpc();
            }
        }

        private void LateUpdate()
        {
            // Only the owner may move the root: OwnerNetworkTransform replicates it
            // from here, so remote clients get the fall for free.
            if (!IsOwner || !IsSpawned || !_rig.IsBuilt)
            {
                return;
            }

            var shouldFollow = _rig.IsCollapsed;
            if (shouldFollow != _rootFollowsRagdoll)
            {
                _rootFollowsRagdoll = shouldFollow;
                _controller.SetMotorSuspended(shouldFollow);

                if (!shouldFollow)
                {
                    // Recovering: drop the root onto the ground under the pelvis
                    // before the CharacterController takes over again.
                    transform.position = ProjectToGround(_rig.PelvisPosition);
                    _rig.SnapToAnimatedPose();
                    return;
                }
            }

            if (_rootFollowsRagdoll)
            {
                transform.position = _rig.PelvisPosition;
            }
        }

        private void OnTensionChanged(float previous, float current)
        {
            _rig.SetTargetTension(current);
        }

        private static Vector3 ProjectToGround(Vector3 origin)
        {
            return Physics.Raycast(origin + Vector3.up, Vector3.down, out var hit, 5f)
                ? hit.point
                : origin;
        }
    }
}
