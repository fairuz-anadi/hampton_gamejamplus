using UnityEngine;

/// <summary>
/// The voyage clock: dawn, morning, noon, afternoon, dusk, night and back to dawn, once every cycleSeconds (5 min).
/// Owner: Samprity. It only keeps time and works out what the light should be; PartMood applies it to the scene
/// and lays the story part's mood on top.
///
/// The run is lined up so the iceberg (expected about 8 minutes in) arrives at night, like the real ship.
/// If the iceberg comes early or late, the clock fast-forwards to night when the iceberg starts.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    public struct TimeOfDay
    {
        public Vector3 sunEuler, moonEuler;
        public Color sunColor;
        public float sunIntensity, moonIntensity;
        public Color ambientSky, ambientEquator, ambientGround;
        public Color fogColor;
        public float fogDensity;
        [Tooltip("0 = lamps off (bright day), 1 = lamps fully on (night).")]
        public float lampLevel;
        public float skyExposure;
        /// <summary>0 at night, 1 in full daylight.</summary>
        public float daylight;
    }

    [Header("Clock")]
    [Tooltip("Seconds for one full dawn-to-dawn day.")]
    [Min(10f)] public float cycleSeconds = 300f;
    [Tooltip("Share of the cycle the sun is up. The rest is night.")]
    [Range(0.4f, 0.9f)] public float dayShare = 0.72f;
    [Tooltip("Line the run up so the iceberg lands at night. Off = start at startPhase.")]
    public bool icebergAtNight = true;
    [Tooltip("Roughly when the iceberg finale starts, in seconds from the first order.")]
    public float expectedIcebergSeconds = 480f;
    [Tooltip("Where in the cycle the iceberg should land (0.86 = middle of the night).")]
    [Range(0f, 1f)] public float icebergPhase = 0.86f;
    [Tooltip("Used when icebergAtNight is off. 0 = dawn.")]
    [Range(0f, 1f)] public float startPhase = 0f;
    [Tooltip("Seconds to fast-forward to night if the iceberg starts in daylight.")]
    [Min(0.5f)] public float catchUpSeconds = 5f;

    [Header("Sun")]
    [Range(10f, 89f)] public float sunHighest = 66f;
    public float sunriseYaw = 70f, sunsetYaw = 290f;

    [Header("Colours")]
    public Color noonSun = new Color(1f, 0.96f, 0.88f);
    public Color lowSun = new Color(1f, 0.55f, 0.32f);
    public Color daySky = new Color(0.62f, 0.72f, 0.88f), dayEquator = new Color(0.6f, 0.6f, 0.62f), dayGround = new Color(0.36f, 0.33f, 0.3f);
    public Color duskSky = new Color(0.58f, 0.44f, 0.5f), duskEquator = new Color(0.66f, 0.46f, 0.38f);
    // Night is a bright moonlit night: dark enough to read as night, light enough to play.
    public Color nightSky = new Color(0.3f, 0.36f, 0.55f), nightEquator = new Color(0.21f, 0.24f, 0.36f), nightGround = new Color(0.1f, 0.11f, 0.15f);
    public Color dayFog = new Color(0.74f, 0.83f, 0.92f), duskFog = new Color(0.9f, 0.6f, 0.48f), nightFog = new Color(0.13f, 0.17f, 0.28f);

    [Header("Night")]
    [Tooltip("Moonlight strength at midnight.")]
    [Min(0f)] public float moonBrightness = 0.95f;
    [Tooltip("Sky brightness at midnight (1.3 = full day).")]
    [Min(0f)] public float nightSkyExposure = 0.5f;

    /// <summary>0..1 through the day: 0 = dawn, ~0.36 = noon, dayShare = dusk, 0.86 = middle of the night.</summary>
    public float Phase { get; private set; }
    public string PhaseName => NameOf(Phase);
    public float RunStartPhase => icebergAtNight ? Mathf.Repeat(icebergPhase - expectedIcebergSeconds / cycleSeconds, 1f) : startPhase;

    float fastTo = -1f;

    void OnEnable()
    {
        GameEvents.StateChanged += OnStateChanged;
        Phase = RunStartPhase;
    }

    void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

    void OnStateChanged(GameState s)
    {
        if (s == GameState.Part1) { Phase = RunStartPhase; fastTo = -1f; }
        if (s == GameState.Iceberg && Sample(Phase).daylight > 0.05f) fastTo = icebergPhase;
    }

    void Update()
    {
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && ENABLE_INPUT_SYSTEM
        // F10: jump to the next part of the day, to check each look without waiting.
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.f10Key.wasPressedThisFrame)
        {
            string[] order = { "Dawn", "Morning", "Noon", "Afternoon", "Dusk", "Night" };
            int i = System.Array.IndexOf(order, PhaseName);
            SetPhase(PhaseOf(order[(i + 1) % order.Length]));
        }
