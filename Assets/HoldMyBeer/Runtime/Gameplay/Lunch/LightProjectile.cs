using HoldMyBeer.Gameplay.Interaction;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Lunch
{
    /// <summary>
    /// A throwaway object — food, a blowgun pellet, a piece of chalk — replicated the
    /// cheap way: the launch is an event, the flight is simulated locally on every
    /// peer, and only the resting pose is corrected by the server.
    ///
    /// A potato in mid-air does not need to be identical to the centimetre; a potato
    /// lying on the floor does. That asymmetry is the whole point: it is what lets a
    /// food fight happen at all on a host who is also playing.
    /// </summary>
    [RequireComponent(typeof(GrabbableItem))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class LightProjectile : NetworkBehaviour
    {
        [Tooltip("En dessous de cette vitesse pendant un instant, l'objet est considéré posé.")]
        [SerializeField, Min(0f)] private float restSpeed = 0.35f;
        [SerializeField, Min(0f)] private float restSeconds = 0.4f;

        /// <summary>Where it ended up. The one thing everyone must agree on.</summary>
        private readonly NetworkVariable<Vector3> _restPosition = new();
        private readonly NetworkVariable<float> _restYaw = new();
        private readonly NetworkVariable<bool> _atRest = new(true);

        private GrabbableItem _item;
        private Rigidbody _body;
        private NetworkTransform _networkTransform;
        private bool _wasHeld;
        private float _slowSince;

        private void Awake()
        {
            _item = GetComponent<GrabbableItem>();
            _body = GetComponent<Rigidbody>();
            _networkTransform = GetComponent<NetworkTransform>();
        }

        public override void OnNetworkSpawn()
        {
            _atRest.OnValueChanged += HandleRestChanged;
            _restPosition.OnValueChanged += HandleRestPositionChanged;
        }

        public override void OnNetworkDespawn()
        {
            _atRest.OnValueChanged -= HandleRestChanged;
            _restPosition.OnValueChanged -= HandleRestPositionChanged;
        }

        private void HandleRestPositionChanged(Vector3 previous, Vector3 current) => SnapTo(current, _restYaw.Value);

        private void Update()
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            var held = _item.IsHeld;
            if (held)
            {
                _wasHeld = true;
                return;
            }

            if (_wasHeld)
            {
                _wasHeld = false;
                Launch(transform.position, _body.linearVelocity);
                return;
            }

            if (_atRest.Value)
            {
                return;
            }

            // Settled: publish the resting pose once and let replication take over
            // again. Until then nobody has been streaming this object at all.
            if (_body.linearVelocity.sqrMagnitude > restSpeed * restSpeed)
            {
                _slowSince = 0f;
                return;
            }

            if (_slowSince <= 0f)
            {
                _slowSince = Time.time;
            }
            else if (Time.time - _slowSince >= restSeconds)
            {
                _restPosition.Value = transform.position;
                _restYaw.Value = transform.eulerAngles.y;
                _atRest.Value = true;
            }
        }

        /// <summary>Server only. Sends everyone the same impulse and lets them simulate.</summary>
        private void Launch(Vector3 position, Vector3 velocity)
        {
            _atRest.Value = false;
            _slowSince = 0f;
            LaunchRpc(position, velocity);
        }

        [Rpc(SendTo.Everyone)]
        private void LaunchRpc(Vector3 position, Vector3 velocity)
        {
            // The flight is nobody's authority: every peer runs the same throw from the
            // same starting point, and any drift is settled by the resting pose.
            if (_networkTransform != null)
            {
                _networkTransform.enabled = false;
            }

            _body.isKinematic = false;
            _body.detectCollisions = true;
            transform.position = position;
            _body.linearVelocity = velocity;
            _body.angularVelocity = Random.insideUnitSphere * 4f;
        }

        private void HandleRestChanged(bool previous, bool current)
        {
            if (current)
            {
                SnapTo(_restPosition.Value, _restYaw.Value);
            }
        }

        private void SnapTo(Vector3 position, float yaw)
        {
            if (!_atRest.Value)
            {
                return;
            }

            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;

            if (_networkTransform != null)
            {
                _networkTransform.enabled = true;
            }
        }
    }
}
