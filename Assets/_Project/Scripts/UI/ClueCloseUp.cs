using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A close look at a clue: the camera eases in on it and a note slides in on the right. Owner: Samprity.
/// Made for the Captain's bottle (Anadi's ShipClues): call ClueCloseUp.ShowBottle(bottle, player) when the player
/// presses E on it, and DressBottle(bottle) once when the bottle is built so it looks like the one in the close-up:
/// clear glass, empty and dry, the cork still sealed with wax. It was never full.
/// The camera goes back to the player when they press E again, walk away, or after a few seconds. It never pauses
/// the game or blocks anything: orders, timers and the Captain carry on.
/// </summary>
[DefaultExecutionOrder(200)] // after GameplayCameraRig (100), so the close-up wins for as long as it's up
public class ClueCloseUp : MonoBehaviour
{
    public const string BottleTitle = "THE CAPTAIN'S BOTTLE";
    public static readonly string[] BottleLines = { "It was never full.", "The Captain was never drunk." };

    [Tooltip("Seconds the close-up stays up if nobody closes it.")]
    public float holdSeconds = 7f;
    [Tooltip("Walking this far from the clue closes it.")]
    public float leaveRange = 2.6f;
    public float blendSeconds = 0.55f;
    public float closeUpFov = 30f;

    /// <summary>A close-up is on screen.</summary>
    public static bool IsOpen => instance != null && instance.target != null;

    static ClueCloseUp instance;
    Transform target;
    Component viewer;
    IInteractSource source;
    bool wasHeld;
    float openedAt, openedAtReal, weight;
    bool closing;
    float baseFov = -1f;
    bool fovTouched;

    RectTransform canvasRect, note;
    CanvasGroup noteGroup;
    Text titleText, bodyText;

    // ---------- API ----------

    /// <summary>Eases the camera in on the bottle and shows the note on the right.</summary>
    public static void ShowBottle(Transform bottle, Component player) => Show(bottle, player, BottleTitle, BottleLines);

    public static void Show(Transform clue, Component player, string title, string[] lines)
    {
        if (clue == null) return;
        if (instance == null) instance = new GameObject("[ClueCloseUp]").AddComponent<ClueCloseUp>();
        instance.Open(clue, player, title, lines);
    }

    public static void Hide()
    {
        if (instance != null) instance.closing = true;
    }

    /// <summary>
    /// Re-dresses a bottle as the Captain's: replaces its renderers' look with the clear, empty, sealed bottle from
    /// Resources/CaptainSet (the same one the close-up shows), standing where the old one stood.
    /// </summary>
    public static void DressBottle(Transform bottle)
    {
        var set = CaptainSet.Load();
        if (bottle == null || set == null || set.clueBottle == null) return;
        var b = Bounds(bottle);
        foreach (var r in bottle.GetComponentsInChildren<Renderer>()) r.enabled = false;
        var look = Instantiate(set.clueBottle, bottle);
        look.name = "BottleLook";
        look.transform.position = new Vector3(b.center.x, b.min.y, b.center.z);
        look.transform.rotation = bottle.rotation;
        // The clue bottle is modelled 30 cm tall; match whatever it replaces.
        look.transform.localScale = Vector3.one * Mathf.Clamp(b.size.y / 0.3f, 0.5f, 3f) / Mathf.Max(0.0001f, bottle.lossyScale.y);
    }

    // ---------- Close-up ----------

