using HoldMyBeer.Interaction;
using HoldMyBeer.Player.Wobble;
using UnityEngine;

namespace HoldMyBeer.Player.Hands
{
    /// <summary>
    /// Decides where the hands go and keeps a held item glued to the local hand.
    ///
    /// Runs on every peer, not just the owner: each machine attaches the item to the
    /// hand IT simulates. That is the whole reason nothing about the attachment is
    /// replicated — the ragdolls diverge, and letting each one carry its own copy is
    /// what makes the item look welded to the hand everywhere.
    /// </summary>
    public sealed class PlayerHands : MonoBehaviour
    {
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private WobbleRig wobbleRig;
        [SerializeField] private HandIkDriver ikDriver;
        [SerializeField] private HandSettings settings = HandSettings.Default;

        private IGrabbable _heldItem;
        private HandSide _heldHand = HandSide.None;
        private IGrabbable _reachTarget;

        private Vector3 _leftGoal;
        private Vector3 _rightGoal;
        private float _leftWeight;
        private float _rightWeight;

        public HandSide HeldHand => _heldHand;

        /// <summary>The hand a rule should throw from, in world space.</summary>
        public Vector3 ActiveHandPosition => HandBodyPosition(ActiveHand);

        public Quaternion ActiveHandRotation => HandBodyRotation(ActiveHand);

        public Vector3 ActiveHandVelocity
        {
            get
            {
                var body = HandBody(ActiveHand);
                return body != null ? body.linearVelocity : Vector3.zero;
            }
        }

        private HandSide ActiveHand => _heldHand != HandSide.None ? _heldHand : HandSide.Right;

        /// <summary>Told by the interactor what the crosshair is on, owner side only.</summary>
        public void SetReachTarget(IGrabbable target)
        {
            _reachTarget = target;
        }

        public void SetHeldItem(IGrabbable item, HandSide hand)
        {
            _heldItem = item;
            _heldHand = item != null ? hand : HandSide.None;
        }

        private void LateUpdate()
        {
            if (cameraPivot == null || ikDriver == null)
            {
                return;
            }

            UpdateHand(HandSide.Left, ref _leftGoal, ref _leftWeight);
            UpdateHand(HandSide.Right, ref _rightGoal, ref _rightWeight);

            AttachHeldItem();
        }

        private void UpdateHand(HandSide side, ref Vector3 goal, ref float weight)
        {
            var mirror = side == HandSide.Left ? -1f : 1f;
            var targetWeight = settings.RestWeight;

            Vector3 targetGoal;

            if (_heldItem != null && _heldHand == side)
            {
                targetGoal = CameraPoint(settings.CarryOffset, mirror);
                targetWeight = settings.CarryWeight;
            }
            else if (_reachTarget != null && side == HandSide.Right && _heldItem == null)
            {
                targetGoal = _reachTarget.GripAnchor.position;
                targetWeight = settings.ReachWeight;
            }
            else
            {
                targetGoal = CameraPoint(settings.RestOffset, mirror);
            }

            // Smoothed rather than snapped: the IK goal is what the physical arm chases,
            // so a jump here would be a jerk of the whole arm rather than a wobble.
            goal = goal == Vector3.zero
                ? targetGoal
                : Vector3.Lerp(goal, targetGoal, 1f - Mathf.Exp(-settings.GoalBlendSpeed * Time.deltaTime));

            weight = Mathf.Lerp(weight, targetWeight,
                1f - Mathf.Exp(-settings.WeightBlendSpeed * Time.deltaTime));

            ikDriver.SetGoal(side, goal, cameraPivot.rotation, weight);
        }

        private Vector3 CameraPoint(Vector3 offset, float mirror)
        {
            return cameraPivot.TransformPoint(new Vector3(offset.x * mirror, offset.y, offset.z));
        }

        /// <summary>
        /// Places the item so its grip anchor meets the palm, every frame, on every
        /// peer. Anchor-relative rather than origin-relative so a mug can hang from
        /// its handle instead of floating by its centre.
        /// </summary>
        private void AttachHeldItem()
        {
            if (_heldItem == null)
            {
                return;
            }

            var hand = HandBody(_heldHand);
            if (hand == null)
            {
                return;
            }

            var body = _heldItem.Body;
            var anchor = _heldItem.GripAnchor;

            body.rotation = hand.rotation;
            body.position = hand.position + (body.position - anchor.position);
        }

        private Rigidbody HandBody(HandSide side)
        {
            if (wobbleRig == null || !wobbleRig.IsBuilt)
            {
                return null;
            }

            return side == HandSide.Left ? wobbleRig.LeftHand : wobbleRig.RightHand;
        }

        private Vector3 HandBodyPosition(HandSide side)
        {
            var body = HandBody(side);
            return body != null ? body.position : transform.position + transform.forward;
        }

        private Quaternion HandBodyRotation(HandSide side)
        {
            var body = HandBody(side);
            return body != null ? body.rotation : transform.rotation;
        }
    }
}
