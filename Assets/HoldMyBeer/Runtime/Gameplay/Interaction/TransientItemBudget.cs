using System.Collections.Generic;
using Unity.Netcode;

namespace HoldMyBeer.Gameplay.Interaction
{
    /// <summary>
    /// A hard cap on how many short-lived objects the server keeps alive at once:
    /// blowgun pellets, portions off a tray, thrown food.
    ///
    /// The food fight is the worst network case in the game — dozens of rigidbodies on
    /// a host who is also a player. A cap is not late optimisation here, it is the
    /// feature that keeps the phase playable: past the ceiling the oldest object goes,
    /// which reads as the floor being cleared rather than as anything breaking.
    /// </summary>
    public sealed class TransientItemBudget
    {
        private readonly Queue<NetworkObject> _live = new();
        private readonly int _capacity;

        public TransientItemBudget(int capacity)
        {
            _capacity = capacity < 1 ? 1 : capacity;
        }

        public int Count => _live.Count;

        /// <summary>Server only. Makes room if needed, then takes ownership of the tail.</summary>
        public void Track(NetworkObject item)
        {
            if (item == null)
            {
                return;
            }

            _live.Enqueue(item);

            while (_live.Count > _capacity)
            {
                var oldest = _live.Dequeue();
                if (oldest != null && oldest.IsSpawned)
                {
                    oldest.Despawn();
                }
            }
        }
    }
}
