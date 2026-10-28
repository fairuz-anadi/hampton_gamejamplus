using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shows a player what their answer to an order did (playtest: "I can report complete and I see nothing change").
///   - The first order of the run: a crewmate whispers that you're allowed to refuse, and which key does it.
///   - The first time a player reports done (honest or fake, they look the same), refuses, or lets one run out: a big
///     card with the game paused, showing how the three meters moved and that they decide the ending. Space goes on.
///   - Every answer after that: a small note in the bottom right for a couple of seconds, no pause.
/// Only players' orders, never the bots'. Spawns itself in gameplay scenes, built in code. Owner: Samprity.
/// </summary>
public class ChoiceFeedback : MonoBehaviour
{
    public static bool IsShowing { get; private set; }

    [Min(1f)] public float toastSeconds = 2.6f;
    [Min(2f)] public float hintSeconds = 8f;
    [Tooltip("Seconds after the first order comes in before the crewmate whispers.")]
    [Min(0f)] public float hintDelay = 1.6f;

    static readonly Color Brass = new Color(0.86f, 0.68f, 0.34f);
    static readonly Color Cream = new Color(0.97f, 0.93f, 0.84f);
    static readonly Color DimCream = new Color(0.74f, 0.71f, 0.64f);
    static readonly Color Navy = new Color(0.106f, 0.137f, 0.22f, 0.97f);
    const string Up = "#8fd08a", Down = "#e8837a", Flat = "#9aa3b5";

    static ChoiceFeedback instance;

    Canvas canvas;
    RectTransform card, toast, hint;
    CanvasGroup cardGroup, toastGroup, hintGroup, dimGroup;
    Text cardKind, cardTitle, cardNote, toastHead, toastBody, hintName, hintLine;
    readonly Text[] cardRows = new Text[3], cardValues = new Text[3];

