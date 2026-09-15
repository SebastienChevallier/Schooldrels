using HoldMyBeer.Gameplay.Day;
using HoldMyBeer.Interaction;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Pranks
{
    /// <summary>Aiming at an <see cref="UploadSpot"/> with reputation to post.</summary>
    public sealed class UploadRule : IInteractionRule
    {
        private readonly IDayStateProvider _day;
        private readonly float _maxReach;

        public UploadRule(IDayStateProvider day, float maxReach)
        {
            _day = day;
            _maxReach = maxReach;
        }

        public bool IsCharged => false;

        public bool CanApply(in InteractionRequest request, out string prompt)
        {
            prompt = null;

            if (request.AimedTarget is not UploadSpot { IsAvailable: true } ||
                Vector3.Distance(request.AimPoint, request.HandPosition) > _maxReach ||
                _day.Current is not { Phase: DayPhase.Playing } day ||
                !day.TryGetScore(request.ClientId, out var score) || score.Pending <= 0)
            {
                return false;
            }

            prompt = $"Poster la vidéo (+{score.Pending})";
            return true;
        }

        public void Apply(in InteractionRequest request, IInteractionContext context)
        {
            context.BankReputation(request.ClientId);
        }
    }
}
