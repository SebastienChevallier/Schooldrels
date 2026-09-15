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
        private readonly Quaternion _worldToJointSpace;
        private readonly Quaternion _jointSpaceToWorld;

        private float _appliedSpring = -1f;
        private float _appliedDamper = -1f;
        private float _appliedMaxForce = -1f;

        private readonly float _stiffness;
        private bool _rigid;

        public WobbleBone(Rigidbody body, ConfigurableJoint joint, Transform animated,
                          float stiffness, bool rigidWhileBraced)
        {
            RigidWhileBraced = rigidWhileBraced;
            Body = body;
            _joint = joint;
            _physical = body.transform;
            _animated = animated;
            _stiffness = Mathf.Max(0f, stiffness);
            _startLocalRotation = _physical.localRotation;

            // Joint space is built from the joint axes, which the builder leaves at
            // their defaults — and those defaults do NOT produce identity here, so
            // the conjugation below is load-bearing rather than decorative.
            var right = joint.axis;
            var forward = Vector3.Cross(joint.axis, joint.secondaryAxis).normalized;
            var up = Vector3.Cross(forward, right).normalized;
            _worldToJointSpace = Quaternion.LookRotation(forward, up);
            _jointSpaceToWorld = Quaternion.Inverse(_worldToJointSpace);
        }

        public Rigidbody Body { get; }
        public Transform Animated => _animated;

        /// <summary>Trunk and legs: pinned rather than sprung while the player is upright.</summary>
        public bool RigidWhileBraced { get; }

        /// <summary>
        /// Switches a bone between pinned and simulated. Velocity is cleared on the way
        /// back to dynamic: a body that was kinematic has stale momentum, and letting it
        /// through is how a collapse turns into a launch.
        /// </summary>
        public void SetRigid(bool rigid)
        {
            if (_rigid == rigid)
            {
                return;
            }

            _rigid = rigid;
            Body.isKinematic = rigid;

            if (!rigid)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Pins the bone straight onto its animated counterpart. MovePosition rather
        /// than the transform, so interpolation still smooths it between fixed steps.
        /// </summary>
        public void DriveKinematic()
        {
            Body.MovePosition(_animated.position);
            Body.MoveRotation(_animated.rotation);
        }

        /// <summary>
        /// Pushes the animated pose into the joint drive target, converted into joint
        /// space. At rest the conversion collapses to identity, which is exactly why
        /// omitting it looks correct on a static pose and falls apart as soon as an
        /// animation moves the bone.
        /// </summary>
        public void MatchAnimatedRotation()
        {
            _joint.targetRotation = _jointSpaceToWorld
                                    * Quaternion.Inverse(_animated.localRotation)
                                    * _startLocalRotation
                                    * _worldToJointSpace;
        }

        /// <summary>
        /// Only slerpDrive is written: the joints are built in Slerp mode, where PhysX
        /// ignores the per-axis drives entirely. Skipped when nothing changed, since
        /// the values only move during a collapse or recovery ramp.
        /// </summary>
        public void ApplyTension(float rigSpring, float rigDamper, float maxForce)
        {
            var spring = rigSpring * _stiffness;
            var damper = rigDamper * _stiffness;

            if (Mathf.Approximately(spring, _appliedSpring) &&
                Mathf.Approximately(damper, _appliedDamper) &&
                Mathf.Approximately(maxForce, _appliedMaxForce))
            {
                return;
            }

            _appliedSpring = spring;
            _appliedDamper = damper;
            _appliedMaxForce = maxForce;

            _joint.slerpDrive = new JointDrive
            {
                positionSpring = spring,
                positionDamper = damper,
                maximumForce = maxForce
            };
        }

        /// <summary>
        /// Teleports the bone onto its animated counterpart and kills its momentum.
        /// Used at spawn and by the watchdog.
        /// </summary>
        public void SnapToAnimated()
        {
            // Through the Rigidbody rather than the Transform: an interpolated body
            // moved by its transform smears across the frame it was resnapped on.
            Body.position = _animated.position;
            Body.rotation = _animated.rotation;
            _physical.SetPositionAndRotation(_animated.position, _animated.rotation);
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }

        public void ClampVelocity(float maxSpeed, float maxAngularSpeed)
        {
            ClampBody(Body, maxSpeed, maxAngularSpeed, SnapToAnimated);
        }

        /// <summary>
        /// A misconfigured joint produces NaN, and one NaN contaminates the whole rig
        /// in a single frame. Clamping is cheaper than debugging that. Shared with the
        /// pelvis, which has no joint and is therefore not a WobbleBone.
        /// </summary>
        public static void ClampBody(Rigidbody body, float maxSpeed, float maxAngularSpeed,
                                     System.Action onDiverged)
        {
            var velocity = body.linearVelocity;
            var angular = body.angularVelocity;

            // IsFinite rather than IsNaN: a diverging solver reaches infinity just as
            // readily, and an infinite velocity poisons the rig the same way.
            if (!float.IsFinite(velocity.sqrMagnitude) ||
                !float.IsFinite(angular.sqrMagnitude) ||
                !float.IsFinite(body.position.sqrMagnitude))
            {
                onDiverged?.Invoke();
                return;
            }

            if (velocity.sqrMagnitude > maxSpeed * maxSpeed)
            {
                body.linearVelocity = velocity.normalized * maxSpeed;
            }

            if (angular.sqrMagnitude > maxAngularSpeed * maxAngularSpeed)
            {
                body.angularVelocity = angular.normalized * maxAngularSpeed;
            }
        }
    }
}
