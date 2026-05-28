using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using MathGame.Core;
using MathGame.Player;
using MathGame.Question;

namespace MathGame.Network
{
    /// <summary>
    /// Server-only game logic. Lives on the same GameObject as NetworkGameState.
    /// Manages question flow, answer validation, timers, and ELO for one match.
    /// </summary>
    [RequireComponent(typeof(NetworkGameState))]
    public class ServerGameLogic : MonoBehaviour
    {
        // ── State ───────────────────────────────────────────────────────────
        private NetworkGameState         _state;
        private List<QuestionData>       _questions;
        private RankConfigData           _config;
        private Dictionary<ulong, bool>  _answeredThisQuestion = new();
        private bool                     _matchEnded;

        // ── Player session data ─────────────────────────────────────────────
        private ulong  _p1Id, _p2Id;
        private int    _p1Elo, _p2Elo;
        private int    _p1GamesPlayed, _p2GamesPlayed;

        // ── Init ────────────────────────────────────────────────────────────

        public void InitMatch(
            ulong p1Id, string p1Name, int p1Elo, int p1Games,
            ulong p2Id, string p2Name, int p2Elo, int p2Games)
        {
            _state = GetComponent<NetworkGameState>();

            _p1Id = p1Id;  _p1Elo = p1Elo;  _p1GamesPlayed = p1Games;
            _p2Id = p2Id;  _p2Elo = p2Elo;  _p2GamesPlayed = p2Games;

            // Write player info to NetworkVariables
            _state.Player1ClientId.Value = p1Id;
            _state.Player2ClientId.Value = p2Id;
            _state.Player1Name.Value     = ToFixed(p1Name);
            _state.Player2Name.Value     = ToFixed(p2Name);
            _state.Player1Elo.Value      = p1Elo;
            _state.Player2Elo.Value      = p2Elo;

            // Determine match rank from average ELO
            var matchRank = RankSystem.GetMatchRank(p1Elo, p2Elo);
            _state.MatchRankInt.Value = (int)matchRank;
            _config = RankConfigProvider.GetDefault(matchRank);

            // Generate all questions server-side (seed is broadcast to clients)
            int seed = Random.Range(int.MinValue, int.MaxValue);
            _state.QuestionSeed.Value = seed;
            var gen = new QuestionGenerator(seed);
            _questions = new List<QuestionData>(_config.questionsPerRound);
            for (int i = 0; i < _config.questionsPerRound; i++)
                _questions.Add(gen.Generate(_config));

            _state.TimeRemaining.Value = _config.roundDurationSeconds;
            _matchEnded = false;

            StartCoroutine(RunCountdown());
        }

        // ── Update ──────────────────────────────────────────────────────────

        private void Update()
        {
            if (_state == null || _matchEnded) return;
            if (_state.CurrentPhase != GamePhase.Playing) return;

            _state.TimeRemaining.Value -= Time.deltaTime;
            if (_state.TimeRemaining.Value <= 0f)
            {
                _state.TimeRemaining.Value = 0f;
                EndMatch();
            }
        }

        // ── Countdown ───────────────────────────────────────────────────────

        private IEnumerator RunCountdown()
        {
            _state.PhaseInt.Value = (int)GamePhase.Countdown;
            for (int i = 3; i >= 1; i--)
            {
                _state.CountdownClientRpc(i);
                yield return new WaitForSeconds(1f);
            }
            _state.PhaseInt.Value = (int)GamePhase.Playing;
            LoadQuestion(0);
        }

        // ── Question management ─────────────────────────────────────────────

        private void LoadQuestion(int index)
        {
            if (index >= _questions.Count)
            {
                EndMatch();
                return;
            }

            _answeredThisQuestion.Clear();
            _answeredThisQuestion[_p1Id] = false;
            _answeredThisQuestion[_p2Id] = false;

            _state.QuestionIndex.Value = index + 1; // 1-based for display
            _state.NextQuestionClientRpc(index + 1);
        }

        // ── Answer handling ─────────────────────────────────────────────────

