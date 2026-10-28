using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Talking to the Captain in person. Owner: Samprity. Added by CaptainOnDeck.
/// A player standing by him with free hands and no station or chart in reach gets "E  Talk to the Captain". He
/// stops, turns to them and answers in a speech bubble, typed out like his speaker lines (slower and stumbling when
/// he's had a few, the bubble swaying in Part 2). E finishes the line / turns the page / ends it; walking off ends it,
/// and so does his next line on the speaker. Talking changes nothing in the game: no meters, no orders, no timers.
/// What he says, all of it from CaptainScript except Part 1's small talk:
///   you have an order:  he repeats it, word for word as he gave it on the speaker;
///   Part 1:             small talk (PartOneTalk, below; Riham may rewrite these);
///   Part 2:             his rambles for his mood, or his asides to a favourite (Captain's Trust 75+) or the
///                       suspicious ones (25 or less);
///   Part 3:             "It doesn't matter now."
/// </summary>
public class CaptainTalk : MonoBehaviour
{
    [Tooltip("How close (metres, flat) a player must stand to talk to him.")]
    public float range = 1.9f;
    [Tooltip("Walking this far away ends the conversation.")]
    public float leaveRange = 3.2f;
    public float charsPerSecond = 34f;

    /// <summary>Part 1 small talk, warm and a bit pleased with himself. Riham: rewrite freely.</summary>
    public static readonly string[] PartOneTalk =
    {
        "Ah, the new one. {seat}, isn't it? Welcome aboard.\nMind the boiler. It bites.",
        "Two days to the Promised Port, {seat}. Keep the fires fed and we'll have ourselves a record.",
        "Questions, {seat}? Write them down.\nThen feed them to the boiler. Saves everyone time.",
        "You're doing fine, {seat}. Better than fine.\nAdequate, even.",
        "The boiler, the wheel, the doors, the wireless. Learn them well, {seat}.\nI'll be watching. Fondly.",
    };

    static readonly float[] MoodTyping = { 1f, 0.97f, 0.9f, 0.8f, 0.75f };
    static readonly float[] MoodStumble = { 0f, 0f, 0.03f, 0.08f, 0f };

    CaptainOnDeck captain;
    Transform chart;
    RectTransform canvasRect, prompt, bubble, lineRect;
    CanvasGroup promptGroup, bubbleGroup;
    Text promptText, lineText, moreText;

    Component near, talker;
    IInteractSource lastSource;
    bool wasHeld;
    string[] pages;
    int page;
    float shownChars, pauseLeft;
    string lastLine;
    Vector2 lineHome;

    /// <summary>A conversation is on screen.</summary>
    public static bool IsOpen { get; private set; }

    void Start()
    {
        captain = GetComponent<CaptainOnDeck>();
        var c = GameObject.Find("Chart");
        if (c != null) chart = c.transform;
        Build();
    }

    void OnDestroy()
    {
        IsOpen = false;
        if (canvasRect != null) Destroy(canvasRect.gameObject);
    }

    void Update()
    {
        if (captain == null || !captain.Ready || canvasRect == null) return;
        var gm = GameManager.Instance;
        bool playing = gm != null && gm.State >= GameState.Part1 && gm.State <= GameState.Part3
                       && !(PauseMenu.Instance != null && PauseMenu.Instance.IsPaused);

        if (talker != null)
        {
            bool speaker = CaptainDirector.Instance != null && CaptainDirector.Instance.IsSpeaking;
            if (!playing || speaker || Flat(talker.transform.position) > leaveRange) Close();
        }

        near = null;
        if (talker == null && playing && captain.Available)
            foreach (var c in CrewRegistry.All)
            {
                if (c.IsBot || !(c is Component comp) || !CanTalk(comp)) continue;
                near = comp;
                break;
            }

        // Interact, on the press (not the hold), from whoever is talking or about to.
        var who = talker != null ? talker : near;
        var src = who != null ? who.GetComponent<IInteractSource>() : null;
        bool held = src != null && src.InteractHeld;
        bool pressed = src == lastSource && held && !wasHeld;
        lastSource = src;
        wasHeld = held;
        if (pressed)
        {
            if (talker == null) Open(near);
            else Advance();
        }

        Type();
        Draw();
    }

