using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using UnityEditor;
using UnityEngine;
// This line resolves the ambiguity
using Debug = UnityEngine.Debug;

public static class GnubgInstallLogic
{
    public const string BaseDownloadUrl = "https://github.com/reayd-falmouth/gnubg/releases/download/latest";
    public const string AssetWindows = "gnubg-Windows.zip";
    public const string AssetMac     = "gnubg-macOS.zip";
    public const string AssetLinux   = "gnubg-Linux.zip";

    public static string InstallPath => Path.Combine(Application.dataPath, "StreamingAssets", "gnubg");

    public static string GetAssetName(BuildTarget target)
    {
        return target switch
        {
            BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64 => AssetWindows,
            BuildTarget.StandaloneOSX => AssetMac,
            BuildTarget.StandaloneLinux64 => AssetLinux,
            _ => null
        };
    }

    public static void RunInstall(string assetName)
    {
        string url = $"{BaseDownloadUrl}/{assetName}";
        string tempDir = Path.Combine(Path.GetTempPath(), "GnubgInstallerCache");
        string zipPath = Path.Combine(tempDir, assetName);
        string extractPath = Path.Combine(tempDir, assetName + "_extracted");

        try
        {
            // 1. Cleanup & Prep
            if (Directory.Exists(InstallPath)) Directory.Delete(InstallPath, true);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            
            Directory.CreateDirectory(InstallPath);
            Directory.CreateDirectory(tempDir);

            // 2. Download (Using WebClient for thread-blocking safety during build)
            Debug.Log($"[GNUBG] Downloading {assetName}...");
            using (var client = new WebClient())
            {
                client.DownloadFile(url, zipPath);
            }

            // 3. Extract & Flatten
            ZipFile.ExtractToDirectory(zipPath, extractPath);
            string[] subdirs = Directory.GetDirectories(extractPath);
            string sourceDir = (subdirs.Length == 1) ? subdirs[0] : extractPath;
            CopyDirectory(sourceDir, InstallPath);

            // 4. Copy Python Script
            CopyPythonScript();

            // 5. Permissions
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            string binary = Path.Combine(InstallPath, "bin", "gnubg");
            if (File.Exists(binary)) EnsureUnixExecutableBit(binary);
#endif
            Debug.Log($"[GNUBG] ✅ Installation successful: {InstallPath}");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    private static void CopyPythonScript()
    {
        string[] guids = AssetDatabase.FindAssets("libgnubg t:TextAsset");
        if (guids.Length > 0)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            string sourcePy = Path.GetFullPath(assetPath);
            string targetBin = Path.Combine(InstallPath, "bin");
            
            if (!Directory.Exists(targetBin)) Directory.CreateDirectory(targetBin);
            File.Copy(sourcePy, Path.Combine(targetBin, "libgnubg.py"), true);
        }
        else
        {
            Debug.LogError("[GNUBG] Could not find libgnubg.py in project!");
        }
    }

    public static void CopyDirectory(string sourceDir, string destDir)
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
        var psi = new ProcessStartInfo { FileName = "chmod", Arguments = $"+x \"{path}\"", UseShellExecute = false };
        Process.Start(psi)?.WaitForExit();
    }
}