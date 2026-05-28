using UnityEngine;

namespace MathGame.Network
{
    /// <summary>
    /// Detects headless (-batchmode) startup and automatically kicks off the
    /// server flow: UGS auth → start NGO server → register Lobby slots.
    /// On a normal client build this component does nothing.
    /// </summary>
    public class ServerBootstrap : MonoBehaviour
    {
        [SerializeField] private string defaultPublicIP = "127.0.0.1";

        private void Start()
        {
            if (Application.isBatchMode)
            {
                Debug.Log("[ServerBootstrap] Headless mode detected — starting server.");
                StartServerFlow();
            }
        }

        private async void StartServerFlow()
        {
            // Allow ConnectionManager to read command-line IP first
            string publicIP = GetPublicIP();

            try
            {
                await MatchmakingService.Instance.InitUGS();
                ConnectionManager.Instance.StartAsServer();
                await MatchmakingService.Instance.RegisterServerLobbies(
                    publicIP, ConnectionManager.Instance.ServerPort);

                Debug.Log($"[ServerBootstrap] Server ready. Public IP: {publicIP}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ServerBootstrap] Startup failed: {e}");
            }
        }

        private string GetPublicIP()
        {
            // Command-line overrides inspector default
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-serverIP") return args[i + 1];
            return defaultPublicIP;
        }
    }
}
