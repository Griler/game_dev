using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MathGame.Core;
using MathGame.Player;

namespace MathGame.UI
{
    /// <summary>
    /// Root HUD controller. Wires up all sub-panels and listens to GameManager events.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("Sub-panels")]
        public QuestionPanel  questionPanel;
        public NumberTilePool numberTilePool;
        public TimerBar       timerBar;

        [Header("Player cards (0 = local, 1 = remote)")]
        public PlayerInfoCard[] playerCards;

        [Header("Countdown")]
        [SerializeField] private GameObject      _countdownOverlay;
        [SerializeField] private TextMeshProUGUI _countdownText;

        [Header("Result Screen")]
        [SerializeField] private GameObject      _resultPanel;
        [SerializeField] private TextMeshProUGUI _resultTitleText;
        [SerializeField] private TextMeshProUGUI _resultScoreText;
        [SerializeField] private Button          _rematchButton;

        // Stored delegates so we can unsubscribe properly
        private Action<int, int, int>       _onScoreUpdated;
        private Action<float>               _onTimerTick;
        private Action<bool, int, int>      _onMatchEnded;
        private Action<GamePhase, GamePhase> _onPhaseChanged;

        private GameManager _gm;

        private void Awake()
        {
            numberTilePool.QuestionPanel = questionPanel;
            _countdownOverlay.SetActive(false);
            _resultPanel.SetActive(false);
        }

        private void Start()
        {
            _gm = GameManager.Instance;
            if (_gm == null) return;

            _onScoreUpdated = (local, remote, _) =>
            {
                playerCards[0]?.SetScore(local);
                playerCards[1]?.SetScore(remote);
            };

            _onTimerTick = seconds => timerBar.UpdateTime(seconds);

            _onMatchEnded = ShowResult;

            _onPhaseChanged = (_, next) =>
            {
                if (next == GamePhase.Playing)
                    timerBar.SetDuration(_gm.CurrentConfig?.roundDurationSeconds ?? 60f);
            };

            _gm.OnScoreUpdated              += _onScoreUpdated;
            _gm.OnTimerTick                 += _onTimerTick;
            _gm.OnMatchEnded                += _onMatchEnded;
            _gm.stateMachine.OnPhaseChanged += _onPhaseChanged;

            playerCards[0]?.Setup(_gm.LocalPlayer.displayName,  _gm.LocalPlayer.currentRank,  _gm.LocalPlayer.eloRating);
            playerCards[1]?.Setup(_gm.RemotePlayer.displayName, _gm.RemotePlayer.currentRank, _gm.RemotePlayer.eloRating);

            _rematchButton?.onClick.AddListener(() =>
            {
                _resultPanel.SetActive(false);
                _gm.StartGame();
            });
        }

        private void OnDestroy()
        {
            if (_gm == null) return;
            _gm.OnScoreUpdated              -= _onScoreUpdated;
            _gm.OnTimerTick                 -= _onTimerTick;
            _gm.OnMatchEnded                -= _onMatchEnded;
            _gm.stateMachine.OnPhaseChanged -= _onPhaseChanged;
        }

        public void ShowCountdown(int number)
        {
            _countdownOverlay.SetActive(true);
            _countdownText.text = number.ToString();
        }

        public void HideCountdown()
        {
            _countdownOverlay.SetActive(false);
        }

        private void ShowResult(bool localWon, int localScore, int remoteScore)
        {
            _resultPanel.SetActive(true);
            _resultTitleText.text = localWon ? "BẠN THẮNG!"
                                  : localScore == remoteScore ? "HÒA!"
                                  : "BẠN THUA!";
            _resultScoreText.text = $"{localScore}  −  {remoteScore}";
        }
    }
}
