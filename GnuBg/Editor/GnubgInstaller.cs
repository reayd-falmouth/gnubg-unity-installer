using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
public static class GnubgInstaller
{
    private const string BaseDownloadUrl = "https://github.com/reayd-falmouth/gnubg/releases/download/latest";

    private const string AssetWindows = "gnubg-Windows.zip";
    private const string AssetMac     = "gnubg-macOS.zip";
    private const string AssetLinux   = "gnubg-Linux.zip";

    private static readonly string PackageBinaryRoot =
        Path.Combine("Packages", "gnubg.unity.installer", "Runtime", "Binaries");

    [MenuItem("Tools/GNUBG/Install to Package Folder (Direct Download)")]
    [MenuItem("Tools/GNUBG/Install to Package Folder (Direct Download)")]
    public static void InstallAllPlatforms()
    {
        try
        {
            if (!Directory.Exists(PackageBinaryRoot))
                Directory.CreateDirectory(PackageBinaryRoot);

#if UNITY_EDITOR_WIN
            InstallPlatform("windows", AssetWindows);
#elif UNITY_EDITOR_OSX
        InstallPlatform("macos", AssetMac);
#elif UNITY_EDITOR_LINUX
        InstallPlatform("linux", AssetLinux);
#else
        Debug.LogError("[GNUBG Installer] ❌ Unsupported platform.");
        return;
#endif

            AssetDatabase.Refresh();
            Debug.Log($"[GNUBG Installer] ✅ Installation complete in: {PackageBinaryRoot}");
        }
        catch (Exception ex)
        {
            Debug.LogError("[GNUBG Installer] ❌ " + ex);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void InstallPlatform(string platformFolder, string assetName)
    {
        string url = $"{BaseDownloadUrl}/{assetName}";
        string targetDir = Path.Combine(PackageBinaryRoot, platformFolder);
        
        string cacheDir = Path.Combine("Library", "GnubgInstallerCache");
        if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);
        string zipPath = Path.Combine(cacheDir, assetName);

        Debug.Log($"[GNUBG Installer] Downloading {assetName}...");
        DownloadFile(url, zipPath, $"Downloading {assetName}");

        // Clean target directory
        if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
        Directory.CreateDirectory(targetDir);

        Debug.Log($"[GNUBG Installer] Extracting to {targetDir}");
        ZipFile.ExtractToDirectory(zipPath, targetDir);

        // Standardize folder structure
        FlattenIfNestedPlatformFolder(targetDir, platformFolder);

        // Set permissions for Unix-based systems
        if (platformFolder != "windows")
            EnsureUnixExecutableBit(Path.Combine(targetDir, "gnubg"));
    }

    private static void DownloadFile(string url, string outPath, string label)
    {
        using (var req = UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("User-Agent", "Unity-Gnubg-Installer");
            var op = req.SendWebRequest();

            while (!op.isDone)
            {
                EditorUtility.DisplayProgressBar("GNUBG Installer", label, op.progress);
            }

            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"Failed to download {url}: {req.error}");

            File.WriteAllBytes(outPath, req.downloadHandler.data);
        }
    }

    private static void FlattenIfNestedPlatformFolder(string targetDir, string platformFolder)
    {
        string nested = Path.Combine(targetDir, platformFolder);
        if (!Directory.Exists(nested)) return;

        foreach (var dir in Directory.GetDirectories(nested))
            Directory.Move(dir, Path.Combine(targetDir, Path.GetFileName(dir)));

        foreach (var file in Directory.GetFiles(nested))
            File.Move(file, Path.Combine(targetDir, Path.GetFileName(file)));

        Directory.Delete(nested, true);
    }

    private static void EnsureUnixExecutableBit(string path)
    {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
        if (!File.Exists(path)) return;
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
