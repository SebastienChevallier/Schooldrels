using UnityEngine;

namespace HoldMyBeer.Player.Wobble
{
    /// <summary>
    /// Per-bone wobble settings, carried by the ragdoll prefab so each limb can be
    /// tuned without touching the others.
    ///
    /// One global spring cannot serve both ends of the body. The trunk must not lag
    /// behind the player at all, while the arms are the whole point of the effect —
    /// those are different requirements, not different numbers on the same dial.
    /// </summary>
    public sealed class WobbleBoneTuning : MonoBehaviour
    {
        [SerializeField] private bool rigidWhileBraced = true;
        [SerializeField, Min(0f)] private float stiffness = 1f;

        /// <summary>
        /// True for the trunk and legs: while the player is on their feet the bone is
        /// kinematic and pinned to the animated pose, so it cannot lag by even a frame.
        /// A spring, however stiff, always lags — that is what a spring is.
        ///
        /// Ignored during a collapse: everything goes dynamic then, or it would not be
        /// a ragdoll.
        /// </summary>
        public bool RigidWhileBraced => rigidWhileBraced;

        /// <summary>
        /// Multiplies the rig-wide spring, for bones that stay dynamic. Lower means
        /// looser: along an arm this decreases from shoulder to hand, so the swing
        /// grows towards the fingers the way a real arm does.
        /// </summary>
        public float Stiffness => stiffness;
    }
}
