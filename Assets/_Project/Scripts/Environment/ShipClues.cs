using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The small "wait, what?" clues around the ship (Anadi, 28 Oct), all following the final plan:
///   The bottle: from Part 2 a bottle stands by the bridge voice pipe. E on it: "It's full. He hasn't drunk a drop."
///   The slip: once in Part 2, between lines, the Captain's strip reads "...we'll make Grey Cove..." and flickers
///             into "the Promised Port".
///   The clock: the wireless room's wall clock keeps time in Part 1 and runs backwards from Part 2.
///   The binnacle: a needle on the bridge compass swings off the bow as OffMoments' compass drifts north.
///   The chart: the bridge chart's pencil course grows each part (Resources/BridgeChart/chart_part1..3).
///   The message: in Part 2 a torn telegram meant for the Captain lies on the wireless room floor, glinting. E picks
///             it up and reads it full screen; putting it down, the player thinks "Wait. What?" (PlayerThought).
///   The mood: a cold vignette that breathes in Part 2 and closes in, red-tinged, in Part 3.
/// Built in code from what's already on the deck. Spawned by GameSystems in Ship. Owner: Anadi.
/// </summary>
public class ShipClues : MonoBehaviour
{
    [Header("The bottle")]
    public float bottleRange = 1.8f;
    public string bottleLine = "It's full. He hasn't drunk a drop.";
    [Header("The slip")]
    [Tooltip("Seconds into Part 2 before he slips (at the next quiet moment).")]
    public float slipAfter = 45f;
    [Header("The message")]
    public float messageAfter = 20f;
    public string message = "TO THE CAPTAIN ONLY.  BOATS READY AT G.C.  NO WITNESSES.";

    static ShipClues instance;
    Transform ship, bottle, minuteHand, hourHand, needle;
    Renderer chartRenderer;
    Texture2D[] chartPages = new Texture2D[3];
    int chartShown = -1;
    float part2At = -1f;
    bool slipped, messageShown;
    float slipAt = -1f, messageAt = -1f;
    Component reader;
    bool wasHeld;

    // The bottle's prompt and note.
    RectTransform canvasRect, prompt, note;
    CanvasGroup promptGroup, noteGroup;
    Text promptText;
    float noteUntil;

    /// <summary>A player this close to the bottle (in Part 2+) is looking at it, not talking to the Captain.</summary>
    public static bool NearBottle(Vector3 p) => instance != null && instance.BottleOut && Flat(p, instance.bottle.position) < instance.bottleRange;

    bool BottleOut => bottle != null && bottle.gameObject.activeSelf;

    void Awake() => instance = this;

    void Start()
    {
        var mood = FindAnyObjectByType<PartMood>();
        ship = mood != null ? mood.transform : null;
        for (int i = 0; i < 3; i++) chartPages[i] = Resources.Load<Texture2D>($"BridgeChart/chart_part{i + 1}");
        // The chart quad on the table (not ChartView's full-screen image, also called "Chart").
        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            if (r.name == "Chart") { chartRenderer = r; break; }
        var clock = GameObject.Find("WirelessClock")?.transform;
        if (clock != null) { minuteHand = clock.Find("MinuteHand"); hourHand = clock.Find("HourHand"); }
        BuildBottle();
        BuildNeedle();
        BuildUi();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (canvasRect != null) Destroy(canvasRect.gameObject);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        int part = Mathf.Clamp(gm.Part, 1, 3);
        if (gm.State >= GameState.Iceberg) part = 3;
        if (part >= 2 && part2At < 0f) part2At = Time.time;
        float inPart2 = part2At < 0f ? -1f : Time.time - part2At;

        Chart(part);
        Clock(part);
        Needle();
        Bottle(part, gm);
        Slip(part, inPart2);
        Message(part, inPart2);
        Mood(part);
    }

    // ---------- The chart ----------

