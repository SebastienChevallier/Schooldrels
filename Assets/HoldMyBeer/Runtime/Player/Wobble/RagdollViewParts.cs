using UnityEngine;

namespace HoldMyBeer.Player.Wobble
{
    /// <summary>
    /// Switches the ragdoll between the full body everyone else sees and the arms-only
    /// version its owner sees.
    ///
    /// Two renderers over one skeleton rather than hiding bones: scaling a bone away
    /// wrecks the physics that hangs off it, which is exactly how the head ended up
    /// deviating 156 degrees from its target instead of 19.
    /// </summary>
    public sealed class RagdollViewParts : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer[] fullBody;
        [SerializeField] private SkinnedMeshRenderer[] armsOnly;

        private bool _armsBuilt;

        public void SetOwnerView(bool ownerView)
        {
            if (ownerView)
            {
                BuildArmsMeshes();
            }

            Apply(fullBody, !ownerView);
            Apply(armsOnly, ownerView);
        }

        /// <summary>
        /// Filled on first use rather than baked into the prefab: an arms mesh created
        /// and referenced within a single editor frame does not survive serialisation
        /// reliably, and the symptom is a renderer showing nothing at all.
        /// </summary>
        private void BuildArmsMeshes()
        {
            if (_armsBuilt || fullBody == null || armsOnly == null)
            {
                return;
            }

            _armsBuilt = true;

            var count = Mathf.Min(fullBody.Length, armsOnly.Length);
            for (var i = 0; i < count; i++)
            {
                if (fullBody[i] == null || armsOnly[i] == null || armsOnly[i].sharedMesh != null)
                {
                    continue;
                }

                armsOnly[i].sharedMesh = ArmsOnlyMesh.For(fullBody[i].sharedMesh, fullBody[i].bones);
            }
        }

        private static void Apply(SkinnedMeshRenderer[] renderers, bool visible)
        {
            if (renderers == null)
            {
                return;
            }

            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }
    }
}
