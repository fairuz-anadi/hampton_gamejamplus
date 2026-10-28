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
/// Plays Ship with one keyboard player and checks the Captain on deck end to end. Owner: Samprity.
///   Unity.exe -batchmode -projectPath . -executeMethod CaptainChecks.RunBatch      (no -quit: it exits itself)
/// He spawns at the voice pipe; goes to it when an order is issued; walks his rounds; a player can walk up and talk
/// to him with E (and the talk ends); Part 2 makes him drunk and he swigs; Part 3 makes him stiff and sober; at the
/// iceberg he stands aside. No errors on the way. Writes Logs/CaptainChecks.txt and Logs/Captain_*.png.
/// </summary>
public static class CaptainChecks
{
    static readonly StringBuilder report = new StringBuilder();
    static readonly List<string> errors = new List<string>();
    static bool ok = true;
    static int step;
    static float stepStart;
    static bool reloaded, orderSeen, sipSeen, stumbleSeen;
    static Keyboard one;
    static int presses;
    static float lastPress, part3At = -1f, sipAt = -1f;
    static EnterPlayModeOptions savedOptions;
    static bool savedEnabled;
    static InputSettings.BackgroundBehavior savedBackground;
    static InputSettings.EditorInputBehaviorInPlayMode savedEditorInput;

