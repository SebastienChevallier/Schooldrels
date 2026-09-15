using UnityEngine;

namespace HoldMyBeer.Interaction
{
    /// <summary>
    /// Everything a rule needs to decide, gathered once by the caller. Passed by
    /// <c>in</c> so a rule cannot alter what the next rule will see.
    /// </summary>
    public readonly struct InteractionRequest
    {
        public InteractionRequest(
            ulong clientId,
            IGrabbable heldItem,
            IGrabbable aimedGrabbable,
            IInteractionTarget aimedTarget,
            Vector3 aimPoint,
            Vector3 aimDirection,
            Vector3 handPosition,
            Quaternion handRotation,
            Vector3 handVelocity,
            float charge = 0f)
        {
            Charge = Mathf.Clamp01(charge);
            ClientId = clientId;
            HeldItem = heldItem;
            AimedGrabbable = aimedGrabbable;
            AimedTarget = aimedTarget;
            AimPoint = aimPoint;
            AimDirection = aimDirection;
            HandPosition = handPosition;
            HandRotation = handRotation;
            HandVelocity = handVelocity;
        }

        public ulong ClientId { get; }

        /// <summary>What the player is already holding, or null.</summary>
        public IGrabbable HeldItem { get; }

        /// <summary>A grabbable under the crosshair, or null.</summary>
        public IGrabbable AimedGrabbable { get; }

        /// <summary>A non-grabbable target under the crosshair, or null.</summary>
        public IInteractionTarget AimedTarget { get; }

        public Vector3 AimPoint { get; }
        public Vector3 AimDirection { get; }

        // The hand pose comes from the requesting client: the ragdoll is never
        // replicated, so the server genuinely does not know where the hand is.
        // Rules must treat these as untrusted and the context clamps what it can.
        public Vector3 HandPosition { get; }
        public Quaternion HandRotation { get; }
        public Vector3 HandVelocity { get; }

        /// <summary>
        /// How long the player held the button, normalised to 0..1. Reported by the
        /// client and therefore clamped on arrival, like every other value here.
        /// </summary>
        public float Charge { get; }

        public bool HasHeldItem => HeldItem != null;
    }
}
