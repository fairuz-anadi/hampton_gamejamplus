using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Everything drawn over the opening: letterbox bars, the Captain's subtitles, Skip intro, the title and menu (Begin
/// Voyage, Settings, Quit), the settings panel, the crew-join strip and the black fades. Owner: Samprity.
/// Built in code at Awake from a handful of fonts, sprites and sounds the scene builder assigns, so there is no prefab
/// to keep in sync. OpeningDirector decides what shows when; this only draws and reports clicks.
/// </summary>
public class OpeningMenu : MonoBehaviour
{
    [Header("Look")]
    public Font titleFont;
    public Font bodyFont;
    public Font italicFont;
    public Sprite diamond;
    public Sprite speakerIcon;
    [Tooltip("The title lockup (Tools/make_title_logo.py). Without it the title is set in the title font.")]
    public Sprite titleLogo;
    public Sprite keycap, disc;

    [Header("Sounds")]
    public AudioClip hoverSound;
    public AudioClip confirmSound;

    public event System.Action BeginVoyage, Quit, SkipIntro;

    public static readonly Color Cream = new Color(0.97f, 0.93f, 0.84f);
    public static readonly Color Brass = new Color(0.86f, 0.68f, 0.34f);
    static readonly Color DimCream = new Color(0.78f, 0.74f, 0.66f, 0.85f);

    Canvas canvas;
    CanvasGroup menuGroup, settingsGroup, mainButtons, subtitleGroup, skipGroup, titleGroup;
    RectTransform barTop, barBottom;
    Image fade;
    Text subtitleSpeaker, subtitleLine, crewStrip, credits;
    GameObject creditsPanel, padHints, keyHints;
    Image subtitleIcon;
    Button beginButton;
    AudioSource uiAudio;
    bool interactive;
    float letterbox;
    Coroutine fadeRoutine, subtitleRoutine;

    public bool SettingsOpen { get; private set; }
    public bool CreditsOpen => creditsPanel != null && creditsPanel.activeSelf;

    // ---------- Building ----------

