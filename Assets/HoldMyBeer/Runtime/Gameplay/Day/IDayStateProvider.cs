using System;

namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>
    /// The day only exists inside the Game scene, so rules and UI built at boot watch
    /// this instead of resolving the day once. Mirrors <c>ILobbyProvider</c>.
    /// </summary>
    public interface IDayStateProvider
    {
        IDayState Current { get; }

        event Action<IDayState> CurrentChanged;
    }
}
