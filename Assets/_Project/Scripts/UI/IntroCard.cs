using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// "How to play" card at the start of the first voyage in Ship.unity. Time is frozen until a player dismisses it,
/// so the first order waits for it (plan: "the first order waits until the tutorial prompt is dismissed").
/// Shown once per session; Play again and Restart skip it. Spawned by GameSystems. Owner: Anadi.
/// Look by Samprity: drawn with uGUI in the menu's type (UiTheme), over a soft shade so the deck stays visible, and
/// it waits for the fade in from Begin Voyage before appearing.
/// </summary>
public class IntroCard : MonoBehaviour
{
    public static IntroCard Instance { get; private set; }
    public static bool IsShowing => Instance != null && Instance.showing;

    static bool shownThisSession;

    bool showing;
    Canvas canvas;
    CanvasGroup group;

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
        Build();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        if (showing) Time.timeScale = 1f;
        if (canvas != null) Destroy(canvas.gameObject);
    }

    void Update()
    {
        if (!showing) return;
        // Appear once the screen has faded in from the menu, not over black.
        bool ready = !OpeningDirector.HandoffPending;
        if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, ready ? 1f : 0f, Time.unscaledDeltaTime * 2.5f);
        if (!ready || group == null || group.alpha < 0.6f) return;

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

    // ---------- Look ----------

    void Build()
    {
        var theme = UiTheme.Load();
        Font F(Font f) => f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        canvas = new GameObject("[IntroCard]", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        var root = (RectTransform)canvas.transform;

        var shade = Box(root, new Color(0.02f, 0.025f, 0.04f, 0.62f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        shade.raycastTarget = false;
        var card = Box(root, new Color(0.03f, 0.035f, 0.05f, 0.82f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 660f));
        var c = card.rectTransform;
        // A hairline brass frame (an Outline effect would flood the whole panel).
        var frame = new Color(UiTheme.Brass.r, UiTheme.Brass.g, UiTheme.Brass.b, 0.5f);
        Box(c, frame, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero).rectTransform.sizeDelta = new Vector2(0f, 2f);
        Box(c, frame, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero).rectTransform.sizeDelta = new Vector2(0f, 2f);
        Box(c, frame, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero).rectTransform.sizeDelta = new Vector2(2f, 0f);
        Box(c, frame, new Vector2(1f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero).rectTransform.sizeDelta = new Vector2(2f, 0f);

        float y = -64f;
        Txt(c, F(theme ? theme.body : null), 22, UiTheme.Brass, "Y O U R   F I R S T   W A T C H", TextAnchor.MiddleCenter, y, 34f); y -= 52f;
        Txt(c, F(theme ? theme.title : null), 62, UiTheme.Cream, "All hands on deck", TextAnchor.MiddleCenter, y, 70f); y -= 64f;
        if (theme != null && theme.diamond != null)
        {
            var rule = Box(c, new Color(UiTheme.Brass.r, UiTheme.Brass.g, UiTheme.Brass.b, 0.7f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(360f, 2f));
            rule.raycastTarget = false;
            var gem = Box(c, UiTheme.Brass, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(14f, 14f));
            gem.sprite = theme.diamond;
        }
        y -= 44f;
        var body = F(theme ? theme.body : null);
        var italic = F(theme ? theme.italic : null);
        Txt(c, italic, 30, UiTheme.DimCream, "The Captain gives each of you orders over the ship's speaker. For every order, choose:", TextAnchor.MiddleCenter, y, 44f); y -= 70f;
        Row(c, body, "OBEY", "Work the station till its light turns green, then report.", "hold E, then R", y); y -= 58f;
        Row(c, body, "FAKE IT", "Report it done while the light is still red. Someone might notice.", "R", y); y -= 58f;
        Row(c, body, "REFUSE", "Say no. Everyone hears it.", "X", y); y -= 76f;
        Txt(c, body, 26, UiTheme.DimCream, "Walk with W A S D or the left stick.  Some jobs need two: call for help with Q.  Esc pauses.", TextAnchor.MiddleCenter, y, 36f); y -= 64f;
        Txt(c, F(theme ? theme.title : null), 30, UiTheme.Brass, "Press  Enter  or  A  to take your post", TextAnchor.MiddleCenter, y, 40f);
    }

    static void Row(RectTransform card, Font font, string choice, string what, string keys, float y)
    {
        Txt(card, font, 30, UiTheme.Brass, choice, TextAnchor.MiddleRight, y, 40f, -390f, 180f);
        Txt(card, font, 27, UiTheme.Cream, what, TextAnchor.MiddleLeft, y, 40f, 30f, 620f);
        Txt(card, font, 24, UiTheme.DimCream, keys, TextAnchor.MiddleRight, y, 40f, 450f, 180f);
    }

    static Text Txt(RectTransform parent, Font font, int size, Color color, string text, TextAnchor align, float y, float h, float x = 0f, float w = 1060f)
    {
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font; t.fontSize = size; t.color = color; t.text = text; t.alignment = align;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow; // Crimson's line height is taller than its size
        var r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
        var s = t.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.6f);
        return t;
    }

    static Image Box(RectTransform parent, Color color, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
    {
        var img = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = color;
        var r = img.rectTransform;
        r.anchorMin = min; r.anchorMax = max;
        if (min == Vector2.zero && max == Vector2.one) { r.offsetMin = r.offsetMax = Vector2.zero; }
        else { r.anchoredPosition = pos; r.sizeDelta = size; }
        return img;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => shownThisSession = false;
}
