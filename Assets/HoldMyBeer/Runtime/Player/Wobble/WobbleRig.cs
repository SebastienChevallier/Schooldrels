using System.Collections.Generic;
using UnityEngine;

namespace HoldMyBeer.Player.Wobble
{
    /// <summary>
    /// Owns the physical ragdoll and drives it towards the animated pose.
    /// Deliberately network-agnostic: it receives a tension and applies it, so it
    /// runs identically on the local avatar and on every remote one.
    /// </summary>
    public sealed class WobbleRig : MonoBehaviour
    {
        private const string PelvisBone = "mixamorig:Hips";
        private const string HeadBone = "mixamorig:Head";
        private const string LeftHandBone = "mixamorig:LeftHand";
        private const string RightHandBone = "mixamorig:RightHand";

        [SerializeField] private GameObject ragdollPrefab;
        [SerializeField] private Transform animatedRigRoot;
        [SerializeField] private WobbleSettings settings = WobbleSettings.Default;

        private readonly List<WobbleBone> _bones = new();

        private GameObject _ragdollInstance;
        private WobbleSolver _solver;
        private Rigidbody _pelvis;
        private Transform _animatedPelvis;
        private Transform _ragdollHead;
        private float _targetTension = 1f;
        private bool _rootSlavedToPelvis;

        public bool IsBuilt => _ragdollInstance != null;

        /// <summary>
        /// The physical hands, exposed so the hand layer can hang an item off the bone
        /// this machine actually simulates. Null until <see cref="Build"/> has run.
        /// </summary>
        public Rigidbody LeftHand { get; private set; }

        public Rigidbody RightHand { get; private set; }

        public float Tension => _solver?.Tension ?? 1f;
        public bool IsCollapsed => _solver != null && _solver.IsCollapsed;
        public Vector3 PelvisPosition => _pelvis != null ? _pelvis.position : transform.position;

        public void SetTargetTension(float tension)
        {
            _targetTension = Mathf.Clamp01(tension);
        }

        /// <summary>
        /// Told by <see cref="PlayerRagdollState"/> when the root has been handed over
        /// to the pelvis. While that holds, the towing spring must stay off: the
        /// animated pelvis it pulls towards is a child of the very root the pelvis is
        /// driving, so the two would push each other into the air.
        /// </summary>
        public void SetRootSlavedToPelvis(bool slaved)
        {
            _rootSlavedToPelvis = slaved;
        }

        /// <summary>
        /// Instantiates the ragdoll at the current pose. Must be called AFTER the
        /// root has been placed, otherwise the ragdoll is born at the world origin
        /// and gets catapulted at the first FixedUpdate.
        /// </summary>
        public void Build()
        {
            if (IsBuilt)
            {
                return;
            }

            if (ragdollPrefab == null || animatedRigRoot == null)
            {
                Debug.LogError(
                    $"[Hold My Beer] WobbleRig on '{name}' is missing its ragdoll prefab " +
                    "or animated rig reference. Re-run Tools > Hold My Beer > Generate Project Assets.");
                return;
            }

            _solver = new WobbleSolver(settings, settings.IdleTension);
            _targetTension = settings.IdleTension;

            _ragdollInstance = Instantiate(ragdollPrefab, transform.position, transform.rotation);
            // Named per instance so a stray ragdoll in DontDestroyOnLoad can be traced
            // back to the avatar that leaked it.
            _ragdollInstance.name = $"PlayerRagdoll_{GetEntityId()}";

            // The Player is spawned dynamically, so NGO keeps it across the
            // Menu -> Game load. The ragdoll must survive with it or we are left
            // holding a null reference.
            DontDestroyOnLoad(_ragdollInstance);

            PairBones();
            IgnoreOwnCharacterController();
            SnapToAnimatedPose();
        }

