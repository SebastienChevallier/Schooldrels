using System;
using System.Collections.Generic;

namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>
    /// The school day as every peer sees it. Read-only apart from the host's
    /// "next day" intent: the clock, the draw, the scoring and the detentions are all
    /// decided server-side.
    ///
    /// Everything here is backed by replicated state rather than by events, so a
    /// player who joins at lunchtime sees the day exactly as it is.
    /// </summary>
    public interface IDayState
    {
        /// <summary>1-based. Three days make a cycle.</summary>
        int DayInCycle { get; }

        /// <summary>1-based. Grows forever; there is no final cycle.</summary>
        int Cycle { get; }

        int DaysPerCycle { get; }

        /// <summary>Team reputation needed over the whole cycle.</summary>
        int Quota { get; }

        /// <summary>Team reputation banked since the start of the cycle.</summary>
        int TeamReputation { get; }

        DayPhase Phase { get; }

        /// <summary>Seconds left in the current phase. Always derived from server time.</summary>
        float SecondsRemaining { get; }

        /// <summary>The subject being taught right now, or null outside a class.</summary>
        CourseDefinition CurrentCourse { get; }

        CourseDefinition CourseFor(DayPhase phase);

        /// <summary>The room of <see cref="CurrentCourse"/>, or <see cref="RoomId.None"/>.</summary>
        RoomId CurrentCourseRoom { get; }

        /// <summary>Today's lunch draw, or null.</summary>
        CourseDefinition Menu { get; }

        /// <summary>What this cycle has unlocked. Never null once a day has started.</summary>
        ProgressionTier Tier { get; }

        IReadOnlyList<StudentScore> Scores { get; }

        bool TryGetScore(ulong clientId, out StudentScore score);

        /// <summary>An adult who sees this player right now gives chase.</summary>
        bool IsWanted(ulong clientId);

        bool IsDetained(ulong clientId);

        /// <summary>Raised on every peer for a line worth showing: a prank, a detention.</summary>
        event Action<string> Announced;

        /// <summary>Raised on every peer when the bell rings. Never used as state.</summary>
        event Action<DayPhase> PhaseChanged;

        /// <summary>Host only; ignored elsewhere. Starts the next day, or the next cycle.</summary>
        void RequestNextDay();
    }
}
