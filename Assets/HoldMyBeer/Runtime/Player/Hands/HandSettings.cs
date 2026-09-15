using UnityEngine;

namespace HoldMyBeer.Player.Hands
{
    /// <summary>Tunable hand and reach values, editable per prefab variant.</summary>
    [System.Serializable]
    public struct HandSettings
    {
        [SerializeField] private Vector3 restOffset;
        [SerializeField] private Vector3 carryOffset;
        [SerializeField] private float restWeight;
        [SerializeField] private float carryWeight;
        [SerializeField] private float reachWeight;
        [SerializeField] private float weightBlendSpeed;
        [SerializeField] private float offsetBlendSpeed;
        [SerializeField] private float swayFollowSpeed;
        [SerializeField] private float maxSwayAngle;
        [SerializeField] private float reachDistance;

        /// <summary>Where an idle hand sits, in swayed view space.</summary>
        public Vector3 RestOffset => restOffset;

        /// <summary>Where a carried item sits, in swayed view space.</summary>
        public Vector3 CarryOffset => carryOffset;

        public float RestWeight => restWeight;
        public float CarryWeight => carryWeight;
        public float ReachWeight => reachWeight;
        public float WeightBlendSpeed => weightBlendSpeed;
        public float OffsetBlendSpeed => offsetBlendSpeed;

        /// <summary>
        /// How fast the hands catch up with the view rotation. This is the whole of the
        /// wobble now: low values make the arms swing wide when you whip the camera
        /// around, high values glue them to the view. It never affects translation.
        /// </summary>
        public float SwayFollowSpeed => swayFollowSpeed;

        /// <summary>
        /// Ceiling on how far the hands may trail the view. Without it a fast spin
        /// leaves the arms pointing backwards and the IK tears the shoulders apart.
        /// </summary>
        public float MaxSwayAngle => maxSwayAngle;

        public float ReachDistance => reachDistance;

        public static HandSettings Default => new()
        {
            restOffset = new Vector3(0.20f, -0.07f, 0.38f),
            carryOffset = new Vector3(0.17f, 0.00f, 0.40f),
            restWeight = 0.65f,
            carryWeight = 0.9f,
            reachWeight = 0.85f,
            weightBlendSpeed = 6f,
            offsetBlendSpeed = 10f,
            swayFollowSpeed = 7f,
            maxSwayAngle = 35f,
            reachDistance = 2.2f
        };
    }
}
