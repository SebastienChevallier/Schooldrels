using System;
using System.Collections.Generic;

namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>
    /// The school day as every peer sees it. Read-only apart from the host's
    /// "next day" intent: scoring and detentions are decided server-side.
    /// </summary>
    public interface IDayState
    {
        int DayNumber { get; }
        int Quota { get; }
        int TeamReputation { get; }
        DayPhase Phase { get; }
        float SecondsRemaining { get; }
        IReadOnlyList<StudentScore> Scores { get; }

        bool TryGetScore(ulong clientId, out StudentScore score);

        bool IsWanted(ulong clientId);

        /// <summary>Raised on every peer for a line worth showing: a prank, a detention.</summary>
        event Action<string> Announced;

        /// <summary>Host only; ignored elsewhere. Starts the next day, or day 1 after a loss.</summary>
        void RequestNextDay();
    }
}
