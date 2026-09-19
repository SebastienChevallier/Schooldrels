using HoldMyBeer.Core;
using HoldMyBeer.Gameplay.Day;
using HoldMyBeer.Gameplay.Items;
using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace HoldMyBeer.Gameplay.Adults
{
    /// <summary>
    /// The school's monster. Walks its rounds, turns towards noise, and chases any
    /// student it can see who pranked in the last few seconds. Catching one sends
    /// them to detention and wipes their unposted reputation.
    ///
    /// Server-only brain; clients just watch the replicated transform and state.
    /// Deliberately readable rather than clever: players must be able to learn it.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class Supervisor : NetworkBehaviour
    {
        private const string RagdollLayerName = "PlayerRagdoll";

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 2.2f;
        [SerializeField, Min(0f)] private float chaseSpeed = 5.2f;
        [Tooltip("How far the goal must move before the path is recomputed.")]
        [SerializeField, Min(0f)] private float repathDistance = 0.5f;
        [SerializeField, Min(0f)] private float arrivalDistance = 0.6f;

        [Header("Senses")]
        [SerializeField, Min(0f)] private float sightRange = 14f;
        [SerializeField, Range(1f, 180f)] private float sightHalfAngle = 55f;
        [SerializeField] private float eyeHeight = 1.7f;
        [SerializeField, Min(0f)] private float catchDistance = 1.3f;
        [Tooltip("How long a chase survives without seeing the student.")]
        [SerializeField, Min(0f)] private float loseTrackSeconds = 3f;
        [SerializeField, Min(0f)] private float investigateLingerSeconds = 3f;

        [Header("Rôle")]
        [Tooltip("Le prof reste dans la salle où a lieu le cours ; le surveillant fait le tour du lycée.")]
        [SerializeField] private bool teachesTheClass;

        [Header("Look")]
        [SerializeField] private Renderer bodyRenderer;

        private readonly NetworkVariable<byte> _state = new((byte)SupervisorState.Patrol);

        private NavMeshAgent _agent;
        private MischiefBus _mischief;
        private IDayStateProvider _day;
        private ISchoolLayout _layout;
        private SmokeField _smoke;
        private int _sightMask = ~0;

        private int _waypoint;
        private Vector3 _goal;
        private ulong _target;
        private float _lastSeenAt;
        private float _lingerUntil;
        private SupervisorState _shownState = (SupervisorState)255;

        /// <summary>How sharp the adults are this cycle. Capped by the progression table.</summary>
        private float Vigilance => _day?.Current?.Tier != null ? _day.Current.Tier.VigilanceScale : 1f;

        private SupervisorState State
        {
            get => (SupervisorState)_state.Value;
            set => _state.Value = (byte)value;
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();

            // The ragdoll bones hang off the avatar: without masking them out, a
            // student's own arm would hide them from sight.
            var ragdollLayer = LayerMask.NameToLayer(RagdollLayerName);
            _sightMask = ragdollLayer >= 0 ? ~(1 << ragdollLayer) : ~0;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                // Clients only render. A live agent would snap the replicated
                // transform back onto its own idea of the mesh every frame.
                _agent.enabled = false;
                return;
            }

            if (AppServices.IsReady)
            {
                AppServices.Container.TryResolve(out _day);
                AppServices.Container.TryResolve(out _layout);
                AppServices.Container.TryResolve(out _smoke);
                if (AppServices.Container.TryResolve(out _mischief))
                {
                    _mischief.Reported += HandleMischief;
                }
            }

            _goal = transform.position;
            _agent.Warp(transform.position);
        }

        public override void OnNetworkDespawn()
        {
            if (_mischief != null)
            {
                _mischief.Reported -= HandleMischief;
            }
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            RefreshLook();

            if (!IsServer)
            {
                return;
            }

            var current = _day?.Current;
            if (current == null || !current.Phase.IsInPlay())
            {
                // After the bell, adults go back to walking: nobody gets caught on
                // the results screen.
                if (State != SupervisorState.Patrol)
                {
                    State = SupervisorState.Patrol;
                }

                Halt();

                return;
            }

            switch (State)
            {
                case SupervisorState.Patrol:
                    Patrol();
                    LookForSuspects(current);
                    break;
                case SupervisorState.Investigate:
                    Investigate();
                    LookForSuspects(current);
                    break;
                case SupervisorState.Chase:
                    Chase(current);
                    break;
            }
        }

        private void Patrol()
        {
            var route = CurrentRoute();
            if (route is not { Length: > 0 })
            {
                return;
            }

            var waypoint = route[_waypoint % route.Length];
            if (MoveTowards(waypoint.position, walkSpeed))
            {
                _waypoint = (_waypoint + 1) % route.Length;
            }
        }

        /// <summary>
        /// A teacher walks the room being taught in; everyone else walks the school.
        /// Two behaviours, one component: the difference is which markers it is given,
        /// not a second brain to keep in sync with this one.
        /// </summary>
        private Transform[] CurrentRoute()
        {
            if (!teachesTheClass || _layout == null)
            {
                return _layout?.PatrolRoute;
            }

            var room = _day?.Current?.CurrentCourseRoom ?? RoomId.None;
            if (room != RoomId.None && _layout.TryGetRoom(room, out var classroom) &&
                classroom.TeacherRoute is { Length: > 0 })
            {
                return classroom.TeacherRoute;
            }

            return _layout.PatrolRoute;
        }

        private void Investigate()
        {
            if (!MoveTowards(_goal, walkSpeed * 1.4f))
            {
                return;
            }

            if (_lingerUntil <= 0f)
            {
                _lingerUntil = Time.time + investigateLingerSeconds;
            }

            // Standing there looking around is what tells players "it heard something".
            Halt();
            transform.Rotate(Vector3.up, 90f * Time.deltaTime);

            if (Time.time >= _lingerUntil)
            {
                _lingerUntil = 0f;
                State = SupervisorState.Patrol;
            }
        }

        private void Chase(IDayState day)
        {
            if (!TryGetStudent(_target, out var student))
            {
                State = SupervisorState.Patrol;
                return;
            }

            if (CanSee(student.position))
            {
                _lastSeenAt = Time.time;
                _goal = student.position;
            }
            else if (Time.time - _lastSeenAt > loseTrackSeconds)
            {
                // Lost them: go and search where they were last seen.
                _lingerUntil = 0f;
                State = SupervisorState.Investigate;
                return;
            }

            MoveTowards(_goal, chaseSpeed);

            var flat = student.position - transform.position;
            flat.y = 0f;
            if (flat.magnitude <= catchDistance && day is DayState concrete)
            {
                // Caught with the loot: the theft is off, and the PC goes back to its
                // room. Being seen carrying it is the risk the objective is made of.
                if (TryGetLoot(_target, out var loot))
                {
                    loot.ReturnHome();
                }

                concrete.SendToDetention(_target);
                _lingerUntil = 0f;
                _goal = transform.position;
                State = SupervisorState.Investigate;
            }
        }

        /// <summary>
        /// Wanted for a prank, or simply in the corridor while a class is on: both are
        /// reasons to be chased, and the second is what makes a class phase tense.
        /// Someone already in detention is left alone.
        /// </summary>
        private void LookForSuspects(IDayState day)
        {
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null || day.IsDetained(client.ClientId))
                {
                    continue;
                }

                var position = client.PlayerObject.transform.position;
                if (day is DayState concrete ? !concrete.IsSuspect(client.ClientId, position)
                        : !day.IsWanted(client.ClientId))
                {
                    continue;
                }

                if (CanSee(position))
                {
                    _target = client.ClientId;
                    _lastSeenAt = Time.time;
                    State = SupervisorState.Chase;
                    return;
                }
            }
        }

        private void HandleMischief(MischiefReport report)
        {
            if (State == SupervisorState.Chase)
            {
                return;
            }

            var heard = report.NoiseRadius > 0f &&
                        Vector3.Distance(transform.position, report.Position) <= report.NoiseRadius;

            if (heard)
            {
                _goal = report.Position;
                _lingerUntil = 0f;
                State = SupervisorState.Investigate;
            }

            // A silent prank done in plain sight is still caught red-handed: the
            // Wanted flag it raises is picked up by the next LookForSuspects.
        }

        private bool TryGetLoot(ulong clientId, out LootItem loot)
        {
            loot = null;

            if (!AppServices.IsReady || !AppServices.Container.TryResolve<IHeldItemTracker>(out var tracker) ||
                !tracker.TryGetHeldItem(clientId, out var held) || held is not Component component)
            {
                return false;
            }

            return component.TryGetComponent(out loot);
        }

        private bool CanSee(Vector3 studentFeet)
        {
            var eye = transform.position + Vector3.up * eyeHeight;
            var chest = studentFeet + Vector3.up * 1.2f;
            var toStudent = chest - eye;
            var distance = toStudent.magnitude;

            if (distance > sightRange * Vigilance)
            {
                return false;
            }

            // Smoke is the one thing that takes an adult's senses away, so it is asked
            // here rather than baked into the ray: the particle system is decoration.
            if (_smoke != null && _smoke.Blocks(eye, chest))
            {
                return false;
            }

            var flatForward = transform.forward;
            if (distance > catchDistance * 2f && Vector3.Angle(flatForward, toStudent) > sightHalfAngle)
            {
                return false;
            }

            // Walls block sight; the student's own capsule does not count as a wall.
            if (Physics.Raycast(eye, toStudent / distance, out var hit, distance, _sightMask,
                    QueryTriggerInteraction.Ignore))
            {
                return hit.collider.GetComponentInParent<NetworkObject>() is { IsPlayerObject: true };
            }

            return true;
        }

        private bool TryGetStudent(ulong clientId, out Transform student)
        {
            student = NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) &&
                      client.PlayerObject != null
                ? client.PlayerObject.transform
                : null;
            return student != null;
        }

        /// <summary>True once within arrival distance of the point.</summary>
        private bool MoveTowards(Vector3 point, float speed)
        {
            var offset = point - transform.position;
            offset.y = 0f;

            if (offset.magnitude < arrivalDistance)
            {
                Halt();
                return true;
            }

            if (!_agent.isOnNavMesh)
            {
                return false;
            }

            _agent.speed = speed;
            _agent.updateRotation = true;
            _agent.isStopped = false;

            // A chase moves the goal every frame; recomputing the path each time would
            // be wasted work for a change of a few centimetres.
            if (!_agent.hasPath || (_agent.destination - point).sqrMagnitude > repathDistance * repathDistance)
            {
                _agent.SetDestination(point);
            }

            // Unreachable goal (a student on a desk, outside the mesh): the agent stops
            // at the closest point, which must count as arrived or it waits forever.
            return !_agent.pathPending && _agent.hasPath && _agent.remainingDistance <= _agent.stoppingDistance &&
                   _agent.pathStatus != NavMeshPathStatus.PathComplete;
        }

        private void Halt()
        {
            if (_agent.isOnNavMesh && !_agent.isStopped)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }

            // Lets the idle look-around turn the body without the agent turning it back.
            _agent.updateRotation = false;
        }

        private void RefreshLook()
        {
            if (bodyRenderer == null || _shownState == State)
            {
                return;
            }

            _shownState = State;
            bodyRenderer.material.color = State switch
            {
                SupervisorState.Chase => new Color(0.9f, 0.15f, 0.1f),
                SupervisorState.Investigate => new Color(0.95f, 0.65f, 0.1f),
                _ => new Color(0.2f, 0.35f, 0.75f)
            };
        }
    }
}
