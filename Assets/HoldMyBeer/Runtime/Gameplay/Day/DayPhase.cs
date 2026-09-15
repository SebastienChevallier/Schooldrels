namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>Serialised as a byte over the network.</summary>
    public enum DayPhase : byte
    {
        Playing = 0,
        QuotaMet = 1,
        Loser = 2
    }
}
