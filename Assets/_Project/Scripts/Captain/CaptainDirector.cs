using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The voice on the speaker. Queues the Captain's lines, types them out Undertale-style one text box (page) at a time,
/// blips per letter, and reacts to orders, reports, refusals and caught fakes. Owner: Riham.
///
/// He gets drunker as the voyage goes on (Mood 0-4, see CaptainScript): his lines change, he types slower and
/// with stumbles, and his voice drops. Lines are split into pages on "\n". A line stops after its current page
/// when something more important is waiting (or the speaker is backed up), so the first page of an order is
/// always heard and the flavour pages give way. Space finishes the text box at once, or moves on if it's all there.
///
/// With a human aboard, every text box waits for Space (or LB) before the next, so nothing is missed during a break.
/// Orders wait for him: OrderDirector asks ReadyForOrder, and the order is issued the moment he announces it. While
/// it's open its line stays up and Space does nothing. Reactions (done, refused, ran out, caught fakes, tutorial
/// prompts, the vote) cut in at once with no Space needed; what they interrupted comes back after.
/// Bot-only voyages keep the old timed pages, so test runs play out on their own.
///
/// Samprity: build the text box from PageStarted / TextChanged (or VisibleText), swap the portrait on Mood, play
/// the blip voice on Blip and speaker static on StokerBrain.StaticBurst, then set DrawFallbackSubtitles = false.
/// Anadi: GameManager already waits MonologueSeconds before the iceberg.
/// </summary>
public class CaptainDirector : MonoBehaviour
{
    public static CaptainDirector Instance { get; private set; }

    /// <summary>A new line starts. The text is the final wording, with corrections already applied.</summary>
    public static event System.Action<string> LineStarted;
    /// <summary>A new text box starts within a line: the page's final wording.</summary>
    public static event System.Action<string> PageStarted;
    /// <summary>The visible subtitle changed (every letter, and "" when the line clears).</summary>
    public static event System.Action<string> TextChanged;
    /// <summary>One "blip" of the Captain's voice.</summary>
    public static event System.Action Blip;
    public static event System.Action LineFinished;

    /// <summary>Simple IMGUI subtitles until the real HUD exists.</summary>
    public static bool DrawFallbackSubtitles = true;

    [Header("Typewriter")]
    [SerializeField] float charsPerSecond = 34f;
    [SerializeField] float punctuationPause = 0.12f;
    [SerializeField] float holdSeconds = 1.6f;
    [SerializeField] float holdPerChar = 0.02f;
    [SerializeField] float correctionPause = 0.5f;

    [Header("Drunkenness, by mood 0-4")]
    [SerializeField] float[] moodTypingSpeed = { 1f, 0.97f, 0.9f, 0.8f, 0.75f };
    [Tooltip("Extra pitch on top of his voice for each mood (the voice itself already drops as he 'drinks').")]
    [SerializeField] float[] moodVoicePitch = { 1f, 1f, 1f, 1f, 1f };
    [Tooltip("Chance per letter of a little drunken stumble (pause) at moods 2 and 3.")]
    [SerializeField] float[] moodStumbleChance = { 0f, 0f, 0.03f, 0.08f, 0f };

    [Header("Chatter")]
    [SerializeField, Range(0f, 1f)] float praiseChance = 0.6f;
    [SerializeField, Range(0f, 1f)] float expiredLineChance = 0.6f;
    [SerializeField] Vector2 asideGap = new Vector2(30f, 45f);

    [Header("Voice")]
    [SerializeField] bool placeholderBlip = true;
    [SerializeField, Range(0f, 1f)] float blipVolume = 0.85f;

    enum Priority { Chatter, Order, Story }

    struct Line
    {
        public string text;
        public Priority priority;
        /// <summary>The ActiveOrder this line belongs to (its announcement or a prompt about it), or 0.</summary>
        public int orderId;
    }

    readonly List<Line> queue = new List<Line>(8);
    readonly StringBuilder typed = new StringBuilder(160);
    Coroutine speaking;
    bool typing, skipPage, nextPage, pausedLastFrame, onLastPage;
    Line current;
    int nowFrame = -1, nowInserted;

    /// <summary>True while a text box is still typing out (Space would finish it); false while it's held on screen.</summary>
    public bool IsTyping => speaking != null && typing;

    /// <summary>Nothing on the speaker and nothing waiting.</summary>
    public bool IsQuiet => speaking == null && queue.Count == 0;

