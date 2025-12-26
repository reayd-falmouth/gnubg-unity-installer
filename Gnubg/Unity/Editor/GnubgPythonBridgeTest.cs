using System;
using System.IO;
using System.Threading.Tasks;
using Gnubg.Unity.Runtime.Bridge;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only integration test for the GNUBG → Python bridge.
/// This validates that:
///  - GNUBG executable is callable
///  - libgnubg.py is executed
///  - JSON output is produced and readable
///
/// This is NOT a unit test; it is a diagnostic smoke test.
/// </summary>
public static class GnubgPythonBridgeTest
{
    [MenuItem("Tools/GNUBG/Test Python Bridge")]
    public static async void RunBridgeSmokeTest()
    {
        Debug.Log("[GNUBG TEST] Starting Python bridge smoke test…");

        // Use deterministic identifiers so reruns overwrite cleanly
        string matchRef = "unity_test_match";
        string gameId   = "AEAAAAAAAgAAAA:cAluAAAAAAAA";

        try
        {
            string jsonPath = await GnubgPythonBridge.RunAsync(
                matchRef: matchRef,
                gameId: gameId,
                variation: "standard",
                jacoby: false,
                action: "hint"
            );

            if (string.IsNullOrEmpty(jsonPath))
            {
                Debug.LogError("[GNUBG TEST] ❌ Bridge returned null or empty path");
                return;
            }

            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"[GNUBG TEST] ❌ JSON file not found at: {jsonPath}");
                return;
            }

            string json = File.ReadAllText(jsonPath);

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogError("[GNUBG TEST] ❌ JSON output is empty");
                return;
            }

            // Basic structural validation (not schema validation)
            try
            {
                JsonUtility.FromJson<DummyJsonCheck>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError("[GNUBG TEST] ❌ JSON parsing failed\n" + ex);
                return;
            }

            Debug.Log(
                "[GNUBG TEST] ✅ SUCCESS\n" +
                $"JSON path: {jsonPath}\n" +
                $"Bytes: {json.Length}"
            );
        }
        catch (Exception ex)
        {
            Debug.LogError("[GNUBG TEST] ❌ Exception thrown\n" + ex);
        }
    }

    /// <summary>
    /// Dummy type used only to assert JSON is syntactically valid.
    /// We do not care about fields here.
    /// </summary>
    [Serializable]
    private class DummyJsonCheck { }
}
