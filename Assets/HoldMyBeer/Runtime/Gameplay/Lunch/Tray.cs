using HoldMyBeer.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.Gameplay.Lunch
{
    /// <summary>
    /// The canteen tray. It fills itself as you walk the line — there is no choosing,
    /// which is the joke — and then you take portions off it, one per use, and those
    /// portions are what flies across the room.
    ///
    /// How many portions are left is replicated state: a player joining mid-lunch has
    /// to see a half-eaten tray as a half-eaten tray.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class Tray : NetworkBehaviour, IUsableItem
    {
        [SerializeField, Min(1)] private int capacity = 4;
        [SerializeField, Min(1f)] private float throwSpeed = 9f;
        [Tooltip("Petit lob : la bouffe doit arriver en cloche, pas en balle de fusil.")]
        [SerializeField, Min(0f)] private float lob = 1.5f;

        [Tooltip("Les visuels de portions, masqués au fur et à mesure qu'on les prend.")]
        [SerializeField] private GameObject[] portionVisuals = System.Array.Empty<GameObject>();

        private readonly NetworkVariable<byte> _portions = new();
        private readonly NetworkVariable<byte> _menuItem = new();

        private GameObject[] _menuPrefabs = System.Array.Empty<GameObject>();

        public int Portions => _portions.Value;
        public bool IsFull => _portions.Value >= capacity;

        public bool IsCharged => false;

        public override void OnNetworkSpawn()
        {
            _portions.OnValueChanged += HandlePortionsChanged;
            RefreshVisuals();
        }

        public override void OnNetworkDespawn() => _portions.OnValueChanged -= HandlePortionsChanged;

        private void HandlePortionsChanged(byte previous, byte current) => RefreshVisuals();

        /// <summary>Server only. The serving line hands the tray what today's menu is.</summary>
        public void Serve(GameObject[] menuPrefabs)
        {
            if (!IsServer || IsFull)
            {
                return;
            }

            _menuPrefabs = menuPrefabs ?? System.Array.Empty<GameObject>();
            _portions.Value = (byte)capacity;
            _menuItem.Value = 0;
        }

        public bool CanUse(in InteractionRequest request, out string prompt)
        {
            prompt = null;

            if (!IsSpawned || _portions.Value == 0)
            {
                return false;
            }

            prompt = $"Balancer une portion ({_portions.Value})";
            return true;
        }

        public void Use(in InteractionRequest request, IInteractionContext context)
        {
            if (!IsServer || _portions.Value == 0 || _menuPrefabs.Length == 0)
            {
                return;
            }

            var prefab = _menuPrefabs[_menuItem.Value % _menuPrefabs.Length];

            // The tray throws for you rather than handing the portion over: a player
            // carries one thing at a time here, and juggling tray and potato would be
            // fiddly where the joke needs to be immediate.
            var origin = request.HandPosition + request.AimDirection * 0.35f + Vector3.up * 0.1f;
            if (!context.SpawnItem(prefab, origin, Quaternion.identity,
                    request.AimDirection * throwSpeed + Vector3.up * lob))
            {
                return;
            }

            _portions.Value--;
            _menuItem.Value++;
        }

        private void RefreshVisuals()
        {
            for (var i = 0; i < portionVisuals.Length; i++)
            {
                if (portionVisuals[i] != null)
                {
                    portionVisuals[i].SetActive(i < _portions.Value);
                }
            }
        }
    }
}
