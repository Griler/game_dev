using System.Collections.Generic;
using UnityEngine;
using MathGame.Core;
using MathGame.Player;
using MathGame.Question;

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
        private ulong              _localClientId;

        // ── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (Unity.Netcode.NetworkManager.Singleton != null)
                _localClientId = Unity.Netcode.NetworkManager.Singleton.LocalClientId;
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

            state.QuestionIndex.OnValueChanged += OnQuestionIndexChanged;
            state.PhaseInt.OnValueChanged       += OnPhaseChanged;
            state.Player1Score.OnValueChanged   += OnScoreChanged;
            state.Player2Score.OnValueChanged   += OnScoreChanged;
            state.QuestionSeed.OnValueChanged   += OnSeedChanged;

            // If seed is already set (late join), generate questions now
            if (state.QuestionSeed.Value != 0)
                GenerateQuestionsFromSeed(state.QuestionSeed.Value);

            UpdatePlayerCards();
        }

        // ── NetworkVariable callbacks ────────────────────────────────────────

        private void OnSeedChanged(int _, int seed)
        {
            if (seed != 0) GenerateQuestionsFromSeed(seed);
        }

        private void OnQuestionIndexChanged(int _, int index)
        {
            // Handled via NextQuestionClientRpc for immediate UI response
        }

        private void OnPhaseChanged(int _, int phaseInt)
        {
            var phase = (GamePhase)phaseInt;
            var gm    = GameManager.Instance;
            if (gm == null) return;

            if (phase == GamePhase.Playing)
                gm.stateMachine.TransitionTo(GamePhase.Playing);
            else if (phase == GamePhase.QuestionResult)
                gm.stateMachine.TransitionTo(GamePhase.QuestionResult);
            else if (phase == GamePhase.ShowResult)
                gm.stateMachine.TransitionTo(GamePhase.ShowResult);
        }

        private void OnScoreChanged(int _, int __)
        {
            if (_state == null) return;
            bool localIsP1 = _localClientId == _state.Player1ClientId.Value;
            int localScore  = localIsP1 ? _state.Player1Score.Value : _state.Player2Score.Value;
            int remoteScore = localIsP1 ? _state.Player2Score.Value : _state.Player1Score.Value;

            GameManager.Instance?.hud?.playerCards[0]?.SetScore(localScore);
            GameManager.Instance?.hud?.playerCards[1]?.SetScore(remoteScore);
        }

        // ── ClientRpc entry points (called by NetworkGameState) ───────────────

        public void OnCountdown(int count)
        {
            GameManager.Instance?.hud?.ShowCountdown(count);
            if (count == 1)
                // HideCountdown is called when Playing phase begins
                GameManager.Instance?.hud?.HideCountdown();
        }

        public void OnNextQuestion(int questionIndex)
        {
            if (_cachedQuestions == null || questionIndex <= 0) return;
            int idx = questionIndex - 1;
            if (idx >= _cachedQuestions.Count) return;

            var q   = _cachedQuestions[idx];
            var cfg = RankConfigProvider.GetDefault(_state.MatchRank);
            var gm  = GameManager.Instance;

            gm?.hud?.questionPanel?.DisplayQuestion(q, questionIndex, cfg.questionsPerRound);
            gm?.hud?.numberTilePool?.SetupPool(q.poolNumbers);
        }

        public void OnAnswerResult(ulong answererId, bool correct, int[] correctAnswers)
        {
            // Only trigger UI feedback on the client that sent the answer
            if (answererId != _localClientId) return;
            GameManager.Instance?.hud?.questionPanel?.ShowAnswerFeedback(correct, correctAnswers);
        }

        public void OnMatchEnd(ulong winnerId, int p1Score, int p2Score,
            int eloChangeP1, int eloChangeP2)
        {
            if (_state == null) return;

            bool localIsP1  = _localClientId == _state.Player1ClientId.Value;
            bool localWon   = winnerId == _localClientId;
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
            _cachedQuestions = new List<QuestionData>(config.questionsPerRound);
            for (int i = 0; i < config.questionsPerRound; i++)
                _cachedQuestions.Add(gen.Generate(config));
        }

        private void UpdatePlayerCards()
        {
            if (_state == null) return;
            var hud = GameManager.Instance?.hud;
            if (hud == null) return;

            bool localIsP1 = _localClientId == _state.Player1ClientId.Value;

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
            _state.QuestionIndex.OnValueChanged -= OnQuestionIndexChanged;
            _state.PhaseInt.OnValueChanged       -= OnPhaseChanged;
            _state.Player1Score.OnValueChanged   -= OnScoreChanged;
            _state.Player2Score.OnValueChanged   -= OnScoreChanged;
            _state.QuestionSeed.OnValueChanged   -= OnSeedChanged;
        }
    }
}
