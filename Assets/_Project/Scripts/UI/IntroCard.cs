using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// "How to play" card at the start of the first voyage in Ship.unity. Time is frozen until a player dismisses it,
/// so the first order waits for it (plan: "the first order waits until the tutorial prompt is dismissed").
/// Shown once per session; Play again and Restart skip it. Spawned by GameSystems. Owner: Anadi.
/// Samprity: placeholder drawn with IMGUI; restyle freely.
/// </summary>
public class IntroCard : MonoBehaviour
{
    public static IntroCard Instance { get; private set; }
    public static bool IsShowing => Instance != null && Instance.showing;

    static bool shownThisSession;

    const float RefHeight = 1080f;

    bool showing;
    GUIStyle title, body, hint;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Start()
    {
        if (shownThisSession) { Destroy(this); return; }
        shownThisSession = true;
        showing = true;
        Time.timeScale = 0f;
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        if (showing) Time.timeScale = 1f;
    }

    void Update()
    {
        if (!showing) return;
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) Dismiss();
        foreach (var pad in Gamepad.all)
            if (pad.buttonSouth.wasPressedThisFrame) { Dismiss(); break; }
    }

    public void Dismiss()
    {
        if (!showing) return;
        showing = false;
        Time.timeScale = 1f;
        Destroy(this);
    }

    void OnGUI()
    {
        if (!showing) return;
        float s = Screen.height / RefHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s;
        title ??= new GUIStyle(GUI.skin.label) { fontSize = 56, fontStyle = FontStyle.Bold };
        body ??= new GUIStyle(GUI.skin.label) { fontSize = 28, wordWrap = true, richText = true };
        hint ??= new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0f, 0f, w, RefHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(w * 0.5f - 560f, 150f, 1120f, 800f));
        GUILayout.Label("All hands on deck", title);
        GUILayout.Space(16f);
        GUILayout.Label("The Captain gives each crew member orders over the speaker. For every order you choose:", body);
        GUILayout.Label("•  <b>Obey</b>: do the work at the station until its light turns green, then <b>Report done</b> (R / Y).", body);
        GUILayout.Label("•  <b>Fake it</b>: report done while the light is still red. Someone might notice.", body);
        GUILayout.Label("•  <b>Refuse</b> (X / B): everyone hears it.", body);
        GUILayout.Space(10f);
        GUILayout.Label("Move with W A S D or the left stick. Hold <b>E / A</b> to work. Some jobs need two people: <b>Call for help</b> with Q / X.", body);
        GUILayout.Label("Watch the station lights and the three meters. Esc or Start pauses.", body);
        GUILayout.Space(30f);
        GUILayout.Label("Press Enter or A to set sail", hint);
        GUILayout.EndArea();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => shownThisSession = false;
}
