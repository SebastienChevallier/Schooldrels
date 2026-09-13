namespace HoldMyBeer.Interaction
{
    /// <summary>
    /// Answers "what is this player holding?" on every peer. Items publish their own
    /// holder when the replicated state changes, so there is no second replicated
    /// variable to fall out of sync with the first.
    /// </summary>
    public interface IHeldItemTracker
    {
        bool TryGetHeldItem(ulong clientId, out IGrabbable item);

        void Publish(IGrabbable item, ulong previousHolder);
    }
}
