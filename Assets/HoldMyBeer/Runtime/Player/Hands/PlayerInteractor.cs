using HoldMyBeer.Core;
using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Player.Hands
{
    /// <summary>
    /// Aims, asks the registry what would happen, and forwards the intent to the
    /// server. Holds no interaction logic of its own: every behaviour lives in a rule,
    /// so adding one never touches this file.
    /// </summary>
    [RequireComponent(typeof(PlayerHands))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerInteractor : NetworkBehaviour
    {
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private float aimDistance = 3f;

        private PlayerHands _hands;
        private IInteractionRegistry _registry;
        private IInteractionContext _context;
        private IHeldItemTracker _tracker;
        private int _aimMask = ~0;

        /// <summary>What the crosshair would do right now, or null. For the UI later.</summary>
        public string Prompt { get; private set; }

        private void Awake()
        {
            _hands = GetComponent<PlayerHands>();

            // The ragdoll must not block aiming: the player's own body hangs in front
            // of the camera and would swallow every ray.
            var ragdollLayer = LayerMask.NameToLayer("PlayerRagdoll");
            _aimMask = ragdollLayer >= 0 ? ~(1 << ragdollLayer) : ~0;
        }

        public override void OnNetworkSpawn()
        {
            // Resolved once here rather than per frame, per CLAUDE.md §6.
            if (AppServices.IsReady)
            {
                AppServices.Container.TryResolve(out _registry);
                AppServices.Container.TryResolve(out _context);
                AppServices.Container.TryResolve(out _tracker);
            }
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            // Runs on every peer: each machine attaches the item to the hand it
            // simulates, so each machine needs to know what this player carries.
            SyncHeldItem();

            if (!IsOwner)
            {
                return;
            }

            UpdateAim();
        }

        private void SyncHeldItem()
        {
            if (_tracker == null)
            {
                return;
            }

            if (_tracker.TryGetHeldItem(OwnerClientId, out var item) && item.IsHeld)
            {
                _hands.SetHeldItem(item, item.Hand);
            }
            else
            {
                _hands.SetHeldItem(null, HandSide.None);
            }
        }

        private void UpdateAim()
        {
            if (cameraPivot == null || _registry == null)
            {
                return;
            }

            var origin = cameraPivot.position;
            var direction = cameraPivot.forward;

            IGrabbable grabbable = null;
            IInteractionTarget target = null;
            var aimPoint = origin + direction * aimDistance;

            if (Physics.Raycast(origin, direction, out var hit, aimDistance, _aimMask,
                    QueryTriggerInteraction.Ignore))
            {
                aimPoint = hit.point;
                grabbable = hit.collider.GetComponentInParent<IGrabbable>();
                target = hit.collider.GetComponentInParent<IInteractionTarget>();
            }

            // Reaching is a local, cosmetic affordance: the hand starts moving towards
            // the object before any round trip, and nothing breaks if the grab is then
            // refused by the server.
            _hands.SetReachTarget(_hands.HeldHand == HandSide.None ? grabbable : null);

            var request = BuildRequest(grabbable, target, aimPoint, direction);
            Prompt = _registry.TryResolve(in request, out _, out var prompt) ? prompt : null;

            if (playerController != null && playerController.Input.InteractPressedThisFrame)
            {
                RequestInteractRpc(
                    ToReference(grabbable),
                    ToReference(target),
                    aimPoint,
                    direction,
                    request.HandPosition,
                    request.HandRotation,
                    request.HandVelocity);
            }
        }

        private InteractionRequest BuildRequest(
            IGrabbable grabbable, IInteractionTarget target, Vector3 aimPoint, Vector3 direction)
        {
            IGrabbable heldItem = null;
            if (_tracker != null && _tracker.TryGetHeldItem(OwnerClientId, out var found) && found.IsHeld)
            {
                heldItem = found;
            }

            return new InteractionRequest(
                OwnerClientId,
                heldItem,
                grabbable,
                target,
                aimPoint,
                direction,
                _hands.ActiveHandPosition,
                _hands.ActiveHandRotation,
                _hands.ActiveHandVelocity);
        }

        [Rpc(SendTo.Server)]
        private void RequestInteractRpc(
            NetworkObjectReference grabbableRef,
            NetworkObjectReference targetRef,
            Vector3 aimPoint,
            Vector3 aimDirection,
            Vector3 handPosition,
            Quaternion handRotation,
            Vector3 handVelocity,
            RpcParams rpcParams = default)
        {
            // A player may only act for themselves. Same rule as the ragdoll collapse,
            // and the same reason: the sender id is the one thing NGO guarantees.
            var sender = rpcParams.Receive.SenderClientId;
            if (sender != OwnerClientId || _registry == null || _context == null)
            {
                return;
            }

            IGrabbable held = null;
            if (_tracker != null && _tracker.TryGetHeldItem(sender, out var found) && found.IsHeld)
            {
                held = found;
            }

            var request = new InteractionRequest(
                sender,
                held,
                Resolve<IGrabbable>(grabbableRef),
                Resolve<IInteractionTarget>(targetRef),
                aimPoint,
                aimDirection,
                handPosition,
                handRotation,
                handVelocity);

            // Re-resolved server-side rather than trusting the client's choice of rule:
            // the client tells us what it aimed at, never what should happen.
            if (_registry.TryResolve(in request, out var rule, out _))
            {
                rule.Apply(in request, _context);
            }
        }

        private static NetworkObjectReference ToReference(object candidate)
        {
            return candidate is Component component &&
                   component.TryGetComponent<NetworkObject>(out var networkObject)
                ? new NetworkObjectReference(networkObject)
                : default;
        }

        private static T Resolve<T>(NetworkObjectReference reference) where T : class
        {
            return reference.TryGet(out var networkObject)
                ? networkObject.GetComponentInParent<T>()
                : null;
        }
    }
}