    public static void RunBatch()
    {
        report.Clear().Append("[Captain checks]\n");
        errors.Clear();
        ok = true;
        step = 0;
        reloaded = orderSeen = sipSeen = stumbleSeen = false;
        part3At = -1f;
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

    static void OnOrder(ActiveOrder o) => orderSeen = true;

    static void Check(bool pass, string what)
    {
        ok &= pass;
        report.Append($"  {(pass ? "PASS" : "FAIL")}  {what}\n");
    }

    static void Note(string what) => report.Append($"  ....  {what}\n");
    static void Next() { step++; stepStart = Time.realtimeSinceStartup; }
    static void Fail(string why) { Check(false, "Timed out: " + why); Finish(); }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        float inStep = Time.realtimeSinceStartup - stepStart;
        var cap = CaptainOnDeck.Instance;
        var gm = GameManager.Instance;
        if (cap != null && cap.Ready)
        {
            if (cap.Body.IsSipping) sipSeen = true;
            if (cap.Body.IsStumbling) stumbleSeen = true;
        }

        switch (step)
        {
            case 0: // Join with a keyboard the way the menu does, then (re)load Ship
                if (gm == null) return;
                if (!reloaded)
                {
                    reloaded = true;
                    one = InputSystem.AddDevice<Keyboard>("CaptainCheckKeyboard");
                    PlayerSlots.Clear();
                    PlayerSlots.Join(one);
                    SceneManager.LoadScene("Ship");
                    stepStart = Time.realtimeSinceStartup;
                    return;
                }
                if (Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length == 0) { if (inStep > 15f) Fail("no player joined"); return; }
                GameEvents.OrderIssued -= OnOrder;
                GameEvents.OrderIssued += OnOrder;
                Next();
                break;

            case 1: // He's on the bridge, at the voice pipe
                if (cap == null || !cap.Ready) { if (inStep > 15f) Fail("the Captain never appeared"); return; }
                IntroCard.Instance?.Dismiss();
                Check(Flat(cap.Position - cap.PipeSpot) < 0.6f, $"Spawns at the bridge voice pipe ({cap.Position:F1})");
                Check(cap.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh, "Stands on the crew's NavMesh");
                Check(cap.RoundCount >= 3, $"Has rounds to walk ({cap.RoundCount} spots, all clear of the stations)");
                Check(cap.GetComponent<ICrewActor>() == null, "Is not one of the crew (orders never go to him)");
                Next();
                break;

            case 2: // An order: he's at the pipe, talking into it
                if (!orderSeen) { if (inStep > 60f) Fail("no order was issued"); return; }
                if (Flat(cap.Position - cap.PipeSpot) > 0.5f || cap.ModeName != "AtPipe") { if (inStep > 20f) Fail($"didn't go to the pipe for the order ({cap.ModeName})"); return; }
                Check(true, "Goes to the voice pipe when an order is issued");
                if (CaptainDirector.Instance != null && CaptainDirector.Instance.IsSpeaking) Check(cap.Body.Talking, "Plays his talking animation while the speaker line types");
                Shoot("1_order_at_pipe", cap);
                Next();
                break;

            case 3: // Between orders he walks his rounds
                if (cap.ModeName != "Walking" || cap.Body.Speed < 0.4f) { if (inStep > 90f) Fail($"never left the pipe on his rounds ({cap.ModeName})"); return; }
                Check(true, $"Walks his rounds between orders ({cap.Body.Speed:0.00} m/s, upright: drunk {cap.Drunkenness:0.00})");
                Check(cap.Drunkenness < 0.01f, "Sober in Part 1");
                Shoot("2_rounds", cap);
                Next();
                break;

            case 4: // A player walks up and talks to him
            {
                if (!cap.Available || cap.ModeName != "Dwelling") { if (inStep > 90f) Fail($"never stood still long enough to talk ({cap.ModeName})"); return; }
                var player = Object.FindAnyObjectByType<PlayerController>();
                // Walk up the way a player would: from an open side of him (at a window that's the room behind him),
                // on the deck and out of reach of every station.
                var spot = cap.Position + cap.transform.forward * 1.2f;
                for (int i = 0; i < 12; i++)
                {
                    var want = cap.Position + Quaternion.Euler(0f, i * 30f, 0f) * cap.transform.forward * 1.2f;
                    if (!UnityEngine.AI.NavMesh.SamplePosition(want, out var hit, 0.3f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                    if (StationRegistry.All.Any(s => Vector3.Distance(s.Position, hit.position) < 2.7f)) continue;
                    if (Physics.Linecast(cap.Position + Vector3.up, hit.position + Vector3.up, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;
                    spot = hit.position;
                    break;
                }
                var yaw = Quaternion.LookRotation(Flat3(cap.Position - spot)).eulerAngles.y;
                player.GetComponent<ThirdPersonMover>().Teleport(spot, yaw);
                Camera.main.GetComponent<AutoFollowCamera>()?.SnapToTarget();
                presses = 0;
                Next();
                break;
            }

            case 5:
                if (inStep < 0.6f) return;
                if (presses == 0) { Press(); presses = 1; return; }
                if (inStep < 1.4f) return;
                Check(CaptainTalk.IsOpen && cap.InConversation, "E by the Captain opens a conversation, and he stops for it");
                if (!CaptainTalk.IsOpen)
                {
                    var p = Object.FindAnyObjectByType<PlayerController>();
                    var it = p.GetComponent<Interactor>();
                    Note($"Captain mode {cap.ModeName}, available {cap.Available}, player {Flat(p.transform.position - cap.Position):0.00} m away, " +
                         $"station in reach {it?.NearestStation?.Type.ToString() ?? "none"}, carrying {it?.IsCarrying}, speaker busy {CaptainDirector.Instance?.IsSpeaking}");
                    Next(); Next(); return;
                }
                Next();
                break;

            case 6: // He's facing the player; the line types out; E pages through and ends it
                if (inStep < 2.5f) return;
                {
                    var player = Object.FindAnyObjectByType<PlayerController>();
                    float facing = Vector3.Angle(cap.transform.forward, Flat3(player.transform.position - cap.Position));
                    Check(facing < 30f, $"Turns to face whoever's talking ({facing:0}°)");
                }
                Capture("3_talk");
                lastPress = 0f;
                Next();
                break;

            case 7: // one press every half second: finish the line, next page, ... done
                if (CaptainTalk.IsOpen && inStep < 8f)
                {
                    if (inStep - lastPress >= 0.5f) { Press(); lastPress = inStep; }
                    return;
                }
                Check(!CaptainTalk.IsOpen && !cap.InConversation, "E pages through his line and ends the conversation");
                gm.DebugSkipPart(); // Part 2
                Next();
                break;

            case 8: // Part 2: the drunk act
                if (gm.State != GameState.Part2) { if (inStep > 10f) Fail("Part 2 never started"); return; }
                if (inStep < 20f || !sipSeen) { if (inStep > 70f) Fail("no swig in Part 2"); return; }
                Check(cap.Drunkenness > 0.3f, $"Acts drunk in Part 2 (drunk {cap.Drunkenness:0.00}, mood {CaptainDirector.Instance?.Mood})");
                Check(sipSeen, "Takes a swig from the bottle");
                Note(stumbleSeen ? "Stumbled while walking" : "No stumble seen yet (they come every 6-16 s of walking)");
                Next();
                break;

            case 9: // Catch a swig (bottle at his mouth, a second in) and a drunk walk on camera
                if (!cap.Body.IsSipping) { sipAt = -1f; if (inStep < 40f) return; }
                else
                {
                    if (sipAt < 0f) sipAt = inStep;
                    if (inStep - sipAt < 0.95f) return;
                }
                Shoot("4_drunk_swig", cap);
                Next();
                break;

            case 10:
                if (cap.Body.Speed < 0.4f && inStep < 60f) return;
                Shoot("5_drunk_walk", cap);
                gm.DebugSkipPart(); // Part 3
                Next();
                break;

            case 11: // Part 3: the act drops
                // Part 3 begins when its first order is issued, a few seconds after the skip: time it from there.
                if (gm.State != GameState.Part3) { if (inStep > 20f) Fail("Part 3 never started"); return; }
                if (part3At < 0f) part3At = inStep;
                if (inStep - part3At < 3f) return;
                Check(cap.Body.Stiff && cap.Drunkenness < 0.05f, $"Part 3: stands straight, not drunk at all (stiff {cap.Body.Stiff}, drunk {cap.Drunkenness:0.00})");
                Shoot("6_part3", cap);
                gm.StartIceberg();
                Next();
                break;

            case 12: // The iceberg: he stands aside and watches the bow
                if (cap.ModeName != "Watch") { if (inStep > 25f) Fail($"never stood aside for the iceberg ({cap.ModeName})"); return; }
                Check(Flat(cap.Position - cap.WatchSpot) < 0.6f, "Stands aside at the port window for the iceberg");
                Shoot("7_iceberg", cap);
                Check(errors.Count == 0, errors.Count == 0 ? "No errors or exceptions" : $"{errors.Count} errors: " + string.Join(" | ", errors.Distinct().Take(5)));
                Finish();
                break;
        }
    }

    static void Press()
    {
        InputSystem.QueueStateEvent(one, new KeyboardState(Key.E));
        InputSystem.Update();
        EditorApplication.delayCall += () => { if (one != null) InputSystem.QueueStateEvent(one, new KeyboardState()); };
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
    static Vector3 Flat3(Vector3 v) { v.y = 0f; return v; }

    /// <summary>A three-quarter shot of the Captain from in front, to Logs/Captain_name.png.</summary>
    static void Shoot(string name, CaptainOnDeck cap)
    {
        var go = new GameObject("CheckCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 45f;
        cam.nearClipPlane = 0.05f;
        // Of a ring of spots around him, the first (from his front) with a clear line to his chest: no walls in shot.
        var t = cap.transform;
        var chest = t.position + Vector3.up * 1.2f;
        var best = chest + t.forward * 2.4f + Vector3.up * 0.7f;
        for (int i = 0; i < 16; i++)
        {
            var dir = Quaternion.Euler(0f, (i % 2 == 0 ? 1 : -1) * (25f + i / 2 * 22f), 0f) * t.forward;
            var p = chest + dir * 2.4f + Vector3.up * 0.7f;
            if (!Physics.Linecast(chest, p, ~(1 << 2), QueryTriggerInteraction.Ignore) && !Physics.CheckSphere(p, 0.3f, ~(1 << 2), QueryTriggerInteraction.Ignore)) { best = p; break; }
        }
        go.transform.position = best;
        go.transform.LookAt(chest);
        Render(cam, name);
        Object.DestroyImmediate(go);
    }

    /// <summary>The player's own view with the HUD, to Logs/Captain_name.png.</summary>
    static void Capture(string name)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
        foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + 0.05f; }
        Canvas.ForceUpdateCanvases();
        Render(cam, name);
        foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
    }

    static void Render(Camera cam, string name)
    {
        var rt = new RenderTexture(1280, 720, 24);
        var target = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = target;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        System.IO.Directory.CreateDirectory("Logs");
        System.IO.File.WriteAllBytes($"Logs/Captain_{name}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        GameEvents.OrderIssued -= OnOrder;
        report.Append(ok ? "ALL PASSED" : "SOME CHECKS FAILED");
        System.IO.File.WriteAllText("Logs/CaptainChecks.txt", report.ToString());
        Debug.Log(report.ToString());
        EditorSettings.enterPlayModeOptionsEnabled = savedEnabled;
        EditorSettings.enterPlayModeOptions = savedOptions;
        InputSystem.settings.backgroundBehavior = savedBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorInput;
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
    }
}
