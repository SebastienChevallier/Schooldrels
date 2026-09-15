using UnityEngine;

namespace HoldMyBeer.Interaction
{
    /// <summary>
    /// One act of mischief, as the rest of the game needs to hear about it: who, where,
    /// how much it is worth, and how far away an adult can hear it.
    /// </summary>
    public readonly struct MischiefReport
    {
        public MischiefReport(ulong clientId, Vector3 position, int reputation, float noiseRadius, string label)
        {
            ClientId = clientId;
            Position = position;
            Reputation = reputation;
            NoiseRadius = noiseRadius;
            Label = label;
        }

        public ulong ClientId { get; }
        public Vector3 Position { get; }
        public int Reputation { get; }

        /// <summary>0 for a silent prank: only an adult who sees it will react.</summary>
        public float NoiseRadius { get; }

        public string Label { get; }
    }
}
