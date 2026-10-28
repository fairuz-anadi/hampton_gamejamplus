using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Points each player at their own order's station (playtest: "unsure where the tasks are"). A brass diamond bobs
/// over the station with its name and how far it is; when the station is off screen or behind the camera, it waits at
/// the screen's edge with an arrow toward it. Carry orders point at the pickup first, then the delivery. It steps aside
/// once the player is at the station, where the station's own prompt takes over. Same target as the minimap's pulsing
/// ring. Spawns itself in gameplay scenes, built in code. Owner: Samprity.
/// </summary>
public class TaskPointer : MonoBehaviour
{
    [Tooltip("Metres above the station the marker floats.")]
    public float heightAbove = 2.6f;
    [Tooltip("Closer than this and the station's own prompt is enough.")]
    public float hideWithin = 3.2f;
    [Tooltip("Canvas units kept clear round the edge: the order cards at the top, the Captain's strip at the bottom.")]
    public float edgeTop = 380f, edgeBottom = 230f, edgeSide = 90f;

    static readonly Color Brass = new Color(0.86f, 0.68f, 0.34f);
    static readonly Color Cream = new Color(0.97f, 0.93f, 0.84f);

    static TaskPointer instance;

    class Pointer
    {
        public RectTransform root, arrow;
        public CanvasGroup group;
        public Image diamond, arrowImg;
        public Text label;
        public float shown;
    }

