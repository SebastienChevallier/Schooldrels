using UnityEngine;

namespace HoldMyBeer.Player.Wobble
{
    /// <summary>
    /// Tunable wobble values, editable per prefab variant. Mirrors the shape of
    /// <see cref="PlayerMovementSettings"/> so both read the same way in the inspector.
    /// </summary>
    [System.Serializable]
    public struct WobbleSettings
    {
        [SerializeField] private float idleTension;
        [SerializeField] private float springAtFullTension;
        [SerializeField] private float damper;
        [SerializeField] private float maxForce;
        [SerializeField] private float collapseDuration;
        [SerializeField] private float recoverDuration;
        [SerializeField] private float pelvisFollowSpring;
        [SerializeField] private float pelvisFollowDamper;
        [SerializeField] private float pelvisUprightSpring;
        [SerializeField] private float pelvisUprightDamper;
        [SerializeField] private float maxBoneSpeed;
        [SerializeField] private float maxBoneAngularSpeed;
        [SerializeField] private float watchdogDistance;
        [SerializeField] private float collapseTensionThreshold;

        public float IdleTension => idleTension;
        public float SpringAtFullTension => springAtFullTension;
        public float Damper => damper;
        public float MaxForce => maxForce;
        public float CollapseDuration => collapseDuration;
        public float RecoverDuration => recoverDuration;
        public float PelvisFollowSpring => pelvisFollowSpring;
        public float PelvisFollowDamper => pelvisFollowDamper;

        /// <summary>
        /// Torque holding the pelvis upright. Without it the body tips over and stays
        /// there: the joints only constrain bones relative to each other, so a
        /// correctly posed character lying face down satisfies every one of them.
        /// </summary>
        public float PelvisUprightSpring => pelvisUprightSpring;

        public float PelvisUprightDamper => pelvisUprightDamper;
        public float MaxBoneSpeed => maxBoneSpeed;
        public float MaxBoneAngularSpeed => maxBoneAngularSpeed;
        public float WatchdogDistance => watchdogDistance;

        /// <summary>Below this tension the root stops being driven by the CharacterController.</summary>
        public float CollapseTensionThreshold => collapseTensionThreshold;

        public static WobbleSettings Default => new()
        {
            idleTension = 1f,
            springAtFullTension = 3000f,
            damper = 120f,
            maxForce = 20000f,
            collapseDuration = 0.2f,
            recoverDuration = 1f,
            pelvisFollowSpring = 900f,
            pelvisFollowDamper = 45f,
            pelvisUprightSpring = 1500f,
            pelvisUprightDamper = 90f,
            maxBoneSpeed = 30f,
            maxBoneAngularSpeed = 25f,
            watchdogDistance = 6f,
            collapseTensionThreshold = 0.25f
        };
    }
}
