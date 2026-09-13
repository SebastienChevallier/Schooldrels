using HoldMyBeer.Interaction;
using UnityEngine;

namespace HoldMyBeer.Player.Hands
{
    /// <summary>
    /// Writes hand goals into the humanoid IK pass. Lives on the AnimatedRig rather
    /// than on the player root because Unity only calls OnAnimatorIK on the object
    /// that carries the Animator — a constraint worth obeying rather than working
    /// around.
    ///
    /// Deliberately witless: it applies what it is given and decides nothing, which
    /// keeps every decision in <see cref="PlayerHands"/> where it can be reasoned
    /// about without an Animator in the way.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class HandIkDriver : MonoBehaviour
    {
        private struct Goal
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Weight;
        }

        private Animator _animator;
        private Goal _left;
        private Goal _right;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _left.Rotation = Quaternion.identity;
            _right.Rotation = Quaternion.identity;
        }

        public void SetGoal(HandSide side, Vector3 position, Quaternion rotation, float weight)
        {
            var goal = new Goal
            {
                Position = position,
                Rotation = rotation,
                Weight = Mathf.Clamp01(weight)
            };

            if (side == HandSide.Left)
            {
                _left = goal;
            }
            else if (side == HandSide.Right)
            {
                _right = goal;
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            Apply(AvatarIKGoal.LeftHand, _left);
            Apply(AvatarIKGoal.RightHand, _right);
        }

        private void Apply(AvatarIKGoal goal, Goal value)
        {
            _animator.SetIKPositionWeight(goal, value.Weight);
            _animator.SetIKRotationWeight(goal, value.Weight);

            if (value.Weight <= 0f)
            {
                return;
            }

            _animator.SetIKPosition(goal, value.Position);
            _animator.SetIKRotation(goal, value.Rotation);
        }
    }
}
