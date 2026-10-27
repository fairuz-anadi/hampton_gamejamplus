using System.Text;
using UnityEngine;

/// <summary>
/// The "something's off" moments that live on the HUD. Owner: Riham. Samprity draws the real HUD from these
/// values and then sets OffMoments.DrawFallbackHud = false.
///
///   Compass vs wheel:  CompassHeading slowly drifts north through Part 2 (more after "two points north" is obeyed),
///                      while WheelHeading stays on the course. The wheel says straight, the compass doesn't.
///   Ship's clock:      ClockText runs from 21:20 toward 23:40, and once in Part 2 it quietly skips back a minute.
///   ETA:               EtaText, "Promised Port: ETA 2 days", goes UP after every off order that's carried out,
///                      and reads "—" in Part 3.
///   Wireless:          WirelessMessage fires with the warning's text when an ice warning arrives. Words in
///                      [brackets] in CaptainScript.IceWarningText would arrive blacked out (none are, right now).
/// (The voice getting lower as he drinks is in CaptainDirector.)
/// </summary>
public class OffMoments : MonoBehaviour
{
    public static OffMoments Instance { get; private set; }

    /// <summary>Simple IMGUI compass/clock/ETA panel until the real HUD exists.</summary>
    public static bool DrawFallbackHud = true;

    /// <summary>An ice warning arrived at the Wireless desk: its number and its text.</summary>
    public static event System.Action<int, string> WirelessMessage;

    [Header("Compass")]
    [Tooltip("The course the ship should hold, in degrees. 262 is roughly west-south-west, toward New York.")]
    [SerializeField] float course = 262f;
    [Tooltip("How far the compass drifts north over Part 2, in degrees.")]
    [SerializeField] float part2Drift = 14f;
    [SerializeField] float part2DriftSeconds = 240f;
    [Tooltip("Obeying 'Two points north' (order 11) turns the real heading this much.")]
    [SerializeField] float twoPointsNorth = 22.5f;

    [Header("Clock")]
    [SerializeField] int clockStartMinutes = 21 * 60 + 20;
    [Tooltip("Ship's-clock minutes per real second. 0.27 takes a 9 minute run from 21:20 to about 23:45.")]
    [SerializeField] float clockMinutesPerSecond = 0.27f;
    [SerializeField] float clockSkipIntoPart2 = 50f;

    [Header("ETA")]
    [SerializeField] float etaStartHours = 48f;
    [SerializeField] float etaPerOffOrder = 7f;

    [Header("Fallback HUD")]
    [SerializeField] float messageSeconds = 8f;

    float runStart;
    float part2Start = -1f;
    int part;
    float extraDrift;
    float clockOffsetMinutes;
    bool clockSkipped;
    float etaHours;
    string lastMessage;
    float messageUntil;

    /// <summary>What the compass shows, in degrees (0 = north).</summary>
    public float CompassHeading
    {
        get
        {
            float drift = extraDrift;
            if (part2Start >= 0f) drift += part2Drift * Mathf.Clamp01((Time.time - part2Start) / part2DriftSeconds);
            // Drifting north from a westerly course means the number goes up toward 360.
            return Mathf.Repeat(course + drift, 360f);
        }
    }

    /// <summary>What the wheel's indicator shows: always the course. That's the point.</summary>
    public float WheelHeading => course;

    public int ClockMinutes => clockStartMinutes + Mathf.FloorToInt((Time.time - runStart) * clockMinutesPerSecond + clockOffsetMinutes);
    public string ClockText => $"{(ClockMinutes / 60) % 24:00}:{ClockMinutes % 60:00}";

