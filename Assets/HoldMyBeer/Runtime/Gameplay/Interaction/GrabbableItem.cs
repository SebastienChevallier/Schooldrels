using HoldMyBeer.Core;
using HoldMyBeer.Interaction;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Interaction
{
    /// <summary>
    /// An object a player can pick up. The holder lives here and nowhere else: the
    /// player replicates nothing about what it carries, so the two can never disagree.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GrabbableItem : NetworkBehaviour, IGrabbable
    {
        public const ulong NoHolder = ulong.MaxValue;

        [SerializeField] private Transform gripAnchor;

        private readonly NetworkVariable<ulong> _holder = new(
            NoHolder,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<byte> _hand = new(
            (byte)HandSide.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Rigidbody _body;
        private NetworkTransform _networkTransform;
        private IHeldItemTracker _tracker;

        public Transform Body => transform;
        public Transform GripAnchor => gripAnchor != null ? gripAnchor : transform;
        public float Mass => _body != null ? _body.mass : 1f;
        public bool IsHeld => _holder.Value != NoHolder;
        public ulong HolderClientId => _holder.Value;
        public HandSide Hand => (HandSide)_hand.Value;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _networkTransform = GetComponent<NetworkTransform>();
        }

        public override void OnNetworkSpawn()
        {
            if (AppServices.IsReady)
            {
                AppServices.Container.TryResolve(out _tracker);
            }

            _holder.OnValueChanged += OnHolderChanged;
            ApplyHeldState(NoHolder, _holder.Value);
        }

        public override void OnNetworkDespawn()
        {
            _holder.OnValueChanged -= OnHolderChanged;

            // A player holding this when it despawns must not keep a dangling
            // reference: publish the vacancy before disappearing.
            _tracker?.Publish(this, _holder.Value);
        }

        /// <summary>Server only. Returns false when someone else already has it.</summary>
        public bool TryHold(ulong clientId, HandSide hand)
        {
            if (!IsServer || IsHeld)
            {
                return false;
            }

            _holder.Value = clientId;
            _hand.Value = (byte)hand;
            return true;
        }

        /// <summary>Server only.</summary>
        public void ReleaseTo(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (!IsServer)
            {
                return;
            }

            var previous = _holder.Value;
            _holder.Value = NoHolder;
            _hand.Value = (byte)HandSide.None;

            // Written after clearing the holder so the re-enabled NetworkTransform
            // replicates the release pose rather than the last attached one.
            transform.SetPositionAndRotation(position, rotation);
            _body.linearVelocity = velocity;
            _body.angularVelocity = Vector3.zero;

            _tracker?.Publish(this, previous);
        }

        private void OnHolderChanged(ulong previous, ulong current)
        {
            ApplyHeldState(previous, current);
            _tracker?.Publish(this, previous);
        }

        /// <summary>
        /// Runs on every peer. While held, the item is driven locally by whichever
        /// hand this machine simulates, so physics and replication must both let go
        /// of it or they would fight the attachment and stream a diverging position.
        /// </summary>
        private void ApplyHeldState(ulong previous, ulong current)
        {
            var held = current != NoHolder;

            _body.isKinematic = held;
            _body.detectCollisions = !held;

            if (_networkTransform != null)
            {
                _networkTransform.enabled = !held;
            }
        }
    }
}
