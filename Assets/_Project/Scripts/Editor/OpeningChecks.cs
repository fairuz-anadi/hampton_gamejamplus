using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Checks the opening end to end, headless. Owner: Samprity. Writes Logs/OpeningChecks.txt and Logs/Opening_*.png.
///   Unity.exe -batchmode -projectPath . -executeMethod OpeningChecks.RunBatch      (no -quit: it exits itself)
/// 1. Edit mode: walks the camera route every 1/20 s and reports anything within 0.2 m of the lens (every mesh gets a
///    temporary collider for this) or any crew member it passes through.
/// 2. Play mode (domain reload off for the run): Skip intro lands on the menu with nothing started; the full intro
///    plays with frames captured along the way; Begin Voyage loads Ship, the follow camera takes over from the menu's
///    pose, the crew wear their models, W walks the player, and no order was issued before Begin. Errors and
///    exceptions logged during the run fail it.
/// </summary>
public static class OpeningChecks
{
    static readonly StringBuilder report = new StringBuilder();
    static readonly List<string> errors = new List<string>();
    static bool ok = true;
    static int step;
    static float stepStart, lastCapture = -1f;
    static Vector3 menuPos;
    static Quaternion menuRot;
    static Vector3 playerStart;
    static Keyboard keyboard;
    static float followingSince = -1f;
    static InputSettings.BackgroundBehavior savedBackground;
    static InputSettings.EditorInputBehaviorInPlayMode savedEditorInput;
    static EnterPlayModeOptions savedOptions;
    static bool savedOptionsEnabled;
    static readonly float[] captureTimes = { 1.5f, 4f, 8f, 12f, 15f, 18.5f, 22f, 25f, 28f, 31f, 34f, 37f, 40f, 43f, 46f, 49f, 52f };
    static int nextCapture;

    public static void RunBatch()
    {
        report.Clear();
        report.Append("[Opening checks]\n");
        PathClearance();

        savedOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        savedOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        // Headless, the editor never has focus, and the Input System drops device input without it.
        savedBackground = InputSystem.settings.backgroundBehavior;
        savedEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        EditorSceneManager.OpenScene(OpeningSceneBuilder.MenuScenePath, OpenSceneMode.Single);
        Application.logMessageReceived += OnLog;
        step = 0;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Check(bool pass, string what)
    {
        ok &= pass;
        report.Append($"  {(pass ? "PASS" : "FAIL")}  {what}\n");
    }

    static void Note(string what) => report.Append($"  ....  {what}\n");

    static void OnLog(string message, string stack, LogType type)
    {
        // The editor's own search indexer throws in batch mode; that isn't the game.
        if (stack != null && stack.Contains("UnityEditor.Search")) return;
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            errors.Add($"{type}: {message.Split('\n')[0]}");
    }

    // ---------- 1. Route clearance ----------

    static void PathClearance()
    {
        var scene = EditorSceneManager.OpenScene(OpeningSceneBuilder.MenuScenePath, OpenSceneMode.Single);
        var path = Object.FindAnyObjectByType<CinematicPath>();
        var temp = new List<Collider>();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (r.GetComponent<Collider>() != null || r.GetComponent<FoamScroll>() != null || r.GetComponent<SeaScroll>() != null) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var mc = r.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            temp.Add(mc);
        }
        var crew = Object.FindObjectsByType<CinematicCrew>(FindObjectsSortMode.None);
        Physics.SyncTransforms();

        var hits = new Dictionary<string, float>();
        float closestCrew = float.MaxValue;
        string closestCrewName = "";
        for (float t = 0f; t <= path.Duration; t += 0.05f)
        {
            path.Evaluate(t, out var p, out _, out _);
            foreach (var c in Physics.OverlapSphere(p, 0.2f, ~0, QueryTriggerInteraction.Ignore))
                if (!hits.ContainsKey(c.name)) hits[c.name] = t;
            foreach (var a in crew)
            {
                // A person is a 0.3 m-radius column from the deck to 1.85 m.
                var f = a.transform.position;
                float dy = p.y < f.y ? f.y - p.y : p.y > f.y + 1.85f ? p.y - f.y - 1.85f : 0f;
                float d = new Vector2(Vector2.Distance(new Vector2(p.x, p.z), new Vector2(f.x, f.z)) - 0.3f, dy).magnitude;
                if (d < closestCrew) { closestCrew = d; closestCrewName = $"{a.name} at {t:0.0}s"; }
            }
        }
        Check(hits.Count == 0, "Camera route clears all geometry by 0.2 m" + (hits.Count == 0 ? "" : ": " + string.Join(", ", hits.Select(h => $"{h.Key} at {h.Value:0.0}s"))));
        Check(closestCrew > 0.25f, $"Camera route keeps clear of the crew (closest {closestCrew:0.00} m, {closestCrewName})");
        foreach (var c in temp) Object.DestroyImmediate(c);
        Note($"Route {path.keys.Count} keys, {path.Duration:0.0}s");
    }

