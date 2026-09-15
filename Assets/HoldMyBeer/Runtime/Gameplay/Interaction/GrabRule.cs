using HoldMyBeer.Interaction;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Interaction
{
    /// <summary>
    /// Empty-handed and aiming at something loose: pick it up. Registered first, so a
    /// game rule that wants to intercept a specific object must go before it.
    /// </summary>
    public sealed class GrabRule : IInteractionRule
    {
        private readonly float _maxReach;

        public GrabRule(float maxReach)
        {
            _maxReach = maxReach;
        }

        public bool CanApply(in InteractionRequest request, out string prompt)
        {
            prompt = null;

            if (request.HasHeldItem || request.AimedGrabbable == null || request.AimedGrabbable.IsHeld)
            {
                return false;
            }

            // Measured from the aim point rather than the hand: the hand position is
            // client-reported, the aim point comes from a ray the server can bound.
            if (Vector3.Distance(request.AimPoint, request.HandPosition) > _maxReach)
            {
                return false;
            }

            prompt = "Prendre";
            return true;
        }

        public void Apply(in InteractionRequest request, IInteractionContext context)
        {
            context.Hold(request.AimedGrabbable, request.ClientId, HandSide.Right);
        }

        /// <summary>Picking something up has nothing to wind up.</summary>
        public bool IsCharged => false;
    }
}
