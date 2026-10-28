using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Pause menu for gameplay scenes: Esc or Start on any gamepad. Resume, Restart voyage (new Stoker), Settings,
/// Main menu, Quit. Works with mouse, keyboard (arrows + Enter, Esc) and gamepad (d-pad + A, B to go back).
/// Spawned with the core systems by GameSystems. Owner: Anadi.
/// Rabib: don't bind Esc/Start to your own pause; call PauseMenu.Instance.Toggle() if your Pause action needs it.
/// Look (Anadi's approved design, 28 Oct): a page of the ship's log laid over the blurred deck, the choices written
/// in by hand, the chosen one circled in red ink, the voyage so far noted at the bottom. Settings is a second page:
/// volumes and camera speed, left/right to change.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    [SerializeField] string menuScene = "MainMenu";

    static readonly string[] Items = { "Resume", "Restart voyage", "Settings", "Main menu", "Quit" };
    static readonly string[] SettingNames = { "Master volume", "Music", "Sound effects", "Camera speed", "Back" };
    static readonly Color Ink = new Color(0.2f, 0.14f, 0.09f), InkSoft = new Color(0.33f, 0.25f, 0.18f), RedInk = new Color(0.75f, 0.24f, 0.2f);

    public bool IsPaused { get; private set; }

    int selected;
    bool settingsPage;
    float circleAt;
    GameObject canvasGo;
    CanvasGroup group;
    RectTransform page, circle;
    Text heading, subheading, footer;
    readonly Text[] rows = new Text[5];
    Texture2D paperTex, circleTex;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Start() => Build();

    void OnDestroy()
    {
        if (paperTex != null) Destroy(paperTex);
        if (circleTex != null) Destroy(circleTex);
        if (canvasGo != null) Destroy(canvasGo);
        if (Instance != this) return;
        Instance = null;
        if (IsPaused) Time.timeScale = 1f;
    }

    void Update()
    {
        var kb = Keyboard.current;
        bool start = false;
        foreach (var pad in Gamepad.all) start |= pad.startButton.wasPressedThisFrame;
        bool esc = kb != null && kb.escapeKey.wasPressedThisFrame;
        if (IsPaused && settingsPage && (esc || MenuControls.Back())) { OpenSettings(false); return; }
        if (esc || start) { Toggle(); return; }

        if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, IsPaused ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        if (canvasGo != null && canvasGo.activeSelf != (IsPaused || group.alpha > 0f)) canvasGo.SetActive(IsPaused || group.alpha > 0f);
        if (!IsPaused) return;

        foreach (var pad in Gamepad.all)
            if (pad.buttonEast.wasPressedThisFrame) { Resume(); return; }

        int was = selected;
        selected = MenuControls.Move(selected, 5);
        // The mouse writes over the keyboard's choice when it moves onto a line; a click picks it.
        var mouse = Mouse.current;
        if (mouse != null)
            for (int i = 0; i < rows.Length; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(rows[i].rectTransform, mouse.position.ReadValue(), null))
                {
                    if (mouse.delta.ReadValue().sqrMagnitude > 0f) selected = i;
                    if (mouse.leftButton.wasPressedThisFrame) { selected = i; Pick(i, 0); }
                }
        if (selected != was) circleAt = Time.unscaledTime;
        if (MenuControls.Confirm()) Pick(selected, 0);
        if (settingsPage && selected < 4)
        {
            int delta = MenuControls.Move(1, 3, vertical: false) - 1;
            if (delta != 0) Pick(selected, delta);
        }
        Draw();
    }

    public void Toggle()
    {
        if (IsPaused) Resume();
        else if (IntroCard.IsShowing || PartCard.IsShowing) return;
        else if (GameManager.Instance == null || GameManager.Instance.State != GameState.Results) Pause();
    }

    public void Pause()
    {
        IsPaused = true;
        selected = 0;
        settingsPage = false;
        circleAt = Time.unscaledTime;
        Time.timeScale = 0f;
        if (canvasGo != null) canvasGo.SetActive(true);
        Refresh();
    }

    public void Resume()
    {
        if (settingsPage) GameSettings.Save();
        IsPaused = false;
        settingsPage = false;
        Time.timeScale = 1f;
    }

    void Pick(int item, int delta)
    {
        if (settingsPage)
        {
            float step = delta == 0 ? 0.1f : delta * 0.1f;
            switch (item)
            {
                case 0: GameSettings.Master = Wrap(GameSettings.Master, step, delta); break;
                case 1: GameSettings.Music = Wrap(GameSettings.Music, step, delta); break;
                case 2: GameSettings.Effects = Wrap(GameSettings.Effects, step, delta); break;
                case 3: GameSettings.CameraTurn = Wrap(GameSettings.CameraTurn, step, delta); break;
                default: OpenSettings(false); return;
            }
            Refresh();
            return;
        }
        switch (item)
        {
            case 0: Resume(); break;
            case 1: GameManager.Restart(); break;
            case 2: OpenSettings(true); break;
            case 3: GameManager.LoadScene(menuScene); break;
            default: Quit(); break;
        }
    }

    /// <summary>Left/right step a setting; Enter (no direction) steps it up and wraps round from full to off.</summary>
    static float Wrap(float v, float step, int delta)
    {
        float n = Mathf.Round((v + step) * 10f) / 10f;
        if (delta == 0 && n > 1.001f) n = 0f;
        return Mathf.Clamp01(n);
    }

    void OpenSettings(bool open)
    {
        if (!open) GameSettings.Save();
        settingsPage = open;
        selected = open ? 0 : 2;
        circleAt = Time.unscaledTime;
        Refresh();
    }

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Look ----------

    void Refresh()
    {
        if (heading == null) return;
        heading.text = settingsPage ? "Settings" : "Ship's Log";
        subheading.text = settingsPage ? "Left and right to change, Esc to go back" : "Night watch  ·  the voyage is paused";
        for (int i = 0; i < 5; i++)
            rows[i].text = settingsPage ? (i < 4 ? $"{SettingNames[i]}     {Value(i)}" : "Back") : Items[i];
        footer.text = settingsPage ? "" : VoyageSoFar();
    }

    static string Value(int i)
    {
        float v = i switch { 0 => GameSettings.Master, 1 => GameSettings.Music, 2 => GameSettings.Effects, _ => GameSettings.CameraTurn };
        if (i == 3) return v < 0.34f ? "gentle" : v < 0.67f ? "steady" : "quick";
        return Mathf.RoundToInt(v * 100f) + "%";
    }

    static string VoyageSoFar()
    {
        var gm = GameManager.Instance;
        string part = gm == null ? "" : gm.Part switch { 1 => "Part One", 2 => "Part Two", 3 => "Part Three", _ => "" };
        int done = RunRecord.Choices.Count;
        int secs = Mathf.Max(0, Mathf.FloorToInt(Time.time - RunRecord.RunStartTime));
        return $"{part}  ·  order {Mathf.Min(done + 1, 12)} of 12  ·  {secs / 60:00}:{secs % 60:00} at sea";
    }

    void Draw()
    {
        for (int i = 0; i < rows.Length; i++)
        {
            bool sel = i == selected;
            rows[i].color = sel ? Ink : InkSoft;
            rows[i].fontSize = sel ? 50 : 46;
        }
        // The red ink circle round the chosen line, drawn on with a little pop.
        var row = rows[selected].rectTransform;
        circle.anchoredPosition = row.anchoredPosition;
        float pop = Mathf.Clamp01((Time.unscaledTime - circleAt) / 0.18f);
        float w = Mathf.Clamp(rows[selected].preferredWidth + 90f, 220f, 560f);
        circle.sizeDelta = new Vector2(w, 74f) * Mathf.Lerp(0.85f, 1f, pop);
        circle.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
    }

    void Build()
    {
        canvasGo = new GameObject("[PauseMenu]", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = canvasGo.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        var root = (RectTransform)canvasGo.transform;

        var shade = new GameObject("Shade", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        shade.transform.SetParent(root, false);
        shade.color = new Color(0.02f, 0.03f, 0.06f, 0.72f);
        var sr = shade.rectTransform;
        sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = sr.offsetMax = Vector2.zero;

        paperTex = PaintPage();
        var paper = new GameObject("Page", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        paper.transform.SetParent(root, false);
        paper.texture = paperTex;
        paper.raycastTarget = false;
        page = paper.rectTransform;
        page.sizeDelta = new Vector2(760f, 880f);
        page.localRotation = Quaternion.Euler(0f, 0f, -2.5f);

        var pirata = Resources.Load<Font>("Fonts/PirataOne-Regular");
        var hand = Resources.Load<Font>("Fonts/PatrickHand-Regular") ?? pirata;
        heading = Label(page, pirata, 70, Ink, new Vector2(0f, 370f));
        subheading = Label(page, hand, 28, InkSoft, new Vector2(0f, 318f));

        circleTex = PaintCircle();
        var c = new GameObject("Circle", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        c.transform.SetParent(page, false);
        c.texture = circleTex;
        c.color = RedInk;
        c.raycastTarget = false;
        circle = c.rectTransform;

        for (int i = 0; i < 5; i++) rows[i] = Label(page, hand, 46, InkSoft, new Vector2(0f, 230f - i * 92f));
        footer = Label(page, hand, 28, InkSoft, new Vector2(0f, -300f));
        Label(page, hand, 30, InkSoft, new Vector2(180f, -380f)).text = "the newcomer";
        Refresh();
        canvasGo.SetActive(false);
    }

    static Text Label(RectTransform parent, Font font, int size, Color color, Vector2 pos)
    {
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font; t.fontSize = size; t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = new Vector2(640f, size * 1.5f);
        return t;
    }

    /// <summary>A cream log page: ruled lines, a red margin, a soft edge and a shadow.</summary>
    static Texture2D PaintPage()
    {
        var p = new EndingPainter(760, 880, new Color(0, 0, 0, 0));
        p.RoundRect(14, 20, 752, 876, 10, new Color(0, 0, 0, 0.45f), 12f);
        p.RoundRect(8, 8, 748, 868, 8, new Color(0.95f, 0.91f, 0.8f));
        p.RoundRectOutline(8, 8, 748, 868, 8, 3, new Color(0.59f, 0.46f, 0.3f));
        for (float y = 150; y < 850; y += 46) p.Line(new Vector2(56, y), new Vector2(720, y), 2, new Color(0.67f, 0.75f, 0.8f, 0.55f));
        p.Line(new Vector2(108, 18), new Vector2(108, 858), 2, new Color(0.8f, 0.36f, 0.32f, 0.6f));
        var rng = new System.Random(5);
        for (int i = 0; i < 90; i++)
            p.Circle(20 + (float)rng.NextDouble() * 720, 20 + (float)rng.NextDouble() * 840, 1.4f, new Color(0.7f, 0.6f, 0.45f, 0.25f));
        return p.ToTexture();
    }

    /// <summary>A loose hand-drawn ring: stretched wide, it's the ink circle round the chosen line.</summary>
    static Texture2D PaintCircle()
    {
        var p = new EndingPainter(256, 128, new Color(0, 0, 0, 0));
        // Two slightly offset passes, like a pen going round twice.
        for (int pass = 0; pass < 2; pass++)
        {
            Vector2 prev = Vector2.zero;
            for (int k = 0; k <= 64; k++)
            {
                float a = k / 64f * Mathf.PI * 2f * (pass == 0 ? 1f : 0.9f) + pass * 0.4f;
                var pt = new Vector2(128 + Mathf.Cos(a) * (118 - pass * 4), 64 + Mathf.Sin(a) * (54 - pass * 3));
                if (k > 0) p.Line(prev, pt, pass == 0 ? 5f : 3f, Color.white);
                prev = pt;
            }
        }
        return p.ToTexture();
    }
}
