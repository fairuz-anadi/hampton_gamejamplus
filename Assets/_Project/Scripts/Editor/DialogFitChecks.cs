using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Plays Ship with one keyboard player and checks the thought cloud grows to fit a long thought. Owner: Samprity.
///   Unity.exe -batchmode -projectPath . -executeMethod DialogFitChecks.RunBatch      (no -quit: it exits itself)
/// A short thought keeps the cloud its usual size; a very long one makes it wider and taller with every line of the
/// text inside it. Writes Logs/DialogFitChecks.txt and Logs/DialogFit_*.png.
/// </summary>
public static class DialogFitChecks
{
    const string Long = "Wait... the compass says we're turning north, but the wheel says west. And the clock went back an hour. " +
                        "And the arrival time keeps getting later every time I do what he says. Something is very, very wrong on this ship.";
    static readonly StringBuilder report = new StringBuilder();
    static readonly List<string> errors = new List<string>();
    static bool ok = true, reloaded;
    static int step;
    static float stepStart;
    static Vector2 shortSize;
    static EnterPlayModeOptions savedOptions;
    static bool savedEnabled;

    public static void RunBatch()
    {
        report.Clear().Append("[Dialog fit checks]\n");
        errors.Clear();
        ok = true;
        reloaded = false;
        step = 0;
        savedEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        savedOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        EditorSceneManager.OpenScene(OpeningSceneBuilder.ShipScenePath, OpenSceneMode.Single);
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

    static void Next() { step++; stepStart = Time.realtimeSinceStartup; }

    static (RectTransform bubble, Text text) Cloud()
    {
        var canvas = GameObject.Find("[PlayerThought]");
        var bubble = canvas != null ? canvas.transform.Find("Thought") as RectTransform : null;
        return (bubble, bubble != null ? bubble.GetComponentInChildren<Text>() : null);
    }

    /// <summary>Every line of the text fits in its box, and the box fits in the cloud.</summary>
    static bool Fits(RectTransform bubble, Text t)
    {
        float need = t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, t.GetGenerationSettings(new Vector2(t.rectTransform.rect.width, 0f))) / t.pixelsPerUnit;
        return need <= t.rectTransform.rect.height + 1f && t.rectTransform.rect.width <= bubble.rect.width && t.rectTransform.rect.height <= bubble.rect.height;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        float inStep = Time.realtimeSinceStartup - stepStart;
        switch (step)
        {
            case 0:
                if (GameManager.Instance == null) return;
                if (!reloaded)
                {
                    reloaded = true;
                    PlayerSlots.Clear();
                    PlayerSlots.Join(InputSystem.AddDevice<Keyboard>("FitCheckKeyboard"));
                    SceneManager.LoadScene("Ship");
                    stepStart = Time.realtimeSinceStartup;
                    return;
                }
                if (Object.FindAnyObjectByType<PlayerThought>() == null || PartCard.IsShowing) { if (inStep > 40f) { Check(false, "Ship never got going"); Finish(); } return; }
                PlayerThought.Think("Grey Cove?", "fit-short");
                Next();
                break;

            case 1: // A short thought: the usual cloud
                if (inStep < 2.2f) return;
                var (b1, t1) = Cloud();
                shortSize = b1.sizeDelta;
                Check(Fits(b1, t1), $"A short thought fits its cloud ({shortSize.x:0}x{shortSize.y:0})");
                Capture("1_short");
                Next();
                break;

            case 2: // A very long one, once the short one has gone
                if (PlayerThought.Busy) { if (inStep > 15f) { Check(false, "the short thought never cleared"); Finish(); } return; }
                PlayerThought.Think(Long, "fit-long");
                Next();
                break;

            case 3:
                if (inStep < 2.4f) return;
                var (b2, t2) = Cloud();
                Check(t2.text == Long, "The long thought is showing");
                Check(Fits(b2, t2), $"Every line of a long thought stays inside the cloud ({b2.sizeDelta.x:0}x{b2.sizeDelta.y:0})");
                Check(b2.sizeDelta.x > shortSize.x && b2.sizeDelta.y >= shortSize.y, "The cloud grew to fit it");
                Capture("2_long");
                Check(errors.Count == 0, errors.Count == 0 ? "No errors or exceptions" : $"{errors.Count} errors: " + string.Join(" | ", errors.Distinct().Take(5)));
                Finish();
                break;
        }
    }

    static void Capture(string name)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
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
        System.IO.File.WriteAllBytes($"Logs/DialogFit_{name}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        report.Append(ok ? "ALL PASSED" : "SOME CHECKS FAILED");
        System.IO.File.WriteAllText("Logs/DialogFitChecks.txt", report.ToString());
        Debug.Log(report.ToString());
        EditorSettings.enterPlayModeOptionsEnabled = savedEnabled;
        EditorSettings.enterPlayModeOptions = savedOptions;
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
    }
}
