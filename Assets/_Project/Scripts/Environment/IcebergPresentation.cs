using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// How the iceberg finale looks and reads. Owner: Samprity. Rabib's IcebergSequence still runs the finale (heading,
/// countdown, steering, the impact angle); this only presents it:
///   the iceberg itself (Resources/Iceberg), coming out of the night from far ahead to the bow, swinging off the bow
///   as the heading changes, the way Rabib's placeholder cube did, which it hides;
///   a styled heading gauge and countdown in place of the debug bar;
///   how hard the camera should shake as it closes (GameplayCameraRig reads Shake).
/// Added by GameplayCameraRig in Ship.
/// </summary>
public class IcebergPresentation : MonoBehaviour
{
    [Tooltip("Metres ahead of the stem when the finale starts, and at impact.")]
    public float startAhead = 170f, impactAhead = 4f;
    [Tooltip("How the distance closes: above 1 it stays far for a while, then comes on fast.")]
    public float approachCurve = 1.6f;

    public static IcebergPresentation Instance { get; private set; }
    /// <summary>0 = still, 1 = the hardest shake, in the last seconds before impact.</summary>
    public float Shake { get; private set; }
    public bool Active => berg != null;

    IcebergSequence sequence;
    GameObject berg;
    Light iceLight;
    float total = -1f;
    Vector3 stem;
    float seaY = -2.2f;

    // HUD
    Canvas canvas;
    RectTransform gauge, marker;
    Text countdown, readout, warning;
    Image markerImage;

