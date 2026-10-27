using UnityEngine;

/// <summary>
/// Puts the time of day (DayNightCycle) on the deck and lays each story part's mood on top. Owner: Samprity.
///   Calm  : the day as it is.
///   Off   : overcast and cold whatever the hour; dimmer sun, grey haze, cool lamps that start to flicker.
///   Truth : red. Emergency lamps on even in daylight, flickering hard; the sun and haze turn blood red.
/// Lives on the ShipDeck prefab, listens to GameEvents.PartChanged and never drives game logic.
/// </summary>
public class PartMood : MonoBehaviour
{
    [System.Serializable]
    public struct Overlay
    {
        [Tooltip("Multiplies the ambient light.")]
        public Color ambientTint;
        [Tooltip("Multiplies the sun's strength (overcast < 1).")]
        public float sunStrength;
        public Color sunTint;
        public Color fogTint;
        [Range(0f, 1f)] public float fogTintAmount;
        public float extraFog;
        public Color lampColor;
        [Tooltip("Turns lamps on regardless of the time of day (emergency lighting).")]
        [Range(0f, 1f)] public float lampsForcedOn;
        [Range(0f, 1f)] public float flicker;
        [Tooltip("Multiplies the lamps' brightness.")]
        public float lampStrength;
        [Tooltip("Multiplies the sky (0.5 grey = unchanged).")]
        public Color skyTint;
        [Tooltip("How much of the sky the clouds cover.")]
        [Range(0f, 1f)] public float cloudCover;
    }

    public DayNightCycle cycle;
    public Light sun, moon;
    [Tooltip("Room lamps. Station status lights stay out of this list.")]
    public Light[] lamps = new Light[0];
    [Min(0f)] public float lampMaxIntensity = 3.2f;
    [Tooltip("Post-processing volumes for Calm, Off and Truth, crossfaded with the overlays.")]
    public UnityEngine.Rendering.Volume calmVolume, offVolume, truthVolume;
    [Tooltip("Port/Sky (physical scattering, clouds, stars). A runtime copy follows the sun and moon and is tinted per part.")]
    public Material sky;
    [Tooltip("Realtime reflection probes (refresh: via scripting), re-rendered as the sky changes so the sea and brass reflect the hour.")]
    public ReflectionProbe[] probes = new ReflectionProbe[0];
    [Min(0.1f)] public float probeRefreshSeconds = 2f;
    [Range(0f, 1f)] public float nightReflection = 0.2f;
    float nextProbeRefresh;
    Color? moonColor;
    [Min(0f)] public float blendSeconds = 4f;

    public Overlay calm = new Overlay
    {
        ambientTint = Color.white, sunStrength = 1f, sunTint = Color.white,
        fogTint = Color.white, fogTintAmount = 0f, extraFog = 0f,
        lampColor = new Color(1f, 0.8f, 0.55f), lampsForcedOn = 0f, flicker = 0f, lampStrength = 1f, skyTint = new Color(0.5f, 0.5f, 0.5f), cloudCover = 0.35f,
    };
    public Overlay off = new Overlay
    {
        ambientTint = new Color(0.86f, 0.9f, 1f), sunStrength = 0.55f, sunTint = new Color(0.88f, 0.93f, 1f),
        fogTint = new Color(0.6f, 0.65f, 0.7f), fogTintAmount = 0.5f, extraFog = 0.007f,
        lampColor = new Color(0.85f, 0.9f, 1f), lampsForcedOn = 0.25f, flicker = 0.25f, lampStrength = 1f, skyTint = new Color(0.42f, 0.44f, 0.47f), cloudCover = 0.85f,
    };
    public Overlay truth = new Overlay
    {
        ambientTint = new Color(1.75f, 0.85f, 0.76f), sunStrength = 0.8f, sunTint = new Color(1f, 0.55f, 0.45f),
        fogTint = new Color(0.5f, 0.2f, 0.16f), fogTintAmount = 0.5f, extraFog = 0.01f,
        lampColor = new Color(1f, 0.24f, 0.17f), lampsForcedOn = 1f, flicker = 0.55f, lampStrength = 1.5f, skyTint = new Color(0.55f, 0.26f, 0.22f), cloudCover = 0.6f,
    };

