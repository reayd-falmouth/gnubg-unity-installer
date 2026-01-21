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
            // Update permissions for both Mac architectures
            string[] macArchs = { "macOS-ARM64", "macOS-Intel" };
            foreach (string arch in macArchs)
            {
                string binary = Path.Combine(InstallPath, arch, "bin", "gnubg");
                if (File.Exists(binary)) EnsureUnixExecutableBit(binary);
            }
            
            // Also check the root bin just in case (for Linux/Windows)
            string rootBinary = Path.Combine(InstallPath, "bin", "gnubg");
            if (File.Exists(rootBinary)) EnsureUnixExecutableBit(rootBinary);
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
        // 1. Try to find the package path formally via Unity's Package list
        string packagePath = "Packages/gnubg.unity.installer/GnuBg/Unity/Runtime/libgnubg.py";
    
        // 2. If it's not at the hardcoded path, try to find it by name as a fallback
        if (!File.Exists(Path.GetFullPath(packagePath)))
        {
            string[] guids = AssetDatabase.FindAssets("libgnubg");
            foreach (var guid in guids)
            {
                string foundPath = AssetDatabase.GUIDToAssetPath(guid);
                if (foundPath.EndsWith("libgnubg.py"))
                {
                    packagePath = foundPath;
                    break;
                }
            }
        }

        string sourcePy = Path.GetFullPath(packagePath);

        if (File.Exists(sourcePy))
        {
            // Define all possible subfolders that need the script
            string[] subFolders = { "macOS-Intel", "macOS-ARM64", "Windows", "Linux" };

            foreach (string folder in subFolders)
            {
                string platformPath = Path.Combine(InstallPath, folder);
                if (Directory.Exists(platformPath))
                {
                    string targetBin = Path.Combine(platformPath, "bin");
                    if (!Directory.Exists(targetBin)) Directory.CreateDirectory(targetBin);

                    string destination = Path.Combine(targetBin, "libgnubg.py");
                    File.Copy(sourcePy, destination, true);
                    Debug.Log($"[GNUBG] ✔ Bundled libgnubg.py into {folder}/bin/");
                }
            }
        }
        else
        {
            Debug.LogError($"[GNUBG] ❌ Critical Error: Could not find libgnubg.py at {sourcePy}. " +
                           "Check that the file is in your Package's Runtime folder.");
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