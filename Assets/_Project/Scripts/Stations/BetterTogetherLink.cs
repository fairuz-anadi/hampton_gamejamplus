using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// "Better together": whenever 2+ crew hold the same station at once, a pulsing gold arc links them; when that
/// station turns green, a small particle burst plays on it. Self-spawning, gameplay scenes only, like
/// MastSway/MiniMap. Owner: Rabib.
///
/// One LineRenderer + one ParticleSystem per station (4, built once here as a fixed pool — never Instantiated
/// again). Reads only each station's own fresh holders (Station.GetFreshHolders) and IsDone; never IsStoker or
/// any sabotage target, so it's exactly as visible for any pair of crew as for the Stoker. Hidden during
/// GameState.Iceberg.
/// </summary>
public class BetterTogetherLink : MonoBehaviour
{
    [SerializeField] bool effectEnabled = true;
    [SerializeField] Color linkColor = new Color(0.95f, 0.78f, 0.42f);
    [SerializeField] float lineWidth = 0.05f;
    [SerializeField] float arcHeight = 0.55f;
    [SerializeField] int arcSegments = 14;
    [SerializeField] float pulseCycles = 1.1f;
    [SerializeField] Color burstColor = new Color(1f, 0.85f, 0.5f);

    static readonly StationType[] Types = (StationType[])System.Enum.GetValues(typeof(StationType));

    LineRenderer[] lines;
    ParticleSystem[] bursts;
    bool[] wasDone;
    Vector3[] arcPoints;
    readonly ICrewActor[] holderBuffer = new ICrewActor[4];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || FindAnyObjectByType<BetterTogetherLink>() != null || !GameSystems.IsGameplayScene(scene.name)) return;
        new GameObject("[BetterTogetherLink]").AddComponent<BetterTogetherLink>();
    }

    void Awake()
    {
        arcPoints = new Vector3[Mathf.Max(2, arcSegments)];

        var lineMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        var burstMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { color = burstColor };

        lines = new LineRenderer[Types.Length];
        bursts = new ParticleSystem[Types.Length];
        wasDone = new bool[Types.Length];

        for (int i = 0; i < Types.Length; i++)
        {
            var lineGo = new GameObject("Link_" + Types[i]);
            lineGo.transform.SetParent(transform, false);
            var line = lineGo.AddComponent<LineRenderer>();
            line.positionCount = arcPoints.Length;
            line.widthMultiplier = lineWidth;
            line.useWorldSpace = true;
            line.sharedMaterial = lineMat;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            lines[i] = line;

            var burstGo = new GameObject("Burst_" + Types[i]);
            burstGo.transform.SetParent(transform, false);
            var ps = burstGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.6f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = burstGo.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = burstMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bursts[i] = ps;
        }
    }

    void LateUpdate()
    {
        bool hidden = !effectEnabled || (GameManager.Instance != null && GameManager.Instance.State == GameState.Iceberg);

        for (int i = 0; i < Types.Length; i++)
        {
            var station = StationRegistry.Get(Types[i]) as Station;
            if (station == null)
            {
                lines[i].enabled = false;
                continue;
            }

            // Burst once on the false -> true edge, regardless of whether the effect itself is hidden right now,
            // so a station that turns green during the iceberg doesn't silently owe a burst it'll never play.
            bool done = station.IsDone;
            if (done && !wasDone[i] && !hidden) bursts[i].Emit(16);
            wasDone[i] = done;

            if (hidden) { lines[i].enabled = false; continue; }

            int count = station.GetFreshHolders(holderBuffer);
            if (count < 2) { lines[i].enabled = false; continue; }

            DrawArc(lines[i], holderBuffer[0].Position, holderBuffer[1].Position);
        }
    }

    void DrawArc(LineRenderer line, Vector3 a, Vector3 b)
    {
        line.enabled = true;
        int n = arcPoints.Length;
        for (int p = 0; p < n; p++)
        {
            float t = p / (float)(n - 1);
            Vector3 point = Vector3.Lerp(a, b, t);
            point.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
            arcPoints[p] = point;
        }
        line.SetPositions(arcPoints);

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseCycles * Mathf.PI * 2f);
        var c = linkColor; c.a = Mathf.Lerp(0.55f, 1f, pulse);
        line.startColor = line.endColor = c;
        line.widthMultiplier = lineWidth * (1f + 0.25f * pulse);
    }
}
