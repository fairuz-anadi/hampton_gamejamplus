using UnityEngine;

/// <summary>
/// The iceberg finale. Owner: Rabib.
/// Tells GameManager the iceberg is handled here, then on GameState.Iceberg runs steerSeconds of steering:
/// the ship's heading starts startAngle off the iceberg and a current pulls it back there, while anyone holding
/// Interact at the Wheel steers with Move left/right (two steerers turn twice as fast, bots add nothing).
/// At the end it calls GameManager.FinishIceberg(heading): 0 = head-on, EndingEvaluator folds the sign.
/// A placeholder white iceberg ahead of the bow and an on-screen heading bar show how it's going.
/// The camera eases in on the bow while steering. At impact there's a short beat before Results: the camera
/// shakes and water rises up the screen, both harder the worse the hit (head-on, glancing, scrape).
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

    [Header("Camera and impact")]
    [Tooltip("How far the camera moves from its deck view toward the bow (0 = stays put, 1 = right on the bow framing).")]
    [SerializeField, Range(0f, 1f)] float zoomAmount = 0.55f;
    [SerializeField] float zoomSeconds = 3f;
    [Tooltip("Seconds between the hit and the Results screen: the shake and the water rising.")]
    [SerializeField] float impactBeat = 2.5f;
    [SerializeField] float maxShake = 0.6f;
    [SerializeField] Color floodColor = new Color(0.08f, 0.22f, 0.35f, 0.55f);

    bool running;
    float elapsed;
    float heading;
    float rest;
    int steererCount;
    Wheel wheel;
    Vector3 bow;
    GameObject iceberg;

    Camera cam;
    Vector3 camStartPos;
    Quaternion camStartRot;
    bool cameraMoved;
    float impactAngle;
    float impactTimer = -1f;
    float damage01; // 0 head-on .. 1 scrape, drives shake and water height

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
        if (impactTimer >= 0f) { TickImpactBeat(); return; }
        if (!running) return;
        ZoomCamera(Mathf.Clamp01(elapsed / Mathf.Max(0.01f, zoomSeconds)));

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
        impactAngle = heading;
        running = false;
        if (wheel != null) { wheel.SteeringOpen = false; wheel.ClearSteering(); }
        var kind = EndingEvaluator.Classify(impactAngle);
        damage01 = kind == ImpactKind.HeadOn ? 0.2f : kind == ImpactKind.Glancing ? 0.55f : 1f;
        impactTimer = 0f;
        Debug.Log($"[IcebergSequence] Impact at {impactAngle:0.0}° ({kind}).");
    }

    /// <summary>The beat after the hit: shake, water rising, then Results.</summary>
    void TickImpactBeat()
    {
        impactTimer += Time.deltaTime;
        if (cam != null)
        {
            float fade = 1f - Mathf.Clamp01(impactTimer / impactBeat);
            var offset = Random.insideUnitSphere * maxShake * damage01 * fade;
            cam.transform.position = Vector3.Lerp(camStartPos, BowCameraPos(), zoomAmount) + offset;
        }
        if (impactTimer < impactBeat) return;

        impactTimer = -1f;
        float angle = impactAngle;
        StopSteering();
        if (GameManager.Instance != null) GameManager.Instance.FinishIceberg(angle);
    }

    // ---------- Camera ----------

    Vector3 BowCameraPos() => bow + new Vector3(0f, 14f, -16f);

    /// <summary>Eases the deck camera part of the way toward a view of the bow and the iceberg.</summary>
    void ZoomCamera(float t)
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
            camStartPos = cam.transform.position;
            camStartRot = cam.transform.rotation;
            cameraMoved = true;
        }
        float k = Mathf.SmoothStep(0f, 1f, t) * zoomAmount;
        var lookRot = Quaternion.LookRotation((bow + Vector3.forward * 4f) - BowCameraPos(), Vector3.up);
        cam.transform.SetPositionAndRotation(Vector3.Lerp(camStartPos, BowCameraPos(), k), Quaternion.Slerp(camStartRot, lookRot, k));
    }

    void RestoreCamera()
    {
        if (cameraMoved && cam != null) cam.transform.SetPositionAndRotation(camStartPos, camStartRot);
        cameraMoved = false;
        cam = null;
    }

    void StopSteering()
    {
        running = false;
        impactTimer = -1f;
        RestoreCamera();
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

        // A primitive's default material renders magenta in builds, so use the URP Lit one from Resources.
        var renderer = iceberg.GetComponent<Renderer>();
        var lit = Resources.Load<Material>("RuntimeLit");
        if (lit != null) renderer.sharedMaterial = lit;
        var mat = renderer.material;
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
        if (impactTimer >= 0f) { DrawFlood(); return; }
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

    /// <summary>Water rising up the screen after the hit; higher for worse damage.</summary>
    void DrawFlood()
    {
        float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(impactTimer / impactBeat)) * Mathf.Lerp(0.2f, 0.75f, damage01);
        float h = Screen.height * rise;
        var old = GUI.color;
        GUI.color = floodColor;
        GUI.DrawTexture(new Rect(0f, Screen.height - h, Screen.width, h), Texture2D.whiteTexture);
        GUI.color = old;
    }

    static void Band(float x, float y, float w, float h, float from, float to, Color color)
    {
        GUI.color = color;
        float x0 = x + (from + 90f) / 180f * w, x1 = x + (to + 90f) / 180f * w;
        GUI.DrawTexture(new Rect(x0, y, x1 - x0, h), Texture2D.whiteTexture);
    }
}
