using HoldMyBeer.Core;
using HoldMyBeer.Gameplay.Day;
using HoldMyBeer.Gameplay.Interaction;
using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Pranks
{
    /// <summary>
    /// A grabbable that lights when it leaves the hand and goes off a moment later.
    /// No rule of its own: <c>ThrowRule</c> already throws it, and the fuse only
    /// watches the holder change. The delay is the gameplay — thrown near an adult,
    /// the blast is heard, but the thrower may already be around the corner.
    /// </summary>
    [RequireComponent(typeof(GrabbableItem))]
    public sealed class Firecracker : NetworkBehaviour
    {
        [SerializeField, Min(0f)] private float fuseSeconds = 2.5f;
        [SerializeField, Min(0)] private int reputation = 25;
        [SerializeField, Min(0f)] private float noiseRadius = 25f;
        [SerializeField, Min(0f)] private float blastRadius = 4f;
        [SerializeField, Min(0f)] private float blastForce = 9f;
        [SerializeField, Min(0f)] private float rearmSeconds = 20f;
        [SerializeField] private Renderer fuseRenderer;

        private readonly NetworkVariable<bool> _lit = new();

        private GrabbableItem _item;
        private Rigidbody _body;
        private MischiefBus _mischief;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private bool _wasHeld;
        private ulong _lastHolder;
        private float _explodeAt;
        private float _rearmAt;
        private Color _restColor;

        private void Awake()
        {
            _item = GetComponent<GrabbableItem>();
            _body = GetComponent<Rigidbody>();

            if (fuseRenderer != null)
            {
                _restColor = fuseRenderer.sharedMaterial.color;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _homePosition = transform.position;
                _homeRotation = transform.rotation;

                if (AppServices.IsReady)
                {
                    AppServices.Container.TryResolve(out _mischief);
                }
            }
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (fuseRenderer != null)
            {
                fuseRenderer.material.color =
                    _lit.Value && Mathf.Repeat(Time.time * 8f, 1f) < 0.5f ? Color.yellow : _restColor;
            }

            if (IsServer)
            {
                TickServer();
            }
        }

        private void TickServer()
        {
            if (_rearmAt > 0f)
            {
                if (Time.time >= _rearmAt)
                {
                    _rearmAt = 0f;
                }

                return;
            }

            var held = _item.IsHeld;
            if (held)
            {
                _lastHolder = _item.HolderClientId;
            }
            else if (_wasHeld && !_lit.Value)
            {
                _lit.Value = true;
                _explodeAt = Time.time + fuseSeconds;
            }

            _wasHeld = held;

            // Grabbed back while lit: the fuse keeps burning. Holding it is on you.
            if (_lit.Value && Time.time >= _explodeAt)
            {
                Explode();
            }
        }

        private void Explode()
        {
            var position = transform.position;
            _lit.Value = false;

            foreach (var hit in Physics.OverlapSphere(position, blastRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.attachedRigidbody != null && hit.attachedRigidbody != _body && !hit.attachedRigidbody.isKinematic)
                {
                    hit.attachedRigidbody.AddExplosionForce(blastForce, position, blastRadius, 0.5f,
                        ForceMode.VelocityChange);
                }
            }

            _mischief?.Publish(new MischiefReport(_lastHolder, position, reputation, noiseRadius, "PÉTARD !"));
            BlastRpc(position);

            if (_item.IsHeld)
            {
                // Went off in someone's hand: drop it where they stand before sending it home.
                _item.ReleaseTo(position, transform.rotation, Vector3.zero);
            }

            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(_homePosition, _homeRotation);
            _wasHeld = false;
            _rearmAt = Time.time + rearmSeconds;
        }

        [Rpc(SendTo.Everyone)]
        private void BlastRpc(Vector3 position)
        {
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(flash.GetComponent<Collider>());
            flash.transform.position = position;
            flash.transform.localScale = Vector3.one * blastRadius * 0.6f;
            flash.GetComponent<Renderer>().material.color = new Color(1f, 0.8f, 0.2f);

            var light = flash.AddComponent<Light>();
            light.color = new Color(1f, 0.7f, 0.3f);
            light.range = blastRadius * 3f;
            light.intensity = 6f;

            Destroy(flash, 0.15f);
        }
    }
}
