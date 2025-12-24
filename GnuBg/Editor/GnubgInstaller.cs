using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class GitHubRelease
{
    public List<GitHubAsset> assets;
}

[Serializable]
public class GitHubAsset
{
    public string name;
    public string browser_download_url;
}

[InitializeOnLoad]
public static class GnubgInstaller
{
    // ---------------------------------------------------------------------
    // GitHub release configuration
    // ---------------------------------------------------------------------
    private const string GitHubOwner = "reayd-falmouth";
    private const string GitHubRepo  = "gnubg-unity-installer";

    private const string AssetWindows = "gnubg-Windows.zip";
    private const string AssetMac     = "gnubg-macOS.zip";
    private const string AssetLinux   = "gnubg-Linux.zip";

    // ---------------------------------------------------------------------
    // Target: PACKAGE folder (Editor / dev only)
    // ---------------------------------------------------------------------
    private static readonly string PackageBinaryRoot =
        Path.Combine(
            "Packages",
            "gnubg.unity.installer",
            "Runtime",
            "Binaries"
        );

    // ---------------------------------------------------------------------
    // Menu
    // ---------------------------------------------------------------------
    [MenuItem("Tools/GNUBG/Install to Package Folder (Dev Only)")]
    public static void InstallAllPlatforms()
    {
        try
        {
            Directory.CreateDirectory(PackageBinaryRoot);

            InstallPlatform("windows", AssetWindows);
            InstallPlatform("macos",   AssetMac);
            InstallPlatform("linux",   AssetLinux);

            AssetDatabase.Refresh();

            Debug.Log(
                "[GNUBG Installer] ✅ Installed GNUBG binaries into package folder:\n" +
                PackageBinaryRoot
            );
        }
        catch (Exception ex)
        {
            Debug.LogError("[GNUBG Installer] ❌ " + ex);
            EditorUtility.ClearProgressBar();
        }
    }

    // ---------------------------------------------------------------------
    // Core install logic
    // ---------------------------------------------------------------------
    private static void InstallPlatform(string platformFolder, string assetName)
    {
        string targetDir = Path.Combine(PackageBinaryRoot, platformFolder);
        Directory.CreateDirectory(targetDir);

        string releaseJson = FetchLatestReleaseJson();
        string url = FindAssetDownloadUrl(releaseJson, assetName);

        if (string.IsNullOrEmpty(url))
            throw new Exception(
                $"Could not find release asset '{assetName}' in latest release."
            );

        string cacheDir = Path.Combine("Library", "GnubgInstallerCache");
        Directory.CreateDirectory(cacheDir);

        string zipPath = Path.Combine(cacheDir, assetName);
        DownloadFile(url, zipPath, $"Downloading {assetName}");

        // Clean target dir
        foreach (var d in Directory.GetDirectories(targetDir))
            Directory.Delete(d, true);
        foreach (var f in Directory.GetFiles(targetDir))
            File.Delete(f);

        ExtractZip(zipPath, targetDir);
        FlattenIfNestedPlatformFolder(targetDir, platformFolder);
        NormalizeExecutableName(targetDir, platformFolder);

        if (platformFolder != "windows")
            EnsureUnixExecutableBit(Path.Combine(targetDir, "gnubg-cli"));
    }

    // ---------------------------------------------------------------------
    // GitHub helpers
    // ---------------------------------------------------------------------
    private static string FetchLatestReleaseJson()
    {
        string owner = "reayd-falmouth";
        string repo = "gnubg-unity-installer";
        string url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

        Debug.Log($"[GNUBG] Requesting URL: {url}");

        using (var webRequest = UnityWebRequest.Get(url))
        {
            // GitHub API REQUIRES a User-Agent header
            webRequest.SetRequestHeader("User-Agent", "Unity-Gnubg-Installer");
        
            var operation = webRequest.SendWebRequest();
            // Wait for completion (if in Editor) or use async
        
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[GNUBG] Error at {url}: {webRequest.error}");
                return null;
            }
            return webRequest.downloadHandler.text;
        }
    }

    private static string FindAssetDownloadUrl(string releaseJson, string assetName)
    {
        // 1. Parse the JSON string into our C# object structure
        GitHubRelease release = JsonUtility.FromJson<GitHubRelease>(releaseJson);

        // 2. Safety check: ensure assets list isn't null or empty
        if (release?.assets == null)
            return null;

        // 3. Search for the specific asset by name
        foreach (var asset in release.assets)
        {
            if (asset.name == assetName)
            {
                return asset.browser_download_url;
            }
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
            EditorUtility.DisplayProgressBar(
                "GNUBG Installer",
                label,
                op.progress
            );
        }

        EditorUtility.ClearProgressBar();

        if (req.result != UnityWebRequest.Result.Success)
            throw new Exception($"Download failed: {req.error}");

        File.WriteAllBytes(outPath, req.downloadHandler.data);
    }

    // ---------------------------------------------------------------------
    // File helpers
    // ---------------------------------------------------------------------
    private static void ExtractZip(string zipPath, string targetDir)
    {
        ZipFile.ExtractToDirectory(zipPath, targetDir);
    }

    private static void FlattenIfNestedPlatformFolder(
        string targetDir,
        string platformFolder
    )
    {
        string nested = Path.Combine(targetDir, platformFolder);
        if (!Directory.Exists(nested))
            return;

        foreach (var dir in Directory.GetDirectories(nested))
            Directory.Move(dir, Path.Combine(targetDir, Path.GetFileName(dir)));

        foreach (var file in Directory.GetFiles(nested))
            File.Move(file, Path.Combine(targetDir, Path.GetFileName(file)));

        Directory.Delete(nested, true);
    }

    private static void NormalizeExecutableName(
        string targetDir,
        string platformFolder
    )
    {
        if (platformFolder == "windows")
        {
            string cli = Path.Combine(targetDir, "gnubg-cli.exe");
            if (File.Exists(cli))
                return;

            string gnubg = Path.Combine(targetDir, "gnubg.exe");
            if (File.Exists(gnubg))
            {
                File.Move(gnubg, cli);
                return;
            }
        }
        else
        {
            string cli = Path.Combine(targetDir, "gnubg-cli");
            if (File.Exists(cli))
                return;

            string gnubg = Path.Combine(targetDir, "gnubg");
            if (File.Exists(gnubg))
            {
                File.Move(gnubg, cli);
                return;
            }
        }
    }

    private static void EnsureUnixExecutableBit(string path)
    {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
        if (!File.Exists(path))
            return;

        try
        {
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
            Debug.LogWarning("[GNUBG Installer] chmod failed: " + e.Message);
        }
#endif
    }
}
