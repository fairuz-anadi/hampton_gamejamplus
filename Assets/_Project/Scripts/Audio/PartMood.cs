using UnityEngine;

/// <summary>
/// The deck's lighting mood per story part: warm (Calm) -> cold (Off) -> red and flickering (Truth). Owner: Samprity.
/// Lives on the ShipDeck prefab, so dropping the prefab into a scene brings the look with it.
/// Listens to GameEvents.PartChanged and never drives game logic.
/// Post-processing volumes and mixer snapshots per part hook in here later.
/// </summary>
public class PartMood : MonoBehaviour
{
    [System.Serializable]
    public struct Mood
    {
        public Color ambientSky, ambientEquator, ambientGround;
        public Color fogColor;
        [Range(0f, 0.1f)] public float fogDensity;
        public Color lampColor;
        public float lampIntensity;
        public Color moonColor;
        public float moonIntensity;
        [Tooltip("0 = steady lamps, 1 = heavy flicker.")]
        [Range(0f, 1f)] public float flicker;
    }

    public Light moon;
    [Tooltip("Room lamps that change colour per part. Station status lights stay out of this list.")]
    public Light[] lamps = new Light[0];
    [Tooltip("Post-processing volumes for Calm, Off and Truth, crossfaded with the lights.")]
    public UnityEngine.Rendering.Volume calmVolume, offVolume, truthVolume;
    [Tooltip("Night sky (Skybox/Panoramic). Its tint follows the fog colour so the horizon matches.")]
    public Material sky;
    [Min(0f)] public float blendSeconds = 4f;

    public Mood calm = new Mood
    {
        ambientSky = new Color(0.42f, 0.42f, 0.50f), ambientEquator = new Color(0.32f, 0.28f, 0.25f), ambientGround = new Color(0.12f, 0.10f, 0.09f),
        fogColor = new Color(0.10f, 0.12f, 0.20f), fogDensity = 0.012f,
        lampColor = new Color(1f, 0.76f, 0.48f), lampIntensity = 3.4f,
        moonColor = new Color(0.62f, 0.70f, 1f), moonIntensity = 0.7f, flicker = 0f,
    };
    public Mood off = new Mood
    {
        ambientSky = new Color(0.30f, 0.36f, 0.50f), ambientEquator = new Color(0.20f, 0.22f, 0.30f), ambientGround = new Color(0.08f, 0.09f, 0.12f),
        fogColor = new Color(0.07f, 0.10f, 0.18f), fogDensity = 0.022f,
        lampColor = new Color(0.82f, 0.88f, 1f), lampIntensity = 2.8f,
        moonColor = new Color(0.55f, 0.70f, 1f), moonIntensity = 0.85f, flicker = 0.25f,
    };
    public Mood truth = new Mood
    {
        ambientSky = new Color(0.26f, 0.10f, 0.11f), ambientEquator = new Color(0.17f, 0.07f, 0.07f), ambientGround = new Color(0.05f, 0.02f, 0.02f),
        fogColor = new Color(0.08f, 0.03f, 0.04f), fogDensity = 0.03f,
        lampColor = new Color(1f, 0.15f, 0.12f), lampIntensity = 3.6f,
        moonColor = new Color(0.45f, 0.55f, 0.85f), moonIntensity = 0.3f, flicker = 0.7f,
    };

    Mood from, to, current;
    float blend = 1f;
    Material skyCopy;
    readonly float[] weightsFrom = { 1f, 0f, 0f }, weightsTo = { 1f, 0f, 0f }, weights = { 1f, 0f, 0f };

    void OnEnable() => GameEvents.PartChanged += OnPartChanged;
    void OnDisable() => GameEvents.PartChanged -= OnPartChanged;

    void Awake()
    {
        ApplyImmediate(1);
    }

    void OnPartChanged(int part)
    {
        from = current;
        to = For(part);
        System.Array.Copy(weights, weightsFrom, 3);
        SetOneHot(weightsTo, part);
        blend = blendSeconds > 0f ? 0f : 1f;
    }

    public Mood For(int part) => part switch { 2 => off, >= 3 => truth, _ => calm };

    static void SetOneHot(float[] w, int part)
    {
        int i = part switch { 2 => 1, >= 3 => 2, _ => 0 };
        w[0] = w[1] = w[2] = 0f;
        w[i] = 1f;
    }

    void Update()
    {
        if (blend < 1f) blend = Mathf.Min(1f, blend + Time.deltaTime / blendSeconds);
        float t = Mathf.SmoothStep(0f, 1f, blend);
        current = Lerp(from, to, t);
        for (int i = 0; i < 3; i++) weights[i] = Mathf.Lerp(weightsFrom[i], weightsTo[i], t);
        Apply(current, Time.time);
    }

    /// <summary>Sets lights, ambient, fog and post-processing straight away. The scene builder calls this in edit mode.</summary>
    public void ApplyImmediate(int part)
    {
        current = to = from = For(part);
        SetOneHot(weightsTo, part);
        System.Array.Copy(weightsTo, weightsFrom, 3);
        System.Array.Copy(weightsTo, weights, 3);
        blend = 1f;
        Apply(current, 0f);
    }

    void Apply(Mood m, float time)
    {
        if (calmVolume != null) calmVolume.weight = weights[0];
        if (offVolume != null) offVolume.weight = weights[1];
        if (truthVolume != null) truthVolume.weight = weights[2];
        if (sky != null)
        {
            // In play mode tint a copy, so the sky asset on disk doesn't change while playing.
            if (Application.isPlaying && skyCopy == null) skyCopy = new Material(sky);
            var s = Application.isPlaying ? skyCopy : sky;
            RenderSettings.skybox = s;
            s.SetColor("_Tint", Color.Lerp(m.fogColor * 4f, Color.white * 0.5f, 0.5f));
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = m.ambientSky;
        RenderSettings.ambientEquatorColor = m.ambientEquator;
        RenderSettings.ambientGroundColor = m.ambientGround;
        RenderSettings.fog = m.fogDensity > 0f;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = m.fogColor;
        RenderSettings.fogDensity = m.fogDensity;

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = sky != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = m.fogColor;
        }

        if (moon != null) { moon.color = m.moonColor; moon.intensity = m.moonIntensity; }

        for (int i = 0; i < lamps.Length; i++)
        {
            var l = lamps[i];
            if (l == null) continue;
            float f = 1f;
            if (m.flicker > 0f)
            {
                // Each lamp flickers on its own; a deep dip now and then sells "the power is failing".
                float n = Mathf.PerlinNoise(time * 7f, i * 3.7f);
                f = Mathf.Lerp(1f, n < 0.25f ? 0.15f : 0.7f + 0.6f * n, m.flicker);
            }
            l.color = m.lampColor;
            l.intensity = m.lampIntensity * f;
        }
    }

    static Mood Lerp(Mood a, Mood b, float t) => new Mood
    {
        ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, t),
        ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, t),
        ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, t),
        fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
        fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t),
        lampColor = Color.Lerp(a.lampColor, b.lampColor, t),
        lampIntensity = Mathf.Lerp(a.lampIntensity, b.lampIntensity, t),
        moonColor = Color.Lerp(a.moonColor, b.moonColor, t),
        moonIntensity = Mathf.Lerp(a.moonIntensity, b.moonIntensity, t),
        flicker = Mathf.Lerp(a.flicker, b.flicker, t),
    };
}
