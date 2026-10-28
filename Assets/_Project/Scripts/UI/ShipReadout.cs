using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The ship's instruments in the HUD, right-aligned beside the time-of-day dial: compass, wheel, ship's clock and the
/// ETA, plus the wireless message when an ice warning comes in. Riham's OffMoments makes the numbers drift; this only
/// draws them, and turns OffMoments' plain fallback box off so the two never overlap. Built by HudBuilder.
///
/// The reveal (final plan, Part 3): partway into the Captain's last speech the PORT row glitches and turns into
/// GREY COVE, counting down from 20 minutes to the iceberg. Riham can call ShipReadout.RevealGreyCove() from the exact
/// line instead; otherwise it fires at revealAt of the truth wait.
/// </summary>
public class ShipReadout : MonoBehaviour
{
    public static ShipReadout Instance { get; private set; }

    public CanvasGroup group;
    public Text compass, wheel, clock, eta;
    public CanvasGroup wirelessGroup;
    public Text wireless;
    [Min(1f)] public float wirelessSeconds = 8f;

    [Header("Grey Cove reveal")]
    [Tooltip("When in the Captain's last speech the readout turns, as a share of the truth wait.")]
    [Range(0f, 1f)] public float revealAt = 0.6f;
    [Min(0.1f)] public float glitchSeconds = 1.8f;
    public int minutesToCove = 20;
    public Color revealColor = new Color(0.95f, 0.38f, 0.3f);

    float wirelessLeft;
    Text etaLabel;
    Color labelColor, valueColor;
    float truthStart = -1f, truthLength, revealTime = -1f;

    void Awake()
    {
        Instance = this;
        // The PORT row's label, built beside the value by HudBuilder.
        var label = eta != null ? eta.transform.parent.Find("PortLabel") : null;
        if (label != null) etaLabel = label.GetComponent<Text>();
        if (etaLabel != null) labelColor = etaLabel.color;
        if (eta != null) valueColor = eta.color;
    }

    void OnEnable()
    {
        OffMoments.DrawFallbackHud = false;
        OffMoments.WirelessMessage += OnWireless;
        GameEvents.TruthStarted += OnTruth;
    }

    void OnDisable()
    {
        OffMoments.DrawFallbackHud = true;
        OffMoments.WirelessMessage -= OnWireless;
        GameEvents.TruthStarted -= OnTruth;
        if (Instance == this) Instance = null;
    }

    /// <summary>Puts any text on the wireless strip for a while (ShipClues' message meant for the Captain).</summary>
    public void ShowWireless(string text, float seconds)
    {
        wireless.text = text;
        wirelessLeft = seconds;
    }

    void OnWireless(int number, string text)
    {
        wireless.text = text;
        wirelessLeft = wirelessSeconds;
    }

    void OnTruth()
    {
        truthStart = Time.time;
        truthLength = GameManager.Instance != null ? GameManager.Instance.TruthWaitSeconds : 60f;
        if (revealTime < 0f) revealTime = truthStart + truthLength * revealAt;
    }

    /// <summary>Turn the readout to Grey Cove now (for the Captain's reveal line).</summary>
    public static void RevealGreyCove()
    {
        if (Instance != null && (Instance.revealTime < 0f || Instance.revealTime > Time.time)) Instance.revealTime = Time.time;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        var off = OffMoments.Instance;
        bool show = off != null && gm != null && gm.Part >= 1 && gm.Part <= 3;
        float dt = Time.unscaledDeltaTime;
        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, dt * 3f);

        wirelessLeft -= dt;
        wirelessGroup.alpha = Mathf.MoveTowards(wirelessGroup.alpha, show && wirelessLeft > 0f ? 1f : 0f, dt * 3f);
        if (!show) return;

        float c = off.CompassHeading, w = off.WheelHeading;
        compass.text = $"{OffMoments.PointName(c)} {c:000}°";
        wheel.text = $"{OffMoments.PointName(w)} {w:000}°";
        clock.text = off.ClockText;
        Notice(off, c, w);

