using System;
using HoldMyBeer.Interaction;

namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>
    /// Server-side fan-out of every prank. The day scores it and the adults listen for
    /// it, without either knowing the other exists — or which rule caused it.
    /// </summary>
    public sealed class MischiefBus
    {
        public event Action<MischiefReport> Reported;

        public void Publish(in MischiefReport report) => Reported?.Invoke(report);
    }
}
