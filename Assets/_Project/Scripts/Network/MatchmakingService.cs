using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using MathGame.Player;

namespace MathGame.Network
{
    /// <summary>
    /// Wraps Unity Lobby for rank-based matchmaking.
    /// Server: creates one lobby per rank containing its IP + port.
    /// Client: searches lobbies by rank, reads IP + port, then connects via NGO.
    /// </summary>
    public class MatchmakingService : MonoBehaviour
    {
        public static MatchmakingService Instance { get; private set; }

        private const string KEY_SERVER_IP   = "ServerIP";
        private const string KEY_SERVER_PORT = "ServerPort";
        private const string KEY_RANK        = "Rank";

        private readonly Dictionary<PlayerRank, Lobby> _serverLobbies = new();

        // ── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── UGS Init ────────────────────────────────────────────────────────

        public async Task InitUGS()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized) return;

            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Debug.Log($"[UGS] Signed in as {AuthenticationService.Instance.PlayerId}");
        }

        // ── Server-side ─────────────────────────────────────────────────────

        /// <summary>
        /// Creates one lobby per rank so clients can find this server.
        /// Call once during ServerBootstrap startup.
        /// </summary>
        public async Task RegisterServerLobbies(string publicIP, ushort port)
        {
            foreach (PlayerRank rank in Enum.GetValues(typeof(PlayerRank)))
            {
                try
                {
                    var options = new CreateLobbyOptions
                    {
                        MaxPlayers = 50,
                        IsPrivate  = false,
                        Data = new Dictionary<string, DataObject>
                        {
                            [KEY_SERVER_IP]   = new DataObject(DataObject.VisibilityOptions.Member, publicIP),
                            [KEY_SERVER_PORT] = new DataObject(DataObject.VisibilityOptions.Member, port.ToString()),
                            [KEY_RANK]        = new DataObject(DataObject.VisibilityOptions.Public,
                                                    ((int)rank).ToString(),
                                                    DataObject.IndexOptions.N1),
                        }
                    };

                    var lobby = await LobbyService.Instance.CreateLobbyAsync(
                        $"MathGame_{rank}", 50, options);

                    _serverLobbies[rank] = lobby;
                    Debug.Log($"[Server] Lobby created for {rank}: {lobby.Id}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Server] Failed to create lobby for {rank}: {e.Message}");
                }
            }
        }

        // ── Client-side ──────────────────────────────────────────────────────

        /// <summary>
        /// Finds a server lobby matching the player's rank and returns its IP:port.
        /// Retries up to 30 seconds before giving up.
        /// </summary>
        public async Task<(string ip, ushort port)> FindServerLobby(PlayerRank rank)
        {
            float elapsed = 0f;
            const float timeout = 30f;
            const float retryDelay = 5f;

            while (elapsed < timeout)
            {
                try
                {
                    var options = new QueryLobbiesOptions
                    {
                        Count   = 5,
                        Filters = new List<QueryFilter>
                        {
                            new QueryFilter(
                                QueryFilter.FieldOptions.N1,
                                ((int)rank).ToString(),
                                QueryFilter.OpOptions.EQ),
                        }
                    };

                    var results = await LobbyService.Instance.QueryLobbiesAsync(options);

                    if (results.Results.Count > 0)
                    {
                        var lobby = results.Results[0];
                        string ip   = lobby.Data[KEY_SERVER_IP].Value;
                        ushort port = ushort.Parse(lobby.Data[KEY_SERVER_PORT].Value);
                        Debug.Log($"[Client] Found server for {rank}: {ip}:{port}");
                        return (ip, port);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Client] Lobby query failed: {e.Message}");
                }

                Debug.Log($"[Client] No lobby for {rank}, retrying in {retryDelay}s…");
                await Task.Delay(TimeSpan.FromSeconds(retryDelay));
                elapsed += retryDelay;
            }

            throw new TimeoutException($"No server lobby found for rank {rank} after {timeout}s.");
        }
    }
}
