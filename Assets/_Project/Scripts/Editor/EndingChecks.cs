using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays the ending film for all three endings in the Results scene and photographs every shot. Owner: Samprity.
///   Unity.exe -batchmode -projectPath . -executeMethod EndingChecks.RunBatch      (no -quit: it exits itself)
/// For each ending: the film starts, runs 12-15 s through shots 1-3, then hands over to the results screen with the
/// scene's own camera, lights and canvases back. No errors. Writes Logs/EndingChecks.txt and Logs/Ending_*.png.
/// </summary>
public static class EndingChecks
{
    const string ResultsScenePath = "Assets/_Project/Scenes/Results.unity";
    static readonly Ending[] Order = { Ending.WhatIf, Ending.ManySaved, Ending.CaptainWins };
    static readonly StringBuilder report = new StringBuilder();
    static readonly List<string> errors = new List<string>();
    static bool ok = true;
    static int index, step, lastShot;
    static float started, shotAt;
    static bool shotTaken;
    static EnterPlayModeOptions savedOptions;
    static bool savedEnabled;

    public static void RunBatch()
    {
        report.Clear().Append("[Ending checks]\n");
        errors.Clear();
        ok = true;
        index = 0;
        step = 0;
        savedEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        savedOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.OpenScene(ResultsScenePath, OpenSceneMode.Single);
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void OnLog(string message, string stack, LogType type)
    {
        if (stack != null && stack.Contains("UnityEditor.Search")) return;
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors.Add($"{type}: {message.Split('\n')[0]}");
    }

    static void Check(bool pass, string what)
    {
        ok &= pass;
        report.Append($"  {(pass ? "PASS" : "FAIL")}  {what}\n");
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        float now = Time.realtimeSinceStartup;
        switch (step)
        {
            case 0: // Load Results with this ending recorded
                RunRecord.HasEnding = true;
                RunRecord.Ending = Order[index];
                RunRecord.FinalShipSafety = 60;
                SceneManager.LoadScene("Results");
                started = now;
                lastShot = 0;
                step = 1;
                break;

            case 1: // The film starts
                if (!EndingCinematic.Playing) { if (now - started > 8f) { Check(false, $"{Order[index]}: the film never started"); NextEnding(); } return; }
                Check(true, $"{Order[index]}: the film plays over the results");
                step = 2;
                break;

            case 2: // Photograph each shot partway in; wait for the end
                int shot = EndingCinematic.Shot;
                if (shot != lastShot) { lastShot = shot; shotAt = now; shotTaken = false; }
                if (shot > 0 && !shotTaken && now - shotAt > (shot == 3 ? 3.4f : 2.2f))
                {
                    shotTaken = true;
                    Capture($"{(int)Order[index] + 1}_{Order[index]}_shot{shot}");
                }
                // Headless rendering is slow, so allow plenty of real time; the length itself is measured in film time.
                if (EndingCinematic.Playing) { if (now - started > 150f) { Check(false, $"{Order[index]}: the film never ended"); NextEnding(); } return; }
                float length = EndingCinematic.Elapsed;
                Check(length >= 12.5f && length <= 16f, $"{Order[index]}: runs {length:0.0} s of film (about 13-14 s, then a 1 s fade into the results)");
                var results = Object.FindAnyObjectByType<ResultsScreen>();
                Check(results != null && results.enabled, $"{Order[index]}: the results screen is back and taking input");
                Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Any(c => c.enabled), $"{Order[index]}: the scene's own camera is back");
                Check(GameObject.Find("[EndingStage]") == null, $"{Order[index]}: the stage is cleared away");
                Capture($"{(int)Order[index] + 1}_{Order[index]}_results");
                NextEnding();
                break;
        }
    }

    static void NextEnding()
    {
        index++;
        if (index < Order.Length) { step = 0; return; }
        Check(errors.Count == 0, errors.Count == 0 ? "No errors or exceptions" : $"{errors.Count} errors: " + string.Join(" | ", errors.Distinct().Take(5)));
        Finish();
    }

    /// <summary>The screen as the player sees it, film overlay included, to Logs/Ending_name.png.</summary>
    static void Capture(string name)
    {
        var cam = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c => c.enabled).OrderByDescending(c => c.depth).FirstOrDefault();
        if (cam == null) return;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy).ToList();
        foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + 0.05f; }
        Canvas.ForceUpdateCanvases();
        var rt = new RenderTexture(1280, 720, 24);
        var target = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = target;
        foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        System.IO.Directory.CreateDirectory("Logs");
        System.IO.File.WriteAllBytes($"Logs/Ending_{name}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        report.Append(ok ? "ALL PASSED" : "SOME CHECKS FAILED");
        System.IO.File.WriteAllText("Logs/EndingChecks.txt", report.ToString());
        Debug.Log(report.ToString());
        EditorSettings.enterPlayModeOptionsEnabled = savedEnabled;
        EditorSettings.enterPlayModeOptions = savedOptions;
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
    }
}
