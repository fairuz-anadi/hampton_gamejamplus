using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// The voice on the speaker. Queues the Captain's lines, types them out as subtitles, blips per letter,
/// and reacts to orders, reports, refusals and caught fakes. Owner: Riham.
///
/// Samprity: build the styled subtitle from TextChanged (or VisibleText), play the blip voice on Blip and
/// speaker static on StokerBrain.StaticBurst, then set CaptainDirector.DrawFallbackSubtitles = false.
/// Anadi: the Truth monologue runs about MonologueSeconds; GameManager.truthSeconds should be at least that.
/// </summary>
public class CaptainDirector : MonoBehaviour
{
    public static CaptainDirector Instance { get; private set; }

    /// <summary>A new line starts. The text is the final wording, with corrections already applied.</summary>
    public static event System.Action<string> LineStarted;
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

    [Header("Chatter")]
    [SerializeField, Range(0f, 1f)] float praiseChance = 0.5f;
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
    float staticUntil;

    AudioSource blipSource;
    AudioClip blipClip;
    int blipCount;

    public string VisibleText { get; private set; } = "";
    public bool IsSpeaking => speaking != null;

    /// <summary>Roughly how long the Truth monologue takes to say, in seconds.</summary>
    public float MonologueSeconds
    {
        get
        {
            float t = 0f;
            foreach (var line in CaptainScript.Monologue) t += EstimateSeconds(line);
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

    public void Silence()
    {
        queue.Clear();
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
        foreach (var line in CaptainScript.Monologue) Say(line, Priority.Story);
    }

    void OnOrderIssued(ActiveOrder o)
    {
        string who = o.Assignee != null ? CrewRegistry.SeatName(o.Assignee.Seat) + ". " : "";
        string line = who + o.Data.captainLine;

        string knows = DoorFourAside(o);
        if (knows != null) line += " " + knows;

        Say(line, Priority.Order);
    }

    void OnOrderResolved(ActiveOrder o, Choice choice)
    {
        int seat = o.Assignee?.Seat ?? 0;
        bool off = o.Data.IsOff;
        switch (choice)
        {
            case Choice.Obey:
            case Choice.Fake:
                // Honest or fake, the speaker can't tell the difference.
                string line = CaptainScript.ReportDone;
                if (Random.value < praiseChance)
                    line += " " + Pick(off ? CaptainScript.PraiseOff : CaptainScript.PraiseCalm);
                Say(Fill(line, seat, o), Priority.Order);
                break;
            case Choice.Refuse:
                Say(Fill(Pick(off ? CaptainScript.RefuseOff : CaptainScript.RefuseNormal), seat, o), Priority.Order);
                break;
            default:
                if (Random.value < expiredLineChance)
                    Say(Fill(Pick(o.Data.part == 1 ? CaptainScript.ExpiredCalm : CaptainScript.ExpiredOff), seat, o), Priority.Chatter);
                break;
        }
    }

    void OnFakeNoticed(ActiveOrder o)
    {
        Say(Fill(Pick(CaptainScript.FakeNoticed), o.Assignee?.Seat ?? 0, o), Priority.Story);
    }

    void OnIceWarningDelivered(int n)
    {
        if (Random.value < 0.6f) Say(Pick(CaptainScript.IceWarningDelivered), Priority.Chatter);
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
        if (speaking == null && queue.Count > 0)
        {
            var next = queue[0];
            queue.RemoveAt(0);
            speaking = StartCoroutine(Speak(next.text));
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

    IEnumerator Speak(string text)
    {
        typed.Clear();
        LineStarted?.Invoke(FinalText(text));

        int i = 0;
        while (i < text.Length)
        {
            // {wrong|right}: type the wrong word, pause, erase it, type the right one.
            if (text[i] == '{')
            {
                int bar = text.IndexOf('|', i);
                int close = text.IndexOf('}', i);
                if (bar > i && close > bar)
                {
                    string wrong = text.Substring(i + 1, bar - i - 1);
                    string right = text.Substring(bar + 1, close - bar - 1);
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
            yield return TypeChar(text[i]);
            i++;
        }

        yield return new WaitForSeconds(holdSeconds + typed.Length * holdPerChar);
        SetVisible("");
        speaking = null;
        LineFinished?.Invoke();
    }

    WaitForSeconds TypeChar(char c)
    {
        typed.Append(c);
        SetVisible(typed.ToString());
        if (!char.IsWhiteSpace(c) && !char.IsPunctuation(c) && (blipCount++ & 1) == 0) DoBlip();
        float wait = 1f / charsPerSecond;
        if (c == '.' || c == ',' || c == '?' || c == '!' || c == ':') wait += punctuationPause;
        return new WaitForSeconds(wait);
    }

    void SetVisible(string s)
    {
        VisibleText = s;
        TextChanged?.Invoke(s);
    }

    float EstimateSeconds(string text)
    {
        string final = FinalText(text);
        int corrections = 0;
        foreach (char c in text) if (c == '|') corrections++;
        return final.Length / charsPerSecond + holdSeconds + final.Length * holdPerChar + corrections * (correctionPause + 0.6f) + 0.3f;
    }

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
        // The voice gets lower and slower-feeling each part.
        int part = GameManager.Instance != null ? GameManager.Instance.Part : 1;
        float basePitch = part >= 3 ? 0.75f : part == 2 ? 0.88f : 1f;
        blipSource.pitch = basePitch * Random.Range(0.92f, 1.08f);
        blipSource.PlayOneShot(blipClip, blipVolume);
    }

    // ---------- Fallback subtitles ----------

    GUIStyle subtitleStyle, nameStyle;

    void OnGUI()
    {
        if (!DrawFallbackSubtitles) return;
        bool showStatic = Time.time < staticUntil;
        if (VisibleText.Length == 0 && !showStatic) return;

        float s = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s;

        subtitleStyle ??= new GUIStyle(GUI.skin.box)
        {
            fontSize = 30, wordWrap = true, alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(24, 24, 16, 16), richText = false,
        };
        nameStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter };

        var rect = new Rect(w * 0.5f - 600f, 1080f - 200f, 1200f, 130f);
        if (VisibleText.Length > 0)
        {
            GUI.Label(new Rect(rect.x, rect.y - 32f, rect.width, 30f), "CAPTAIN", nameStyle);
            GUI.Box(rect, VisibleText, subtitleStyle);
        }
        if (showStatic)
            GUI.Label(new Rect(rect.x, rect.y - 64f, rect.width, 30f), "~ kssshhht ~", nameStyle);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        LineStarted = null;
        TextChanged = null;
        Blip = null;
        LineFinished = null;
        DrawFallbackSubtitles = true;
    }
}
