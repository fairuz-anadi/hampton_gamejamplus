using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One Captain's order on screen: who it's for, his line, where, a timer ring, and the Report/Refuse keys.
/// Slides in when issued, stamps the result, then fades out. Owner: Samprity.
/// A player's own order slides in from the right edge with a brass flash and sits on top (OrderCards); a bot's
/// order is dimmed and a little smaller, so the eye goes to what's yours (Anadi).
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

    float appear;
    float holdResult;
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
