using System.Collections;
using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Pranks
{
    /// <summary>
    /// A fixed thing in the school that can be messed with bare-handed: a fire alarm,
    /// a blackboard. One component, tuned per prefab, so a new prank of this kind is
    /// data rather than code.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PrankTarget : NetworkBehaviour, IInteractionTarget
    {
        [SerializeField] private string prompt = "Faire une bêtise";
        [SerializeField] private string announcement = "une bêtise";
        [SerializeField, Min(0)] private int reputation = 20;
        [Tooltip("0 = silent: only an adult who sees the prankster reacts.")]
        [SerializeField, Min(0f)] private float noiseRadius = 15f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 30f;
        [SerializeField] private Renderer feedbackRenderer;
        [SerializeField] private Color feedbackColor = Color.red;
        [SerializeField, Min(0f)] private float feedbackSeconds = 4f;

        private readonly NetworkVariable<double> _readyAt = new();

        private Color _restColor;
        private Coroutine _feedback;

        public Transform Body => transform;
        public string Prompt => prompt;
        public string Announcement => announcement;
        public int Reputation => reputation;
        public float NoiseRadius => noiseRadius;

        public bool IsAvailable => IsSpawned && NetworkManager.ServerTime.Time >= _readyAt.Value;

        private void Awake()
        {
            if (feedbackRenderer != null)
            {
                _restColor = feedbackRenderer.sharedMaterial.color;
            }
        }

        /// <summary>Server only. False while cooling down.</summary>
        public bool TryTrigger()
        {
            if (!IsServer || !IsAvailable)
            {
                return false;
            }

            _readyAt.Value = NetworkManager.ServerTime.Time + cooldownSeconds;
            PlayFeedbackRpc();
            return true;
        }

        // An event, not state: a player joining mid-alarm missing the flash is fine.
        [Rpc(SendTo.Everyone)]
        private void PlayFeedbackRpc()
        {
            if (feedbackRenderer == null)
            {
                return;
            }

            if (_feedback != null)
            {
                StopCoroutine(_feedback);
            }

            _feedback = StartCoroutine(Blink());
        }

        private IEnumerator Blink()
        {
            var material = feedbackRenderer.material;
            var end = Time.time + feedbackSeconds;
            while (Time.time < end)
            {
                material.color = Mathf.Repeat(Time.time * 4f, 1f) < 0.5f ? feedbackColor : _restColor;
                yield return null;
            }

            material.color = _restColor;
            _feedback = null;
        }
    }
}
