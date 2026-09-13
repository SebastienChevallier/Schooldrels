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
            // Pushed well forward rather than merely lowered. Close to the lens a small
            // drop is a huge angle — at 0.25 m a hand hanging 0.25 m low sits 45 degrees
            // below centre, outside the frame. At 0.6 m the same droop is 22 degrees and
            // stays in view.
            //
            // Aimed slightly ABOVE the pivot on purpose: the arms are deliberately the
            // loosest bones on the body, so the physical hand settles roughly 0.2 m
            // under its IK goal and this is what pays for that sag.
            restOffset = new Vector3(0.20f, 0.30f, 0.58f),
            carryOffset = new Vector3(0.17f, 0.34f, 0.56f),
            restWeight = 0.65f,
            carryWeight = 0.9f,
            reachWeight = 0.85f,
            weightBlendSpeed = 6f,
            goalBlendSpeed = 12f,
            reachDistance = 2.2f
        };
    }
}
