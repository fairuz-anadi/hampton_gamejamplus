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
/// Notification look (Riham, 28 Oct, the default): a big solid card with only what to do, where, and the keys; after
/// a few seconds it folds into the circular timer with the station's icon and a "TAB details" hint under it. Tab (or
/// View on a controller) opens it out of the circle again (OrderCards handles the key).
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
    [Tooltip("The big card that folds into its timer circle after a few seconds; Tab opens it again.")]
    public bool notification = true;
    [Tooltip("Seconds a new order stays open before folding into its circle.")]
    public float openSeconds = 3f;
    [Tooltip("Seconds a card opened with Tab stays open before folding again.")]
    public float reopenSeconds = 6f;

    /// <summary>Height this card takes in the stack right now (OrderCards spaces them by it).</summary>
    public float StackHeight => notification ? Mathf.Lerp(ClosedSize + TabHintHeight, OpenHeight, open) : ((RectTransform)transform).sizeDelta.y;
    public bool IsOpen => openTarget;

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
        if (!laidOut)
        {
            laidOut = true;
            if (notification) Notification();
            else if (compact) Compact();
        }
    }

    // ---------- Notification look ----------

    const float OpenWidth = 640f, OpenHeight = 168f, ClosedSize = 124f, TabHintHeight = 40f, RingCentre = 62f;
    RectTransform bgRect, rimRect, ringRect, tabHint, dropRect;
    CanvasGroup contentGroup, tabGroup;
    float open = 1f;
    bool openTarget = true;
    float closeAt;

    /// <summary>Opens the card out of its circle (or folds it back), for Tab.</summary>
    public void SetOpen(bool on)
    {
        openTarget = on;
        closeAt = on ? Time.unscaledTime + reopenSeconds : 0f;
    }

    /// <summary>Rebuilds the card as a solid panel that grows out of its timer circle, anchored at the top right.</summary>
    void Notification()
    {
        var rt = (RectTransform)transform;
        rt.sizeDelta = new Vector2(OpenWidth, OpenHeight);
        foreach (var t in new Component[] { header, captainLine }) if (t != null) t.gameObject.SetActive(false);
        var pill = body.Find("Pill")?.GetComponent<Image>();
        var fontBold = timerText != null ? timerText.font : header.font;

        // A brass rim behind a solid dark panel; the panel clips the words, so they're revealed as it grows.
        var rim = new GameObject("Rim", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        rim.transform.SetParent(body, false);
        rim.transform.SetAsFirstSibling();
        rim.sprite = Crisp();
        rim.type = Image.Type.Sliced;
        rim.color = Orange;
        rim.raycastTarget = false;
        rimRect = rim.rectTransform;
        TopRight(rimRect);
        // A soft shadow under the sheet, so it lifts off the deck.
        var drop = new GameObject("Drop", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        drop.transform.SetParent(body, false);
        drop.transform.SetAsFirstSibling();
        drop.sprite = pill != null ? pill.sprite : rim.sprite; // the HUD's soft pill makes a good shadow
        drop.type = Image.Type.Sliced;
        drop.color = new Color(0f, 0f, 0f, 0.35f);
        drop.raycastTarget = false;
        dropRect = drop.rectTransform;
        TopRight(dropRect);
        if (pill != null)
        {
            pill.sprite = Crisp();
            pill.type = Image.Type.Sliced;
            pill.color = Paper;
            bgRect = pill.rectTransform;
            TopRight(bgRect);
            pill.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(8, 0);
            if (flashImage != null)
            {
                flashImage.transform.SetParent(bgRect, false);
                var fr = flashImage.rectTransform;
                fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
            }
        }

        // Everything but the ring lives in a content layer pinned to the panel's right edge, inside the mask.
        var content = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        content.SetParent(bgRect != null ? bgRect : body, false);
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(1f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(OpenWidth, OpenHeight);
        contentGroup = content.GetComponent<CanvasGroup>();

        // What to do: big and bold.
        task = new GameObject("Task", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        task.transform.SetParent(content, false);
        task.font = fontBold;
        task.fontSize = 34;
        task.color = Ink;
        task.alignment = TextAnchor.MiddleLeft;
        task.raycastTarget = false;
        task.horizontalOverflow = HorizontalWrapMode.Wrap;
        task.verticalOverflow = VerticalWrapMode.Truncate;
        task.resizeTextForBestFit = true;
        task.resizeTextMinSize = 22;
        task.resizeTextMaxSize = 34;
        task.lineSpacing = 0.9f;
        Place(task.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -12f), new Vector2(OpenWidth - 32f - 140f, 76f));

        // Where, and the seconds, along the bottom left; keys bottom right, clear of the ring.
        if (where != null)
        {
            where.transform.SetParent(content, false);
            where.fontSize = 22;
            where.color = Rust;
            where.alignment = TextAnchor.LowerLeft;
            Place(where.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 50f), new Vector2(360f, 30f));
        }
        if (timerText != null)
        {
            timerText.transform.SetParent(content, false);
            timerText.fontSize = 24;
            timerCalm = Ink;
            timerUrgent = InkRed;
            timerText.alignment = TextAnchor.LowerRight;
            Place(timerText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-132f, 50f), new Vector2(70f, 30f));
        }
        if (hints != null)
        {
            foreach (var g in hints.GetComponentsInChildren<Graphic>(true)) g.color = InkSoft;
            hints.transform.SetParent(content, false);
            // The keys are laid out from the hints box's right edge, so it sits left-aligned with room to spare.
            Place((RectTransform)hints.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 12f), ((RectTransform)hints.transform).sizeDelta);
        }
        if (botNote != null)
        {
            botNote.color = InkSoft;
            botNote.transform.SetParent(content, false);
            botNote.alignment = TextAnchor.LowerLeft;
            Place(botNote.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 14f), new Vector2(300f, 26f));
        }
        if (stamp != null)
        {
            if (stamp.TryGetComponent(out Outline so)) so.effectColor = new Color(1f, 0.98f, 0.92f, 0.9f);
            stamp.transform.SetParent(content, false);
            Place(stamp.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-140f, 0f), new Vector2(440f, 54f));
        }

        // The timer ring with the station's icon: it stays put, and the panel opens out of it.
        var marker = body.Find("Marker") as RectTransform;
        if (marker != null)
        {
            ringRect = marker;
            marker.SetAsLastSibling();
            Place(marker, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-RingCentre, -RingCentre), new Vector2(96f, 96f));
            var gem = marker.Find("Diamond");
            if (gem != null) gem.gameObject.SetActive(false);
            var back = marker.Find("RingBack")?.GetComponent<Image>();
            if (back != null) back.color = new Color(Ink.r, Ink.g, Ink.b, 0.14f);
            var face = new GameObject("Face", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            face.transform.SetParent(marker, false);
            face.transform.SetAsFirstSibling();
            face.sprite = rim.sprite;
            face.type = Image.Type.Sliced;
            face.pixelsPerUnitMultiplier = rim.sprite != null && rim.sprite.border.x > 0f ? rim.sprite.border.x / 46f : 1f;
            face.color = new Color(1f, 0.98f, 0.93f);
            face.raycastTarget = false;
            face.rectTransform.sizeDelta = new Vector2(92f, 92f);
            icon = new GameObject("Icon", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            icon.transform.SetParent(marker, false);
            icon.raycastTarget = false;
            icon.rectTransform.sizeDelta = new Vector2(46f, 46f);
        }

        // Thin accent down the panel's left edge: brass, or red for the Captain's suspicious orders.
        accent = new GameObject("Accent", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        accent.transform.SetParent(content, false);
        accent.raycastTarget = false;
        var ar = accent.rectTransform;
        ar.anchorMin = new Vector2(0f, 0f); ar.anchorMax = new Vector2(0f, 1f);
        ar.pivot = new Vector2(0f, 0.5f);
        ar.offsetMin = new Vector2(12f, 26f); ar.offsetMax = new Vector2(17f, -26f);

        offBadge = new GameObject("OffBadge", typeof(RectTransform), typeof(Image)).gameObject;
        offBadge.transform.SetParent(content, false);
        var bi = offBadge.GetComponent<Image>();
        bi.color = Red;
        bi.raycastTarget = false;
        Place(bi.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-132f, -14f), new Vector2(34f, 26f));
        var bt = NewText("Text", offBadge.transform, fontBold, 19, Cream, "?!", TextAnchor.MiddleCenter);
        Stretch(bt.rectTransform);
        offBadge.SetActive(false);

        // Under the circle when it's folded: [TAB] details.
        tabHint = new GameObject("TabHint", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        tabHint.SetParent(body, false);
        Place(tabHint, new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-RingCentre, -ClosedSize - 4f), new Vector2(150f, 32f));
        tabGroup = tabHint.GetComponent<CanvasGroup>();
        var cap = new GameObject("Key", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        cap.transform.SetParent(tabHint, false);
        cap.sprite = Crisp();
        cap.type = Image.Type.Sliced;
        cap.color = Paper;
        cap.raycastTarget = false;
        Place(cap.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(62f, 32f));
        cap.gameObject.AddComponent<Outline>().effectColor = Orange;
        tabKeyText = NewText("Letter", cap.transform, fontBold, 19, Ink, "TAB", TextAnchor.MiddleCenter);
        Stretch(tabKeyText.rectTransform);
        var label = NewText("Label", tabHint, fontBold, 22, Cream, "details", TextAnchor.MiddleLeft);
        Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(70f, 0f), new Vector2(90f, 30f));
        label.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);
        ApplyOpen();
    }

    Text tabKeyText;

    /// <summary>Sizes the panel between its circle and fully open; fades the words with it.</summary>
    void ApplyOpen()
    {
        float e = open * open * (3f - 2f * open);
        float w = Mathf.Lerp(ClosedSize, OpenWidth, e), h = Mathf.Lerp(ClosedSize, OpenHeight, e);
        if (bgRect != null)
        {
            bgRect.sizeDelta = new Vector2(w, h);
            // Folded, the corners grow to half its size, so it's a true circle; open, the usual soft pill.
            var img = bgRect.GetComponent<Image>();
            if (img.sprite != null && img.sprite.border.x > 0f) img.pixelsPerUnitMultiplier = Mathf.Lerp(img.sprite.border.x / (ClosedSize * 0.5f), img.sprite.border.x / 22f, e);
        }
        if (dropRect != null)
        {
            dropRect.sizeDelta = new Vector2(w + 10f, h + 10f);
            dropRect.anchoredPosition = new Vector2(2f, -4f);
            var img = dropRect.GetComponent<Image>();
            if (img.sprite != null && img.sprite.border.x > 0f) img.pixelsPerUnitMultiplier = Mathf.Lerp(img.sprite.border.x / (ClosedSize * 0.5f + 5f), 1f, e);
        }
        if (rimRect != null)
        {
            rimRect.sizeDelta = new Vector2(w + 6f, h + 6f);
            var img = rimRect.GetComponent<Image>();
            if (img.sprite != null && img.sprite.border.x > 0f) img.pixelsPerUnitMultiplier = Mathf.Lerp(img.sprite.border.x / (ClosedSize * 0.5f + 3f), img.sprite.border.x / 25f, e);
        }
        if (rimRect != null) rimRect.anchoredPosition = new Vector2(3f, 3f);
        if (contentGroup != null)
        {
            contentGroup.alpha = Mathf.Clamp01((e - 0.45f) / 0.55f);
            bool show = contentGroup.alpha > 0.001f;
            if (contentGroup.gameObject.activeSelf != show) contentGroup.gameObject.SetActive(show);
        }
        if (ringRect != null) ringRect.anchoredPosition = new Vector2(-RingCentre, -Mathf.Lerp(RingCentre, OpenHeight * 0.5f, e));
        if (tabGroup != null) tabGroup.alpha = ForPlayer && !leaving ? Mathf.Clamp01(1f - e * 1.8f) : 0f;
        if (tabKeyText != null) tabKeyText.text = UnityEngine.InputSystem.Gamepad.current != null ? "TAB/VIEW" : "TAB";
        if (tabKeyText != null) ((RectTransform)tabKeyText.transform.parent).sizeDelta = new Vector2(tabKeyText.text.Length > 3 ? 112f : 62f, 32f);
        if (tabHint != null) tabHint.Find("Label").GetComponent<RectTransform>().anchoredPosition = new Vector2(tabKeyText != null && tabKeyText.text.Length > 3 ? 122f : 72f, 0f);
    }

    static Sprite crisp;

    /// <summary>A hard-edged rounded rectangle for 9-slicing (the HUD's pill is deliberately blurred, which smudges paper).</summary>
    static Sprite Crisp()
    {
        if (crisp != null) return crisp;
        const int size = 128, radius = 48;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.5f)));
            }
        tex.Apply();
        crisp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return crisp;
    }

    static void TopRight(RectTransform r)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = Vector2.zero;
    }

    static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static Text NewText(string name, Transform parent, Font font, int size, Color color, string text, TextAnchor align)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font; t.fontSize = size; t.color = color; t.text = text;
        t.alignment = align;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    static readonly Color Brass = new Color(0.86f, 0.68f, 0.34f), Red = new Color(0.84f, 0.3f, 0.24f), Cream = new Color(0.97f, 0.93f, 0.84f);
    // The notification card is a sheet of ship's paper: cream with warm ink, an orange rim and accents.
    static readonly Color Paper = new Color(0.97f, 0.93f, 0.83f), Ink = new Color(0.16f, 0.11f, 0.08f),
        InkSoft = new Color(0.35f, 0.27f, 0.2f), Orange = new Color(0.88f, 0.5f, 0.18f), Rust = new Color(0.62f, 0.3f, 0.12f),
        InkRed = new Color(0.74f, 0.18f, 0.13f), InkGreen = new Color(0.2f, 0.45f, 0.24f);

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
        // A new order arrives open, then folds into its circle after openSeconds.
        open = 0.35f;
        openTarget = true;
        closeAt = Time.unscaledTime + openSeconds;
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
            accent.color = notification ? (off ? InkRed : Color.clear) : off ? Red : Brass; // paper: only the suspicious ones get a stripe
            offBadge.SetActive(off);
            if (icon != null) { icon.texture = IconFor(d.station); icon.color = notification ? Ink : Cream; }
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
        stamp.color = c == Choice.Refuse || c == Choice.Expired ? timerUrgent : notification ? InkGreen : new Color(0.75f, 0.95f, 0.75f);
        stamp.enabled = true;
        if (notification) { openTarget = true; closeAt = 0f; } // open up to show how it ended
        Order = null;
        leaving = true;
        holdResult = 1.3f;
    }

    bool laidOut;

    /// <summary>Editor preview (no Awake in edit mode): lay the card out once, as Awake would.</summary>
    public void PrepareForPreview()
    {
        if (laidOut) return;
        laidOut = true;
        if (notification) Notification();
        else if (compact) Compact();
    }

    /// <summary>Editor preview: show the card fully in, no animation.</summary>
    public void SnapIn()
    {
        appear = 1f;
        if (notification) { open = openTarget ? 1f : 0f; ApplyOpen(); }
        Apply();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (notification)
        {
            if (Order != null && openTarget && closeAt > 0f && Time.unscaledTime >= closeAt) openTarget = false;
            open = Mathf.MoveTowards(open, openTarget ? 1f : 0f, dt * 3.2f);
            ApplyOpen();
        }
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
        float dim = ForPlayer || leaving || notification ? 1f : crewAlpha;
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
