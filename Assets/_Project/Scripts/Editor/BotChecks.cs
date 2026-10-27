using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Plays Ship.unity headless and watches the bots: they're crew models (not capsules), they walk on the NavMesh,
/// work stations and play their work loop while they do. Owner: Samprity.
///   Unity.exe -batchmode -projectPath . -executeMethod BotChecks.RunBatch       (no -quit: it exits itself)
/// Writes Logs/BotChecks.txt and Logs/Bots_*.png. Runs the game 4x fast for about two and a half minutes of game time.
/// </summary>
public static class BotChecks
{
    const float GameSeconds = 150f;
    static readonly StringBuilder report = new StringBuilder();
    static readonly List<string> errors = new List<string>();
    static readonly Dictionary<int, float> walked = new Dictionary<int, float>();
    static readonly Dictionary<int, Vector3> last = new Dictionary<int, Vector3>();
    static readonly HashSet<int> worked = new HashSet<int>(), pushed = new HashSet<int>();
    static readonly Dictionary<int, string> workedAt = new Dictionary<int, string>();
    static bool ok = true, started, looked, capturedWork;
    static float startTime, realStart;
    static EnterPlayModeOptions savedOptions;
    static bool savedEnabled;

    public static void RunBatch()
    {
        report.Clear().Append("[Bot checks]\n");
        savedEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        savedOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
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

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        var bots = Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None).OrderBy(b => b.Seat).ToList();

        if (!started)
        {
            if (GameManager.Instance == null || bots.Count == 0) return;
            if (IntroCard.IsShowing) { IntroCard.Instance.Dismiss(); return; }
            started = true;
            startTime = Time.time;
            realStart = Time.realtimeSinceStartup;
            Time.timeScale = 4f;
            return;
        }

        // The camera rig dresses the crew a beat after they appear; look once it has.
        if (!looked)
        {
            if (Time.realtimeSinceStartup - realStart < 1.5f) return;
            looked = true;
            Check(bots.Count == 3, $"Three bots crew seats 2-4 ({string.Join(", ", bots.Select(b => b.Seat))})");
            foreach (var b in bots)
            {
                bool model = b.GetComponentInChildren<SkinnedMeshRenderer>() != null;
                bool capsule = b.GetComponents<MeshRenderer>().Any(r => r.enabled);
                var anim = b.GetComponentInChildren<Animator>();
                Check(model && !capsule, $"Seat {b.Seat} is a crew model, not a capsule ({b.name})");
                Check(anim != null && anim.runtimeAnimatorController != null && anim.runtimeAnimatorController.name == "CrewCinematic",
                    $"Seat {b.Seat} animates with the crew controller ({anim?.runtimeAnimatorController?.name})");
                var agent = b.GetComponent<UnityEngine.AI.NavMeshAgent>();
                Check(agent != null && agent.isOnNavMesh, $"Seat {b.Seat} stands on the NavMesh");
                last[b.Seat] = b.transform.position;
                walked[b.Seat] = 0f;
            }
            Capture("start", overview: true);
            return;
        }

        foreach (var b in bots)
        {
            var p = b.transform.position;
            if (last.TryGetValue(b.Seat, out var q)) walked[b.Seat] += Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(q.x, 0f, q.z));
            last[b.Seat] = p;
            if (b.IsWorking)
            {
                worked.Add(b.Seat);
                workedAt[b.Seat] = b.Activity;
                var anim = b.GetComponentInChildren<Animator>();
                if (anim != null && anim.GetCurrentAnimatorStateInfo(0).IsName("Push"))
                {
                    pushed.Add(b.Seat);
                    if (!capturedWork) { capturedWork = true; CaptureNear(b); }
                }
            }
        }

        if (Time.time - startTime < GameSeconds) return;
        Capture("later", overview: true);
        foreach (var b in bots)
            Check(walked[b.Seat] > 3f, $"Seat {b.Seat} walks ({walked[b.Seat]:0.0} m in {GameSeconds:0}s of game)");
        Check(worked.Count >= 2, $"Bots work stations (seats {string.Join(", ", worked)}: {string.Join("; ", workedAt.Select(w => $"{w.Key} {w.Value}"))})");
        Check(pushed.Count >= 1 && pushed.SetEquals(pushed.Intersect(worked)), $"Working bots play the work loop (seats {string.Join(", ", pushed)})");
        var od = OrderDirector.Instance;
        Check(RunRecord.Choices.Count > 0 || (od != null && od.Active.Count > 0), $"Orders are flowing ({RunRecord.Choices.Count} resolved so far)");
        Check(errors.Count == 0, errors.Count == 0 ? "No errors or exceptions logged" : $"{errors.Count} errors: " + string.Join(" | ", errors.Distinct().Take(6)));
        Finish();
    }

    static void Capture(string name, bool overview)
    {
        var go = new GameObject("CheckCamera", typeof(Camera));
        var cam = go.GetComponent<Camera>();
        cam.fieldOfView = 45f;
        var look = Quaternion.Euler(55f, 0f, 0f);
        go.transform.SetPositionAndRotation(new Vector3(0f, 0f, 18f) - look * Vector3.forward * 34f, look);
        Render(cam, name);
        Object.DestroyImmediate(go);
    }

    static void CaptureNear(BotBrain b)
    {
        var go = new GameObject("CheckCamera", typeof(Camera));
        var cam = go.GetComponent<Camera>();
        cam.fieldOfView = 45f;
        var t = b.transform;
        go.transform.position = t.position - t.forward * 3.2f + Vector3.up * 2.4f + t.right * 0.8f;
        go.transform.LookAt(t.position + Vector3.up * 0.9f);
        Render(cam, $"working_seat{b.Seat}");
        Object.DestroyImmediate(go);
    }

    static void Render(Camera cam, string name)
    {
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes($"Logs/Bots_{name}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        cam.targetTexture = null;
        rt.Release();
        Object.DestroyImmediate(rt);
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        Time.timeScale = 1f;
        report.Append(ok ? "ALL PASSED" : "SOME CHECKS FAILED");
        System.IO.File.WriteAllText("Logs/BotChecks.txt", report.ToString());
        Debug.Log(report.ToString());
        EditorSettings.enterPlayModeOptionsEnabled = savedEnabled;
        EditorSettings.enterPlayModeOptions = savedOptions;
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
    }
}
