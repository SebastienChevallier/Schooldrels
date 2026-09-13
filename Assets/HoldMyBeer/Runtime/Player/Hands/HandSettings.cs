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
        [SerializeField] private float goalBlendSpeed;
        [SerializeField] private float reachDistance;

        /// <summary>Where an idle hand floats, in camera-pivot space.</summary>
        public Vector3 RestOffset => restOffset;

        /// <summary>Where a carried item sits, in camera-pivot space.</summary>
        public Vector3 CarryOffset => carryOffset;

        public float RestWeight => restWeight;
        public float CarryWeight => carryWeight;
        public float ReachWeight => reachWeight;
        public float WeightBlendSpeed => weightBlendSpeed;
        public float GoalBlendSpeed => goalBlendSpeed;
        public float ReachDistance => reachDistance;

        public static HandSettings Default => new()
        {
            // Forward and low: the hands hang in view without filling the screen, and
            // because the offsets are camera-relative they swing when the player looks
            // around, which is most of where the idle wiggle comes from.
            restOffset = new Vector3(0.28f, -0.35f, 0.45f),
            carryOffset = new Vector3(0.22f, -0.25f, 0.55f),
            restWeight = 0.45f,
            carryWeight = 0.9f,
            reachWeight = 0.85f,
            weightBlendSpeed = 6f,
            goalBlendSpeed = 12f,
            reachDistance = 2.2f
        };
    }
}
