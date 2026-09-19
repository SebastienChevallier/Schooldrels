using UnityEngine;

namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>
    /// A subject: its room and the items that only exist while it is being taught.
    /// Data, not code — adding a subject is an asset plus its prefabs in the network
    /// list, and no system changes.
    /// </summary>
    [CreateAssetMenu(menuName = "Hold My Beer/Course", fileName = "Course")]
    public sealed class CourseDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Cours";
        [SerializeField] private RoomId room = RoomId.General;

        [Tooltip("Spawnés par le serveur à l'ouverture de la salle, dépawnés à sa fermeture.")]
        [SerializeField] private GameObject[] items = System.Array.Empty<GameObject>();

        [Tooltip("Multiplie la réput' des bêtises faites dans cette salle pendant ce cours.")]
        [SerializeField, Min(0f)] private float reputationScale = 1f;

        [Tooltip("Plusieurs matières peuvent partager une salle (français/maths/philo) : elles partagent alors ce groupe.")]
        [SerializeField] private bool sharesGeneralRoom;

        public string DisplayName => displayName;
        public RoomId Room => room;
        public GameObject[] Items => items;
        public float ReputationScale => reputationScale;
        public bool SharesGeneralRoom => sharesGeneralRoom;
    }
}
