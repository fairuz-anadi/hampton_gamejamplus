using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The speed reward on screen: a brass "SMARTLY DONE" stamp that thumps in above the deck when a player obeys with
/// time to spare, with the Captain's Trust it earned and the streak so far. Listens to GameEvents.SmartlyDone
/// (OrderDirector decides). Built in code in the menu's type (UiTheme), like the how-to-play card. Owner: Anadi.
/// Spawned by GameSystems.
/// </summary>
public class SmartStamp : MonoBehaviour
{
    [Min(0.5f)] public float holdSeconds = 1.8f;

    CanvasGroup group;
    RectTransform stamp;
    Text title, detail;
    float age = -1f;

    void OnEnable() => GameEvents.SmartlyDone += OnSmart;
    void OnDisable() => GameEvents.SmartlyDone -= OnSmart;

    void Start() => Build();

    void OnDestroy()
    {
        if (group != null) Destroy(group.gameObject);
    }

    void OnSmart(ActiveOrder o, int streak)
    {
        if (group == null) return;
        title.text = streak >= 3 && streak % 3 == 0 ? "SMARTLY DONE!" : "SMARTLY DONE";
        detail.text = $"Captain's Trust +2   ·   streak {streak}";
        age = 0f;
    }

    void Update()
    {
        if (group == null || age < 0f) return;
        age += Time.unscaledDeltaTime;
        // Thump in (big and tilted, settling), hold, then fade.
        float settle = Mathf.Clamp01(age / 0.18f);
        stamp.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, settle);
        stamp.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-9f, -4f, settle));
        group.alpha = age < holdSeconds ? Mathf.Clamp01(age / 0.08f) : Mathf.Clamp01(1f - (age - holdSeconds) / 0.5f);
        if (age > holdSeconds + 0.5f) age = -1f;
    }

    void Build()
    {
        var theme = UiTheme.Load();
        Font F(Font f) => f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvas = new GameObject("[SmartStamp]", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = canvas.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        stamp = new GameObject("Stamp", typeof(RectTransform)).GetComponent<RectTransform>();
        stamp.SetParent(canvas.transform, false);
        stamp.anchorMin = stamp.anchorMax = new Vector2(0.5f, 0.66f);
        stamp.sizeDelta = new Vector2(560f, 130f);

        var plate = new GameObject("Plate", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        plate.transform.SetParent(stamp, false);
        plate.color = new Color(0.03f, 0.035f, 0.05f, 0.78f);
        plate.raycastTarget = false;
        Stretch(plate.rectTransform);
        // A double brass border, like a rubber stamp's frame.
        Frame(stamp, 0f, 3f);
        Frame(stamp, 8f, 1.5f);

        title = Label(stamp, F(theme ? theme.title : null), 52, UiTheme.Brass, new Vector2(0f, 14f), 64f);
        detail = Label(stamp, F(theme ? theme.body : null), 22, UiTheme.Cream, new Vector2(0f, -34f), 30f);
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static void Frame(RectTransform parent, float inset, float width)
    {
        var c = UiTheme.Brass;
        void Edge(Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            var img = new GameObject("Edge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = c;
            img.raycastTarget = false;
            var r = img.rectTransform;
            r.anchorMin = min; r.anchorMax = max; r.offsetMin = offMin; r.offsetMax = offMax;
        }
        Edge(new Vector2(0f, 1f), Vector2.one, new Vector2(inset, -inset - width), new Vector2(-inset, -inset));
        Edge(Vector2.zero, new Vector2(1f, 0f), new Vector2(inset, inset), new Vector2(-inset, inset + width));
        Edge(Vector2.zero, new Vector2(0f, 1f), new Vector2(inset, inset), new Vector2(inset + width, -inset));
        Edge(new Vector2(1f, 0f), Vector2.one, new Vector2(-inset - width, inset), new Vector2(-inset, -inset));
    }

    static Text Label(RectTransform parent, Font font, int size, Color color, Vector2 pos, float height)
    {
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font; t.fontSize = size; t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = new Vector2(540f, height);
        t.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);
        return t;
    }
}
