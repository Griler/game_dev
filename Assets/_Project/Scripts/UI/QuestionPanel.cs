using System;
using System.Collections;
using UnityEngine;
using TMPro;
using MathGame.Question;
using MathGame.Core;

namespace MathGame.UI
{
    /// <summary>
    /// Owns the expression text and blank slots.
    /// Orchestrates left-to-right blank filling and triggers answer submission.
    /// </summary>
    public class QuestionPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _expressionText;
        [SerializeField] private TextMeshProUGUI _questionCounterText;
        [SerializeField] private BlankSlot[]     _blankSlots;          // sized to max blanks (3)

        private QuestionData _question;
        private int[]        _filledValues;
        private NumberTile[] _tilesInBlanks;    // which tile occupies each blank
        private int          _currentBlankIndex;
        private bool         _submitted;

        // ── Public API ───────────────────────────────────────────────────────

        public void DisplayQuestion(QuestionData q, int questionIndex, int totalQuestions)
        {
            _question          = q;
            _filledValues      = new int[q.blankCount];
            _tilesInBlanks     = new NumberTile[q.blankCount];
            _currentBlankIndex = 0;
            _submitted         = false;

            // Show/hide blank slots based on blank count
            for (int i = 0; i < _blankSlots.Length; i++)
            {
                bool active = i < q.blankCount;
                _blankSlots[i].gameObject.SetActive(active);
                if (active) _blankSlots[i].SetEmpty();
            }

            // totalQuestions <= 0 → open-ended race, show just the running count.
            _questionCounterText.text = totalQuestions > 0
                ? $"Câu {questionIndex}/{totalQuestions}"
                : $"Câu {questionIndex}";
            RefreshExpressionText();
        }

        /// <summary>Called by NumberTile when it is tapped.</summary>
        public void FillNextBlank(int value, NumberTile sourceTile)
        {
            if (_submitted) return;
            if (_currentBlankIndex >= _question.blankCount) return;

            _filledValues[_currentBlankIndex]  = value;
            _tilesInBlanks[_currentBlankIndex] = sourceTile;
            _blankSlots[_currentBlankIndex].SetValue(value);
            _currentBlankIndex++;

            RefreshExpressionText();

            if (_currentBlankIndex == _question.blankCount)
                TrySubmit();
        }

        /// <summary>Called by backspace button; clears the most recently filled blank.</summary>
        public void ClearLastBlank()
        {
            if (_submitted) return;
            if (_currentBlankIndex <= 0) return;

            _currentBlankIndex--;
            _tilesInBlanks[_currentBlankIndex]?.SetUsed(false);
            _tilesInBlanks[_currentBlankIndex] = null;
            _blankSlots[_currentBlankIndex].SetEmpty();

            RefreshExpressionText();
        }

        /// <summary>Shows correct/wrong flash after GameManager validates the answer.</summary>
        public void ShowAnswerFeedback(bool correct, int[] correctValues)
        {
            for (int i = 0; i < _question.blankCount; i++)
            {
                if (i < _blankSlots.Length && _blankSlots[i].gameObject.activeSelf)
                {
                    if (correct)
                        _blankSlots[i].FlashCorrect();
                    else
                        _blankSlots[i].FlashWrong();
                }
            }

            if (!correct)
                StartCoroutine(ResetAfterDelay(1.1f));
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void TrySubmit()
        {
            if (_submitted) return;
            _submitted = true;
            GameManager.Instance?.SubmitAnswer(_filledValues);
        }

        private void RefreshExpressionText()
        {
            if (_question == null) return;

            // Split on __ to avoid replacing into a string that still contains __
            string[] parts = _question.expressionTemplate.Split(
                new string[] { "__" }, StringSplitOptions.None);

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                sb.Append(parts[i]);
                if (i < parts.Length - 1) // each gap between parts = one blank
                {
                    if (i < _currentBlankIndex)
                        sb.Append($"<color=#5BE3FF><b>{_filledValues[i]}</b></color>");
                    else
                        sb.Append("<color=#FFD700><b>?</b></color>");
                }
            }

            _expressionText.text = sb.ToString();
        }

        private IEnumerator ResetAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (_question == null) yield break;

            // Re-enable all tiles that were placed
            for (int i = 0; i < _question.blankCount; i++)
                _tilesInBlanks[i]?.SetUsed(false);

            // Reset blanks
            _filledValues      = new int[_question.blankCount];
            _tilesInBlanks     = new NumberTile[_question.blankCount];
            _currentBlankIndex = 0;
            _submitted         = false;

            for (int i = 0; i < _question.blankCount && i < _blankSlots.Length; i++)
                _blankSlots[i]?.SetEmpty();

            RefreshExpressionText();
        }
    }
}
