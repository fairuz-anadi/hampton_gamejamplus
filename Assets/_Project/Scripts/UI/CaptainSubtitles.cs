using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Captain's styled subtitles, bottom centre: a speaker horn that pulses on each blip of his voice, "THE CAPTAIN",
/// and his line typing out. Owner: Samprity. Riham's CaptainDirector does the typing (and the corrections);
/// this only draws what it says, and turns its plain fallback subtitles off.
///   Part 1  warm brass, steady
///   Part 2  colder, the trim loses its warmth
///   Part 3  red trim, the text trembles slightly
/// Also shows the Stoker's speaker static ("kssshhht") and plays a short crackle for it.
/// </summary>
public class CaptainSubtitles : MonoBehaviour
{
    [System.Serializable]
    public struct Style
    {
        public Color trim, label, text;
        [Tooltip("How much the text trembles, in canvas units.")]
        public float tremble;
    }

    public CanvasGroup group;
    public RectTransform panel;
    public Text line, sizer, label, staticText;
    public Image trim, horn;
    [Min(200f)] public float maxTextWidth = 980f;
    [Min(100f)] public float minTextWidth = 380f;
    public Vector2 padding = new Vector2(150f, 74f);
    [Tooltip("Radio strip: the panel spans the screen and only its height follows the line (words wrap in a fixed column).")]
    public bool strip;
    [Min(40f)] public float stripMinHeight = 96f;

    public Style calm = new Style { trim = new Color(0.86f, 0.68f, 0.34f, 0.8f), label = new Color(0.86f, 0.68f, 0.34f), text = new Color(0.97f, 0.93f, 0.84f), tremble = 0f };
    public Style off = new Style { trim = new Color(0.6f, 0.68f, 0.78f, 0.8f), label = new Color(0.68f, 0.76f, 0.88f), text = new Color(0.9f, 0.93f, 0.98f), tremble = 0f };
    public Style truth = new Style { trim = new Color(0.85f, 0.18f, 0.14f, 0.9f), label = new Color(0.95f, 0.35f, 0.3f), text = new Color(1f, 0.88f, 0.84f), tremble = 1.4f };

    [Header("The drunk act (final plan)")]
    [Tooltip("Part 2: the Captain 'drinks' and his line sways on the speaker strip, more as the part goes on.")]
    public float dizzySway = 1f; // degrees; the line pivots at its left end, so the far end moves ~25 px
    [Tooltip("How far the swaying line drifts, in canvas units.")]
    public float dizzyDrift = 9f;
    [Tooltip("Seconds into Part 2 before the act is at its worst.")]
    [Min(1f)] public float dizzyRamp = 150f;
    [Tooltip("Part 3: 'the bottle was never full'. The line goes dead still and sharp.")]
    public bool soberInTruth = true;

    [Header("Speaker static")]
    [Min(0f)] public float staticSeconds = 1.6f;
    [Range(0f, 1f)] public float staticVolume = 0.25f;

    float shown, target;
    float pulse;
    float staticLeft;
    Vector2 lineHome;
    Style style;
    AudioSource crackle;
    AudioClip crackleClip;

    void OnEnable()
    {
        CaptainDirector.DrawFallbackSubtitles = false;
        CaptainDirector.LineStarted += OnLineStarted;
        CaptainDirector.TextChanged += OnTextChanged;
        CaptainDirector.Blip += OnBlip;
        StokerBrain.StaticBurst += OnStatic;
        GameEvents.PartChanged += OnPartChanged;
        lineHome = line.rectTransform.anchoredPosition;
        ApplyStyle(GameManager.Instance != null && GameManager.Instance.Part > 0 ? GameManager.Instance.Part : 1);
        if (CaptainDirector.Instance != null && CaptainDirector.Instance.VisibleText.Length > 0)
            OnTextChanged(CaptainDirector.Instance.VisibleText);
    }

    void OnDisable()
    {
        CaptainDirector.DrawFallbackSubtitles = true;
        CaptainDirector.LineStarted -= OnLineStarted;
        CaptainDirector.TextChanged -= OnTextChanged;
        CaptainDirector.Blip -= OnBlip;
        StokerBrain.StaticBurst -= OnStatic;
        GameEvents.PartChanged -= OnPartChanged;
    }

