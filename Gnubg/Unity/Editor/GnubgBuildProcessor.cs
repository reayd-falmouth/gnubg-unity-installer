using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Networking;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class GnubgBuildProcessor : IPreprocessBuildWithReport
{
    // Execution Order: 0 means it runs early in the build process
    public int callbackOrder => 0;

    private const string BaseDownloadUrl = "https://github.com/reayd-falmouth/gnubg/releases/download/latest";
    private const string AssetWindows = "gnubg-Windows.zip";
    private const string AssetMac     = "gnubg-macOS.zip";
    private const string AssetLinux   = "gnubg-Linux.zip";

    public void OnPreprocessBuild(BuildReport report)
    {
        Debug.Log("[GNUBG Auto-Installer] 🚀 Starting Pre-Build Process...");

        // 1. Determine the asset based on the BUILD TARGET (not the Editor OS)
        string assetName = GetAssetForTarget(report.summary.platform);

        if (string.IsNullOrEmpty(assetName))
        {
            Debug.LogError($"[GNUBG Auto-Installer] ❌ Unsupported Build Target: {report.summary.platform}. GNUBG will not be included.");
            return;
        }

        // 2. Define Paths
        // We strictly use Assets/StreamingAssets so Unity includes it in the build
        string streamingAssetsPath = Path.Combine(Application.dataPath, "StreamingAssets", "gnubg");
        
        // Optional: Check if already installed to save time
        if (IsVersionInstalled(streamingAssetsPath)) 
        {
             Debug.Log("[GNUBG Auto-Installer] ✅ Files detected. Skipping download.");
             return;
        }

        // 3. Perform Install
        InstallPlatform(assetName, streamingAssetsPath);
    }

    private string GetAssetForTarget(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return AssetWindows;
            case BuildTarget.StandaloneOSX:
                return AssetMac;
            case BuildTarget.StandaloneLinux64:
                return AssetLinux;
            default:
                return null;
        }
    }

    private void InstallPlatform(string assetName, string installPath)
    {
        string url = $"{BaseDownloadUrl}/{assetName}";
        
        // Use a temporary cache outside Assets folder to avoid importing temp files
        string tempDir = Path.Combine(Path.GetTempPath(), "GnubgInstallerCache");
        string zipPath = Path.Combine(tempDir, assetName);
        string extractPath = Path.Combine(tempDir, assetName + "_extracted");

        try
        {
            // Clean / Prep Directories
            if (Directory.Exists(installPath)) Directory.Delete(installPath, true);
            Directory.CreateDirectory(installPath);
            
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);
            
            // Download
            Debug.Log($"[GNUBG Auto-Installer] Downloading {assetName} for build...");
            DownloadFileBlocking(url, zipPath);

            // Extract
            Debug.Log($"[GNUBG Auto-Installer] Extracting...");
            ZipFile.ExtractToDirectory(zipPath, extractPath);

            // Flatten and Copy
            string[] subdirs = Directory.GetDirectories(extractPath);
            string sourceDir = (subdirs.Length == 1) ? subdirs[0] : extractPath;

            CopyDirectory(sourceDir, installPath);
            
            // Post-Install: Copy Python Script
            // Note: Adjust "Packages/..." path if your package structure differs
            string sourcePy = Path.GetFullPath("Packages/gnubg.unity.installer/GnuBg/Unity/Runtime/libgnubg.py");
            string targetBin = Path.Combine(installPath, "bin");

            if (File.Exists(sourcePy))
            {
                Directory.CreateDirectory(targetBin);
                File.Copy(sourcePy, Path.Combine(targetBin, "libgnubg.py"), true);
                Debug.Log("[GNUBG Auto-Installer] ✔ Copied libgnubg.py");
            }

            // Post-Install: Permissions (Mac/Linux)
            #if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            string binary = Path.Combine(installPath, "bin", "gnubg");
            if (File.Exists(binary)) EnsureUnixExecutableBit(binary);
            #endif

            Debug.Log($"[GNUBG Auto-Installer] ✅ Install Complete at {installPath}");
        }
        catch (Exception ex)
        {
            throw new BuildFailedException($"[GNUBG Auto-Installer] Failed: {ex.Message}");
        }
        finally
        {
            // Cleanup temp files so they don't clutter drive
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            EditorUtility.ClearProgressBar();
        }
    }

    private void DownloadFileBlocking(string url, string outPath)
    {
        // We use a simple WebClient here because it blocks the thread safely during Build
        // UnityWebRequest requires the main thread update loop which can be flaky in batch mode builds
        using (var client = new System.Net.WebClient())
        {
            client.DownloadFile(url, outPath);
        }
    }

    private bool IsVersionInstalled(string path)
    {
        // Simple check: does the folder exist and have content?
        // You could add a 'version.txt' check here for more robustness
        return Directory.Exists(path) && Directory.GetFiles(path).Length > 0;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDir, file);
            string destFile = Path.Combine(destDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destFile));
            File.Copy(file, destFile, true);
        }
    }

    private static void EnsureUnixExecutableBit(string path)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{path}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit();
        }
        catch (Exception e)
        {
            Debug.LogWarning("Could not set executable bit: " + e.Message);
        }
    }
}