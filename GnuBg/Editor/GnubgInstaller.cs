using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

    [InitializeOnLoad]
    public static class GnubgInstaller
    {
        // ---- Configure these to point at your GNUBG build repo ----
        private const string GitHubOwner = "reayd-falmouth";
        private const string GitHubRepo  = "gnubg-unity-installer";

        // Asset names you attach to the GitHub Release:
        private const string AssetWindows = "gnubg-Windows.zip";
        private const string AssetMac     = "gnubg-macOS.zip";
        private const string AssetLinux   = "gnubg-Linux.zip";

        // Where your runtime code expects binaries:
        // Application.streamingAssetsPath/gnubg/<platform>/gnubg-cli(.exe)  :contentReference[oaicite:4]{index=4}
        private static readonly string StreamingRoot =
            Path.Combine(Application.dataPath, "StreamingAssets", "gnubg");

        private const string PrefKeyInstalled = "com.reayd-falmouth.gnubg.installer.installed";

        static GnubgInstaller()
        {
            // Auto-install once per project (can be re-run from menu)
            EditorApplication.delayCall += () =>
            {
                if (!EditorPrefs.GetBool(PrefKeyInstalled, false))
                    InstallAllPlatforms();
            };
        }

        [MenuItem("Tools/GNUBG/Install or Update Binaries")]
        public static void InstallAllPlatforms()
        {
            try
            {
                Directory.CreateDirectory(StreamingRoot);

                // Download sequentially so we can show progress clearly.
                InstallPlatform("windows", AssetWindows);
                InstallPlatform("macos",   AssetMac);
                InstallPlatform("linux",   AssetLinux);

                AssetDatabase.Refresh();
                EditorPrefs.SetBool(PrefKeyInstalled, true);
                UnityEngine.Debug.Log("[GNUBG Installer] ✅ Installed binaries into Assets/StreamingAssets/gnubg");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[GNUBG Installer] ❌ " + ex);
                EditorUtility.ClearProgressBar();
            }
        }

        private static void InstallPlatform(string platformFolder, string assetName)
        {
            string targetDir = Path.Combine(StreamingRoot, platformFolder);
            Directory.CreateDirectory(targetDir);

            // If already present, you can skip. Comment this out if you always want updates.
            // if (File.Exists(Path.Combine(targetDir, platformFolder == "windows" ? "gnubg-cli.exe" : "gnubg-cli")))
            //     return;

            var release = FetchLatestReleaseJson();
            var url = FindAssetDownloadUrl(release, assetName);
            if (string.IsNullOrEmpty(url))
                throw new Exception($"Could not find release asset '{assetName}' in latest release.");

            string cacheDir = Path.Combine("Library", "GnubgInstallerCache");
            Directory.CreateDirectory(cacheDir);

            string zipPath = Path.Combine(cacheDir, assetName);
            DownloadFile(url, zipPath, $"Downloading {assetName}");

            // Clean target and extract
            if (Directory.Exists(targetDir))
            {
                // Keep folder but clear contents
                foreach (var d in Directory.GetDirectories(targetDir)) Directory.Delete(d, true);
                foreach (var f in Directory.GetFiles(targetDir)) File.Delete(f);
            }

            ExtractZip(zipPath, targetDir);

            // If your zip contains "windows/..." inside it, flatten one level
            FlattenIfNestedPlatformFolder(targetDir, platformFolder);

            // Normalize executable name to what your runtime expects. :contentReference[oaicite:5]{index=5}
            NormalizeExecutableName(targetDir, platformFolder);

            // Ensure executable bit on mac/linux
            if (platformFolder != "windows")
                EnsureUnixExecutableBit(Path.Combine(targetDir, "gnubg-cli"));

            // Optional: you can drop your libgnubg.py here as well if you want it bundled per platform.
            // Your runtime expects it at: .../StreamingAssets/gnubg/<platform>/libgnubg.py  :contentReference[oaicite:6]{index=6}
        }

        private static string FetchLatestReleaseJson()
        {
            string api = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
            using var req = UnityWebRequest.Get(api);

            // GitHub requires a User-Agent header for API requests.
            req.SetRequestHeader("User-Agent", "Unity-GNUBG-Installer");

            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                EditorUtility.DisplayProgressBar("GNUBG Installer", "Fetching latest release metadata...", op.progress);
            }

            EditorUtility.ClearProgressBar();

            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"GitHub API request failed: {req.error}\n{req.downloadHandler.text}");

            return req.downloadHandler.text;
        }

        private static string FindAssetDownloadUrl(string releaseJson, string assetName)
        {
            // Parse GitHub release JSON, locate assets[].name == assetName, return browser_download_url
            // GitHub docs: release assets include browser_download_url for direct download. :contentReference[oaicite:7]{index=7}
            var root = MiniJson.Deserialize(releaseJson) as Dictionary<string, object>;
            if (root == null || !root.TryGetValue("assets", out var assetsObj)) return null;

            var assets = assetsObj as List<object>;
            if (assets == null) return null;

            foreach (var a in assets)
            {
                if (a is not Dictionary<string, object> ad) continue;
                if (!ad.TryGetValue("name", out var n) || (n?.ToString() != assetName)) continue;
                if (ad.TryGetValue("browser_download_url", out var url))
                    return url?.ToString();
            }

            return null;
        }

        private static void DownloadFile(string url, string outPath, string label)
        {
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("User-Agent", "Unity-GNUBG-Installer");

            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                EditorUtility.DisplayProgressBar("GNUBG Installer", label, op.progress);
            }

            EditorUtility.ClearProgressBar();

            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"Download failed: {req.error}");

            File.WriteAllBytes(outPath, req.downloadHandler.data);
        }

        private static void ExtractZip(string zipPath, string targetDir)
        {
            // ZipFile requires .NET 4.x scripting runtime (common in modern Unity)
            ZipFile.ExtractToDirectory(zipPath, targetDir);
        }

        private static void FlattenIfNestedPlatformFolder(string targetDir, string platformFolder)
        {
            // If the zip already contains a top-level folder named the platform,
            // move its contents up one level.
            string nested = Path.Combine(targetDir, platformFolder);
            if (!Directory.Exists(nested)) return;

            foreach (var dir in Directory.GetDirectories(nested))
            {
                string name = Path.GetFileName(dir);
                Directory.Move(dir, Path.Combine(targetDir, name));
            }

            foreach (var file in Directory.GetFiles(nested))
            {
                string name = Path.GetFileName(file);
                File.Move(file, Path.Combine(targetDir, name));
            }

            Directory.Delete(nested, true);
        }

        private static void NormalizeExecutableName(string targetDir, string platformFolder)
        {
            if (platformFolder == "windows")
            {
                string cli = Path.Combine(targetDir, "gnubg-cli.exe");
                if (File.Exists(cli)) return;

                string gnubg = Path.Combine(targetDir, "gnubg.exe");
                if (File.Exists(gnubg))
                {
                    File.Move(gnubg, cli);
                    return;
                }

                // Fallback: maybe it was under bin/
                string binGnubg = Path.Combine(targetDir, "bin", "gnubg.exe");
                if (File.Exists(binGnubg))
                {
                    File.Move(binGnubg, cli);
                }
            }
            else
            {
                string cli = Path.Combine(targetDir, "gnubg-cli");
                if (File.Exists(cli)) return;

                string gnubg = Path.Combine(targetDir, "gnubg");
                if (File.Exists(gnubg))
                {
                    File.Move(gnubg, cli);
                    return;
                }

                string binGnubg = Path.Combine(targetDir, "bin", "gnubg");
                if (File.Exists(binGnubg))
                {
                    File.Move(binGnubg, cli);
                }
            }
        }

        private static void EnsureUnixExecutableBit(string path)
        {
            if (!File.Exists(path)) return;

#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            try
            {
                // chmod +x <path>
                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"+x \"{path}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                p?.WaitForExit();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[GNUBG Installer] chmod failed: " + e.Message);
            }
#endif
        }
    }

