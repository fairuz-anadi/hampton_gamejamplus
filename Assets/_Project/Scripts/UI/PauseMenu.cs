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
/// volumes and camera speed as ink sliders: drag them with the mouse, or hold left/right (playtest: clicking a row
/// over and over to step it round was not obvious).
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

    // The settings page's sliders, one per setting row (0-3).
    const int SliderCount = 4;
    const float TrackWidth = 200f, TrackX = 140f, ValueX = 305f, LabelX = -110f, LabelWidth = 320f;
    GameObject sliderRoot;
    readonly RectTransform[] tracks = new RectTransform[SliderCount], knobs = new RectTransform[SliderCount];
    readonly RectTransform[] fills = new RectTransform[SliderCount];
    readonly Text[] values = new Text[SliderCount];
    int dragging = -1;
    int holdDir;
    float nextRepeat;
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
                if (dragging < 0 && RectTransformUtility.RectangleContainsScreenPoint(rows[i].rectTransform, mouse.position.ReadValue(), null))
                {
                    if (mouse.delta.ReadValue().sqrMagnitude > 0f) selected = i;
                    // A setting's name only selects it; its slider does the changing.
                    if (mouse.leftButton.wasPressedThisFrame) { selected = i; if (!settingsPage || i >= SliderCount) Pick(i, 0); }
                }
        if (settingsPage) SliderInput(mouse);
        if (selected != was) circleAt = Time.unscaledTime;
        if (MenuControls.Confirm()) Pick(selected, 0);
        if (settingsPage && selected < SliderCount) HoldToChange();
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
        dragging = -1;
        if (settingsPage) GameSettings.Save();
        IsPaused = false;
        settingsPage = false;
        Time.timeScale = 1f;
    }

    void Pick(int item, int delta)
    {
        if (settingsPage)
        {
            if (item >= SliderCount) { OpenSettings(false); return; }
            // Enter or A on a slider does nothing: left/right or the mouse move it.
            if (delta != 0) SetSetting(item, Mathf.Round((GetSetting(item) + delta * 0.05f) * 20f) / 20f);
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

    static float GetSetting(int i) => i switch
    {
        0 => GameSettings.Master, 1 => GameSettings.Music, 2 => GameSettings.Effects, _ => GameSettings.CameraTurn,
    };

    static void SetSetting(int i, float v)
    {
        v = Mathf.Clamp01(v);
        switch (i)
        {
            case 0: GameSettings.Master = v; break;
            case 1: GameSettings.Music = v; break;
            case 2: GameSettings.Effects = v; break;
            default: GameSettings.CameraTurn = v; break;
        }
    }

    /// <summary>Press on a slider (or just beside it) and drag; it follows the mouse until the button comes up.</summary>
    void SliderInput(Mouse mouse)
    {
        if (mouse == null) return;
        var pos = mouse.position.ReadValue();
        if (dragging < 0)
            for (int i = 0; i < SliderCount; i++)
            {
                if (!OverTrack(i, pos)) continue;
                if (mouse.delta.ReadValue().sqrMagnitude > 0f) selected = i;
                if (mouse.leftButton.wasPressedThisFrame) { dragging = i; selected = i; }
            }
        if (dragging < 0) return;
        if (!mouse.leftButton.isPressed) { dragging = -1; GameSettings.Save(); return; }
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(tracks[dragging], pos, null, out var local))
            SetSetting(dragging, Mathf.InverseLerp(-TrackWidth * 0.5f, TrackWidth * 0.5f, local.x));
    }

    bool OverTrack(int i, Vector2 screen)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(tracks[i], screen, null, out var local)) return false;
        return Mathf.Abs(local.x) <= TrackWidth * 0.5f + 18f && Mathf.Abs(local.y) <= 30f;
    }

    /// <summary>Tap left/right for a small step; hold it and the slider keeps moving.</summary>
    void HoldToChange()
    {
        int tap = MenuControls.Step(vertical: false);
        if (tap != 0) { holdDir = tap; nextRepeat = Time.unscaledTime + 0.35f; Pick(selected, tap); return; }
        if (holdDir == 0) return;
        if (!Held(holdDir)) { holdDir = 0; return; }
        if (Time.unscaledTime < nextRepeat) return;
        nextRepeat = Time.unscaledTime + 0.05f;
        Pick(selected, holdDir);
    }

    static bool Held(int dir)
    {
        var kb = Keyboard.current;
        if (kb != null && (dir < 0 ? kb.leftArrowKey.isPressed || kb.aKey.isPressed : kb.rightArrowKey.isPressed || kb.dKey.isPressed)) return true;
        foreach (var pad in Gamepad.all) if (dir < 0 ? pad.dpad.left.isPressed : pad.dpad.right.isPressed) return true;
        return false;
    }

    void OpenSettings(bool open)
    {
        dragging = -1;
        holdDir = 0;
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
        subheading.text = settingsPage ? "Drag a slider, or hold left and right  ·  Esc to go back" : "Night watch  ·  the voyage is paused";
        for (int i = 0; i < 5; i++)
        {
            rows[i].text = settingsPage ? SettingNames[i] : Items[i];
            // On the settings page the names sit on the left, their sliders on the right; Back stays centred.
            bool left = settingsPage && i < SliderCount;
            var r = rows[i].rectTransform;
            rows[i].alignment = left ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            r.anchoredPosition = new Vector2(left ? LabelX : 0f, r.anchoredPosition.y);
            r.sizeDelta = new Vector2(left ? LabelWidth : 640f, r.sizeDelta.y);
        }
        footer.text = settingsPage ? "" : VoyageSoFar();
        if (sliderRoot != null) sliderRoot.SetActive(settingsPage);
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
            bool slim = settingsPage && i < SliderCount;
            rows[i].color = sel ? Ink : InkSoft;
            rows[i].fontSize = slim ? (sel ? 42 : 40) : sel ? 50 : 46;
        }
        if (settingsPage) DrawSliders();
        // The red ink circle round the chosen line, drawn on with a little pop.
        var row = rows[selected].rectTransform;
        var at = row.anchoredPosition;
        // A left-aligned name: ring the words, not the middle of their box.
        if (rows[selected].alignment == TextAnchor.MiddleLeft)
            at.x += (Mathf.Min(rows[selected].preferredWidth, row.sizeDelta.x) - row.sizeDelta.x) * 0.5f;
        circle.anchoredPosition = at;
        float pop = Mathf.Clamp01((Time.unscaledTime - circleAt) / 0.18f);
        float w = Mathf.Clamp(rows[selected].preferredWidth + 90f, 220f, 560f);
        circle.sizeDelta = new Vector2(w, 74f) * Mathf.Lerp(0.85f, 1f, pop);
        circle.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
    }

    void DrawSliders()
    {
        for (int i = 0; i < SliderCount; i++)
        {
            float v = GetSetting(i);
            bool sel = i == selected;
            fills[i].sizeDelta = new Vector2(TrackWidth * v, fills[i].sizeDelta.y);
            knobs[i].anchoredPosition = new Vector2(-TrackWidth * 0.5f + TrackWidth * v, 0f);
            knobs[i].sizeDelta = Vector2.one * (sel || dragging == i ? 24f : 20f);
            values[i].text = Value(i);
            values[i].color = sel ? Ink : InkSoft;
        }
    }

    void BuildSliders(Font hand)
    {
        sliderRoot = new GameObject("Sliders", typeof(RectTransform));
        var root = (RectTransform)sliderRoot.transform;
        root.SetParent(page, false);
        root.sizeDelta = Vector2.zero;
        for (int i = 0; i < SliderCount; i++)
        {
            float y = 230f - i * 92f; // level with its row
            var track = Bar(root, new Color(InkSoft.r, InkSoft.g, InkSoft.b, 0.35f), new Vector2(TrackX, y), new Vector2(TrackWidth, 6f));
            tracks[i] = track;
            // The inked part, from the left end up to the knob.
            var fill = Bar(track, RedInk, Vector2.zero, new Vector2(0f, 6f));
            fill.anchorMin = fill.anchorMax = fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = Vector2.zero;
            fills[i] = fill;
            var knob = Bar(track, Ink, Vector2.zero, new Vector2(20f, 20f));
            knob.localRotation = Quaternion.Euler(0f, 0f, 45f); // a small ink diamond
            knobs[i] = knob;
            values[i] = Label(root, hand, 34, InkSoft, new Vector2(ValueX, y));
            values[i].rectTransform.sizeDelta = new Vector2(100f, 50f);
        }
        sliderRoot.SetActive(false);
    }

    static RectTransform Bar(RectTransform parent, Color c, Vector2 pos, Vector2 size)
    {
        var img = new GameObject("Bar", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = c;
        img.raycastTarget = false;
        var r = img.rectTransform;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        return r;
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
        BuildSliders(hand);
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
