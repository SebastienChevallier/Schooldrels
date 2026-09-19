using HoldMyBeer.Core;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>Scene-authored markers, registered the same way as <c>SpawnPointRegistry</c>.</summary>
    public sealed class SchoolLayout : MonoBehaviour, ISchoolLayout
    {
        [SerializeField] private Transform detentionPoint;
        [SerializeField] private Transform[] patrolRoute;

        [Tooltip("Vide : les Room enfants de cet objet sont collectées au réveil.")]
        [SerializeField] private Room[] rooms = System.Array.Empty<Room>();

        public Transform DetentionPoint => detentionPoint;
        public Transform[] PatrolRoute => patrolRoute;
        public Room[] Rooms => rooms;

        private void Awake()
        {
            if (rooms == null || rooms.Length == 0)
            {
                rooms = GetComponentsInChildren<Room>(true);
            }

            if (AppServices.IsReady)
            {
                AppServices.Container.Register<ISchoolLayout>(this);
            }
        }

        private void OnDestroy()
        {
            if (AppServices.IsReady &&
                AppServices.Container.TryResolve<ISchoolLayout>(out var current) &&
                ReferenceEquals(current, this))
            {
                AppServices.Container.Unregister<ISchoolLayout>();
            }
        }

        public bool TryGetRoom(RoomId id, out Room room)
        {
            foreach (var candidate in rooms)
            {
                if (candidate != null && candidate.Id == id)
                {
                    room = candidate;
                    return true;
                }
            }

            room = null;
            return false;
        }

        public Room RoomAt(Vector3 position)
        {
            foreach (var candidate in rooms)
            {
                if (candidate != null && candidate.Contains(position))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
