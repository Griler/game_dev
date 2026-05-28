using System;
using UnityEngine;
using MathGame.Network;
using MathGame.Player;
using MathGame.Question;
using MathGame.UI;

namespace MathGame.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        // ── Inspector ────────────────────────────────────────────────────────
        [Header("References")]
        public GameStateMachine stateMachine;
        public GameHUD          hud;

        [Header("Local Player Setup (offline / testing)")]
        public PlayerRank localRank = PlayerRank.Bronze;

        [Header("Optional: override configs per rank (leave empty to use defaults)")]
        public RankConfig[] rankConfigAssets;

        // ── Runtime state ────────────────────────────────────────────────────
        public PlayerData LocalPlayer  { get; private set; }
        public PlayerData RemotePlayer { get; private set; }

        private QuestionGenerator  _generator;
        private QuestionData       _currentQuestion;
        private RankConfigData     _currentConfig;

        private int   _localScore;
        private int   _remoteScore;
        private int   _questionIndex;
        private float _roundTimeRemaining;
        private int   _seed;

        private bool  _waitingForNextQuestion;

        // When true, the match is driven by the network layer (ServerGameLogic +
        // ClientGameProxy). The offline question loop, timer and ELO must stay idle.
        private bool  _networked;
        public  bool  IsNetworked => _networked;
        public  void  SetNetworkedMode(bool on) => _networked = on;

        // ── Events ───────────────────────────────────────────────────────────
        public event Action<int, int, int>  OnScoreUpdated;      // (localScore, remoteScore, questionIndex)
        public event Action<float>          OnTimerTick;         // seconds remaining
        public event Action<bool, int, int> OnMatchEnded;        // (localWon, localScore, remoteScore)
        public event Action<int>            OnCountdownTick;     // 3 2 1
        public event Action<QuestionData>   OnQuestionReady;

        public RankConfigData CurrentConfig => _currentConfig;

        // ── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            LocalPlayer  = PlayerData.Load();
            RemotePlayer = new PlayerData { displayName = "Opponent", eloRating = 1000 };
        }

        private void Start()
        {
            stateMachine.OnPhaseChanged += HandlePhaseChange;
        }

        private void OnDestroy()
        {
            if (stateMachine != null)
                stateMachine.OnPhaseChanged -= HandlePhaseChange;
        }

        private void Update()
        {
            if (_networked) return; // server/ClientGameProxy own the timer in network mode
            if (stateMachine.CurrentPhase == GamePhase.Playing)
                TickPlayingTimer();
        }

        // ── Public API ───────────────────────────────────────────────────────

        public void StartGame()
        {
            if (_networked) return; // network matches are started by the server, not here

            _seed            = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            _generator       = new QuestionGenerator(_seed);
            _currentConfig   = GetConfig(RankSystem.GetMatchRank(LocalPlayer.eloRating, RemotePlayer.eloRating));
            _localScore      = 0;
            _remoteScore     = 0;
            _questionIndex   = 0;
            _roundTimeRemaining = _currentConfig.roundDurationSeconds;

            stateMachine.TransitionTo(GamePhase.Countdown);
            StartCoroutine(RunCountdown());
        }

        /// <summary>Called by QuestionPanel when the player submits a complete answer.</summary>
        public void SubmitAnswer(int[] filledValues)
        {
            // In multiplayer: route answer to server; server handles scoring + feedback
            if (NetworkGameState.Instance != null &&
                Unity.Netcode.NetworkManager.Singleton != null &&
                Unity.Netcode.NetworkManager.Singleton.IsClient &&
                !Unity.Netcode.NetworkManager.Singleton.IsServer)
            {
                NetworkGameState.Instance.SubmitAnswerServerRpc(filledValues);
                return;
            }

            if (stateMachine.CurrentPhase != GamePhase.Playing) return;
            if (_waitingForNextQuestion) return;

            bool correct = CheckAnswer(filledValues, _currentQuestion.correctAnswers);

            if (correct)
                _localScore++;

            OnScoreUpdated?.Invoke(_localScore, _remoteScore, _questionIndex);

            stateMachine.TransitionTo(GamePhase.QuestionResult);
            hud?.questionPanel?.ShowAnswerFeedback(correct, _currentQuestion.correctAnswers);

            _waitingForNextQuestion = true;
            Invoke(nameof(AdvanceQuestion), 1.2f);
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void HandlePhaseChange(GamePhase previous, GamePhase next)
        {
            // In network mode the server drives question loading and match end;
            // running the offline path here would dereference the null generator.
            if (_networked) return;

            if (next == GamePhase.Playing)
                LoadNextQuestion();
            else if (next == GamePhase.RoundEnd)
                EndMatch();
        }

        private void TickPlayingTimer()
        {
            _roundTimeRemaining -= Time.deltaTime;
            OnTimerTick?.Invoke(_roundTimeRemaining);

            if (_roundTimeRemaining <= 0f)
            {
                _roundTimeRemaining = 0f;
                stateMachine.TransitionTo(GamePhase.RoundEnd);
            }
        }

        private void LoadNextQuestion()
        {
            _waitingForNextQuestion = false;
            _currentQuestion = _generator.Generate(_currentConfig);
            _questionIndex++;
            OnQuestionReady?.Invoke(_currentQuestion);

            if (_questionIndex > _currentConfig.questionsPerRound)
            {
                stateMachine.TransitionTo(GamePhase.RoundEnd);
                return;
            }

            hud?.questionPanel?.DisplayQuestion(_currentQuestion, _questionIndex, _currentConfig.questionsPerRound);
            hud?.numberTilePool?.SetupPool(_currentQuestion.poolNumbers);
        }

        private void AdvanceQuestion()
        {
            if (_questionIndex >= _currentConfig.questionsPerRound)
            {
                stateMachine.TransitionTo(GamePhase.RoundEnd);
                return;
            }

            stateMachine.TransitionTo(GamePhase.Playing);
        }

        private void EndMatch()
        {
            bool localWon = _localScore > _remoteScore;
            float scoreA  = _localScore + _remoteScore == 0
                ? 0.5f
                : (float)_localScore / (_localScore + _remoteScore);

            var (newLocal, newRemote) = EloCalculator.Calculate(
                LocalPlayer.eloRating, RemotePlayer.eloRating,
                scoreA, LocalPlayer.gamesPlayed, RemotePlayer.gamesPlayed);

            LocalPlayer.eloRating   = newLocal;
            LocalPlayer.gamesPlayed++;
            if (localWon) LocalPlayer.gamesWon++;
            LocalPlayer.currentRank = RankSystem.GetRank(newLocal);
            LocalPlayer.Save();

            stateMachine.TransitionTo(GamePhase.ShowResult);
            OnMatchEnded?.Invoke(localWon, _localScore, _remoteScore);
        }

        private static bool CheckAnswer(int[] filled, int[] correct)
        {
            if (filled.Length != correct.Length) return false;
            for (int i = 0; i < correct.Length; i++)
                if (filled[i] != correct[i]) return false;
            return true;
        }

        private RankConfigData GetConfig(PlayerRank rank)
        {
            if (rankConfigAssets != null)
            {
                foreach (var asset in rankConfigAssets)
                {
                    if (asset != null && asset.rank == rank)
                    {
                        return new RankConfigData
                        {
                            rank              = asset.rank,
                            blankCount        = asset.blankCount,
                            allowedOperators  = asset.allowedOperators,
                            minValue          = asset.minValue,
                            maxValue          = asset.maxValue,
                            poolSize          = asset.poolSize,
                            questionsPerRound = asset.questionsPerRound,
                            roundDurationSeconds = asset.roundDurationSeconds,
                        };
                    }
                }
            }
            return RankConfigProvider.GetDefault(rank);
        }

        private System.Collections.IEnumerator RunCountdown()
        {
            for (int i = 3; i >= 1; i--)
            {
                OnCountdownTick?.Invoke(i);
                hud?.ShowCountdown(i);
                yield return new WaitForSeconds(1f);
            }
            hud?.HideCountdown();
            stateMachine.TransitionTo(GamePhase.Playing);
        }
    }
}
