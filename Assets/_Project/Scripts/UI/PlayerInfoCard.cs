using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MathGame.Player;

namespace MathGame.UI
{
    /// <summary>
    /// Displays one player's name, current score, and rank badge in the top bar.
    /// </summary>
    public class PlayerInfoCard : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _scoreText;
        [SerializeField] private TextMeshProUGUI _rankText;
        [SerializeField] private Image           _rankBadge;

        [Header("Rank badge colors (Bronze → Diamond)")]
        [SerializeField] private Color[] _rankColors = new Color[]
        {
            new Color(0.80f, 0.50f, 0.20f),   // Bronze
            new Color(0.75f, 0.75f, 0.75f),   // Silver
            new Color(1.00f, 0.84f, 0.00f),   // Gold
            new Color(0.22f, 0.75f, 0.90f),   // Platinum
            new Color(0.70f, 0.30f, 1.00f),   // Diamond
        };

        public void Setup(string playerName, PlayerRank rank, int elo)
        {
            _nameText.text = playerName;
            _rankText.text = RankSystem.GetRankName(rank);

            int rankIdx = (int)rank;
            if (_rankBadge != null && rankIdx < _rankColors.Length)
                _rankBadge.color = _rankColors[rankIdx];

            SetScore(0);
        }

        public void SetScore(int score)
        {
            _scoreText.text = score.ToString();
        }
    }
}
