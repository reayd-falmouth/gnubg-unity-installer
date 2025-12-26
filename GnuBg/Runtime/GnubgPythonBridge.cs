using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

// Needed for Application.streamingAssetsPath

namespace gnubg_unity_installer.GnuBg.Runtime
{
    public static class GnubgPythonBridge
    {
        /// <summary>
        /// Path to GNUBG executable inside StreamingAssets.
        /// Unity copies this folder into the build unchanged.
        /// </summary>
        private static string GnubgExe =>
            Path.Combine(
                Application.streamingAssetsPath,
                "gnubg",
                PlatformFolder,
                ExecutableName
            );


        /// <summary>
        /// Path to the Python GNUBG bridge script.
        /// </summary>
        private static string LibScript =>
            Path.Combine(
                Application.streamingAssetsPath,
                "gnubg",
                PlatformFolder,
                "libgnubg.py"
            );

        /// <summary>
        /// Executable name per platform.
        /// </summary>
        private static string ExecutableName
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                return "gnubg.exe";
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                return "gnubg";
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
                return "gnubg";
#else
                throw new PlatformNotSupportedException("GNUBG external process not supported on this platform.");
#endif
            }
        }
        
        private static string PlatformFolder
        {
            get
            {
        #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                return "bin";
        #elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                return "bin";
        #elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
                return "bin";
        #else
                throw new PlatformNotSupportedException("GNUBG not supported on this platform.");
        #endif
            }
        }

        /// <summary>
        /// Runs GNUBG asynchronously in a background thread.
        /// Returns the path to the JSON file once finished.
        /// </summary>
        public static async Task<string> RunAsync(
            string matchRef,
            string gameId,
            string variation = "standard",
            bool jacoby = false,
            string action = "hint")
        {
            // Use a temp folder to store output (same as before)
            string homeDir = Path.Combine(Path.GetTempPath(), matchRef);
            Directory.CreateDirectory(homeDir);

            var env = new Dictionary<string, string>
            {
                { "MATCH_REF", matchRef },
                { "GAME_ID", gameId },
                { "VARIATION", variation },
                { "JACOBY", jacoby.ToString().ToLower() },
                { "ACTION", action },
                { "HOME", homeDir }
            };

            return await Task.Run(() =>
            {
                try
                {
                    string exe = GnubgExe;
                    string script = LibScript;

                    if (!File.Exists(exe))
                        UnityEngine.Debug.LogError($"GNUBG executable not found: {exe}");

                    if (!File.Exists(script))
                        UnityEngine.Debug.LogError($"GNUBG script not found: {script}");

                    var psi = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = $"-q -t -p \"{script}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,

                        // Make GNUBG run inside its StreamingAssets directory
                        WorkingDirectory = Path.Combine(
                            Application.streamingAssetsPath,
                            "gnubg",
                            PlatformFolder
                        )
                    };

                    foreach (var kv in env)
                        psi.Environment[kv.Key] = kv.Value;

                    using (var process = new Process { StartInfo = psi })
                    {
                        process.Start();

                        string stdout = process.StandardOutput.ReadToEnd();
                        string stderr = process.StandardError.ReadToEnd();
                        process.WaitForExit();

                        if (!string.IsNullOrEmpty(stdout))
                            UnityEngine.Debug.Log("[GNUBG stdout]\n" + stdout);

                        if (!string.IsNullOrEmpty(stderr))
                            UnityEngine.Debug.LogWarning("[GNUBG stderr]\n" + stderr);
                    }

                    string jsonPath = Path.Combine(homeDir, $"{matchRef}.json");
                    return File.Exists(jsonPath) ? jsonPath : null;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError("[GNUBG] Error: " + ex.Message);
                    return null;
                }
            });
        }
    }
}