    Overlay from, to, current;
    float blend = 1f;
    Material skyCopy;
    readonly float[] weightsFrom = { 1f, 0f, 0f }, weightsTo = { 1f, 0f, 0f }, weights = { 1f, 0f, 0f };

    void OnEnable() => GameEvents.PartChanged += OnPartChanged;
    void OnDisable() => GameEvents.PartChanged -= OnPartChanged;

    void Awake() => SetPart(1);

    void OnPartChanged(int part)
    {
        from = current;
        to = For(part);
        System.Array.Copy(weights, weightsFrom, 3);
        SetOneHot(weightsTo, part);
        blend = blendSeconds > 0f ? 0f : 1f;
    }

    public Overlay For(int part) => part switch { 2 => off, >= 3 => truth, _ => calm };

    static void SetOneHot(float[] w, int part)
    {
        int i = part switch { 2 => 1, >= 3 => 2, _ => 0 };
        w[0] = w[1] = w[2] = 0f;
        w[i] = 1f;
    }

    void SetPart(int part)
    {
        current = to = from = For(part);
        SetOneHot(weightsTo, part);
        System.Array.Copy(weightsTo, weightsFrom, 3);
        System.Array.Copy(weightsTo, weights, 3);
        blend = 1f;
    }

    void Update()
    {
        if (blend < 1f) blend = Mathf.Min(1f, blend + Time.deltaTime / blendSeconds);
        float t = Mathf.SmoothStep(0f, 1f, blend);
        current = Lerp(from, to, t);
        for (int i = 0; i < 3; i++) weights[i] = Mathf.Lerp(weightsFrom[i], weightsTo[i], t);
        Apply(cycle != null ? cycle.Sample() : Noon(), current, Time.time, Application.isPlaying);
    }

    /// <summary>
    /// Shows a story part at the clock's run-start time, straight away. The scene builder calls this in edit mode,
    /// so the saved scene keeps the sky asset itself.
    /// </summary>
    public void ApplyImmediate(int part)
    {
        SetPart(part);
        var tod = cycle != null ? cycle.Sample(cycle.RunStartPhase) : Noon();
        Apply(tod, current, 0f, false);
    }

    /// <summary>Editor previews: a story part at a chosen point of the day, with the sky tinted (not saved).</summary>
    public void Preview(int part, float phase)
    {
        SetPart(part);
        Apply(cycle != null ? cycle.Sample(phase) : Noon(), current, 0f, true);
    }

    DayNightCycle.TimeOfDay Noon() => new DayNightCycle.TimeOfDay
    {
        sunEuler = new Vector3(55f, 35f, 0f), sunColor = Color.white, sunIntensity = 1.6f,
        ambientSky = new Color(0.6f, 0.7f, 0.85f), ambientEquator = new Color(0.6f, 0.6f, 0.6f), ambientGround = new Color(0.35f, 0.32f, 0.3f),
        fogColor = new Color(0.74f, 0.83f, 0.92f), fogDensity = 0.006f, skyExposure = 1.3f, daylight = 1f,
    };

    void Apply(DayNightCycle.TimeOfDay d, Overlay o, float time, bool tintSky)
    {
        if (calmVolume != null) calmVolume.weight = weights[0];
        if (offVolume != null) offVolume.weight = weights[1];
        if (truthVolume != null) truthVolume.weight = weights[2];

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = d.ambientSky * o.ambientTint;
        RenderSettings.ambientEquatorColor = d.ambientEquator * o.ambientTint;
        RenderSettings.ambientGroundColor = d.ambientGround * o.ambientTint;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        var fog = Color.Lerp(d.fogColor, d.fogColor * o.fogTint * 1.6f, o.fogTintAmount);
        RenderSettings.fogColor = fog;
        RenderSettings.fogDensity = d.fogDensity + o.extraFog;
        // Scene reflections not covered by a fresh probe come from a cubemap of the daytime sky; dim them at night so the
        // sea at the horizon and the brass don't shine with daylight.
        RenderSettings.reflectionIntensity = Mathf.Lerp(nightReflection, 1f, d.daylight);

        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(d.sunEuler);
            sun.color = d.sunColor * o.sunTint;
            sun.intensity = d.sunIntensity * o.sunStrength;
            sun.enabled = sun.intensity > 0.01f;
        }
        if (moon != null)
        {
            moon.transform.rotation = Quaternion.Euler(d.moonEuler);
            // The moon takes the part's tint too: grey-blue when things are Off, a blood moon for the Truth.
            if (moonColor == null) moonColor = moon.color;
            moon.color = moonColor.Value * o.sunTint;
            moon.intensity = d.moonIntensity;
            moon.enabled = moon.intensity > 0.01f;
        }
        // URP's main (shadow-casting) light: the sun by day, the moon by night.
        var main = sun != null && moon != null ? (sun.intensity >= moon.intensity ? sun : moon) : (sun != null ? sun : moon);
        if (main != null) RenderSettings.sun = main;

