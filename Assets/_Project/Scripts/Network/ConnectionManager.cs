using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using MathGame.Player;

namespace MathGame.Network
{
    /// <summary>
    /// Starts NGO as server or client and wires connection-approval callbacks.
    /// On the server, reads -serverIP and -serverPort from command-line args.
    /// </summary>
    public class ConnectionManager : MonoBehaviour
    {
        public static ConnectionManager Instance { get; private set; }

        [Header("Defaults (overridden by command-line on server)")]
        [SerializeField] private string serverIP   = "127.0.0.1";
        [SerializeField] private ushort serverPort = 7777;

        public string ServerIP   => serverIP;
        public ushort ServerPort => serverPort;

        // ── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            ParseCommandLineArgs();
        }

        // ── Public API ──────────────────────────────────────────────────────

        /// <summary>Start as dedicated server — listens on all interfaces.</summary>
        public void StartAsServer()
        {
            var transport = GetTransport();
            transport.SetConnectionData("0.0.0.0", serverPort);

            NetworkManager.Singleton.ConnectionApprovalCallback = OnConnectionApproval;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            NetworkManager.Singleton.StartServer();
            Debug.Log($"[Server] Started on port {serverPort}");
        }

        /// <summary>Start as client — connects to the given IP:port.</summary>
        public void StartAsClient(string ip, ushort port, string playerName, int elo, int gamesPlayed)
        {
            var transport = GetTransport();
            transport.SetConnectionData(ip, port);

            var payload = new PlayerConnectionData
            {
                playerName  = playerName,
                eloRating   = elo,
            };
            NetworkManager.Singleton.NetworkConfig.ConnectionData = payload.ToBytes();

            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.StartClient();

            Debug.Log($"[Client] Connecting to {ip}:{port}");
        }

        // ── Callbacks ───────────────────────────────────────────────────────

        private void OnConnectionApproval(
            NetworkManager.ConnectionApprovalRequest  request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved  = true;
            response.CreatePlayerObject = false;

            if (request.Payload == null || request.Payload.Length == 0)
            {
                Debug.LogWarning($"[Server] Client {request.ClientNetworkId} sent empty payload.");
                ServerMatchManager.Instance?.RegisterClient(
                    request.ClientNetworkId, "Unknown", 800, 0);
                return;
            }

            var data = PlayerConnectionData.FromBytes(request.Payload);
            int games = PlayerPrefs.GetInt($"games_{data.playerName}", 0); // fallback
            ServerMatchManager.Instance?.RegisterClient(
                request.ClientNetworkId, data.playerName, data.eloRating, games);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (NetworkManager.Singleton.IsServer)
                ServerMatchManager.Instance?.OnClientDisconnected(clientId);
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private UnityTransport GetTransport() =>
            NetworkManager.Singleton.GetComponent<UnityTransport>();

        private void ParseCommandLineArgs()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-serverIP")
                    serverIP = args[i + 1];
                else if (args[i] == "-serverPort" && ushort.TryParse(args[i + 1], out ushort p))
                    serverPort = p;
            }
        }
    }
}
