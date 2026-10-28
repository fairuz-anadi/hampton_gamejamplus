using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The bridge chart up close. Standing at the chart table with nothing in hand and no station in reach, the player
/// gets "E  Study the chart"; E opens the chart full screen (the inked course to the Promised Port, and the Captain's
/// pencil line north to an X marked "G.C."), E again or walking away puts it down. Reads the picture straight off the
/// deck's "Chart" quad, so the art lives in one place. Built in code; spawned by GameSystems in Ship. Owner: Anadi.
/// </summary>
public class ChartView : MonoBehaviour
{
    [Tooltip("How close (metres, flat) a player must stand to the chart to study it.")]
    public float range = 1.9f;
    [Tooltip("Walking this far away puts the chart down.")]
    public float closeRange = 2.6f;
    public float promptHeight = 0.55f;

    /// <summary>The chart is up full screen (name tags and the like stay out of its way).</summary>
    public static bool IsOpen => instance != null && instance.open;
    static ChartView instance;

    Transform chart;
    RectTransform canvasRect, prompt;
    CanvasGroup promptGroup, viewGroup;
    Text promptText, closeText;
    Component reader; // the player studying it, while open
    bool open, wasHeld;

    void Awake() => instance = this;

    void Start()
    {
        var go = GameObject.Find("Chart");
        if (go == null) { enabled = false; return; }
        chart = go.transform;
        var r = go.GetComponent<Renderer>();
        Build(r != null && r.sharedMaterial != null ? r.sharedMaterial.mainTexture : null);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (canvasRect != null) Destroy(canvasRect.gameObject);
    }

    void Update()
    {
        if (chart == null || canvasRect == null) return;
        var gm = GameManager.Instance;
        bool playing = gm != null && gm.State >= GameState.Part1 && gm.State <= GameState.Iceberg
                       && !(PauseMenu.Instance != null && PauseMenu.Instance.IsPaused);

        // Whoever is close enough with free hands. Only humans: the bots never need the chart.
        Component near = null;
        foreach (var c in CrewRegistry.All)
        {
            if (c.IsBot || !(c is Component comp) || !CanStudy(comp)) continue;
            near = comp;
            break;
        }

        if (open && (!playing || reader == null || Flat(reader.transform.position) > closeRange)) SetOpen(false, null);

        var who = open ? reader : near;
        var src = who != null ? who.GetComponent<IInteractSource>() : null;
        bool held = playing && src != null && src.InteractHeld;
        if (held && !wasHeld && who != null) SetOpen(!open, who);
        wasHeld = held;

        bool showPrompt = playing && !open && near != null && Camera.main != null;
        promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, showPrompt ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        viewGroup.alpha = Mathf.MoveTowards(viewGroup.alpha, open ? 1f : 0f, Time.unscaledDeltaTime * 5f);
        if (showPrompt)
        {
            promptText.text = $"{HudText.KeyFor(HudText.Key.Interact, near)}   Study the chart";
            Vector3 sp = Camera.main.WorldToScreenPoint(chart.position + Vector3.up * promptHeight);
            if (sp.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out var local))
                prompt.anchoredPosition = local;
        }
    }

    bool CanStudy(Component who)
    {
        if (Flat(who.transform.position) > range) return false;
        var interactor = who.GetComponent<Interactor>();
        return interactor == null || (!interactor.IsCarrying && interactor.NearestStation == null);
    }

    float Flat(Vector3 p)
    {
        Vector3 d = p - chart.position;
        d.y = 0f;
        return d.magnitude;
    }

    void SetOpen(bool on, Component who)
    {
        open = on;
        reader = on ? who : null;
        if (on) closeText.text = $"{HudText.KeyFor(HudText.Key.Interact, who)}   Put it down";
    }

    void Build(Texture chartTex)
    {
        var theme = UiTheme.Load();
        var font = theme != null && theme.body != null ? theme.body : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var hand = Resources.Load<Font>("Fonts/PatrickHand-Regular") ?? font;

        var canvas = new GameObject("[ChartView]", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45; // over the HUD, under the pause menu
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)canvas.transform;

        // The prompt over the table.
        prompt = new GameObject("Prompt", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        prompt.SetParent(canvasRect, false);
        prompt.sizeDelta = new Vector2(320f, 48f);
        promptGroup = prompt.GetComponent<CanvasGroup>();
        promptGroup.alpha = 0f;
        promptGroup.blocksRaycasts = false;
        Box(prompt, new Color(0.03f, 0.04f, 0.07f, 0.72f), Vector2.zero, new Vector2(320f, 48f));
        promptText = Label(prompt, font, 24, new Color(0.97f, 0.93f, 0.84f), Vector2.zero, new Vector2(300f, 44f));

        // The chart, full screen.
        var view = new GameObject("View", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        view.SetParent(canvasRect, false);
        view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one;
        view.offsetMin = view.offsetMax = Vector2.zero;
        viewGroup = view.GetComponent<CanvasGroup>();
        viewGroup.alpha = 0f;
        viewGroup.blocksRaycasts = false;
        var dim = Box(view, new Color(0.02f, 0.03f, 0.05f, 0.82f), Vector2.zero, Vector2.zero);
        dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one;
        float w = 1500f, h = chartTex != null ? w * chartTex.height / chartTex.width : 980f;
        Box(view, new Color(0f, 0f, 0f, 0.5f), new Vector2(10f, -12f), new Vector2(w + 12f, h + 12f));
        var img = new GameObject("Chart", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        img.transform.SetParent(view, false);
        img.texture = chartTex;
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = new Vector2(w, h);
        img.rectTransform.anchoredPosition = new Vector2(0f, 20f);
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -1.2f);
        closeText = Label(view, hand, 30, new Color(0.97f, 0.93f, 0.84f), new Vector2(0f, -h * 0.5f - 30f), new Vector2(600f, 44f));
    }

    static Image Box(RectTransform parent, Color c, Vector2 pos, Vector2 size)
    {
        var img = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = c;
        img.raycastTarget = false;
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = size;
        return img;
    }

    static Text Label(RectTransform parent, Font font, int size, Color c, Vector2 pos, Vector2 box)
    {
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font; t.fontSize = size; t.color = c;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = box;
        return t;
    }
}
