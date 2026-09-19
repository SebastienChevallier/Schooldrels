using HoldMyBeer.Core;
using HoldMyBeer.Gameplay.Day;
using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Lunch
{
    /// <summary>
    /// The queue. You walk past holding a tray and it fills with today's menu — drawn
    /// by the server like everything else, so everyone eats the same thing.
    ///
    /// No choosing on purpose: a canteen you can pick from is a shop, and the joke is
    /// that you get what you get.
    /// </summary>
    public sealed class ServingLine : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float radius = 2.5f;
        [SerializeField, Min(0.05f)] private float checkInterval = 0.3f;

        private NetworkManager _networkManager;
        private IHeldItemTracker _tracker;
        private IDayStateProvider _day;
        private float _nextCheck;

        private void Start()
        {
            _networkManager = NetworkManager.Singleton;

            if (AppServices.IsReady)
            {
                AppServices.Container.TryResolve(out _tracker);
                AppServices.Container.TryResolve(out _day);
            }
        }

        private void Update()
        {
            if (_networkManager == null || !_networkManager.IsServer || _tracker == null ||
                Time.time < _nextCheck)
            {
                return;
            }

            _nextCheck = Time.time + checkInterval;

            var menu = _day?.Current?.Menu;
            if (menu == null || _day.Current.Phase != DayPhase.Lunch)
            {
                return;
            }

            foreach (var client in _networkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null ||
                    Vector3.Distance(client.PlayerObject.transform.position, transform.position) > radius)
                {
                    continue;
                }

                if (_tracker.TryGetHeldItem(client.ClientId, out var held) && held is Component component &&
                    component.TryGetComponent<Tray>(out var tray) && !tray.IsFull)
                {
                    tray.Serve(menu.Items);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
