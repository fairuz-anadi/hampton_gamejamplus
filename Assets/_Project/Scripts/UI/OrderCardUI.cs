using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One Captain's order on screen: who it's for, his line, where, a timer ring, and the Report/Refuse keys.
/// Slides in when issued, stamps the result, then fades out. Owner: Samprity.
/// A player's own order slides in from the right edge with a brass flash and sits on top (OrderCards); a bot's
/// order is dimmed and a little smaller, so the eye goes to what's yours (Anadi).
/// Compact look (Anadi, 28 Oct): the card reshapes itself at Awake into a slim, see-through strip: the station's
/// icon inside the timer ring, the task in a few words, the seconds and two keys. A brass edge for normal orders,
/// red with a "?!" badge for the Captain's suspicious ones. His full line is still on the radio strip.
/// </summary>
public class OrderCardUI : MonoBehaviour
{
    public CanvasGroup group;
    public RectTransform body;
    public Text header, captainLine, where, timerText, stamp, botNote;
    public Image timerFill;
    public GameObject hints;
    public Text reportKey, refuseKey;
    public Color timerCalm = new Color(0.95f, 0.9f, 0.78f);
    public Color timerUrgent = new Color(0.95f, 0.3f, 0.25f);
    [Tooltip("Seconds left when the ring turns red and pulses.")]
    public float urgentBelow = 5f;

    [Tooltip("How far a new order slides in from, in canvas units to the right.")]
    public float slideFrom = 420f;
    [Tooltip("Opacity and scale of an order a bot is handling, so the player's own stand out.")]
    [Range(0.2f, 1f)] public float crewAlpha = 0.68f;
    [Range(0.6f, 1f)] public float crewScale = 0.9f;

    public ActiveOrder Order { get; private set; }
    public bool IsFree => Order == null && !leaving;
    /// <summary>This card is a player's order (not a bot's).</summary>
    public bool ForPlayer { get; private set; }

    [Tooltip("The slim card: icon, task, timer and keys only.")]
    public bool compact = false; // Anadi kept the previous card (28 Oct); true gives the slim one

    float appear;
    float holdResult;
    Text task;
    RawImage icon;
    Image accent;
    GameObject offBadge;
    static Texture2D[] stationIcons;
    bool leaving;
    float flash;
    Image flashImage;

