using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Items
{
    /// <summary>The lab's hot plate: a fixed thing that turns a full flask into a bang.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class HeatSource : NetworkBehaviour, IInteractionTarget
    {
        public Transform Body => transform;

        public bool IsAvailable => IsSpawned;
    }
}