    // What's been explained this run: reports (obey and fake together), refusals, time running out.
    bool shownReport, shownRefuse, shownExpired, hinted;
    ActiveOrder hintOrder;
    float hintAt = -1f, hintUntil = -1f, toastUntil = -1f, cardOpenedAt;
    float prevTimeScale = 1f;
    ChoiceEntry pendingCard;
    ActiveOrder pendingOrder;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive || !GameSystems.IsGameplayScene(scene.name) || instance != null) return;
        new GameObject("[ChoiceFeedback]").AddComponent<ChoiceFeedback>();
    }

    void Awake()
    {
        instance = this;
        Build();
    }

    void OnEnable()
    {
        GameEvents.OrderIssued += OnIssued;
        GameEvents.OrderResolved += OnResolved;
    }

    void OnDisable()
    {
        GameEvents.OrderIssued -= OnIssued;
        GameEvents.OrderResolved -= OnResolved;
    }

    void OnDestroy()
    {
        if (IsShowing) { IsShowing = false; Time.timeScale = prevTimeScale; }
        if (instance == this) instance = null;
        if (canvas != null) Destroy(canvas.gameObject);
    }

    /// <summary>Closes the card now, as Space would (for tests and anything that must take the screen).</summary>
    public static void Dismiss() { if (instance != null && IsShowing) instance.CloseCard(); }

    static bool Human(ActiveOrder o) => o != null && o.Assignee != null && !o.Assignee.IsBot;

    // ---------- Events ----------

    void OnIssued(ActiveOrder o)
    {
        if (hinted || !Human(o)) return;
        hinted = true;
        hintOrder = o;
        hintAt = Time.unscaledTime + hintDelay;
    }

    void OnResolved(ActiveOrder o, Choice choice)
    {
        if (o == hintOrder) HideHint();
        if (!Human(o)) return;
        var entry = RunRecord.Choices.LastOrDefault(e => e.orderNumber == o.Number);
        if (entry == null) return;

        bool first = choice switch
        {
            Choice.Obey or Choice.Fake => !shownReport,
            Choice.Refuse => !shownRefuse,
            _ => !shownExpired,
        };
        // The iceberg never waits for a card: from there on it's the small note only.
        bool finale = GameManager.Instance != null && GameManager.Instance.State >= GameState.Iceberg;
        if (!first || finale) { ShowToast(o, entry); return; }
        if (choice == Choice.Obey || choice == Choice.Fake) shownReport = true;
        else if (choice == Choice.Refuse) shownRefuse = true;
        else shownExpired = true;
        // A card already up (two answers at once): this one gets the small note instead.
        if (IsShowing || pendingCard != null) { ShowToast(o, entry); return; }
        pendingCard = entry;
        pendingOrder = o;
    }

    // ---------- Update ----------

    void Update()
    {
        float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        bool blocked = PauseMenu.Instance != null && PauseMenu.Instance.IsPaused || PartCard.IsShowing || IntroCard.IsShowing;

        if (pendingCard != null && !IsShowing && !blocked && !ClueCloseUp.IsOpen && !ChartView.IsOpen)
        {
            OpenCard(pendingOrder, pendingCard);
            pendingCard = null;
            pendingOrder = null;
        }
        if (IsShowing && now - cardOpenedAt > 0.6f && ContinuePressed()) CloseCard();

        if (hintAt > 0f && now >= hintAt && !IsShowing && !blocked) ShowHint();

        Fade(cardGroup, IsShowing, dt, 6f);
        Fade(dimGroup, IsShowing, dt, 6f);
        Fade(toastGroup, now < toastUntil && !IsShowing, dt, 5f);
        Fade(hintGroup, now < hintUntil && !IsShowing && !blocked, dt, 4f);
        if (IsShowing) card.localScale = Vector3.one * Mathf.Lerp(card.localScale.x, 1f, 1f - Mathf.Exp(-dt * 12f));
    }

    static void Fade(CanvasGroup g, bool on, float dt, float speed)
    {
        if (g != null) g.alpha = Mathf.MoveTowards(g.alpha, on ? 1f : 0f, dt * speed);
    }

    static bool ContinuePressed()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) return true;
        foreach (var pad in Gamepad.all) if (pad.buttonSouth.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame) return true;
        return false;
    }

    // ---------- Words ----------

    static string TaskLine(OrderData d)
    {
        string line = string.IsNullOrEmpty(d.resultsLine) ? HudText.Where(d) : d.resultsLine;
        return line.Length > 0 ? char.ToUpperInvariant(line[0]) + line.Substring(1) : line;
    }

    /// <summary>"YOU REFUSED" with one player, "EZRA REFUSED" with more, so everyone at the screen knows whose it was.</summary>
    static string Kind(ActiveOrder o, Choice c)
    {
        bool several = CrewRegistry.All.Count(a => !a.IsBot) > 1;
        string who = several ? HudText.Seat(o.Assignee.Seat).ToUpperInvariant() : "YOU";
        return c switch
        {
            // Honest and fake reports read the same, like the order card's stamp: the Captain finds out later.
            Choice.Obey or Choice.Fake => $"{who} REPORTED IT DONE",
            Choice.Refuse => $"{who} REFUSED",
            _ => several ? $"{who} RAN OUT OF TIME" : "YOU RAN OUT OF TIME",
        };
    }

    static string Note(Choice c) => c switch
    {
        Choice.Obey or Choice.Fake =>
            "Every order moves these three, and where they end up decides how the voyage ends.\nReport a job done without doing it and the ship pays for it. He may find out, too.",
        Choice.Refuse =>
            "Refusing costs you his trust, but not every order is worth obeying.\nEvery order moves these three, and where they end up decides how the voyage ends.",
        _ => "Doing nothing is an answer too, and it costs.\nEvery order moves these three, and where they end up decides how the voyage ends.",
    };

    static string Signed(int v) => v > 0 ? $"<color={Up}>+{v}</color>" : v < 0 ? $"<color={Down}>-{-v}</color>" : $"<color={Flat}>0</color>";

    // ---------- Card ----------

    void OpenCard(ActiveOrder o, ChoiceEntry e)
    {
        HideToast();
        cardKind.text = Kind(o, e.choice);
        cardTitle.text = TaskLine(e.order);
        string[] names = { "Captain's Trust", "Ship Safety", "Crew Trust" };
        int[] values = { e.delta.captainTrust, e.delta.shipSafety, e.delta.crewTrust };
        for (int i = 0; i < 3; i++)
        {
            cardRows[i].text = names[i];
            int v = values[i];
            cardValues[i].text = v == 0 ? $"<color={Flat}>no change</color>" : Signed(v);
        }
        cardNote.text = Note(e.choice) + $"\n<color=#dcae57>{(Gamepad.current != null ? "TAB/VIEW" : "TAB")}</color> shows the meters any time.";
        card.localScale = Vector3.one * 0.94f;
        cardOpenedAt = Time.unscaledTime;
        prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        IsShowing = true;
    }

    void CloseCard()
    {
        IsShowing = false;
        Time.timeScale = prevTimeScale;
    }

    // ---------- Toast and hint ----------

    void ShowToast(ActiveOrder o, ChoiceEntry e)
    {
        toastHead.text = $"{Kind(o, e.choice)}  ·  {TaskLine(e.order).ToUpperInvariant()}";
        toastBody.text = $"Captain {Signed(e.delta.captainTrust)}     Safety {Signed(e.delta.shipSafety)}     Crew {Signed(e.delta.crewTrust)}";
        toastUntil = Time.unscaledTime + toastSeconds;
    }

    void HideToast() => toastUntil = -1f;

    void ShowHint()
    {
        hintAt = -1f;
        if (hintOrder == null || hintOrder.IsResolved) return;
        // A crewmate who isn't one of the players says it; not the Stoker if there's anyone else.
        var bots = CrewRegistry.All.Where(a => a.IsBot).ToList();
        var who = bots.FirstOrDefault(a => !a.IsStoker) ?? bots.FirstOrDefault();
        hintName.text = who != null ? HudText.Seat(who.Seat) : "A crewmate";
        string refuse = HudText.KeyFor(HudText.Key.Refuse, hintOrder.Assignee as Component);
        string report = HudText.KeyFor(HudText.Key.Report, hintOrder.Assignee as Component);
        hintLine.text = $"Psst. You can say no.\n<b>{refuse}</b>  refuse     <b>{report}</b>  done";
        hintUntil = Time.unscaledTime + hintSeconds;
    }

    void HideHint()
    {
        hintAt = -1f;
        hintUntil = -1f;
    }

    // ---------- Building ----------

    void Build()
    {
        var theme = UiTheme.Load();
        Font F(Font f) => f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Font body = F(theme ? theme.body : null), italic = F(theme ? theme.italic : null), title = F(theme ? theme.title : null);

        canvas = new GameObject("[ChoiceFeedback UI]", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 125; // over the HUD, the bubbles and the chart; under the part cards and the pause menu
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvas.transform;

        // The card: the screen dims, a navy card with a brass edge in the middle.
        var dim = Panel(root, "Dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, out dimGroup);
        Stretch(Box(dim, new Color(0.02f, 0.03f, 0.06f, 0.62f), Vector2.zero, Vector2.zero).rectTransform);

        var cardSize = new Vector2(640f, 470f);
        card = Panel(root, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), cardSize, out cardGroup);
        Box(card, Brass, Vector2.zero, cardSize + new Vector2(4f, 4f));
        Box(card, Navy, Vector2.zero, cardSize);
        cardKind = Label(card, body, 22, Brass, new Vector2(0f, 190f), new Vector2(560f, 30f), TextAnchor.MiddleLeft);
        cardTitle = Label(card, title, 34, Cream, new Vector2(0f, 148f), new Vector2(560f, 46f), TextAnchor.MiddleLeft);
        for (int i = 0; i < 3; i++)
        {
            float y = 88f - i * 52f;
            Box(card, new Color(1f, 1f, 1f, 0.09f), new Vector2(0f, y + 26f), new Vector2(560f, 1.5f));
            cardRows[i] = Label(card, body, 26, Cream, new Vector2(0f, y), new Vector2(560f, 40f), TextAnchor.MiddleLeft);
            cardValues[i] = Label(card, body, 28, Cream, new Vector2(0f, y), new Vector2(560f, 40f), TextAnchor.MiddleRight);
        }
        Box(card, new Color(1f, 1f, 1f, 0.09f), new Vector2(0f, -42f), new Vector2(560f, 1.5f)); // under the last row
        cardNote = Label(card, italic, 22, DimCream, new Vector2(0f, -110f), new Vector2(560f, 120f), TextAnchor.UpperLeft);
        cardNote.lineSpacing = 1.1f;
        var go = Label(card, body, 22, Brass, new Vector2(0f, -205f), new Vector2(560f, 30f), TextAnchor.MiddleRight);
        go.text = Gamepad.current != null ? "A  continue" : "SPACE  continue";

        // The toast: a slim navy note bottom right, over the Captain's strip (the order cards fill the top right).
        var toastSize = new Vector2(560f, 78f);
        toast = Panel(root, "Toast", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f - toastSize.x * 0.5f, 250f + toastSize.y * 0.5f), toastSize, out toastGroup);
        Box(toast, new Color(Brass.r, Brass.g, Brass.b, 0.9f), Vector2.zero, toastSize + new Vector2(3f, 3f));
        Box(toast, Navy, Vector2.zero, toastSize);
        toastHead = Label(toast, body, 18, Brass, new Vector2(0f, 17f), new Vector2(520f, 26f), TextAnchor.MiddleLeft);
        toastBody = Label(toast, body, 24, Cream, new Vector2(0f, -15f), new Vector2(520f, 34f), TextAnchor.MiddleLeft);

        // The hint: a paper whisper bubble, bottom left above the Captain's strip, like the crew's own.
        var hintSize = new Vector2(420f, 128f);
        hint = Panel(root, "Hint", Vector2.zero, Vector2.zero, new Vector2(40f + hintSize.x * 0.5f, 250f + hintSize.y * 0.5f), hintSize, out hintGroup);
        hint.localRotation = Quaternion.Euler(0f, 0f, 0.8f);
        Box(hint, new Color(0f, 0f, 0f, 0.35f), new Vector2(7f, -9f), hintSize);
        Box(hint, new Color(0.97f, 0.93f, 0.84f, 0.98f), Vector2.zero, hintSize);
        hintName = Label(hint, body, 20, new Color(0.55f, 0.2f, 0.15f), new Vector2(0f, 42f), new Vector2(370f, 26f), TextAnchor.MiddleLeft);
        hintLine = Label(hint, italic, 32, new Color(0.23f, 0.16f, 0.11f), new Vector2(0f, -14f), new Vector2(370f, 84f), TextAnchor.MiddleLeft);
        hintLine.lineSpacing = 1.15f;
    }

    static RectTransform Panel(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, out CanvasGroup group)
    {
        var rt = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        group = rt.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Image Box(RectTransform parent, Color c, Vector2 pos, Vector2 size)
    {
        var img = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = c;
        img.raycastTarget = false;
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = size;
        return img;
    }

    static Text Label(RectTransform parent, Font font, int size, Color c, Vector2 pos, Vector2 box, TextAnchor a)
    {
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font; t.fontSize = size; t.color = c; t.alignment = a;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = box;
        return t;
    }
}
