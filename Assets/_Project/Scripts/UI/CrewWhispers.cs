using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// When the Captain gives a player a suspicious order (the "off" ones and the last one), a crewmate comes over.
/// They walk up behind the player, tap them on the shoulder (the player flinches), and say their piece in a bubble
/// next to them: usually an honest crewmate doubting the order ("Lock the lifeboats? Who locks the lifeboats?"),
/// sometimes the Stoker pushing them to obey, which is the clue. While they talk, the player is left unsure: a "?"
/// thought cloud bobs over their head, the screen edges darken with a slow heartbeat. If the crewmate can't reach
/// the player in time they just call out from where they are. Owner: Anadi. Spawned by GameSystems in Ship.
/// The walk is driven from here: BotBrain hands the bot over while Holds(bot) is true.
/// </summary>
public class CrewWhispers : MonoBehaviour
{
    [Tooltip("Seconds after the order arrives before the crewmate sets off (the Captain's line comes first).")]
    [Min(0f)] public float delay = 1.5f;
    [Tooltip("Longest a crewmate walks to reach the player before calling out instead.")]
    [Min(1f)] public float approachSeconds = 10f;
    [Min(1f)] public float showSeconds = 5.5f;
    [Tooltip("Chance the Stoker, not an honest crewmate, comes over.")]
    [Range(0f, 1f)] public float stokerChance = 0.35f;
    [Tooltip("Crewmates further away than this don't come.")]
    public float reach = 80f; // the whole ship: someone always comes
    [Tooltip("Metres above the crewmate's feet the bubble floats.")]
    public float heightAbove = 2.3f;

    const float RefHeight = 1080f;

    enum Phase { Idle, Waiting, Approach, Tap, Talk }
    static CrewWhispers instance;
    Phase phase;
    float phaseAt;
    BotBrain bot;
    Component player;
    string line;
    CrewGestures.Player botGesture, playerGesture;

    RectTransform canvasRect, bubble, thought;
    CanvasGroup group, thoughtGroup;
    RawImage vignette;
    Text nameText, lineText;
    Image nameDot;
    Transform speaker;
    float shownAt = -1f;
    AudioSource heart;

    /// <summary>The crewmate whose bubble is showing (their floating name tag hides meanwhile), or null.</summary>
    public static Transform Speaking => instance != null && instance.shownAt >= 0f && Time.time - instance.shownAt < instance.showSeconds ? instance.speaker : null;

    /// <summary>BotBrain asks this every think: true while this crewmate is walking over or talking.</summary>
    public static bool Holds(BotBrain b) => instance != null && b != null && instance.bot == b && instance.phase >= Phase.Approach;

    void OnEnable() { instance = this; GameEvents.OrderIssued += OnOrder; }
    void OnDisable() { GameEvents.OrderIssued -= OnOrder; if (instance == this) instance = null; }
    void Start() => Build();

    void OnDestroy()
    {
        Release();
        if (canvasRect != null) Destroy(canvasRect.gameObject);
    }

    void OnOrder(ActiveOrder o)
    {
        if (phase != Phase.Idle || o.Assignee == null || o.Assignee.IsBot || o.Data.kind == OrderKind.Normal) return;
        player = o.Assignee as Component;
        if (player == null) return;
        bool stoker = Random.value < stokerChance;
        var who = PickCrewmate(player.transform.position, stoker);
        if (who == null && stoker) { stoker = false; who = PickCrewmate(player.transform.position, false); }
        if (who == null) return;
        bot = who;
        line = stoker ? Pick(StokerLines) : DoubtFor(o.Data);
        Go(Phase.Waiting);
    }

    /// <summary>The nearest honest bot (or the Stoker when asked for) within reach.</summary>
    BotBrain PickCrewmate(Vector3 near, bool stoker)
    {
        BotBrain best = null;
        float bestDist = reach * reach;
        foreach (var c in CrewRegistry.All)
        {
            if (!c.IsBot || c.IsStoker != stoker || c.IsLockedOut) continue;
            var b = (c as Component)?.GetComponent<BotBrain>();
            if (b == null || b.Mover == null) continue;
            float dd = (c.Position - near).sqrMagnitude;
            if (dd < bestDist) { bestDist = dd; best = b; }
        }
        return best;
    }

    static string DoubtFor(OrderData d)
    {
        string n = d.name;
        if (n.Contains("FullSpeed")) return "Full speed through an ice field? Are you sure about this?";
        if (n.Contains("FileIceWarning")) return "File it unread? Those warnings are about us.";
        if (n.Contains("LockLifeboats")) return "Lock the lifeboats? Who locks the lifeboats?";
        if (n.Contains("TwoPointsNorth")) return "North? The Promised Port isn't north.";
        if (d.kind == OrderKind.Final) return "Don't. Not now. Think about what he's asking.";
        return Pick(GenericDoubts);
    }

