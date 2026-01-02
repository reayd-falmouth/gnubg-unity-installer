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
    private const string AssetMacIntel   = "gnubg-macOS-Intel.zip";
    private const string AssetMacARM     = "gnubg-macOS-ARM64.zip";
    private const string AssetLinux   = "gnubg-Linux.zip";

    private static readonly string InstallPath =
        Path.Combine(Application.dataPath, "StreamingAssets", "gnubg");

    [MenuItem("Tools/GNUBG/Install (Current Platform Only)")]
    [MenuItem("Tools/GNUBG/Install (Current Platform Only)")]
    public static void InstallCurrentPlatform()
    {
        try
        {
            string platform;
            string asset;

#if UNITY_EDITOR_WIN
            platform = "windows";
            asset = AssetWindows;
#elif UNITY_EDITOR_OSX
            platform = "macos";
            // MINIMAL UPDATE: Detect architecture to pick the correct Mac asset
            asset = IsRunningOnAppleSilicon() ? AssetMacARM : AssetMacIntel;
#elif UNITY_EDITOR_LINUX
            platform = "linux";
            asset = AssetLinux;
#else
            Debug.LogError("[GNUBG Installer] ❌ Unsupported platform.");
            return;
#endif

            InstallPlatform(platform, asset);
            AssetDatabase.Refresh();
            Debug.Log($"[GNUBG Installer] ✅ Installed for platform: {platform} ({asset})");
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
        string cacheDir = Path.Combine("Library", "GnubgInstallerCache");
        string tempExtractDir = Path.Combine(cacheDir, platformFolder + "_extract");

        string targetDir = InstallPath;
        Directory.CreateDirectory(cacheDir);
        if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
        Directory.CreateDirectory(targetDir);
        if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true);
        Directory.CreateDirectory(tempExtractDir);

        string zipPath = Path.Combine(cacheDir, assetName);

        Debug.Log($"[GNUBG Installer] Downloading {assetName}...");
        DownloadFile(url, zipPath, $"Downloading {assetName}");

        Debug.Log($"[GNUBG Installer] Extracting to temp dir: {tempExtractDir}");
        ZipFile.ExtractToDirectory(zipPath, tempExtractDir);

        // Flatten nested gnubg-Windows or gnubg-Linux folders
        string[] subdirs = Directory.GetDirectories(tempExtractDir);
        if (subdirs.Length == 1 && Directory.Exists(subdirs[0]))
        {
            Debug.Log($"[GNUBG Installer] Flattening top-level folder: {Path.GetFileName(subdirs[0])}");
            CopyDirectory(subdirs[0], targetDir);
        }
        else
        {
            CopyDirectory(tempExtractDir, targetDir);
        }
        
        // Copy libgnubg.py from package Runtime folder into bin/
        string sourcePy = Path.Combine("Packages", "gnubg.unity.installer", "GnuBg", "Unity", "Runtime", "libgnubg.py");
        string targetBin = Path.Combine(targetDir, "bin");

        if (File.Exists(sourcePy))
        {
            Directory.CreateDirectory(targetBin); // ensure bin/ exists
            string destPy = Path.Combine(targetBin, "libgnubg.py");
            File.Copy(sourcePy, destPy, overwrite: true);
            Debug.Log("[GNUBG Installer] ✔ Copied libgnubg.py into bin/");
        }
        else
        {
            Debug.LogWarning("[GNUBG Installer] ⚠ libgnubg.py not found in package Runtime folder");
        }


#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
        string binary = Path.Combine(targetDir, "bin", "gnubg");
        if (File.Exists(binary)) EnsureUnixExecutableBit(binary);
#endif
    }

    private static void DownloadFile(string url, string outPath, string label)
    {
        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("User-Agent", "Unity-Gnubg-Installer");

        var op = req.SendWebRequest();
        while (!op.isDone)
            EditorUtility.DisplayProgressBar("GNUBG Installer", label, op.progress);

        if (req.result != UnityWebRequest.Result.Success)
            throw new Exception($"Download failed: {req.error}");

        File.WriteAllBytes(outPath, req.downloadHandler.data);
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        foreach (string dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            string target = dir.Replace(sourceDir, destDir);
            Directory.CreateDirectory(target);
        }

        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string target = file.Replace(sourceDir, destDir);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void EnsureUnixExecutableBit(string path)
    {
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
    }
    
    private static bool IsRunningOnAppleSilicon()
    {
        // Check the process architecture of the running Unity Editor
        return RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    }
}
