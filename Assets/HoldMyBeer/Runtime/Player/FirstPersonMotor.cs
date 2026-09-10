using UnityEngine;

namespace HoldMyBeer.Player
{
    /// <summary>
    /// Pure movement maths over a CharacterController. Knows nothing about input
    /// devices or networking, which is what makes it trivial to unit test and to
    /// reuse later for a server-authoritative rewrite.
    /// </summary>
    public sealed class FirstPersonMotor
    {
        private readonly CharacterController _controller;
        private readonly PlayerMovementSettings _settings;

        private Vector3 _velocity;
        private bool _wasGrounded = true;

        public FirstPersonMotor(CharacterController controller, PlayerMovementSettings settings)
        {
            _controller = controller;
            _settings = settings;
        }

        public bool IsGrounded => _controller.isGrounded;

        /// <summary>
        /// Downward speed at the frame the character touched the ground again, or 0.
        /// Only valid on the landing frame: it is recomputed by every Tick.
        /// </summary>
        public float LandingImpactSpeed { get; private set; }

        public void Tick(Vector2 moveInput, bool sprint, bool jump, float deltaTime)
        {
            var transform = _controller.transform;
            var direction = transform.right * moveInput.x + transform.forward * moveInput.y;
            var speed = sprint ? _settings.SprintSpeed : _settings.WalkSpeed;

            _velocity.x = direction.x * speed;
            _velocity.z = direction.z * speed;

            LandingImpactSpeed = 0f;

            if (_controller.isGrounded)
            {
                if (!_wasGrounded)
                {
                    LandingImpactSpeed = -_velocity.y;
                }

                _wasGrounded = true;

                // A small downward bias keeps isGrounded stable on slopes and steps.
                _velocity.y = -2f;

                if (jump)
                {
                    _velocity.y = Mathf.Sqrt(_settings.JumpHeight * -2f * _settings.Gravity);
                }
            }
            else
            {
                _wasGrounded = false;
                _velocity.y += _settings.Gravity * deltaTime;
            }

            _controller.Move(_velocity * deltaTime);
        }
    }
}
