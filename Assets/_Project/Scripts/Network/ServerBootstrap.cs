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
            string publicIP = GetPublicIP();
            bool noLobby = HasFlag("-noLobby");

            try
            {
                if (!noLobby)
                    await MatchmakingService.Instance.InitUGS();

                ConnectionManager.Instance.StartAsServer();

                if (!noLobby)
                    await MatchmakingService.Instance.RegisterServerLobbies(
                        publicIP, ConnectionManager.Instance.ServerPort);

                Debug.Log($"[ServerBootstrap] Server ready. Public IP: {publicIP}, noLobby: {noLobby}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ServerBootstrap] Startup failed: {e}");
            }
        }

        private string GetPublicIP()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-serverIP") return args[i + 1];
            return defaultPublicIP;
        }

        private static bool HasFlag(string flag)
        {
            foreach (var arg in System.Environment.GetCommandLineArgs())
                if (arg == flag) return true;
            return false;
        }
    }
}