    Canvas canvas;
    RectTransform canvasRect;
    readonly List<Pointer> pointers = new List<Pointer>(4);
    Sprite diamondSprite;
    Font font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive || !GameSystems.IsGameplayScene(scene.name) || instance != null) return;
        new GameObject("[TaskPointer]").AddComponent<TaskPointer>();
    }

    void Awake()
    {
        instance = this;
        var theme = UiTheme.Load();
        diamondSprite = theme != null ? theme.diamond : null;
        font = theme != null && theme.body != null ? theme.body : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        canvas = new GameObject("[TaskPointer UI]", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30; // under the crew's bubbles and every screen
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)canvas.transform;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (canvas != null) Destroy(canvas.gameObject);
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        var humans = CrewRegistry.All.Where(c => !c.IsBot).ToList();
        while (pointers.Count < humans.Count) pointers.Add(MakePointer());

        // Nothing over a screen that has the game stopped, or over the chart, a clue or the Captain's talk.
        bool covered = Time.timeScale == 0f || ChartView.IsOpen || ClueCloseUp.IsOpen || CaptainTalk.IsOpen;
        for (int i = 0; i < pointers.Count; i++)
        {
            var p = pointers[i];
            Vector3? target = i < humans.Count && cam != null && !covered ? Target(humans[i]) : null;
            bool show = target.HasValue && Flat(target.Value - humans[i].Position) > hideWithin;
            p.shown = Mathf.MoveTowards(p.shown, show ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            p.group.alpha = p.shown;
            if (!target.HasValue || p.shown <= 0f) continue;
            Place(p, cam, target.Value, humans[i], humans.Count > 1);
        }
    }

    /// <summary>The station the player's own order wants now: the pickup, then where it goes once they hold it.</summary>
    static Vector3? Target(ICrewActor actor)
    {
        var order = OrderDirector.Instance != null ? OrderDirector.Instance.GetOrderFor(actor) : null;
        if (order == null || order.IsResolved) return null;
        var type = order.Data.station;
        if (order.Data.isCarry)
        {
            var interactor = (actor as Component)?.GetComponent<Interactor>();
            if (interactor != null && interactor.IsCarrying) type = order.Data.deliverTo;
        }
        var station = StationRegistry.Get(type);
        return station != null ? station.Position : (Vector3?)null;
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    void Place(Pointer p, Camera cam, Vector3 station, ICrewActor actor, bool several)
    {
        float bob = Mathf.Sin(Time.unscaledTime * 2.6f) * 0.12f;
        Vector3 world = station + Vector3.up * (heightAbove + bob);
        Vector3 vp = cam.WorldToViewportPoint(world);
        bool behind = vp.z < 0f;
        if (behind) { vp.x = 1f - vp.x; vp.y = 1f - vp.y; } // mirrored, so the arrow still points the right way round

        Vector2 size = canvasRect.rect.size;
        var pos = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);
        float halfW = size.x * 0.5f - edgeSide, top = size.y * 0.5f - edgeTop, bottom = -size.y * 0.5f + edgeBottom;
        bool onScreen = !behind && pos.x > -halfW && pos.x < halfW && pos.y > bottom && pos.y < top;
        if (!onScreen)
        {
            // Out along the line from the middle of the safe area to the station, stopped at its edge.
            var mid = new Vector2(0f, (top + bottom) * 0.5f);
            var dir = pos - mid;
            if (behind && dir.y > 0f) dir.y = -dir.y;
            if (dir.sqrMagnitude < 1f) dir = Vector2.down;
            float sx = dir.x != 0f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
            float sy = dir.y > 0f ? (top - mid.y) / dir.y : dir.y < 0f ? (bottom - mid.y) / dir.y : float.MaxValue;
            pos = mid + dir * Mathf.Min(sx, sy);
            p.arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        }
        p.arrowImg.enabled = !onScreen;
        p.root.anchoredPosition = pos;

        var order = OrderDirector.Instance.GetOrderFor(actor);
        StationType type = order.Data.station;
        if (order.Data.isCarry && (actor as Component)?.GetComponent<Interactor>() is Interactor it && it.IsCarrying) type = order.Data.deliverTo;
        string name = type == StationType.Wheel && order.Data.isCarry && type == order.Data.deliverTo ? "Bridge" : HudText.StationName(type);
        int metres = Mathf.RoundToInt(Flat(station - actor.Position));
        string who = several ? HudText.Seat(actor.Seat) + "  ·  " : "";
        p.label.text = $"{who}{name.ToUpperInvariant()}  <color=#efe6d2>{metres} m</color>";
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
        p.diamond.rectTransform.localScale = Vector3.one * (1f + 0.12f * pulse);
    }

    Pointer MakePointer()
    {
        var p = new Pointer();
        p.root = new GameObject("Pointer", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        p.root.SetParent(canvasRect, false);
        p.root.sizeDelta = new Vector2(80f, 80f);
        p.group = p.root.GetComponent<CanvasGroup>();
        p.group.alpha = 0f;
        p.group.blocksRaycasts = false;

        // A dark backing so the brass reads on the pale deck and the bright sea.
        Img(p.root, null, new Color(0.08f, 0.1f, 0.16f, 0.8f), new Vector2(56f, 56f), 45f);
        Img(p.root, null, Brass, new Vector2(46f, 46f), 45f);
        Img(p.root, null, new Color(0.08f, 0.1f, 0.16f, 0.9f), new Vector2(40f, 40f), 45f);
        p.diamond = Img(p.root, diamondSprite, Brass, new Vector2(30f, 30f), diamondSprite != null ? 0f : 45f);

        p.arrow = new GameObject("Arrow", typeof(RectTransform)).GetComponent<RectTransform>();
        p.arrow.SetParent(p.root, false);
        p.arrowImg = Img(p.arrow, Triangle(), Brass, new Vector2(34f, 26f), 0f);
        p.arrowImg.preserveAspect = false;
        p.arrowImg.rectTransform.anchoredPosition = new Vector2(0f, 54f);

        p.label = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<Text>();
        p.label.transform.SetParent(p.root, false);
        p.label.font = font; p.label.fontSize = 26; p.label.color = Brass;
        p.label.alignment = TextAnchor.UpperCenter;
        p.label.horizontalOverflow = HorizontalWrapMode.Overflow;
        p.label.verticalOverflow = VerticalWrapMode.Overflow;
        p.label.supportRichText = true;
        p.label.raycastTarget = false;
        p.label.rectTransform.anchoredPosition = new Vector2(0f, -56f);
        p.label.rectTransform.sizeDelta = new Vector2(360f, 34f);
        var outline = p.label.GetComponent<Outline>();
        outline.effectColor = new Color(0.05f, 0.06f, 0.1f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return p;
    }

    static Sprite triangle;

    /// <summary>An arrowhead pointing up, drawn once.</summary>
    static Sprite Triangle()
    {
        if (triangle != null) return triangle;
        const int w = 64, h = 48;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float half = (1f - (y + 0.5f) / h) * w * 0.5f, d = Mathf.Abs(x + 0.5f - w * 0.5f);
                byte a = (byte)(Mathf.Clamp01(half - d + 0.5f) * 255f);
                px[y * w + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return triangle = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    static Image Img(RectTransform parent, Sprite sprite, Color c, Vector2 size, float angle)
    {
        var img = new GameObject("Image", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.sprite = sprite;
        img.color = c;
        img.preserveAspect = sprite != null;
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = size;
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        return img;
    }
}
