namespace HoldMyBeer.Interaction
{
    /// <summary>
    /// One contextual action. Rules compose exactly like
    /// <c>IConnectionApprovalPolicy</c>: the registry walks them in registration order
    /// and the first one that accepts wins, so the order IS the priority.
    /// </summary>
    public interface IInteractionRule
    {
        /// <summary>
        /// Pure, no side effect. Runs on the client to label the crosshair prompt and
        /// on the server to pick the rule to apply. Splitting the question from the
        /// act is what lets the client ask "what would happen?" without a round trip,
        /// and lets the server refuse before anything has mutated.
        /// </summary>
        bool CanApply(in InteractionRequest request, out string prompt);

        /// <summary>
        /// Server only, and only after <see cref="CanApply"/> returned true there.
        /// </summary>
        void Apply(in InteractionRequest request, IInteractionContext context);

        /// <summary>
        /// True for actions the player winds up: the button press starts a charge and
        /// the action fires on release, with <see cref="InteractionRequest.Charge"/>
        /// filled in. False fires immediately on press.
        ///
        /// It lives on the rule rather than in the interactor so that a future rule —
        /// pulling a pint by holding, winding up a shove — gets the behaviour without
        /// anyone touching the player code.
        /// </summary>
        bool IsCharged { get; }
    }
}
