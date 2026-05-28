using MathGame.Player;

namespace MathGame.Question
{
    /// <summary>
    /// Provides hardcoded default RankConfig data so the game works at runtime
    /// even if ScriptableObject assets have not been created in the Editor yet.
    /// </summary>
    public static class RankConfigProvider
    {
        public static RankConfigData GetDefault(PlayerRank rank)
        {
            return rank switch
            {
                PlayerRank.Bronze => new RankConfigData
                {
                    rank              = PlayerRank.Bronze,
                    blankCount        = 1,
                    allowedOperators  = new[] { Operator.Add, Operator.Subtract },
                    minValue          = 1,
                    maxValue          = 20,
                    poolSize          = 6,
                    questionsPerRound = 10,
                    roundDurationSeconds = 60f,
                },
                PlayerRank.Silver => new RankConfigData
                {
                    rank              = PlayerRank.Silver,
                    blankCount        = 2,
                    allowedOperators  = new[] { Operator.Add, Operator.Subtract, Operator.Multiply },
                    minValue          = 1,
                    maxValue          = 50,
                    poolSize          = 7,
                    questionsPerRound = 10,
                    roundDurationSeconds = 60f,
                },
                PlayerRank.Gold => new RankConfigData
                {
                    rank              = PlayerRank.Gold,
                    blankCount        = 2,
                    allowedOperators  = new[] { Operator.Add, Operator.Subtract, Operator.Multiply, Operator.Divide },
                    minValue          = 1,
                    maxValue          = 100,
                    poolSize          = 8,
                    questionsPerRound = 10,
                    roundDurationSeconds = 60f,
                },
                PlayerRank.Platinum => new RankConfigData
                {
                    rank              = PlayerRank.Platinum,
                    blankCount        = 2,
                    allowedOperators  = new[] { Operator.Add, Operator.Subtract, Operator.Multiply },
                    minValue          = 2,
                    maxValue          = 100,
                    poolSize          = 8,
                    questionsPerRound = 10,
                    roundDurationSeconds = 60f,
                },
                _ => new RankConfigData
                {
                    rank              = PlayerRank.Diamond,
                    blankCount        = 3,
                    allowedOperators  = new[] { Operator.Add, Operator.Subtract, Operator.Multiply },
                    minValue          = 2,
                    maxValue          = 100,
                    poolSize          = 9,
                    questionsPerRound = 10,
                    roundDurationSeconds = 60f,
                },
            };
        }
    }

    /// <summary>Pure-data mirror of RankConfig (no Unity dependency) used by QuestionGenerator.</summary>
    public class RankConfigData
    {
        public PlayerRank rank;
        public int blankCount;
        public Operator[] allowedOperators;
        public int minValue;
        public int maxValue;
        public int poolSize;
        public int questionsPerRound;
        public float roundDurationSeconds;
    }
}
