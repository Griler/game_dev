using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using MathGame.Core;
using MathGame.Player;
using MathGame.Question;
using MathGame.UI;

namespace MathGame.Network
{
    /// <summary>
    /// Singleton on the client side.
    /// Bridges NetworkGameState events → existing GameHUD / QuestionPanel UI.
    /// Also intercepts GameManager.SubmitAnswer so answers travel via ServerRpc.
    /// </summary>
    public class ClientGameProxy : MonoBehaviour
    {
        public static ClientGameProxy Instance { get; private set; }

        private NetworkGameState   _state;
        private List<QuestionData> _cachedQuestions;
        private int                _shownQIndex = -1; // last question index displayed to me

        // Read fresh every time: LocalClientId is only valid AFTER the client has
        // connected, so caching it in Start() (pre-connection) would yield 0.
        private ulong LocalClientId =>
            Unity.Netcode.NetworkManager.Singleton != null
                ? Unity.Netcode.NetworkManager.Singleton.LocalClientId
                : 0ul;

        // ── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnsubscribeState();
        }

        // ── Called by NetworkGameState.OnNetworkSpawn ────────────────────────

        public void OnNetworkGameStateSpawned(NetworkGameState state)
        {
            UnsubscribeState();
            _state = state;

            // Hand the local GameManager over to network control so its offline
            // question loop / timer / ELO path stops running.
            GameManager.Instance?.SetNetworkedMode(true);

            // The match is live — dismiss the "Finding match…" overlay.
            Object.FindFirstObjectByType<MatchmakingPanel>()?.Hide();

            state.Player1QIndex.OnValueChanged   += OnQIndexChanged;
            state.Player2QIndex.OnValueChanged   += OnQIndexChanged;
            state.PhaseInt.OnValueChanged        += OnPhaseChanged;
            state.Player1Score.OnValueChanged    += OnScoreChanged;
            state.Player2Score.OnValueChanged    += OnScoreChanged;
            state.QuestionSeed.OnValueChanged    += OnSeedChanged;
            state.TimeRemaining.OnValueChanged   += OnTimeChanged;
            // Player info is written by InitMatch *after* Spawn(), so it may arrive
            // a tick later than this callback — refresh the cards when it does.
            state.Player1ClientId.OnValueChanged += OnPlayerInfoChanged;
            state.Player2ClientId.OnValueChanged += OnPlayerInfoChanged;
            state.Player1Name.OnValueChanged     += OnPlayerInfoChanged;
            state.Player2Name.OnValueChanged     += OnPlayerInfoChanged;

            // If seed is already set (late join), generate questions now
            if (state.QuestionSeed.Value != 0)
                GenerateQuestionsFromSeed(state.QuestionSeed.Value);

            UpdatePlayerCards();
        }

        // ── NetworkVariable callbacks ────────────────────────────────────────

        private void OnSeedChanged(int _, int seed)
        {
            if (seed != 0)
            {
                GenerateQuestionsFromSeed(seed);
                TryShowMyQuestion(); // seed may arrive after QIndex was already set
            }
        }

        private void OnQIndexChanged(int _, int __) => TryShowMyQuestion();

        private void OnPhaseChanged(int _, int phaseInt)
        {
            var phase = (GamePhase)phaseInt;
            var gm    = GameManager.Instance;
            if (gm == null) return;

            if (phase == GamePhase.Playing)
            {
                gm.hud?.HideCountdown();
                gm.stateMachine.TransitionTo(GamePhase.Playing);
                // After TransitionTo: GameHUD's own handler also sets a duration from
                // the (null on clients) offline config, so set the real one last.
                gm.hud?.timerBar?.SetDuration(
                    RankConfigProvider.GetDefault(_state.MatchRank).roundDurationSeconds);
                TryShowMyQuestion();
            }
            else if (phase == GamePhase.ShowResult)
                gm.stateMachine.TransitionTo(GamePhase.ShowResult);
        }

        private void OnTimeChanged(float _, float remaining)
        {
            GameManager.Instance?.hud?.timerBar?.UpdateTime(remaining);
        }

        private void OnPlayerInfoChanged(ulong _, ulong __) => UpdatePlayerCards();
        private void OnPlayerInfoChanged(FixedString64Bytes _, FixedString64Bytes __) => UpdatePlayerCards();

        private void OnScoreChanged(int _, int __)
        {
            if (_state == null) return;
            bool localIsP1 = LocalClientId == _state.Player1ClientId.Value;
            int localScore  = localIsP1 ? _state.Player1Score.Value : _state.Player2Score.Value;
            int remoteScore = localIsP1 ? _state.Player2Score.Value : _state.Player1Score.Value;

            GameManager.Instance?.hud?.playerCards[0]?.SetScore(localScore);
            GameManager.Instance?.hud?.playerCards[1]?.SetScore(remoteScore);
        }

        // ── ClientRpc entry points (called by NetworkGameState) ───────────────

