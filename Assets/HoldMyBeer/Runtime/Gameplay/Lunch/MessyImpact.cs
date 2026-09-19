using HoldMyBeer.Core;
using HoldMyBeer.Gameplay.Day;
using HoldMyBeer.Gameplay.Interaction;
using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Lunch
{
    /// <summary>
    /// Hitting someone with food. No damage anywhere in this game — only noise, mess
    /// and reputation — so the whole component is: who threw it, who it hit, how loud.
    /// </summary>
    [RequireComponent(typeof(GrabbableItem))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class MessyImpact : NetworkBehaviour
    {
        [SerializeField] private string label = "balance de la bouffe";
        [SerializeField, Min(0)] private int reputation = 8;
        [Tooltip("Bonus quand la cible est un adulte : c'est le geste que tout le monde cherche.")]
        [SerializeField, Min(0)] private int adultBonus = 20;
        [SerializeField, Min(0f)] private float noiseRadius = 10f;
        [SerializeField, Min(0f)] private float minimumSpeed = 3f;

        private GrabbableItem _item;
        private Rigidbody _body;
        private MischiefBus _mischief;
        private ulong _thrower = GrabbableItem.NoHolder;
        private bool _spent;

        private void Awake()
        {
            _item = GetComponent<GrabbableItem>();
            _body = GetComponent<Rigidbody>();
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
            if (IsServer && _item.IsHeld)
            {
                _thrower = _item.HolderClientId;
                _spent = false;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer || _spent || _thrower == GrabbableItem.NoHolder ||
                _body.linearVelocity.magnitude < minimumSpeed)
            {
                return;
            }

            var hit = collision.collider.GetComponentInParent<NetworkObject>();
            if (hit == null || hit.OwnerClientId == _thrower)
            {
                return;
            }

            var isAdult = hit.GetComponentInParent<Adults.Supervisor>() != null;
            if (!isAdult && !hit.IsPlayerObject)
            {
                return;
            }

            _spent = true;
            _mischief?.Publish(new MischiefReport(
                _thrower, transform.position, reputation + (isAdult ? adultBonus : 0), noiseRadius,
                isAdult ? "touche un adulte" : label));
        }
    }
}
