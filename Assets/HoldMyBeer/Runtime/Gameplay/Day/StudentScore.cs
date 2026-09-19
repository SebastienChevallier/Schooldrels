using System;
using Unity.Netcode;

namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>
    /// One player's standing for the current day. Replicated in a NetworkList, so
    /// every field must stay unmanaged.
    ///
    /// There is nothing to post any more: reputation is earned the moment the prank
    /// lands. What a detention takes is half of it, which is why the number a player
    /// carries is worth protecting without ever having to run somewhere.
    /// </summary>
    public struct StudentScore : INetworkSerializable, IEquatable<StudentScore>
    {
        public ulong ClientId;

        /// <summary>Earned today. A detention halves it.</summary>
        public int Reputation;

        public int Detentions;

        /// <summary>Server time until which an adult who sees this player gives chase.</summary>
        public double WantedUntil;

        /// <summary>
        /// The phase this student is locked in detention until, as a byte, or
        /// <see cref="NotDetained"/>. Stored as a phase rather than a timestamp so the
        /// sentence is "half a day" whatever the schedule says.
        /// </summary>
        public byte DetainedUntilPhase;

        public const byte NotDetained = byte.MaxValue;

        public bool IsDetained => DetainedUntilPhase != NotDetained;

        public static StudentScore ForClient(ulong clientId) =>
            new() { ClientId = clientId, DetainedUntilPhase = NotDetained };

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Reputation);
            serializer.SerializeValue(ref Detentions);
            serializer.SerializeValue(ref WantedUntil);
            serializer.SerializeValue(ref DetainedUntilPhase);
        }

        public bool Equals(StudentScore other) =>
            ClientId == other.ClientId && Reputation == other.Reputation &&
            Detentions == other.Detentions && WantedUntil.Equals(other.WantedUntil) &&
            DetainedUntilPhase == other.DetainedUntilPhase;

        public override bool Equals(object obj) => obj is StudentScore other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(ClientId, Reputation, Detentions, WantedUntil, DetainedUntilPhase);
    }
}