    void Open(Transform clue, Component player, string title, string[] lines)
    {
        if (canvasRect == null) Build();
        // The player's own field of view, read before the close-up touches it, so it goes back exactly.
        if (!fovTouched && Camera.main != null) baseFov = Camera.main.fieldOfView;
        target = clue;
        viewer = player;
        source = player != null ? player.GetComponent<IInteractSource>() : null;
        wasHeld = source != null && source.InteractHeld; // the press that opened it doesn't also close it
        openedAt = Time.time;
        openedAtReal = Time.unscaledTime;
        closing = false;
        titleText.text = title;
        bodyText.text = string.Join("\n", lines);
        // A dressed bottle turns its label to whoever picked it up.
        var look = clue.Find("BottleLook");
        if (look != null && player != null)
        {
            var d = player.transform.position - look.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) look.rotation = Quaternion.LookRotation(d);
        }
    }

    void Update()
    {
        if (target == null) { weight = 0f; return; }
        bool held = source != null && source.InteractHeld;
        if (held && !wasHeld && Time.time - openedAt > 0.3f) closing = true;
        wasHeld = held;
        if (Time.time - openedAt > holdSeconds) closing = true;
        if (viewer != null && Flat(viewer.transform.position - target.position) > leaveRange) closing = true;
        var gm = GameManager.Instance;
        if (gm != null && (gm.State < GameState.Part1 || gm.State > GameState.Part3)) closing = true;
        if (PauseMenu.Instance != null && PauseMenu.Instance.IsPaused) closing = true;

        float dt = Time.unscaledDeltaTime;
        weight = Mathf.MoveTowards(weight, closing ? 0f : 1f, dt / blendSeconds);
        noteGroup.alpha = Mathf.MoveTowards(noteGroup.alpha, !closing && Time.unscaledTime - openedAtReal > 0.35f ? 1f : 0f, dt * 3.5f);
        // The note slides in from the right as it fades up, between the readout (top right) and the minimap.
        note.anchoredPosition = new Vector2(-330f + 60f * (1f - noteGroup.alpha), 100f);
        if (closing && weight <= 0f) { target = null; viewer = null; source = null; noteGroup.alpha = 0f; }
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        if (target == null || weight <= 0f)
        {
            if (fovTouched) { cam.fieldOfView = baseFov; fovTouched = false; }
            return;
        }
        fovTouched = true;

        // Where the rig put the camera this frame, eased towards a close look at the clue from the player's side,
        // a little above it and off to the left so the note on the right has room.
        var b = Bounds(target);
        Vector3 centre = b.center;
        Vector3 from = viewer != null ? viewer.transform.position : cam.transform.position;
        Vector3 dir = from - centre;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = -cam.transform.forward;
        dir.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        float reach = Mathf.Max(0.55f, b.size.y * 2.6f);
        Vector3 eye = centre + dir * reach + Vector3.up * (b.size.y * 0.45f) - side * (reach * 0.35f);
        var look = Quaternion.LookRotation(centre + side * (b.size.y * 0.35f) - eye);

        float w = Mathf.SmoothStep(0f, 1f, weight);
        cam.transform.position = Vector3.Lerp(cam.transform.position, eye, w);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, look, w);
        cam.fieldOfView = Mathf.Lerp(baseFov, closeUpFov, w);
    }

    void OnDisable()
    {
        var cam = Camera.main;
        if (cam != null && fovTouched) { cam.fieldOfView = baseFov; fovTouched = false; }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (canvasRect != null) Destroy(canvasRect.gameObject);
    }

    static Bounds Bounds(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position + Vector3.up * 0.15f, Vector3.one * 0.3f);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    // ---------- The note ----------

    void Build()
    {
        var theme = UiTheme.Load();
        Font F(Font f) => f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvas = new GameObject("[ClueCloseUp]", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 46; // over the HUD and the chart, under the pause menu
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)canvas.transform;

        // A paper note on the right of the screen, like the crew's speech bubbles.
        note = new GameObject("Note", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        note.SetParent(canvasRect, false);
        note.anchorMin = note.anchorMax = new Vector2(1f, 0.5f);
        note.sizeDelta = new Vector2(520f, 230f);
        note.localRotation = Quaternion.Euler(0f, 0f, 1.2f);
        noteGroup = note.GetComponent<CanvasGroup>();
        noteGroup.alpha = 0f;
        noteGroup.blocksRaycasts = false;

        var shadow = Box(note, new Color(0f, 0f, 0f, 0.35f), new Vector2(8f, -10f), new Vector2(520f, 230f));
        shadow.name = "Shadow";
        Box(note, new Color(0.97f, 0.93f, 0.84f, 0.98f), Vector2.zero, new Vector2(520f, 230f));
        Box(note, new Color(0.55f, 0.2f, 0.15f, 0.5f), new Vector2(-110f, 62f), new Vector2(240f, 2f));
        titleText = Label(note, F(theme ? theme.body : null), 20, new Color(0.55f, 0.2f, 0.15f), new Vector2(-50f, 84f), new Vector2(380f, 28f), TextAnchor.MiddleLeft);
        bodyText = Label(note, F(theme ? theme.italic : null), 36, new Color(0.23f, 0.16f, 0.11f), new Vector2(0f, -18f), new Vector2(460f, 140f), TextAnchor.MiddleLeft);
        bodyText.lineSpacing = 1.1f;
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
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = box;
        return t;
    }
}
