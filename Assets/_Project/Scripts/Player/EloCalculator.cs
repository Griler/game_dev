using System;

namespace MathGame.Player
{
    public static class EloCalculator
    {
        private const int K_NEW    = 40;   // fewer than 30 games played
        private const int K_NORMAL = 20;
        private const int K_HIGH   = 10;   // ELO >= 2400

        /// <summary>
        /// Returns updated ELO for both players after a match.
        /// scoreA: 1.0 = A won, 0.5 = draw, 0.0 = A lost.
        /// </summary>
        public static (int newEloA, int newEloB) Calculate(
            int eloA, int eloB, float scoreA,
            int gamesPlayedA, int gamesPlayedB)
        {
            float expectedA = 1f / (1f + (float)Math.Pow(10.0, (eloB - eloA) / 400.0));
            float expectedB = 1f - expectedA;
            float scoreB    = 1f - scoreA;

            int kA = GetK(eloA, gamesPlayedA);
            int kB = GetK(eloB, gamesPlayedB);

            int newEloA = eloA + (int)Math.Round(kA * (scoreA - expectedA));
            int newEloB = eloB + (int)Math.Round(kB * (scoreB - expectedB));

            return (Math.Max(0, newEloA), Math.Max(0, newEloB));
        }

        /// <summary>Convenience: derive scoreA from question counts.</summary>
        public static (int newEloA, int newEloB) CalculateFromScores(
            int eloA, int eloB,
            int correctA, int correctB,
            int gamesPlayedA, int gamesPlayedB)
        {
            float scoreA = correctA + correctB == 0
                ? 0.5f
                : (float)correctA / (correctA + correctB);

            return Calculate(eloA, eloB, scoreA, gamesPlayedA, gamesPlayedB);
        }

        private static int GetK(int elo, int gamesPlayed)
        {
            if (gamesPlayed < 30) return K_NEW;
            if (elo >= 2400)      return K_HIGH;
            return K_NORMAL;
        }
    }
}
