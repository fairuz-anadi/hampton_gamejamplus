using UnityEngine;

/// <summary>
/// Grey Cove, there in the world (Anadi, 28 Oct): from Part 2 the dark cliffs of a bay show through the haze off the
/// starboard bow, to the north, and creep closer as the night goes on; in Part 3 the wreckers' lanterns are lit at the
/// mouth of the bay, blinking. The ship never moves (the sea scrolls past it), so the cove is what moves. No colliders.
/// Built from primitives with the RuntimeLit material so it survives builds. Spawned by GameSystems in Ship.
/// Owner: Anadi.
/// </summary>
public class GreyCoveOnHorizon : MonoBehaviour
{
    [Tooltip("Bearing from the bow, degrees clockwise: north of our heading is to starboard.")]
    public float bearing = 48f;
    [Tooltip("Distance when it first appears (Part 2) and by the iceberg, in metres.")]
    public float farDistance = 430f, nearDistance = 240f;
    [Tooltip("Seconds over which it closes in, from the start of Part 2.")]
    public float closeInSeconds = 300f;

    Transform root, ship;
    Light[] lanterns;
    UnityEngine.UI.RawImage[] lanternGlows;
    Texture2D glowTex;
    float part2At = -1f;

    void Start()
    {
        var mood = FindAnyObjectByType<PartMood>();
        ship = mood != null ? mood.transform : null;
        Build();
        root.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (glowTex != null) Destroy(glowTex);
        if (root != null) Destroy(root.gameObject);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || root == null) return;
        int part = gm.Part;
        bool show = part >= 2 || gm.State >= GameState.Iceberg;
        if (show && part2At < 0f) part2At = Time.time;
        if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
        if (!show) return;

        float k = Mathf.Clamp01((Time.time - part2At) / closeInSeconds);
        float dist = Mathf.Lerp(farDistance, nearDistance, k * k * (3f - 2f * k));
        Vector3 origin = ship != null ? ship.position : Vector3.zero;
        Quaternion heading = ship != null ? ship.rotation : Quaternion.identity;
        Vector3 dir = heading * (Quaternion.Euler(0f, bearing, 0f) * Vector3.forward);
        root.position = origin + dir * dist + Vector3.down * 2f;
        root.rotation = Quaternion.LookRotation(dir, Vector3.up); // local +z runs away from the ship: the bay opens toward us

        bool lit = part >= 3 || gm.State >= GameState.Iceberg;
        for (int i = 0; i < lanterns.Length; i++)
        {
            float blink = Mathf.PerlinNoise(Time.time * 0.9f, i * 3.7f);
            bool on = lit && blink > 0.35f;
            lanterns[i].enabled = on;
            lanternGlows[i].enabled = on;
            // The glow always faces the camera.
            var cam = Camera.main;
            if (cam != null) lanternGlows[i].transform.parent.rotation = Quaternion.LookRotation(lanternGlows[i].transform.parent.position - cam.transform.position);
        }
    }

    void Build()
    {
        root = new GameObject("[GreyCove]").transform;
        var lit = Resources.Load<Material>("RuntimeLit");
        Material Mat(Color c)
        {
            var m = lit != null ? new Material(lit) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            return m;
        }
        var rock = Mat(new Color(0.11f, 0.12f, 0.15f));
        var rockLight = Mat(new Color(0.16f, 0.17f, 0.2f));
        var snow = Mat(new Color(0.78f, 0.82f, 0.86f));

        // Two headlands curving round a bay whose mouth faces the ship: piles of tilted, overlapping rocks that read
        // as one jagged ridge through the haze, highest at the points.
        var rng = new System.Random(19);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        foreach (float side in new[] { -1f, 1f })
        {
            for (int i = 0; i < 16; i++)
            {
                float t = i / 15f;
                float ang = Mathf.Lerp(18f, 115f, t) * side * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Sin(ang) * 75f, 0f, Mathf.Cos(ang) * 75f + 45f) + new Vector3(R(-5f, 5f), 0f, R(-5f, 5f));
                float h = Mathf.Lerp(12f, 30f, Mathf.Sin(t * Mathf.PI * 0.8f)) + R(-4f, 6f);
                // A rock: a squat block tipped over at a slant, half sunk in the sea.
                Block(root, p + Vector3.up * h * 0.3f, new Vector3(R(10f, 18f), h, R(9f, 16f)), new Vector3(R(-25f, 25f), R(0f, 360f), R(-30f, 30f)), i % 3 == 0 ? rockLight : rock);
                if (i % 2 == 0) Block(root, p + new Vector3(R(-6f, 6f), h * 0.55f, R(-6f, 6f)), new Vector3(R(5f, 9f), h * 0.6f, R(5f, 9f)), new Vector3(R(-35f, 35f), R(0f, 360f), R(-35f, 35f)), rock);
                if (h > 28f) Block(root, p + Vector3.up * (h * 0.75f), new Vector3(R(6f, 10f), 2.5f, R(6f, 10f)), new Vector3(R(-15f, 15f), R(0f, 360f), R(-15f, 15f)), snow);
            }
        }
        // The back of the bay, low and dark.
        for (int i = 0; i < 9; i++)
            Block(root, new Vector3(R(-60f, 60f), 4f, 125f + R(-10f, 10f)), new Vector3(R(18f, 30f), 14f + R(-3f, 6f), 14f), new Vector3(R(-12f, 12f), R(-30f, 30f), R(-15f, 15f)), rock);

        // The wreckers' lanterns at the mouth of the bay: a soft glow drawn as a world-space sprite (UI shaders ignore
        // fog, so they shine through the night haze like real lamps at sea), plus a small light on the rocks.
        glowTex = GlowTexture();
        lanterns = new Light[5];
        lanternGlows = new UnityEngine.UI.RawImage[5];
        for (int i = 0; i < 5; i++)
        {
            var holder = new GameObject("Lantern", typeof(RectTransform), typeof(Canvas));
            holder.transform.SetParent(root, false);
            holder.transform.localPosition = new Vector3(-36f + i * 18f + R(-4f, 4f), 4f + R(0f, 9f), 30f + R(-6f, 6f));
            var canvas = holder.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var hr = (RectTransform)holder.transform;
            hr.sizeDelta = new Vector2(12f, 12f);
            var img = new GameObject("Glow", typeof(RectTransform), typeof(UnityEngine.UI.RawImage)).GetComponent<UnityEngine.UI.RawImage>();
            img.transform.SetParent(holder.transform, false);
            img.texture = glowTex;
            img.color = new Color(1f, 0.78f, 0.42f);
            img.raycastTarget = false;
            img.rectTransform.sizeDelta = new Vector2(12f, 12f);
            lanternGlows[i] = img;
            var light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.75f, 0.4f);
            light.range = 22f;
            light.intensity = 30f;
            light.shadows = LightShadows.None;
            lanterns[i] = light;
        }
    }

    static Texture2D GlowTexture()
    {
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01(Mathf.Exp(-d * d * 9f) * 1.4f + Mathf.Clamp01(1f - d * 4f));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        t.SetPixels32(px); t.Apply();
        return t;
    }

    static void Block(Transform parent, Vector3 pos, Vector3 size, Vector3 euler, Material m)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(b.GetComponent<Collider>());
        b.name = "Cliff";
        b.transform.SetParent(parent, false);
        b.transform.localPosition = pos;
        b.transform.localRotation = Quaternion.Euler(euler);
        b.transform.localScale = size;
        b.GetComponent<Renderer>().sharedMaterial = m;
    }
}
