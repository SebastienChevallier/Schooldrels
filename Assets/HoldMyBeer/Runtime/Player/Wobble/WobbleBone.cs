using UnityEngine;

namespace HoldMyBeer.Player.Wobble
{
    /// <summary>
    /// One physical bone, sprung towards its animated counterpart. This is the only
    /// place in the project that touches the ConfigurableJoint API: swapping the
    /// technique later means rewriting this file and nothing else.
    /// </summary>
    public sealed class WobbleBone
    {
        private readonly ConfigurableJoint _joint;
        private readonly Transform _physical;
        private readonly Transform _animated;

        // targetRotation is expressed in the joint space captured at configuration
        // time, NOT in current local space. Forgetting this conversion is what
        // produces a permanently twisted character.
        private readonly Quaternion _startLocalRotation;

        public WobbleBone(Rigidbody body, ConfigurableJoint joint, Transform animated)
        {
            Body = body;
            _joint = joint;
            _physical = body.transform;
            _animated = animated;
            _startLocalRotation = _physical.localRotation;
        }

        public Rigidbody Body { get; }
        public Transform Animated => _animated;

        /// <summary>Pushes the animated pose into the joint's drive target.</summary>
        public void MatchAnimatedRotation()
        {
            _joint.targetRotation =
                Quaternion.Inverse(_animated.localRotation) * _startLocalRotation;
        }

        public void ApplyTension(float spring, float damper, float maxForce)
        {
            var drive = new JointDrive
            {
                positionSpring = spring,
                positionDamper = damper,
                maximumForce = maxForce
            };

            _joint.angularXDrive = drive;
            _joint.angularYZDrive = drive;
            _joint.slerpDrive = drive;
        }

        /// <summary>
        /// Teleports the bone onto its animated counterpart and kills its momentum.
        /// Used at spawn and by the watchdog.
        /// </summary>
        public void SnapToAnimated()
        {
            _physical.SetPositionAndRotation(_animated.position, _animated.rotation);
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// A misconfigured joint produces NaN, and one NaN contaminates the whole rig
        /// in a single frame. Clamping is cheaper than debugging that.
        /// </summary>
        public void ClampVelocity(float maxSpeed, float maxAngularSpeed)
        {
            var velocity = Body.linearVelocity;
            var angular = Body.angularVelocity;

            // IsFinite rather than IsNaN: a diverging solver reaches infinity just as
            // readily, and an infinite velocity poisons the rig the same way.
            if (!float.IsFinite(velocity.sqrMagnitude) || !float.IsFinite(angular.sqrMagnitude))
            {
                SnapToAnimated();
                return;
            }

            if (velocity.sqrMagnitude > maxSpeed * maxSpeed)
            {
                Body.linearVelocity = velocity.normalized * maxSpeed;
            }

            if (angular.sqrMagnitude > maxAngularSpeed * maxAngularSpeed)
            {
                Body.angularVelocity = angular.normalized * maxAngularSpeed;
            }
        }
    }
}
