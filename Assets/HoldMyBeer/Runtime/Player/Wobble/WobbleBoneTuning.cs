using UnityEngine;

namespace HoldMyBeer.Player.Wobble
{
    /// <summary>
    /// Per-bone stiffness multiplier, carried by the ragdoll prefab so each limb can
    /// be tuned without touching the others.
    ///
    /// One global spring cannot serve both ends of the body: a torso loose enough to
    /// be funny is a torso that folds, and a torso stiff enough to hold is arms that
    /// no longer swing. The whole point of the wobble is the arms, so they get the
    /// low values and the spine gets the high ones.
    /// </summary>
    public sealed class WobbleBoneTuning : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float stiffness = 1f;

        /// <summary>Multiplies the rig-wide spring and damper for this bone.</summary>
        public float Stiffness => stiffness;
    }
}
