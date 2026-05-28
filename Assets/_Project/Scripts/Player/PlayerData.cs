using UnityEngine;

namespace MathGame.Player
{
    [System.Serializable]
    public class PlayerData
    {
        public string playerId;
        public string displayName;
        public int    eloRating;
        public int    gamesPlayed;
        public int    gamesWon;
        public PlayerRank currentRank;

        // ── Persistence via PlayerPrefs ──────────────────────────────────────

        private const string KEY_ELO   = "player_elo";
        private const string KEY_GAMES = "player_games";
        private const string KEY_WINS  = "player_wins";
        private const string KEY_NAME  = "player_name";

        public void Save()
        {
            PlayerPrefs.SetInt(KEY_ELO,   eloRating);
            PlayerPrefs.SetInt(KEY_GAMES, gamesPlayed);
            PlayerPrefs.SetInt(KEY_WINS,  gamesWon);
            PlayerPrefs.SetString(KEY_NAME, displayName ?? "Player");
            PlayerPrefs.Save();
        }

        public static PlayerData Load()
        {
            var data = new PlayerData
            {
                eloRating   = PlayerPrefs.GetInt(KEY_ELO, 800),
                gamesPlayed = PlayerPrefs.GetInt(KEY_GAMES, 0),
                gamesWon    = PlayerPrefs.GetInt(KEY_WINS, 0),
                displayName = PlayerPrefs.GetString(KEY_NAME, "Player"),
            };
            data.currentRank = RankSystem.GetRank(data.eloRating);
            return data;
        }
    }
}