        public void Teardown()
        {
            if (_ragdollInstance != null)
            {
                Destroy(_ragdollInstance);
                _ragdollInstance = null;
            }

            _bones.Clear();
            _pelvis = null;
            _animatedPelvis = null;
            _ragdollHead = null;
            LeftHand = null;
            RightHand = null;
            _solver = null;
        }

        public void SnapToAnimatedPose()
        {
            // The pelvis first, and not as an afterthought: the ragdoll is a Transform
            // hierarchy rooted at it, so moving it afterwards would drag every bone we
            // just placed by the pelvis delta and undo the whole snap.
            if (_pelvis != null && _animatedPelvis != null)
            {
                _pelvis.position = _animatedPelvis.position;
                _pelvis.rotation = _animatedPelvis.rotation;
                _pelvis.transform.SetPositionAndRotation(
                    _animatedPelvis.position, _animatedPelvis.rotation);
                _pelvis.linearVelocity = Vector3.zero;
                _pelvis.angularVelocity = Vector3.zero;
            }

            foreach (var bone in _bones)
            {
                bone.SnapToAnimated();
            }
        }

        /// <summary>
        /// Shrinks the head bone away for the owner. Y Bot is a single mesh, so there
        /// is no separate first-person arm rig to maintain: the player simply looks
        /// out from a body whose head is scaled to nothing.
        /// </summary>
        public void SetHeadVisible(bool visible)
        {
            if (_ragdollHead != null)
            {
                // Not zero: a null scale is a non-invertible matrix and the skinning
                // complains about it every frame.
                _ragdollHead.localScale = visible ? Vector3.one : Vector3.one * 0.0001f;
            }
        }

        private void PairBones()
        {
            _bones.Clear();

            foreach (var joint in _ragdollInstance.GetComponentsInChildren<ConfigurableJoint>())
            {
                var animated = FindByName(animatedRigRoot, joint.name);
                if (animated == null)
                {
                    Debug.LogError(
                        $"[Hold My Beer] Ragdoll bone '{joint.name}' has no match in the " +
                        "animated rig. The two rigs come from different models.");
                    continue;
                }

                _bones.Add(new WobbleBone(joint.GetComponent<Rigidbody>(), joint, animated));
            }

            // The pelvis has no joint, so it is not in the loop above. It is the bone
            // towed by the root, and the spring towing it is what makes the body lag
            // behind — the wobble itself.
            var pelvisTransform = FindByName(_ragdollInstance.transform, PelvisBone);
            _pelvis = pelvisTransform != null ? pelvisTransform.GetComponent<Rigidbody>() : null;

            if (_pelvis == null)
            {
                Debug.LogError($"[Hold My Beer] Ragdoll has no '{PelvisBone}' rigidbody.");
            }

            // Cached once: FixedUpdate runs 50 times a second per player, and
            // CLAUDE.md forbids lookups in the per-frame path for exactly this reason.
            _animatedPelvis = FindByName(animatedRigRoot, PelvisBone);
            if (_animatedPelvis == null)
            {
                Debug.LogError($"[Hold My Beer] Animated rig has no '{PelvisBone}' bone.");
            }

            _ragdollHead = FindByName(_ragdollInstance.transform, HeadBone);

            LeftHand = FindBody(LeftHandBone);
            RightHand = FindBody(RightHandBone);
        }

        private Rigidbody FindBody(string boneName)
        {
            var bone = FindByName(_ragdollInstance.transform, boneName);
            return bone != null ? bone.GetComponent<Rigidbody>() : null;
        }

        /// <summary>
        /// The ragdoll must hit the ground but not the CharacterController towing it.
        /// Those two share the Default layer, so no collision-matrix row can separate
        /// them — it has to be done per collider pair, per instance.
        /// </summary>
        private void IgnoreOwnCharacterController()
        {
            if (!TryGetComponent<CharacterController>(out var controller))
            {
                return;
            }

            foreach (var bodyCollider in _ragdollInstance.GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(bodyCollider, controller, true);
            }
        }

