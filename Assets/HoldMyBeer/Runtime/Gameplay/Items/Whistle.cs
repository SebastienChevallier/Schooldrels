using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Items
{
    /// <summary>
    /// The PE whistle. It scores nothing and it is the most useful object in the
    /// school: it makes a noise somewhere the adults can hear, so they go there.
    /// Everything in this game is about where the adults are looking.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class Whistle : NetworkBehaviour, IUsableItem
    {
        [SerializeField, Min(0f)] private float noiseRadius = 45f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 6f;

        private readonly NetworkVariable<double> _readyAt = new();

        public bool IsCharged => false;

        public bool CanUse(in InteractionRequest request, out string prompt)
        {
            prompt = null;

            if (!IsSpawned || NetworkManager.ServerTime.Time < _readyAt.Value)
            {
                return false;
            }

            prompt = "Siffler";
            return true;
        }

        public void Use(in InteractionRequest request, IInteractionContext context)
        {
            if (!IsServer || NetworkManager.ServerTime.Time < _readyAt.Value)
            {
                return;
            }

            _readyAt.Value = NetworkManager.ServerTime.Time + cooldownSeconds;

            // Worth nothing in reputation on purpose: a diversion that also paid would
            // be strictly better than doing anything else.
            context.ReportMischief(new MischiefReport(
                request.ClientId, transform.position, 0, noiseRadius, "coup de sifflet"));
        }
    }
}