#endif
        float dt = Time.deltaTime;
        if (fastTo >= 0f)
        {
            float remaining = Mathf.Repeat(fastTo - Phase, 1f);
            float step = Mathf.Max(remaining, 0.02f) * dt / catchUpSeconds * 3f;
            if (step >= remaining) { Phase = fastTo; fastTo = -1f; }
            else Phase = Mathf.Repeat(Phase + step, 1f);
        }
        else Phase = Mathf.Repeat(Phase + dt / cycleSeconds, 1f);
    }

    /// <summary>Jump the clock (debug, previews).</summary>
    public void SetPhase(float p) { Phase = Mathf.Repeat(p, 1f); fastTo = -1f; }

    public TimeOfDay Sample() => Sample(Phase);

    public TimeOfDay Sample(float p)
    {
        p = Mathf.Repeat(p, 1f);
        float elevation, yaw;
        if (p < dayShare)
        {
            float d = p / dayShare;
            elevation = sunHighest * Mathf.Sin(Mathf.PI * d);
            yaw = Mathf.Lerp(sunriseYaw, sunsetYaw, d);
        }
        else
        {
            float n = (p - dayShare) / (1f - dayShare);
            elevation = -30f * Mathf.Sin(Mathf.PI * n);
            yaw = Mathf.Lerp(sunsetYaw, sunriseYaw + 360f, n);
        }

        float daylight = Mathf.Clamp01((elevation + 4f) / 18f);
        // Warm light near the horizon, strongest just above it (sunrise, sunset).
        float lowWarm = elevation > -6f ? 1f - Mathf.Clamp01(Mathf.Abs(elevation - 3f) / 22f) : 0f;

        var t = new TimeOfDay
        {
            daylight = daylight,
            sunEuler = new Vector3(Mathf.Max(elevation, 2f), yaw, 0f),
            moonEuler = new Vector3(48f, yaw + 180f, 0f),
            sunColor = Color.Lerp(noonSun, lowSun, lowWarm),
            sunIntensity = 1.7f * Mathf.Clamp01(elevation / 12f),
            moonIntensity = moonBrightness * (1f - daylight),
            ambientSky = Color.Lerp(Color.Lerp(nightSky, daySky, daylight), duskSky, lowWarm * 0.6f),
            ambientEquator = Color.Lerp(Color.Lerp(nightEquator, dayEquator, daylight), duskEquator, lowWarm * 0.6f),
            ambientGround = Color.Lerp(nightGround, dayGround, daylight),
            fogColor = Color.Lerp(Color.Lerp(nightFog, dayFog, daylight), duskFog, lowWarm * 0.55f),
            fogDensity = Mathf.Lerp(0.009f, 0.006f, daylight),
            lampLevel = 1f - Mathf.Clamp01((elevation - 2f) / 14f),
            skyExposure = Mathf.Lerp(nightSkyExposure, 1.3f, daylight),
        };
        return t;
    }

    public string NameOf(float p)
    {
        p = Mathf.Repeat(p, 1f);
        if (p >= dayShare) return "Night";
        float d = p / dayShare;
        return d < 0.12f ? "Dawn" : d < 0.38f ? "Morning" : d < 0.62f ? "Noon" : d < 0.88f ? "Afternoon" : "Dusk";
    }

    /// <summary>Middle of a named part of the day, for previews and debug keys.</summary>
    public float PhaseOf(string name) => name switch
    {
        "Dawn" => dayShare * 0.05f,
        "Morning" => dayShare * 0.25f,
        "Noon" => dayShare * 0.5f,
        "Afternoon" => dayShare * 0.75f,
        "Dusk" => dayShare * 0.95f,
        _ => dayShare + (1f - dayShare) * 0.5f,
    };
}
