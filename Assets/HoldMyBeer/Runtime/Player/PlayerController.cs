using HoldMyBeer.Core;
using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Player
{
    /// <summary>
    /// Drives the local avatar. Everything here runs on the owner only: remote
    /// avatars are moved by <see cref="OwnerNetworkTransform"/> replication, so this
    /// component disables itself on them.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerController : NetworkBehaviour
    {
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener playerAudioListener;
        [SerializeField] private PlayerMovementSettings settings = PlayerMovementSettings.Default;

        [Tooltip("Masse à partir de laquelle un objet tenu commence à ralentir le joueur.")]
        [SerializeField, Min(0.1f)] private float heavyItemMass = 4f;

        [Tooltip("Vitesse minimale en portant le plus lourd des objets.")]
        [SerializeField, Range(0.1f, 1f)] private float heavyItemSlowdown = 0.55f;

        private CharacterController _controller;
        private FirstPersonMotor _motor;
        private IPlayerInputSource _input;
        private PlayerRagdollState _ragdollState;
        private IHeldItemTracker _tracker;
        private float _pitch;
        private bool _motorSuspended;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _motor = new FirstPersonMotor(_controller, settings);
            _input = new KeyboardMousePlayerInputSource(settings.LookSensitivity);
            _ragdollState = GetComponent<PlayerRagdollState>();

            SetLocalRigActive(false);
            enabled = false;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                return;
            }

            SetLocalRigActive(true);
            SetCursorLocked(true);

            // Resolved once, here rather than in Update: the container is not a
            // per-frame lookup table (CLAUDE.md §6).
            if (AppServices.IsReady)
            {
                AppServices.Container.TryResolve(out _tracker);
            }

            enabled = true;
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                SetCursorLocked(false);
            }

            enabled = false;
        }

        /// <summary>
        /// Hands control of the root over to the ragdoll (or takes it back). Called
        /// by <see cref="PlayerRagdollState"/> when the tension crosses the collapse
        /// threshold; the CharacterController and the physics body must never drive
        /// the same transform at once.
        /// </summary>
        /// <summary>
        /// Shared with the hand layer so a second device poll is not spun up for the
        /// same keyboard. Read-only: the controller stays the one that owns it.
        /// </summary>
        public IPlayerInputSource Input => _input;

        public void SetMotorSuspended(bool suspended)
        {
            if (_motorSuspended == suspended)
            {
                return;
            }

            _motorSuspended = suspended;
            _controller.enabled = !suspended;
        }

        private void Update()
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }

            // Escape releases the cursor so the player can reach the menu again.
            if (UnityEngine.InputSystem.Keyboard.current is { escapeKey: { wasPressedThisFrame: true } })
            {
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);
            }

            // Looking around stays available while collapsed: losing the camera on top
            // of losing control of the body is disorienting rather than funny.
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                ApplyLook(_input.Look);
            }

            if (_motorSuspended)
            {
                return;
            }

            _motor.SpeedScale = CarryingSpeedScale();
            _motor.Tick(_input.Move, _input.SprintHeld, _input.JumpPressedThisFrame, Time.deltaTime);

            if (_ragdollState != null &&
                _motor.LandingImpactSpeed > settings.CollapseImpactSpeed)
            {
                _ragdollState.RequestCollapseRpc();
            }
        }

        /// <summary>
        /// Heavy things are meant to be a problem. The mass already lives on the item,
        /// so this needs no new contract — and the player layer still knows nothing
        /// about what a PC or a tray actually is.
        /// </summary>
        private float CarryingSpeedScale()
        {
            if (_tracker == null || !_tracker.TryGetHeldItem(OwnerClientId, out var item) || !item.IsHeld)
            {
                return 1f;
            }

            var excess = Mathf.InverseLerp(heavyItemMass, heavyItemMass * 3f, item.Mass);
            return Mathf.Lerp(1f, heavyItemSlowdown, excess);
        }

        private void ApplyLook(Vector2 look)
        {
            // Yaw turns the body (and is therefore replicated); pitch stays local to
            // the camera pivot so it never fights the network transform.
            transform.Rotate(Vector3.up * look.x, Space.Self);

            _pitch = Mathf.Clamp(_pitch - look.y, -settings.MaxPitch, settings.MaxPitch);
            if (cameraPivot != null)
            {
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private void SetLocalRigActive(bool active)
        {
            if (playerCamera != null)
            {
                playerCamera.enabled = active;
            }

            if (playerAudioListener != null)
            {
                playerAudioListener.enabled = active;
            }
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
