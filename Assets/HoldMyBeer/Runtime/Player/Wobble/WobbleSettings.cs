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
        public float MaxBoneSpeed => maxBoneSpeed;
        public float MaxBoneAngularSpeed => maxBoneAngularSpeed;
        public float WatchdogDistance => watchdogDistance;

        /// <summary>Below this tension the root stops being driven by the CharacterController.</summary>
        public float CollapseTensionThreshold => collapseTensionThreshold;

        public static WobbleSettings Default => new()
        {
            idleTension = 1f,
            springAtFullTension = 800f,
            damper = 30f,
            maxForce = 1500f,
            collapseDuration = 0.2f,
            recoverDuration = 1f,
            pelvisFollowSpring = 900f,
            pelvisFollowDamper = 45f,
            maxBoneSpeed = 30f,
            maxBoneAngularSpeed = 25f,
            watchdogDistance = 6f,
            collapseTensionThreshold = 0.25f
        };
    }
}
