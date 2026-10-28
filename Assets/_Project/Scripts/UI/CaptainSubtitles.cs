using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Captain's styled subtitles, bottom centre: a speaker horn that pulses on each blip of his voice, "THE CAPTAIN",
/// and his line typing out. Owner: Samprity. Riham's CaptainDirector does the typing (and the corrections);
/// this only draws what it says, and turns its plain fallback subtitles off.
///   Part 1  warm brass, steady
///   Part 2  colder, the trim loses its warmth
///   Part 3  red trim, the text trembles slightly
/// Also shows the Stoker's speaker static ("kssshhht") and plays a short crackle for it, and a keycap hint at the
/// strip's right end for CaptainDirector's Space key (Riham): dim "skip" while he types, then a bright "next" that pops
/// in, nudges its arrow and pulses, harder the longer it waits; hidden while he holds an open order's line.
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
        EnsureSkipHint();
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
        if (hintRoot != null)
        {
            keyFrame.color = style.label;
            keyText.color = style.label;
            verbText.color = style.text;
            arrow.color = style.label;
        }
    }

    // ---------- The Space hint (Riham) ----------

    const float HintReserve = 330f; // canvas units kept clear at the strip's right end so the words never run under it
    RectTransform hintRoot, keyRect;
    CanvasGroup hintGroup;
    Image keyFrame, keyFill, arrow;
    Text keyText, verbText;
    string hintState = "", hintKey = "";
    float hintSince, hintAlpha;

    /// <summary>A brass-edged keycap ("SPACE"), the verb ("next" / "skip") and an arrow, at the strip's right end.</summary>
    void EnsureSkipHint()
    {
        if (hintRoot != null || panel == null || label == null) return;
        if (strip)
            foreach (var t in new[] { line, sizer })
            {
                var r = t.rectTransform;
                r.offsetMax = new Vector2(r.offsetMax.x - HintReserve, r.offsetMax.y);
            }

        hintRoot = new GameObject("SpaceHint", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        hintRoot.SetParent(panel, false);
        hintRoot.anchorMin = hintRoot.anchorMax = new Vector2(1f, 0f);
        hintRoot.pivot = new Vector2(1f, 0.5f);
        hintRoot.anchoredPosition = new Vector2(-40f, 48f);
        hintRoot.sizeDelta = new Vector2(HintReserve - 40f, 52f);
        hintGroup = hintRoot.GetComponent<CanvasGroup>();
        hintGroup.alpha = 0f;
        hintGroup.blocksRaycasts = false;

        keyFrame = HintImage("Key", hintRoot, null);
        keyRect = keyFrame.rectTransform;
        keyFill = HintImage("Fill", keyRect, null);
        keyFill.rectTransform.anchorMin = Vector2.zero;
        keyFill.rectTransform.anchorMax = Vector2.one;
        keyFill.rectTransform.offsetMin = new Vector2(3f, 3f);
        keyFill.rectTransform.offsetMax = new Vector2(-3f, -3f);
        keyText = HintText("KeyText", keyRect, 22, TextAnchor.MiddleCenter);
        keyText.rectTransform.anchorMin = Vector2.zero;
        keyText.rectTransform.anchorMax = Vector2.one;
        keyText.rectTransform.offsetMin = keyText.rectTransform.offsetMax = Vector2.zero;

        verbText = HintText("Verb", hintRoot, 30, TextAnchor.MiddleRight);
        verbText.rectTransform.anchorMin = verbText.rectTransform.anchorMax = verbText.rectTransform.pivot = new Vector2(1f, 0.5f);
        verbText.rectTransform.anchoredPosition = new Vector2(-38f, 0f);
        verbText.rectTransform.sizeDelta = new Vector2(110f, 44f);

        arrow = HintImage("Arrow", hintRoot, ArrowSprite());
        arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
        arrow.rectTransform.sizeDelta = new Vector2(22f, 26f);
        ApplyStyle(part);
    }

    Image HintImage(string name, RectTransform parent, Sprite sprite)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    Text HintText(string name, RectTransform parent, int size, TextAnchor align)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = label.font;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Sprite arrowSprite;

    /// <summary>A solid right-pointing triangle, drawn once (fonts can't be trusted to have one).</summary>
    static Sprite ArrowSprite()
    {
        if (arrowSprite != null) return arrowSprite;
        const int w = 22, h = 26;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float half = (h * 0.5f) * (1f - (x + 0.5f) / w);
                float a = Mathf.Clamp01(half - Mathf.Abs(y + 0.5f - h * 0.5f) + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        arrowSprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f));
        return arrowSprite;
    }

    void UpdateHint(float dt)
    {
        if (hintRoot == null) return;
        var captain = CaptainDirector.Instance;
        string state = captain == null || target <= 0f ? "" : captain.IsTyping ? "skip" : captain.HoldingForOrder ? "" : "next";
        float now = Time.unscaledTime;
        if (state != hintState)
        {
            hintState = state;
            hintSince = now;
            if (state.Length > 0) verbText.text = state;
        }

        string key = UnityEngine.InputSystem.Gamepad.current != null ? "SPACE / LB" : "SPACE";
        if (key != hintKey)
        {
            hintKey = key;
            keyText.text = key;
        }
        // Lay out right to left: arrow, verb, keycap.
        float verbW = verbText.preferredWidth;
        verbText.rectTransform.sizeDelta = new Vector2(verbW + 4f, 44f);
        keyRect.anchorMin = keyRect.anchorMax = keyRect.pivot = new Vector2(1f, 0.5f);
        keyRect.sizeDelta = new Vector2(keyText.preferredWidth + 30f, 44f);
        keyRect.anchoredPosition = new Vector2(-38f - verbW - 16f, 0f);

        float wantAlpha = state == "next" ? 1f : state == "skip" ? 0.5f : 0f;
        hintAlpha = Mathf.MoveTowards(hintAlpha, wantAlpha, dt * (wantAlpha > hintAlpha ? 8f : 4f));
        hintGroup.alpha = hintAlpha;

        float since = now - hintSince;
        if (state == "next")
        {
            // Pops in, then the arrow nudges and the key glows; after a few seconds unread, it breathes bigger too.
            float pop = Mathf.Clamp01(1f - since / 0.35f);
            bool nagging = since > 3f;
            float beat = 0.5f + 0.5f * Mathf.Sin(now * (nagging ? 6f : 4.5f));
            hintRoot.localScale = Vector3.one * (1f + 0.35f * pop * pop + (nagging ? 0.07f * beat : 0.02f * beat));
            arrow.rectTransform.anchoredPosition = new Vector2(-6f + 8f * Mathf.Abs(Mathf.Sin(now * 4.5f)), 0f);
            var glow = style.label;
            keyFill.color = Color.Lerp(new Color(0.02f, 0.025f, 0.04f, 0.95f), new Color(glow.r, glow.g, glow.b, 0.55f), beat * (nagging ? 0.9f : 0.45f));
        }
        else
        {
            hintRoot.localScale = Vector3.one;
            arrow.rectTransform.anchoredPosition = new Vector2(-6f, 0f);
            keyFill.color = new Color(0.02f, 0.025f, 0.04f, 0.95f);
        }
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

        UpdateHint(dt);

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