    bool CanTalk(Component who)
    {
        if (Flat(who.transform.position) > range) return false;
        if (chart != null && Vector3.Distance(Flat3(who.transform.position), Flat3(chart.position)) < 2.1f) return false; // the chart's E
        if (ChartView.IsOpen) return false;
        if (ShipClues.NearBottle(who.transform.position)) return false; // E there looks at his bottle (Anadi)
        var interactor = who.GetComponent<Interactor>();
        return interactor == null || (!interactor.IsCarrying && interactor.NearestStation == null);
    }

    // ---------- The conversation ----------

    void Open(Component who)
    {
        if (who == null) return;
        talker = who;
        IsOpen = true;
        pages = LineFor(who).Split('\n');
        page = 0;
        shownChars = 0f;
        pauseLeft = 0.25f;
        captain.BeginConversation(who);
        FitPage(pages[0]);
        CaptainNarrator.SayInPerson(pages[0]); // his recorded voice, if there is one
    }

    void Advance()
    {
        if (pages == null) { Close(); return; }
        if (shownChars < pages[page].Length) { shownChars = pages[page].Length; return; } // finish the line first
        if (++page >= pages.Length) { Close(); return; }
        shownChars = 0f;
        pauseLeft = 0.15f;
        FitPage(pages[page]);
        CaptainNarrator.SayInPerson(pages[page]);
    }

    void Close()
    {
        talker = null;
        pages = null;
        IsOpen = false;
        captain.EndConversation();
        CaptainNarrator.StopInPerson();
    }

    void Type()
    {
        if (pages == null) return;
        string text = pages[page];
        if (shownChars < text.Length)
        {
            int mood = Mood;
            if (pauseLeft > 0f) pauseLeft -= Time.deltaTime;
            else
            {
                int before = (int)shownChars;
                shownChars = Mathf.Min(text.Length, shownChars + Time.deltaTime * charsPerSecond * MoodTyping[mood]);
                // Same rhythm as his speaker lines: a beat on punctuation, and little drunken stumbles.
                for (int i = before; i < (int)shownChars; i++)
                {
                    char ch = text[i];
                    if (ch == '.' || ch == ',' || ch == '?' || ch == '!' || ch == ':') { pauseLeft = 0.12f; shownChars = i + 1; break; }
                    if (Random.value < MoodStumble[mood]) { pauseLeft = Random.Range(0.1f, 0.3f); shownChars = i + 1; break; }
                }
            }
        }
        lineText.text = text.Substring(0, (int)shownChars);
        bool done = shownChars >= text.Length;
        string key = HudText.KeyFor(HudText.Key.Interact, talker);
        moreText.text = !done ? "" : page < pages.Length - 1 ? $"{key}  more" : $"{key}  done";
    }

    static int Mood => CaptainDirector.Instance != null ? Mathf.Clamp(CaptainDirector.Instance.Mood, 0, 4) : 0;

    string LineFor(Component who)
    {
        var actor = who.GetComponent<ICrewActor>();
        int seat = actor != null ? actor.Seat : 0;
        string line;
        var order = actor != null && OrderDirector.Instance != null ? OrderDirector.Instance.GetOrderFor(actor) : null;
        if (order != null && !order.IsResolved && !string.IsNullOrEmpty(order.Data.captainLine))
            line = FinalText(order.Data.captainLine);
        else
        {
            int mood = Mood;
            int trust = MeterSystem.Instance != null ? MeterSystem.Instance.CaptainTrust : 50;
            string[] from =
                mood == 0 ? PartOneTalk :
                mood >= 4 ? new[] { CaptainScript.ForMood(CaptainScript.Expired, 4)[0] } :
                trust >= 75 ? CaptainScript.PrivateLines :
                trust <= 25 ? CaptainScript.SuspiciousLines :
                CaptainScript.ForMood(CaptainScript.Rambles, mood);
            if (from == null || from.Length == 0) from = PartOneTalk;
            line = from[Random.Range(0, from.Length)];
            if (from.Length > 1 && line == lastLine) line = from[(System.Array.IndexOf(from, line) + 1) % from.Length];
        }
        lastLine = line;
        return line.Replace("{seat}", seat > 0 ? CrewRegistry.SeatName(seat) : "sailor");
    }