    /// <summary>A human is aboard, so text boxes wait for Space and orders wait for him.</summary>
    public bool WaitsForPlayers
    {
        get { foreach (var c in CrewRegistry.All) if (!c.IsBot) return true; return false; }
    }

    /// <summary>
    /// Showing the last box said about an open order: it stays until the order is done, refused or runs out, and
    /// Space does nothing (CaptainSubtitles hides its hint).
    /// </summary>
    public bool HoldingForOrder =>
        speaking != null && !typing && onLastPage && current.orderId != 0 && OrderOpen(current.orderId)
        && !queue.Exists(l => l.orderId == current.orderId);
    float nextAsideAt = float.MaxValue;
    bool monologueStarted;
    int lastMood;
    /// <summary>The final order's line until it has been spoken; the monologue opens with it if it hasn't.</summary>
    string unspokenFinalLine;
    float staticUntil;

    AudioSource blipSource;
    int blipCount;

    public string VisibleText { get; private set; } = "";

    /// <summary>
    /// True once the voyage's opening line ("Ahem. Is this on?...") has been said. Nothing else on the speaker comes
    /// before it, and TutorialHints stays quiet until then.
    /// </summary>
    public bool OpeningDone => !HasOpeningQueued() && speakingText != CaptainScript.Part1Start;

    string speakingText;
    /// <summary>His voice is going: a text box is typing out (not while a finished one waits for Space).</summary>
    public bool IsSpeaking => speaking != null && typing;

    /// <summary>
    /// How far gone he is: 0 Part 1 (sober), 1 orders 6-7, 2 orders 8-9, 3 orders 10-11, 4 Part 3 (bottle empty).
    /// Samprity: use it to pick the portrait.
    /// </summary>
    public int Mood => MoodFor(GameManager.Instance != null ? GameManager.Instance.Part : 1,
                               OrderDirector.Instance != null ? OrderDirector.Instance.NextIndex : 6);

    static int MoodFor(int part, int issued) =>
        part <= 1 ? 0 : part >= 3 ? 4 : issued <= 7 ? 1 : issued <= 9 ? 2 : 3;

    /// <summary>The final speech for this run, picked when the last order is done.</summary>
    string[] monologue = CaptainScript.Monologue;

    /// <summary>Roughly how long the final speech takes to say, in seconds.</summary>
    public float MonologueSeconds
    {
        get
        {
            float t = 0f;
            foreach (var line in monologue) t += EstimateSeconds(line, 4);
            if (unspokenFinalLine != null) t += EstimateSeconds(unspokenFinalLine, 4);
            return t;
        }
    }

