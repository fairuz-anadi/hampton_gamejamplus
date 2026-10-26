using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Windows x64 builds for every milestone. Owner: Anadi. Output goes to Builds/, which git ignores.
///   In the Editor:  Port > Build Windows (Development) / (Release)
///   Headless:       Unity.exe -batchmode -quit -projectPath . -executeMethod BuildWindows.Development [-buildOutput D:/somewhere]
/// Development builds keep the debug keys and panel. Release builds drop them (use this for RC2 and the jam submission).
/// </summary>
public static class BuildWindows
{
    const string ExeName = "ThePromisedPort.exe";

    [MenuItem("Port/Build Windows (Development)")]
    public static void Development() => Build(true);

    [MenuItem("Port/Build Windows (Release)")]
    public static void Release() => Build(false);

    static void Build(bool development)
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        string folder = ArgValue("-buildOutput")
            ?? Path.Combine(Directory.GetCurrentDirectory(), "Builds", development ? "ThePromisedPort_Windows_dev" : "ThePromisedPort_Windows");

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(folder, ExeName),
            target = BuildTarget.StandaloneWindows64,
            options = development ? BuildOptions.Development : BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        bool ok = summary.result == BuildResult.Succeeded;
        string msg = $"[BuildWindows] {summary.result}: {options.locationPathName} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors, {summary.totalTime.TotalSeconds:0}s)";
        if (ok) Debug.Log(msg); else Debug.LogError(msg);

        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        else if (ok) EditorUtility.RevealInFinder(options.locationPathName);
    }

    static string ArgValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