    void Awake()
    {
        if (group != null) group.alpha = 0f;
        if (stamp != null) stamp.enabled = false;
        if (body != null) body.pivot = new Vector2(1f, 0.5f); // a dimmed crew card shrinks toward the screen edge
        // A brass wash over the card's pill, flashed when a player's order arrives.
        var pill = body != null ? body.Find("Pill") : null;
        if (pill != null && pill.TryGetComponent(out Image src))
        {
            flashImage = new GameObject("Flash", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            flashImage.transform.SetParent(pill.parent, false);
            flashImage.transform.SetSiblingIndex(pill.GetSiblingIndex() + 1);
            flashImage.sprite = src.sprite;
            flashImage.type = src.type;
            flashImage.raycastTarget = false;
            var r = flashImage.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            flashImage.color = new Color(0.86f, 0.68f, 0.34f, 0f);
        }
        if (compact) Compact();
    }

    static readonly Color Brass = new Color(0.86f, 0.68f, 0.34f), Red = new Color(0.84f, 0.3f, 0.24f), Cream = new Color(0.97f, 0.93f, 0.84f);

    /// <summary>Reshapes the built card into the slim strip.</summary>
    void Compact()
    {
        const float w = 470f, h = 92f;
        ((RectTransform)transform).sizeDelta = new Vector2(w, h);
        var stack = GetComponentInParent<OrderCards>();
        if (stack != null) stack.spacing = h + 12f;
        if (header != null) header.gameObject.SetActive(false);
        if (captainLine != null) captainLine.gameObject.SetActive(false);
        if (where != null) where.gameObject.SetActive(false);
        var pill = body.Find("Pill")?.GetComponent<Image>();
        if (pill != null) pill.color = new Color(0.04f, 0.055f, 0.09f, 0.72f);

        // The timer ring with the station's icon inside.
        var marker = body.Find("Marker") as RectTransform;
        if (marker != null)
        {
            marker.anchoredPosition = new Vector2(48f, -h * 0.5f);
            marker.sizeDelta = new Vector2(60f, 60f);
            var gem = marker.Find("Diamond");
            if (gem != null) gem.gameObject.SetActive(false);
            icon = new GameObject("Icon", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            icon.transform.SetParent(marker, false);
            icon.raycastTarget = false;
            icon.rectTransform.sizeDelta = new Vector2(30f, 30f);
        }

        accent = new GameObject("Accent", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        accent.transform.SetParent(body, false);
        accent.raycastTarget = false;
        var ar = accent.rectTransform;
        ar.anchorMin = new Vector2(0f, 0f); ar.anchorMax = new Vector2(0f, 1f);
        ar.pivot = new Vector2(0f, 0.5f);
        ar.offsetMin = new Vector2(2f, 12f); ar.offsetMax = new Vector2(7f, -12f);

        // The task, in a few words.
        task = new GameObject("Task", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        task.transform.SetParent(body, false);
        task.font = timerText != null ? timerText.font : header.font;
        task.fontSize = 26;
        task.color = Cream;
        task.alignment = TextAnchor.MiddleLeft;
        task.raycastTarget = false;
        task.horizontalOverflow = HorizontalWrapMode.Wrap;
        task.verticalOverflow = VerticalWrapMode.Truncate;
        task.resizeTextForBestFit = true;
        task.resizeTextMinSize = 18;
        task.resizeTextMaxSize = 26;
        var tr = task.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f);
        tr.pivot = new Vector2(0f, 1f);
        tr.anchoredPosition = new Vector2(92f, -8f);
        tr.sizeDelta = new Vector2(w - 92f - 44f, 40f);

        // Seconds bottom-left, keys bottom-right.
        if (timerText != null)
        {
            var sr = timerText.rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(0f, 0f);
            sr.pivot = new Vector2(0f, 0f);
            sr.anchoredPosition = new Vector2(92f, 10f);
            sr.sizeDelta = new Vector2(60f, 26f);
            timerText.alignment = TextAnchor.LowerLeft;
            timerText.fontSize = 19;
        }
        if (hints != null)
        {
            var hr = (RectTransform)hints.transform;
            hr.anchoredPosition = new Vector2(-14f, 6f);
            var report = hints.transform.Find("Report/Label")?.GetComponent<Text>();
            if (report != null) report.text = "Done";
        }

        // "?!" on the Captain's suspicious orders.
        offBadge = new GameObject("OffBadge", typeof(RectTransform), typeof(Image)).gameObject;
        offBadge.transform.SetParent(body, false);
        var bi = offBadge.GetComponent<Image>();
        bi.color = Red;
        bi.raycastTarget = false;
        var br = bi.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(1f, 1f);
        br.pivot = new Vector2(1f, 1f);
        br.anchoredPosition = new Vector2(-12f, -10f);
        br.sizeDelta = new Vector2(30f, 24f);
        var bt = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        bt.transform.SetParent(offBadge.transform, false);
        bt.font = task.font; bt.fontSize = 18; bt.color = Cream; bt.text = "?!";
        bt.alignment = TextAnchor.MiddleCenter;
        bt.raycastTarget = false;
        bt.rectTransform.anchorMin = Vector2.zero; bt.rectTransform.anchorMax = Vector2.one;
        bt.rectTransform.offsetMin = bt.rectTransform.offsetMax = Vector2.zero;
        offBadge.SetActive(false);
    }

    static Texture2D IconFor(StationType t)
    {
        if (stationIcons == null)
        {
            stationIcons = new Texture2D[4];
            var white = Color.white;
            // Boiler: a flame.
            var p = new EndingPainter(64, 64, new Color(0, 0, 0, 0));
            p.Polygon(new[] { new Vector2(32, 4), new Vector2(40, 20), new Vector2(48, 30), new Vector2(46, 22), new Vector2(54, 34), new Vector2(56, 46), new Vector2(50, 56), new Vector2(40, 61), new Vector2(32, 62), new Vector2(24, 61), new Vector2(14, 56), new Vector2(8, 46), new Vector2(12, 34), new Vector2(20, 24), new Vector2(24, 32), new Vector2(28, 18) }, white);
            stationIcons[0] = p.ToTexture();
            // Wheel: a ship's wheel.
            p = new EndingPainter(64, 64, new Color(0, 0, 0, 0));
            p.Ring(32, 32, 18, 5, white);
            for (int a = 0; a < 360; a += 45)
            {
                float r = a * Mathf.Deg2Rad;
                p.Line(new Vector2(32, 32), new Vector2(32 + Mathf.Cos(r) * 29, 32 + Mathf.Sin(r) * 29), 5, white);
            }
            p.Circle(32, 32, 6, white);
            stationIcons[1] = p.ToTexture();
            // Doors: a lever in its slot.
            p = new EndingPainter(64, 64, new Color(0, 0, 0, 0));
            p.RoundRectOutline(18, 30, 46, 60, 6, 5, white);
            p.Line(new Vector2(32, 46), new Vector2(46, 8), 6, white);
            p.Circle(46, 8, 7, white);
            stationIcons[2] = p.ToTexture();
            // Wireless: radio waves over a mast.
            p = new EndingPainter(64, 64, new Color(0, 0, 0, 0));
            p.Line(new Vector2(32, 30), new Vector2(32, 60), 5, white);
            p.Circle(32, 28, 6, white);
            p.Arc(32, 28, 14, 5, 200, 340, white);
            p.Arc(32, 28, 24, 5, 205, 335, white);
            stationIcons[3] = p.ToTexture();
        }
        int i = Mathf.Clamp((int)t, 0, stationIcons.Length - 1);
        return stationIcons[i];
    }

    public void Bind(ActiveOrder o)
    {
        Order = o;
        var actor = o.Assignee;
        Show(o.Number, o.Data, actor != null ? actor.Seat : 0, actor == null || !actor.IsBot, actor as Component);
        SetTimer(o.TimeLeft, o.Data.timer);
    }

    /// <summary>Fills the card. Also used by the editor preview, which has no live orders.</summary>
    public void Show(int number, OrderData d, int seat, bool human, Component actor)
    {
        leaving = false;
        holdResult = 0f;
        ForPlayer = human;
        appear = 0f;
        flash = human ? 1f : 0f;
        // Doubled spaces: the bold face's space is too narrow to read at this size.
        string seatName = HudText.Seat(seat).ToUpperInvariant().Replace(" ", "  ");
        header.text = seat > 0 ? $"ORDER  {number}     ·     {seatName}" : $"ORDER  {number}";
        captainLine.text = $"“{d.captainLine}”";
        if (task != null)
        {
            string line = string.IsNullOrEmpty(d.resultsLine) ? HudText.Where(d) : d.resultsLine;
            task.text = line.Length > 0 ? char.ToUpperInvariant(line[0]) + line.Substring(1) : line;
            bool off = d.kind != OrderKind.Normal;
            accent.color = off ? Red : Brass;
            offBadge.SetActive(off);
            if (icon != null) { icon.texture = IconFor(d.station); icon.color = Cream; }
        }
        where.text = HudText.Where(d);
        hints.SetActive(human);
        botNote.gameObject.SetActive(!human);
        if (human)
        {
            reportKey.text = HudText.KeyFor(HudText.Key.Report, actor);
            refuseKey.text = HudText.KeyFor(HudText.Key.Refuse, actor);
        }
        else botNote.text = $"{HudText.Seat(seat)} is on it";
        stamp.enabled = false;
    }

    public void SetTimer(float timeLeft, float total)
    {
        float t = total > 0f ? Mathf.Clamp01(timeLeft / total) : 0f;
        timerFill.fillAmount = t;
        timerText.text = Mathf.CeilToInt(Mathf.Max(0f, timeLeft)).ToString();
        bool urgent = timeLeft <= urgentBelow;
        float pulse = urgent ? 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 10f) : 1f;
        var c = urgent ? timerUrgent : timerCalm;
        timerFill.color = new Color(c.r * pulse, c.g * pulse, c.b * pulse, 1f);
        timerText.color = urgent ? timerUrgent : timerCalm;
    }

    /// <summary>Stamps how the order ended, holds it a moment, then lets the card fade.</summary>
    public void Resolve(Choice c)
    {
        stamp.text = HudText.ResultStamp(c);
        stamp.color = c == Choice.Refuse || c == Choice.Expired ? timerUrgent : new Color(0.75f, 0.95f, 0.75f);
        stamp.enabled = true;
        Order = null;
        leaving = true;
        holdResult = 1.3f;
    }

    /// <summary>Editor preview: show the card fully in, no animation.</summary>
    public void SnapIn()
    {
        appear = 1f;
        Apply();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (Order != null)
        {
            appear = Mathf.MoveTowards(appear, 1f, dt * 2.6f);
            SetTimer(Order.TimeLeft, Order.Data.timer);
        }
        else if (leaving)
        {
            holdResult -= dt;
            if (holdResult <= 0f)
            {
                appear = Mathf.MoveTowards(appear, 0f, dt * 3f);
                if (appear <= 0f) { leaving = false; stamp.enabled = false; }
            }
        }
        Apply();
    }

    void Apply()
    {
        float dim = ForPlayer || leaving ? 1f : crewAlpha;
        if (group != null) group.alpha = Mathf.Clamp01(appear * 1.6f) * dim;
        if (body != null)
        {
            // Slides in from the right with a little overshoot; leaves by fading where it is.
            float x = leaving ? 0f : (1f - EaseOutBack(appear)) * slideFrom;
            body.anchoredPosition = new Vector2(x, 0f);
            float s = ForPlayer || leaving ? 1f : crewScale;
            body.localScale = Vector3.one * s;
        }
        if (flashImage != null)
        {
            flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 1.6f);
            var c = flashImage.color;
            c.a = 0.4f * flash * flash;
            flashImage.color = c;
        }
    }

    static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.4f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
