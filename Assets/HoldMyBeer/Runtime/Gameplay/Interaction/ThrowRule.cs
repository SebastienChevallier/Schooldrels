using HoldMyBeer.Interaction;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Interaction
{
    /// <summary>
    /// The catch-all: holding something and nothing more specific claimed the request,
    /// so throw it. Must be registered LAST — it accepts every request that carries an
    /// item, and anything after it would be dead code.
    /// </summary>
    public sealed class ThrowRule : IInteractionRule
    {
        private readonly float _impulse;

        public ThrowRule(float impulse)
        {
            _impulse = impulse;
        }

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
            // The hand carries the item, so its own motion is part of the throw: a
            // flick of the wrist adds to the aim rather than being ignored.
            var velocity = request.AimDirection.normalized * _impulse + request.HandVelocity;

            context.Release(
                request.HeldItem,
                request.HandPosition,
                request.HandRotation,
                velocity);
        }
    }
}
