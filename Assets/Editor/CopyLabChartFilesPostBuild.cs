using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// After a build, copies the LabChart files that LabChartFro looks for next to the running
/// application (project root in the Editor, the .exe's folder in a build) into the build
/// folder: the .adiset/.adicht that R launches LabChart with, and the FRO VBS templates.
/// Without this the build cannot open LabChart or set the FRO outputs.
/// </summary>
public class CopyLabChartFilesPostBuild : IPostprocessBuildWithReport
{
    static readonly string[] Patterns = { "*.adiset", "*.adicht", "DoublePulse.vbs", "SinglePulse.vbs", "NoPulse.vbs" };

    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows &&
            report.summary.platform != BuildTarget.StandaloneWindows64) return;

        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string buildFolder = Path.GetDirectoryName(report.summary.outputPath);

        var copied = new List<string>();
        foreach (string pattern in Patterns)
            foreach (string src in Directory.GetFiles(projectRoot, pattern))
            {
                string name = Path.GetFileName(src);
                File.Copy(src, Path.Combine(buildFolder, name), true);
                copied.Add(name);
            }

        if (copied.Count == 0)
            Debug.LogWarning($"[CopyLabChartFilesPostBuild] No LabChart files found in {projectRoot}");
        else
            Debug.Log($"[CopyLabChartFilesPostBuild] Copied to {buildFolder}: {string.Join(", ", copied)}");
    }
}