        if (sky != null)
        {
            if (tintSky)
            {
                if (skyCopy == null) skyCopy = new Material(sky) { hideFlags = HideFlags.DontSave };
                // The sky scatters from the sun's true height (the light never dips below 2 degrees), so twilight
                // glows after sunset and the night goes dark by itself.
                skyCopy.SetVector("_SunDir", Quaternion.Euler(d.sunElevation, d.sunYaw, 0f) * Vector3.back);
                skyCopy.SetFloat("_SunStrength", o.sunStrength);
                if (moon != null) skyCopy.SetVector("_MoonDir", -moon.transform.forward);
                skyCopy.SetFloat("_MoonStrength", 1f - d.daylight);
                skyCopy.SetFloat("_Stars", Mathf.SmoothStep(0f, 1f, (0.6f - d.daylight) / 0.6f));
                skyCopy.SetColor("_HorizonColor", fog);
                skyCopy.SetColor("_SkyTint", o.skyTint * 2f);
                skyCopy.SetFloat("_CloudCover", o.cloudCover);
                RenderSettings.skybox = skyCopy;
            }
            else RenderSettings.skybox = sky;
        }

        // Probes keep whatever sky they last saw, so re-render them as it changes. Time-sliced, so a refresh costs a
        // face or two per frame; editor previews refresh every call.
        if (tintSky && (!Application.isPlaying || time >= nextProbeRefresh))
        {
            nextProbeRefresh = time + probeRefreshSeconds;
            foreach (var probe in probes)
            {
                if (probe == null) continue;
                // Edit-mode previews render one frame, so a time-sliced refresh would never finish.
                if (!Application.isPlaying) probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                probe.RenderProbe();
            }
        }

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = sky != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = fog;
        }

        float lampLevel = Mathf.Max(d.lampLevel, o.lampsForcedOn);
        for (int i = 0; i < lamps.Length; i++)
        {
            var l = lamps[i];
            if (l == null) continue;
            float f = 1f;
            if (o.flicker > 0f)
            {
                // Each lamp flickers on its own; a deep dip now and then sells "the power is failing".
                float n = Mathf.PerlinNoise(time * 7f, i * 3.7f);
                f = Mathf.Lerp(1f, n < 0.25f ? 0.15f : 0.7f + 0.6f * n, o.flicker);
            }
            l.color = o.lampColor;
            l.intensity = lampMaxIntensity * o.lampStrength * lampLevel * f;
            l.enabled = l.intensity > 0.01f;
        }
    }

    static Overlay Lerp(Overlay a, Overlay b, float t) => new Overlay
    {
        ambientTint = Color.Lerp(a.ambientTint, b.ambientTint, t),
        sunStrength = Mathf.Lerp(a.sunStrength, b.sunStrength, t),
        sunTint = Color.Lerp(a.sunTint, b.sunTint, t),
        fogTint = Color.Lerp(a.fogTint, b.fogTint, t),
        fogTintAmount = Mathf.Lerp(a.fogTintAmount, b.fogTintAmount, t),
        extraFog = Mathf.Lerp(a.extraFog, b.extraFog, t),
        lampColor = Color.Lerp(a.lampColor, b.lampColor, t),
        lampsForcedOn = Mathf.Lerp(a.lampsForcedOn, b.lampsForcedOn, t),
        flicker = Mathf.Lerp(a.flicker, b.flicker, t),
        lampStrength = Mathf.Lerp(a.lampStrength, b.lampStrength, t),
        skyTint = Color.Lerp(a.skyTint, b.skyTint, t),
        cloudCover = Mathf.Lerp(a.cloudCover, b.cloudCover, t),
    };
}
