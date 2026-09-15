using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Pranks
{
    /// <summary>
    /// The one corner of the school with signal. Reputation only counts once it is
    /// posted from here, which is what forces the trip back — Lethal Company's ship.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class UploadSpot : NetworkBehaviour, IInteractionTarget
    {
        public Transform Body => transform;

        public bool IsAvailable => IsSpawned;
    }
}
