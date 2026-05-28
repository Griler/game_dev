using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using MathGame.Player;

namespace MathGame.Network
{
    /// <summary>
    /// Runs only on the dedicated server.
    /// Manages the waiting queue per rank and spawns NetworkGameState rooms
    /// when two compatible players are available.
    /// </summary>
    public class ServerMatchManager : MonoBehaviour
    {
        public static ServerMatchManager Instance { get; private set; }

        [SerializeField] private NetworkObject networkGameStatePrefab;

        // ── Internal types ──────────────────────────────────────────────────
        private struct WaitingClient
        {
            public ulong  clientId;
            public string name;
            public int    elo;
            public int    gamesPlayed;
        }

        // ── State ───────────────────────────────────────────────────────────
        private readonly Dictionary<PlayerRank, Queue<WaitingClient>> _queues = new();
        private readonly Dictionary<ulong, ServerGameLogic>           _activeMatches = new(); // clientId → logic

        // ── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            foreach (PlayerRank rank in System.Enum.GetValues(typeof(PlayerRank)))
                _queues[rank] = new Queue<WaitingClient>();
        }

        // ── Public API ──────────────────────────────────────────────────────

        /// <summary>Called by ConnectionManager when a new client connects and sends its data.</summary>
        public void RegisterClient(ulong clientId, string playerName, int elo, int gamesPlayed)
        {
            var rank   = RankSystem.GetRank(elo);
            var client = new WaitingClient
            {
                clientId    = clientId,
                name        = playerName,
                elo         = elo,
                gamesPlayed = gamesPlayed,
            };

            _queues[rank].Enqueue(client);
            Debug.Log($"[MatchManager] Client {clientId} ({playerName}, ELO {elo}) queued for {rank}");

            TryMatch(rank);
        }

        /// <summary>Called when a client disconnects.</summary>
        public void OnClientDisconnected(ulong clientId)
        {
            // Remove from waiting queue if present
            foreach (var queue in _queues.Values)
            {
                var temp = new Queue<WaitingClient>();
                while (queue.Count > 0)
                {
                    var entry = queue.Dequeue();
                    if (entry.clientId != clientId)
                        temp.Enqueue(entry);
                }
                while (temp.Count > 0)
                    queue.Enqueue(temp.Dequeue());
            }

            // Notify active match if in one
            if (_activeMatches.TryGetValue(clientId, out var logic))
                logic?.OnOpponentDisconnected(clientId);
        }

        /// <summary>Called by ServerGameLogic when a match concludes.</summary>
        public void OnMatchFinished(ulong p1Id, ulong p2Id)
        {
            _activeMatches.Remove(p1Id);
            _activeMatches.Remove(p2Id);
        }

        // ── Private ─────────────────────────────────────────────────────────

        private void TryMatch(PlayerRank rank)
        {
            if (_queues[rank].Count < 2) return;

            var p1 = _queues[rank].Dequeue();
            var p2 = _queues[rank].Dequeue();
            StartMatch(p1, p2);
        }

        private void StartMatch(WaitingClient p1, WaitingClient p2)
        {
            if (networkGameStatePrefab == null)
            {
                Debug.LogError("[MatchManager] networkGameStatePrefab is not assigned!");
                return;
            }

            var go  = Instantiate(networkGameStatePrefab.gameObject);
            var net = go.GetComponent<NetworkObject>();

            // Restrict visibility to just these two players
            net.CheckObjectVisibility = clientId =>
                clientId == p1.clientId || clientId == p2.clientId;

            net.Spawn();

            var logic = go.GetComponent<ServerGameLogic>();
            logic.InitMatch(
                p1.clientId, p1.name, p1.elo, p1.gamesPlayed,
                p2.clientId, p2.name, p2.elo, p2.gamesPlayed);

            _activeMatches[p1.clientId] = logic;
            _activeMatches[p2.clientId] = logic;

            Debug.Log($"[MatchManager] Match started: {p1.name} vs {p2.name}");
        }
    }
}
