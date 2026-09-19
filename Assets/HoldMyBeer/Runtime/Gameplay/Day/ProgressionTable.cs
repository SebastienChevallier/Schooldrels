using UnityEngine;

namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>
    /// The tiers in order. The active one is replicated as an index, like the courses.
    ///
    /// Past the last tier the game keeps going on the last one: the loop is endless,
    /// so running out of authored content must never stop it.
    /// </summary>
    [CreateAssetMenu(menuName = "Hold My Beer/Progression Table", fileName = "ProgressionTable")]
    public sealed class ProgressionTable : ScriptableObject
    {
        [SerializeField] private ProgressionTier[] tiers = System.Array.Empty<ProgressionTier>();

        [Tooltip("Plafond dur de vigilance : au-delà, le jeu devient injouable et cesse d'être infini.")]
        [SerializeField, Min(1f)] private float maxVigilanceScale = 1.6f;

        public int Count => tiers.Length;

        /// <summary>Clamped: a cycle past the last authored tier keeps the last one.</summary>
        public ProgressionTier For(int cycleIndex)
        {
            if (tiers.Length == 0)
            {
                return null;
            }

            return tiers[Mathf.Clamp(cycleIndex, 0, tiers.Length - 1)];
        }

        public float ClampVigilance(float scale) => Mathf.Min(scale, maxVigilanceScale);
    }
}