        public void HandleAnswer(ulong clientId, int[] filledValues)
        {
            if (_matchEnded) return;
            if (_state.CurrentPhase != GamePhase.Playing) return;

            // Reject duplicate answer for the same question
            if (!_answeredThisQuestion.ContainsKey(clientId)) return;
            if (_answeredThisQuestion[clientId]) return;
            _answeredThisQuestion[clientId] = true;

            int qIndex = _state.QuestionIndex.Value - 1;
            if (qIndex < 0 || qIndex >= _questions.Count) return;

            var question = _questions[qIndex];
            bool correct = CheckAnswer(filledValues, question.correctAnswers);

            if (correct)
            {
                if (clientId == _p1Id) _state.Player1Score.Value++;
                else                   _state.Player2Score.Value++;
            }

            _state.ShowAnswerResultClientRpc(clientId, correct, question.correctAnswers);

            // Advance when both players have answered
            bool bothAnswered = _answeredThisQuestion[_p1Id] && _answeredThisQuestion[_p2Id];
            if (bothAnswered)
                StartCoroutine(AdvanceQuestion());
        }

        private IEnumerator AdvanceQuestion()
        {
            _state.PhaseInt.Value = (int)GamePhase.QuestionResult;
            yield return new WaitForSeconds(1.2f);

            int nextIndex = _state.QuestionIndex.Value; // already 1-based, next = current value
            if (nextIndex >= _questions.Count)
                EndMatch();
            else
            {
                _state.PhaseInt.Value = (int)GamePhase.Playing;
                LoadQuestion(nextIndex);
            }
        }

        // ── Match end ───────────────────────────────────────────────────────

        private void EndMatch()
        {
            if (_matchEnded) return;
            _matchEnded = true;
            _state.PhaseInt.Value = (int)GamePhase.ShowResult;

            int p1Score = _state.Player1Score.Value;
            int p2Score = _state.Player2Score.Value;

            var (newElo1, newElo2) = EloCalculator.CalculateFromScores(
                _p1Elo, _p2Elo, p1Score, p2Score, _p1GamesPlayed, _p2GamesPlayed);

            int eloChange1 = newElo1 - _p1Elo;
            int eloChange2 = newElo2 - _p2Elo;

            ulong winnerId = p1Score > p2Score ? _p1Id
                           : p2Score > p1Score ? _p2Id
                           : ulong.MaxValue; // draw

            _state.MatchEndClientRpc(winnerId, p1Score, p2Score, eloChange1, eloChange2);

            // Notify ServerMatchManager so it can clean up this room
            ServerMatchManager.Instance?.OnMatchFinished(_p1Id, _p2Id);
        }

        /// <summary>Called when the opponent disconnects during the match.</summary>
        public void OnOpponentDisconnected(ulong disconnectedId)
        {
            if (_matchEnded) return;
            _matchEnded = true;
            _state.PhaseInt.Value = (int)GamePhase.ShowResult;

            int p1Score = _state.Player1Score.Value;
            int p2Score = _state.Player2Score.Value;

            ulong winnerId = disconnectedId == _p1Id ? _p2Id : _p1Id;
            _state.MatchEndClientRpc(winnerId, p1Score, p2Score, 10, -10);

            ServerMatchManager.Instance?.OnMatchFinished(_p1Id, _p2Id);
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        /// <summary>FixedString64Bytes holds up to ~61 UTF-8 bytes; clamp to stay safe.</summary>
        private static FixedString64Bytes ToFixed(string s)
        {
            if (string.IsNullOrEmpty(s)) return new FixedString64Bytes("Player");
            if (s.Length > 20) s = s.Substring(0, 20);
            return new FixedString64Bytes(s);
        }

        private static bool CheckAnswer(int[] filled, int[] correct)
        {
            if (filled == null || correct == null) return false;
            if (filled.Length != correct.Length)   return false;
            for (int i = 0; i < correct.Length; i++)
                if (filled[i] != correct[i]) return false;
            return true;
        }
    }
}
