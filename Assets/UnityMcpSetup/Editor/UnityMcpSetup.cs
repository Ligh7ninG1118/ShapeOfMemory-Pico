using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

namespace Workshop.EditorTools
{
    [InitializeOnLoad]
    public static class UnityMcpSetup
    {
        private static bool connecting;
        private static double nextAttempt;
        static UnityMcpSetup()
        {
            if (!Application.isBatchMode)
                EditorApplication.update += CheckConnection;
        }
        private static void CheckConnection()
        {
            if (connecting || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.timeSinceStartup < nextAttempt) return;
            nextAttempt = EditorApplication.timeSinceStartup + 15;
            if (!MCPServiceLocator.Bridge.IsRunning) Connect();
        }
        [MenuItem("MetaHackthone/Connect Unity MCP")]
        public static async void Connect()
        {
            if (connecting || Application.isBatchMode) return;
            connecting = true;
            try
            {
                EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
                EditorPrefs.SetString("MCPForUnity.HttpTransportScope", "local");
                EditorPrefs.SetString("MCPForUnity.HttpUrl", "http://127.0.0.1:8080");
                EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);
                EditorPrefs.SetBool("MCPForUnity.HttpServerLaunchConfirmed", true);
                EditorConfigurationCache.Instance.Refresh();
                bool connected = await MCPServiceLocator.Bridge.StartAsync();
                Debug.Log("Workshop Unity MCP connected: " + connected);
            }
            catch (System.Exception error)
            {
                Debug.LogWarning("Workshop Unity MCP connection failed; retrying in 15 seconds: " + error.Message);
            }
            finally
            {
                connecting = false;
                nextAttempt = EditorApplication.timeSinceStartup + 15;
            }
        }
    }
}