    static readonly Color Port = new Color(0.62f, 0.16f, 0.12f, 0.9f), Glancing = new Color(0.78f, 0.55f, 0.22f, 0.9f), HeadOn = new Color(0.32f, 0.6f, 0.42f, 0.95f);
    const float GaugeWidth = 640f;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        End();
    }

    void OnEnable() => GameEvents.StateChanged += OnStateChanged;
    void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

    void OnStateChanged(GameState state)
    {
        if (state == GameState.Iceberg) Begin();
        else End();
    }

    void Begin()
    {
        sequence = FindAnyObjectByType<IcebergSequence>();
        if (sequence == null) return;
        IcebergSequence.DrawDebugGui = false;
        total = -1f;

        // The stem: the tip of the prow, else the bow end of the deck.
        var prow = GameObject.Find("Prow");
        stem = prow != null && prow.TryGetComponent(out Renderer pr) ? new Vector3(0f, 0f, pr.bounds.max.z) : new Vector3(0f, 0f, ShipLayout.Z(36f));
        var sea = GameObject.Find("Sea");
        if (sea != null) seaY = sea.transform.position.y;

        var prefab = Resources.Load<GameObject>("Iceberg");
        if (prefab != null)
        {
            berg = Instantiate(prefab);
            berg.name = "[Iceberg Look]";
            iceLight = berg.GetComponentInChildren<Light>();
        }
        BuildHud();
        Place(0f, 0f);
    }

    void End()
    {
        IcebergSequence.DrawDebugGui = true;
        if (berg != null) Destroy(berg);
        if (canvas != null) Destroy(canvas.gameObject);
        berg = null;
        Shake = 0f;
    }

    void Update()
    {
        if (berg == null || sequence == null) return;
        float left = sequence.SecondsLeft;
        if (total < 0f && left > 0f) total = left; // the sequence starts its clock in the same event
        float t = total > 0f ? Mathf.Clamp01(1f - left / total) : 0f;
        Place(t, sequence.Heading);
        // A rumble building as it closes; after the hit, Rabib's impact beat says how hard (worse damage, harder).
        bool hit = total > 0f && left <= 0f;
        Shake = hit ? Mathf.Clamp01(sequence.ImpactShake * 1.4f) : 0.75f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.82f, 1f, t));

        // Rabib's placeholder cube, if it's there: the real one stands in for it.
        var cube = GameObject.Find("[Iceberg]");
        if (cube != null && cube.TryGetComponent(out Renderer r)) r.enabled = false;

        UpdateHud(left, sequence.Heading);
    }

    /// <summary>Closes from far ahead to the stem, swung round the bow by the heading (pointing right, it's off to the left).</summary>
    void Place(float t, float heading)
    {
        float dist = Mathf.Lerp(startAhead, impactAhead, Mathf.Pow(t, approachCurve));
        var dir = Quaternion.Euler(0f, -heading, 0f) * Vector3.forward;
        // The berg's own radius keeps its face, not its centre, at that distance.
        var pos = stem + dir * (dist + 15f);
        pos.y = seaY;
        berg.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 200f - heading * 0.5f + t * 12f, 0f));
        if (iceLight != null)
        {
            // Keep the cold light between the ice and the ship, so the face we see is lit.
            iceLight.transform.position = pos - dir * 30f + Vector3.up * 18f;
            iceLight.range = 120f;
            iceLight.intensity = Mathf.Lerp(14f, 6f, t); // far off it needs a lot to read against the night
        }
    }

    // ---------- Gauge ----------

    void BuildHud()
    {
        var theme = UiTheme.Load();
        Font F(Font f) => f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        canvas = new GameObject("[IcebergHud]", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvas.transform;

        var panel = Box(root, new Color(0.02f, 0.025f, 0.04f, 0.62f), new Vector2(0f, -78f), new Vector2(GaugeWidth + 120f, 150f));
        var p = panel.rectTransform;
        Txt(p, F(theme ? theme.body : null), 20, UiTheme.Brass, "I C E B E R G ,   D E A D   A H E A D", new Vector2(0f, 50f), 600f);
        countdown = Txt(p, F(theme ? theme.title : null), 44, UiTheme.Cream, "30", new Vector2(GaugeWidth * 0.5f + 20f, 6f), 90f);
        countdown.alignment = TextAnchor.MiddleLeft;

        gauge = new GameObject("Gauge", typeof(RectTransform)).GetComponent<RectTransform>();
        gauge.SetParent(p, false);
        gauge.anchoredPosition = new Vector2(-20f, 10f);
        gauge.sizeDelta = new Vector2(GaugeWidth, 14f);
        Band(-90f, 90f, Port);
        Band(-EndingEvaluator.GlancingMaxAngle, EndingEvaluator.GlancingMaxAngle, Glancing);
        Band(-EndingEvaluator.HeadOnMaxAngle, EndingEvaluator.HeadOnMaxAngle, HeadOn);
        var ice = Box(gauge, UiTheme.Cream, new Vector2(0f, 0f), new Vector2(2f, 30f)); // the iceberg, dead ahead
        ice.raycastTarget = false;
        markerImage = Box(gauge, UiTheme.Cream, Vector2.zero, new Vector2(20f, 20f));
        if (theme != null) markerImage.sprite = theme.diamond;
        marker = markerImage.rectTransform;
        Txt(gauge, F(theme ? theme.body : null), 16, UiTheme.DimCream, "PORT", new Vector2(-GaugeWidth * 0.5f + 24f, -22f), 80f);
        Txt(gauge, F(theme ? theme.body : null), 16, UiTheme.DimCream, "STARBOARD", new Vector2(GaugeWidth * 0.5f - 44f, -22f), 120f);

        readout = Txt(p, F(theme ? theme.italic : null), 24, UiTheme.DimCream, "", new Vector2(-20f, -38f), GaugeWidth);
        warning = Txt(p, F(theme ? theme.title : null), 26, new Color(1f, 0.45f, 0.38f), "", new Vector2(-20f, -70f), GaugeWidth + 100f);
    }

    void UpdateHud(float left, float heading)
    {
        if (canvas == null) return;
        countdown.text = Mathf.CeilToInt(left).ToString();
        countdown.color = left < 6f ? new Color(1f, 0.55f, 0.45f) : UiTheme.Cream;
        marker.anchoredPosition = new Vector2(Mathf.Clamp(heading, -90f, 90f) / 180f * GaugeWidth, 0f);
        marker.localRotation = Quaternion.Euler(0f, 0f, 0f);
        float a = Mathf.Abs(heading);
        markerImage.color = a <= EndingEvaluator.HeadOnMaxAngle ? new Color(0.75f, 1f, 0.8f) : a <= EndingEvaluator.GlancingMaxAngle ? new Color(1f, 0.85f, 0.55f) : new Color(1f, 0.6f, 0.5f);

        var wheel = StationRegistry.Get(StationType.Wheel) as Wheel;
        int steering = 0;
        if (wheel != null) wheel.SteerSum(0.2f, out steering);
        string side = a < 1f ? "dead on" : $"{a:0}° to {(heading < 0f ? "port" : "starboard")}";
        readout.text = $"Heading {side}    ·    {(steering == 0 ? "nobody at the wheel" : steering == 1 ? "1 at the wheel" : steering + " at the wheel")}";
        warning.text = wheel != null && wheel.IsBroken ? "THE WHEEL IS JAMMED — hold E at the wheel to free it" : "";
    }

    void Band(float from, float to, Color color)
    {
        var b = Box(gauge, color, new Vector2((from + to) * 0.5f / 180f * GaugeWidth, 0f), new Vector2((to - from) / 180f * GaugeWidth, 14f));
        b.raycastTarget = false;
    }

    static Image Box(RectTransform parent, Color color, Vector2 pos, Vector2 size)
    {
        var img = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = color;
        img.raycastTarget = false;
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = parent == null || parent.GetComponent<Canvas>() == null ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        return img;
    }

    static Text Txt(RectTransform parent, Font font, int size, Color color, string text, Vector2 pos, float width)
    {
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font; t.fontSize = size; t.color = color; t.text = text;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = new Vector2(width, size + 12f);
        var s = t.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.6f);
        return t;
    }
}