    /// <summary>Sizes the panel to the finished line once, so it doesn't grow and jump while typing.</summary>
    void OnLineStarted(string finalText)
    {
        sizer.text = finalText;
        if (strip)
        {
            // Words wrap in the strip's text column; the strip just gets taller for a long line. The layout must be
            // current first, or the column reads as 0 wide and every word looks like its own line.
            Canvas.ForceUpdateCanvases();
            float column = line.rectTransform.rect.width;
            if (column < 200f) column = maxTextWidth;
            float h = sizer.cachedTextGeneratorForLayout.GetPreferredHeight(finalText, sizer.GetGenerationSettings(new Vector2(column, 0f))) / sizer.pixelsPerUnit;
            line.rectTransform.sizeDelta = new Vector2(line.rectTransform.sizeDelta.x, Mathf.Max(h, 34f));
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, Mathf.Max(stripMinHeight, h + padding.y));
            line.text = "";
            target = 1f;
            return;
        }
        float width = Mathf.Clamp(sizer.preferredWidth, minTextWidth, maxTextWidth);
        sizer.rectTransform.sizeDelta = new Vector2(width, sizer.rectTransform.sizeDelta.y);
        float height = sizer.cachedTextGeneratorForLayout.GetPreferredHeight(finalText, sizer.GetGenerationSettings(new Vector2(width, 0f))) / sizer.pixelsPerUnit;
        line.rectTransform.sizeDelta = new Vector2(width, Mathf.Max(height, 34f));
        panel.sizeDelta = new Vector2(width + padding.x, Mathf.Max(height, 34f) + padding.y);
        line.text = "";
        target = 1f;
    }

    void OnTextChanged(string visible)
    {
        if (visible.Length == 0) { target = 0f; return; }
        line.text = visible;
        target = 1f;
    }

    void OnBlip() => pulse = 1f;

    void OnStatic(StokerBrain _)
    {
        staticLeft = staticSeconds;
        if (staticVolume <= 0f || (AudioManager.Instance != null && AudioManager.Instance.HandlesStatic)) return;
        if (crackle == null)
        {
            crackle = gameObject.AddComponent<AudioSource>();
            crackle.playOnAwake = false;
            crackle.spatialBlend = 0f;
            crackleClip = MakeCrackle();
        }
        crackle.PlayOneShot(crackleClip, staticVolume);
    }

    void OnPartChanged(int part) => ApplyStyle(part);

    int part = 1;
    float partSince;

    void ApplyStyle(int part)
    {
        if (part != this.part) partSince = Time.time;
        this.part = part;
        style = part switch { 2 => off, >= 3 => truth, _ => calm };
        if (part >= 3 && soberInTruth) style.tremble = 0f;
        trim.color = style.trim;
        label.color = style.label;
        line.color = style.text;
        horn.color = style.label;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        shown = Mathf.MoveTowards(shown, target, dt * (target > shown ? 6f : 2.5f));
        group.alpha = Mathf.SmoothStep(0f, 1f, shown);

        pulse = Mathf.MoveTowards(pulse, 0f, dt * 8f);
        horn.rectTransform.localScale = Vector3.one * (1f + 0.12f * pulse);

        line.rectTransform.anchoredPosition = style.tremble > 0f
            ? lineHome + new Vector2(Mathf.PerlinNoise(Time.unscaledTime * 23f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.unscaledTime * 19f) - 0.5f) * 2f * style.tremble
            : lineHome;

        // The drunk act: in Part 2 the line rolls and drifts like a man who's had a few, worse as the night goes on;
        // in Part 3 it snaps dead still, because he was never drunk at all.
        float act = part == 2 ? Mathf.Lerp(0.3f, 1f, Mathf.Clamp01((Time.time - partSince) / dizzyRamp)) : 0f;
        if (act > 0f)
        {
            float t = Time.unscaledTime;
            line.rectTransform.anchoredPosition += new Vector2(Mathf.Sin(t * 0.9f), Mathf.Cos(t * 1.7f) * 0.4f) * dizzyDrift * act;
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (Mathf.Sin(t * 1.3f) + (Mathf.PerlinNoise(t * 0.7f, 5f) - 0.5f)) * dizzySway * act);
            line.rectTransform.localScale = Vector3.one * (1f + 0.025f * Mathf.Sin(t * 2.1f) * act);
            horn.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.1f) * 6f * act);
        }
        else
        {
            line.rectTransform.localRotation = Quaternion.identity;
            line.rectTransform.localScale = Vector3.one;
            horn.rectTransform.localRotation = Quaternion.identity;
        }

        staticLeft -= dt;
        bool showStatic = staticLeft > 0f;
        staticText.enabled = showStatic;
        if (showStatic)
        {
            // Flickers, like a bad line.
            var c = staticText.color;
            c.a = Mathf.PerlinNoise(Time.unscaledTime * 30f, 3f) > 0.35f ? 1f : 0.25f;
            staticText.color = c;
            if (shown < 0.5f) group.alpha = Mathf.Max(group.alpha, 0.85f);
        }
    }

    /// <summary>Editor preview: show a finished line in a given part's style.</summary>
    public void Preview(string text, int part, bool withStatic)
    {
        lineHome = line.rectTransform.anchoredPosition;
        ApplyStyle(part);
        OnLineStarted(text);
        line.text = text;
        shown = target = 1f;
        group.alpha = 1f;
        staticText.enabled = withStatic;
    }

    /// <summary>About a second and a half of radio crackle: noise with random dropouts and pops.</summary>
    static AudioClip MakeCrackle()
    {
        const int rate = 22050;
        int n = rate * 3 / 2;
        var data = new float[n];
        var rng = new System.Random(7);
        float smooth = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float env = Mathf.Clamp01(t * 12f) * Mathf.Clamp01((1f - t) * 4f);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            smooth = Mathf.Lerp(smooth, noise, 0.35f);
            float gate = Mathf.PerlinNoise(t * 40f, 1.3f) > 0.4f ? 1f : 0.2f;
            float pop = rng.NextDouble() < 0.0015 ? 3f : 1f;
            data[i] = Mathf.Clamp(smooth * env * gate * pop * 0.6f, -1f, 1f);
        }
        var clip = AudioClip.Create("SpeakerStatic", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
