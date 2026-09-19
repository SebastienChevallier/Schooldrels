namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>
    /// The school day, phase by phase. Serialised as a byte over the network, so the
    /// numbers are part of the contract: append, never renumber.
    ///
    /// The order is also the running order — <see cref="DayPhaseExtensions.Next"/>
    /// relies on it.
    /// </summary>
    public enum DayPhase : byte
    {
        /// <summary>Everyone lands at the gate. Nothing is open, nothing scores.</summary>
        Arrival = 0,

        /// <summary>Free corridor time before the first class: scouting, key stealing.</summary>
        MorningBreak = 1,

        Class1 = 2,

        Lunch = 3,

        Class2 = 4,

        /// <summary>Everything closes; last chance to free a friend.</summary>
        Dismissal = 5,

        /// <summary>The day is over and the board is up. No chrono.</summary>
        Recap = 6
    }

    public static class DayPhaseExtensions
    {
        /// <summary>True while the day is actually being played (adults hunt, pranks score).</summary>
        public static bool IsInPlay(this DayPhase phase) => phase != DayPhase.Recap;

        /// <summary>True during a class: a room is open and being taught in.</summary>
        public static bool IsClass(this DayPhase phase) => phase is DayPhase.Class1 or DayPhase.Class2;

        public static DayPhase Next(this DayPhase phase) =>
            phase == DayPhase.Recap ? DayPhase.Recap : (DayPhase)((byte)phase + 1);

        public static string Label(this DayPhase phase) => phase switch
        {
            DayPhase.Arrival => "Arrivée",
            DayPhase.MorningBreak => "Pause",
            DayPhase.Class1 => "Cours 1",
            DayPhase.Lunch => "Déjeuner",
            DayPhase.Class2 => "Cours 2",
            DayPhase.Dismissal => "Sortie",
            _ => "Récap"
        };
    }
}
