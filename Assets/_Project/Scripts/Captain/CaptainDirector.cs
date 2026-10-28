using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// The voice on the speaker. Queues the Captain's lines, types them out Undertale-style one text box (page) at a time,
/// blips per letter, and reacts to orders, reports, refusals and caught fakes. Owner: Riham.
///
/// He gets drunker as the voyage goes on (Mood 0-4, see CaptainScript): his lines change, he types slower and
/// with stumbles, and his voice drops. Lines are split into pages on "\n". A line stops after its current page
/// when something more important is waiting (or the speaker is backed up), so the first page of an order is
/// always heard and the flavour pages give way.
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
    [SerializeField] float[] moodVoicePitch = { 1f, 0.95f, 0.88f, 0.8f, 0.72f };
    [Tooltip("Chance per letter of a little drunken stumble (pause) at moods 2 and 3.")]
    [SerializeField] float[] moodStumbleChance = { 0f, 0f, 0.03f, 0.08f, 0f };

    [Header("Chatter")]
    [SerializeField, Range(0f, 1f)] float praiseChance = 0.6f;
    [SerializeField, Range(0f, 1f)] float expiredLineChance = 0.6f;
    [SerializeField] Vector2 asideGap = new Vector2(30f, 45f);

    [Header("Placeholder voice")]
    [SerializeField] bool placeholderBlip = true;
    [SerializeField, Range(0f, 1f)] float blipVolume = 0.2f;

    enum Priority { Chatter, Order, Story }

    struct Line
    {
        public string text;
        public Priority priority;
    }

    readonly List<Line> queue = new List<Line>(8);
    readonly StringBuilder typed = new StringBuilder(160);
    Coroutine speaking;
    float nextAsideAt = float.MaxValue;
    bool monologueStarted;
    int lastMood;
    /// <summary>The final order's line until it has been spoken; the monologue opens with it if it hasn't.</summary>
    string unspokenFinalLine;
    float staticUntil;

    AudioSource blipSource;
    AudioClip blipClip;
    int blipCount;

    public string VisibleText { get; private set; } = "";

    /// <summary>
    /// True once the voyage's opening line ("Ahem. Is this on?...") has been said. Nothing else on the speaker comes
    /// before it, and TutorialHints stays quiet until then.
    /// </summary>
    public bool OpeningDone => !HasOpeningQueued() && speakingText != CaptainScript.Part1Start;

    string speakingText;
    public bool IsSpeaking => speaking != null;

    /// <summary>
    /// How far gone he is: 0 Part 1 (sober), 1 orders 6-7, 2 orders 8-9, 3 orders 10-11, 4 Part 3 (bottle empty).
    /// Samprity: use it to pick the portrait.
    /// </summary>
    public int Mood
    {
        get
        {
            int part = GameManager.Instance != null ? GameManager.Instance.Part : 1;
            if (part <= 1) return 0;
            if (part >= 3) return 4;
            int issued = OrderDirector.Instance != null ? OrderDirector.Instance.NextIndex : 6;
            return issued <= 7 ? 1 : issued <= 9 ? 2 : 3;
        }
    }

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
    void Say(string text, Priority priority, bool interrupt = false)
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
        queue.Insert(at, new Line { text = text, priority = priority });
    }

    /// <summary>For other systems (e.g. the muster vote): the Captain says something now-ish.</summary>
    public void SayStory(string text) => Say(text, Priority.Story);

    /// <summary>For TutorialHints: queued like an order line, so it never jumps ahead of the order it's about.</summary>
    public void SayTutorial(string text) => Say(text, Priority.Order);

    public void Silence()
    {
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
        Say(line, Priority.Order);
    }

    void OnOrderResolved(ActiveOrder o, Choice choice)
    {
        int seat = o.Assignee?.Seat ?? 0;
        int mood = Mood;
        switch (choice)
        {
            case Choice.Obey:
            case Choice.Fake:
                // Honest or fake, the speaker can't tell the difference. Praise is its own text box.
                string line = CaptainScript.ReportDone;
                if (Random.value < praiseChance)
                    line += "\n" + Pick(CaptainScript.ForMood(CaptainScript.Praise, mood));
                Say(Fill(line, seat, o), Priority.Order);
                break;
            case Choice.Refuse:
                Say(Fill(Pick(CaptainScript.ForMood(CaptainScript.Refuse, mood)), seat, o), Priority.Order);
                break;
            default:
                if (Random.value < expiredLineChance)
                    Say(Fill(Pick(CaptainScript.ForMood(CaptainScript.Expired, mood)), seat, o), Priority.Chatter);
                break;
        }
    }

    void OnFakeNoticed(ActiveOrder o)
    {
        Say(Fill(Pick(CaptainScript.FakeNoticed), o.Assignee?.Seat ?? 0, o), Priority.Story);
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
        // Wait for the intro card ("How to play") to close, then the opening line goes first.
        if (speaking == null && queue.Count > 0 && !IntroCard.IsShowing && !PartCard.IsShowing) // and the part's title card (Anadi)
        {
            int at = Mathf.Max(0, queue.FindIndex(l => l.text == CaptainScript.Part1Start));
            var next = queue[at];
            queue.RemoveAt(at);
            speaking = StartCoroutine(Speak(next.text, next.priority));
        }

        if (Time.time >= nextAsideAt && speaking == null && queue.Count == 0)
        {
            nextAsideAt = Time.time + Random.Range(asideGap.x, asideGap.y);
            Aside();
        }
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

        for (int p = 0; p < pages.Length; p++)
        {
            string page = pages[p];
            if (page.Length == 0) continue;
            if (p > 0 && ShouldYield(priority)) break;
            typed.Clear();
            PageStarted?.Invoke(FinalText(page));

            int i = 0;
            while (i < page.Length)
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
                        foreach (char c in wrong) yield return TypeChar(c);
                        yield return new WaitForSeconds(correctionPause);
                        for (int k = 0; k < wrong.Length; k++)
                        {
                            typed.Length--;
                            SetVisible(typed.ToString());
                            yield return new WaitForSeconds(0.5f / charsPerSecond);
                        }
                        yield return new WaitForSeconds(0.15f);
                        foreach (char c in right) yield return TypeChar(c);
                        i = close + 1;
                        continue;
                    }
                }
                yield return TypeChar(page[i]);
                i++;
            }

            // Counts as heard once its first text box is fully typed, not when it starts.
            if (p == 0 && text == unspokenFinalLine) unspokenFinalLine = null;
            yield return new WaitForSeconds(holdSeconds + typed.Length * holdPerChar);
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

    WaitForSeconds TypeChar(char c)
    {
        typed.Append(c);
        SetVisible(typed.ToString());
        if (!char.IsWhiteSpace(c) && !char.IsPunctuation(c) && (blipCount++ & 1) == 0) DoBlip();
        int mood = Mood;
        float wait = 1f / (charsPerSecond * MoodValue(moodTypingSpeed, mood, 1f));
        if (c == '.' || c == ',' || c == '?' || c == '!' || c == ':') wait += punctuationPause;
        if (Random.value < MoodValue(moodStumbleChance, mood, 0f)) wait += Random.Range(0.1f, 0.3f); // *hic*
        return new WaitForSeconds(wait);
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

    // ---------- Placeholder blip voice ----------

    void MakeBlip()
    {
        const int rate = 22050;
        int n = rate * 35 / 1000;
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 180f * t));
            data[i] = square * (1f - (float)i / n) * 0.5f;
        }
        blipClip = AudioClip.Create("CaptainBlip", n, 1, rate, false);
        blipClip.SetData(data, 0);
        blipSource = gameObject.AddComponent<AudioSource>();
        blipSource.playOnAwake = false;
        blipSource.spatialBlend = 0f;
    }

    void DoBlip()
    {
        Blip?.Invoke();
        if (!placeholderBlip || blipSource == null) return;
        // The voice drops with every drink, and wobbles more.
        int mood = Mood;
        float wobble = 0.08f + 0.04f * Mathf.Min(mood, 3);
        blipSource.pitch = MoodValue(moodVoicePitch, mood, 1f) * Random.Range(1f - wobble, 1f + wobble);
        blipSource.PlayOneShot(blipClip, blipVolume);
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
