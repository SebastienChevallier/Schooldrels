using HoldMyBeer.Gameplay.Day;
using HoldMyBeer.Interaction;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Rooms
{
    /// <summary>
    /// Aiming at a door: open it, with the key if it is locked.
    ///
    /// Registered between <c>GrabRule</c> and <c>ThrowRule</c>, because a player
    /// holding a key must be able to use it rather than throw it — the order IS the
    /// priority, and <c>ThrowRule</c> accepts anything held.
    /// </summary>
    public sealed class OpenDoorRule : IInteractionRule
    {
        private readonly float _maxReach;

        public OpenDoorRule(float maxReach)
        {
            _maxReach = maxReach;
        }

        public bool IsCharged => false;

        public bool CanApply(in InteractionRequest request, out string prompt)
        {
            prompt = null;

            if (request.AimedTarget is not Door { IsAvailable: true } door || door.IsOpen ||
                Vector3.Distance(request.AimPoint, request.HandPosition) > _maxReach)
            {
                return false;
            }

            if (HasKeyFor(request, door.Room))
            {
                prompt = door.IsLocked ? "Déverrouiller" : "Ouvrir";
                return true;
            }

            if (!door.IsLocked)
            {
                prompt = "Ouvrir";
                return true;
            }

            // Empty-handed on a locked door: accept, and do nothing. "Locked" is
            // information the player needs — that is how they learn keys exist — and
            // eating the click costs nothing when there is nothing else to do.
            // Holding something else, the rule stands aside so ThrowRule still works.
            if (!request.HasHeldItem)
            {
                prompt = "Fermée à clé";
                return true;
            }

            return false;
        }

        public void Apply(in InteractionRequest request, IInteractionContext context)
        {
            if (request.AimedTarget is Door door)
            {
                door.TryOpen(HasKeyFor(request, door.Room));
            }
        }

        private static bool HasKeyFor(in InteractionRequest request, RoomId room) =>
            request.HeldItem is Component held && held.TryGetComponent<KeyItem>(out var key) && key.Room == room;
    }
}
