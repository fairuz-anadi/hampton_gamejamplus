using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Windows x64 builds for every milestone. Owner: Anadi. Output goes to Builds/, which git ignores.
///   In the Editor:  Port > Build Windows (Development) / (Release)
///   Headless:       Unity.exe -batchmode -quit -projectPath . -executeMethod BuildWindows.Development [-buildOutput D:/somewhere]
/// Development builds keep the debug keys and panel. Release builds drop them, add README.txt and pack
/// ThePromisedPort_Windows_v1.0.zip next to the build folder (use this for RC1, RC2 and the jam submission).
/// </summary>
public static class BuildWindows
{
    public const string Version = "1.0";
    const string ExeName = "ThePromisedPort.exe";
    const string ZipFolderName = "ThePromisedPort";
    const string DontShipSuffix = "_ButDontShipItWithYourGame";

    const string ReadmeText =
@"THE PROMISED PORT  v{0}
GameJamPlus 2026/27 entry by Anadi, Rabib, Riham and Samprity (GJ+ Bangladesh).

HOW TO RUN
Unzip, then double-click ThePromisedPort.exe. Windows 10/11, 64-bit. Unity isn't needed.

PLAYERS
1-3 players on one screen. Bots fill the rest of the 4 crew seats, so it plays well solo.
On the main menu: Space (keyboard) or A (gamepad) takes a seat, Enter or Start sets sail.
A run takes about 9 minutes.

CONTROLS              Keyboard    Gamepad
Move                  W A S D     Left stick
Interact (hold)       E           A
Report done           R           Y
Refuse                X           B
Call for help         Q           X
Muster vote (bell)    F           RB
Pause                 Esc         Start

Follow the Captain's orders. For each one you can obey, fake it, or refuse.
Watch the station lights and the three meters.
";

    [MenuItem("Port/Build Windows (Development)")]
    public static void Development() => Build(true);

    [MenuItem("Port/Build Windows (Release)")]
    public static void Release() => Build(false);

    static void Build(bool development)
    {
        PlayerSettings.bundleVersion = Version;
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

        string zip = null;
        if (ok && !development)
        {
            File.WriteAllText(Path.Combine(folder, "README.txt"), string.Format(ReadmeText, Version));
            zip = Pack(folder);
            Debug.Log($"[BuildWindows] Packed {zip}");
        }

        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        else if (ok) EditorUtility.RevealInFinder(zip ?? options.locationPathName);
    }

    /// <summary>Zips the build folder into ThePromisedPort_Windows_vX.zip beside it, skipping the Burst debug folder.</summary>
    static string Pack(string folder)
    {
        string zipPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(folder)), $"ThePromisedPort_Windows_v{Version}.zip");
        if (File.Exists(zipPath)) File.Delete(zipPath);

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(folder, file).Replace('\\', '/');
                if (rel.Split('/')[0].EndsWith(DontShipSuffix)) continue;
                archive.CreateEntryFromFile(file, $"{ZipFolderName}/{rel}", System.IO.Compression.CompressionLevel.Optimal);
            }
        }
        return zipPath;
    }

    static string ArgValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
