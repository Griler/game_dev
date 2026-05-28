using NUnit.Framework;
using MathGame.Question;
using MathGame.Player;

namespace MathGame.Tests
{
    public class QuestionGeneratorTests
    {
        private RankConfigData ConfigFor(PlayerRank rank) =>
            RankConfigProvider.GetDefault(rank);

        [Test]
        public void SameSeedProducesIdenticalQuestion()
        {
            var genA = new QuestionGenerator(42);
            var genB = new QuestionGenerator(42);

            var cfgB = ConfigFor(PlayerRank.Bronze);
            var qA   = genA.Generate(cfgB);
            var qB   = genB.Generate(cfgB);

            Assert.AreEqual(qA.expressionTemplate, qB.expressionTemplate);
            Assert.AreEqual(qA.correctAnswers[0],  qB.correctAnswers[0]);
        }

        [Test]
        public void BronzeHasOneBlank()
        {
            var gen = new QuestionGenerator(1);
            var q   = gen.Generate(ConfigFor(PlayerRank.Bronze));
            Assert.AreEqual(1, q.blankCount);
            Assert.AreEqual(1, q.correctAnswers.Length);
        }

        [Test]
        public void GoldHasTwoBlanks()
        {
            var gen = new QuestionGenerator(1);
            var q   = gen.Generate(ConfigFor(PlayerRank.Gold));
            Assert.AreEqual(2, q.blankCount);
            Assert.AreEqual(2, q.correctAnswers.Length);
        }

        [Test]
        public void DiamondHasThreeBlanks()
        {
            var gen = new QuestionGenerator(1);
            var q   = gen.Generate(ConfigFor(PlayerRank.Diamond));
            Assert.AreEqual(3, q.blankCount);
            Assert.AreEqual(3, q.correctAnswers.Length);
        }

        [Test]
        public void PoolAlwaysContainsAllCorrectAnswers()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                foreach (PlayerRank rank in System.Enum.GetValues(typeof(PlayerRank)))
                {
                    var gen = new QuestionGenerator(seed);
                    var cfg = ConfigFor(rank);
                    var q   = gen.Generate(cfg);

                    foreach (int correct in q.correctAnswers)
                    {
                        bool found = false;
                        foreach (int poolNum in q.poolNumbers)
                            if (poolNum == correct) { found = true; break; }
                        Assert.IsTrue(found,
                            $"Pool missing correct answer {correct} for rank {rank} seed {seed}.\n" +
                            $"Pool: [{string.Join(", ", q.poolNumbers)}]\n" +
                            $"Q: {q.expressionTemplate}");
                    }
                }
            }
        }

        [Test]
        public void PoolSizeMatchesConfig()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                foreach (PlayerRank rank in System.Enum.GetValues(typeof(PlayerRank)))
                {
                    var gen = new QuestionGenerator(seed);
                    var cfg = ConfigFor(rank);
                    var q   = gen.Generate(cfg);
                    Assert.AreEqual(cfg.poolSize, q.poolNumbers.Length,
                        $"Wrong pool size for rank {rank} seed {seed}");
                }
            }
        }

        [Test]
        public void CorrectAnswersArePositive()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                foreach (PlayerRank rank in System.Enum.GetValues(typeof(PlayerRank)))
                {
                    var gen = new QuestionGenerator(seed);
                    var q   = gen.Generate(ConfigFor(rank));
                    foreach (int v in q.correctAnswers)
                        Assert.Greater(v, 0, $"Non-positive answer in rank {rank} seed {seed}: {q.expressionTemplate}");
                }
            }
        }

        [Test]
        public void ExpressionTemplateContainsCorrectNumberOfBlanks()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                foreach (PlayerRank rank in System.Enum.GetValues(typeof(PlayerRank)))
                {
                    var gen   = new QuestionGenerator(seed);
                    var q     = gen.Generate(ConfigFor(rank));
                    int count = 0;
                    int pos   = 0;
                    while ((pos = q.expressionTemplate.IndexOf("__", pos)) >= 0)
                    {
                        count++;
                        pos += 2;
                    }
                    Assert.AreEqual(q.blankCount, count,
                        $"Blank count mismatch for rank {rank} seed {seed}: {q.expressionTemplate}");
                }
            }
        }
    }
}
