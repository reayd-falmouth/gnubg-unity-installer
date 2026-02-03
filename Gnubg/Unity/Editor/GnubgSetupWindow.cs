using UnityEditor;
using UnityEngine;

namespace gnubg_unity_installer.Gnubg.Unity.Editor
{
    public class GnubgSetupWindow : EditorWindow
    {
        [InitializeOnLoadMethod]
        private static void OpenOnFirstRun()
        {
            // Only open if not installed and we haven't nagged them yet
            bool isInstalled = System.IO.Directory.Exists(GnubgInstallLogic.InstallPath);
            if (!isInstalled && !SessionState.GetBool("GnubgSetupShown", false))
            {
                SessionState.SetBool("GnubgSetupShown", true);
                GetWindow<GnubgSetupWindow>("GNUBG Setup");
            }
        }

        [MenuItem("Tools/GNUBG/Setup Wizard")]
        public static void ShowWindow() => GetWindow<GnubgSetupWindow>("GNUBG Setup");

        private void OnGUI()
        {
            GUILayout.Label("GNUBG AI Integration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This asset requires the GNU Backgammon (GNUBG) binaries to function.\n" +
                "Due to GPL licensing, these must be downloaded separately.", 
                MessageType.Info);

            if (GUILayout.Button("Download & Install GNUBG (Requires Internet)"))
            {
                GnubgInstaller.InstallCurrentPlatform();
            }

            if (GUILayout.Button("Test Installation"))
            {
                GnubgPythonBridgeTest.RunBridgeSmokeTest();
            }
        }
    }
}