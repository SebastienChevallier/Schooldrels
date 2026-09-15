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
    /// One school day: the clock, the quota, and who owes what. Spawned dynamically by
    /// the server so it outlives nothing but the session, like <c>LobbyNetworkService</c>.
    ///
    /// The loop it enforces is the whole risk/reward of the game: pranks earn pending
    /// reputation, only posting it makes it count, and detention wipes what was not
    /// posted yet.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DayState : NetworkBehaviour, IDayState
    {
        [SerializeField, Min(10f)] private float dayDurationSeconds = 300f;
        [SerializeField, Min(1)] private int firstDayQuota = 100;
        [Tooltip("Each day's quota is the previous one times this.")]
        [SerializeField, Min(1f)] private float quotaGrowth = 1.5f;
        [Tooltip("How long after a prank an adult who sees you gives chase.")]
        [SerializeField, Min(0f)] private float wantedSeconds = 8f;

        private readonly NetworkVariable<int> _day = new(1);
        private readonly NetworkVariable<int> _quota = new();
        private readonly NetworkVariable<int> _team = new();
        private readonly NetworkVariable<byte> _phase = new((byte)DayPhase.Playing);
        private readonly NetworkVariable<double> _endsAt = new();
        private readonly NetworkList<StudentScore> _scores = new();
        private readonly List<StudentScore> _snapshot = new();

        private DayStateProvider _provider;
        private MischiefBus _mischief;
        private ILobbyProvider _lobby;

        public int DayNumber => _day.Value;
        public int Quota => _quota.Value;
        public int TeamReputation => _team.Value;
        public DayPhase Phase => (DayPhase)_phase.Value;
        public IReadOnlyList<StudentScore> Scores => _snapshot;

        public float SecondsRemaining =>
            IsSpawned ? Mathf.Max(0f, (float)(_endsAt.Value - NetworkManager.ServerTime.Time)) : 0f;

        public event Action<string> Announced;

        public override void OnNetworkSpawn()
        {
            _scores.OnListChanged += HandleScoresChanged;
            RebuildSnapshot();

            if (AppServices.IsReady)
            {
                if (AppServices.Container.TryResolve<IDayStateProvider>(out var provider))
                {
                    _provider = provider as DayStateProvider;
                    _provider?.Set(this);
                }

                AppServices.Container.TryResolve(out _lobby);

                if (IsServer && AppServices.Container.TryResolve(out _mischief))
                {
                    _mischief.Reported += HandleMischief;
                }
            }

            if (IsServer)
            {
                StartDay(1, regroup: false);
            }
        }

        public override void OnNetworkDespawn()
        {
            _scores.OnListChanged -= HandleScoresChanged;

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

            score = default;
            return false;
        }

        public bool IsWanted(ulong clientId) =>
            IsSpawned && TryGetScore(clientId, out var score) && score.WantedUntil > NetworkManager.ServerTime.Time;

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
            if (!IsServer || Phase != DayPhase.Playing || !TryGetIndex(clientId, out var index))
            {
                return;
            }

            var score = _scores[index];
            var lost = score.Pending;
            score.Pending = 0;
            score.Detentions++;
            score.WantedUntil = 0;
            _scores[index] = score;

            if (AppServices.IsReady && AppServices.Container.TryResolve<ISchoolLayout>(out var layout) &&
                layout.DetentionPoint != null)
            {
                Teleport(clientId, layout.DetentionPoint.position, layout.DetentionPoint.rotation);
            }

            Announce(lost > 0 ? $"{NameOf(clientId)} est collé ! -{lost} réput'" : $"{NameOf(clientId)} est collé !");
        }

        /// <summary>Server only. Returns false when there was nothing to post.</summary>
        public bool Bank(ulong clientId)
        {
            if (!IsServer || Phase != DayPhase.Playing || !TryGetIndex(clientId, out var index))
            {
                return false;
            }

            var score = _scores[index];
            if (score.Pending <= 0)
            {
                return false;
            }

            _team.Value += score.Pending;
            Announce($"{NameOf(clientId)} poste sa vidéo : +{score.Pending}");

            score.Posted += score.Pending;
            score.Pending = 0;
            _scores[index] = score;
            return true;
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || Phase != DayPhase.Playing ||
                NetworkManager.ServerTime.Time < _endsAt.Value)
            {
                return;
            }

            var met = _team.Value >= _quota.Value;
            _phase.Value = (byte)(met ? DayPhase.QuotaMet : DayPhase.Loser);
            Announce(met ? "DRIIING ! Quota atteint." : "DRIIING ! Quota raté... LOOSER.");
        }

        [Rpc(SendTo.Server)]
        private void NextDayRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId || Phase == DayPhase.Playing)
            {
                return;
            }

            // A loss sends the whole class back to Monday: the quota only ever grows
            // for a group that keeps meeting it.
            StartDay(Phase == DayPhase.QuotaMet ? _day.Value + 1 : 1, regroup: true);
        }

        private void StartDay(int day, bool regroup)
        {
            _day.Value = day;
            _quota.Value = Mathf.RoundToInt(firstDayQuota * Mathf.Pow(quotaGrowth, day - 1));
            _team.Value = 0;
            _endsAt.Value = NetworkManager.ServerTime.Time + dayDurationSeconds;
            _phase.Value = (byte)DayPhase.Playing;

            _scores.Clear();
            var index = 0;
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                _scores.Add(new StudentScore { ClientId = clientId });

                // The first day starts where PlayerSpawner already put everyone; every
                // later one comes back from wherever the bell caught them.
                if (regroup)
                {
                    SendToSpawn(clientId, index);
                }

                index++;
            }

            Announce($"Jour {day} — quota : {_quota.Value} réput'");
        }

        private void HandleMischief(MischiefReport report)
        {
            if (Phase != DayPhase.Playing || !TryGetIndex(report.ClientId, out var index))
            {
                return;
            }

            var score = _scores[index];
            score.Pending += report.Reputation;
            score.WantedUntil = NetworkManager.ServerTime.Time + wantedSeconds;
            _scores[index] = score;

            Announce($"{NameOf(report.ClientId)} : {report.Label} +{report.Reputation}");
        }

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
                _scores.Add(new StudentScore { ClientId = clientId });
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
        private void AnnounceRpc(FixedString128Bytes message)
        {
            Announced?.Invoke(message.ToString());
        }

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
