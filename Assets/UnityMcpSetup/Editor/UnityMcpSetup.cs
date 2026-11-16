using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            if (Application.isBatchMode) return;
            // Before the package's own startup work (its setup window's dependency check and its server auto-start
            // run on later ticks), so both see a uv installed since the Hub started.
            RefreshPath();
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
            double retryIn = 15;
            try
            {
                EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
                EditorPrefs.SetString("MCPForUnity.HttpTransportScope", "local");
                EditorPrefs.SetString("MCPForUnity.HttpUrl", "http://127.0.0.1:8080");
                EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);
                EditorPrefs.SetBool("MCPForUnity.HttpServerLaunchConfirmed", true);
                EditorConfigurationCache.Instance.Refresh();
                RefreshPath();
                // The package launches the server only once per editor session, so after that launch fails (uv not
                // installed yet, say) launching it again is up to this loop.
                var server = MCPServiceLocator.Server;
                if (!server.IsLocalHttpServerReachable())
                {
                    if (server.IsManagedServerLaunchProcessAlive()) return; // A launch is still booting; let it finish.
                    if (!await LaunchServer(server)) { retryIn = 60; return; }
                }
                bool connected = await MCPServiceLocator.Bridge.StartAsync();
                Debug.Log("Workshop Unity MCP connected: " + connected);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Workshop Unity MCP connection failed; retrying in 15 seconds: " + error.Message);
            }
            finally
            {
                connecting = false;
                nextAttempt = EditorApplication.timeSinceStartup + retryIn;
            }
        }

        // Waits as the package's own auto-start does: while the launch process lives (a first run downloads Python and
        // the server, which can take minutes), failing once it exits without opening the port.
        private static async Task<bool> LaunchServer(IServerManagementService server)
        {
            if (!server.CanStartLocalServer() || !server.StartLocalHttpServer(quiet: true)) return false;
            double start = EditorApplication.timeSinceStartup;
            while (!server.IsLocalHttpServerReachable())
            {
                double elapsed = EditorApplication.timeSinceStartup - start;
                if ((elapsed > 1 && !server.IsManagedServerLaunchProcessAlive()) || elapsed > 300)
                {
                    server.LogLocalHttpServerLaunchFailure();
                    return false;
                }
                await Task.Delay(500);
            }
            return true;
        }

        // The editor keeps the PATH it started with, so a tool installed while it's open (uv, which the server runs
        // under) stays invisible until a restart of the editor and of the Hub that launched it. Windows keeps the
        // current PATH in the registry; merge it in. The package searches this process's PATH for uvx, and the server's
        // shell inherits it.
        private static void RefreshPath()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor) return;
            var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Where(d => d.Length > 0).ToList();
            var known = new HashSet<string>(dirs, StringComparer.OrdinalIgnoreCase);
            foreach (var target in new[] { EnvironmentVariableTarget.Machine, EnvironmentVariableTarget.User })
                foreach (string entry in (Environment.GetEnvironmentVariable("PATH", target) ?? "").Split(Path.PathSeparator))
                {
                    string dir = Environment.ExpandEnvironmentVariables(entry.Trim());
                    if (dir.Length > 0 && known.Add(dir)) dirs.Add(dir);
                }
            Environment.SetEnvironmentVariable("PATH", string.Join(Path.PathSeparator.ToString(), dirs));
        }
    }
}