    static readonly string[] GenericDoubts =
    {
        "Are you sure you're going to listen to the Captain?",
        "Something's not right about that order.",
        "You don't have to do everything he says.",
    };

    static readonly string[] StokerLines =
    {
        "Orders are orders, newcomer. Just do it.",
        "The Captain knows what he's doing. Don't make him wait.",
        "Don't listen to the others. Keep the Captain happy.",
    };

    static string Pick(string[] lines) => lines[Random.Range(0, lines.Length)];

    void Go(Phase p) { phase = p; phaseAt = Time.time; }
    float InPhase => Time.time - phaseAt;

    void Update()
    {
        if (group == null) return;
        if (phase != Phase.Idle && (bot == null || player == null || GameManager.Instance == null || GameManager.Instance.State >= GameState.Iceberg))
        { Release(); phase = Phase.Idle; }

        switch (phase)
        {
            case Phase.Waiting:
                if (InPhase >= delay) Go(Phase.Approach);
                break;
            case Phase.Approach:
            {
                // Come up at the player's right shoulder, a little ahead, so the camera behind sees you both.
                var pt = player.transform;
                Vector3 spot = pt.position + pt.right * 1.05f + pt.forward * 0.45f;
                bot.Mover.GoTo(BotMover.OnNavMesh(spot, 1.5f), 0.3f);
                float dist = BotMover.FlatDistance(bot.transform.position, pt.position);
                if (dist < 1.4f) { StartTap(); }
                else if (InPhase > approachSeconds) { Speak(); Go(Phase.Talk); }
                break;
            }
            case Phase.Tap:
                Face(bot.transform, player.transform.position);
                if (InPhase > 0.5f) TurnPlayerTo(bot.transform.position);
                if (InPhase > 0.55f && playerGesture == null) Flinch();
                if (InPhase > 1.1f) { Speak(); Go(Phase.Talk); }
                break;
            case Phase.Talk:
                Face(bot.transform, player.transform.position);
                TurnPlayerTo(bot.transform.position);
                if (InPhase > showSeconds || BotMover.FlatDistance(bot.transform.position, player.transform.position) > 9f)
                { Release(); phase = Phase.Idle; }
                break;
        }
        botGesture?.Tick();
        if (playerGesture != null && !playerGesture.Tick()) playerGesture = null;
        Draw();
    }

    void StartTap()
    {
        bot.Mover.Stop();
        botGesture?.Dispose();
        var g = CrewGestures.Load();
        botGesture = g != null ? new CrewGestures.Player(bot.GetComponentInChildren<Animator>(), g.tap, false, 0.15f) : null;
        Go(Phase.Tap);
    }

    void Flinch()
    {
        var g = CrewGestures.Load();
        playerGesture = g != null ? new CrewGestures.Player(player.GetComponentInChildren<Animator>(), g.startle, false, 0.12f) : null;
        if (playerGesture != null && !playerGesture.Alive) playerGesture = null;
    }

    void Speak()
    {
        if (bot != null) bot.Mover.Stop();
        botGesture?.Dispose();
        var g = CrewGestures.Load();
        botGesture = g != null ? new CrewGestures.Player(bot.GetComponentInChildren<Animator>(), g.talk, true, 0.25f) : null;
        speaker = bot.transform;
        var actor = bot.GetComponent<ICrewActor>();
        int seat = actor != null ? actor.Seat : 0;
        nameText.text = actor != null ? CrewRegistry.CrewName(actor).ToUpperInvariant() : "";
        if (seat >= 0 && seat < CrewRoster.SeatColors.Length) nameDot.color = CrewRoster.SeatColors[seat];
        lineText.text = line;
        FitBubble();
        shownAt = Time.time;
        if (heart != null) heart.Play();
    }

    /// <summary>
    /// (Samprity) Grows the paper to fit the line, so a long doubt never spills out of it: the name and its seat dot
    /// ride the top edge, the tail stays at the bottom.
    /// </summary>
    void FitBubble()
    {
        var paper = bubble.Find("Paper") as RectTransform;
        if (paper == null) return;
        const float width = 420f;
        float h = Mathf.Max(30f, lineText.cachedTextGeneratorForLayout.GetPreferredHeight(lineText.text, lineText.GetGenerationSettings(new Vector2(width, 0f))) / lineText.pixelsPerUnit);
        float paperH = Mathf.Max(96f, h + 50f);
        paper.sizeDelta = new Vector2(460f, paperH);
        float top = 20f + paperH;
        nameDot.rectTransform.anchoredPosition = new Vector2(-200f, top - 22f);
        nameText.rectTransform.anchoredPosition = new Vector2(-80f, top - 22f);
        lineText.rectTransform.sizeDelta = new Vector2(width, h);
        lineText.rectTransform.anchoredPosition = new Vector2(0f, 20f + (paperH - 34f) * 0.5f + 4f);
        bubble.sizeDelta = new Vector2(460f, paperH + 14f);
    }

