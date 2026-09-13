using UnityEngine;

namespace HoldMyBeer.Player
{
    /// <summary>
    /// Abstracts where input comes from. Keeps the controller testable and leaves
    /// room for a gamepad, a rebindable action asset, or a replay/bot source later.
    /// </summary>
    public interface IPlayerInputSource
    {
        /// <summary>Normalized (x = strafe, y = forward).</summary>
        Vector2 Move { get; }

        /// <summary>Frame delta, already scaled by sensitivity.</summary>
        Vector2 Look { get; }

        bool JumpPressedThisFrame { get; }

        bool SprintHeld { get; }

        /// <summary>
        /// The single contextual action. There is no separate grab or throw input:
        /// what it does is decided by the interaction rules, so the input layer never
        /// has to learn about new verbs.
        /// </summary>
        bool InteractPressedThisFrame { get; }
    }
}