        if (revealTime >= 0f && Time.time >= revealTime) DrawGreyCove();
        else eta.text = off.EtaText.Replace("Promised Port: ", "");
        Flag(eta, ref etaFlash);
    }

    // ---------- Making the clues noticeable (Anadi, 28 Oct) ----------

    float compassFlash, clockFlash, etaFlash;
    int lastClock = -1;
    string lastEta;

    /// <summary>
    /// The readout reacts when something doesn't add up: the compass turns red and pulses once it no longer agrees with
    /// the wheel, the clock flickers red when it jumps back, the arrival time flashes when it gets longer. The first
    /// time each happens, the player stops and thinks about it (PlayerThought).
    /// </summary>
    void Notice(OffMoments off, float c, float w)
    {
        float drift = Mathf.Abs(Mathf.DeltaAngle(w, c));
        if (drift >= 2f)
        {
            compassFlash = Mathf.Max(compassFlash, 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f)); // stays faintly red
            if (drift >= 3f) PlayerThought.Think("Wait... the compass says we're turning north. The wheel says west.", "compass");
        }
        int minutes = ParseClock(off.ClockText);
        if (lastClock >= 0 && minutes >= 0 && minutes < lastClock)
        {
            clockFlash = 1f;
            PlayerThought.Think("Did the clock just... go backwards?", "clock");
        }
        if (minutes >= 0) lastClock = minutes;
        string e = off.EtaText;
        if (lastEta != null && e != lastEta && e.Contains("days") && e.Length > lastEta.Length)
        {
            etaFlash = 1f;
            PlayerThought.Think("Two days... and now it's longer? We're going full speed!", "eta");
        }
        lastEta = e;
        Flag(compass, ref compassFlash);
        Flag(clock, ref clockFlash);
    }

    void Flag(Text t, ref float flash)
    {
        if (t == null) return;
        if (!baseColors.TryGetValue(t, out var normal)) baseColors[t] = normal = t.color;
        if (t == eta && revealTime >= 0f && Time.time >= revealTime) return; // Grey Cove draws its own colour
        float f = Mathf.Clamp01(flash);
        bool flicker = f > 0.5f && Mathf.PerlinNoise(Time.unscaledTime * 20f, t.GetHashCode() % 100) > 0.6f;
        t.color = Color.Lerp(normal, revealColor, flicker ? 0.3f : f);
        t.transform.localScale = Vector3.one * (1f + 0.12f * Mathf.Max(0f, f - 0.4f));
        flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 0.35f);
    }

    readonly System.Collections.Generic.Dictionary<Text, Color> baseColors = new System.Collections.Generic.Dictionary<Text, Color>();

    static int ParseClock(string s)
    {
        var parts = s.Split(':');
        return parts.Length == 2 && int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int m) ? h * 60 + m : -1;
    }

    /// <summary>A second or two of glitching between the two ports, then Grey Cove counting down to the iceberg.</summary>
    bool bigRevealShown;

    void DrawGreyCove()
    {
        float since = Time.time - revealTime;
        if (!bigRevealShown) { bigRevealShown = true; GreyCoveReveal.Play(eta.rectTransform, minutesToCove); } // the big moment first
        float icebergAt = truthStart >= 0f ? truthStart + truthLength : revealTime + 60f;
        float left = Mathf.Clamp01((icebergAt - Time.time) / Mathf.Max(1f, icebergAt - revealTime));
        int minutes = Mathf.CeilToInt(minutesToCove * left);
        string cove = minutes > 0 ? $"{minutes} min" : "dead ahead";

        if (since < glitchSeconds)
        {
            // Flickers between what you were told and where you're going, like a bad signal.
            bool truth = Mathf.PerlinNoise(Time.unscaledTime * 18f, 0.5f) > 0.5f - since / glitchSeconds * 0.4f;
            if (etaLabel != null) { etaLabel.text = truth ? "G R E Y   C O V E" : "P O R T"; etaLabel.color = truth ? revealColor : labelColor; }
            eta.text = truth ? cove : "ETA 2 days";
            eta.color = truth ? revealColor : valueColor;
            return;
        }
        if (etaLabel != null) { etaLabel.text = "G R E Y   C O V E"; etaLabel.color = revealColor; }
        eta.text = cove;
        eta.color = Color.Lerp(valueColor, revealColor, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
    }
}
