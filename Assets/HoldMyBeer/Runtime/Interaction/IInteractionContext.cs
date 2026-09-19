using UnityEngine;

namespace HoldMyBeer.Interaction
{
    /// <summary>
    /// What a rule is allowed to do, and the only way it can act. A rule never sees
    /// NetworkManager or NetworkObject: it says what should happen and the context —
    /// which runs server-side — decides how to make it true on every peer.
    ///
    /// This is the seam that keeps rules as plain game code. Adding a capability here
    /// (spawning a replacement prefab, playing a replicated effect) is the expected
    /// way to grow the system.
    /// </summary>
    public interface IInteractionContext
    {
        /// <summary>Puts an item in a player's hand. Fails if it is already held.</summary>
        bool Hold(IGrabbable item, ulong clientId, HandSide hand);

        /// <summary>
        /// Drops or throws an item. The velocity is clamped by the implementation:
        /// it originates from the client and is not trusted.
        /// </summary>
        bool Release(IGrabbable item, Vector3 position, Quaternion rotation, Vector3 velocity);

        void Despawn(IGrabbable item);

        /// <summary>Credits the prank and makes it audible to the adults nearby.</summary>
        void ReportMischief(in MischiefReport report);

        /// <summary>
        /// Spawns a networked prefab into the world, already moving. Used by items that
        /// produce other items: a blowgun pellet, a portion taken off a tray, a cloud.
        ///
        /// What it spawns counts against a live-object budget, so a food fight cannot
        /// drown the host in NetworkObjects. Returns false when the budget refuses, or
        /// off the server.
        /// </summary>
        bool SpawnItem(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 velocity);
    }
}
