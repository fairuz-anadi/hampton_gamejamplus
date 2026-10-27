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
    Image subtitleIcon;
    Button beginButton;
    AudioSource uiAudio;
    bool interactive;
    float letterbox;
    Coroutine fadeRoutine, subtitleRoutine;

    public bool SettingsOpen { get; private set; }
    public bool CreditsOpen => credits != null && credits.gameObject.activeSelf;

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
        subtitleLine = Label(t, "Line", italicFont, 40, Cream, "", TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(1500f, 120f));
        AddShadow(subtitleLine.gameObject, 0.85f);
        AddShadow(subtitleSpeaker.gameObject, 0.7f);
        subtitleGroup.alpha = 0f;
    }

    void BuildSkip(RectTransform root)
    {
        skipGroup = Group(root, "Skip", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-150f, 46f), new Vector2(260f, 44f));
        var b = TextButton((RectTransform)skipGroup.transform, "SkipIntro", "Skip intro   ›", 24, Vector2.zero, new Vector2(260f, 44f), () => SkipIntro?.Invoke(), withMarker: false);
        var hint = Label((RectTransform)skipGroup.transform, "Hint", bodyFont, 16, DimCream, "Esc  ·  Start", TextAnchor.MiddleRight, new Vector2(1f, 0f), new Vector2(-110f, -14f), new Vector2(220f, 22f));
        hint.color = new Color(DimCream.r, DimCream.g, DimCream.b, 0.6f);
        b.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    void BuildMenu(RectTransform root)
    {
        menuGroup = Group(root, "Menu", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var m = (RectTransform)menuGroup.transform;

        // A soft shadow down the left, so the type reads over the lit deck without boxing the scene in.
        var shade = new GameObject("Shade", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        shade.transform.SetParent(m, false);
        shade.texture = Gradient();
        shade.color = new Color(0.02f, 0.025f, 0.04f, 0.88f);
        shade.raycastTarget = false;
        var st = shade.rectTransform;
        st.anchorMin = Vector2.zero; st.anchorMax = new Vector2(0.62f, 1f);
        st.offsetMin = st.offsetMax = Vector2.zero;

        titleGroup = Group(m, "Title", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f + 430f, -250f), new Vector2(860f, 300f));
        var tt = (RectTransform)titleGroup.transform;
        Label(tt, "Overline", bodyFont, 22, Brass, "A   T A L E   O F   T H E   S E A", TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(430f, -24f), new Vector2(860f, 30f));
        var title = Label(tt, "Title", titleFont, 104, Cream, "THE PROMISED PORT", TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(550f, -112f), new Vector2(1100f, 130f));
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        AddShadow(title.gameObject, 0.6f);
        // Brass rule with a diamond, like a ship's nameboard.
        var rule = Panel(tt, "Rule", new Color(Brass.r, Brass.g, Brass.b, 0.8f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -188f), new Vector2(520f, 2f), new Vector2(0f, 0.5f));
        var gem = new GameObject("Diamond", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        gem.transform.SetParent(tt, false);
        gem.sprite = diamond; gem.color = Brass;
        Place(gem.rectTransform, new Vector2(0f, 1f), new Vector2(530f, -188f), new Vector2(16f, 16f));
        Label(tt, "Subtitle", italicFont, 36, DimCream, "Every voyage begins with trust.", TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(430f, -236f), new Vector2(860f, 50f));

        mainButtons = Group(m, "Buttons", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f + 260f, -110f), new Vector2(520f, 260f));
        var bt = (RectTransform)mainButtons.transform;
        beginButton = TextButton(bt, "BeginVoyage", "BEGIN VOYAGE", 44, new Vector2(0f, 80f), new Vector2(520f, 70f), OnBeginClicked);
        var settings = TextButton(bt, "Settings", "SETTINGS", 34, new Vector2(0f, 0f), new Vector2(520f, 58f), () => OpenSettings(true));
        var quit = TextButton(bt, "Quit", "QUIT", 34, new Vector2(0f, -66f), new Vector2(520f, 58f), () => { Click(); Quit?.Invoke(); });
        Chain(beginButton, settings, quit);

        crewStrip = Label(m, "Crew", bodyFont, 22, DimCream, "", TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(150f + 520f, 70f), new Vector2(1040f, 34f));
        var creditsLink = TextButton(m, "CreditsLink", "Credits", 22, Vector2.zero, new Vector2(160f, 36f), ToggleCredits, withMarker: false);
        Place((RectTransform)creditsLink.transform, new Vector2(1f, 0f), new Vector2(-110f, 66f), new Vector2(160f, 36f));
        creditsLink.navigation = new Navigation { mode = Navigation.Mode.None };
        var creditsAsset = Resources.Load<TextAsset>("Credits");
        credits = Label(m, "CreditsText", bodyFont, 22, Cream, creditsAsset != null ? creditsAsset.text : "Credits.txt is missing.", TextAnchor.UpperLeft, new Vector2(1f, 0.5f), new Vector2(-520f, 0f), new Vector2(860f, 760f));
        credits.horizontalOverflow = HorizontalWrapMode.Wrap;
        credits.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
        credits.gameObject.SetActive(false);

        BuildSettings(m);
    }

    void BuildSettings(RectTransform m)
    {
        settingsGroup = Group(m, "Settings", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f + 330f, -120f), new Vector2(660f, 520f));
        var s = (RectTransform)settingsGroup.transform;
        Label(s, "Heading", titleFont, 40, Cream, "SETTINGS", TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(330f, -26f), new Vector2(660f, 52f));

        var rows = new List<Selectable>();
        float y = -96f;
        rows.Add(SliderRow(s, "Master volume", ref y, GameSettings.Master, v => GameSettings.Master = v, v => Percent(v)));
        rows.Add(SliderRow(s, "Music", ref y, GameSettings.Music, v => GameSettings.Music = v, v => Percent(v)));
        rows.Add(SliderRow(s, "Sound effects", ref y, GameSettings.Effects, v => GameSettings.Effects = v, v => Percent(v)));
        rows.Add(SliderRow(s, "Camera turn speed", ref y, GameSettings.CameraTurn, v => GameSettings.CameraTurn = v, v => v < 0.34f ? "Gentle" : v < 0.67f ? "Steady" : "Quick"));
        var names = QualitySettings.names;
        var quality = SliderRow(s, "Graphics quality", ref y, GameSettings.Quality, v => GameSettings.Quality = Mathf.RoundToInt(v), v => names[Mathf.Clamp(Mathf.RoundToInt(v), 0, names.Length - 1)], 0f, names.Length - 1, true);
        rows.Add(quality);
        rows.Add(SliderRow(s, "Fullscreen", ref y, GameSettings.Fullscreen ? 1f : 0f, v => GameSettings.Fullscreen = v > 0.5f, v => v > 0.5f ? "On" : "Off", 0f, 1f, true));
        y -= 14f;
        var back = TextButton(s, "Back", "BACK", 34, Vector2.zero, new Vector2(520f, 58f), () => OpenSettings(false));
        Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(260f, y), new Vector2(520f, 58f));
        rows.Add(back);
        Chain(rows.ToArray());
        settingsGroupFirst = rows[0];
        SetGroup(settingsGroup, false);
    }

    Selectable settingsGroupFirst;

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
        if (!show) { OpenSettings(false, silent: true); if (credits != null) credits.gameObject.SetActive(false); }
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
        credits.gameObject.SetActive(!credits.gameObject.activeSelf);
    }

    /// <summary>Esc / B while a sub-panel is open. True if it closed something.</summary>
    public bool Back()
    {
        if (SettingsOpen) { OpenSettings(false); return true; }
        if (CreditsOpen) { ToggleCredits(); return true; }
        return false;
    }

    void OnBeginClicked()
    {
        if (!interactive) return;
        if (confirmSound != null) uiAudio.PlayOneShot(confirmSound);
        BeginVoyage?.Invoke();
    }

    void Click() { if (hoverSound != null) uiAudio.PlayOneShot(hoverSound, 0.8f); }

    // ---------- Helpers ----------

    Button TextButton(RectTransform parent, string name, string text, int size, Vector2 pos, Vector2 box, UnityEngine.Events.UnityAction onClick, bool withMarker = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(MenuButtonFx));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, new Vector2(0f, 0.5f), pos + new Vector2(box.x * 0.5f, 0f), box);
        var hit = go.GetComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f); // invisible, just catches the pointer
        var label = Label((RectTransform)go.transform, "Label", size >= 40 ? titleFont : bodyFont, size, DimCream, text,
            withMarker ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, new Vector2(0f, 0.5f), new Vector2(box.x * 0.5f + (withMarker ? 34f : 0f), 0f), box);
        AddShadow(label.gameObject, 0.5f);
        Image marker = null;
        if (withMarker)
        {
            marker = new GameObject("Marker", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            marker.transform.SetParent(go.transform, false);
            marker.sprite = diamond;
            marker.color = Brass;
            Place(marker.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(14f, 14f));
        }
        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(onClick);
        var fx = go.GetComponent<MenuButtonFx>();
        fx.label = label;
        fx.marker = marker;
        fx.normal = DimCream;
        fx.highlight = Cream;
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
        label.rectTransform.anchoredPosition = basePos + new Vector2(8f * s, 0f);
        if (marker != null)
        {
            marker.color = new Color(accent.r, accent.g, accent.b, s);
            marker.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, s);
        }
    }
}