        private void FixedUpdate()
        {
            if (!IsBuilt || _pelvis == null)
            {
                return;
            }

            var tension = _solver.Tick(_targetTension, Time.fixedDeltaTime);
            var spring = _solver.CurrentSpring;
            var damper = _solver.CurrentDamper;
            var maxForce = _solver.CurrentMaxForce;

            foreach (var bone in _bones)
            {
                bone.MatchAnimatedRotation();
                bone.ApplyTension(spring, damper, maxForce);
                bone.ClampVelocity(settings.MaxBoneSpeed, settings.MaxBoneAngularSpeed);
            }

            // The pelvis has no joint, so it is not a WobbleBone and would otherwise be
            // the one body never clamped — while being the only one we push directly.
            WobbleBone.ClampBody(_pelvis, settings.MaxBoneSpeed, settings.MaxBoneAngularSpeed,
                SnapToAnimatedPose);

            TowPelvis(tension);
            UprightPelvis(tension);
            RunWatchdog();
        }

        /// <summary>
        /// The spring that tows the pelvis towards the animated pose. This lag IS
        /// the wobble: too stiff and the body is rigid, too soft and it drags.
        /// </summary>
        private void TowPelvis(float tension)
        {
            if (tension <= 0f || _rootSlavedToPelvis || _animatedPelvis == null)
            {
                return;
            }

            var offset = _animatedPelvis.position - _pelvis.position;
            var force = offset * (settings.PelvisFollowSpring * tension)
                        - _pelvis.linearVelocity * (settings.PelvisFollowDamper * tension);

            _pelvis.AddForce(force, ForceMode.Acceleration);
        }

        /// <summary>
        /// Torque that keeps the pelvis facing the way the animated body faces.
        /// The joints only constrain bones relative to one another, so without this
        /// a perfectly posed character lying face down satisfies every joint and the
        /// avatar simply stays on the floor. Scaled by tension, so a full collapse
        /// still goes properly limp.
        /// </summary>
        private void UprightPelvis(float tension)
        {
            if (tension <= 0f || _rootSlavedToPelvis || _animatedPelvis == null)
            {
                return;
            }

            var delta = _animatedPelvis.rotation * Quaternion.Inverse(_pelvis.rotation);
            delta.ToAngleAxis(out var angle, out var axis);

            // ToAngleAxis returns [0,360]; past half a turn the short way round is the
            // other direction, and the axis degenerates when there is nothing to correct.
            if (angle > 180f)
            {
                angle -= 360f;
            }

            // Damping is applied even when the spring term is dropped: cutting both
            // near alignment leaves the pelvis free to coast through the target and
            // oscillate around it forever.
            var damping = -_pelvis.angularVelocity * (settings.PelvisUprightDamper * tension);

            if (Mathf.Abs(angle) < 0.01f || !float.IsFinite(angle) ||
                !float.IsFinite(axis.sqrMagnitude))
            {
                _pelvis.AddTorque(damping, ForceMode.Acceleration);
                return;
            }

            var torque = axis.normalized * (angle * Mathf.Deg2Rad * settings.PelvisUprightSpring * tension)
                         + damping;

            _pelvis.AddTorque(torque, ForceMode.Acceleration);
        }

        private void RunWatchdog()
        {
            // This will happen: a scene load, a timeScale of 0, a paused debugger.
            // Without the resnap the only recourse is restarting the match.
            // Negated rather than written as a plain greater-than: a NaN drift fails
            // every comparison, so `drift > threshold` would stay false in the one
            // case the watchdog exists for.
            var drift = (_pelvis.position - transform.position).sqrMagnitude;
            if (!(drift <= settings.WatchdogDistance * settings.WatchdogDistance))
            {
                SnapToAnimatedPose();
            }
        }

        private void OnDestroy()
        {
            Teardown();
        }

        private static Transform FindByName(Transform root, string boneName)
        {
            if (root.name == boneName)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindByName(root.GetChild(i), boneName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