    // ---------- 2. Play mode ----------

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        float now = Time.realtimeSinceStartup;
        float inStep = now - stepStart;
        var director = Object.FindAnyObjectByType<OpeningDirector>();

        switch (step)
        {
            case 0: // Opening loaded: intro running, nothing from the game exists
                if (director == null || director.State == OpeningDirector.Phase.Boot) { Timeout(inStep, 20f, "opening never started"); return; }
                Check(director.State == OpeningDirector.Phase.Intro, $"Startup plays the intro (state {director.State})");
                Check(GameManager.Instance == null && OrderDirector.Instance == null && MeterSystem.Instance == null, "No GameManager, orders or meters during the intro");
                CountCamerasAndListeners("intro");
                Check(Object.FindAnyObjectByType<MainMenuScreen>() == null, "Old placeholder menu not spawned");
                Next();
                break;

            case 1: // Skip after a few seconds
                if (director.IntroTime < 4f) return;
                director.Skip();
                Check(director.State == OpeningDirector.Phase.Menu, "Skip intro lands on the menu");
                Check(GameManager.Instance == null && CrewRegistry.All.Count == 0, "Skipping starts no gameplay");
                var cam = Camera.main;
                menuPos = cam.transform.position; menuRot = cam.transform.rotation;
                var path = Object.FindAnyObjectByType<CinematicPath>();
                path.Evaluate(path.Duration, out var endPos, out _, out _);
                Check(Vector3.Distance(menuPos, endPos) < 0.01f, "Skip puts the camera on the menu shot");
                Next();
                break;

            case 2: // Let the menu settle, capture it, then replay the intro from the top
                if (inStep < 3f) return;
                Capture("menu_after_skip", withUi: true);
                typeof(OpeningDirector).GetProperty("IntroSeenThisSession").SetValue(null, false);
                SceneManager.LoadScene("MainMenu");
                Time.timeScale = 3f; // play the minute faster; fades and frames still work
                nextCapture = 0;
                Next();
                break;

            case 3: // Full intro with frames along the way
                if (director == null || director.State == OpeningDirector.Phase.Boot) { Timeout(inStep, 20f, "replayed intro never started"); return; }
                Time.timeScale = 3f;
                if (director.State == OpeningDirector.Phase.Intro)
                {
                    while (nextCapture < captureTimes.Length && director.IntroTime >= captureTimes[nextCapture])
                        Capture($"intro_{nextCapture + 1:00}_{captureTimes[nextCapture]:0}s", withUi: true, nextCapture++);
                    Timeout(inStep, 60f, "intro didn't finish");
                    return;
                }
                Time.timeScale = 1f;
                Check(director.State == OpeningDirector.Phase.Menu, $"Intro runs into the menu by itself ({director.IntroTime:0.0}s of camera)");
                Check(director.IntroTime <= 60f, "Intro is under 60 s");
                Check(GameManager.Instance == null && CrewRegistry.All.Count == 0, "Watching the whole intro starts no gameplay");
                Next();
                break;

            case 4: // Menu: settings round trip, then Begin Voyage with a (virtual) keyboard
                if (inStep < 2.5f) return;
                Capture("menu", withUi: true);
                var menu = Object.FindAnyObjectByType<OpeningMenu>();
                float music = GameSettings.Music;
                menu.OpenSettings(true);
                Capture("settings", withUi: true);
                GameSettings.Music = 0.31f;
                Check(Mathf.Abs(PlayerPrefs.GetFloat("Port.Settings.Music") - 0.31f) < 0.001f, "Settings save to PlayerPrefs");
                GameSettings.Music = music;
                menu.OpenSettings(false);
                Check(!menu.SettingsOpen, "Settings Back returns to the menu");
                keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
                PlayerSlots.Clear();
                director.Begin();
                Check(director.State == OpeningDirector.Phase.Starting, "Begin Voyage starts the voyage");
                Check(PlayerSlots.Devices.Count == 1, $"Begin Voyage seats the keyboard player ({PlayerSlots.Devices.Count} joined)");
                Next();
                break;

            case 5: // Ship loaded: follow camera on seat 1 in the menu's pose, crew dressed, how-to-play waiting
                if (SceneManager.GetActiveScene().name != "Ship" || GameManager.Instance == null) { Timeout(inStep, 30f, "Ship never loaded"); return; }
                var rig = GameplayCameraRig.Instance;
                if (rig == null || !rig.IsFollowing) { Timeout(inStep, 15f, "follow camera never took over"); return; }
                if (followingSince < 0f) followingSince = now;
                if (now - followingSince < 1.5f) return; // the crew are dressed a beat after they appear
                Check(GameManager.Instance.State == GameState.Part1, $"Run starts at Part 1 ({GameManager.Instance.State})");
                Check(OrderDirector.Instance != null && OrderDirector.Instance.Active.Count == 0, "No order issued while the how-to-play card holds the start");
                var ms = MeterSystem.Instance;
                Check(ms != null && Mathf.Approximately(ms.CaptainTrust, 50f) && Mathf.Approximately(ms.ShipSafety, 80f) && Mathf.Approximately(ms.CrewTrust, 60f),
                    $"Meters untouched at the start ({ms?.CaptainTrust}/{ms?.ShipSafety}/{ms?.CrewTrust})");
                Check(rig.Followed != null && rig.Followed.Seat == 1, "Third-person camera follows seat 1");
                var c = Camera.main;
                float posErr = Vector3.Distance(c.transform.position, menuPos), rotErr = Quaternion.Angle(c.transform.rotation, menuRot);
                Check(posErr < 0.3f && rotErr < 3f, $"Gameplay camera starts in the menu's pose ({posErr:0.00} m, {rotErr:0.0}°)");
                CountCamerasAndListeners("gameplay");
                int dressed = Object.FindObjectsByType<CrewVisual>(FindObjectsSortMode.None).Length;
                Check(dressed == 4, $"All four crew wear crew models ({dressed})");
                Check(IntroCard.IsShowing, "How-to-play card is up and holding time");
                Capture("handoff_card", withUi: true);
                IntroCard.Instance?.Dismiss();
                playerStart = rig.Followed.transform.position;
                Capture("handoff", withUi: false);
                Next();
                break;

            case 6: // Hold W briefly: forward is the furnace, about two metres of clear deck
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                if (inStep < 0.6f) return;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                var player = GameplayCameraRig.Instance.Followed;
                float moved = Vector3.Distance(player.transform.position, playerStart);
                Check(moved > 1f, $"W walks the player ({moved:0.0} m in 0.6 s)");
                var toPlayer = player.transform.position - Camera.main.transform.position;
                Check(Vector3.Dot(Camera.main.transform.forward, toPlayer.normalized) > 0.8f && toPlayer.magnitude < 7f, "Camera stays behind the player");
                Capture("gameplay_walk", withUi: true);
                Next();
                break;

            case 7: // Let the HUD and orders get going, then finish
                if (inStep < 6f) return;
                Capture("gameplay_hud", withUi: true);
                Check(errors.Count == 0, errors.Count == 0 ? "No errors or exceptions logged" : $"{errors.Count} errors logged: " + string.Join(" | ", errors.Distinct().Take(6)));
                Finish();
                break;
        }
    }

    static void CountCamerasAndListeners(string phase)
    {
        int cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled && x.targetTexture == null);
        int listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled);
        Check(cams == 1 && listeners == 1, $"One camera and one audio listener in {phase} ({cams}, {listeners})");
    }

    static void Next()
    {
        step++;
        stepStart = Time.realtimeSinceStartup;
    }

    static void Timeout(float inStep, float limit, string what)
    {
        if (inStep < limit) return;
        Check(false, "Timed out: " + what);
        Finish();
    }

    /// <summary>Renders the main camera, and the screen-space UI with it when asked, into Logs/Opening_name.png.</summary>
    static void Capture(string name, bool withUi, int index = -1)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var rt = new RenderTexture(1280, 720, 24);
        var canvases = withUi ? Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(cv => cv.isRootCanvas && cv.renderMode == RenderMode.ScreenSpaceOverlay).ToList() : new List<Canvas>();
        foreach (var cv in canvases) { cv.renderMode = RenderMode.ScreenSpaceCamera; cv.worldCamera = cam; cv.planeDistance = cam.nearClipPlane + 0.05f; }
        if (canvases.Count > 0) Canvas.ForceUpdateCanvases();
        var target = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = target;
        foreach (var cv in canvases) cv.renderMode = RenderMode.ScreenSpaceOverlay;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes($"Logs/Opening_{name}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        Time.timeScale = 1f;
        report.Append(ok ? "ALL PASSED" : "SOME CHECKS FAILED");
        System.IO.File.WriteAllText("Logs/OpeningChecks.txt", report.ToString());
        Debug.Log(report.ToString());
        EditorSettings.enterPlayModeOptionsEnabled = savedOptionsEnabled;
        EditorSettings.enterPlayModeOptions = savedOptions;
        InputSystem.settings.backgroundBehavior = savedBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorInput;
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
    }
}
