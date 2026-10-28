using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays Ship with one keyboard player and checks a player can see what their answers do, and where to go.
/// Owner: Samprity.
///   Unity.exe -batchmode -projectPath . -executeMethod ChoiceChecks.RunBatch      (no -quit: it exits itself)
/// The first order brings a crewmate's hint and a pointer to its station; the first refusal and the first report each
/// stop the game with a card of the meter changes, and Space goes on; the next answer gets the small note. Writes
/// Logs/ChoiceChecks.txt and Logs/Choice_*.png.
/// </summary>
public static class ChoiceChecks
{
    static readonly StringBuilder report = new StringBuilder();
    static readonly List<string> errors = new List<string>();
    static bool ok = true, reloaded;
    static int step;
    static float stepStart, lastSpace;
    static int releaseFrame = -1;
    static Keyboard kb;
    static ActiveOrder order;
    static EnterPlayModeOptions savedOptions;
    static bool savedEnabled;
    static InputSettings.BackgroundBehavior savedBackground;
    static InputSettings.EditorInputBehaviorInPlayMode savedEditorInput;

    public static void RunBatch()
    {
        report.Clear().Append("[Choice checks]\n");
        errors.Clear();
        ok = true;
        reloaded = false;
        step = 0;
        lastSpace = -10f;
        releaseFrame = -1;
        order = seen = null;
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

    static void Note(string what) => report.Append($"  ....  {what}\n");
    static void Next() { step++; stepStart = Time.realtimeSinceStartup; }
    static void Fail(string why) { Check(false, "Timed out: " + why); Finish(); }

    static ICrewActor Player => CrewRegistry.All.FirstOrDefault(c => !c.IsBot);

    static float Alpha(string ui, string child)
    {
        var root = GameObject.Find(ui);
        var t = root != null ? root.transform.Find(child) : null;
        var g = t != null ? t.GetComponent<CanvasGroup>() : null;
        return g != null ? g.alpha : -1f;
    }

    static float PointerAlpha()
    {
        var root = GameObject.Find("[TaskPointer UI]");
        if (root == null) return -1f;
        return root.GetComponentsInChildren<CanvasGroup>().Select(g => g.alpha).DefaultIfEmpty(0f).Max();
    }

    /// <summary>Space now and then, so the Captain's boxes move on and his orders come in.</summary>
    static void ReadOn(float inStep)
    {
        IntroCard.Instance?.Dismiss();
        if (inStep - lastSpace > 2.5f && !ChoiceFeedback.IsShowing) { lastSpace = inStep; Space(); }
        ReleaseSpace();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        float inStep = Time.realtimeSinceStartup - stepStart;
        switch (step)
        {
            case 0: // Join with a keyboard and (re)load Ship
                if (GameManager.Instance == null) return;
                if (!reloaded)
                {
                    reloaded = true;
                    PlayerSlots.Clear();
                    kb = InputSystem.AddDevice<Keyboard>("ChoiceCheckKeyboard");
                    PlayerSlots.Join(kb);
                    SceneManager.LoadScene("Ship");
                    stepStart = Time.realtimeSinceStartup;
                    return;
                }
                if (Player == null || OrderDirector.Instance == null) { if (inStep > 20f) Fail("Ship never started"); return; }
                Check(Object.FindAnyObjectByType<ChoiceFeedback>() != null, "Choice feedback is in Ship by itself");
                Check(Object.FindAnyObjectByType<TaskPointer>() != null, "Task pointer is in Ship by itself");
                Next();
                break;

            case 1: // The player's first order: the crewmate's hint and the pointer
                ReadOn(inStep);
                order ??= OrderDirector.Instance.GetOrderFor(Player);
                if (order == null) { if (inStep > 150f) Fail("no order ever came for the player"); return; }
                if (Alpha("[ChoiceFeedback UI]", "Hint") < 0.95f) { if (inStep > 160f) Fail("the crewmate's hint never showed"); return; }
                Note($"First order: {order.Data.name} at {order.Data.station}");
                Check(true, "A crewmate whispers how to refuse on the first order");
                // Off to the far end of the ship, so the pointer has somewhere to point.
                if (Player is Component pc)
                {
                    var away = StationRegistry.Get(order.Data.station == StationType.Wheel ? StationType.Boiler : StationType.Wheel);
                    if (away != null) Teleport(pc, away.Position + Vector3.right * 1.5f);
                }
                Next();
                break;

            case 2: // A beat for the camera to settle, then the pointer is up
                ReadOn(inStep);
                if (inStep < 1.5f) return;
                Check(PointerAlpha() > 0.9f, $"A pointer shows where the order's station is (alpha {PointerAlpha():0.00})");
                Capture("1_hint_pointer");
                Next();
                break;

            case 3: // Refuse it: the big card, time stopped
                OrderDirector.Instance.Refuse(Player);
                Next();
                break;

            case 4:
                if (!ChoiceFeedback.IsShowing) { if (inStep > 3f) Fail("no card after the first refusal"); return; }
                if (inStep < 0.9f) return;
                Check(Time.timeScale == 0f, "The first refusal stops the game with a card");
                Check(PointerAlpha() < 0.05f, "The pointer steps aside while the card is up");
                Capture("2_card_refuse");
                Space();
                Next();
                break;

            case 5: // Space closes it, and time runs again
                ReleaseSpace();
                if (ChoiceFeedback.IsShowing) { if (inStep > 2f) Fail("Space never closed the card"); return; }
                Check(Time.timeScale > 0f, "Space closes the card and the game runs on");
                order = null;
                Next();
                break;

            case 6: // The next order, reported done: its own first card
                ReadOn(inStep);
                order ??= OrderDirector.Instance.GetOrderFor(Player);
                if (order == null) { if (inStep > 150f) Fail("no second order for the player"); return; }
                if (!Settled()) return;
                OrderDirector.Instance.Resolve(order, Choice.Obey);
                Next();
                break;

            case 7:
                if (!ChoiceFeedback.IsShowing) { if (inStep > 3f) Fail("no card after the first report"); return; }
                if (inStep < 0.9f) return;
                Check(true, "The first report gets its own card");
                Capture("3_card_report");
                Space();
                order = null;
                Next();
                break;

            case 8: // The one after: just the small note, no stop
                ReleaseSpace();
                if (ChoiceFeedback.IsShowing) return;
                ReadOn(inStep);
                order ??= OrderDirector.Instance.GetOrderFor(Player);
                if (order == null) { if (inStep > 150f) Fail("no third order for the player"); return; }
                if (!Settled()) return;
                OrderDirector.Instance.Resolve(order, Choice.Obey);
                Next();
                break;

            case 9:
                if (inStep < 0.6f) return;
                Check(!ChoiceFeedback.IsShowing && Time.timeScale > 0f, "A second report doesn't stop the game");
                Check(Alpha("[ChoiceFeedback UI]", "Toast") > 0.9f, "It shows the small note of meter changes instead");
                Capture("4_toast");
                Check(errors.Count == 0, errors.Count == 0 ? "No errors or exceptions" : $"{errors.Count} errors: " + string.Join(" | ", errors.Distinct().Take(5)));
                Finish();
                break;
        }
    }

    static ActiveOrder seen;
    static float seenAt;

    /// <summary>The order has been up a moment, as it would be before a real player answers it.</summary>
    static bool Settled()
    {
        if (seen != order) { seen = order; seenAt = Time.realtimeSinceStartup; }
        return Time.realtimeSinceStartup - seenAt > 1f;
    }

    static void Teleport(Component c, Vector3 pos)
    {
        var agent = c.GetComponent<UnityEngine.AI.NavMeshAgent>();
        var cc = c.GetComponent<CharacterController>();
        if (agent != null && agent.isOnNavMesh) agent.Warp(pos);
        else
        {
            if (cc != null) cc.enabled = false;
            c.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }
    }

    static void Space()
    {
        if (kb == null) return;
        InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.Space));
        releaseFrame = Time.frameCount + 3;
    }

    static void ReleaseSpace()
    {
        if (releaseFrame < 0 || Time.frameCount < releaseFrame || kb == null) return;
        InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
        releaseFrame = -1;
    }

    static void Capture(string name)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
        // Highest sorting order nearest the camera, as on screen.
        var ordered = canvases.OrderBy(c => c.sortingOrder).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            var c = ordered[i];
            c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam;
            c.planeDistance = cam.nearClipPlane + 0.05f + (ordered.Count - i) * 0.002f;
        }
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
        System.IO.File.WriteAllBytes($"Logs/Choice_{name}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        report.Append(ok ? "ALL PASSED" : "SOME CHECKS FAILED");
        System.IO.File.WriteAllText("Logs/ChoiceChecks.txt", report.ToString());
        Debug.Log(report.ToString());
        EditorSettings.enterPlayModeOptionsEnabled = savedEnabled;
        EditorSettings.enterPlayModeOptions = savedOptions;
        InputSystem.settings.backgroundBehavior = savedBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorInput;
        if (Time.timeScale == 0f) Time.timeScale = 1f;
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
    }
}