    // ---------- Setup ----------

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (placeholderBlip) MakeBlip();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        GameEvents.StateChanged += OnStateChanged;
        GameEvents.PartChanged += OnPartChanged;
        GameEvents.TruthStarted += OnTruthStarted;
        GameEvents.OrderIssued += OnOrderIssued;
        GameEvents.OrderResolved += OnOrderResolved;
        GameEvents.FakeNoticed += OnFakeNoticed;
        GameEvents.IceWarningDelivered += OnIceWarningDelivered;
        StokerBrain.StaticBurst += OnStaticBurst;
    }

    void OnDisable()
    {
        GameEvents.StateChanged -= OnStateChanged;
        GameEvents.PartChanged -= OnPartChanged;
        GameEvents.TruthStarted -= OnTruthStarted;
        GameEvents.OrderIssued -= OnOrderIssued;
        GameEvents.OrderResolved -= OnOrderResolved;
        GameEvents.FakeNoticed -= OnFakeNoticed;
        GameEvents.IceWarningDelivered -= OnIceWarningDelivered;
        StokerBrain.StaticBurst -= OnStaticBurst;
    }

    // ---------- Public API ----------

    /// <summary>Queue a line. Story lines jump ahead of orders, orders ahead of chatter. Extra chatter is dropped.</summary>
    void Say(string text, Priority priority, bool interrupt = false, int orderId = 0)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (interrupt) Silence();

        if (priority == Priority.Chatter)
        {
            foreach (var l in queue) if (l.priority == Priority.Chatter) return;
        }

        int at = queue.Count;
        for (int i = 0; i < queue.Count; i++)
            if (queue[i].priority < priority) { at = i; break; }
        queue.Insert(at, new Line { text = text, priority = priority, orderId = orderId });
    }

    /// <summary>
    /// A reaction: on screen at once, no Space needed. What it cut off comes back after it (unless that was about an
    /// order that's now closed). Several in one frame play in the order they were said, an order's announcement
    /// ahead of any prompt about the same order. Without a human aboard it's queued like before.
    /// </summary>
    void SayNow(string text, Priority priority, int orderId = 0, bool announce = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (!WaitsForPlayers) { Say(text, priority, orderId: orderId); return; }
        if (Time.frameCount != nowFrame)
        {
            nowFrame = Time.frameCount;
            nowInserted = 0;
            Interrupt();
        }
        int at = nowInserted;
        if (announce)
            for (int i = 0; i < nowInserted; i++)
                if (queue[i].orderId == orderId) { at = i; break; }
        queue.Insert(at, new Line { text = text, priority = priority, orderId = orderId });
        nowInserted++;
    }

    /// <summary>Stops the line on screen and puts it back at the front, unless it was about an order that's closed.</summary>
    void Interrupt()
    {
        if (speaking == null) return;
        StopCoroutine(speaking);
        var cut = current;
        speaking = null;
        speakingText = null;
        typing = skipPage = nextPage = onLastPage = false;
        SetVisible("");
        bool keep = cut.orderId == 0 || OrderOpen(cut.orderId);
        if (keep && !queue.Exists(l => l.text == cut.text)) queue.Insert(0, cut);
    }

    /// <summary>An order closed: drop what's left to say about it, including its line if it's on screen.</summary>
    void EndOrder(int orderId)
    {
        queue.RemoveAll(l => l.orderId == orderId);
        if (speaking != null && current.orderId == orderId)
        {
            StopCoroutine(speaking);
            speaking = null;
            speakingText = null;
            typing = skipPage = nextPage = onLastPage = false;
            SetVisible("");
        }
    }

    static bool OrderOpen(int orderId)
    {
        var od = OrderDirector.Instance;
        if (od == null) return false;
        foreach (var o in od.Active) if (o.Id == orderId) return true;
        return false;
    }

    /// <summary>
    /// OrderDirector asks before issuing each order (when a human is aboard). False while he's still talking or a
    /// box waits for Space; a new drink is announced first. True while he holds another open order's line, which the
    /// new announcement then replaces (co-op).
    /// </summary>
    public bool ReadyForOrder(int part, int orderNumber)
    {
        if (!WaitsForPlayers) return true;
        int mood = MoodFor(part, orderNumber);
        if (mood > lastMood)
        {
            lastMood = mood;
            Say(CaptainScript.MoodStart[Mathf.Clamp(mood, 0, CaptainScript.Moods - 1)], Priority.Order);
            return false;
        }
        return queue.Count == 0 && (speaking == null || HoldingForOrder);
    }

    /// <summary>For other systems (e.g. the muster vote): the Captain says it now.</summary>
    public void SayStory(string text) => SayNow(text, Priority.Story);

    /// <summary>For TutorialHints: said now, tied to that crew member's open order, which comes back after it.</summary>
    public void SayTutorial(string text, int seat = 0)
    {
        var order = OrderDirector.Instance != null ? OrderDirector.Instance.GetOrderFor(CrewRegistry.BySeat(seat)) : null;
        SayNow(text, Priority.Order, order != null ? order.Id : 0);
    }

    public void Silence()
    {
        typing = skipPage = nextPage = onLastPage = false;
        queue.Clear();
        speakingText = null;
        if (speaking != null) StopCoroutine(speaking);
        speaking = null;
        SetVisible("");
    }

    // ---------- Reactions ----------

    void OnStateChanged(GameState s)
    {
        switch (s)
        {
            case GameState.Part1:
                Silence();
                monologueStarted = false;
                lastMood = 0;
                unspokenFinalLine = null;
                nextAsideAt = float.MaxValue;
                break;
            case GameState.Iceberg:
                if (!monologueStarted) Say(CaptainScript.IcebergCall, Priority.Story, true);
                nextAsideAt = float.MaxValue;
                break;
            case GameState.Results:
                Silence();
                break;
        }
    }

    void OnPartChanged(int part)
    {
        if (part == 1) Say(CaptainScript.Part1Start, Priority.Story);
        if (part == 2)
        {
            Say(CaptainScript.Part2Start, Priority.Story);
            nextAsideAt = Time.time + Random.Range(asideGap.x, asideGap.y) * 0.6f;
        }
    }

    void OnTruthStarted()
    {
        monologueStarted = true;
        nextAsideAt = float.MaxValue;
        Silence();
        // "Full ahead" leads into the confession, so it's never cut by the monologue starting.
        if (unspokenFinalLine != null) Say(unspokenFinalLine, Priority.Story);

        var meters = MeterSystem.Instance;
        int safety = meters != null ? meters.ShipSafety : 80;
        monologue = CaptainScript.VoyageGoingBadly(safety, RunRecord.LifeboatStoreLocked)
            ? CaptainScript.MonologueLost
            : CaptainScript.Monologue;
        foreach (var line in monologue) Say(line, Priority.Story);
    }

    void OnOrderIssued(ActiveOrder o)
    {
        // (With a human aboard, ReadyForOrder has already announced any new drink, so this finds nothing new.)
        // Another drink: announce the new mood once, just before the order that starts it.
        int mood = Mood;
        if (mood > lastMood)
        {
            lastMood = mood;
            Say(CaptainScript.MoodStart[Mathf.Clamp(mood, 0, CaptainScript.Moods - 1)], Priority.Order);
        }

        string who = o.Assignee != null ? CrewRegistry.SeatName(o.Assignee.Seat) + ". " : "";
        string line = who + o.Data.captainLine;

        string knows = DoorFourAside(o);
        if (knows != null) line += "\n" + knows;

        if (o.Data.kind == OrderKind.Final) unspokenFinalLine = line;
        SayNow(line, Priority.Order, o.Id, announce: true);
    }

    void OnOrderResolved(ActiveOrder o, Choice choice)
    {
        int seat = o.Assignee?.Seat ?? 0;
        int mood = Mood;
        EndOrder(o.Id);
        switch (choice)
        {
            case Choice.Obey:
            case Choice.Fake:
                // Honest or fake, the speaker can't tell the difference. Praise is its own text box.
                string line = CaptainScript.ReportDone;
                if (Random.value < praiseChance)
                    line += "\n" + Pick(CaptainScript.ForMood(CaptainScript.Praise, mood));
                SayNow(Fill(line, seat, o), Priority.Order);
                break;
            case Choice.Refuse:
                SayNow(Fill(Pick(CaptainScript.ForMood(CaptainScript.Refuse, mood)), seat, o), Priority.Order);
                break;
            default:
                if (Random.value < expiredLineChance)
                    SayNow(Fill(Pick(CaptainScript.ForMood(CaptainScript.Expired, mood)), seat, o), Priority.Chatter);
                break;
        }
    }

    void OnFakeNoticed(ActiveOrder o)
    {
        SayNow(Fill(Pick(CaptainScript.FakeNoticed), o.Assignee?.Seat ?? 0, o), Priority.Story);
    }

    void OnIceWarningDelivered(int n)
    {
        if (Random.value < 0.6f)
            Say(Fill(Pick(CaptainScript.ForMood(CaptainScript.IceWarningDelivered, Mood)), PickListener(), null), Priority.Chatter);
    }

    void OnStaticBurst(StokerBrain stoker) => staticUntil = Time.time + 1.6f;

    /// <summary>Order 8 (reopen door four): the Captain knows how order 3 went, though he wasn't there.</summary>
    static string DoorFourAside(ActiveOrder o)
    {
        if (!o.Data.name.Contains("OpenDoorFour")) return null;
        foreach (var c in RunRecord.Choices)
        {
            if (c.order == null || !c.order.name.Contains("CloseDoorFour")) continue;
            return c.choice switch
            {
                Choice.Obey when c.seat == (o.Assignee?.Seat ?? 0) => CaptainScript.DoorFourYouClosed,
                Choice.Obey => Fill(CaptainScript.DoorFourWasClosedBy, c.seat, null),
                Choice.Fake => Fill(CaptainScript.DoorFourWasNeverClosed, c.seat, null),
                _ => CaptainScript.DoorFourWasRefused,
            };
        }
        return null;
    }

    // ---------- Asides in Part 2 ----------

    void Update()
    {
        // Space: finish the text box at once, or if it's already all there, go straight on to the next one. Not while
        // a card or the pause menu has the game stopped, nor on the frame one closes (its Space shouldn't skip too).
        bool paused = Time.timeScale == 0f;
        if (NextPressed() && !paused && !pausedLastFrame && speaking != null)
        {
            if (typing) skipPage = true;
            else nextPage = true;
        }
        pausedLastFrame = paused;

        // Wait for the intro card ("How to play") to close, then the opening line goes first.
        if (speaking == null && queue.Count > 0 && !IntroCard.IsShowing && !PartCard.IsShowing) // and the part's title card (Anadi)
        {
            int at = Mathf.Max(0, queue.FindIndex(l => l.text == CaptainScript.Part1Start));
            current = queue[at];
            queue.RemoveAt(at);
            speaking = StartCoroutine(Speak(current.text, current.priority));
        }

        if (Time.time >= nextAsideAt && speaking == null && queue.Count == 0)
        {
            nextAsideAt = Time.time + Random.Range(asideGap.x, asideGap.y);
            Aside();
        }
    }

    /// <summary>Space on the keyboard, or LB on any controller.</summary>
    static bool NextPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame) return true;
        foreach (var pad in Gamepad.all) if (pad.leftShoulder.wasPressedThisFrame) return true;
        return false;
    }

    void Aside()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.State != GameState.Part2) return;

        if (MeterRules.CaptainSharesPrivateLines)
            Say(Fill(Pick(CaptainScript.PrivateLines), PickListener(), null), Priority.Chatter);
        else if (MeterRules.CaptainSuspicious)
            Say(Fill(Pick(CaptainScript.SuspiciousLines), MostDefiant(), null), Priority.Chatter);
        else
        {
            var rambles = CaptainScript.ForMood(CaptainScript.Rambles, Mood);
            if (rambles.Length > 0) Say(Fill(Pick(rambles), PickListener(), null), Priority.Chatter);
        }
    }

    /// <summary>A human if there is one, so the private line lands on a real player.</summary>
    static int PickListener()
    {
        var humans = new List<int>(4);
        foreach (var c in CrewRegistry.All) if (!c.IsBot) humans.Add(c.Seat);
        if (humans.Count > 0) return humans[Random.Range(0, humans.Count)];
        return CrewRegistry.All.Count > 0 ? CrewRegistry.All[Random.Range(0, CrewRegistry.All.Count)].Seat : 1;
    }

    /// <summary>The seat that refused or ignored him the most.</summary>
    static int MostDefiant()
    {
        var counts = new int[CrewRoster.SeatCount + 1];
        foreach (var c in RunRecord.Choices)
            if (c.seat > 0 && c.seat < counts.Length && (c.choice == Choice.Refuse || c.choice == Choice.Expired)) counts[c.seat]++;
        int best = PickListener();
        for (int s = 1; s < counts.Length; s++) if (counts[s] > counts[best]) best = s;
        return best;
    }

    // ---------- Typewriter ----------

    IEnumerator Speak(string text, Priority priority)
    {
        speakingText = text;
        LineStarted?.Invoke(FinalText(text));
        if (text == CaptainScript.RevealLine) ShipReadout.RevealGreyCove();
        var pages = text.Split('\n');
        int lastPage = pages.Length - 1;
        while (lastPage > 0 && pages[lastPage].Length == 0) lastPage--;

        for (int p = 0; p < pages.Length; p++)
        {
            string page = pages[p];
            if (page.Length == 0) continue;
            // Flavour pages give way only on bot voyages; with a player aboard every box is read.
            if (p > 0 && !WaitsForPlayers && ShouldYield(priority)) break;
            onLastPage = p == lastPage;
            typed.Clear();
            PageStarted?.Invoke(FinalText(page));
            typing = true;
            skipPage = nextPage = false;

            int i = 0;
            while (i < page.Length && !skipPage)
            {
                // {wrong|right}: type the wrong word, pause, erase it, type the right one.
                if (page[i] == '{')
                {
                    int bar = page.IndexOf('|', i);
                    int close = page.IndexOf('}', i);
                    if (bar > i && close > bar)
                    {
                        string wrong = page.Substring(i + 1, bar - i - 1);
                        string right = page.Substring(bar + 1, close - bar - 1);
                        foreach (char c in wrong) { if (skipPage) break; yield return Pause(TypeChar(c)); }
                        yield return Pause(correctionPause);
                        for (int k = 0; k < wrong.Length && !skipPage; k++)
                        {
                            typed.Length--;
                            SetVisible(typed.ToString());
                            yield return Pause(0.5f / charsPerSecond);
                        }
                        yield return Pause(0.15f);
                        foreach (char c in right) { if (skipPage) break; yield return Pause(TypeChar(c)); }
                        i = close + 1;
                        continue;
                    }
                }
                yield return Pause(TypeChar(page[i]));
                i++;
            }

            // Skipped: the whole box at once, corrections already made.
            if (skipPage)
            {
                typed.Clear();
                typed.Append(FinalText(page));
                SetVisible(typed.ToString());
            }
            typing = skipPage = false;

            // Counts as heard once its first text box is fully typed, not when it starts.
            if (p == 0 && text == unspokenFinalLine) unspokenFinalLine = null;
            yield return Hold(holdSeconds + typed.Length * holdPerChar);
        }

        SetVisible("");
        speaking = null;
        speakingText = null;
        LineFinished?.Invoke();
    }

    bool HasOpeningQueued() => queue.Exists(l => l.text == CaptainScript.Part1Start);

    /// <summary>Between pages: give way if something more important is waiting, or (except story) if two lines are.</summary>
    bool ShouldYield(Priority current)
    {
        foreach (var l in queue) if (l.priority > current) return true;
        return current != Priority.Story && queue.Count >= 2;
    }

    /// <summary>
    /// A finished box stays until Space (on an open order's last box, until the order closes). Bot-only voyages just
    /// wait the old timed hold.
    /// </summary>
    IEnumerator Hold(float seconds)
    {
        nextPage = false;
        for (float t = 0f; ; t += Time.deltaTime)
        {
            if (!WaitsForPlayers) { if (t >= seconds || nextPage) break; }
            else if (HoldingForOrder) nextPage = false; // stuck on the order: Space does nothing
            else if (nextPage) break;
            yield return null;
        }
        nextPage = false;
    }

    /// <summary>Waits in game time, cut short by Space (skipPage while typing, nextPage while holding).</summary>
    IEnumerator Pause(float seconds)
    {
        for (float t = 0f; t < seconds && !skipPage && !nextPage; t += Time.deltaTime) yield return null;
    }

    /// <summary>Types one letter and returns how long to wait before the next.</summary>
    float TypeChar(char c)
    {
        typed.Append(c);
        SetVisible(typed.ToString());
        if (!char.IsWhiteSpace(c) && !char.IsPunctuation(c) && (blipCount++ & 1) == 0) DoBlip(c);
        int mood = Mood;
        float wait = 1f / (charsPerSecond * MoodValue(moodTypingSpeed, mood, 1f));
        if (c == '.' || c == ',' || c == '?' || c == '!' || c == ':') wait += punctuationPause;
        if (Random.value < MoodValue(moodStumbleChance, mood, 0f)) wait += Random.Range(0.1f, 0.3f); // *hic*
        return wait;
    }

    void SetVisible(string s)
    {
        VisibleText = s;
        TextChanged?.Invoke(s);
    }

    float EstimateSeconds(string text, int mood)
    {
        float t = 0.3f;
        float cps = charsPerSecond * MoodValue(moodTypingSpeed, mood, 1f);
        foreach (var page in text.Split('\n'))
        {
            string final = FinalText(page);
            int corrections = 0, stops = 0;
            foreach (char c in page) if (c == '|') corrections++;
            foreach (char c in final) if (c == '.' || c == ',' || c == '?' || c == '!' || c == ':') stops++;
            t += final.Length / cps + stops * punctuationPause + holdSeconds + final.Length * holdPerChar + corrections * (correctionPause + 0.6f);
        }
        return t;
    }

    static float MoodValue(float[] values, int mood, float fallback) =>
        values != null && values.Length > 0 ? values[Mathf.Clamp(mood, 0, values.Length - 1)] : fallback;

    /// <summary>The line as it finally reads: {wrong|right} becomes right.</summary>
    static string FinalText(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                int bar = text.IndexOf('|', i);
                int close = text.IndexOf('}', i);
                if (bar > i && close > bar)
                {
                    sb.Append(text, bar + 1, close - bar - 1);
                    i = close;
                    continue;
                }
            }
            sb.Append(text[i]);
        }
        return sb.ToString();
    }

    static string Fill(string line, int seat, ActiveOrder o)
    {
        line = line.Replace("{seat}", seat > 0 ? CrewRegistry.SeatName(seat) : "someone");
        if (o != null) line = line.Replace("{task}", o.Data.resultsLine);
        return line;
    }

    static string Pick(string[] lines) => lines[Random.Range(0, lines.Length)];

    // ---------- His voice ----------
    // Undertale-style: a short sound per couple of letters, but his own. A warm, slightly gravelly baritone saying a
    // vowel ("ah", "eh", "ee", "oh", "oo"), picked from the letter so a word always sounds the same. Synthesised
    // here (harmonics shaped by vowel formants, a little breath and gravel), one set per mood:
    //   0  sober: brisk, bright, a confident little lift at the start of each syllable
    //   1-3  "drunk": lower, longer, sliding downhill, wobbling, vowels going mushy toward "uh", more breath
    //   4  Part 3, serious again: lowest, short and flat, no wobble, dark and gravelly; the act is over

    const int VoiceRate = 22050;
    static readonly float[] VoiceF0 = { 120f, 113f, 105f, 97f, 88f };          // Hz
    static readonly float[] VoiceLength = { 0.065f, 0.075f, 0.09f, 0.11f, 0.06f };
    static readonly float[] VoiceLift = { 0.06f, 0.03f, 0f, 0f, 0f };          // pitch rise into the syllable
    static readonly float[] VoiceGlide = { 0.04f, 0.09f, 0.16f, 0.26f, 0.01f }; // pitch slide down across it
    static readonly float[] VoiceVibrato = { 0.008f, 0.02f, 0.04f, 0.065f, 0f };
    static readonly float[] VoiceVibratoRate = { 6f, 5.5f, 4.5f, 3.5f, 0f };
    static readonly float[] VoiceBreath = { 0.035f, 0.05f, 0.08f, 0.11f, 0.03f };
    static readonly float[] VoiceGravel = { 0.12f, 0.12f, 0.16f, 0.2f, 0.32f };
    static readonly float[] VoiceBright = { 1.15f, 1.05f, 0.9f, 0.78f, 0.7f };
    static readonly float[] VoiceSlur = { 0f, 0.15f, 0.35f, 0.55f, 0f };        // vowels drift toward "uh"
    static readonly float[] VoiceWobble = { 0.035f, 0.06f, 0.09f, 0.13f, 0.012f }; // random pitch per syllable

    // F1, F2, F3 (Hz) for ah, eh, ee, oh, oo, and "uh" for the slur.
    static readonly Vector3[] Vowels =
    {
        new Vector3(730f, 1090f, 2440f), new Vector3(530f, 1840f, 2480f), new Vector3(300f, 2200f, 2950f),
        new Vector3(570f, 840f, 2410f), new Vector3(320f, 870f, 2240f),
    };
    static readonly Vector3 Schwa = new Vector3(520f, 1400f, 2450f);

    AudioClip[,] voice;

    void MakeBlip()
    {
        voice = new AudioClip[5, Vowels.Length];
        var rng = new System.Random(1912);
        for (int m = 0; m < 5; m++)
            for (int v = 0; v < Vowels.Length; v++)
                voice[m, v] = Syllable(m, v, rng);
        blipSource = gameObject.AddComponent<AudioSource>();
        blipSource.playOnAwake = false;
        blipSource.spatialBlend = 0f;
    }

    static AudioClip Syllable(int mood, int vowel, System.Random rng)
    {
        float len = VoiceLength[mood], f0 = VoiceF0[mood];
        var formant = Vector3.Lerp(Vowels[vowel], Schwa, VoiceSlur[mood]);
        float bright = VoiceBright[mood];
        int n = Mathf.CeilToInt(VoiceRate * len);
        var data = new float[n];
        double phase = 0;
        float noise = 0f, peak = 0.0001f;
        float attack = mood == 4 ? 0.004f : 0.006f + 0.004f * mood;
        var tilt = new float[64]; // the voice's natural roll-off, 1 / k^0.5 (gentle, for a little treble)
        for (int k = 1; k < tilt.Length; k++) tilt[k] = 1f / Mathf.Pow(k, 0.5f);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / VoiceRate, u = t / len;
            float f = f0 * (1f + VoiceLift[mood] * Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 2.5f)) - VoiceGlide[mood] * u * u)
                         * (1f + VoiceVibrato[mood] * Mathf.Sin(2f * Mathf.PI * VoiceVibratoRate[mood] * t));
            phase += 2.0 * System.Math.PI * f / VoiceRate;

            // Harmonics of the voice, each weighted by how close it sits to the vowel's three formants.
            float s = 0f;
            for (int k = 1; k * f < 5000f && k < tilt.Length; k++)
            {
                float h = k * f;
                float g = Resonance(h, formant.x, 90f) + bright * 0.7f * Resonance(h, formant.y, 120f)
                        + bright * 0.35f * Resonance(h, formant.z, 180f)
                        + bright * 0.18f * Resonance(h, 3400f, 400f) + 0.04f; // presence, so he cuts through the ship
                s += (float)System.Math.Sin(k * phase) * g * tilt[k];
            }
            // Gravel: a slow flutter at half the pitch, like a voice worn by shouting over engines.
            s *= 1f - VoiceGravel[mood] * 0.5f * (1f + (float)System.Math.Sin(phase * 0.5));
            // Breath: soft filtered noise under it.
            noise += 0.5f * ((float)rng.NextDouble() * 2f - 1f - noise);
            s += noise * VoiceBreath[mood] * 6f;

            float env = Mathf.Min(1f, t / attack) * (u < 0.5f ? 1f : Mathf.Pow(1f - (u - 0.5f) / 0.5f, 2f));
            data[i] = s * env;
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        for (int i = 0; i < n; i++) data[i] *= 0.9f / peak;
        var clip = AudioClip.Create($"CaptainVoice_{mood}_{vowel}", n, 1, VoiceRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Resonance(float freq, float centre, float width)
    {
        float d = (freq - centre) / width;
        return 1f / (1f + d * d);
    }

    /// <summary>The same letter always gets the same vowel, so words keep their shape.</summary>
    static int VowelFor(char c)
    {
        switch (char.ToLowerInvariant(c))
        {
            case 'a': return 0;
            case 'e': return 1;
            case 'i': case 'y': return 2;
            case 'o': return 3;
            case 'u': case 'w': return 4;
            default: return char.ToLowerInvariant(c) % Vowels.Length;
        }
    }

    void DoBlip(char c)
    {
        Blip?.Invoke();
        if (!placeholderBlip || blipSource == null || voice == null) return;
        int mood = Mathf.Clamp(Mood, 0, 4);
        float pitch = MoodValue(moodVoicePitch, mood, 1f) * Random.Range(1f - VoiceWobble[mood], 1f + VoiceWobble[mood]);
        if (mood == 3 && Random.value < 0.1f) pitch *= 0.86f; // the odd syllable that falls off a cliff
        // Each syllable cuts the last, like Undertale's voices, so it reads as speech rather than a buzz.
        blipSource.Stop();
        blipSource.clip = voice[mood, VowelFor(c)];
        blipSource.pitch = pitch;
        blipSource.volume = blipVolume;
        blipSource.Play();
    }

    // ---------- Fallback subtitles ----------

    GUIStyle subtitleStyle, nameStyle;

    /// <summary>Undertale-style placeholder: black box, white border, text typed top-left. Wobbles when he's drunk.</summary>
    void OnGUI()
    {
        if (!DrawFallbackSubtitles) return;
        bool showStatic = Time.time < staticUntil;
        if (VisibleText.Length == 0 && !showStatic) return;

        float s = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s;

        subtitleStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 32, wordWrap = true, alignment = TextAnchor.UpperLeft, richText = false,
            normal = { textColor = Color.white },
        };
        nameStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };

        var box = new Rect(w * 0.5f - 600f, 1080f - 230f, 1200f, 180f);
        if (VisibleText.Length > 0)
        {
            var old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(box.x + 5f, box.y + 5f, box.width - 10f, box.height - 10f), Texture2D.whiteTexture);
            GUI.color = old;

            int mood = Mood;
            float shake = mood >= 2 && mood <= 3 ? (mood - 1) * 1.2f : 0f;
            var jitter = new Vector2(Random.Range(-shake, shake), Random.Range(-shake, shake));
            GUI.Label(new Rect(box.x + 30f, box.y + 14f, 200f, 26f), "CAPTAIN", nameStyle);
            GUI.Label(new Rect(box.x + 30f + jitter.x, box.y + 46f + jitter.y, box.width - 60f, box.height - 56f), VisibleText, subtitleStyle);
        }
        if (showStatic)
            GUI.Label(new Rect(box.x + box.width - 220f, box.y + 14f, 200f, 26f), "~ kssshhht ~", nameStyle);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        LineStarted = null;
        PageStarted = null;
        TextChanged = null;
        Blip = null;
        LineFinished = null;
        DrawFallbackSubtitles = true;
    }
}
