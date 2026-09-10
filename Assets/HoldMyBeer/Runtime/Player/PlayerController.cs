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

        private CharacterController _controller;
        private FirstPersonMotor _motor;
        private IPlayerInputSource _input;
        private PlayerRagdollState _ragdollState;
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

            _motor.Tick(_input.Move, _input.SprintHeld, _input.JumpPressedThisFrame, Time.deltaTime);

            if (_ragdollState != null &&
                _motor.LandingImpactSpeed > settings.CollapseImpactSpeed)
            {
                _ragdollState.RequestCollapseRpc();
            }
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
