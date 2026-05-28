using UnityEngine;

namespace MathGame.Network
{
    [System.Serializable]
    public struct PlayerConnectionData
    {
        public string playerName;
        public int    eloRating;

        public byte[] ToBytes() =>
            System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));

        public static PlayerConnectionData FromBytes(byte[] bytes)
        {
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            return JsonUtility.FromJson<PlayerConnectionData>(json);
        }
    }
}