    /// <summary>A line as it finally reads: CaptainDirector types {wrong|right} as a correction, which ends as right.</summary>
    static string FinalText(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                int bar = text.IndexOf('|', i), close = text.IndexOf('}', i);
                if (bar > i && close > bar) { sb.Append(text, bar + 1, close - bar - 1); i = close; continue; }
            }
            sb.Append(text[i]);
        }
        return sb.ToString();
    }

    float Flat(Vector3 p) => Vector3.Distance(Flat3(p), Flat3(captain.Position));
    static Vector3 Flat3(Vector3 p) => new Vector3(p.x, 0f, p.z);

    // ---------- Drawing ----------

    void Draw()
    {
        float dt = Time.unscaledDeltaTime;
        var cam = Camera.main;
        bool showPrompt = near != null && talker == null && cam != null;
        bool showBubble = talker != null && cam != null;
        promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, showPrompt ? 1f : 0f, dt * 6f);
        bubbleGroup.alpha = Mathf.MoveTowards(bubbleGroup.alpha, showBubble ? 1f : 0f, dt * 5f);
        if (cam == null) return;

        Vector3 sp = cam.WorldToScreenPoint(captain.HeadTop);
        if (sp.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out var local)) return;
        if (showPrompt) { promptText.text = $"{HudText.KeyFor(HudText.Key.Interact, near)}   Talk to the Captain"; prompt.anchoredPosition = local; }

        if (bubbleGroup.alpha > 0f)
        {
            // Beside him, clear of the order cards and readout (top right) and the speaker strip (bottom).
            var half = canvasRect.rect.size * 0.5f;
            local.x = Mathf.Clamp(local.x + 60f, -half.x + 320f, half.x - 620f);
            local.y = Mathf.Clamp(local.y, -half.y + 260f, half.y - bubble.sizeDelta.y - 320f);
            bubble.anchoredPosition = Vector2.Lerp(bubble.anchoredPosition, local, 1f - Mathf.Exp(-dt * 14f));

            // Part 2: the words won't sit still. Part 3: dead still.
            float act = GameManager.Instance != null && GameManager.Instance.State == GameState.Part2 ? Mathf.Clamp01(Mood / 3f) : 0f;
            float t = Time.unscaledTime;
            lineRect.anchoredPosition = lineHome + new Vector2(Mathf.Sin(t * 0.9f), Mathf.Cos(t * 1.7f) * 0.4f) * 6f * act;
            lineRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.3f) * 1.2f * act);
        }
    }

    void Build()
    {
        var theme = UiTheme.Load();
        Font F(Font f) => f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvas = new GameObject("[CaptainTalk]", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 41; // over the crew's whispers, under the chart and the pause menu
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)canvas.transform;

        // The prompt over his cap, like the chart table's.
        prompt = new GameObject("Prompt", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        prompt.SetParent(canvasRect, false);
        prompt.sizeDelta = new Vector2(380f, 48f);
        promptGroup = prompt.GetComponent<CanvasGroup>();
        promptGroup.alpha = 0f;
        promptGroup.blocksRaycasts = false;
        Box(prompt, "Back", new Color(0.03f, 0.04f, 0.07f, 0.72f), Vector2.zero, new Vector2(380f, 48f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        promptText = Label(prompt, F(theme ? theme.body : null), 24, new Color(0.97f, 0.93f, 0.84f), Vector2.zero, new Vector2(360f, 44f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));

        // His speech bubble: the crew's paper, a little larger, with his title in brass-red.
        bubble = new GameObject("Bubble", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        bubble.SetParent(canvasRect, false);
        bubble.sizeDelta = new Vector2(560f, 150f);
        bubble.pivot = new Vector2(0.5f, 0f);
        bubbleGroup = bubble.GetComponent<CanvasGroup>();
        bubbleGroup.alpha = 0f;
        bubbleGroup.blocksRaycasts = false;
        var paper = new Color(0.97f, 0.93f, 0.84f, 0.97f);
        var bottom = new Vector2(0.5f, 0f);
        Box(bubble, "Tail", paper, new Vector2(-180f, 14f), new Vector2(26f, 26f), bottom, new Vector2(0.5f, 0.5f)).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        paperRect = Box(bubble, "Paper", paper, new Vector2(0f, 20f), new Vector2(560f, 136f), bottom, bottom).rectTransform;
        ruleRect = Box(bubble, "Rule", new Color(0.55f, 0.2f, 0.15f, 0.5f), new Vector2(-150f, 124f), new Vector2(220f, 2f), bottom, new Vector2(0.5f, 0.5f)).rectTransform;

        var ink = new Color(0.23f, 0.16f, 0.11f);
        var title = Label(bubble, F(theme ? theme.body : null), 18, new Color(0.55f, 0.2f, 0.15f), new Vector2(-150f, 138f), new Vector2(240f, 24f), TextAnchor.MiddleLeft, bottom);
        title.text = "THE CAPTAIN";
        titleRect = title.rectTransform;
        // Typed from the top left, so the words don't climb the box as more lines arrive.
        lineText = Label(bubble, F(theme ? theme.italic : null), 26, ink, new Vector2(0f, 74f), new Vector2(LineWidth, 80f), TextAnchor.UpperLeft, bottom);
        lineText.horizontalOverflow = HorizontalWrapMode.Wrap;
        lineRect = lineText.rectTransform;
        lineHome = lineRect.anchoredPosition;
        moreText = Label(bubble, F(theme ? theme.body : null), 18, new Color(0.45f, 0.36f, 0.28f), new Vector2(190f, 34f), new Vector2(140f, 22f), TextAnchor.MiddleRight, bottom);
    }

    const float LineWidth = 510f;
    RectTransform paperRect, ruleRect, titleRect;

    /// <summary>
    /// Sizes the paper to the whole page before it types out, so it never grows mid-line and a long page never spills
    /// out of it: the title and rule ride the top edge, the "E more" hint stays in the bottom corner.
    /// </summary>
    void FitPage(string page)
    {
        if (paperRect == null) return;
        float h = lineText.cachedTextGeneratorForLayout.GetPreferredHeight(page, lineText.GetGenerationSettings(new Vector2(LineWidth, 0f))) / lineText.pixelsPerUnit;
        h = Mathf.Max(34f, h);
        float paperH = Mathf.Max(136f, h + 84f);
        paperRect.sizeDelta = new Vector2(560f, paperH);
        titleRect.anchoredPosition = new Vector2(-150f, 20f + paperH - 18f);
        ruleRect.anchoredPosition = new Vector2(-150f, 20f + paperH - 32f);
        lineRect.sizeDelta = new Vector2(LineWidth, h);
        lineHome = new Vector2(0f, 20f + paperH - 44f - h * 0.5f);
        lineRect.anchoredPosition = lineHome;
        bubble.sizeDelta = new Vector2(560f, paperH + 20f);
    }

    static Image Box(RectTransform parent, string name, Color c, Vector2 pos, Vector2 size, Vector2 anchor, Vector2 pivot)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = c;
        img.raycastTarget = false;
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
        return img;
    }

    static Text Label(RectTransform parent, Font font, int size, Color c, Vector2 pos, Vector2 box, TextAnchor a, Vector2 anchor)
    {
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font; t.fontSize = size; t.color = c; t.alignment = a;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var r = t.rectTransform;
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = pos; r.sizeDelta = box;
        return t;
    }
}
