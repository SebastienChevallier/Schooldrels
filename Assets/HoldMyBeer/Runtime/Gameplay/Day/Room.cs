using UnityEngine;

namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>
    /// A room of the school, as game logic needs it: where it is, how big it is, where
    /// its door goes, where its items land, and where an adult stands to teach.
    ///
    /// Scene-authored and purely descriptive — it owns no networked state. The door
    /// does (see <c>Door</c>), because whether a room is open is shared truth.
    /// </summary>
    public sealed class Room : MonoBehaviour
    {
        [SerializeField] private RoomId id = RoomId.General;

        [Tooltip("Taille intérieure, centrée sur cet objet. Sert à savoir qui est dans la salle.")]
        [SerializeField] private Vector3 size = new(12f, 4f, 10f);

        [Tooltip("Où est posée la porte de la salle.")]
        [SerializeField] private Transform doorMarker;

        [Tooltip("Où apparaissent les items du cours qui s'y donne.")]
        [SerializeField] private Transform[] itemMarkers = System.Array.Empty<Transform>();

        [Tooltip("La ronde du prof pendant son cours. Vide : il reste au centre.")]
        [SerializeField] private Transform[] teacherRoute = System.Array.Empty<Transform>();

        public RoomId Id => id;
        public Transform DoorMarker => doorMarker;
        public Transform[] ItemMarkers => itemMarkers;
        public Transform[] TeacherRoute => teacherRoute;
        public Vector3 Centre => transform.position;

        /// <summary>
        /// Flat test on purpose: a student standing on a desk is still in the room, and
        /// the greybox has no ceilings worth arguing about.
        /// </summary>
        public bool Contains(Vector3 point)
        {
            var local = transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.z * 0.5f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
