using HoldMyBeer.Interaction;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Pranks
{
    /// <summary>Empty hands, aiming at a <see cref="PrankTarget"/> that is ready: do it.</summary>
    public sealed class PrankRule : IInteractionRule
    {
        private readonly float _maxReach;

        public PrankRule(float maxReach)
        {
            _maxReach = maxReach;
        }

        public bool IsCharged => false;

        public bool CanApply(in InteractionRequest request, out string prompt)
        {
            prompt = null;

            if (request.HasHeldItem || request.AimedTarget is not PrankTarget { IsAvailable: true } target ||
                Vector3.Distance(request.AimPoint, request.HandPosition) > _maxReach)
            {
                return false;
            }

            prompt = $"{target.Prompt} (+{target.Reputation})";
            return true;
        }

        public void Apply(in InteractionRequest request, IInteractionContext context)
        {
            if (request.AimedTarget is not PrankTarget target || !target.TryTrigger())
            {
                return;
            }

            context.ReportMischief(new MischiefReport(
                request.ClientId, target.Body.position, target.Reputation, target.NoiseRadius, target.Announcement));
        }
    }
}
