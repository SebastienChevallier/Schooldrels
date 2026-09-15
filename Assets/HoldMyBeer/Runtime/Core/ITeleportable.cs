using UnityEngine;

namespace HoldMyBeer.Core
{
    /// <summary>
    /// Lets the server move an avatar it does not simulate. Movement is
    /// owner-authoritative, so a server-side position write would simply be
    /// overwritten by the owner's next replicated frame.
    /// </summary>
    public interface ITeleportable
    {
        /// <summary>Server only.</summary>
        void TeleportTo(Vector3 position, Quaternion rotation);
    }
}