    void Awake()
    {
        uiAudio = gameObject.AddComponent<AudioSource>();
        uiAudio.playOnAwake = false;
        uiAudio.spatialBlend = 0f;
        gameObject.AddComponent<AudioCategory>().kind = AudioCategory.Kind.Effects;

        EnsureEventSystem();
        canvas = new GameObject("OpeningCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvas.transform;

        // Letterbox bars for the cinematic; they slide away when the menu arrives.
        barTop = Panel(root, "LetterboxTop", new Color(0f, 0f, 0f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f)).rectTransform;
        barBottom = Panel(root, "LetterboxBottom", new Color(0f, 0f, 0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f)).rectTransform;
        SetLetterbox(1f);

        BuildSubtitles(root);
        BuildSkip(root);
        BuildMenu(root);

        fade = Panel(root, "Fade", Color.black, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        fade.raycastTarget = false;
        SetFade(1f);
        ShowMenu(false, instant: true);
        ShowSkip(false);
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    void BuildSubtitles(RectTransform root)
    {
        subtitleGroup = Group(root, "Subtitles", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 175f), new Vector2(1500f, 170f));
        var t = (RectTransform)subtitleGroup.transform;
        subtitleIcon = new GameObject("Speaker", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        subtitleIcon.transform.SetParent(t, false);
        subtitleIcon.sprite = speakerIcon;
        subtitleIcon.color = Brass;
        subtitleIcon.preserveAspect = true;
        Place(subtitleIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(-150f, -18f), new Vector2(30f, 30f));
        subtitleSpeaker = Label(t, "Speaker", bodyFont, 24, Brass, "THE CAPTAIN", TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(10f, -18f), new Vector2(560f, 34f));
        // The Captain's opening lines in the typewriter face (Special Elite, from UiTheme), like a ship's log (Anadi).
        var theme = UiTheme.Load();
        var lineFont = theme != null && theme.typewriter != null ? theme.typewriter : italicFont;
        subtitleLine = Label(t, "Line", lineFont, 36, Cream, "", TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(1500f, 120f));
        // Hang the line from its top edge, just under "THE CAPTAIN", so the two never overlap.
        subtitleLine.rectTransform.pivot = new Vector2(0.5f, 1f);
        subtitleLine.rectTransform.anchoredPosition = new Vector2(0f, -42f);
        AddShadow(subtitleLine.gameObject, 0.85f);
        AddShadow(subtitleSpeaker.gameObject, 0.7f);
        subtitleGroup.alpha = 0f;
    }

    void BuildSkip(RectTransform root)
    {
        // Anadi's skip button: a brass-edged pill in the letterbox bar, with running arrows and the intro's progress.
        skipGroup = Group(root, "Skip", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-230f, 54f), new Vector2(360f, 66f));
        SkipIntroButton.Build((RectTransform)skipGroup.transform, bodyFont, () => SkipIntro?.Invoke());
    }

    // Layout, in 1920x1080 reference pixels: the title lockup on the left, the menu right-aligned on the right with
    // a brass band behind the selected item, a fading rule and button hints under it.
    const float Margin = 160f, ColumnWidth = 560f;

    void BuildMenu(RectTransform root)
    {
        menuGroup = Group(root, "Menu", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var m = (RectTransform)menuGroup.transform;

        // Darker round the edges and under the title, so the type reads over the lit deck without boxing it in.
        Stretch(Raw(m, "Vignette", Vignette(), new Color(0.01f, 0.012f, 0.02f, 0.92f)).rectTransform, Vector2.zero, Vector2.one);
        Stretch(Raw(m, "Shade", Gradient(), new Color(0.02f, 0.025f, 0.04f, 0.68f)).rectTransform, Vector2.zero, new Vector2(0.5f, 1f));
        Stretch(Raw(m, "ShadeRight", Ramp(0f, 0.75f), new Color(0.02f, 0.025f, 0.04f, 0.8f)).rectTransform, new Vector2(0.55f, 0f), Vector2.one);

        // Title lockup, left of centre and a little low (clear of the newcomer at the furnace), the tagline under it.
        const float logoW = 760f;
        titleGroup = Group(m, "Title", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Margin - 40f + logoW * 0.5f, -30f), new Vector2(logoW, 400f));
        var tt = (RectTransform)titleGroup.transform;
        if (titleLogo != null)
        {
            var logo = new GameObject("Logo", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            logo.transform.SetParent(tt, false);
            logo.sprite = titleLogo;
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            float h = logoW * titleLogo.rect.height / titleLogo.rect.width;
            Place(logo.rectTransform, new Vector2(0f, 1f), new Vector2(logoW * 0.5f, -h * 0.5f), new Vector2(logoW, h));
            var tagline = Tagline(tt, new Vector2(logoW * 0.5f + 30f, -h - 34f), logoW);
        }
        else
        {
            var title = Label(tt, "Title", titleFont, 104, Cream, "THE PROMISED PORT", TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(550f, -112f), new Vector2(1100f, 130f));
            AddShadow(title.gameObject, 0.6f);
            Tagline(tt, new Vector2(440f, -204f), 880f);
        }

        // The menu: right-aligned, a little below centre.
        mainButtons = Group(m, "Buttons", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Margin - ColumnWidth * 0.5f, -70f), new Vector2(ColumnWidth, 300f));
        var bt = (RectTransform)mainButtons.transform;
        beginButton = TextButton(bt, "BeginVoyage", "Begin Voyage", 42, new Vector2(0f, 105f), new Vector2(ColumnWidth, 60f), OnBeginClicked);
        var settings = TextButton(bt, "Settings", "Settings", 42, new Vector2(0f, 45f), new Vector2(ColumnWidth, 60f), () => OpenSettings(true));
        var creditsButton = TextButton(bt, "Credits", "Credits", 42, new Vector2(0f, -15f), new Vector2(ColumnWidth, 60f), ToggleCredits);
        var quit = TextButton(bt, "Quit", "Quit", 42, new Vector2(0f, -75f), new Vector2(ColumnWidth, 60f), () => { Click(); Quit?.Invoke(); });
        Chain(beginButton, settings, creditsButton, quit);

        // Credits: in the right column, in place of the menu.
        creditsPanel = new GameObject("Credits", typeof(RectTransform), typeof(Image));
        creditsPanel.transform.SetParent(m, false);
        var cp = (RectTransform)creditsPanel.transform;
        var cpBack = creditsPanel.GetComponent<Image>();
        cpBack.color = new Color(0.02f, 0.025f, 0.04f, 0.7f);
        cpBack.raycastTarget = false;
        Place(cp, new Vector2(1f, 0.5f), new Vector2(-Margin - 400f, 60f), new Vector2(800f, 560f));
        var creditsAsset = Resources.Load<TextAsset>("Credits");
        credits = Label(cp, "Text", bodyFont, 22, Cream, creditsAsset != null ? creditsAsset.text : "Credits.txt is missing.", TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(740f, 520f));
        creditsPanel.SetActive(false);

        BuildSettings(m);
        BuildNames(m);
    }

    // ---------- Crew names (Riham): Begin Voyage asks each player's name first; the Captain calls them by it ----------

    CanvasGroup namesGroup;
    readonly List<GameObject> nameRows = new List<GameObject>();
    readonly List<InputField> nameFields = new List<InputField>();
    Button setSailButton, namesBackButton;
    int namesShown = -1;

    public bool NamesOpen { get; private set; }

    void BuildNames(RectTransform m)
    {
        namesGroup = Group(m, "Names", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Margin - 330f, 40f), new Vector2(660f, 520f));
        var s = (RectTransform)namesGroup.transform;
        Label(s, "Heading", titleFont, 40, Cream, "Your names", TextAnchor.MiddleRight, new Vector2(0f, 1f), new Vector2(330f, -26f), new Vector2(660f, 52f));
        Label(s, "Note", bodyFont, 24, DimCream, "The Captain will call you by these.", TextAnchor.MiddleRight, new Vector2(0f, 1f), new Vector2(330f, -70f), new Vector2(660f, 34f));

        float y = -130f;
        for (int seat = 1; seat <= PlayerSlots.MaxPlayers; seat++)
        {
            var row = new GameObject("Player" + seat, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(s, false);
            Place(row, new Vector2(0f, 1f), new Vector2(330f, y), new Vector2(660f, 56f));
            Label(row, "Label", bodyFont, 28, Cream, "Player " + seat, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(130f, 0f), new Vector2(260f, 44f));
            nameFields.Add(NameField(row, CrewRegistry.DefaultSeatName(seat)));
            nameRows.Add(row.gameObject);
            y -= 66f;
        }

        y = -130f - 66f * PlayerSlots.MaxPlayers - 20f;
        setSailButton = TextButton(s, "SetSail", "Set Sail", 38, Vector2.zero, new Vector2(660f, 56f), ConfirmNames);
        Place((RectTransform)setSailButton.transform, new Vector2(0f, 1f), new Vector2(330f, y), new Vector2(660f, 56f));
        namesBackButton = TextButton(s, "Back", "Back", 38, Vector2.zero, new Vector2(660f, 56f), () => OpenNames(false));
        Place((RectTransform)namesBackButton.transform, new Vector2(0f, 1f), new Vector2(330f, y - 60f), new Vector2(660f, 56f));
        SetGroup(namesGroup, false);
    }

    /// <summary>A name box. Left empty, the greyed seat name shows, and that's what the Captain calls you.</summary>
    InputField NameField(RectTransform row, string placeholderText)
    {
        var go = new GameObject("Field", typeof(RectTransform), typeof(Image), typeof(InputField));
        go.transform.SetParent(row, false);
        Place((RectTransform)go.transform, new Vector2(0f, 0.5f), new Vector2(470f, 0f), new Vector2(380f, 48f));
        var back = go.GetComponent<Image>();
        back.color = new Color(0.02f, 0.025f, 0.04f, 0.75f);
        var rule = Panel((RectTransform)go.transform, "Rule", Brass, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        rule.rectTransform.sizeDelta = new Vector2(0f, 2f);

        var text = Label((RectTransform)go.transform, "Text", bodyFont, 30, Cream, "", TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(350f, 44f));
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        var hint = Label((RectTransform)go.transform, "Placeholder", italicFont != null ? italicFont : bodyFont, 30, new Color(DimCream.r, DimCream.g, DimCream.b, 0.5f),
            placeholderText, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(350f, 44f));

        var field = go.GetComponent<InputField>();
        field.textComponent = text;
        field.placeholder = hint;
        field.characterLimit = PlayerSlots.MaxNameLength;
        field.lineType = InputField.LineType.SingleLine;
        field.caretColor = Brass;
        field.selectionColor = new Color(Brass.r, Brass.g, Brass.b, 0.35f);
        var colors = field.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(1f, 0.95f, 0.85f); colors.selectedColor = new Color(1f, 0.92f, 0.78f);
        field.colors = colors;
        // Enter in a name field sets sail, the same as the button.
        field.onEndEdit.AddListener(_ =>
        {
            var kb = Keyboard.current;
            if (NamesOpen && kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) ConfirmNames();
        });
        return field;
    }

    /// <summary>Shows one name field per human who'll play (at least one), remembered from last time.</summary>
    public void OpenNames(bool open, bool silent = false, bool selectField = true)
    {
        if (namesGroup == null) return;
        if (!silent) Click();
        NamesOpen = open;
        SetGroup(namesGroup, open);
        SetGroup(mainButtons, !open && interactive);
        if (open)
        {
            namesShown = -1;
            RefreshNameRows();
            for (int i = 0; i < nameFields.Count; i++)
            {
                nameFields[i].text = PlayerSlots.NameFor(i + 1);
            }
        }
        if (interactive) StartCoroutine(SelectNextFrame(open ? (selectField ? nameFields[0] : setSailButton) : beginButton));
    }

    /// <summary>Rows follow how many have joined, so a friend pressing Start while this is open gets a field.</summary>
    void RefreshNameRows()
    {
        int count = Mathf.Clamp(PlayerSlots.HumanCount, 1, nameRows.Count);
        if (count == namesShown) return;
        namesShown = count;
        var chain = new List<Selectable>();
        for (int i = 0; i < nameRows.Count; i++)
        {
            nameRows[i].SetActive(i < count);
            if (i < count) chain.Add(nameFields[i]);
        }
        chain.Add(setSailButton);
        chain.Add(namesBackButton);
        Chain(chain.ToArray());
    }

    void ConfirmNames()
    {
        if (!NamesOpen) return;
        for (int i = 0; i < namesShown && i < nameFields.Count; i++) PlayerSlots.SetName(i + 1, nameFields[i].text);
        PlayerPrefs.Save();
        NamesOpen = false; // the whole menu fades out next, so the main buttons don't come back first
        SetGroup(namesGroup, false);
        if (confirmSound != null) uiAudio.PlayOneShot(confirmSound);
        BeginVoyage?.Invoke();
    }

    /// <summary>"[key] Back   [key] Select", right-aligned under the rule.</summary>
    GameObject Hints(RectTransform parent, Sprite cap, string backKey, string back, string selectKey, string select)
    {
        var row = (RectTransform)new GameObject("Hints", typeof(RectTransform)).transform;
        row.SetParent(parent, false);
        Place(row, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(ColumnWidth, 40f));
        float x = -10f; // from the column's right edge, working leftwards
        foreach (var (key, label) in new[] { (selectKey, select), (backKey, back) })
        {
            var t = Label(row, label, bodyFont, 28, Cream, label, TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(x - 70f, 0f), new Vector2(140f, 36f));
            AddShadow(t.gameObject, 0.6f);
            float w = t.preferredWidth;
            float capW = Mathf.Max(34f, 18f + key.Length * 12f);
            var capImg = new GameObject("Key", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            capImg.transform.SetParent(row, false);
            capImg.sprite = cap;
            capImg.type = cap != null && cap.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            capImg.color = new Color(Cream.r, Cream.g, Cream.b, 0.92f);
            capImg.raycastTarget = false;
            Place(capImg.rectTransform, new Vector2(1f, 0.5f), new Vector2(x - w - 12f - capW * 0.5f, 0f), new Vector2(capW, 34f));
            // The cap sprites are outlines: the key's letters go inside in the same cream.
            Label(capImg.rectTransform, "Glyph", bodyFont, 20, Cream, key, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(capW, 34f));
            x -= w + 12f + capW + 36f;
        }
        return row.gameObject;
    }

    void Update()
    {
        // Hints for what the player holds: a controller's buttons when one is plugged in, else the keyboard's.
        bool pad = Gamepad.current != null;
        if (NamesOpen) RefreshNameRows();
        if (padHints != null && padHints.activeSelf != pad) padHints.SetActive(pad);
        if (keyHints != null && keyHints.activeSelf == pad) keyHints.SetActive(!pad);
    }

    void BuildSettings(RectTransform m)
    {
        settingsGroup = Group(m, "Settings", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Margin - 330f, 40f), new Vector2(660f, 520f));
        var s = (RectTransform)settingsGroup.transform;
        Label(s, "Heading", titleFont, 40, Cream, "Settings", TextAnchor.MiddleRight, new Vector2(0f, 1f), new Vector2(330f, -26f), new Vector2(660f, 52f));

        var rows = new List<Selectable>();
        float y = -96f;
        rows.Add(SliderRow(s, "Master volume", ref y, GameSettings.Master, v => GameSettings.Master = v, v => Percent(v)));
        rows.Add(SliderRow(s, "Music", ref y, GameSettings.Music, v => GameSettings.Music = v, v => Percent(v)));
        rows.Add(SliderRow(s, "Sound effects", ref y, GameSettings.Effects, v => GameSettings.Effects = v, v => Percent(v)));
        rows.Add(SliderRow(s, "Camera speed", ref y, GameSettings.CameraTurn, v => GameSettings.CameraTurn = v, v => v < 0.34f ? "Gentle" : v < 0.67f ? "Steady" : "Quick"));
        var names = QualitySettings.names;
        var quality = SliderRow(s, "Graphics quality", ref y, GameSettings.Quality, v => GameSettings.Quality = Mathf.RoundToInt(v), v => names[Mathf.Clamp(Mathf.RoundToInt(v), 0, names.Length - 1)], 0f, names.Length - 1, true);
        rows.Add(quality);
        rows.Add(SliderRow(s, "Fullscreen", ref y, GameSettings.Fullscreen ? 1f : 0f, v => GameSettings.Fullscreen = v > 0.5f, v => v > 0.5f ? "On" : "Off", 0f, 1f, true));
        y -= 14f;
        var controls = TextButton(s, "Controls", "Controls", 38, Vector2.zero, new Vector2(660f, 56f), () => OpenControls(true));
        Place((RectTransform)controls.transform, new Vector2(0f, 1f), new Vector2(330f, y), new Vector2(660f, 56f));
        rows.Add(controls);
        y -= 60f;
        var back = TextButton(s, "Back", "Back", 38, Vector2.zero, new Vector2(660f, 56f), () => OpenSettings(false));
        Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(330f, y), new Vector2(660f, 56f));
        rows.Add(back);
        Chain(rows.ToArray());
        settingsGroupFirst = rows[0];
        controlsButton = controls;
        SetGroup(settingsGroup, false);
        BuildControls(m);
    }

    /// <summary>The how-to-play card, now kept under Settings > Controls instead of popping up at the start (Anadi).</summary>
    void BuildControls(RectTransform m)
    {
        controlsGroup = Group(m, "Controls", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 760f));
        var c = (RectTransform)controlsGroup.transform;
        var back = Panel(c, "Card", new Color(0.03f, 0.035f, 0.05f, 0.92f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        back.rectTransform.sizeDelta = Vector2.zero;
        IntroCard.BuildManual(c);
        var close = TextButton(c, "Back", "Back", 38, Vector2.zero, new Vector2(300f, 56f), () => OpenControls(false));
        Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(300f, 56f));
        controlsFirst = close;
        SetGroup(controlsGroup, false);
    }

    public bool ControlsOpen { get; private set; }

    public void OpenControls(bool open)
    {
        if (controlsGroup == null) return;
        Click();
        ControlsOpen = open;
        SetGroup(controlsGroup, open);
        SetGroup(settingsGroup, !open);
        SetGroup(titleGroup, !open);
        if (interactive) StartCoroutine(SelectNextFrame(open ? controlsFirst : controlsButton));
    }

    /// <summary>"EVERY VOYAGE BEGINS WITH TRUST." in Bungee, brass, tilted a touch.</summary>
    Text Tagline(RectTransform parent, Vector2 pos, float width)
    {
        var font = Resources.Load<Font>("Fonts/Bungee-Regular");
        var t = Label(parent, "Tagline", font != null ? font : italicFont, font != null ? 30 : 34, Brass, "Every voyage begins with trust.",
            TextAnchor.MiddleLeft, new Vector2(0f, 1f), pos, new Vector2(width, 50f));
        t.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2.5f);
        AddShadow(t.gameObject, 0.8f);
        return t;
    }

    Selectable settingsGroupFirst, controlsFirst, controlsButton;
    CanvasGroup controlsGroup;

    static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

    Slider SliderRow(RectTransform parent, string label, ref float y, float value, System.Action<float> onChange, System.Func<float, string> show,
        float min = 0f, float max = 1f, bool whole = false)
    {
        var row = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(parent, false);
        Place(row, new Vector2(0f, 1f), new Vector2(330f, y), new Vector2(660f, 52f));
        Label(row, "Label", bodyFont, 28, Cream, label, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(150f, 0f), new Vector2(300f, 44f));
        var valueText = Label(row, "Value", bodyFont, 24, Brass, "", TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(-60f, 0f), new Vector2(120f, 44f));

        var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(row, false);
        Place((RectTransform)sliderGo.transform, new Vector2(0f, 0.5f), new Vector2(420f, 0f), new Vector2(220f, 24f));
        var track = Panel((RectTransform)sliderGo.transform, "Track", new Color(1f, 1f, 1f, 0.18f), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));
        track.rectTransform.sizeDelta = new Vector2(0f, 3f);
        var fillArea = new GameObject("FillArea", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(sliderGo.transform, false);
        fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(0f, 3f);
        var fill = Panel(fillArea, "Fill", Brass, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        var handleArea = new GameObject("HandleArea", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(sliderGo.transform, false);
        handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = handleArea.offsetMax = Vector2.zero;
        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        handle.transform.SetParent(handleArea, false);
        handle.sprite = diamond;
        handle.color = Cream;
        handle.rectTransform.sizeDelta = new Vector2(20f, 20f);

        var slider = sliderGo.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = whole;
        slider.value = value;
        var colors = slider.colors;
        colors.normalColor = Cream; colors.highlightedColor = Brass; colors.selectedColor = Brass; colors.pressedColor = Brass;
        slider.colors = colors;
        valueText.text = show(value);
        slider.onValueChanged.AddListener(v => { onChange(v); valueText.text = show(v); });
        y -= 60f;
        return slider;
    }

    // ---------- What the director drives ----------

    public void SetFade(float alpha)
    {
        if (fade == null) return;
        fade.color = new Color(0f, 0f, 0f, alpha);
        fade.raycastTarget = alpha > 0.99f;
    }

    public Coroutine FadeTo(float alpha, float seconds)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(alpha, seconds));
        return fadeRoutine;
    }

    IEnumerator FadeRoutine(float to, float seconds)
    {
        float from = fade.color.a;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            SetFade(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds)));
            yield return null;
        }
        SetFade(to);
    }

    /// <summary>0 = no bars, 1 = full cinematic letterbox.</summary>
    public void SetLetterbox(float amount)
    {
        letterbox = Mathf.Clamp01(amount);
        float h = 1080f * 0.1f * letterbox;
        barTop.sizeDelta = new Vector2(0f, h);
        barBottom.sizeDelta = new Vector2(0f, h);
    }

    public float Letterbox => letterbox;

    public void ShowSkip(bool show) => SetGroup(skipGroup, show, interactable: show);

    /// <summary>Shows a line of the Captain's: speaker label, then the line fading in. Empty text hides it.</summary>
    public void ShowSubtitle(string speaker, string line, bool overSpeaker = false)
    {
        if (subtitleRoutine != null) StopCoroutine(subtitleRoutine);
        subtitleRoutine = StartCoroutine(SubtitleRoutine(speaker, line, overSpeaker));
    }

    IEnumerator SubtitleRoutine(string speaker, string line, bool overSpeaker)
    {
        if (subtitleGroup.alpha > 0.01f)
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime) { subtitleGroup.alpha = Mathf.Lerp(1f, 0f, t / 0.25f); yield return null; }
        subtitleGroup.alpha = 0f;
        if (string.IsNullOrEmpty(line)) yield break;
        subtitleSpeaker.text = speaker;
        subtitleLine.text = line;
        subtitleIcon.enabled = overSpeaker && speakerIcon != null;
        for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime) { subtitleGroup.alpha = Mathf.SmoothStep(0f, 1f, t / 0.45f); yield return null; }
        subtitleGroup.alpha = 1f;
    }

    public void ShowMenu(bool show, bool instant = false)
    {
        interactive = show;
        if (!show) { OpenSettings(false, silent: true); if (NamesOpen) OpenNames(false, silent: true); if (creditsPanel != null) creditsPanel.SetActive(false); }
        if (instant)
        {
            SetGroup(menuGroup, show);
            SetGroup(mainButtons, show);
            if (titleGroup != null) titleGroup.alpha = show ? 1f : 0f;
        }
        else StartCoroutine(MenuRoutine(show));
        if (show) StartCoroutine(SelectNextFrame(beginButton));
    }

    IEnumerator MenuRoutine(bool show)
    {
        menuGroup.blocksRaycasts = show;
        menuGroup.interactable = show;
        if (show)
        {
            // Title first, then the buttons, each sliding up a touch as it fades in.
            menuGroup.alpha = 1f;
            SetGroup(mainButtons, true);
            mainButtons.alpha = 0f;
            titleGroup.alpha = 0f;
            var tp = ((RectTransform)titleGroup.transform).anchoredPosition;
            var bp = ((RectTransform)mainButtons.transform).anchoredPosition;
            for (float t = 0f; t < 2.2f; t += Time.unscaledDeltaTime)
            {
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1.4f));
                float b = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.8f) / 1.2f));
                titleGroup.alpha = a;
                mainButtons.alpha = b;
                ((RectTransform)titleGroup.transform).anchoredPosition = tp + new Vector2(0f, -16f * (1f - a));
                ((RectTransform)mainButtons.transform).anchoredPosition = bp + new Vector2(0f, -16f * (1f - b));
                yield return null;
            }
            ((RectTransform)titleGroup.transform).anchoredPosition = tp;
            ((RectTransform)mainButtons.transform).anchoredPosition = bp;
            titleGroup.alpha = mainButtons.alpha = 1f;
        }
        else
        {
            float from = menuGroup.alpha;
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime) { menuGroup.alpha = Mathf.Lerp(from, 0f, t / 0.6f); yield return null; }
            SetGroup(menuGroup, false);
        }
    }

    IEnumerator SelectNextFrame(Selectable s)
    {
        yield return null;
        if (s != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
    }

    public void SetCrewStrip(string text) { if (crewStrip != null) crewStrip.text = text; }

    public void OpenSettings(bool open, bool silent = false)
    {
        if (settingsGroup == null) return;
        if (!silent) Click();
        SettingsOpen = open;
        SetGroup(settingsGroup, open);
        SetGroup(mainButtons, !open && interactive);
        if (!open) GameSettings.Save();
        if (interactive) StartCoroutine(SelectNextFrame(open ? settingsGroupFirst : beginButton));
    }

    public void ToggleCredits()
    {
        Click();
        bool open = !creditsPanel.activeSelf;
        creditsPanel.SetActive(open);
        SetGroup(mainButtons, !open && interactive);
        if (!open && interactive) StartCoroutine(SelectNextFrame(beginButton));
    }

    /// <summary>Esc / B while a sub-panel is open. True if it closed something.</summary>
    public bool Back()
    {
        if (NamesOpen) { OpenNames(false); return true; }
        if (ControlsOpen) { OpenControls(false); return true; }
        if (SettingsOpen) { OpenSettings(false); return true; }
        if (CreditsOpen) { ToggleCredits(); return true; }
        return false;
    }

    void OnBeginClicked()
    {
        if (!interactive || NamesOpen) return;
        // A controller can't type, so it lands on Set Sail; the keyboard lands in the first name box.
        bool pad = false;
        foreach (var g in Gamepad.all) if (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame) pad = true;
        OpenNames(true, selectField: !pad);
    }

    void Click() { if (hoverSound != null) uiAudio.PlayOneShot(hoverSound, 0.8f); }

    // ---------- Helpers ----------

    /// <summary>
    /// A menu item: right-aligned type over an invisible hit box. With a marker, a brass band fades in behind it, with
    /// a diamond at the band's left end, while it's hovered or selected (MenuButtonFx).
    /// </summary>
    Button TextButton(RectTransform parent, string name, string text, int size, Vector2 pos, Vector2 box, UnityEngine.Events.UnityAction onClick, bool withMarker = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(MenuButtonFx));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, new Vector2(0f, 0.5f), pos + new Vector2(box.x * 0.5f, 0f), box);
        var hit = go.GetComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f); // invisible, just catches the pointer

        RawImage band = null;
        Image marker = null;
        if (withMarker)
        {
            band = Raw((RectTransform)go.transform, "Band", Band(), new Color(Brass.r, Brass.g, Brass.b, 0f));
            Place(band.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(30f, 0f), new Vector2(box.x + 60f, box.y - 6f));
            marker = new GameObject("Marker", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            marker.transform.SetParent(go.transform, false);
            marker.sprite = diamond;
            marker.color = Brass;
            Place(marker.rectTransform, new Vector2(0f, 0.5f), new Vector2(box.x * 0.36f, 0f), new Vector2(18f, 18f));
        }
        var label = Label((RectTransform)go.transform, "Label", bodyFont, size, DimCream, text,
            TextAnchor.MiddleRight, new Vector2(0f, 0.5f), new Vector2(box.x * 0.5f - (withMarker ? 6f : 0f), 0f), box);
        AddShadow(label.gameObject, 0.55f);

        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(onClick);
        var fx = go.GetComponent<MenuButtonFx>();
        fx.label = label;
        fx.marker = marker;
        fx.band = band;
        fx.normal = DimCream;
        fx.highlight = Color.white;
        fx.accent = Brass;
        fx.onHighlight = () => { if (hoverSound != null) uiAudio.PlayOneShot(hoverSound, 0.5f); };
        return button;
    }

    static void Chain(params Selectable[] items)
    {
        for (int i = 0; i < items.Length; i++)
        {
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnUp = items[(i - 1 + items.Length) % items.Length];
            nav.selectOnDown = items[(i + 1) % items.Length];
            if (items[i] is Slider) { nav.selectOnLeft = null; nav.selectOnRight = null; }
            items[i].navigation = nav;
        }
    }

    Text Label(RectTransform parent, string name, Font font, int size, Color color, string text, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.color = color;
        t.text = text;
        t.alignment = align;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        Place(t.rectTransform, anchor, pos, box);
        return t;
    }

    static Image Panel(RectTransform parent, string name, Color color, Vector2 min, Vector2 max, Vector2 pivot)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = color;
        img.raycastTarget = false;
        var r = img.rectTransform;
        r.anchorMin = min; r.anchorMax = max; r.pivot = pivot;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return img;
    }

    static CanvasGroup Group(RectTransform parent, string name, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = min; r.anchorMax = max;
        r.pivot = new Vector2(0.5f, 0.5f);
        if (min == Vector2.zero && max == Vector2.one) { r.offsetMin = r.offsetMax = Vector2.zero; }
        else { r.anchoredPosition = pos; r.sizeDelta = size; }
        return go.GetComponent<CanvasGroup>();
    }

    static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    static void SetGroup(CanvasGroup g, bool on, bool? interactable = null)
    {
        if (g == null) return;
        g.alpha = on ? 1f : 0f;
        g.interactable = interactable ?? on;
        g.blocksRaycasts = interactable ?? on;
    }

    static void AddShadow(GameObject go, float alpha)
    {
        var s = go.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, alpha);
        s.effectDistance = new Vector2(2f, -2f);
    }

    RawImage Raw(RectTransform parent, string name, Texture2D tex, Color color)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        img.transform.SetParent(parent, false);
        img.texture = tex;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static void Stretch(RectTransform r, Vector2 min, Vector2 max)
    {
        r.anchorMin = min; r.anchorMax = max;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    /// <summary>Alpha ramp left to right, from a to b.</summary>
    static Texture2D Ramp(float a, float b)
    {
        var tex = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Ramp" };
        for (int x = 0; x < 256; x++) tex.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.Lerp(a, b, x / 255f)));
        tex.Apply();
        return tex;
    }

    /// <summary>The selection band: clear at the left, full from just past the middle, easing off at the right.</summary>
    static Texture2D Band()
    {
        var tex = new Texture2D(256, 8, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Band" };
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 256; x++)
        {
            float u = x / 255f, v = Mathf.Abs(y / 7f - 0.5f) * 2f;
            float a = Mathf.SmoothStep(0f, 1f, u / 0.6f) * Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(0.75f, 1f, u));
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * (1f - v * v * 0.5f)));
        }
        tex.Apply();
        return tex;
    }

    /// <summary>Dark round the edges, heaviest across the bottom.</summary>
    static Texture2D Vignette()
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Vignette" };
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float u = x / (N - 1f) * 2f - 1f, v = y / (N - 1f) * 2f - 1f;
            float d = Mathf.Sqrt(u * u * 0.8f + v * v);
            float a = Mathf.SmoothStep(0.45f, 1.35f, d) + Mathf.Clamp01(-v - 0.35f) * 0.35f;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
        }
        tex.Apply();
        return tex;
    }

    static Texture2D Gradient()
    {
        var tex = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "MenuShade" };
        for (int x = 0; x < 256; x++)
        {
            float u = x / 255f;
            tex.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.Pow(1f - u, 1.6f)));
        }
        tex.Apply();
        return tex;
    }
}

