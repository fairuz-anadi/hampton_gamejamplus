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
/// Plays the iceberg finale headless and checks how it's presented. Owner: Samprity.
///   Unity.exe -batchmode -projectPath . -executeMethod IcebergChecks.RunBatch      (no -quit: it exits itself)
/// A virtual keyboard joins, the player is put at the Wheel facing the bow, the finale starts. Checks: the real
/// iceberg replaces the placeholder cube and the debug bar, it closes on the bow, holding E with D steers it off,
/// a second player joining switches to the shared bow view, and the run still ends in Results.
/// Writes Logs/IcebergChecks.txt and Logs/Iceberg_*.png.
/// </summary>
public static class IcebergChecks
{
    static readonly StringBuilder report = new StringBuilder();
    static readonly List<string> errors = new List<string>();
    static bool ok = true;
    static int step;
    static float stepStart;
    static Keyboard one, two;
    static bool reloaded;
    static float firstDistance, headingBefore;
    static readonly HashSet<float> captured = new HashSet<float>();
    static EnterPlayModeOptions savedOptions;
    static bool savedEnabled;
    static InputSettings.BackgroundBehavior savedBackground;
    static InputSettings.EditorInputBehaviorInPlayMode savedEditorInput;

    public static void RunBatch()
    {
        report.Clear().Append("[Iceberg checks]\n");
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

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        float inStep = Time.realtimeSinceStartup - stepStart;
        var seq = Object.FindAnyObjectByType<IcebergSequence>();
        var look = IcebergPresentation.Instance;

        switch (step)
        {
            case 0: // Join with a keyboard the way the menu does: seat it, then (re)load Ship
                if (GameManager.Instance == null) return;
                if (!reloaded)
                {
                    reloaded = true;
                    one = InputSystem.AddDevice<Keyboard>("CheckKeyboard1");
                    PlayerSlots.Clear();
                    PlayerSlots.Join(one);
                    SceneManager.LoadScene("Ship");
                    stepStart = Time.realtimeSinceStartup;
                    return;
                }
                if (Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length == 0) { if (inStep > 15f) Fail("no player joined"); return; }
                Next();
                break;

            case 1: // To the Wheel, facing the bow; the iceberg
                if (inStep < 1f) return;
                IntroCard.Instance?.Dismiss();
                var player = Object.FindAnyObjectByType<PlayerController>();
                var spot = GameObject.Find("Station_Wheel/WorkSpot_A");
                player.GetComponent<ThirdPersonMover>().Teleport(spot.transform.position, 0f);
                Camera.main.GetComponent<AutoFollowCamera>()?.SnapToTarget();
                GameManager.Instance.StartIceberg();
                Next();
                break;

            case 2: // The berg is up, the cube and the debug bar are not
                if (inStep < 0.5f) return;
                var berg = GameObject.Find("[Iceberg Look]");
                Check(GameManager.Instance.State == GameState.Iceberg, "The finale is running");
                Check(berg != null && berg.GetComponentInChildren<MeshRenderer>().enabled, "The real iceberg is in the scene");
                var cube = GameObject.Find("[Iceberg]");
                Check(cube == null || !cube.GetComponent<Renderer>().enabled, "Rabib's placeholder cube is hidden");
                Check(!IcebergSequence.DrawDebugGui, "The debug heading bar is replaced by the gauge");
                firstDistance = DistanceToBow(berg);
                Capture("01_start", true);
                Next();
                break;

            case 3: // Hold E and D: steer to starboard
                if (inStep < 0.1f) headingBefore = seq.Heading;
                InputSystem.QueueStateEvent(one, new KeyboardState(Key.E, Key.D));
                if (inStep < 4f) return;
                InputSystem.QueueStateEvent(one, new KeyboardState());
                float turned = seq.Heading - headingBefore;
                Check(turned > 3f, $"Holding E with D at the Wheel steers to starboard ({headingBefore:0.0}° to {seq.Heading:0.0}°)");
                Capture("02_steering", true);
                Next();
                break;

            case 4: // Halfway, then a second player joins for the shared view
                if (seq == null || seq.SecondsLeft > 15f) return;
                Capture("03_halfway", true);
                Check(DistanceToBow(GameObject.Find("[Iceberg Look]")) < firstDistance - 30f, $"The iceberg closes on the bow ({firstDistance:0} m to {DistanceToBow(GameObject.Find("[Iceberg Look]")):0} m)");
                // A second player, spawned the way PlayerJoin seats players who joined on the menu.
                two = InputSystem.AddDevice<Keyboard>("CheckKeyboard2");
                var prefab = PlayerInputManager.instance != null ? PlayerInputManager.instance.playerPrefab : null;
                if (prefab != null)
                {
                    var second = PlayerInput.Instantiate(prefab, playerIndex: 1, controlScheme: "Keyboard", pairWithDevice: two);
                    var at = GameObject.Find("Spawn_2");
                    if (at != null) second.GetComponent<ThirdPersonMover>().Teleport(at.transform.position, 180f);
                }
                Next();
                break;

            case 5:
                if (inStep < 3.5f) return;
                int humans = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length;
                Check(humans == 2 && !GameplayCameraRig.Instance.IsFollowing, $"Two players share the bow view ({humans} players)");
                Capture("04_shared_bow_view", true);
                Next();
                break;

            case 6: // The last seconds: shaking, berg at the bow
                if (seq == null || seq.SecondsLeft > 2f) return;
                Check(look != null && look.Shake > 0.3f, $"The camera shakes as it closes ({look?.Shake:0.00})");
                Capture("05_impact", true);
                Next();
                break;

            case 7: // Impact: Results
                if (SceneManager.GetActiveScene().name != "Results") { if (inStep > 10f) Fail("never reached Results"); return; }
                Check(true, "Impact goes on to Results");
                Check(IcebergSequence.DrawDebugGui, "The debug bar setting is restored after the finale");
                Check(errors.Count == 0, errors.Count == 0 ? "No errors or exceptions logged" : $"{errors.Count} errors: " + string.Join(" | ", errors.Distinct().Take(6)));
                Finish();
                break;
        }
    }

    static float DistanceToBow(GameObject berg)
    {
        if (berg == null) return -1f;
        var p = berg.transform.position;
        return Vector2.Distance(new Vector2(p.x, p.z), new Vector2(0f, 42.5f));
    }

    static void Fail(string why) { Check(false, "Timed out: " + why); Finish(); }

    static void Capture(string name, bool withUi)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var rt = new RenderTexture(1280, 720, 24);
        var canvases = withUi ? Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList() : new List<Canvas>();
        foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + 0.05f; }
        Canvas.ForceUpdateCanvases();
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
        System.IO.File.WriteAllBytes($"Logs/Iceberg_{name}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        report.Append(ok ? "ALL PASSED" : "SOME CHECKS FAILED");
        System.IO.File.WriteAllText("Logs/IcebergChecks.txt", report.ToString());
        Debug.Log(report.ToString());
        EditorSettings.enterPlayModeOptionsEnabled = savedEnabled;
        EditorSettings.enterPlayModeOptions = savedOptions;
        InputSystem.settings.backgroundBehavior = savedBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorInput;
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
    }
}
