using System;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay
{
    /// <summary>
    /// Spawns networked props at scene-authored markers. Props are spawned rather than
    /// placed in the scene so they follow the one path already proven here — the
    /// prefab list — instead of in-scene object hashes baked by a code generator.
    /// </summary>
    public sealed class NetworkPropSpawner : MonoBehaviour
    {
        [Serializable]
        public struct Placement
        {
            public GameObject prefab;
            public Transform marker;
        }

        [SerializeField] private Placement[] placements = Array.Empty<Placement>();

        private void Start()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            foreach (var placement in placements)
            {
                if (placement.prefab == null || placement.marker == null)
                {
                    Debug.LogError("[Hold My Beer] NetworkPropSpawner has an empty placement. " +
                                   "Re-run Tools > Hold My Beer > Generate Project Assets.");
                    continue;
                }

                var instance = Instantiate(placement.prefab, placement.marker.position, placement.marker.rotation);
                instance.GetComponent<NetworkObject>().Spawn();
            }
        }
    }
}
