using HoldMyBeer.Interaction;
using HoldMyBeer.Player.Wobble;
using UnityEngine;

namespace HoldMyBeer.Player.Hands
{
    /// <summary>
    /// Decides where the hands go and keeps a held item glued to the local hand.
    ///
    /// The wobble lives here and nowhere else. The hands trail the ROTATION of the
    /// view and follow its POSITION exactly: turning your head makes the arms swing,
    /// walking does not make them drag. Every bone is pinned to the animated pose
    /// while the player is upright, so no lag can creep in from physics.
    ///
    /// Runs on every peer, not just the owner: each machine attaches the item to the
    /// hand IT simulates, which is why nothing about the attachment is replicated.
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

        private Quaternion _sway = Quaternion.identity;
        private bool _swayReady;

        private Vector3 _leftOffset;
        private Vector3 _rightOffset;
        private float _leftWeight;
        private float _rightWeight;
        private float _reachBlend;

        private Vector3 _lastHandPosition;
        private Vector3 _handVelocity;

        public HandSide HeldHand => _heldHand;

        public Vector3 ActiveHandPosition => HandBodyPosition(ActiveHand);

        public Quaternion ActiveHandRotation => HandBodyRotation(ActiveHand);

        /// <summary>
        /// Derived from successive positions rather than read off the Rigidbody: the
        /// bones are kinematic while the player is upright, and a kinematic body
        /// reports no velocity at all. A throw would otherwise lose the hand's motion.
        /// </summary>
        public Vector3 ActiveHandVelocity => _handVelocity;

        private HandSide ActiveHand => _heldHand != HandSide.None ? _heldHand : HandSide.Right;

        /// <summary>Told by the interactor what the crosshair is on, owner side only.</summary>
        public void SetReachTarget(IGrabbable target)
        {
            _reachTarget = target;
        }

        public void SetHeldItem(IGrabbable item, HandSide hand)
        {
            var changed = !ReferenceEquals(_heldItem, item);

            _heldItem = item;
            _heldHand = item != null ? hand : HandSide.None;

            if (changed && wobbleRig != null)
            {
                wobbleRig.SetArmsVisible(item != null);
            }
        }

        private void LateUpdate()
        {
            if (cameraPivot == null || ikDriver == null)
            {
                return;
            }

            var deltaTime = Time.deltaTime;

            UpdateSway(deltaTime);
            UpdateReachBlend(deltaTime);

            UpdateHand(HandSide.Left, ref _leftOffset, ref _leftWeight, deltaTime);
            UpdateHand(HandSide.Right, ref _rightOffset, ref _rightWeight, deltaTime);

            AttachHeldItem();
            TrackHandVelocity(deltaTime);
        }

        /// <summary>
        /// The swayed view rotation: a damped copy of where the player is looking.
        /// Only the rotation is damped — position is taken live — which is what makes
        /// the arms react to a turn without dragging behind a walk.
        /// </summary>
        private void UpdateSway(float deltaTime)
        {
            var target = cameraPivot.rotation;

            if (!_swayReady)
            {
                _sway = target;
                _swayReady = true;
                return;
            }

            _sway = Quaternion.Slerp(_sway, target,
                1f - Mathf.Exp(-settings.SwayFollowSpeed * deltaTime));

            // Clamped towards the target, not away from it: past this angle the arms
            // are behind the shoulders and the IK solver starts folding the elbows
            // through the chest.
            if (Quaternion.Angle(_sway, target) > settings.MaxSwayAngle)
            {
                _sway = Quaternion.RotateTowards(target, _sway, settings.MaxSwayAngle);
            }
        }

        private void UpdateReachBlend(float deltaTime)
        {
            var wantsReach = _reachTarget != null && _heldItem == null;
            _reachBlend = Mathf.MoveTowards(_reachBlend, wantsReach ? 1f : 0f, deltaTime * 4f);
        }

        private void UpdateHand(HandSide side, ref Vector3 offset, ref float weight, float deltaTime)
        {
            var mirror = side == HandSide.Left ? -1f : 1f;
            var holdsItem = _heldItem != null && _heldHand == side;

            var targetOffset = holdsItem ? settings.CarryOffset : settings.RestOffset;
            var targetWeight = holdsItem ? settings.CarryWeight : settings.RestWeight;

            // Blended in view space, never in world space: a world-space smoothing would
            // reintroduce exactly the translation lag this design exists to remove.
            offset = offset == Vector3.zero
                ? targetOffset
                : Vector3.Lerp(offset, targetOffset,
                    1f - Mathf.Exp(-settings.OffsetBlendSpeed * deltaTime));

            var goal = ViewPoint(offset, mirror);

            var reachesForItem = side == HandSide.Right && _reachTarget != null && _heldItem == null;
            if (reachesForItem && _reachBlend > 0f)
            {
                goal = Vector3.Lerp(goal, _reachTarget.GripAnchor.position, _reachBlend);
                targetWeight = Mathf.Lerp(targetWeight, settings.ReachWeight, _reachBlend);
            }

            weight = Mathf.Lerp(weight, targetWeight,
                1f - Mathf.Exp(-settings.WeightBlendSpeed * deltaTime));

            ikDriver.SetGoal(side, goal, _sway, weight);
        }

        /// <summary>Live position, swayed rotation. That split is the whole feature.</summary>
        private Vector3 ViewPoint(Vector3 offset, float mirror)
        {
            var mirrored = new Vector3(offset.x * mirror, offset.y, offset.z);
            return cameraPivot.position + _sway * mirrored;
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

        private void TrackHandVelocity(float deltaTime)
        {
            var current = ActiveHandPosition;

            if (deltaTime > 0f && _lastHandPosition != Vector3.zero)
            {
                _handVelocity = (current - _lastHandPosition) / deltaTime;
            }

            _lastHandPosition = current;
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
