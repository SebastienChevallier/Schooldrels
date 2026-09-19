namespace HoldMyBeer.Interaction
{
    /// <summary>
    /// An item that does something when you use it, rather than just being thrown.
    /// Blowing a whistle, firing a blowgun, mixing a reagent, arming a board.
    ///
    /// One rule (<c>UseHeldItemRule</c>) serves all of them: the item answers for
    /// itself, so adding a verb to the game is a component on a prefab and no change
    /// anywhere else. It is deliberately the same shape as <see cref="IInteractionRule"/>,
    /// because it is the same idea one level down.
    /// </summary>
    public interface IUsableItem
    {
        /// <summary>Pure, no side effect: runs on the client for the prompt too.</summary>
        bool CanUse(in InteractionRequest request, out string prompt);

        /// <summary>Server only.</summary>
        void Use(in InteractionRequest request, IInteractionContext context);

        /// <summary>True to wind up on hold and fire on release, like a throw.</summary>
        bool IsCharged { get; }
    }
}
