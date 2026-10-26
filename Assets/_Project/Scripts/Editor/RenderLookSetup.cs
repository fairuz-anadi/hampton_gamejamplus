using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The game's render look: URP quality settings, one post-processing profile per story part, the night sky and the
/// sea's ripple texture. Owner: Samprity. Safe to run again; it overwrites what it made.
/// Called by the ship deck builder; also on its own via Port > Samprity > Apply Render Look.
/// </summary>
public static class RenderLookSetup
{
    const string PcAsset = "Assets/_Project/Settings/URP/PC_RPAsset.asset";
    public const string FxFolder = "Assets/_Project/Art/PostFX";
    public const string SkyMaterialPath = FxFolder + "/NightSky.mat";
    public const string SeaNormalPath = "Assets/_Project/Art/Textures/SeaRipples_Normal.png";
    public static string ProfilePath(string part) => $"{FxFolder}/Mood_{part}.asset";

    [MenuItem("Port/Samprity/Apply Render Look")]
    public static void Apply()
    {
        MovementPrototypeBuilder.EnsureFolder(FxFolder);
        MovementPrototypeBuilder.EnsureFolder("Assets/_Project/Art/Textures");
        TuneUrpAsset();
        BuildProfile("Calm", new Color(1f, 0.94f, 0.86f), 12f, 1.0f, 5f, 0.2f, 0f);
        BuildProfile("Off", new Color(0.86f, 0.92f, 1f), -16f, 0.85f, -10f, 0.27f, 0.05f);
        BuildProfile("Truth", new Color(1f, 0.75f, 0.72f), 0f, 0.7f, -15f, 0.38f, 0.25f);
        BuildSky();
        BuildSeaNormal();
        AssetDatabase.SaveAssets();
        Debug.Log("[RenderLook] Applied");
    }

