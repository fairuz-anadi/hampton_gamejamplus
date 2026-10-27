using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pause menu for gameplay scenes: Esc or Start on any gamepad. Resume, Restart (new Stoker), Main menu, Quit.
/// Works with mouse, keyboard (arrows + Enter) and gamepad (d-pad + A, B to resume).
/// Spawned with the core systems by GameSystems. Owner: Anadi.
/// Rabib: don't bind Esc/Start to your own pause; call PauseMenu.Instance.Toggle() if your Pause action needs it.
/// Drawn with IMGUI on a dark card in Samprity's type and colours (UiTheme), like the Results screen.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    [SerializeField] string menuScene = "MainMenu";

    const float RefHeight = 1080f;
    static readonly string[] Items = { "Resume", "Restart", "Main menu", "Quit" };

    public bool IsPaused { get; private set; }

    int selected;
    GUIStyle eyebrow, title, button, buttonSelected, hint;
    Texture2D shadeTex, cardTex, brassTex, buttonTex, buttonHoverTex, selectedTex;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        foreach (var t in new[] { shadeTex, cardTex, brassTex, buttonTex, buttonHoverTex, selectedTex })
            if (t != null) Destroy(t);
        if (Instance != this) return;
        Instance = null;
        if (IsPaused) Time.timeScale = 1f;
    }

    void Update()
    {
        var kb = Keyboard.current;
        bool toggle = kb != null && kb.escapeKey.wasPressedThisFrame;
        foreach (var pad in Gamepad.all) toggle |= pad.startButton.wasPressedThisFrame;
        if (toggle) { Toggle(); return; }
        if (!IsPaused) return;

        foreach (var pad in Gamepad.all)
            if (pad.buttonEast.wasPressedThisFrame) { Resume(); return; }
        selected = MenuControls.Move(selected, Items.Length);
        if (MenuControls.Confirm()) Choose(selected);
    }

    public void Toggle()
    {
        if (IsPaused) Resume();
        else if (IntroCard.IsShowing) return;
        else if (GameManager.Instance == null || GameManager.Instance.State != GameState.Results) Pause();
    }

    public void Pause()
    {
        IsPaused = true;
        selected = 0;
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
    }

    void Choose(int item)
    {
        switch (item)
        {
            case 0: Resume(); break;
            case 1: GameManager.Restart(); break;
            case 2: GameManager.LoadScene(menuScene); break;
            default: Quit(); break;
        }
    }

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnGUI()
    {
        if (!IsPaused) return;
        float s = Screen.height / RefHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s;
        EnsureStyles();

        // Shade the deck, then a dark card with a brass hairline, like the Results screen.
        GUI.DrawTexture(new Rect(0f, 0f, w, RefHeight), shadeTex);
        var card = new Rect(w * 0.5f - 260f, 220f, 520f, 640f);
        GUI.DrawTexture(card, cardTex);
        GUI.DrawTexture(new Rect(card.x, card.y, card.width, 2f), brassTex);
        GUI.DrawTexture(new Rect(card.x, card.yMax - 2f, card.width, 2f), brassTex);
        GUI.DrawTexture(new Rect(card.x, card.y, 2f, card.height), brassTex);
        GUI.DrawTexture(new Rect(card.xMax - 2f, card.y, 2f, card.height), brassTex);

        GUILayout.BeginArea(new Rect(card.x + 60f, card.y + 44f, card.width - 120f, card.height - 80f));
        GUILayout.Label("T H E   S H I P   W A I T S", eyebrow);
        GUILayout.Label("Paused", title);
        GUILayout.Space(22f);
        for (int i = 0; i < Items.Length; i++)
        {
            if (GUILayout.Button(Items[i], i == selected ? buttonSelected : button, GUILayout.Height(64f))) Choose(i);
            GUILayout.Space(12f);
        }
        GUILayout.FlexibleSpace();
        GUILayout.Label("Arrows or d-pad to choose  ·  Enter or A to pick  ·  Esc or B to resume", hint);
        GUILayout.EndArea();
    }

    static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    void EnsureStyles()
    {
        if (title != null) return;
        var theme = UiTheme.Load();
        Font titleFont = theme ? theme.title : null, bodyFont = theme ? theme.body : null;

        shadeTex = Solid(new Color(0.02f, 0.025f, 0.04f, 0.62f));
        cardTex = Solid(new Color(0.03f, 0.035f, 0.05f, 0.9f));
        brassTex = Solid(new Color(UiTheme.Brass.r, UiTheme.Brass.g, UiTheme.Brass.b, 0.6f));
        buttonTex = Solid(new Color(0.08f, 0.09f, 0.12f, 0.95f));
        buttonHoverTex = Solid(new Color(0.14f, 0.15f, 0.19f, 0.95f));
        selectedTex = Solid(UiTheme.Brass);

        eyebrow = new GUIStyle(GUI.skin.label) { font = bodyFont, fontSize = 20, alignment = TextAnchor.MiddleCenter, normal = { textColor = UiTheme.Brass } };
        title = new GUIStyle(GUI.skin.label) { font = titleFont, fontSize = 72, alignment = TextAnchor.MiddleCenter, fontStyle = titleFont != null ? FontStyle.Normal : FontStyle.Bold, normal = { textColor = UiTheme.Cream } };
        hint = new GUIStyle(GUI.skin.label) { font = bodyFont, fontSize = 18, wordWrap = true, alignment = TextAnchor.MiddleCenter, normal = { textColor = UiTheme.DimCream } };
        button = new GUIStyle(GUI.skin.button)
        {
            font = titleFont, fontSize = 30, alignment = TextAnchor.MiddleCenter, border = new RectOffset(0, 0, 0, 0),
            normal = { background = buttonTex, textColor = UiTheme.Cream },
            hover = { background = buttonHoverTex, textColor = UiTheme.Cream },
            active = { background = buttonHoverTex, textColor = UiTheme.Brass },
        };
        var ink = new Color(0.08f, 0.07f, 0.05f);
        buttonSelected = new GUIStyle(button)
        {
            normal = { background = selectedTex, textColor = ink },
            hover = { background = selectedTex, textColor = ink },
        };
    }
}