    void Chart(int part)
    {
        if (chartRenderer == null || chartShown == part || chartPages[part - 1] == null) return;
        chartShown = part;
        chartRenderer.material.mainTexture = chartPages[part - 1]; // an instance: the asset stays as it is
    }

    // ---------- The wireless room's clock ----------

    void Clock(int part)
    {
        if (minuteHand == null) return;
        // Part 1: an ordinary minute hand. Part 2 on: backwards, a little too fast, catching now and then.
        float speed = part == 1 ? -6f : 30f * (0.6f + Mathf.PerlinNoise(Time.time * 0.7f, 0.2f));
        minuteHand.Rotate(0f, 0f, speed * Time.deltaTime, Space.Self);
        if (hourHand != null) hourHand.Rotate(0f, 0f, speed / 12f * Time.deltaTime, Space.Self);
    }

    // ---------- The binnacle compass ----------

    void BuildNeedle()
    {
        var compass = GameObject.Find("Binnacle")?.transform.Find("Compass");
        if (compass == null) return;
        var lit = Resources.Load<Material>("RuntimeLit");
        needle = new GameObject("CompassNeedle").transform;
        needle.SetParent(compass.parent, false);
        needle.localPosition = compass.localPosition + Vector3.up * 0.19f;
        var red = Part(needle, PrimitiveType.Cube, new Vector3(0f, 0f, 0.1f), new Vector3(0.035f, 0.02f, 0.2f), lit, new Color(0.8f, 0.15f, 0.12f));
        Part(needle, PrimitiveType.Cube, new Vector3(0f, 0f, -0.09f), new Vector3(0.035f, 0.02f, 0.18f), lit, new Color(0.9f, 0.88f, 0.82f));
        Part(needle, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.05f, 0.015f, 0.05f), lit, new Color(0.8f, 0.6f, 0.26f));
    }

    void Needle()
    {
        var off = OffMoments.Instance;
        if (needle == null || off == null) return;
        // North of the ship's heading is to starboard: as the compass reads further north than the wheel, the needle
        // swings that way. Shown three times as large so it can be seen from the bridge.
        float drift = Mathf.DeltaAngle(off.WheelHeading, off.CompassHeading) * 3f;
        needle.localRotation = Quaternion.Euler(0f, drift + Mathf.Sin(Time.time * 1.3f) * 1.5f, 0f);
    }

    // ---------- The bottle ----------

    void BuildBottle()
    {
        // On top of the bridge cabinet, away from the wheel so E there means the bottle.
        if (ship == null) return;
        Transform cabinet = null;
        foreach (var t in ship.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("cabinet_medium") && !t.name.Contains("decorated") && (cabinet == null || t.position.z > cabinet.position.z)) cabinet = t;
        if (cabinet == null) return;
        var bounds = new Bounds(cabinet.position, Vector3.zero);
        foreach (var r in cabinet.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
        var lit = Resources.Load<Material>("RuntimeLit");
        bottle = new GameObject("CaptainsBottle").transform;
        bottle.SetParent(ship, true);
        bottle.position = new Vector3(bounds.center.x - 0.15f, bounds.max.y, bounds.center.z + 0.2f);
        bottle.rotation = ship.rotation;
        Part(bottle, PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0f), new Vector3(0.12f, 0.15f, 0.12f), lit, new Color(0.18f, 0.32f, 0.16f));
        Part(bottle, PrimitiveType.Cylinder, new Vector3(0f, 0.33f, 0f), new Vector3(0.05f, 0.05f, 0.05f), lit, new Color(0.18f, 0.32f, 0.16f));
        Part(bottle, PrimitiveType.Cylinder, new Vector3(0f, 0.39f, 0f), new Vector3(0.045f, 0.02f, 0.045f), lit, new Color(0.55f, 0.38f, 0.22f));
        Part(bottle, PrimitiveType.Cube, new Vector3(0f, 0.15f, 0.061f), new Vector3(0.08f, 0.1f, 0.004f), lit, new Color(0.92f, 0.86f, 0.7f));
        ClueCloseUp.DressBottle(bottle); // (Samprity) looks like the close-up's bottle: dark green, empty, still sealed
        bottle.gameObject.SetActive(false);
    }

    void Bottle(int part, GameManager gm)
    {
        if (bottle == null) return;
        if (bottle.gameObject.activeSelf != (part >= 2)) bottle.gameObject.SetActive(part >= 2);

        bool playing = gm.State >= GameState.Part1 && gm.State <= GameState.Part3 && !(PauseMenu.Instance != null && PauseMenu.Instance.IsPaused);
        Component near = null;
        if (BottleOut && playing)
            foreach (var c in CrewRegistry.All)
            {
                if (c.IsBot || !(c is Component comp) || Flat(comp.transform.position, bottle.position) > bottleRange) continue;
                var it = comp.GetComponent<Interactor>();
                if (it != null && (it.IsCarrying || it.NearestStation != null)) continue;
                near = comp;
                break;
            }

        var src = near != null ? near.GetComponent<IInteractSource>() : null;
        bool held = src != null && src.InteractHeld;
        // (Samprity) E eases the camera in on the bottle, with "It was never full." on a note at the right. Its own E
        // (or walking off) puts it down, so a press while it's up doesn't open it again.
        if (held && !wasHeld && !ClueCloseUp.IsOpen) ClueCloseUp.ShowBottle(bottle, near);
        wasHeld = held;

        float dt = Time.unscaledDeltaTime;
        bool noteUp = Time.time < noteUntil && near != null;
        bool promptUp = near != null && !noteUp && !ClueCloseUp.IsOpen && Camera.main != null;
        promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, promptUp ? 1f : 0f, dt * 6f);
        noteGroup.alpha = Mathf.MoveTowards(noteGroup.alpha, noteUp ? 1f : 0f, dt * 4f);
        if (promptUp)
        {
            promptText.text = $"{HudText.KeyFor(HudText.Key.Interact, near)}   Look at the bottle";
            Vector3 sp = Camera.main.WorldToScreenPoint(bottle.position + Vector3.up * 0.8f);
            if (sp.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out var local)) prompt.anchoredPosition = local;
        }
    }

    // ---------- The slip ----------

    void Slip(int part, float inPart2)
    {
        if (slipped || part != 2 || inPart2 < slipAfter) return;
        var subs = FindAnyObjectByType<CaptainSubtitles>();
        var dir = CaptainDirector.Instance;
        if (subs == null) { slipped = true; return; }
        if (slipAt < 0f)
        {
            // Wait for a quiet moment between his lines.
            if (dir != null && dir.VisibleText.Length > 0) return;
            slipAt = Time.time;
            subs.ShowAside("Midnight, and we'll make Grey Cove right on time.");
            return;
        }
        float t = Time.time - slipAt;
        if (t < 1.3f) return;
        if (t < 2.4f)
        {
            bool fixedUp = Mathf.PerlinNoise(t * 14f, 0.4f) > 0.75f - (t - 1.3f);
            subs.SetAsideText(fixedUp ? "Midnight, and we'll make the Promised Port right on time." : "Midnight, and we'll make Grey Cove right on time.");
            return;
        }
        subs.SetAsideText("Midnight, and we'll make the Promised Port right on time.");
        if (t > 5f) { subs.ShowAside(""); slipped = true; PlayerThought.Think("Grey Cove? Did he just say Grey Cove?", "slip"); }
    }

    // ---------- The message meant for him ----------

    // ---------- The message meant for him: a torn telegram on the wireless room floor ----------

    Transform paper, glint;
    Component paperReader;
    bool paperWasHeld, paperOpen;
    RectTransform paperView, paperPrompt;
    CanvasGroup paperGroup, paperPromptGroup;
    Text paperPromptText;

    void Message(int part, float inPart2)
    {
        var desk = StationRegistry.Get(StationType.Wireless);
        if (paper == null && desk != null && ship != null) BuildPaper(desk.Position);
        if (paper == null) return;
        bool show = part >= 2 && inPart2 >= messageAfter;
        if (paper.gameObject.activeSelf != show) paper.gameObject.SetActive(show);

        // It catches the light: a slow glint so it's seen lying there.
        if (show) glint.localScale = new Vector3(0.7f, 0.004f, 0.7f) * (0.85f + 0.3f * Mathf.Abs(Mathf.Sin(Time.time * 2f)));

        var gm = GameManager.Instance;
        bool playing = gm.State >= GameState.Part1 && gm.State <= GameState.Part3 && !(PauseMenu.Instance != null && PauseMenu.Instance.IsPaused);
        Component near = null;
        if (show && playing)
            foreach (var c in CrewRegistry.All)
            {
                if (c.IsBot || !(c is Component comp) || Flat(comp.transform.position, paper.position) > 1.7f) continue;
                var it = comp.GetComponent<Interactor>();
                if (it != null && it.IsCarrying) continue;
                near = comp;
                break;
            }
        if (paperOpen && (paperReader == null || Flat(paperReader.transform.position, paper.position) > 3f)) ClosePaper();

        var who = paperOpen ? paperReader : near;
        var src = who != null ? who.GetComponent<IInteractSource>() : null;
        bool held = src != null && src.InteractHeld;
        if (held && !paperWasHeld && who != null)
        {
            if (paperOpen) ClosePaper();
            else { paperOpen = true; paperReader = who; messageShown = true; }
        }
        paperWasHeld = held;

        float dt = Time.unscaledDeltaTime;
        paperGroup.alpha = Mathf.MoveTowards(paperGroup.alpha, paperOpen ? 1f : 0f, dt * 5f);
        paperView.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, paperGroup.alpha);
        bool promptUp = near != null && !paperOpen && Camera.main != null;
        paperPromptGroup.alpha = Mathf.MoveTowards(paperPromptGroup.alpha, promptUp ? 1f : 0f, dt * 6f);
        if (promptUp)
        {
            paperPromptText.text = $"{HudText.KeyFor(HudText.Key.Interact, near)}   {(messageShown ? "Read the telegram again" : "Pick up the paper")}";
            Vector3 sp = Camera.main.WorldToScreenPoint(paper.position + Vector3.up * 0.9f);
            if (sp.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out var local)) paperPrompt.anchoredPosition = local;
        }
    }

    /// <summary>True while the telegram is up full screen.</summary>
    public static bool ReadingPaper => instance != null && instance.paperOpen;

    void ClosePaper()
    {
        paperOpen = false;
        paperReader = null;
        PlayerThought.Think("Wait. What? Boats ready... no witnesses? Who's waiting for us at G.C.?", "telegram");
    }

    /// <summary>A crumpled telegram on the floor of the wireless room, out of reach of the desk's own E.</summary>
    void BuildPaper(Vector3 deskPos)
    {
        var lit = Resources.Load<Material>("RuntimeLit");
        paper = new GameObject("TornTelegram").transform;
        paper.SetParent(ship, true);
        Vector3 local = ship.InverseTransformPoint(deskPos);
        // Toward the middle of the room from the desk, far enough that E here can't reach the desk.
        float side = local.x < 0f ? 1f : -1f;
        paper.localPosition = new Vector3(local.x + side * 3.1f, 0.06f, local.z - 0.6f);
        paper.localRotation = Quaternion.Euler(0f, 23f, 0f);
        Part(paper, PrimitiveType.Cube, Vector3.zero, new Vector3(0.34f, 0.01f, 0.24f), lit, new Color(0.93f, 0.89f, 0.76f)).localRotation = Quaternion.Euler(4f, 0f, -3f);
        glint = Part(paper, PrimitiveType.Cylinder, new Vector3(0f, -0.02f, 0f), new Vector3(0.7f, 0.004f, 0.7f), lit, new Color(1f, 0.85f, 0.5f));
        var gmat = glint.GetComponent<Renderer>().material;
        if (gmat.HasProperty("_EmissionColor")) { gmat.EnableKeyword("_EMISSION"); gmat.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.35f) * 1.6f); }
        paper.gameObject.SetActive(false);

        var theme = UiTheme.Load();
        var font = theme != null && theme.body != null ? theme.body : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        paperPrompt = Group("PaperPrompt", new Vector2(380f, 48f), out paperPromptGroup);
        Box(paperPrompt, new Color(0.03f, 0.04f, 0.07f, 0.72f), new Vector2(380f, 48f));
        paperPromptText = Label(paperPrompt, font, 24, new Color(0.97f, 0.93f, 0.84f), new Vector2(360f, 44f));

        // The telegram, full screen.
        paperView = Group("Telegram", new Vector2(980f, 560f), out paperGroup);
        var img = new GameObject("Form", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        img.transform.SetParent(paperView, false);
        img.texture = TelegramTexture();
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = new Vector2(980f, 560f);
        paperView.localRotation = Quaternion.Euler(0f, 0f, 1.8f);
        var pirata = Resources.Load<Font>("Fonts/PirataOne-Regular") ?? font;
        var typewriter = theme != null && theme.typewriter != null ? theme.typewriter : font;
        var hand = Resources.Load<Font>("Fonts/PatrickHand-Regular") ?? font;
        var ink = new Color(0.2f, 0.14f, 0.1f);
        Place(Label(paperView, pirata, 48, ink, new Vector2(900f, 60f)), new Vector2(0f, 220f)).text = "Marconi Wireless Telegram";
        Place(Label(paperView, typewriter, 22, new Color(0.45f, 0.33f, 0.22f), new Vector2(900f, 30f)), new Vector2(0f, 172f)).text = "RECEIVED 22:41      PRIORITY: PRIVATE";
        var body = Place(Label(paperView, typewriter, 40, ink, new Vector2(820f, 220f)), new Vector2(0f, 20f));
        body.text = message.Replace("  ", "\n");
        body.lineSpacing = 1.25f;
        Place(Label(paperView, hand, 28, new Color(0.7f, 0.2f, 0.16f), new Vector2(600f, 40f)), new Vector2(230f, -200f)).text = "burn after reading";
        Place(Label(paperView, hand, 24, new Color(0.45f, 0.33f, 0.22f), new Vector2(600f, 36f)), new Vector2(-180f, -240f)).text = "E   put it down";
        paperView.anchoredPosition = new Vector2(0f, 40f);
    }

    static Text Place(Text t, Vector2 pos) { t.rectTransform.anchoredPosition = pos; return t; }

    static Texture2D TelegramTexture()
    {
        var p = new EndingPainter(980, 560, new Color(0, 0, 0, 0));
        p.RoundRect(12, 18, 976, 556, 6, new Color(0, 0, 0, 0.45f), 12f);
        p.RoundRect(6, 6, 970, 548, 4, new Color(0.93f, 0.88f, 0.74f));
        p.RoundRectOutline(22, 22, 954, 532, 3, 3, new Color(0.55f, 0.4f, 0.26f));
        p.Line(new Vector2(60, 120), new Vector2(920, 120), 2, new Color(0.55f, 0.4f, 0.26f));
        p.Line(new Vector2(60, 400), new Vector2(920, 400), 2, new Color(0.55f, 0.4f, 0.26f, 0.6f));
        // A few creases and specks.
        p.Line(new Vector2(320, 20), new Vector2(360, 540), 2, new Color(0.7f, 0.62f, 0.5f, 0.5f));
        p.Line(new Vector2(20, 300), new Vector2(960, 280), 2, new Color(0.7f, 0.62f, 0.5f, 0.4f));
        var rng = new System.Random(3);
        for (int i = 0; i < 120; i++) p.Circle(20 + (float)rng.NextDouble() * 940, 20 + (float)rng.NextDouble() * 520, 1.5f, new Color(0.6f, 0.48f, 0.32f, 0.25f));
        return p.ToTexture();
    }

    // ---------- The mood: colder and darker from Part 2 ----------

    RawImage mood;

    /// <summary>A faint cold vignette that breathes in Part 2 and closes in, tinged red, in Part 3.</summary>
    void Mood(int part)
    {
        if (mood == null)
        {
            mood = new GameObject("Mood", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            mood.transform.SetParent(canvasRect, false);
            mood.transform.SetAsFirstSibling();
            mood.raycastTarget = false;
            mood.texture = MoodTexture();
            var r = mood.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        }
        float breathe = 0.5f + 0.5f * Mathf.Sin(Time.time * 0.5f);
        Color target = part switch
        {
            1 => new Color(0.02f, 0.04f, 0.08f, 0f),
            2 => new Color(0.02f, 0.06f, 0.1f, 0.32f + 0.12f * breathe),
            _ => new Color(0.12f, 0.02f, 0.03f, 0.5f + 0.12f * breathe),
        };
        mood.color = Color.Lerp(mood.color, target, Time.deltaTime * 0.6f);
    }

    static Texture2D MoodTexture()
    {
        const int n = 128;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 0.707f;
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.SmoothStep(0.5f, 1.05f, d) * 255f));
            }
        t.SetPixels32(px); t.Apply();
        return t;
    }

    // ---------- Helpers ----------

    static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

    static Transform Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material lit, Color c)
    {
        var g = GameObject.CreatePrimitive(type);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = lit != null ? new Material(lit) : r.sharedMaterial;
        r.material.SetColor("_BaseColor", c);
        return g.transform;
    }

    void BuildUi()
    {
        var theme = UiTheme.Load();
        var font = theme != null && theme.body != null ? theme.body : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var hand = Resources.Load<Font>("Fonts/PatrickHand-Regular") ?? font;
        var canvas = new GameObject("[ShipClues]", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 44;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)canvas.transform;

        prompt = Group("Prompt", new Vector2(340f, 48f), out promptGroup);
        Box(prompt, new Color(0.03f, 0.04f, 0.07f, 0.72f), new Vector2(340f, 48f));
        promptText = Label(prompt, font, 24, new Color(0.97f, 0.93f, 0.84f), new Vector2(320f, 44f));

        note = Group("Note", new Vector2(640f, 120f), out noteGroup);
        note.anchorMin = note.anchorMax = new Vector2(0.5f, 0f);
        note.anchoredPosition = new Vector2(0f, 300f);
        note.localRotation = Quaternion.Euler(0f, 0f, -1.5f);
        Box(note, new Color(0.95f, 0.91f, 0.8f, 0.97f), new Vector2(640f, 110f));
        var t = Label(note, hand, 36, new Color(0.22f, 0.15f, 0.1f), new Vector2(600f, 100f));
        t.text = bottleLine;
    }

    RectTransform Group(string name, Vector2 size, out CanvasGroup g)
    {
        var r = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        r.SetParent(canvasRect, false);
        r.sizeDelta = size;
        g = r.GetComponent<CanvasGroup>();
        g.alpha = 0f;
        g.blocksRaycasts = false;
        return r;
    }

    static void Box(RectTransform parent, Color c, Vector2 size)
    {
        var img = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = c;
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = size;
    }

    static Text Label(RectTransform parent, Font font, int size, Color c, Vector2 box)
    {
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font; t.fontSize = size; t.color = c;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.raycastTarget = false;
        t.rectTransform.sizeDelta = box;
        return t;
    }
}
