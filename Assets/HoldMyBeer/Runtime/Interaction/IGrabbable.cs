using UnityEngine;

namespace HoldMyBeer.Interaction
{
    /// <summary>
    /// Something a hand can hold. Deliberately transform-level and free of Netcode:
    /// the player side only ever needs to know where to put the object, and keeping
    /// the contract that thin is what lets HoldMyBeer.Player stay unaware of gameplay.
    /// </summary>
    public interface IGrabbable
    {
        /// <summary>The root transform to move while the item is attached to a hand.</summary>
        Transform Body { get; }

        /// <summary>
        /// Where the palm should meet the object. Attaching by an explicit anchor
        /// rather than by the object origin is what lets a mug hang from its handle.
        /// </summary>
        Transform GripAnchor { get; }

        float Mass { get; }

        bool IsHeld { get; }

        ulong HolderClientId { get; }

        /// <summary>Which hand holds it. Replicated alongside the holder.</summary>
        HandSide Hand { get; }
    }
}
