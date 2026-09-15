using HoldMyBeer.Core;
using HoldMyBeer.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace HoldMyBeer.UI
{
    /// <summary>
    /// The crosshair, the "what would happen here" label, and the throw charge bar.
    ///
    /// Reads a published prompt rather than looking for the local player: the UI has no
    /// business knowing about avatars, rules or Netcode, and this way it keeps working
    /// whatever ends up writing the prompt.
    /// </summary>
    public sealed class CrosshairHud : MonoBehaviour
    {
        private static readonly Color Idle = new(1f, 1f, 1f, 0.35f);
        private static readonly Color Actionable = new(1f, 1f, 1f, 0.95f);

        private IInteractionPrompt _prompt;
        private Image _dot;
        private Text _label;
        private RectTransform _chargeTrack;
        private Image _chargeFill;

        private void Start()
        {
            if (AppServices.IsReady)
            {
                AppServices.Container.TryResolve(out _prompt);
            }

            Build();
        }

        private void Update()
        {
            if (_prompt == null)
            {
                return;
            }

            var hasAction = !string.IsNullOrEmpty(_prompt.Label);

            // The dot brightening is the actual "you can grab this" signal: it sits
            // where the player is already looking, unlike text they have to read.
            _dot.color = hasAction ? Actionable : Idle;

            _label.text = hasAction ? _prompt.Label : string.Empty;

            _chargeTrack.gameObject.SetActive(_prompt.IsCharging);
            if (_prompt.IsCharging)
            {
                _chargeFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(_prompt.Charge), 1f);
            }
        }

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas("HudCanvas", transform);

            // Behind every other canvas: the menu must stay clickable over the top.
            canvas.sortingOrder = -10;

            _dot = CreateImage(canvas.transform, "Crosshair", new Vector2(6f, 6f), Vector2.zero, Idle);

            var labelRect = CreateRect(canvas.transform, "Prompt", new Vector2(420f, 40f),
                new Vector2(0f, -52f));
            _label = UiFactory.CreateLabel(labelRect, string.Empty, 24, TextAnchor.MiddleCenter);
            UiFactory.StretchToParent(_label.rectTransform);

            _chargeTrack = CreateRect(canvas.transform, "ChargeTrack", new Vector2(180f, 8f),
                new Vector2(0f, -86f));
            AddImage(_chargeTrack.gameObject, new Color(0f, 0f, 0f, 0.45f));

            var fill = CreateRect(_chargeTrack, "ChargeFill", Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            _chargeFill = AddImage(fill.gameObject, UiFactory.Accent);

            _chargeTrack.gameObject.SetActive(false);
        }

        private static RectTransform CreateRect(Transform parent, string name, Vector2 size, Vector2 offset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
            return rect;
        }

        private static Image CreateImage(Transform parent, string name, Vector2 size, Vector2 offset,
                                         Color color)
        {
            return AddImage(CreateRect(parent, name, size, offset).gameObject, color);
        }

        private static Image AddImage(GameObject target, Color color)
        {
            var image = target.AddComponent<Image>();
            image.color = color;
            // Nothing here is clickable, and a raycast target over the crosshair would
            // swallow clicks meant for the world.
            image.raycastTarget = false;
            return image;
        }
    }
}