        public void OnCountdown(int count)
        {
            // Hidden later when the Playing phase begins (see OnPhaseChanged).
            GameManager.Instance?.hud?.ShowCountdown(count);
        }

        /// <summary>Show the question at my own (per-player) race index, if it changed.</summary>
        private void TryShowMyQuestion()
        {
            if (_state == null || _cachedQuestions == null) return;

            bool localIsP1 = LocalClientId == _state.Player1ClientId.Value;
            int  myIndex   = localIsP1 ? _state.Player1QIndex.Value : _state.Player2QIndex.Value; // 1-based
            if (myIndex <= 0 || myIndex == _shownQIndex) return;

            int idx = myIndex - 1;
            if (idx >= _cachedQuestions.Count) return;

            _shownQIndex = myIndex;
            var q  = _cachedQuestions[idx];
            var gm = GameManager.Instance;

            // total = 0 → panel shows just "Câu X" (a race has no fixed total).
            gm?.hud?.questionPanel?.DisplayQuestion(q, myIndex, 0);
            gm?.hud?.numberTilePool?.SetupPool(q.poolNumbers);
        }

        public void OnAnswerResult(ulong answererId, bool correct, int[] correctAnswers)
        {
            // Only trigger UI feedback on the client that sent the answer
            if (answererId != LocalClientId) return;
            GameManager.Instance?.hud?.questionPanel?.ShowAnswerFeedback(correct, correctAnswers);
        }

        public void OnMatchEnd(ulong winnerId, int p1Score, int p2Score,
            int eloChangeP1, int eloChangeP2)
        {
            if (_state == null) return;

            bool localIsP1  = LocalClientId == _state.Player1ClientId.Value;
            bool localWon   = winnerId == LocalClientId;
            int  localScore = localIsP1 ? p1Score : p2Score;
            int  remoteScore = localIsP1 ? p2Score : p1Score;
            int  myEloChange = localIsP1 ? eloChangeP1 : eloChangeP2;

            // Persist ELO locally
            var localPlayer = GameManager.Instance?.LocalPlayer;
            if (localPlayer != null)
            {
                localPlayer.eloRating   = Mathf.Max(0, localPlayer.eloRating + myEloChange);
                localPlayer.gamesPlayed++;
                if (localWon) localPlayer.gamesWon++;
                localPlayer.currentRank = RankSystem.GetRank(localPlayer.eloRating);
                localPlayer.Save();
            }

            GameManager.Instance?.OnMatchEnded?.Invoke(localWon, localScore, remoteScore);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void GenerateQuestionsFromSeed(int seed)
        {
            if (_state == null) return;
            var config = RankConfigProvider.GetDefault(_state.MatchRank);
            var gen    = new QuestionGenerator(seed);
            // Must match the server's pool exactly (same seed, same count).
            _cachedQuestions = new List<QuestionData>(NetworkGameState.MaxRaceQuestions);
            for (int i = 0; i < NetworkGameState.MaxRaceQuestions; i++)
                _cachedQuestions.Add(gen.Generate(config));
        }

        private void UpdatePlayerCards()
        {
            if (_state == null) return;
            var hud = GameManager.Instance?.hud;
            if (hud == null) return;

            bool localIsP1 = LocalClientId == _state.Player1ClientId.Value;

            string localName  = localIsP1 ? _state.Player1Name.Value.ToString() : _state.Player2Name.Value.ToString();
            string remoteName = localIsP1 ? _state.Player2Name.Value.ToString() : _state.Player1Name.Value.ToString();
            int    localElo   = localIsP1 ? _state.Player1Elo.Value : _state.Player2Elo.Value;
            int    remoteElo  = localIsP1 ? _state.Player2Elo.Value : _state.Player1Elo.Value;

            hud.playerCards[0]?.Setup(localName,  RankSystem.GetRank(localElo),  localElo);
            hud.playerCards[1]?.Setup(remoteName, RankSystem.GetRank(remoteElo), remoteElo);
        }

        private void UnsubscribeState()
        {
            if (_state == null) return;
            _state.Player1QIndex.OnValueChanged   -= OnQIndexChanged;
            _state.Player2QIndex.OnValueChanged   -= OnQIndexChanged;
            _state.PhaseInt.OnValueChanged        -= OnPhaseChanged;
            _state.Player1Score.OnValueChanged    -= OnScoreChanged;
            _state.Player2Score.OnValueChanged    -= OnScoreChanged;
            _state.QuestionSeed.OnValueChanged    -= OnSeedChanged;
            _state.TimeRemaining.OnValueChanged   -= OnTimeChanged;
            _state.Player1ClientId.OnValueChanged -= OnPlayerInfoChanged;
            _state.Player2ClientId.OnValueChanged -= OnPlayerInfoChanged;
            _state.Player1Name.OnValueChanged     -= OnPlayerInfoChanged;
            _state.Player2Name.OnValueChanged     -= OnPlayerInfoChanged;
        }
    }
}
