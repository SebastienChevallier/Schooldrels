using System;
using Unity.Netcode;

namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>One player's standing for the current day. Replicated in a NetworkList.</summary>
    public struct StudentScore : INetworkSerializable, IEquatable<StudentScore>
    {
        public ulong ClientId;

        /// <summary>Earned but not posted yet: lost on detention.</summary>
        public int Pending;

        /// <summary>What this player has posted today, for the end-of-day screen.</summary>
        public int Posted;

        public int Detentions;

        /// <summary>Server time until which an adult who sees this player gives chase.</summary>
        public double WantedUntil;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Pending);
            serializer.SerializeValue(ref Posted);
            serializer.SerializeValue(ref Detentions);
            serializer.SerializeValue(ref WantedUntil);
        }

        public bool Equals(StudentScore other) =>
            ClientId == other.ClientId && Pending == other.Pending && Posted == other.Posted &&
            Detentions == other.Detentions && WantedUntil.Equals(other.WantedUntil);

        public override bool Equals(object obj) => obj is StudentScore other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(ClientId, Pending, Posted, Detentions, WantedUntil);
    }
}
