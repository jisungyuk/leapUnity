// Editor-only build helpers to bypass Editor UI popups during Build & Run
// Adds menu items under Build/ in the Unity Editor.
// Placed outside an Editor folder due to environment constraints; fully guarded by UNITY_EDITOR.

#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildTools
{
    const string BuildRoot = "Builds/macOS";

    [MenuItem("Build/Build macOS")]
    public static void BuildMacOS()
    {
        Build(false);
    }

    [MenuItem("Build/Build & Run macOS")]
    public static void BuildAndRunMacOS()
    {
        Build(true);
    }

    static void Build(bool runAfter)
    {
        // Collect scenes enabled in Build Settings
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("Build", "No scenes are enabled in Build Settings.", "OK");
            return;
        }

        string product = PlayerSettings.productName;
        string buildDir = Path.Combine(BuildRoot, product + ".app");

        // Ensure parent directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(buildDir));

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildDir,
            target = BuildTarget.StandaloneOSX,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(opts);
        if (report.summary.result != BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog(
                "Build Failed",
                $"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}",
                "OK");
            return;
        }

        Debug.Log($"[BuildTools] Build succeeded → {buildDir}");

        if (runAfter)
        {
            // Launch the app using macOS 'open' to avoid path issues
            var p = new System.Diagnostics.Process();
            p.StartInfo.FileName = "/usr/bin/open";
            p.StartInfo.Arguments = $"\"{buildDir}\"";
            p.StartInfo.UseShellExecute = false;
            try { p.Start(); }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Run Failed",
                    "Built, but failed to launch app:\n" + ex.Message, "OK");
            }
        }
        else
        {
            EditorUtility.DisplayDialog("Build Succeeded", buildDir, "OK");
        }
    }
}
#endif

