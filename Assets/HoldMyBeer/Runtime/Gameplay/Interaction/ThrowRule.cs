using HoldMyBeer.Interaction;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Interaction
{
    /// <summary>
    /// The catch-all: holding something and nothing more specific claimed the request,
    /// so throw it. Must be registered LAST — it accepts every request that carries an
    /// item, and anything after it would be dead code.
    ///
    /// Wound up rather than instant: the button is held, and the longer it is held the
    /// harder and flatter the throw.
    /// </summary>
    public sealed class ThrowRule : IInteractionRule
    {
        private readonly float _minImpulse;
        private readonly float _maxImpulse;
        private readonly float _lobAtZeroCharge;
        private readonly float _handInfluence;

        public ThrowRule(float minImpulse, float maxImpulse, float lobAtZeroCharge, float handInfluence)
        {
            _minImpulse = minImpulse;
            _maxImpulse = maxImpulse;
            _lobAtZeroCharge = lobAtZeroCharge;
            _handInfluence = handInfluence;
        }

        public bool IsCharged => true;

        public bool CanApply(in InteractionRequest request, out string prompt)
        {
            if (!request.HasHeldItem)
            {
                prompt = null;
                return false;
            }

            prompt = "Lancer";
            return true;
        }

        public void Apply(in InteractionRequest request, IInteractionContext context)
        {
            var charge = request.Charge;

            // Two things grow with the charge, and both are what "harder and straighter"
            // means: the speed, and how closely the throw obeys the crosshair.
            var speed = Mathf.Lerp(_minImpulse, _maxImpulse, charge);

            // A flick of the wrist should place a gentle toss, and should not be able to
            // spoil a fully wound-up throw — so its share fades out as the charge fills.
            var lob = Mathf.Lerp(_lobAtZeroCharge, 0f, charge);
            var direction = (request.AimDirection.normalized + Vector3.up * lob).normalized;
            var velocity = direction * speed + request.HandVelocity * (_handInfluence * (1f - charge));

            context.Release(
                request.HeldItem,
                request.HandPosition,
                request.HandRotation,
                velocity);
        }
    }
}
