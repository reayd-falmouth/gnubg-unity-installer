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

    private static readonly string BundledScriptPath =
        "Packages/gnubg.unity.installer/GnuBg/Runtime/libgnubg.py";

    [MenuItem("Tools/GNUBG/Install to Package Folder (Direct Download)")]
    public static void InstallAllPlatforms()
    {
        try
        {
            InstallPlatform("windows", AssetWindows);
            InstallPlatform("macos",   AssetMac);
            InstallPlatform("linux",   AssetLinux);

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
        Directory.CreateDirectory(cacheDir);
        string zipPath = Path.Combine(cacheDir, assetName);

        Debug.Log($"[GNUBG Installer] Downloading {assetName}...");
        DownloadFile(url, zipPath, $"Downloading {assetName}");

        // Clean and extract
        if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
        Directory.CreateDirectory(targetDir);

        string tempExtractDir = Path.Combine(cacheDir, platformFolder + "_extracted");
        if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true);
        ZipFile.ExtractToDirectory(zipPath, tempExtractDir);

        FlattenExtractedStructure(tempExtractDir, targetDir);
        NormalizeExecutableName(targetDir, platformFolder);
        CopyLibgnubg(targetDir);

        if (platformFolder != "windows")
            EnsureUnixExecutableBit(Path.Combine(targetDir, "gnubg-cli"));
    }

    private static void FlattenExtractedStructure(string sourceDir, string targetDir)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(sourceDir, file);
            string dest = Path.Combine(targetDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Move(file, dest, overwrite: true);
        }
    }

    private static void NormalizeExecutableName(string targetDir, string platformFolder)
    {
        string ext = platformFolder == "windows" ? ".exe" : "";
        string cliPath = Path.Combine(targetDir, "gnubg-cli" + ext);
        if (File.Exists(cliPath)) return;

        // Check known fallbacks
        string[] candidates = {
            Path.Combine(targetDir, "bin", "gnubg" + ext),
            Path.Combine(targetDir, "gnubg" + ext)
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                File.Move(path, cliPath);
                Debug.Log($"[GNUBG Installer] Renamed {path} → {cliPath}");
                return;
            }
        }

        Debug.LogWarning($"[GNUBG Installer] Could not find GNUBG executable to normalize.");
    }

    private static void CopyLibgnubg(string targetDir)
    {
        string source = Path.GetFullPath(BundledScriptPath);
        string dest = Path.Combine(targetDir, "libgnubg.py");

        if (!File.Exists(source))
        {
            Debug.LogWarning($"[GNUBG Installer] libgnubg.py not found at: {source}");
            return;
        }

        File.Copy(source, dest, overwrite: true);
        Debug.Log($"[GNUBG Installer] Copied libgnubg.py to {dest}");
    }

    private static void DownloadFile(string url, string outPath, string label)
    {
        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("User-Agent", "Unity-Gnubg-Installer");

        var op = req.SendWebRequest();
        while (!op.isDone)
        {
            EditorUtility.DisplayProgressBar("GNUBG Installer", label, op.progress);
        }

        if (req.result != UnityWebRequest.Result.Success)
            throw new Exception($"Download failed: {url}\nError: {req.error}");

        File.WriteAllBytes(outPath, req.downloadHandler.data);
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
