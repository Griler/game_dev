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
        private const float AdvanceDelay = 0.7f; // correct → pause, then next question
        private const float RetryDelay   = 1.2f; // wrong → pause (matches client reset), retry same

        // ── State ───────────────────────────────────────────────────────────
        private NetworkGameState   _state;
        private List<QuestionData> _questions;
        private RankConfigData     _config;
        private bool               _matchEnded;

        // Per-player race progress (0-based index into _questions) + feedback lock.
        private int  _p1Index, _p2Index;
        private bool _p1Locked, _p2Locked;

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

            // Generate the shared question pool server-side (seed is broadcast to
            // clients). Players race through the same sequence at their own pace.
            int seed = Random.Range(int.MinValue, int.MaxValue);
            _state.QuestionSeed.Value = seed;
            var gen = new QuestionGenerator(seed);
            _questions = new List<QuestionData>(NetworkGameState.MaxRaceQuestions);
            for (int i = 0; i < NetworkGameState.MaxRaceQuestions; i++)
                _questions.Add(gen.Generate(_config));

            _state.TimeRemaining.Value = _config.roundDurationSeconds;
            _matchEnded = false;
            _p1Index = _p2Index = 0;
            _p1Locked = _p2Locked = false;

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

            // Both players start on question 1 at the same moment, then diverge.
            _state.PhaseInt.Value      = (int)GamePhase.Playing;
            _state.Player1QIndex.Value = 1;
            _state.Player2QIndex.Value = 1;
        }

        // ── Answer handling ─────────────────────────────────────────────────

        public void HandleAnswer(ulong clientId, int[] filledValues)
        {
            if (_matchEnded) return;
            if (_state.CurrentPhase != GamePhase.Playing) return;

            bool isP1;
            if      (clientId == _p1Id) isP1 = true;
            else if (clientId == _p2Id) isP1 = false;
            else return; // not a participant of this match

            // Ignore while this player is in the feedback/advance window.
            if (isP1 ? _p1Locked : _p2Locked) return;

            int index = isP1 ? _p1Index : _p2Index;
            if (index < 0 || index >= _questions.Count) return; // ran out of questions

            if (isP1) _p1Locked = true; else _p2Locked = true;

            var question = _questions[index];
            bool correct = CheckAnswer(filledValues, question.correctAnswers);

            _state.ShowAnswerResultClientRpc(clientId, correct, question.correctAnswers);

            if (correct)
            {
                if (isP1) _state.Player1Score.Value++;
                else      _state.Player2Score.Value++;
                StartCoroutine(AdvancePlayer(isP1));     // move on to the next question
            }
            else
            {
                StartCoroutine(UnlockAfter(isP1, RetryDelay)); // retry the same question
            }
        }

        /// <summary>After a short feedback pause, move one player to their next question.</summary>
        private IEnumerator AdvancePlayer(bool isP1)
        {
            yield return new WaitForSeconds(AdvanceDelay);
            if (_matchEnded) yield break;

            if (isP1)
            {
                _p1Index++;
                if (_p1Index < _questions.Count)
                    _state.Player1QIndex.Value = _p1Index + 1;
                _p1Locked = false;
            }
            else
            {
                _p2Index++;
                if (_p2Index < _questions.Count)
                    _state.Player2QIndex.Value = _p2Index + 1;
                _p2Locked = false;
            }
        }

        /// <summary>Wrong answer: keep the same question, just lift the lock so they can retry.</summary>
        private IEnumerator UnlockAfter(bool isP1, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_matchEnded) yield break;
            if (isP1) _p1Locked = false; else _p2Locked = false;
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
