using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class GnubgBuildProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        string assetName = GnubgInstallLogic.GetAssetName(report.summary.platform);
        
        if (string.IsNullOrEmpty(assetName)) return;

        // Skip if already there to speed up build
        if (Directory.Exists(GnubgInstallLogic.InstallPath) && 
            Directory.GetFiles(GnubgInstallLogic.InstallPath).Length > 0)
        {
            return;
        }

        GnubgInstallLogic.RunInstall(assetName);
    }
}