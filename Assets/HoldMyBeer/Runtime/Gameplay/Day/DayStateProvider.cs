using System;

namespace HoldMyBeer.Gameplay.Day
{
    public sealed class DayStateProvider : IDayStateProvider
    {
        public IDayState Current { get; private set; }

        public event Action<IDayState> CurrentChanged;

        public void Set(IDayState day)
        {
            Current = day;
            CurrentChanged?.Invoke(day);
        }
    }
}
