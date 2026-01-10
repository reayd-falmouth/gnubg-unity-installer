using UnityEditor;

public static class GnubgInstaller
{
    [MenuItem("Tools/GNUBG/Install (Current Platform Only)")]
    public static void InstallCurrentPlatform()
    {
        // Map current Editor platform to a BuildTarget
#if UNITY_EDITOR_WIN
        BuildTarget target = BuildTarget.StandaloneWindows64;
#elif UNITY_EDITOR_OSX
        BuildTarget target = BuildTarget.StandaloneOSX;
#else
        BuildTarget target = BuildTarget.StandaloneLinux64;
#endif
        
        string asset = GnubgInstallLogic.GetAssetName(target);
        GnubgInstallLogic.RunInstall(asset);
        AssetDatabase.Refresh();
    }
}