    void Release()
    {
        botGesture?.Dispose(); botGesture = null;
        playerGesture?.Dispose(); playerGesture = null;
        if (heart != null) heart.Stop();
        bot = null;
        shownAt = -1f;
    }

    Vector3 lastPlayerPos;

    /// <summary>The player turns to the crewmate who's talking, unless they're walking somewhere.</summary>
    void TurnPlayerTo(Vector3 target)
    {
        var pt = player.transform;
        bool standing = (pt.position - lastPlayerPos).sqrMagnitude < 0.0004f;
        lastPlayerPos = pt.position;
        if (standing) Face(pt, target, 5f);
    }

    static void Face(Transform t, Vector3 target, float rate = 8f)
    {
        Vector3 d = target - t.position; d.y = 0f;
        if (d.sqrMagnitude > 0.01f) t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(d), Time.deltaTime * rate);
    }

    // ---------- Drawing ----------

    void Draw()
    {
        // (Samprity) Out of the way while something is being read up close (the telegram, the chart, the bottle):
        // it would peek out from behind it.
        bool covered = ShipClues.ReadingPaper || ChartView.IsOpen || ClueCloseUp.IsOpen;
        bool talking = shownAt >= 0f && speaker != null && Time.time - shownAt < showSeconds && !covered;
        bool unsure = (talking || phase == Phase.Tap) && !covered;
        float dt = Time.unscaledDeltaTime;
        group.alpha = Mathf.MoveTowards(group.alpha, talking ? 1f : 0f, dt * 4f);
        thoughtGroup.alpha = Mathf.MoveTowards(thoughtGroup.alpha, unsure ? 1f : 0f, dt * 3f);
        // A slow heartbeat in the dark edges: two quick pulses, then a rest.
        float beat = Mathf.Repeat(Time.time, 1.1f);
        float pulse = Mathf.Exp(-beat * 9f) + 0.7f * Mathf.Exp(-Mathf.Abs(beat - 0.28f) * 9f);
        var vc = vignette.color;
        vc.a = Mathf.MoveTowards(vc.a, unsure ? 0.35f + 0.25f * pulse : 0f, dt * 2f);
        vignette.color = vc;
        var cam = Camera.main;
        if (cam == null) return;

        if (talking && group.alpha > 0f)
        {
            Vector3 sp = cam.WorldToScreenPoint(speaker.position + Vector3.up * heightAbove);
            if (sp.z < 0f) sp = new Vector3(Screen.width - sp.x, 0f, 0f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out var local);
            local.x += 190f; // beside the crewmate, clear of the player
            var half = canvasRect.rect.size * 0.5f;
            // Clear of the order cards and readout (top right) and the radio strip (bottom).
            local.x = Mathf.Clamp(local.x, -half.x + 260f, half.x - 560f);
            local.y = Mathf.Clamp(local.y, -half.y + 260f, half.y - bubble.sizeDelta.y - 320f);
            bubble.anchoredPosition = Vector2.Lerp(bubble.anchoredPosition, local, 1f - Mathf.Exp(-dt * 14f));
        }
        if (player != null && thoughtGroup.alpha > 0f)
        {
            Vector3 tp = cam.WorldToScreenPoint(player.transform.position + Vector3.up * 2.35f);
            if (tp.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, tp, null, out var tl))
                thought.anchoredPosition = tl + new Vector2(-70f, Mathf.Sin(Time.time * 3f) * 6f);
            thought.localScale = Vector3.one * (1f + 0.08f * pulse);
        }
    }

    void Build()
    {
        var theme = UiTheme.Load();
        Font F(Font f) => f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvas = new GameObject("[CrewWhispers]", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, RefHeight);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)canvas.transform;

        // Dark edges that beat while you're unsure. Under everything else here.
        vignette = new GameObject("Unsure", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        vignette.transform.SetParent(canvasRect, false);
        vignette.raycastTarget = false;
        vignette.texture = VignetteTexture();
        vignette.color = new Color(0.02f, 0.03f, 0.08f, 0f);
        var vr = vignette.rectTransform;
        vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = vr.offsetMax = Vector2.zero;

        // The "?" thought cloud over the player.
        thought = new GameObject("Thought", typeof(RectTransform), typeof(CanvasGroup), typeof(RawImage)).GetComponent<RectTransform>();
        thought.SetParent(canvasRect, false);
        thought.sizeDelta = new Vector2(96f, 80f);
        thought.pivot = new Vector2(0.5f, 0f);
        thoughtGroup = thought.GetComponent<CanvasGroup>();
        thoughtGroup.alpha = 0f;
        thoughtGroup.blocksRaycasts = false;
        var cloud = thought.GetComponent<RawImage>();
        cloud.texture = CloudTexture();
        cloud.raycastTarget = false;
        var q = new GameObject("Q", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        q.transform.SetParent(thought, false);
        q.font = Resources.Load<Font>("Fonts/Bungee-Regular") ?? F(theme ? theme.title : null);
        q.fontSize = 40; q.color = new Color(0.2f, 0.14f, 0.1f); q.text = "?"; q.alignment = TextAnchor.MiddleCenter;
        q.raycastTarget = false;
        q.rectTransform.anchoredPosition = new Vector2(0f, 8f);
        q.rectTransform.sizeDelta = new Vector2(80f, 60f);

        // The crewmate's speech bubble.
        var bubbleRoot = new GameObject("Bubble", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        bubbleRoot.SetParent(canvasRect, false);
        bubble = bubbleRoot;
        bubble.sizeDelta = new Vector2(460f, 110f);
        bubble.pivot = new Vector2(0.5f, 0f);
        group = bubbleRoot.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        Image Box(string name, Color c, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(bubble, false);
            img.color = c;
            img.raycastTarget = false;
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
            return img;
        }
        var paper = new Color(0.97f, 0.93f, 0.84f, 0.96f);
        Box("Tail", paper, new Vector2(0f, 14f), new Vector2(26f, 26f), new Vector2(0.5f, 0.5f)).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Box("Paper", paper, new Vector2(0f, 20f), new Vector2(460f, 96f), new Vector2(0.5f, 0f));
        nameDot = Box("Seat", Color.white, new Vector2(-200f, 94f), new Vector2(14f, 14f), new Vector2(0.5f, 0.5f));

        Text Label(Font font, int size, Color c, Vector2 pos, Vector2 box, TextAnchor a)
        {
            var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(bubble, false);
            t.font = font; t.fontSize = size; t.color = c; t.alignment = a;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = pos; r.sizeDelta = box;
            return t;
        }
        var ink = new Color(0.23f, 0.16f, 0.11f);
        nameText = Label(F(theme ? theme.body : null), 18, new Color(0.55f, 0.2f, 0.15f), new Vector2(-80f, 94f), new Vector2(220f, 24f), TextAnchor.MiddleLeft);
        lineText = Label(F(theme ? theme.italic : null), 26, ink, new Vector2(0f, 56f), new Vector2(420f, 60f), TextAnchor.MiddleCenter);

        // A low heartbeat while the player makes up their mind.
        heart = gameObject.AddComponent<AudioSource>();
        heart.clip = HeartbeatClip();
        heart.loop = true;
        heart.playOnAwake = false;
        heart.spatialBlend = 0f;
        heart.volume = 0.55f;
        gameObject.AddComponent<AudioCategory>().kind = AudioCategory.Kind.Effects;
    }

    static Texture2D VignetteTexture()
    {
        const int n = 128;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 0.707f;
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.SmoothStep(0.45f, 1f, d) * 255f));
            }
        t.SetPixels32(px); t.Apply();
        return t;
    }

    static Texture2D CloudTexture()
    {
        var p = new EndingPainter(192, 160, new Color(0, 0, 0, 0));
        var c = new Color(0.97f, 0.94f, 0.86f, 0.97f);
        p.Ellipse(96, 62, 78, 50, c);
        p.Circle(52, 46, 32, c); p.Circle(96, 30, 36, c); p.Circle(140, 46, 32, c);
        p.Circle(70, 118, 14, c); p.Circle(56, 146, 8, c); // the little trail down to the head
        return p.ToTexture();
    }

    static AudioClip HeartbeatClip()
    {
        const int rate = 22050;
        int n = (int)(rate * 1.1f);
        var data = new float[n];
        foreach (var (at, amp) in new[] { (0f, 1f), (0.28f, 0.7f) })
            for (int i = 0; i < (int)(rate * 0.18f); i++)
            {
                float t = i / (float)rate;
                int k = (int)(at * rate) + i;
                if (k < n) data[k] += amp * Mathf.Sin(2f * Mathf.PI * (52f - 40f * t) * t) * Mathf.Exp(-t * 22f) * 0.9f;
            }
        var clip = AudioClip.Create("Heartbeat", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
