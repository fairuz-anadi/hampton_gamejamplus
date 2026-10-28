using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The player's own thoughts (Anadi, 28 Oct): a thought cloud over the local player's head that fills with "...",
/// then the thought itself ("Wait... the compass says north?"). Used when something doesn't add up, so the player
/// notices the mystery. Thoughts queue; each says its piece once. Spawned by GameSystems in Ship. Owner: Anadi.
/// </summary>
public class PlayerThought : MonoBehaviour
{
    static PlayerThought instance;
    static readonly HashSet<string> said = new HashSet<string>();

    const float DotsSeconds = 1.3f;
    public float showSeconds = 4.2f;

    readonly Queue<string> queue = new Queue<string>();
    RectTransform canvasRect, bubble;
    CanvasGroup group;
    Text text;
    string current;
    float started = -1f;

    /// <summary>Thinks this once per voyage (key defaults to the text). Ignored if already thought.</summary>
    public static void Think(string thought, string key = null)
    {
        if (instance == null || !said.Add(key ?? thought)) return;
        instance.queue.Enqueue(thought);
    }

    /// <summary>True while a thought is up (other clues can wait their turn).</summary>
    public static bool Busy => instance != null && (instance.current != null || instance.queue.Count > 0);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => said.Clear();

    void Awake() { instance = this; said.Clear(); }
    void Start() => Build();

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (canvasRect != null) Destroy(canvasRect.gameObject);
    }

    void Update()
    {
        if (group == null) return;
        bool paused = (PauseMenu.Instance != null && PauseMenu.Instance.IsPaused) || PartCard.IsShowing;
        if (current == null && queue.Count > 0 && !paused) { current = queue.Dequeue(); started = Time.time; }

        float t = current != null ? Time.time - started : 0f;
        bool show = current != null && !paused;
        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 5f);
        if (current == null) return;

        if (t < DotsSeconds)
        {
            int dots = 1 + Mathf.FloorToInt(t / DotsSeconds * 3f) % 3;
            text.text = new string('.', dots);
            text.fontSize = 64;
        }
        else
        {
            text.text = current;
            text.fontSize = 34;
        }
        if (t > DotsSeconds + showSeconds) current = null;

        // Over the local player's head, kept on screen.
        var cam = Camera.main;
        Component player = null;
        foreach (var c in CrewRegistry.All) if (!c.IsBot && c is Component comp) { player = comp; break; }
        if (cam == null || player == null) return;
        Vector3 sp = cam.WorldToScreenPoint(player.transform.position + Vector3.up * 2.3f);
        if (sp.z <= 0f) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out var local);
        var half = canvasRect.rect.size * 0.5f;
        local.x = Mathf.Clamp(local.x + 20f, -half.x + 260f, half.x - 560f);
        local.y = Mathf.Clamp(local.y + Mathf.Sin(Time.time * 2.5f) * 5f, -half.y + 260f, half.y - 430f);
        bubble.anchoredPosition = local;
        float pop = Mathf.Clamp01(t / 0.2f);
        bubble.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, pop);
    }

    void Build()
    {
        var canvas = new GameObject("[PlayerThought]", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 42;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)canvas.transform;

        bubble = new GameObject("Thought", typeof(RectTransform), typeof(CanvasGroup), typeof(RawImage)).GetComponent<RectTransform>();
        bubble.SetParent(canvasRect, false);
        bubble.sizeDelta = new Vector2(440f, 150f);
        bubble.pivot = new Vector2(0.15f, 0f);
        group = bubble.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        var cloud = bubble.GetComponent<RawImage>();
        cloud.texture = CloudTexture();
        cloud.raycastTarget = false;

        text = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        text.transform.SetParent(bubble, false);
        text.font = Resources.Load<Font>("Fonts/PatrickHand-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.color = new Color(0.2f, 0.14f, 0.1f);
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.rectTransform.anchoredPosition = new Vector2(0f, 18f);
        text.rectTransform.sizeDelta = new Vector2(370f, 90f);
    }

    static Texture2D CloudTexture()
    {
        var p = new EndingPainter(440, 150, new Color(0, 0, 0, 0));
        var c = new Color(0.97f, 0.94f, 0.86f, 0.97f);
        var edge = new Color(0.3f, 0.22f, 0.16f, 0.9f);
        // Outline first (a slightly larger cloud in ink), then the cloud.
        foreach (var (col, grow) in new[] { (edge, 3f), (c, 0f) })
        {
            p.Ellipse(220, 55, 200 + grow, 46 + grow, col);
            p.Circle(90, 46, 40 + grow, col); p.Circle(175, 30, 44 + grow, col); p.Circle(265, 32, 42 + grow, col); p.Circle(350, 48, 38 + grow, col);
            p.Circle(70, 118, 13 + grow, col); p.Circle(48, 140, 7 + grow, col); // the little trail down to the head
        }
        return p.ToTexture();
    }
}
