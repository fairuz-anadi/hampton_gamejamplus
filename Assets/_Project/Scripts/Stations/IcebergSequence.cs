using UnityEngine;

/// <summary>
/// The iceberg finale. Owner: Rabib.
/// Tells GameManager the iceberg is handled here, then on GameState.Iceberg runs steerSeconds of steering:
/// the ship's heading starts startAngle off the iceberg and a current pulls it back there, while anyone holding
/// Interact at the Wheel steers with Move left/right (two steerers turn twice as fast, bots add nothing).
/// At the end it calls GameManager.FinishIceberg(heading): 0 = head-on, EndingEvaluator folds the sign.
/// A placeholder white iceberg ahead of the bow and an on-screen heading bar show how it's going.
/// </summary>
public class IcebergSequence : MonoBehaviour
{
    public enum StartSide { Random, Left, Right }

    [Header("Steering")]
    [SerializeField] float steerSeconds = 30f;
    [Tooltip("Degrees off the iceberg at the start. The current also pulls back to this, so doing nothing hits at about this angle.")]
    [SerializeField] float startAngle = 35f;
    [Tooltip("Degrees per second each steerer turns the ship at full input.")]
    [SerializeField] float perSteererRate = 3f;
    [Tooltip("How hard the current pulls the heading back to startAngle, per second.")]
    [SerializeField] float pull = 0.05f;
    [SerializeField, Range(0f, 0.9f)] float deadzone = 0.2f;
    [Tooltip("Debug: force which side the ship starts off the iceberg. Left = negative heading.")]
    [SerializeField] StartSide startSide = StartSide.Random;

    [Header("Placeholder iceberg")]
    [Tooltip("Optional. Empty = bowAheadOfWheel metres in front of the Wheel (the bow rail on Samprity's deck).")]
    [SerializeField] Transform bowPoint;
    [SerializeField] float bowAheadOfWheel = 7f;
    [Tooltip("Metres ahead of the bow when the finale starts. 10 keeps it inside the fixed Ship deck camera.")]
    [SerializeField] float startAhead = 10f;
    [Tooltip("Metres ahead of the bow at impact.")]
    [SerializeField] float impactAhead = 2f;
    [SerializeField] Vector3 icebergSize = new Vector3(5f, 5f, 4f);
    [SerializeField] Color icebergColor = new Color(0.93f, 0.97f, 1f);

    bool running;
    float elapsed;
    float heading;
    float rest;
    int steererCount;
    Wheel wheel;
    Vector3 bow;
    GameObject iceberg;

    /// <summary>Current heading off the iceberg in degrees, 0 = head-on.</summary>
    public float Heading => heading;
    public float SecondsLeft => running ? Mathf.Max(0f, steerSeconds - elapsed) : 0f;

    void Awake() => ClaimIceberg();

    // GameManager.Instance may not exist yet when this Awake() runs (scene object order isn't
    // guaranteed), so claim again once every Awake in the scene has finished.
    void Start() => ClaimIceberg();

    static void ClaimIceberg()
    {
        if (GameManager.Instance != null) GameManager.Instance.IcebergHandledExternally = true;
    }

    void OnEnable() => GameEvents.StateChanged += OnStateChanged;

    void OnDisable()
    {
        GameEvents.StateChanged -= OnStateChanged;
        StopSteering();
    }

    void OnStateChanged(GameState state)
    {
        if (state == GameState.Iceberg) Begin();
        else StopSteering(); // Results (debug 1/2/3, F1) or a restart: never finish twice
    }

    void Begin()
    {
        float side = startSide switch
        {
            StartSide.Left => -1f,
            StartSide.Right => 1f,
            _ => Random.value < 0.5f ? -1f : 1f,
        };
        rest = side * startAngle;
        heading = rest;
        elapsed = 0f;
        running = true;

        wheel = StationRegistry.Get(StationType.Wheel) as Wheel;
        if (wheel != null)
        {
            wheel.ClearSteering();
            wheel.SteeringOpen = true;
        }
        else Debug.LogWarning("[IcebergSequence] No Wheel in the scene, so nobody can steer.");

        bow = bowPoint != null ? bowPoint.position
            : wheel != null ? wheel.Position + Vector3.forward * bowAheadOfWheel
            : transform.position;
        SpawnIceberg();
        PlaceIceberg();

        Debug.Log($"[IcebergSequence] Iceberg! {steerSeconds:0}s to steer, starting {heading:0}° off.");
    }

