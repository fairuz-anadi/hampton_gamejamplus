using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays Ship with one keyboard player and checks the Captain's recorded voice is in step with his text boxes.
/// Owner: Samprity. Runs before any real recordings exist: a stand-in tone is registered for his first line.
///   Unity.exe -batchmode -projectPath . -executeMethod NarratorChecks.RunBatch      (no -quit: it exits itself)
/// The recording starts the moment its text box does; the per-letter typing voice is quiet while it plays and comes
/// back after; a line with a typed name finds its "sailor" recording. Writes Logs/NarratorChecks.txt.
/// </summary>
public static class NarratorChecks
{
    static readonly StringBuilder report = new StringBuilder();
    static readonly List<string> errors = new List<string>();
    static bool ok = true, reloaded;
    static int step;
    static float stepStart;
    static string firstPage;
    static EnterPlayModeOptions savedOptions;
    static bool savedEnabled;
    static InputSettings.BackgroundBehavior savedBackground;
    static InputSettings.EditorInputBehaviorInPlayMode savedEditorInput;

    public static void RunBatch()
    {
        report.Clear().Append("[Narrator checks]\n");
        errors.Clear();
        ok = true;
        reloaded = false;
        step = 0;
        firstPage = null;
        lastSpace = -10f;
        releaseFrame = -1;
        savedEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        savedOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        savedBackground = InputSystem.settings.backgroundBehavior;
        savedEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
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
    static void Fail(string why) { Check(false, "Timed out: " + why); Finish(); }

    static bool TypingMuted()
    {
        var captain = CaptainDirector.Instance;
        return captain != null && captain.GetComponents<AudioSource>().All(s => s.mute);
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        float inStep = Time.realtimeSinceStartup - stepStart;
        switch (step)
        {
            case 0: // Join with a keyboard, (re)load Ship, and give the narrator a stand-in for his first line
                if (GameManager.Instance == null) return;
                if (!reloaded)
                {
                    reloaded = true;
                    PlayerSlots.Clear();
                    kb = InputSystem.AddDevice<Keyboard>("NarratorCheckKeyboard");
                    PlayerSlots.Join(kb);
                    SceneManager.LoadScene("Ship");
                    stepStart = Time.realtimeSinceStartup;
                    return;
                }
                if (CaptainDirector.Instance == null || Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length == 0) { if (inStep > 15f) Fail("Ship never started"); return; }
                var narrator = CaptainNarrator.Instance ?? new GameObject("[CaptainNarrator]").AddComponent<CaptainNarrator>();
                // A stand-in tone for every line he can say, the way the real bank has a recording for each.
                var json = System.IO.File.ReadAllText(CaptainVoiceBuilder.Dir + "/lines.json");
                int n = 0;
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(json, "\"key\":\\s*\"((?:[^\"\\\\]|\\\\.)*)\"[^}]*\"variant\":\\s*\"([^\"]*)\""))
                {
                    narrator.Register(System.Text.RegularExpressions.Regex.Unescape(m.Groups[1].Value), Tone(1.6f), m.Groups[2].Value == "*");
                    n++;
                }
                Note($"Stand-in recordings for all {n} exported lines");
                CaptainDirector.PageStarted -= OnPage;
                CaptainDirector.PageStarted += OnPage;
                Next();
                break;

            case 1: // His next text box: the recording plays with it, the typing voice hushes
                IntroCard.Instance?.Dismiss();
                // Space now and then, like a player reading his boxes.
                if (inStep - lastSpace > 3f) { lastSpace = inStep; Space(); }
                ReleaseSpace();
                if (firstPage == null || CaptainNarrator.LastSpoken != firstPage) { if (inStep > 120f) Fail("no text box of his ever played its recording"); return; }
                Note($"Box: \"{firstPage}\"");
                Check(CaptainNarrator.Instance.Speaking, "The recording starts with its text box");
                seaWhileSpeaking = SeaVolume();
                pendingCapture = true;
                Check(TypingMuted(), "The per-letter typing voice is quiet while he speaks");
                Next();
                break;

            case 2: // After the recording: the typing voice comes back
                if (pendingCapture && inStep > 0.8f) { pendingCapture = false; Capture("strip"); } // his text box, with his face
                if (CaptainNarrator.Instance.Speaking)
                {
                    seaWhileSpeaking = Mathf.Min(seaWhileSpeaking, SeaVolume()); // the deepest point of the dip
                    if (inStep > 20f) Fail("the recording never ended");
                    return;
                }
                if (inStep < 2.5f) return; // let the sea come back up
                float seaAfter = SeaVolume();
                Check(seaWhileSpeaking < seaAfter * 0.7f, $"The sea and the ship's sounds drop under his voice ({seaWhileSpeaking:0.00} while he speaks, {seaAfter:0.00} after)");
                bool alwaysMuted = AudioManager.Instance != null && AudioManager.Instance.muteCaptainBlip;
                Check(alwaysMuted || !TypingMuted(), "The typing voice comes back when the recording ends");

                // A line with a name a player typed finds its template ("sailor") recording.
                CaptainNarrator.Instance.Register("{seat} says done. Done is my favourite word.", Tone(0.8f), template: true);
                string typed = "Samprity says done. Done is my favourite word.";
                CaptainNarrator.Say(typed);
                Check(CaptainNarrator.LastSpoken == typed && CaptainNarrator.Instance.Speaking, "A line with a typed name plays its template recording");
                Check(errors.Count == 0, errors.Count == 0 ? "No errors or exceptions" : $"{errors.Count} errors: " + string.Join(" | ", errors.Distinct().Take(5)));
                Finish();
                break;
        }
    }

    static void Note(string what) => report.Append($"  ....  {what}\n");

    static float seaWhileSpeaking;
    static bool pendingCapture;

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
        System.IO.File.WriteAllBytes($"Logs/Narrator_{name}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }

    /// <summary>The sea loop's volume right now (before the player's Effects setting).</summary>
    static float SeaVolume()
    {
        var sea = AudioManager.Instance != null ? AudioManager.Instance.transform.Find("Sea") : null;
        var cat = sea != null ? sea.GetComponent<AudioCategory>() : null;
        return cat != null ? cat.BaseVolume : -1f;
    }

    static float lastSpace = -10f;
    static int releaseFrame = -1;

    /// <summary>The first text box that starts after the stand-ins are in.</summary>
    static void OnPage(string text) { if (firstPage == null && CaptainNarrator.LastSpoken == text) firstPage = text; }

    static Keyboard kb;

    /// <summary>Space down now and held for three game frames, like a real press.</summary>
    static void Space()
    {
        if (kb == null) return;
        // Queued, not forced through now: the game's own frame must see it as pressed this frame.
        InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.Space));
        releaseFrame = Time.frameCount + 3;
    }

    static void ReleaseSpace()
    {
        if (releaseFrame < 0 || Time.frameCount < releaseFrame || kb == null) return;
        InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
        releaseFrame = -1;
    }

    static AudioClip Tone(float seconds)
    {
        const int rate = 22050;
        int n = (int)(rate * seconds);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = 0.1f * Mathf.Sin(2f * Mathf.PI * 180f * i / rate);
        var clip = AudioClip.Create("StandIn", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        report.Append(ok ? "ALL PASSED" : "SOME CHECKS FAILED");
        System.IO.File.WriteAllText("Logs/NarratorChecks.txt", report.ToString());
        Debug.Log(report.ToString());
        EditorSettings.enterPlayModeOptionsEnabled = savedEnabled;
        EditorSettings.enterPlayModeOptions = savedOptions;
        InputSystem.settings.backgroundBehavior = savedBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorInput;
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
    }
}
