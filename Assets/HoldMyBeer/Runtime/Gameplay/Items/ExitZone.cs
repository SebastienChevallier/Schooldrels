using HoldMyBeer.Core;
using HoldMyBeer.Gameplay.Interaction;
using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Items
{
    /// <summary>
    /// The gate. Carrying loot through it is what turns a stolen PC into reputation.
    /// Server-side by construction: the zone watches, the client does not report.
    /// </summary>
    public sealed class ExitZone : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float radius = 3f;
        [SerializeField, Min(0.05f)] private float checkInterval = 0.25f;

        private IHeldItemTracker _tracker;
        private NetworkManager _networkManager;
        private float _nextCheck;

        private void Start()
        {
            _networkManager = NetworkManager.Singleton;

            if (AppServices.IsReady)
            {
                AppServices.Container.TryResolve(out _tracker);
            }
        }

        private void Update()
        {
            if (_networkManager == null || !_networkManager.IsServer || _tracker == null ||
                Time.time < _nextCheck)
            {
                return;
            }

            // A zone does not need to be frame-accurate, and polling every client's
            // position every frame is exactly the kind of cost a host cannot afford.
            _nextCheck = Time.time + checkInterval;

            foreach (var client in _networkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null ||
                    Vector3.Distance(client.PlayerObject.transform.position, transform.position) > radius)
                {
                    continue;
                }

                if (_tracker.TryGetHeldItem(client.ClientId, out var held) && held is Component component &&
                    component.TryGetComponent<LootItem>(out var loot) && loot.TryDeliver(client.ClientId))
                {
                    loot.ReturnHome();
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 0.5f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
