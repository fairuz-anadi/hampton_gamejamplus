using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One Captain's order on screen: who it's for, his line, where, a timer ring, and the Report/Refuse keys.
/// Slides in when issued, stamps the result, then fades out. Owner: Samprity.
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

    public ActiveOrder Order { get; private set; }
    public bool IsFree => Order == null && !leaving;

    float appear;
    float holdResult;
    bool leaving;

    void Awake()
    {
        if (group != null) group.alpha = 0f;
        if (stamp != null) stamp.enabled = false;
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
            appear = Mathf.MoveTowards(appear, 1f, dt * 4f);
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
        if (group != null) group.alpha = appear;
        if (body != null) body.anchoredPosition = new Vector2(0f, (1f - Mathf.SmoothStep(0f, 1f, appear)) * 36f);
    }
}
