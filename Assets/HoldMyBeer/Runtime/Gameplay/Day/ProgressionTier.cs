using UnityEngine;

namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>
    /// What one cleared cycle opens up. This is the reason to start another one, so a
    /// tier that only makes the school harder is a wasted tier: it must always add
    /// something to <i>do</i>.
    ///
    /// Difficulty is capped, content is not — an endless game that becomes unplayable
    /// is not endless.
    /// </summary>
    [CreateAssetMenu(menuName = "Hold My Beer/Progression Tier", fileName = "Tier")]
    public sealed class ProgressionTier : ScriptableObject
    {
        [SerializeField] private string headline = "Nouveau palier";

        [Tooltip("Nombre de matières du catalogue ouvertes au tirage à ce palier (les premières de la liste).")]
        [SerializeField, Min(1)] private int availableCourses = 2;

        [Tooltip("Adultes supplémentaires en plus du surveillant de base.")]
        [SerializeField, Min(0)] private int extraAdults;

        [Tooltip("Multiplie la portée de vue des adultes. Plafonné dans la table.")]
        [SerializeField, Min(0.1f)] private float vigilanceScale = 1f;

        [Tooltip("Probabilité qu'une salle hors cours soit fermée à clé.")]
        [SerializeField, Range(0f, 1f)] private float lockedRoomChance = 0.8f;

        public string Headline => headline;
        public int AvailableCourses => availableCourses;
        public int ExtraAdults => extraAdults;
        public float VigilanceScale => vigilanceScale;
        public float LockedRoomChance => lockedRoomChance;
    }
}