/// <summary>Hover and selection feedback for the menu's text buttons: brighter type, a brass diamond, a small nudge.</summary>
public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public Text label;
    public Image marker;
    public RawImage band;
    public Color normal, highlight, accent;
    public System.Action onHighlight;

    bool hovered, selected;
    float amount;
    Vector2 basePos;
    bool hasBase;

    public void OnPointerEnter(PointerEventData e)
    {
        hovered = true;
        // Pointer and keyboard share one highlight: hovering selects, so arrows carry on from there.
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
            EventSystem.current.SetSelectedGameObject(gameObject);
        else onHighlight?.Invoke();
    }

    public void OnPointerExit(PointerEventData e) => hovered = false;
    public void OnSelect(BaseEventData e) { selected = true; onHighlight?.Invoke(); }
    public void OnDeselect(BaseEventData e) => selected = false;

    void OnDisable() { hovered = selected = false; amount = 0f; Apply(); }

    void Update()
    {
        float target = hovered || selected ? 1f : 0f;
        amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime * 6f);
        Apply();
    }

    void Apply()
    {
        if (label == null) return;
        if (!hasBase) { basePos = label.rectTransform.anchoredPosition; hasBase = true; }
        float s = Mathf.SmoothStep(0f, 1f, amount);
        label.color = Color.Lerp(normal, highlight, s);
        label.rectTransform.anchoredPosition = basePos + new Vector2(-6f * s, 0f);
        if (band != null) band.color = new Color(accent.r, accent.g, accent.b, 0.62f * s);
        if (marker != null)
        {
            marker.color = new Color(accent.r, accent.g, accent.b, s);
            marker.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, s);
        }
    }
}
