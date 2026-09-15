using HoldMyBeer.Core;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>Scene-authored markers, registered the same way as <c>SpawnPointRegistry</c>.</summary>
    public sealed class SchoolLayout : MonoBehaviour, ISchoolLayout
    {
        [SerializeField] private Transform detentionPoint;
        [SerializeField] private Transform[] patrolRoute;

        public Transform DetentionPoint => detentionPoint;
        public Transform[] PatrolRoute => patrolRoute;

        private void Awake()
        {
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
    }
}
