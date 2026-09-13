using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Interaction
{
    /// <summary>
    /// Places a handful of grabbables when the game scene opens. Server-only by
    /// construction, like <c>PlayerSpawner</c>: spawning from a client is a bug, not
    /// a supported path.
    /// </summary>
    public sealed class GrabbableSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject grabbablePrefab;
        [SerializeField, Min(0)] private int count = 6;
        [SerializeField, Min(0.5f)] private float radius = 4f;
        [SerializeField] private float height = 1f;

        private void Start()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            if (grabbablePrefab == null)
            {
                Debug.LogError(
                    "[Hold My Beer] GrabbableSpawner has no prefab. " +
                    "Re-run Tools > Hold My Beer > Generate Project Assets.");
                return;
            }

            for (var i = 0; i < count; i++)
            {
                var angle = i * (360f / Mathf.Max(1, count)) * Mathf.Deg2Rad;
                var position = transform.position +
                               new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);

                var instance = Instantiate(grabbablePrefab, position, Quaternion.identity);
                instance.GetComponent<NetworkObject>().Spawn();
            }
        }
    }
}
