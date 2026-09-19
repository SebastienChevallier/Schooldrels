using UnityEngine;

namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>
    /// How long each phase lasts and what mischief is worth while it runs.
    ///
    /// An asset rather than fields on <c>DayState</c> so the rhythm can be retuned —
    /// and two rhythms compared — without recompiling. See
    /// <c>Documentation/design/day-budget.md</c>: a day is 15 to 20 minutes, and each
    /// phase has a floor below which it no longer has time to exist.
    /// </summary>
    [CreateAssetMenu(menuName = "Hold My Beer/Day Schedule", fileName = "DaySchedule")]
    public sealed class DaySchedule : ScriptableObject
    {
        [Header("Durées (secondes)")]
        [SerializeField, Min(1f)] private float arrival = 20f;
        [SerializeField, Min(1f)] private float morningBreak = 90f;
        [SerializeField, Min(1f)] private float classTime = 300f;
        [SerializeField, Min(1f)] private float lunch = 420f;
        [SerializeField, Min(1f)] private float dismissal = 90f;

        [Header("Multiplicateurs de réput'")]
        [Tooltip("Une bêtise pendant l'arrivée ne rapporte rien : la phase gratuite ne doit pas être la phase optimale.")]
        [SerializeField, Min(0f)] private float arrivalScale;
        [SerializeField, Min(0f)] private float breakScale = 0.5f;
        [Tooltip("Hors de la salle où a lieu le cours.")]
        [SerializeField, Min(0f)] private float classScale = 1f;
        [Tooltip("Dans la salle du cours : c'est là que c'est dangereux, donc c'est là que ça paie.")]
        [SerializeField, Min(0f)] private float inClassRoomScale = 1.5f;
        [SerializeField, Min(0f)] private float lunchScale = 1f;

        public float SecondsFor(DayPhase phase) => phase switch
        {
            DayPhase.Arrival => arrival,
            DayPhase.MorningBreak => morningBreak,
            DayPhase.Class1 => classTime,
            DayPhase.Lunch => lunch,
            DayPhase.Class2 => classTime,
            DayPhase.Dismissal => dismissal,
            _ => 0f
        };

        /// <summary>
        /// <paramref name="inCourseRoom"/> is only meaningful during a class; everywhere
        /// else the phase alone decides.
        /// </summary>
        public float ReputationScale(DayPhase phase, bool inCourseRoom) => phase switch
        {
            DayPhase.Arrival => arrivalScale,
            DayPhase.MorningBreak => breakScale,
            DayPhase.Class1 or DayPhase.Class2 => inCourseRoom ? inClassRoomScale : classScale,
            DayPhase.Lunch => lunchScale,
            DayPhase.Dismissal => breakScale,
            _ => 0f
        };

        /// <summary>The whole day, for the HUD and for sanity checks in the editor.</summary>
        public float TotalSeconds =>
            arrival + morningBreak + classTime * 2f + lunch + dismissal;
    }
}