    public string EtaText
    {
        get
        {
            if (part >= 3) return "Promised Port: ETA —";
            int days = Mathf.FloorToInt(etaHours / 24f);
            int hours = Mathf.RoundToInt(etaHours - days * 24f);
            string d = days == 1 ? "1 day" : $"{days} days";
            return hours == 0 ? $"Promised Port: ETA {d}" : $"Promised Port: ETA {d} {hours} h";
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        ResetRun();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        GameEvents.StateChanged += OnStateChanged;
        GameEvents.PartChanged += OnPartChanged;
        GameEvents.OrderResolved += OnOrderResolved;
        GameEvents.IceWarningArrived += OnIceWarningArrived;
    }

    void OnDisable()
    {
        GameEvents.StateChanged -= OnStateChanged;
        GameEvents.PartChanged -= OnPartChanged;
        GameEvents.OrderResolved -= OnOrderResolved;
        GameEvents.IceWarningArrived -= OnIceWarningArrived;
    }

    void ResetRun()
    {
        runStart = Time.time;
        part2Start = -1f;
        part = 1;
        extraDrift = 0f;
        clockOffsetMinutes = 0f;
        clockSkipped = false;
        etaHours = etaStartHours;
        messageUntil = 0f;
    }

    void OnStateChanged(GameState s)
    {
        if (s == GameState.Part1) ResetRun();
    }

    void OnPartChanged(int p)
    {
        part = p;
        if (p == 2 && part2Start < 0f) part2Start = Time.time;
    }

    void OnOrderResolved(ActiveOrder o, Choice choice)
    {
        if (!o.Data.IsOff || choice == Choice.Refuse) return;
        // Faked or done, the Captain's plan moved forward, and the port somehow gets further away.
        if (choice != Choice.Expired) etaHours += etaPerOffOrder;
        if (choice == Choice.Obey && o.Data.name.Contains("TwoPointsNorth")) extraDrift += twoPointsNorth;
    }

    void OnIceWarningArrived(int n)
    {
        lastMessage = Scramble(n);
        messageUntil = Time.time + messageSeconds;
        WirelessMessage?.Invoke(n, lastMessage);
    }

    void Update()
    {
        // The clock skips back a minute, once, a little way into Part 2.
        if (!clockSkipped && part2Start >= 0f && Time.time - part2Start >= clockSkipIntoPart2)
        {
            clockSkipped = true;
            clockOffsetMinutes -= 1f;
        }
    }

    /// <summary>The warning text with its [bracketed] words blacked out.</summary>
    public static string Scramble(int n)
    {
        var lines = CaptainScript.IceWarningText;
        if (n < 1 || n > lines.Length) return $"MSG {n}. ICE.";
        var src = lines[n - 1];
        var sb = new StringBuilder(src.Length);
        for (int i = 0; i < src.Length; i++)
        {
            if (src[i] == '[')
            {
                int close = src.IndexOf(']', i);
                if (close > i)
                {
                    sb.Append('█', Mathf.Max(3, close - i - 1));
                    i = close;
                    continue;
                }
            }
            sb.Append(src[i]);
        }
        return sb.ToString();
    }

    /// <summary>"WNW", "N" and so on for a heading in degrees.</summary>
    public static string PointName(float degrees)
    {
        string[] points = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
        return points[Mathf.RoundToInt(Mathf.Repeat(degrees, 360f) / 22.5f) % 16];
    }

    // ---------- Fallback HUD ----------

    GUIStyle box, msgStyle;

    void OnGUI()
    {
        if (!DrawFallbackHud) return;
        var gm = GameManager.Instance;
        if (gm == null || gm.Part < 1 || gm.Part > 3) return;

        float s = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s;

        box ??= new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.UpperLeft, padding = new RectOffset(16, 16, 10, 10), richText = true };
        msgStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 22, wordWrap = true, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(16, 16, 12, 12) };

        float c = CompassHeading;
        string hud = $"Compass   {PointName(c)} {c:000}°\nWheel     {PointName(WheelHeading)} {WheelHeading:000}°\nShip's clock  {ClockText}\n{EtaText}";
        GUI.Box(new Rect(w - 400f, 20f, 380f, 140f), hud, box);

        if (Time.time < messageUntil && !string.IsNullOrEmpty(lastMessage))
            GUI.Box(new Rect(w - 620f, 175f, 600f, 110f), "WIRELESS  " + lastMessage, msgStyle);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        WirelessMessage = null;
        DrawFallbackHud = true;
    }
}
