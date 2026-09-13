using System.Collections.Generic;
using HoldMyBeer.Interaction;

namespace HoldMyBeer.Gameplay.Interaction
{
    /// <summary>
    /// Local index of who holds what, rebuilt from the items' own replicated state.
    /// Exists so the player side can answer "what am I carrying?" without a second
    /// replicated variable that could disagree with the first.
    /// </summary>
    public sealed class HeldItemTracker : IHeldItemTracker
    {
        private readonly Dictionary<ulong, IGrabbable> _byClient = new();

        public bool TryGetHeldItem(ulong clientId, out IGrabbable item)
        {
            return _byClient.TryGetValue(clientId, out item);
        }

        public void Publish(IGrabbable item, ulong previousHolder)
        {
            if (item == null)
            {
                return;
            }

            // Clear the old slot first: a hand-off between two players arrives as one
            // change, and dropping this would leave the previous holder still carrying.
            if (previousHolder != GrabbableItem.NoHolder &&
                _byClient.TryGetValue(previousHolder, out var previousItem) &&
                ReferenceEquals(previousItem, item))
            {
                _byClient.Remove(previousHolder);
            }

            if (item.IsHeld)
            {
                _byClient[item.HolderClientId] = item;
            }
        }
    }
}
