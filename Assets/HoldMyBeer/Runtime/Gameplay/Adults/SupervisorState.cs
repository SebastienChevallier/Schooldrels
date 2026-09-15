namespace HoldMyBeer.Gameplay.Adults
{
    /// <summary>Replicated as a byte so every client can colour the supervisor.</summary>
    public enum SupervisorState : byte
    {
        Patrol = 0,
        Investigate = 1,
        Chase = 2
    }
}
