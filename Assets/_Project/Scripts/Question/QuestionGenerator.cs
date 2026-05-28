using System;
using System.Collections.Generic;
using MathGame.Player;

namespace MathGame.Question
{
    /// <summary>
    /// Pure C# (no Unity dependency). Given the same seed, produces identical questions
    /// on both clients — only the seed needs to travel over the network.
    /// </summary>
    public class QuestionGenerator
    {
        private readonly Random _rng;

        public QuestionGenerator(int seed)
        {
            _rng = new Random(seed);
        }

        public QuestionData Generate(RankConfigData config)
        {
            return config.rank switch
            {
                PlayerRank.Bronze   => GenerateSingleBlank(config),
                PlayerRank.Silver   => _rng.Next(2) == 0
                                         ? GenerateSingleBlank(config)
                                         : GenerateDoubleBlank(config),
                PlayerRank.Gold     => GenerateDoubleBlank(config),
                PlayerRank.Platinum => GenerateChainedDouble(config),
                PlayerRank.Diamond  => GenerateTripleBlank(config),
                _                   => GenerateSingleBlank(config),
            };
        }

        // ── Single blank: a OP b = c, hide one operand ─────────────────────────

        private QuestionData GenerateSingleBlank(RankConfigData config)
        {
            var op = PickOperator(config);
            GenerateOperands(op, config, out int a, out int b, out int result);

            bool hideFirst = _rng.Next(2) == 0;
            string template;
            int[] correct;

            if (hideFirst)
            {
                template = $"__ {OpSymbol(op)} {b} = {result}";
                correct  = new[] { a };
            }
            else
            {
                template = $"{a} {OpSymbol(op)} __ = {result}";
                correct  = new[] { b };
            }

            return Build(template, correct, config);
        }

        // ── Double blank: __ OP __ = result ────────────────────────────────────

        private QuestionData GenerateDoubleBlank(RankConfigData config)
        {
            var op = PickOperator(config);
            GenerateOperands(op, config, out int a, out int b, out int result);

            string template = $"__ {OpSymbol(op)} __ = {result}";
            int[]  correct  = new[] { a, b };

            return Build(template, correct, config);
        }

        // ── Chained double (Platinum): (__ + c) × __ = result ─────────────────

        private QuestionData GenerateChainedDouble(RankConfigData config)
        {
            int c, a, b, result;
            int attempts = 0;
            do
            {
                c      = _rng.Next(2, 9);
                a      = _rng.Next(2, 10);
                b      = _rng.Next(2, 7);
                result = (a + c) * b;
                attempts++;
            }
            while ((result > 100 || result < 10) && attempts < 50);

            string template = $"(__ + {c}) × __ = {result}";
            int[]  correct  = new[] { a, b };

            return Build(template, correct, config);
        }

        // ── Triple blank (Diamond): __ × __ + __ = result ─────────────────────

        private QuestionData GenerateTripleBlank(RankConfigData config)
        {
            int a, b, c, result;
            int attempts = 0;
            do
            {
                a      = _rng.Next(2, 10);
                b      = _rng.Next(2, 10);
                c      = _rng.Next(1, 20);
                result = a * b + c;
                attempts++;
            }
            while (result > 150 && attempts < 50);

            string template = $"__ × __ + __ = {result}";
            int[]  correct  = new[] { a, b, c };

            return Build(template, correct, config);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private QuestionData Build(string template, int[] correct, RankConfigData config)
        {
            return new QuestionData
            {
                expressionTemplate = template,
                correctAnswers     = correct,
                poolNumbers        = GeneratePool(correct, config),
                blankCount         = correct.Length,
                targetRank         = config.rank,
            };
        }

        private void GenerateOperands(Operator op, RankConfigData cfg,
                                      out int a, out int b, out int result)
        {
            int min = Math.Max(1, cfg.minValue);
            int max = cfg.maxValue;

            switch (op)
            {
                case Operator.Add:
                    a      = _rng.Next(min, max / 2 + 1);
                    b      = _rng.Next(min, max / 2 + 1);
                    result = a + b;
                    return;

                case Operator.Subtract:
                    a      = _rng.Next(min + 5, max + 1);
                    b      = _rng.Next(min, a);        // b < a → result > 0
                    result = a - b;
                    return;

                case Operator.Multiply:
                    int mulMax = Math.Min(12, max);
                    a      = _rng.Next(2, mulMax + 1);
                    b      = _rng.Next(2, mulMax + 1);
                    result = a * b;
                    return;

                case Operator.Divide:
                    b      = _rng.Next(2, 11);
                    result = _rng.Next(2, max / b + 1);
                    a      = b * result;               // guarantees exact division
                    return;

                default:
                    a = b = result = 1;
                    return;
            }
        }

        private Operator PickOperator(RankConfigData config)
        {
            return config.allowedOperators[_rng.Next(config.allowedOperators.Length)];
        }

        private static string OpSymbol(Operator op) => op switch
        {
            Operator.Add      => "+",
            Operator.Subtract => "−",
            Operator.Multiply => "×",
            Operator.Divide   => "÷",
            _                 => "?",
        };

        /// <summary>
        /// Builds a shuffled pool that always contains all correct answers,
        /// padded with plausible distractors to reach config.poolSize.
        /// </summary>
        private int[] GeneratePool(int[] correct, RankConfigData config)
        {
            var pool = new HashSet<int>(correct);

            // Plausible distractors: ±delta of each correct value
            int attempts = 0;
            while (pool.Count < config.poolSize && attempts < 200)
            {
                attempts++;
                int baseVal = correct[_rng.Next(correct.Length)];
                int delta   = _rng.Next(1, 6) * (_rng.Next(2) == 0 ? 1 : -1);
                int candidate = baseVal + delta;
                if (candidate > 0 && !IsInArray(correct, candidate))
                    pool.Add(candidate);
            }

            // Fill remaining slots with numbers in the config range
            while (pool.Count < config.poolSize)
            {
                int candidate = _rng.Next(config.minValue, config.maxValue + 1);
                if (!IsInArray(correct, candidate))
                    pool.Add(candidate);
            }

            // Shuffle (Fisher-Yates)
            var arr = new List<int>(pool);
            for (int i = arr.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }

            return arr.ToArray();
        }

        private static bool IsInArray(int[] arr, int val)
        {
            foreach (int v in arr)
                if (v == val) return true;
            return false;
        }
    }
}