    /// <summary>Anti-aliasing, HDR grading, more lights per object and crisper shadows on the PC quality level.</summary>
    static void TuneUrpAsset()
    {
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PcAsset);
        if (asset == null) { Debug.LogWarning("[RenderLook] PC_RPAsset not found"); return; }
        var so = new SerializedObject(asset);
        void Set(string prop, int v) { var p = so.FindProperty(prop); if (p != null) p.intValue = v; else Debug.LogWarning("[RenderLook] no " + prop); }
        void SetF(string prop, float v) { var p = so.FindProperty(prop); if (p != null) p.floatValue = v; else Debug.LogWarning("[RenderLook] no " + prop); }
        Set("m_MSAA", 4);
        Set("m_ColorGradingMode", 1);                  // HDR grading, needed for ACES
        Set("m_AdditionalLightsPerObjectLimit", 8);
        Set("m_MainLightShadowmapResolution", 4096);
        Set("m_AdditionalLightsShadowmapResolution", 4096);
        SetF("m_ShadowDistance", 45f);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);

        // The deck's reflection probe refreshes at runtime; the PC level had real-time probes off.
        var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var levels = quality.FindProperty("m_QualitySettings");
        for (int i = 0; i < levels.arraySize; i++)
        {
            var level = levels.GetArrayElementAtIndex(i);
            if (level.FindPropertyRelative("name").stringValue == "PC")
                level.FindPropertyRelative("realtimeReflectionProbes").boolValue = true;
        }
        quality.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildProfile(string part, Color filter, float temperature, float exposure, float saturation, float vignette, float aberration)
    {
        // Reuse the asset so its GUID (and every Volume pointing at it) survives a re-run.
        string path = ProfilePath(part);
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }
        foreach (var old in profile.components.ToArray())
        {
            AssetDatabase.RemoveObjectFromAsset(old);
            Object.DestroyImmediate(old, true);
        }
        profile.components.Clear();

        T Add<T>() where T : VolumeComponent
        {
            var c = profile.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
        var bloom = Add<Bloom>();
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(0.7f);
        bloom.scatter.Override(0.65f);
        var color = Add<ColorAdjustments>();
        color.postExposure.Override(exposure);
        color.contrast.Override(14f);
        color.saturation.Override(saturation);
        color.colorFilter.Override(filter);
        Add<WhiteBalance>().temperature.Override(temperature);
        var vig = Add<Vignette>();
        vig.intensity.Override(vignette);
        vig.smoothness.Override(0.45f);
        var grain = Add<FilmGrain>();
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.18f);
        if (aberration > 0f) Add<ChromaticAberration>().intensity.Override(aberration);
        EditorUtility.SetDirty(profile);
    }

    /// <summary>Equirectangular night sky: navy gradient, stars, a moon. Fog doesn't touch the skybox, so its horizon matches the fog.</summary>
    static void BuildSky()
    {
        const int W = 2048, H = 1024;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        var px = new Color[W * H];
        var horizon = new Color(0.10f, 0.12f, 0.20f);
        var zenith = new Color(0.015f, 0.02f, 0.05f);
        // Moon opposite the moon light's travel direction (light rotation 42, 35).
        Vector3 moonDir = -(Quaternion.Euler(42f, 35f, 0f) * Vector3.forward);
        var rng = new System.Random(1912);
        for (int y = 0; y < H; y++)
        {
            float lat = (y + 0.5f) / H * Mathf.PI - Mathf.PI / 2f;
            float up = Mathf.Sin(lat);
            for (int x = 0; x < W; x++)
            {
                float lon = (x + 0.5f) / W * Mathf.PI * 2f - Mathf.PI;
                var dir = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), up, Mathf.Cos(lat) * Mathf.Cos(lon));
                Color c = up <= 0f ? horizon * 0.8f : Color.Lerp(horizon, zenith, Mathf.Pow(up, 0.45f));
                // Stars, more of them higher up, none in the haze near the horizon.
                double r = rng.NextDouble();
                if (up > 0.08f && r < 0.0016 * up)
                {
                    float b = 0.35f + (float)rng.NextDouble() * 0.65f;
                    c += new Color(b, b, b * 1.05f) * Mathf.Clamp01((up - 0.08f) * 4f);
                }
                float moon = Vector3.Dot(dir, moonDir);
                if (moon > 0.9993f) c = new Color(0.95f, 0.93f, 0.85f);
                else if (moon > 0.985f) c += new Color(0.25f, 0.27f, 0.32f) * Mathf.Pow((moon - 0.985f) / 0.0143f, 3f);
                px[y * W + x] = c;
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        string texPath = FxFolder + "/NightSky.png";
        System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);
        var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
        ti.wrapModeU = TextureWrapMode.Repeat;
        ti.wrapModeV = TextureWrapMode.Clamp;
        ti.mipmapEnabled = false;
        ti.maxTextureSize = 2048;
        ti.SaveAndReimport();

        var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        if (mat == null) { mat = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(mat, SkyMaterialPath); }
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        mat.SetFloat("_Mapping", 1f); // latitude-longitude layout
        mat.SetFloat("_Exposure", 1f);
        mat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
        EditorUtility.SetDirty(mat);
    }

    /// <summary>Tileable ripple normal map for the sea, from a few layered sine waves.</summary>
    static void BuildSeaNormal()
    {
        const int N = 256;
        var h = new float[N * N];
        var rng = new System.Random(4);
        var waves = new (float kx, float ky, float a, float p)[10];
        for (int i = 0; i < waves.Length; i++)
            waves[i] = (rng.Next(1, 9) * (rng.Next(2) == 0 ? 1 : -1), rng.Next(1, 9) * (rng.Next(2) == 0 ? 1 : -1), 1f / (1 + i * 0.6f), (float)rng.NextDouble() * 6.28f);
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float u = x / (float)N * Mathf.PI * 2f, v = y / (float)N * Mathf.PI * 2f, s = 0f;
            foreach (var w in waves) s += w.a * Mathf.Sin(w.kx * u + w.ky * v + w.p);
            h[y * N + x] = s;
        }
        var tex = new Texture2D(N, N, TextureFormat.RGB24, false);
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float dx = h[y * N + (x + 1) % N] - h[y * N + (x + N - 1) % N];
            float dy = h[((y + 1) % N) * N + x] - h[((y + N - 1) % N) * N + x];
            var n = new Vector3(-dx * 0.6f, -dy * 0.6f, 1f).normalized;
            tex.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f));
        }
        tex.Apply();
        System.IO.File.WriteAllBytes(SeaNormalPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(SeaNormalPath);
        var ti = (TextureImporter)AssetImporter.GetAtPath(SeaNormalPath);
        ti.textureType = TextureImporterType.NormalMap;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.SaveAndReimport();
    }
}
