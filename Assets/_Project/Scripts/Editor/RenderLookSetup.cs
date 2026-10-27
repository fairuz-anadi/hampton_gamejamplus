using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The game's render look: URP quality settings, one post-processing profile per story part, the procedural sky and the
/// sea's ripple texture. Owner: Samprity. Safe to run again; it overwrites what it made.
/// Called by the ship deck builder; also on its own via Port > Samprity > Apply Render Look.
/// </summary>
public static class RenderLookSetup
{
    const string PcAsset = "Assets/_Project/Settings/URP/PC_RPAsset.asset";
    public const string FxFolder = "Assets/_Project/Art/PostFX";
    public const string SkyMaterialPath = FxFolder + "/Sky.mat";
    public const string SeaNormalPath = "Assets/_Project/Art/Textures/SeaRipples_Normal.png";
    public static string ProfilePath(string part) => $"{FxFolder}/Mood_{part}.asset";

    [MenuItem("Port/Samprity/Apply Render Look")]
    public static void Apply()
    {
        MovementPrototypeBuilder.EnsureFolder(FxFolder);
        MovementPrototypeBuilder.EnsureFolder("Assets/_Project/Art/Textures");
        TuneUrpAsset();
        BuildProfile("Calm", new Color(1f, 0.97f, 0.92f), 6f, 0.15f, 0f, 0.12f, 0f);
        BuildProfile("Off", new Color(0.9f, 0.94f, 1f), -18f, 0.2f, -25f, 0.2f, 0.05f);
        BuildProfile("Truth", new Color(1f, 0.82f, 0.76f), 20f, 1.0f, -10f, 0.28f, 0.25f);
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

        // Stronger, wider ambient occlusion so props sit on the deck (contact shadows) and corners darken softly.
        var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/_Project/Settings/URP/PC_Renderer.asset");
        if (renderer != null)
            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature == null || feature.name != "ScreenSpaceAmbientOcclusion") continue;
                var fso = new SerializedObject(feature);
                fso.FindProperty("m_Settings.Intensity").floatValue = 0.95f;
                fso.FindProperty("m_Settings.Radius").floatValue = 0.45f;
                fso.FindProperty("m_Settings.DirectLightingStrength").floatValue = 0.35f;
                fso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(feature);
            }

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
        // Lamps, the furnace and status lights glow; nothing else should.
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(0.7f);
        bloom.scatter.Override(0.7f);
        var color = Add<ColorAdjustments>();
        color.postExposure.Override(exposure);
        color.contrast.Override(7f);
        // Painterly colour: cool shadows, warm lights, like the light and shade on an illustration.
        var split = Add<SplitToning>();
        split.shadows.Override(new Color(0.46f, 0.53f, 0.68f));
        split.highlights.Override(new Color(0.78f, 0.68f, 0.56f));
        split.balance.Override(-15f);
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

    /// <summary>
    /// The physical sky (Port/Sky): scattering, clouds and stars, saved with noon values. PartMood drives a runtime copy
    /// from the clock (sun and moon) and the story part (tint, cloud cover).
    /// </summary>
    static void BuildSky()
    {
        var shader = Shader.Find("Port/Sky");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, SkyMaterialPath); }
        mat.shader = shader;
        mat.SetVector("_SunDir", (Quaternion.Euler(55f, 35f, 0f) * Vector3.back));
        mat.SetFloat("_SunStrength", 1f);
        mat.SetFloat("_MoonStrength", 0f);
        mat.SetFloat("_Exposure", 1.2f);
        mat.SetFloat("_Turbidity", 0.8f);
        mat.SetFloat("_MieG", 0.8f);
        mat.SetColor("_SkyTint", Color.white);
        mat.SetColor("_HorizonColor", new Color(0.74f, 0.83f, 0.92f));
        mat.SetFloat("_HazeAmount", 0.65f);
        mat.SetFloat("_Stars", 0f);
        mat.SetTexture("_NoiseTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/PainterlyNoise.png"));
        mat.SetFloat("_CloudCover", 0.35f);
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
