namespace MathGame.Player
{
    public static class RankSystem
    {
        // ELO floor for each rank (index == (int)PlayerRank)
        public static readonly int[] EloThresholds = { 0, 1000, 1500, 2000, 2500 };

        public static readonly string[] RankNames =
            { "Bronze", "Silver", "Gold", "Platinum", "Diamond" };

        public static PlayerRank GetRank(int elo)
        {
            for (int i = EloThresholds.Length - 1; i >= 0; i--)
            {
                if (elo >= EloThresholds[i])
                    return (PlayerRank)i;
            }
            return PlayerRank.Bronze;
        }

        /// <summary>Returns the rank used to select question difficulty when two players meet.</summary>
        public static PlayerRank GetMatchRank(int eloA, int eloB)
        {
            return GetRank((eloA + eloB) / 2);
        }

        public static string GetRankName(PlayerRank rank) => RankNames[(int)rank];
    }
}
