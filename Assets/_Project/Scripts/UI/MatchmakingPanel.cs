using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MathGame.Network;
using MathGame.Player;

namespace MathGame.UI
{
    /// <summary>
    /// "Finding match..." screen. Calls MatchmakingService + ConnectionManager,
    /// then hides itself when the match starts.
    /// </summary>
    public class MatchmakingPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statusLabel;
        [SerializeField] private Button          cancelButton;

        private bool _cancelled;

        private void Awake()
        {
            cancelButton?.onClick.AddListener(OnCancel);
        }

        private void OnDestroy()
        {
            cancelButton?.onClick.RemoveListener(OnCancel);
        }

        public async void StartMatchmaking()
        {
            _cancelled = false;
            gameObject.SetActive(true);
            SetStatus("Signing in…");

            var gm = Core.GameManager.Instance;
            if (gm == null) { SetStatus("GameManager not found!"); return; }

            try
            {
                await MatchmakingService.Instance.InitUGS();
                if (_cancelled) return;

                var rank = gm.LocalPlayer?.currentRank ?? PlayerRank.Bronze;
                SetStatus($"Finding {rank} match…");

                var (ip, port) = await MatchmakingService.Instance.FindServerLobby(rank);
                if (_cancelled) return;

                SetStatus("Connecting…");
                ConnectionManager.Instance.StartAsClient(
                    ip, port,
                    gm.LocalPlayer?.displayName ?? "Player",
                    gm.LocalPlayer?.eloRating   ?? 800,
                    gm.LocalPlayer?.gamesPlayed ?? 0);

                // Panel stays visible until NetworkGameState spawns and game begins
            }
            catch (System.Exception e)
            {
                SetStatus($"Error: {e.Message}");
                Debug.LogError($"[Matchmaking] {e}");
            }
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnCancel()
        {
            _cancelled = true;
            Hide();
        }

        private void SetStatus(string msg)
        {
            if (statusLabel != null) statusLabel.text = msg;
            Debug.Log($"[Matchmaking] {msg}");
        }
    }
}
