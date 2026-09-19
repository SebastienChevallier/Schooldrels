using HoldMyBeer.Gameplay.Interaction;
using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Items
{
    /// <summary>
    /// A flask of reagent. On its own it is a projectile; pointed at another reagent or
    /// at a hot plate it is a reaction — which is exactly the shape CLAUDE.md §5
    /// describes for the empty glass and the beer tap, one pair at a time.
    ///
    /// Three reagents are plenty: that is already six pairs, and nobody remembers more.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class ChemicalItem : NetworkBehaviour, IUsableItem
    {
        public enum Reagent : byte
        {
            A = 0,
            B = 1,
            C = 2
        }

        [SerializeField] private Reagent reagent = Reagent.A;
        [Tooltip("Le nuage spawné par un mélange. Il coupe la vue des adultes.")]
        [SerializeField] private GameObject smokePrefab;
        [SerializeField, Min(0)] private int mixReputation = 35;
        [SerializeField, Min(0)] private int blastReputation = 50;
        [SerializeField, Min(0f)] private float mixNoiseRadius = 8f;
        [SerializeField, Min(0f)] private float blastNoiseRadius = 40f;
        [SerializeField, Min(0.1f)] private float reach = 2.5f;

        public bool IsCharged => false;

        public bool CanUse(in InteractionRequest request, out string prompt)
        {
            prompt = null;

            if (!IsSpawned || Vector3.Distance(request.AimPoint, request.HandPosition) > reach)
            {
                return false;
            }

            if (TryGetOther(request, out var other))
            {
                prompt = $"Mélanger ({reagent} + {other.reagent})";
                return true;
            }

            if (request.AimedTarget is HeatSource { IsAvailable: true })
            {
                prompt = "Faire chauffer";
                return true;
            }

            return false;
        }

        public void Use(in InteractionRequest request, IInteractionContext context)
        {
            if (!IsServer)
            {
                return;
            }

            if (TryGetOther(request, out var other))
            {
                var position = other.transform.position;
                context.SpawnItem(smokePrefab, position + Vector3.up, Quaternion.identity, Vector3.zero);
                context.ReportMischief(new MischiefReport(
                    request.ClientId, position, mixReputation, mixNoiseRadius, "fait fumer la paillasse"));

                // Both flasks are consumed: a reaction you can repeat on the spot is a
                // smoke machine, not a decision.
                if (other.TryGetComponent<GrabbableItem>(out var otherItem))
                {
                    context.Despawn(otherItem);
                }

                context.Despawn(request.HeldItem);
                return;
            }

            if (request.AimedTarget is HeatSource { IsAvailable: true } heat)
            {
                context.ReportMischief(new MischiefReport(
                    request.ClientId, heat.Body.position, blastReputation, blastNoiseRadius, "fait exploser une fiole"));
                context.Despawn(request.HeldItem);
            }
        }

        /// <summary>Another flask, of a different reagent, under the crosshair.</summary>
        private bool TryGetOther(in InteractionRequest request, out ChemicalItem other)
        {
            other = request.AimedGrabbable is Component aimed ? aimed.GetComponent<ChemicalItem>() : null;
            if (other == null || ReferenceEquals(other, this) || other.reagent == reagent)
            {
                other = null;
                return false;
            }

            return true;
        }
    }
}
