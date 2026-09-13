namespace HoldMyBeer.Interaction
{
    /// <summary>
    /// The ordered list of rules. Mirrors ConnectionApprovalHandler.AddPolicy so that
    /// adding an interaction is one class and one line in the composition root.
    /// </summary>
    public interface IInteractionRegistry
    {
        /// <summary>
        /// Appends a rule. Order is priority: a catch-all such as ThrowRule must be
        /// registered last, or nothing after it will ever run.
        /// </summary>
        void AddRule(IInteractionRule rule);

        /// <summary>Finds the rule that would handle this request, if any.</summary>
        bool TryResolve(in InteractionRequest request, out IInteractionRule rule, out string prompt);
    }
}
