using UnityEngine;

namespace HoldMyBeer.Player.Wobble
{
    /// <summary>
    /// Turns a target tension into a smoothed current tension and the joint drive
    /// values that follow from it. Pure maths, no Unity objects — same reason
    /// <see cref="FirstPersonMotor"/> is a plain class: a future server-authoritative
    /// rewrite must not have to touch it.
    /// </summary>
    public sealed class WobbleSolver
    {
        private readonly WobbleSettings _settings;

        public WobbleSolver(WobbleSettings settings, float initialTension)
        {
            _settings = settings;
            Tension = Mathf.Clamp01(initialTension);
        }

        /// <summary>Current tension, 1 = braced, 0 = full ragdoll.</summary>
        public float Tension { get; private set; }

        public bool IsCollapsed => Tension < _settings.CollapseTensionThreshold;

        /// <summary>
        /// Ramps the tension towards <paramref name="targetTension"/>. Collapsing is
        /// deliberately faster than recovering: falling should feel sudden, getting
        /// up should not.
        /// </summary>
        public float Tick(float targetTension, float deltaTime)
        {
            var target = Mathf.Clamp01(targetTension);
            var duration = target < Tension
                ? _settings.CollapseDuration
                : _settings.RecoverDuration;

            if (duration <= 0f)
            {
                Tension = target;
                return Tension;
            }

            Tension = Mathf.MoveTowards(Tension, target, deltaTime / duration);
            return Tension;
        }

        /// <summary>
        /// Joint spring for the current tension. Squared so the last stretch of the
        /// recovery is gentle instead of snapping the body upright.
        /// </summary>
        public float CurrentSpring => _settings.SpringAtFullTension * Tension * Tension;

        public float CurrentDamper => _settings.Damper * Tension;

        public float CurrentMaxForce => _settings.MaxForce;
    }
}
