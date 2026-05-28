using UnityEngine;
using MathGame.Player;

namespace MathGame.Question
{
    [CreateAssetMenu(fileName = "RankConfig", menuName = "MathGame/RankConfig")]
    public class RankConfig : ScriptableObject
    {
        public PlayerRank rank;

        [Tooltip("How many blanks questions of this rank have (max, may vary)")]
        public int blankCount;

        public Operator[] allowedOperators;

        public int minValue;
        public int maxValue;

        [Tooltip("Total tiles shown in the number pool (correct + distractors)")]
        public int poolSize;

        public int questionsPerRound;
        public float roundDurationSeconds;
    }
}
