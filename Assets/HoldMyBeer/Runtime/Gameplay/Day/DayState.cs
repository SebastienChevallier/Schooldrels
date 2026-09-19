using System;
using System.Collections.Generic;
using HoldMyBeer.Core;
using HoldMyBeer.Interaction;
using HoldMyBeer.Networking;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Day
{
    /// <summary>
    /// One school day: the phase machine, the draw, the quota, and who owes what.
    /// Spawned dynamically by the server so it survives the network scene load, like
    /// <c>LobbyNetworkService</c>.
    ///
    /// Everything a latecomer needs — phase, clock, courses, locked rooms, menu,
    /// scores — is a NetworkVariable. Nothing here is announced by RPC alone, because
    /// a player who joins at lunchtime never catches up on an event.
    ///
    /// The loop it enforces: mischief pays more where it is dangerous, a detention
    /// takes half of what you earned, and the quota is judged over three days.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DayState : NetworkBehaviour, IDayState
    {
        private const int DaysPerCycleValue = 3;
        private const byte NoCourse = byte.MaxValue;

        [Header("Données")]
        [SerializeField] private DaySchedule schedule;
        [SerializeField] private CourseCatalog courses;
        [Tooltip("Les menus du self, décrits comme des cours : une salle (le réfectoire) et des items.")]
        [SerializeField] private CourseCatalog menus;
        [SerializeField] private ProgressionTable progression;

        [Header("Règles")]
        [SerializeField, Min(1)] private int firstCycleQuota = 300;
        [Tooltip("Le quota d'un cycle est celui du précédent multiplié par ça.")]
        [SerializeField, Min(1f)] private float quotaGrowth = 1.5f;
        [Tooltip("Combien de temps une bêtise laisse l'élève repérable.")]
        [SerializeField, Min(0f)] private float wantedSeconds = 8f;
        [Tooltip("Part de la réput' personnelle perdue en allant en colle.")]
        [SerializeField, Range(0f, 1f)] private float detentionPenalty = 0.5f;

        private readonly NetworkVariable<int> _cycle = new(1);
        private readonly NetworkVariable<int> _dayInCycle = new(1);
        private readonly NetworkVariable<int> _quota = new();
        private readonly NetworkVariable<int> _team = new();
        private readonly NetworkVariable<byte> _phase = new((byte)DayPhase.Arrival);
        private readonly NetworkVariable<double> _phaseEndsAt = new();
        private readonly NetworkVariable<byte> _course1 = new(NoCourse);
        private readonly NetworkVariable<byte> _course2 = new(NoCourse);
        private readonly NetworkVariable<byte> _menu = new(NoCourse);
        private readonly NetworkVariable<byte> _tier = new();
        private readonly NetworkVariable<uint> _lockedRooms = new();
        private readonly NetworkList<StudentScore> _scores = new();
        private readonly List<StudentScore> _snapshot = new();

        private DayStateProvider _provider;
        private MischiefBus _mischief;
        private ILobbyProvider _lobby;
        private ISchoolLayout _layout;
        private System.Random _random;

        public int DayInCycle => _dayInCycle.Value;
        public int Cycle => _cycle.Value;
        public int DaysPerCycle => DaysPerCycleValue;
        public int Quota => _quota.Value;
        public int TeamReputation => _team.Value;
        public DayPhase Phase => (DayPhase)_phase.Value;
        public IReadOnlyList<StudentScore> Scores => _snapshot;

        public CourseDefinition CurrentCourse => CourseFor(Phase);

        public CourseDefinition Menu => menus != null && _menu.Value != NoCourse ? menus[_menu.Value] : null;

        public ProgressionTier Tier => progression != null ? progression.For(_tier.Value) : null;

        public RoomId CurrentCourseRoom => CurrentCourse != null ? CurrentCourse.Room : RoomId.None;

        public float SecondsRemaining =>
            IsSpawned && Phase.IsInPlay()
                ? Mathf.Max(0f, (float)(_phaseEndsAt.Value - NetworkManager.ServerTime.Time))
                : 0f;

        public event Action<string> Announced;
        public event Action<DayPhase> PhaseChanged;

        public CourseDefinition CourseFor(DayPhase phase)
        {
            if (courses == null)
            {
                return null;
            }

            var index = phase switch
            {
                DayPhase.Class1 => _course1.Value,
                DayPhase.Class2 => _course2.Value,
                DayPhase.Lunch => NoCourse,
                _ => NoCourse
            };

            return index == NoCourse ? null : courses[index];
        }

        /// <summary>True when that room is locked right now. Replicated as a bit mask.</summary>
        public bool IsRoomLocked(RoomId room) => (_lockedRooms.Value & (1u << (int)room)) != 0u;

        public override void OnNetworkSpawn()
        {
            _scores.OnListChanged += HandleScoresChanged;
            _phase.OnValueChanged += HandlePhaseChanged;
            RebuildSnapshot();

            if (AppServices.IsReady)
            {
                AppServices.Container.TryResolve<IDayStateProvider>(out var provider);
                _provider = provider as DayStateProvider;

                AppServices.Container.TryResolve(out _lobby);
                AppServices.Container.TryResolve(out _layout);

                if (IsServer && AppServices.Container.TryResolve(out _mischief))
                {
                    _mischief.Reported += HandleMischief;
                }
            }

            if (IsServer)
            {
                // Seeded from nothing in particular: only the server ever draws, and
                // what it draws is replicated, so reproducibility buys nothing here.
                _random = new System.Random();
                StartCycle(1);
            }

            // Published last, and deliberately: whoever is watching the provider — the
            // director, the HUD — reads the day as soon as it appears, and it must
            // already be a drawn day rather than an empty one.
            _provider?.Set(this);
        }

        public override void OnNetworkDespawn()
        {
            _scores.OnListChanged -= HandleScoresChanged;
            _phase.OnValueChanged -= HandlePhaseChanged;

            if (_mischief != null)
            {
                _mischief.Reported -= HandleMischief;
            }

            if (_provider != null && ReferenceEquals(_provider.Current, this))
            {
                _provider.Set(null);
            }
        }

        public bool TryGetScore(ulong clientId, out StudentScore score)
        {
            foreach (var entry in _snapshot)
            {
                if (entry.ClientId == clientId)
                {
                    score = entry;
                    return true;
                }
            }

            score = StudentScore.ForClient(clientId);
            return false;
        }

        public bool IsWanted(ulong clientId) =>
            IsSpawned && Phase.IsInPlay() && TryGetScore(clientId, out var score) &&
            score.WantedUntil > NetworkManager.ServerTime.Time;

        public bool IsDetained(ulong clientId) =>
            IsSpawned && TryGetScore(clientId, out var score) && score.IsDetained;

        /// <summary>
        /// Being in the corridor while a class is on is suspicious in itself: that is
        /// what makes the class phases tense rather than just better paid.
        /// </summary>
        public bool IsSuspect(ulong clientId, Vector3 position)
        {
            if (IsWanted(clientId))
            {
                return true;
            }

            return Phase.IsClass() && !IsDetained(clientId) && _layout != null && _layout.RoomAt(position) == null;
        }

        public void RequestNextDay()
        {
            if (IsSpawned && IsHost)
            {
                NextDayRpc();
            }
        }

        /// <summary>Server only. An adult caught this student.</summary>
        public void SendToDetention(ulong clientId)
        {
            if (!IsServer || !Phase.IsInPlay() || !TryGetIndex(clientId, out var index))
            {
                return;
            }

            var score = _scores[index];
            if (score.IsDetained)
            {
                return;
            }

            var lost = Mathf.RoundToInt(score.Reputation * detentionPenalty);
            score.Reputation -= lost;
            score.Detentions++;
            score.WantedUntil = 0;

            // Half a day, expressed as a phase rather than a duration: whatever the
            // schedule says, a detention costs the rest of this half-day.
            score.DetainedUntilPhase = (byte)ReleasePhaseFor(Phase);
            _scores[index] = score;

            if (_layout?.DetentionPoint != null)
            {
                Teleport(clientId, _layout.DetentionPoint.position, _layout.DetentionPoint.rotation);
            }

            Announce(lost > 0
                ? $"{NameOf(clientId)} est collé ! -{lost} réput'"
                : $"{NameOf(clientId)} est collé !");
        }

        /// <summary>
        /// Server only. Everyone in detention walks out — what a friend opening the
        /// door is worth. Costs nothing more: the penalty was paid on arrest.
        /// </summary>
        public void ReleaseDetainees()
        {
            if (!IsServer)
            {
                return;
            }

            var freed = 0;
            for (var i = 0; i < _scores.Count; i++)
            {
                var score = _scores[i];
                if (!score.IsDetained)
                {
                    continue;
                }

                score.DetainedUntilPhase = StudentScore.NotDetained;
                _scores[i] = score;
                freed++;
            }

            if (freed > 0)
            {
                Announce(freed == 1 ? "Un élève est délivré !" : $"{freed} élèves sont délivrés !");
            }
        }

        /// <summary>Server only. Credits mischief, scaled by where and when it happened.</summary>
        public void Credit(ulong clientId, int reputation, Vector3 position, string label)
        {
            if (!IsServer || !Phase.IsInPlay() || !TryGetIndex(clientId, out var index))
            {
                return;
            }

            var scaled = Mathf.RoundToInt(reputation * ScaleAt(position));
            var score = _scores[index];
            score.WantedUntil = NetworkManager.ServerTime.Time + wantedSeconds;

            if (scaled > 0)
            {
                score.Reputation += scaled;
                _team.Value += scaled;
            }

            _scores[index] = score;

            Announce(scaled > 0
                ? $"{NameOf(clientId)} : {label} +{scaled}"
                : $"{NameOf(clientId)} : {label}");
        }

        private float ScaleAt(Vector3 position)
        {
            if (schedule == null)
            {
                return 1f;
            }

            var course = CurrentCourse;
            var inCourseRoom = course != null && _layout != null &&
                               _layout.RoomAt(position) is { } room && room.Id == course.Room;

            var scale = schedule.ReputationScale(Phase, inCourseRoom);
            return inCourseRoom && course != null ? scale * course.ReputationScale : scale;
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || !Phase.IsInPlay() ||
                NetworkManager.ServerTime.Time < _phaseEndsAt.Value)
            {
                return;
            }

            EnterPhase(Phase.Next());
        }

        [Rpc(SendTo.Server)]
        private void NextDayRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId || Phase != DayPhase.Recap)
            {
                return;
            }

            if (_dayInCycle.Value < DaysPerCycleValue)
            {
                StartDay(_dayInCycle.Value + 1);
                return;
            }

            // End of the cycle: met the quota and the school opens up, missed it and
            // the same cycle starts over. Never a game over.
            StartCycle(_team.Value >= _quota.Value ? _cycle.Value + 1 : _cycle.Value);
        }

        private void StartCycle(int cycle)
        {
            _cycle.Value = cycle;
            _quota.Value = Mathf.RoundToInt(firstCycleQuota * Mathf.Pow(quotaGrowth, cycle - 1));
            _team.Value = 0;
            _tier.Value = (byte)Mathf.Clamp(cycle - 1, 0, byte.MaxValue - 1);

            StartDay(1);
        }

        private void StartDay(int dayInCycle)
        {
            _dayInCycle.Value = dayInCycle;
            DrawTheDay();

            _scores.Clear();
            var index = 0;
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                _scores.Add(StudentScore.ForClient(clientId));
                SendToSpawn(clientId, index);
                index++;
            }

            EnterPhase(DayPhase.Arrival);
            Announce($"Jour {dayInCycle}/{DaysPerCycleValue} — cycle {_cycle.Value}, quota {_quota.Value}");
        }

        /// <summary>
        /// The whole day is drawn here, once, on the server: the two courses, the menu,
        /// and which rooms are locked. Replicated as indices and a bit mask — a client
        /// drawing its own would be playing a different school.
        /// </summary>
        private void DrawTheDay()
        {
            var tier = Tier;
            var available = courses != null
                ? Mathf.Clamp(tier != null ? tier.AvailableCourses : courses.Count, 1, Mathf.Max(1, courses.Count))
                : 0;

            if (available > 0)
            {
                var first = _random.Next(available);
                var second = available > 1 ? (first + 1 + _random.Next(available - 1)) % available : first;
                _course1.Value = (byte)first;
                _course2.Value = (byte)second;
            }
            else
            {
                _course1.Value = _course2.Value = NoCourse;
            }

            _menu.Value = menus != null && menus.Count > 0 ? (byte)_random.Next(menus.Count) : NoCourse;

            var lockChance = tier != null ? tier.LockedRoomChance : 0.8f;
            var mask = 0u;
            if (_layout != null)
            {
                foreach (var room in _layout.Rooms)
                {
                    // The corridor and the toilets are the two places always open: the
                    // player must never be locked out of somewhere to breathe.
                    if (room == null || room.Id is RoomId.Corridor or RoomId.Toilets)
                    {
                        continue;
                    }

                    if (_random.NextDouble() < lockChance)
                    {
                        mask |= 1u << (int)room.Id;
                    }
                }
            }

            _lockedRooms.Value = mask;
        }

        private void EnterPhase(DayPhase phase)
        {
            _phase.Value = (byte)phase;
            _phaseEndsAt.Value = NetworkManager.ServerTime.Time +
                                 (schedule != null ? schedule.SecondsFor(phase) : 60f);

            ReleaseServedSentences(phase);

            if (phase == DayPhase.Recap)
            {
                AnnounceVerdict();
                return;
            }

            var course = CourseFor(phase);
            Announce(course != null
                ? $"{phase.Label()} : {course.DisplayName}"
                : phase.Label());
        }

        /// <summary>Server only. Detentions run out at the start of the phase they name.</summary>
        private void ReleaseServedSentences(DayPhase phase)
        {
            for (var i = 0; i < _scores.Count; i++)
            {
                var score = _scores[i];
                if (score.IsDetained && (byte)phase >= score.DetainedUntilPhase)
                {
                    score.DetainedUntilPhase = StudentScore.NotDetained;
                    _scores[i] = score;
                }
            }
        }

        private static DayPhase ReleasePhaseFor(DayPhase phase) => phase switch
        {
            DayPhase.Class1 or DayPhase.Lunch => DayPhase.Class2,
            _ => DayPhase.Dismissal
        };

        private void AnnounceVerdict()
        {
            if (_dayInCycle.Value < DaysPerCycleValue)
            {
                Announce($"DRIIING ! Fin du jour {_dayInCycle.Value} — {_team.Value}/{_quota.Value}");
                return;
            }

            Announce(_team.Value >= _quota.Value
                ? $"DRIIING ! Quota tenu : {_team.Value}/{_quota.Value}"
                : $"DRIIING ! Quota raté : {_team.Value}/{_quota.Value} — LOOSER");
        }

        private void HandleMischief(MischiefReport report) =>
            Credit(report.ClientId, report.Reputation, report.Position, report.Label);

        private void HandlePhaseChanged(byte previous, byte current) => PhaseChanged?.Invoke((DayPhase)current);

        private bool TryGetIndex(ulong clientId, out int index)
        {
            for (index = 0; index < _scores.Count; index++)
            {
                if (_scores[index].ClientId == clientId)
                {
                    return true;
                }
            }

            // A player who joined mid-day still gets to play; they just start at zero.
            if (NetworkManager.ConnectedClients.ContainsKey(clientId))
            {
                _scores.Add(StudentScore.ForClient(clientId));
                index = _scores.Count - 1;
                return true;
            }

            return false;
        }

        private void SendToSpawn(ulong clientId, int index)
        {
            if (AppServices.IsReady && AppServices.Container.TryResolve<ISpawnPointProvider>(out var spawns))
            {
                spawns.GetSpawnPose(index, out var position, out var rotation);
                Teleport(clientId, position, rotation);
            }
        }

        private void Teleport(ulong clientId, Vector3 position, Quaternion rotation)
        {
            if (NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) &&
                client.PlayerObject != null &&
                client.PlayerObject.TryGetComponent<ITeleportable>(out var teleportable))
            {
                teleportable.TeleportTo(position, rotation);
            }
        }

        private string NameOf(ulong clientId)
        {
            if (_lobby?.Current != null)
            {
                foreach (var player in _lobby.Current.Players)
                {
                    if (player.ClientId == clientId)
                    {
                        return player.DisplayName.ToString();
                    }
                }
            }

            return $"Élève {clientId}";
        }

        private void Announce(string message)
        {
            var line = new FixedString128Bytes();
            line.CopyFromTruncated(message);
            AnnounceRpc(line);
        }

        [Rpc(SendTo.Everyone)]
        private void AnnounceRpc(FixedString128Bytes message) => Announced?.Invoke(message.ToString());

        private void HandleScoresChanged(NetworkListEvent<StudentScore> change) => RebuildSnapshot();

        private void RebuildSnapshot()
        {
            _snapshot.Clear();
            foreach (var score in _scores)
            {
                _snapshot.Add(score);
            }
        }
    }
}
