using System;
using MathGame.Player;

namespace MathGame.Question
{
    [Serializable]
    public class QuestionData
    {
        // e.g. "__ + __ = 10"  or  "(__ + 3) × __ = 28"
        public string expressionTemplate;

        // Values for each blank, ordered left to right
        public int[] correctAnswers;

        // Shuffled pool of numbers shown to the player (includes correct + distractors)
        public int[] poolNumbers;

        public int blankCount;
        public PlayerRank targetRank;
    }
}