    void Update()
    {
        if (!running) return;

        float steer = wheel != null ? wheel.SteerSum(deadzone, out steererCount) : 0f;
        if (wheel == null) steererCount = 0;
        heading += (perSteererRate * steer + pull * (rest - heading)) * Time.deltaTime;
        heading = Mathf.Clamp(heading, -90f, 90f);

        elapsed += Time.deltaTime;
        PlaceIceberg();

        if (elapsed >= steerSeconds) Impact();
    }

    void Impact()
    {
        float angle = heading;
        StopSteering();
        Debug.Log($"[IcebergSequence] Impact at {angle:0.0}°.");
        if (GameManager.Instance != null) GameManager.Instance.FinishIceberg(angle);
    }

    void StopSteering()
    {
        running = false;
        if (wheel != null)
        {
            wheel.SteeringOpen = false;
            wheel.ClearSteering();
            wheel = null;
        }
        if (iceberg != null) Destroy(iceberg);
        iceberg = null;
    }

    // ---------- Placeholder iceberg ----------

    void SpawnIceberg()
    {
        if (iceberg != null) Destroy(iceberg);
        iceberg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        iceberg.name = "[Iceberg]";
        iceberg.transform.localScale = icebergSize;
        Destroy(iceberg.GetComponent<Collider>()); // scenery only, mustn't block anyone or the NavMesh

        var mat = iceberg.GetComponent<Renderer>().material;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", icebergColor);
        else mat.color = icebergColor;
    }

    /// <summary>Closes in from startAhead to impactAhead, swinging round the bow by the heading: a ship pointing right sees it off to the left.</summary>
    void PlaceIceberg()
    {
        if (iceberg == null) return;
        float t = steerSeconds > 0f ? Mathf.Clamp01(elapsed / steerSeconds) : 1f;
        float dist = Mathf.Lerp(startAhead, impactAhead, t);
        var pos = bow + Quaternion.Euler(0f, -heading, 0f) * Vector3.forward * dist;
        pos.y = bow.y + icebergSize.y * 0.5f;
        iceberg.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 20f - heading, 0f));
    }

    // ---------- Heading bar and countdown ----------

    void OnGUI()
    {
        if (!running) return;

        const float w = 420f, h = 18f;
        float x = (Screen.width - w) * 0.5f, y = 20f;

        var old = GUI.color;
        Band(x, y, w, h, -90f, 90f, new Color(0.75f, 0.2f, 0.15f));
        Band(x, y, w, h, -EndingEvaluator.GlancingMaxAngle, EndingEvaluator.GlancingMaxAngle, new Color(0.9f, 0.65f, 0.2f));
        Band(x, y, w, h, -EndingEvaluator.HeadOnMaxAngle, EndingEvaluator.HeadOnMaxAngle, new Color(0.3f, 0.8f, 0.35f));

        GUI.color = Color.white; // the iceberg, dead ahead
        GUI.DrawTexture(new Rect(x + w * 0.5f - 1f, y - 4f, 2f, h + 8f), Texture2D.whiteTexture);
        GUI.color = Color.black; // the ship's heading
        GUI.DrawTexture(new Rect(x + (heading + 90f) / 180f * w - 3f, y - 6f, 6f, h + 12f), Texture2D.whiteTexture);
        GUI.color = old;

        string jammed = wheel != null && wheel.IsBroken ? "   WHEEL JAMMED, hold Interact to repair" : "";
        GUI.Label(new Rect(x, y + h + 8f, w + 200f, 22f),
            $"ICEBERG AHEAD   Impact in {SecondsLeft:0}s   Heading {heading:0}°   Steering: {steererCount}{jammed}");
    }

    static void Band(float x, float y, float w, float h, float from, float to, Color color)
    {
        GUI.color = color;
        float x0 = x + (from + 90f) / 180f * w, x1 = x + (to + 90f) / 180f * w;
        GUI.DrawTexture(new Rect(x0, y, x1 - x0, h), Texture2D.whiteTexture);
    }
}
