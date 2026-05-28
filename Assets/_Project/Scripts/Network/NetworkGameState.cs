using Unity.Collections;
using Unity.Netcode;
using MathGame.Core;
using MathGame.Player;

namespace MathGame.Network
{
    /// <summary>
    /// Authoritative game state for one match. Spawned by the server; only
    /// visible to the two players in that match via CheckObjectVisibility.
    /// On clients there is exactly one instance → exposed as Instance.
    /// On the server there are many — use direct references instead.
    /// </summary>
    public class NetworkGameState : NetworkBehaviour
    {
        // ── Singleton (client-side only) ────────────────────────────────────
        public static NetworkGameState Instance { get; private set; }

        // ── NetworkVariables (server writes, everyone reads) ────────────────
        static readonly NetworkVariableReadPermission  R = NetworkVariableReadPermission.Everyone;
        static readonly NetworkVariableWritePermission W = NetworkVariableWritePermission.Server;

        public NetworkVariable<int>   Player1Score    = new(0,   R, W);
        public NetworkVariable<int>   Player2Score    = new(0,   R, W);
        public NetworkVariable<int>   QuestionSeed    = new(0,   R, W);
        public NetworkVariable<int>   QuestionIndex   = new(0,   R, W);
        public NetworkVariable<int>   PhaseInt        = new(0,   R, W);
        public NetworkVariable<float> TimeRemaining   = new(60f, R, W);
        public NetworkVariable<ulong> Player1ClientId = new(0ul, R, W);
        public NetworkVariable<ulong> Player2ClientId = new(0ul, R, W);
        public NetworkVariable<FixedString64Bytes> Player1Name =
            new(new FixedString64Bytes(""), R, W);
        public NetworkVariable<FixedString64Bytes> Player2Name =
            new(new FixedString64Bytes(""), R, W);
        public NetworkVariable<int> Player1Elo     = new(800, R, W);
        public NetworkVariable<int> Player2Elo     = new(800, R, W);
        public NetworkVariable<int> MatchRankInt   = new(0,   R, W);

        // ── Convenience properties ──────────────────────────────────────────
        public GamePhase   CurrentPhase => (GamePhase)PhaseInt.Value;
        public PlayerRank  MatchRank    => (PlayerRank)MatchRankInt.Value;

        // ── Lifecycle ───────────────────────────────────────────────────────
        public override void OnNetworkSpawn()
        {
            // Only client sets the singleton; server manages multiple instances directly
            if (!IsServer)
            {
                Instance = this;
                ClientGameProxy.Instance?.OnNetworkGameStateSpawned(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer && Instance == this)
                Instance = null;
        }

        // ── RPCs: Client → Server ───────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void SubmitAnswerServerRpc(int[] filledValues,
            ServerRpcParams rpcParams = default)
        {
            GetComponent<ServerGameLogic>()
                ?.HandleAnswer(rpcParams.Receive.SenderClientId, filledValues);
        }

        // ── RPCs: Server → Clients ──────────────────────────────────────────

        [ClientRpc]
        public void CountdownClientRpc(int count)
        {
            if (!IsServer) ClientGameProxy.Instance?.OnCountdown(count);
        }

        [ClientRpc]
        public void NextQuestionClientRpc(int questionIndex)
        {
            if (!IsServer) ClientGameProxy.Instance?.OnNextQuestion(questionIndex);
        }

        [ClientRpc]
        public void ShowAnswerResultClientRpc(ulong answererId, bool correct,
            int[] correctAnswers)
        {
            if (!IsServer)
                ClientGameProxy.Instance?.OnAnswerResult(answererId, correct, correctAnswers);
        }

        [ClientRpc]
        public void MatchEndClientRpc(ulong winnerId, int p1Score, int p2Score,
            int eloChangeP1, int eloChangeP2)
        {
            if (!IsServer)
                ClientGameProxy.Instance?.OnMatchEnd(
                    winnerId, p1Score, p2Score, eloChangeP1, eloChangeP2);
        }
    }
}
