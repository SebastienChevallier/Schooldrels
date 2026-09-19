using System.Collections.Generic;
using System.Text;
using HoldMyBeer.Core;
using HoldMyBeer.Gameplay.Day;
using HoldMyBeer.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace HoldMyBeer.UI
{
    /// <summary>
    /// The clock, the quota, the "you've been spotted" warning, the feed of pranks, and
    /// the end-of-day verdict. Reads the day through its provider only.
    /// </summary>
    public sealed class DayHud : MonoBehaviour
    {
        private const int FeedLength = 5;
        private const float FeedLineSeconds = 6f;

        private static readonly Color Danger = new(0.95f, 0.25f, 0.2f, 1f);
        private static readonly Color Success = new(0.4f, 0.9f, 0.45f, 1f);

        private readonly Queue<(string text, float expiresAt)> _feed = new();
        private readonly StringBuilder _builder = new();

        private IDayStateProvider _provider;
        private IDayState _day;
        private ILobbyProvider _lobby;

        private Text _clock;
        private Text _phaseLine;
        private Text _quota;
        private Text _mine;
        private Text _wanted;
        private Text _feedText;
        private RectTransform _endPanel;
        private Text _endTitle;
        private Text _endDetails;
        private Button _nextButton;
        private Text _nextCaption;
        private Text _waitHost;
        private DayPhase _shownPhase = (DayPhase)byte.MaxValue;

        private void Start()
        {
            Build();

            if (AppServices.IsReady)
            {
                AppServices.Container.TryResolve(out _lobby);
            }

            if (AppServices.IsReady && AppServices.Container.TryResolve(out _provider))
            {
                _provider.CurrentChanged += Attach;
                Attach(_provider.Current);
            }
        }

        private void OnDestroy()
        {
            if (_provider != null)
            {
                _provider.CurrentChanged -= Attach;
            }

            Attach(null);
        }

        private void Attach(IDayState day)
        {
            if (_day != null)
            {
                _day.Announced -= HandleAnnounced;
            }

            _day = day;

            if (_day != null)
            {
                _day.Announced += HandleAnnounced;
            }
        }

        private void Update()
        {
            var networkManager = NetworkManager.Singleton;
            if (_day == null || networkManager == null)
            {
                return;
            }

            var localId = networkManager.LocalClientId;
            var seconds = Mathf.CeilToInt(_day.SecondsRemaining);
            var phase = _day.Phase;

            _clock.text = $"Jour {_day.DayInCycle}/{_day.DaysPerCycle}   {seconds / 60:00}:{seconds % 60:00}";
            _clock.color = seconds <= 20 && phase.IsInPlay() ? Danger : UiFactory.TextColor;

            // The phase and what it opens: without this the player has no way to know
            // where the class is, and finding the room is half the phase.
            var course = _day.CurrentCourse;
            _phaseLine.text = course != null
                ? $"{phase.Label()} — {course.DisplayName} ({RoomName(course.Room)})"
                : phase.Label();

            _quota.text = $"Cycle {_day.Cycle}   réput' {_day.TeamReputation} / {_day.Quota}";
            _quota.color = _day.TeamReputation >= _day.Quota ? Success : UiFactory.TextColor;

            _day.TryGetScore(localId, out var mine);
            _mine.text = mine.IsDetained
                ? "COLLÉ — attends la prochaine heure, ou qu'un pote ouvre la porte"
                : $"Ta réput' : {mine.Reputation}";
            _mine.color = mine.IsDetained ? Danger : UiFactory.Accent;

            _wanted.gameObject.SetActive(phase.IsInPlay() && !mine.IsDetained && _day.IsWanted(localId));

            RefreshFeed();
            RefreshEndPanel(networkManager.IsHost);
        }

        private void HandleAnnounced(string line)
        {
            _feed.Enqueue((line, Time.time + FeedLineSeconds));
            while (_feed.Count > FeedLength)
            {
                _feed.Dequeue();
            }
        }

        private void RefreshFeed()
        {
            while (_feed.Count > 0 && _feed.Peek().expiresAt < Time.time)
            {
                _feed.Dequeue();
            }

            _builder.Clear();
            foreach (var (text, _) in _feed)
            {
                _builder.AppendLine(text);
            }

            _feedText.text = _builder.ToString();
        }

        private void RefreshEndPanel(bool isHost)
        {
            var phase = _day.Phase;
            if (phase == _shownPhase)
            {
                return;
            }

            _shownPhase = phase;
            var ended = phase == DayPhase.Recap;
            _endPanel.gameObject.SetActive(ended);

            // The player controller only locks the cursor at spawn, so the verdict has
            // to hand it back and the next day has to take it again.
            Cursor.lockState = ended ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = ended;

            if (!ended)
            {
                return;
            }

            // The verdict only exists on the last day of the cycle: the two days
            // before it are a recap, not a judgement, which is what gives the quota
            // its room to recover.
            var lastDay = _day.DayInCycle >= _day.DaysPerCycle;
            var won = lastDay && _day.TeamReputation >= _day.Quota;

            _endTitle.text = !lastDay ? $"FIN DU JOUR {_day.DayInCycle}" : won ? "QUOTA ATTEINT" : "LOOSER";
            _endTitle.color = !lastDay ? UiFactory.TextColor : won ? Success : Danger;
            _endDetails.text = BuildDetails(lastDay, won);

            _nextButton.gameObject.SetActive(isHost);
            _nextCaption.text = !lastDay ? "Jour suivant" : won ? "Cycle suivant" : "Recommencer le cycle";
            _waitHost.gameObject.SetActive(!isHost);
        }

        private string BuildDetails(bool lastDay, bool won)
        {
            _builder.Clear();

            if (!lastDay)
            {
                _builder.AppendLine($"{_day.TeamReputation} réput' sur {_day.Quota} — il reste " +
                                    $"{_day.DaysPerCycle - _day.DayInCycle} jour(s) pour y arriver.");
            }
            else if (won)
            {
                var tier = _day.Tier;
                _builder.AppendLine($"{_day.TeamReputation} réput' pour un quota de {_day.Quota}.");
                _builder.AppendLine(tier != null ? $"Débloqué : {tier.Headline}" : string.Empty);
            }
            else
            {
                _builder.AppendLine($"{_day.TeamReputation} réput' sur {_day.Quota}. " +
                                    "Le cycle recommence au jour 1 — rien n'est perdu.");
            }

            _builder.AppendLine();

            foreach (var score in _day.Scores)
            {
                _builder.AppendLine(
                    $"{NameOf(score.ClientId)} — {score.Reputation} réput', collé {score.Detentions}×");
            }

            return _builder.ToString();
        }

        private static string RoomName(RoomId room) => room switch
        {
            RoomId.General => "salle de cours",
            RoomId.Techno => "salle de techno",
            RoomId.Lab => "labo",
            RoomId.Gym => "gymnase",
            RoomId.Cafeteria => "réfectoire",
            RoomId.Toilets => "WC",
            RoomId.Detention => "bureau du CPE",
            RoomId.Staff => "loge",
            _ => "couloir"
        };

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

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas("DayHudCanvas", transform);
            canvas.sortingOrder = -5;

            _clock = CreateCorner(canvas.transform, new Vector2(0f, 1f), new Vector2(32f, -24f), 34,
                TextAnchor.UpperLeft);
            _phaseLine = CreateCorner(canvas.transform, new Vector2(0f, 1f), new Vector2(32f, -66f), 26,
                TextAnchor.UpperLeft);
            _quota = CreateCorner(canvas.transform, new Vector2(0f, 1f), new Vector2(32f, -102f), 24,
                TextAnchor.UpperLeft);
            _mine = CreateCorner(canvas.transform, new Vector2(0f, 1f), new Vector2(32f, -136f), 24,
                TextAnchor.UpperLeft);
            _mine.color = UiFactory.Accent;

            _feedText = CreateCorner(canvas.transform, new Vector2(1f, 1f), new Vector2(-32f, -24f), 22,
                TextAnchor.UpperRight);
            _feedText.rectTransform.sizeDelta = new Vector2(640f, 200f);

            _wanted = CreateCorner(canvas.transform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), 44,
                TextAnchor.UpperCenter);
            _wanted.text = "REPÉRÉ — ne te fais pas voir !";
            _wanted.color = Danger;
            _wanted.fontStyle = FontStyle.Bold;
            _wanted.gameObject.SetActive(false);

            _endPanel = UiFactory.CreatePanel(canvas.transform, "EndOfDay", new Vector2(760f, 560f));
            _endTitle = UiFactory.CreateLabel(_endPanel, string.Empty, 72, TextAnchor.MiddleCenter);
            _endTitle.fontStyle = FontStyle.Bold;
            _endDetails = UiFactory.CreateLabel(_endPanel, string.Empty, 24, TextAnchor.UpperCenter);
            _endDetails.GetComponent<LayoutElement>().minHeight = 260f;

            _nextButton = UiFactory.CreateButton(_endPanel, "Jour suivant", () => _day?.RequestNextDay());
            _nextCaption = _nextButton.GetComponentInChildren<Text>();
            _waitHost = UiFactory.CreateLabel(_endPanel, "En attente de l'hôte…", 24, TextAnchor.MiddleCenter);

            _endPanel.gameObject.SetActive(false);
        }

        private static Text CreateCorner(Transform parent, Vector2 anchor, Vector2 offset, int size,
                                         TextAnchor alignment)
        {
            var label = UiFactory.CreateLabel(parent, string.Empty, size, alignment);
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(640f, size + 16f);
            return label;
        }
    }
}
