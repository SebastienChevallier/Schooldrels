using UnityEngine;

namespace HoldMyBeer.Interaction
{
    /// <summary>
    /// Something worth aiming at that is not itself picked up: a beer tap, a bin, a
    /// table. Rules match on the concrete implementation, so this contract stays
    /// almost empty on purpose — it exists to be found by a raycast and identified.
    /// </summary>
    public interface IInteractionTarget
    {
        Transform Body { get; }

        /// <summary>False while the target is busy or out of order.</summary>
        bool IsAvailable { get; }
    }
}
