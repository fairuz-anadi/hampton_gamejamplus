using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A crewmate leans in when the Captain gives a player a suspicious order (the "off" ones and the last one).
/// Usually an honest crewmate near the player doubts it out loud ("Lock the lifeboats? Who locks the lifeboats?");
/// sometimes the Stoker gets there first and pushes the player to obey, which is the clue: the one who keeps
/// urging you on is the Captain's man. A speech bubble over the crewmate's head, built in code. Owner: Anadi.
/// Spawned by GameSystems in Ship.
/// </summary>
public class CrewWhispers : MonoBehaviour
{
    [Tooltip("Seconds after the order arrives before the crewmate speaks (the Captain's line comes first).")]
    [Min(0f)] public float delay = 2.5f;
    [Min(1f)] public float showSeconds = 5.5f;
    [Tooltip("Chance the Stoker, not an honest crewmate, speaks up.")]
    [Range(0f, 1f)] public float stokerChance = 0.35f;
    [Tooltip("Metres above the crewmate's feet the bubble floats.")]
    public float heightAbove = 2.3f;

    const float RefHeight = 1080f;

    RectTransform canvasRect, bubble;
    CanvasGroup group;
    Text nameText, lineText;
    Image nameDot;
    Transform speaker;
    float speakAt = -1f, shownAt = -1f;
    string pendingLine;
    ICrewActor pendingSpeaker;

    void OnEnable() => GameEvents.OrderIssued += OnOrder;
    void OnDisable() => GameEvents.OrderIssued -= OnOrder;
    void Start() => Build();

    void OnDestroy()
    {
        if (canvasRect != null) Destroy(canvasRect.gameObject);
    }

    void OnOrder(ActiveOrder o)
    {
        if (o.Assignee == null || o.Assignee.IsBot || o.Data.kind == OrderKind.Normal) return;
        var player = o.Assignee as Component;
        bool stoker = Random.value < stokerChance;
        var who = PickCrewmate(player != null ? player.transform.position : Vector3.zero, stoker);
        if (who == null && stoker) { stoker = false; who = PickCrewmate(player != null ? player.transform.position : Vector3.zero, false); }
        if (who == null) return;
        pendingSpeaker = who;
        pendingLine = stoker ? Pick(StokerLines) : DoubtFor(o.Data);
        speakAt = Time.time + delay;
    }

    /// <summary>The nearest honest bot (or the Stoker when asked for), so the voice comes from someone close by.</summary>
    static ICrewActor PickCrewmate(Vector3 near, bool stoker)
    {
        ICrewActor best = null;
        float bestDist = float.MaxValue;
        foreach (var c in CrewRegistry.All)
        {
            if (!c.IsBot || c.IsStoker != stoker || c.IsLockedOut) continue;
            float dd = (c.Position - near).sqrMagnitude;
            if (dd < bestDist) { bestDist = dd; best = c; }
        }
        return best;
    }

    static string DoubtFor(OrderData d)
    {
        string n = d.name;
        if (n.Contains("FullSpeed")) return "Full speed through an ice field? Are you sure about this?";
        if (n.Contains("FileIceWarning")) return "File it unread? Those warnings are about us.";
        if (n.Contains("LockLifeboats")) return "Lock the lifeboats? Who locks the lifeboats?";
        if (n.Contains("TwoPointsNorth")) return "North? The Promised Port isn't north.";
        if (d.kind == OrderKind.Final) return "Don't. Not now. Think about what he's asking.";
        return Pick(GenericDoubts);
    }

    static readonly string[] GenericDoubts =
    {
        "Are you sure you're going to listen to the Captain?",
        "Something's not right about that order.",
        "You don't have to do everything he says.",
    };

    static readonly string[] StokerLines =
    {
        "Orders are orders, newcomer. Just do it.",
        "The Captain knows what he's doing. Don't make him wait.",
        "Don't listen to the others. Keep the Captain happy.",
    };

    static string Pick(string[] lines) => lines[Random.Range(0, lines.Length)];

    void Update()
    {
        if (group == null) return;
        if (speakAt >= 0f && Time.time >= speakAt && pendingSpeaker != null)
        {
            speakAt = -1f;
            speaker = (pendingSpeaker as Component)?.transform;
            int seat = pendingSpeaker.Seat;
            nameText.text = CrewRegistry.CrewName(pendingSpeaker).ToUpperInvariant();
            if (seat >= 0 && seat < CrewRoster.SeatColors.Length) nameDot.color = CrewRoster.SeatColors[seat];
            lineText.text = pendingLine;
            shownAt = Time.time;
        }

        bool showing = shownAt >= 0f && Time.time - shownAt < showSeconds && speaker != null;
        group.alpha = Mathf.MoveTowards(group.alpha, showing ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        if (!showing || Camera.main == null) return;

        // Follow the crewmate on screen; keep the bubble on screen when they're off to the side.
        Vector3 sp = Camera.main.WorldToScreenPoint(speaker.position + Vector3.up * heightAbove);
        if (sp.z < 0f) sp = new Vector3(Screen.width - sp.x, 0f, 0f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out var local);
        var half = canvasRect.rect.size * 0.5f;
        local.x = Mathf.Clamp(local.x, -half.x + 260f, half.x - 260f);
        local.y = Mathf.Clamp(local.y, -half.y + 260f, half.y - 360f);
        bubble.anchoredPosition = Vector2.Lerp(bubble.anchoredPosition, local, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
    }

    void Build()
    {
        var theme = UiTheme.Load();
        Font F(Font f) => f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvas = new GameObject("[CrewWhispers]", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, RefHeight);
        scaler.matchWidthOrHeight = 0.5f;
        group = canvas.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        canvasRect = (RectTransform)canvas.transform;

        bubble = new GameObject("Bubble", typeof(RectTransform)).GetComponent<RectTransform>();
        bubble.SetParent(canvasRect, false);
        bubble.sizeDelta = new Vector2(460f, 110f);
        bubble.pivot = new Vector2(0.5f, 0f);

        Image Box(string name, Color c, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(bubble, false);
            img.color = c;
            img.raycastTarget = false;
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
            return img;
        }
        var paper = new Color(0.97f, 0.93f, 0.84f, 0.96f);
        Box("Tail", paper, new Vector2(0f, 14f), new Vector2(26f, 26f), new Vector2(0.5f, 0.5f)).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Box("Paper", paper, new Vector2(0f, 20f), new Vector2(460f, 96f), new Vector2(0.5f, 0f));
        nameDot = Box("Seat", Color.white, new Vector2(-200f, 94f), new Vector2(14f, 14f), new Vector2(0.5f, 0.5f));

        Text Label(Font font, int size, Color c, Vector2 pos, Vector2 box, TextAnchor a)
        {
            var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(bubble, false);
            t.font = font; t.fontSize = size; t.color = c; t.alignment = a;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = pos; r.sizeDelta = box;
            return t;
        }
        var ink = new Color(0.23f, 0.16f, 0.11f);
        nameText = Label(F(theme ? theme.body : null), 18, new Color(0.55f, 0.2f, 0.15f), new Vector2(-80f, 94f), new Vector2(220f, 24f), TextAnchor.MiddleLeft);
        lineText = Label(F(theme ? theme.italic : null), 26, ink, new Vector2(0f, 56f), new Vector2(420f, 60f), TextAnchor.MiddleCenter);
        Label(F(theme ? theme.body : null), 16, new Color(ink.r, ink.g, ink.b, 0.6f), new Vector2(170f, 94f), new Vector2(120f, 22f), TextAnchor.MiddleRight).text = "whispers";
    }
}
