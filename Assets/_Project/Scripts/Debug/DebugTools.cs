using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Debug HUD and keys. Only exists in the Editor and in Development builds. Owner: Anadi.
///   F1  skip to the next part        F5  obey the oldest active order
///   F2  cycle meter presets          F6  fake it
///   F3  jump to the iceberg          F7  refuse it
///   F4  reveal the Stoker            F8  restart the run
///   F9  show / hide this panel
/// </summary>
public class DebugTools : MonoBehaviour
{
    bool visible = true;
    bool revealStoker;
    int presetIndex;

    static readonly (string name, int ct, int ss, int crew)[] Presets =
    {
        ("start", 50, 80, 60),
        ("all low", 15, 20, 20),
        ("all high", 90, 90, 90),
        ("best ending", 40, 75, 70),
    };

    GUIStyle box, label;
    readonly StringBuilder sb = new StringBuilder(512);

    void Awake()
    {
        // Belt and braces: GameSystems only adds this in debug builds, but never let it run in a release build.
        if (!Debug.isDebugBuild) Destroy(this);
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.f1Key.wasPressedThisFrame) GameManager.Instance?.DebugSkipPart();
        if (kb.f2Key.wasPressedThisFrame) CyclePreset();
        if (kb.f3Key.wasPressedThisFrame) GameManager.Instance?.StartIceberg();
        if (kb.f4Key.wasPressedThisFrame) revealStoker = !revealStoker;
        if (kb.f5Key.wasPressedThisFrame) ResolveOldest(Choice.Obey);
        if (kb.f6Key.wasPressedThisFrame) ResolveOldest(Choice.Fake);
        if (kb.f7Key.wasPressedThisFrame) ResolveOldest(Choice.Refuse);
        if (kb.f8Key.wasPressedThisFrame) GameManager.Restart();
        if (kb.f9Key.wasPressedThisFrame) visible = !visible;
        if (GameManager.Instance != null && GameManager.Instance.State == GameState.Iceberg)
        {
            // Pick the impact to test each ending: 1 head-on, 2 glancing, 3 scrape.
            if (kb.digit1Key.wasPressedThisFrame) GameManager.Instance.FinishIceberg(0f);
            if (kb.digit2Key.wasPressedThisFrame) GameManager.Instance.FinishIceberg(30f);
            if (kb.digit3Key.wasPressedThisFrame) GameManager.Instance.FinishIceberg(60f);
        }
#endif
    }

    void CyclePreset()
    {
        presetIndex = (presetIndex + 1) % Presets.Length;
        var p = Presets[presetIndex];
        MeterSystem.Instance?.Set(p.ct, p.ss, p.crew);
        Debug.Log($"[Debug] Meters set to '{p.name}'");
    }

    static void ResolveOldest(Choice c)
    {
        var od = OrderDirector.Instance;
        if (od == null || od.Active.Count == 0) return;
        od.Resolve(od.Active[0], c);
    }

    void OnGUI()
    {
        if (!visible) return;
        box ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, padding = new RectOffset(10, 10, 8, 8) };
        label ??= new GUIStyle(GUI.skin.label) { fontSize = 12 };

        var gm = GameManager.Instance;
        var ms = MeterSystem.Instance;
        var od = OrderDirector.Instance;

        GUILayout.BeginArea(new Rect(10, 10, 360, Screen.height - 20));
        GUILayout.BeginVertical(box);

        GUILayout.Label($"<b>State:</b> {(gm != null ? gm.State.ToString() : "-")}", Rich(label));
        if (ms != null)
        {
            Bar("Captain's Trust", ms.CaptainTrust, new Color(0.85f, 0.65f, 0.3f));
            Bar("Ship Safety", ms.ShipSafety, new Color(0.4f, 0.7f, 0.95f));
            Bar("Crew Trust", ms.CrewTrust, new Color(0.45f, 0.8f, 0.55f));
        }

        if (od != null)
        {
            sb.Clear();
            sb.Append($"<b>Orders:</b> {od.NextIndex}/{od.Orders.Count} issued, next in {od.SecondsToNext:0}s\n");
            foreach (var o in od.Active)
            {
                string who = o.Assignee != null ? $"Seat {o.Assignee.Seat}{(o.Assignee.IsBot ? " (bot)" : "")}" : "nobody";
                string light = od.IsStationDone(o) ? "green" : "red";
                sb.Append($"  #{o.Number} {o.Data.resultsLine}\n     {who} · {o.TimeLeft:0}s · {o.Data.CheckStation} {light}\n");
            }
            GUILayout.Label(sb.ToString(), Rich(label));
        }

        if (RunRecord.Choices.Count > 0)
        {
            var last = RunRecord.Choices[RunRecord.Choices.Count - 1];
            GUILayout.Label($"Last: #{last.orderNumber} {last.choice} ({last.delta})", label);
        }

        if (revealStoker)
        {
            var s = CrewRegistry.Stoker;
            GUILayout.Label(s != null ? $"Stoker: Seat {s.Seat}" : "Stoker: no crew registered", label);
        }

        if (gm != null && gm.State == GameState.Iceberg)
            GUILayout.Label("<b>Impact:</b> 1 head-on · 2 glancing · 3 scrape", Rich(label));
        GUILayout.Label("F1 skip part · F2 meters · F3 iceberg · F4 stoker\nF5 obey · F6 fake · F7 refuse · F8 restart · F9 hide", label);
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    static GUIStyle Rich(GUIStyle s) { s.richText = true; return s; }

    void Bar(string name, int value, Color color)
    {
        GUILayout.Label($"{name}: {value}", label);
        var r = GUILayoutUtility.GetRect(320, 10);
        var old = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.5f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = color;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width * value / 100f, r.height), Texture2D.whiteTexture);
        GUI.color = old;
    }
}
