using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays Ship with one keyboard player and checks the Captain's bottle clue end to end. Owner: Samprity.
///   Unity.exe -batchmode -projectPath . -executeMethod ClueChecks.RunBatch      (no -quit: it exits itself)
/// Part 2 puts Anadi's bottle (ShipClues) on the bridge cabinet, dressed as the dark green, empty, sealed bottle;
/// the player walks up and presses E: the camera eases in and the note says it was never full; E again puts it
/// down and the camera goes back to the player. Writes Logs/ClueChecks.txt and Logs/Clue_*.png.
/// </summary>
public static class ClueChecks
{
    static readonly StringBuilder report = new StringBuilder();
    static readonly List<string> errors = new List<string>();
    static bool ok = true, reloaded;
    static int step;
    static float stepStart;
    static Keyboard one;
    static Transform bottle;
    static float farDistance;
    static EnterPlayModeOptions savedOptions;
    static bool savedEnabled;
    static InputSettings.BackgroundBehavior savedBackground;
    static InputSettings.EditorInputBehaviorInPlayMode savedEditorInput;

    public static void RunBatch()
    {
        report.Clear().Append("[Clue checks]\n");
        errors.Clear();
        ok = true;
        reloaded = false;
        step = 0;
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

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        ReleaseIfDue();
        float inStep = Time.realtimeSinceStartup - stepStart;
        var gm = GameManager.Instance;
        switch (step)
        {
            case 0: // Join with a keyboard, then (re)load Ship
                if (gm == null) return;
                if (!reloaded)
                {
                    reloaded = true;
                    one = InputSystem.AddDevice<Keyboard>("ClueCheckKeyboard");
                    PlayerSlots.Clear();
                    PlayerSlots.Join(one);
                    SceneManager.LoadScene("Ship");
                    stepStart = Time.realtimeSinceStartup;
                    return;
                }
                if (Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length == 0) { if (inStep > 15f) Fail("no player joined"); return; }
                Next();
                break;

            case 1: // On to Part 2, where the bottle comes out
                if (inStep < 1.5f || PartCard.IsShowing) { if (inStep > 40f) Fail("the part card never cleared"); return; }
                IntroCard.Instance?.Dismiss();
                gm.DebugSkipPart();
                Next();
                break;

            case 2: // Anadi's bottle on the bridge cabinet, dressed as the Captain's; the player walks up to it
            {
                var go = GameObject.Find("CaptainsBottle");
                if (gm.State != GameState.Part2 || PartCard.IsShowing || go == null) { if (inStep > 90f) Fail("the bottle never came out in Part 2"); return; }
                bottle = go.transform;
                Check(bottle.Find("BottleLook") != null, "Part 2: the Captain's bottle is on the bridge, a dark green bottle, empty, still sealed");
                var player = Object.FindAnyObjectByType<PlayerController>();
                var spot = UnityEngine.AI.NavMesh.SamplePosition(bottle.position + Vector3.left * 1.2f, out var hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas) ? hit.position : bottle.position + Vector3.left * 1.2f;
                var yaw = Quaternion.LookRotation(new Vector3(bottle.position.x - spot.x, 0f, bottle.position.z - spot.z)).eulerAngles.y;
                player.GetComponent<ThirdPersonMover>().Teleport(spot, yaw);
                Camera.main.GetComponent<AutoFollowCamera>()?.SnapToTarget();
                Next();
                break;
            }

            case 3: // E on it, the way a player would
                if (inStep < 1.2f) return;
                farDistance = Vector3.Distance(Camera.main.transform.position, bottle.position);
                Capture("1_before");
                Press();
                Next();
                break;

            case 4:
                if (inStep < 1.8f) return;
                float near = Vector3.Distance(Camera.main.transform.position, bottle.position);
                Check(ClueCloseUp.IsOpen, "E on the bottle opens the close-up");
                Check(near < 1.4f && near < farDistance * 0.6f, $"The camera eases in on the bottle ({farDistance:0.0} m to {near:0.00} m)");
                Capture("2_closeup");
                Press();
                Next();
                break;

            case 5: // E again puts it down; the camera goes back to the player
                if (inStep < 1.8f) return;
                float back = Vector3.Distance(Camera.main.transform.position, bottle.position);
                Check(!ClueCloseUp.IsOpen, "E again puts the bottle down (and doesn't pick it straight back up)");
                Check(back > 1.5f, $"The camera goes back to the player ({back:0.0} m from the bottle)");
                Capture("3_after");
                Check(errors.Count == 0, errors.Count == 0 ? "No errors or exceptions" : $"{errors.Count} errors: " + string.Join(" | ", errors.Distinct().Take(5)));
                Finish();
                break;
        }
    }

    static int releaseAfterFrame = -1;

    /// <summary>E down now and held for three game frames, like a real press (headless frames can be slow).</summary>
    static void Press()
    {
        InputSystem.QueueStateEvent(one, new KeyboardState(Key.E));
        InputSystem.Update();
        releaseAfterFrame = Time.frameCount + 3;
    }

    static void ReleaseIfDue()
    {
        if (releaseAfterFrame < 0 || Time.frameCount < releaseAfterFrame || one == null) return;
        InputSystem.QueueStateEvent(one, new KeyboardState());
        releaseAfterFrame = -1;
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
        System.IO.File.WriteAllBytes($"Logs/Clue_{name}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        report.Append(ok ? "ALL PASSED" : "SOME CHECKS FAILED");
        System.IO.File.WriteAllText("Logs/ClueChecks.txt", report.ToString());
        Debug.Log(report.ToString());
        EditorSettings.enterPlayModeOptionsEnabled = savedEnabled;
        EditorSettings.enterPlayModeOptions = savedOptions;
        InputSystem.settings.backgroundBehavior = savedBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorInput;
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
    }
}
