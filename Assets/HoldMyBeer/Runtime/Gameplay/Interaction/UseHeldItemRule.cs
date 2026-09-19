using HoldMyBeer.Interaction;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Interaction
{
    /// <summary>
    /// The one rule that lets an item speak for itself. Anything holding an
    /// <see cref="IUsableItem"/> component answers whether it can be used right now,
    /// and does the deed server-side.
    ///
    /// Registered between <c>GrabRule</c> and <c>ThrowRule</c>: an item that can be
    /// used is used, and everything else still gets thrown. Adding a verb to the game
    /// therefore touches no rule and no player code — it is a component on a prefab.
    /// </summary>
    public sealed class UseHeldItemRule : IInteractionRule
    {
        private readonly bool _charged;

        /// <param name="charged">
        /// Registered twice: once for instant items, once for the ones you wind up. The
        /// interactor has to know before the click which of the two it is dealing with,
        /// and a rule cannot change its mind halfway through a press.
        /// </param>
        public UseHeldItemRule(bool charged)
        {
            _charged = charged;
        }

        public bool IsCharged => _charged;

        public bool CanApply(in InteractionRequest request, out string prompt)
        {
            prompt = null;
            return TryGetUsable(request, out var usable) && usable.IsCharged == _charged &&
                   usable.CanUse(in request, out prompt);
        }

        public void Apply(in InteractionRequest request, IInteractionContext context)
        {
            if (TryGetUsable(request, out var usable) && usable.IsCharged == _charged)
            {
                usable.Use(in request, context);
            }
        }

        private static bool TryGetUsable(in InteractionRequest request, out IUsableItem usable)
        {
            usable = request.HeldItem is Component held ? held.GetComponent<IUsableItem>() : null;
            return usable != null;
        }
    }
}
