using HoldMyBeer.Core;
using HoldMyBeer.Gameplay;
using HoldMyBeer.Gameplay.Interaction;
using HoldMyBeer.Interaction;
using HoldMyBeer.Networking;
using Unity.Netcode;
using UnityEngine;

namespace HoldMyBeer.App
{
    /// <summary>
    /// The composition root. Lives in the Boot scene, builds every long-lived
    /// service exactly once, then hands over to the Menu scene.
    ///
    /// This is the ONLY place that knows about concrete types. Everything else
    /// receives interfaces, which is what keeps the transports, the lobby and the
    /// spawner swappable without touching each other.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrapper : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private NetworkManager networkManager;

        [Header("Network prefabs")]
        [Tooltip("Spawned by the host as soon as the session opens. Holds the replicated lobby.")]
        [SerializeField] private GameObject lobbyPrefab;

        [Tooltip("Spawned by the server for each player once the Game scene is loaded.")]
        [SerializeField] private GameObject playerPrefab;

        [Header("Rules")]
        [SerializeField, Min(1)] private int maxPlayers = 8;

        [Header("Interaction")]
        [Tooltip("How far from the hand an item may be picked up.")]
        [SerializeField, Min(0.1f)] private float maxGrabReach = 2.5f;

        [Tooltip("Base speed added along the aim direction when throwing.")]
        [SerializeField, Min(0f)] private float throwImpulse = 7f;

        [Tooltip("Server-side cap on any thrown velocity. The client reports it, so it is not trusted.")]
        [SerializeField, Min(1f)] private float maxThrowSpeed = 18f;

        private bool _ownsContainer;
        private ConnectionApprovalHandler _approval;
        private NetworkSessionService _session;
        private ISessionInviteService _invites;

        private void Awake()
        {
            if (AppServices.IsReady)
            {
                // A second Boot (someone loaded it twice): the first one wins.
                Destroy(gameObject);
                return;
            }

            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            DontDestroyOnLoad(gameObject);
            _ownsContainer = true;
            BuildContainer();
        }

        private void OnDestroy()
        {
            // A duplicate Boot destroys itself in Awake; it must not tear down the
            // container that belongs to the instance that won.
            if (!_ownsContainer)
            {
                return;
            }

            _session?.Dispose();
            _approval?.Dispose();
            (_invites as System.IDisposable)?.Dispose();
            AppServices.Clear();
        }

        private void Start()
        {
            if (!AppServices.IsReady)
            {
                return;
            }

            AppServices.Container.Resolve<ISceneLoader>().Load(SceneNames.Menu);
        }

        private void BuildContainer()
        {
            var container = new ServiceContainer();
            AppServices.SetContainer(container);

            container.Register<ICoroutineRunner>(gameObject.AddComponent<CoroutineRunner>());
            container.Register<ISceneLoader>(new SceneLoader(container.Resolve<ICoroutineRunner>()));

            var profile = new PlayerPrefsPlayerProfile();
            container.Register<IPlayerProfile>(profile);

            var transportProvider = new SessionTransportProvider();
            foreach (var sessionTransport in InstallerScanner.DiscoverTransports(networkManager))
            {
                transportProvider.Register(sessionTransport);
            }

            container.Register<ISessionTransportProvider>(transportProvider);

            // Whatever platform is present wins; with none, the null object keeps the
            // menu free of "is there an invite service?" branches.
            _invites = new NullSessionInviteService();
            foreach (var inviteService in InstallerScanner.DiscoverInviteServices(networkManager))
            {
                _invites = inviteService;
                break;
            }

            container.Register(_invites);

            // The platform account beats anything typed in a box: it is the name the
            // player's friends already know them by.
            if (_invites is IPlatformIdentity { HasIdentity: true } identity)
            {
                profile.SetDisplayName(identity.DisplayName);
            }

            _session = new NetworkSessionService(networkManager, transportProvider, profile);
            container.Register<INetworkSessionService>(_session);

            var lobbyProvider = new LobbyProvider();
            container.Register<ILobbyProvider>(lobbyProvider);

            _approval = new ConnectionApprovalHandler(networkManager);
            _approval.AddPolicy(new MaxPlayersPolicy(networkManager, maxPlayers));
            _approval.AddPolicy(new BuildVersionPolicy(Application.version));
            _approval.AddPolicy(new LobbyOpenPolicyAdapter(lobbyProvider));

            container.Register<ILobbyLauncher>(new LobbyLauncher(networkManager, lobbyPrefab, _approval));
            container.Register<IPlayerSpawner>(new PlayerSpawner(networkManager, playerPrefab));

            // Interaction rules compose exactly like the approval policies above: the
            // order IS the priority, and the first rule that accepts wins. Insert new
            // game rules between these two — ThrowRule accepts anything held, so
            // nothing registered after it would ever run.
            var interactions = new InteractionRegistry();
            interactions.AddRule(new GrabRule(maxGrabReach));
            interactions.AddRule(new ThrowRule(throwImpulse));

            container.Register<IInteractionRegistry>(interactions);
            container.Register<IHeldItemTracker>(new HeldItemTracker());
            container.Register<IInteractionContext>(
                new ServerInteractionContext(networkManager, maxThrowSpeed));
        }

        private bool ValidateReferences()
        {
            if (networkManager == null)
            {
                networkManager = FindFirstObjectByType<NetworkManager>();
            }

            if (networkManager == null)
            {
                Debug.LogError("GameBootstrapper: no NetworkManager in the Boot scene.");
                return false;
            }

            if (lobbyPrefab == null || playerPrefab == null)
            {
                Debug.LogError("GameBootstrapper: the lobby and player prefabs must both be assigned.");
                return false;
            }

            return true;
        }
    }
}
