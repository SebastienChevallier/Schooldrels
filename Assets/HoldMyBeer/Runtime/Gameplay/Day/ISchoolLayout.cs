using UnityEngine;

namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>The places in the school that game logic needs to know by role.</summary>
    public interface ISchoolLayout
    {
        /// <summary>Where a caught student is sent.</summary>
        Transform DetentionPoint { get; }

        /// <summary>The supervisor's rounds, walked in order and looped.</summary>
        Transform[] PatrolRoute { get; }
    }
}
