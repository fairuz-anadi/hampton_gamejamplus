using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The game's material style guide, in code. Owner: Samprity.
/// Moves the ship, KayKit and crew materials onto Port/Stylized (painterly variation + soft toon light) with one
/// preset per material role, so every surface shares one visual language, and makes the outline materials.
///   In the Editor:  Port > Samprity > Apply Stylized Look   (the deck and crew builders also call it)
/// Original FBX materials are never edited: KayKit pieces get stylized copies in Art/Materials/Stylized.
/// </summary>
public static class StylizedLook
{
    const string NoisePath = "Assets/_Project/Art/Textures/PainterlyNoise.png";
    const string StylizedFolder = "Assets/_Project/Art/Materials/Stylized";
    public const string CrewOutlinePath = StylizedFolder + "/Outline_Crew.mat";
    public const string InteractOutlinePath = StylizedFolder + "/Outline_Interact.mat";
    const string KayKit = "Assets/_Project/Art/ThirdParty/KayKit";

    static Texture2D noise;
    static Shader stylized, outline;

    public enum Role { PaintedIron, Stone, Wood, Brass, Iron, Coal, Paint, Cloth, Skin, Hair, Prop }

    [MenuItem("Port/Samprity/Apply Stylized Look")]
    public static void Apply()
    {
        Load();
        MovementPrototypeBuilder.EnsureFolder(StylizedFolder);

        // Ship materials made by the deck builder: converted in place.
        var ship = new Dictionary<string, Role>
        {
            ["Ship_WallNavy"] = Role.PaintedIron, ["Ship_FloorSoot"] = Role.Stone, ["Ship_Brass"] = Role.Brass,
            ["Ship_Coal"] = Role.Coal, ["Ship_Cream"] = Role.Paint, ["Ship_DarkWood"] = Role.Wood,
            ["Ship_Steel"] = Role.Iron, ["Ship_Hull"] = Role.PaintedIron, ["Ship_LifeboatWhite"] = Role.Paint,
            ["Ship_RedPaint"] = Role.Paint, ["Ship_FunnelBuff"] = Role.Paint,
        };
        foreach (var kv in ship)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Project/Art/Materials/Ship/{kv.Key}.mat");
            if (m != null) Style(m, kv.Value);
        }

        // KayKit atlases: one stylized copy per atlas and role.
        KayKitMaterial("Dungeon/dungeon_texture.png", "KK_Props", Role.Prop);
        KayKitMaterial("Dungeon/dungeon_texture.png", "KK_StoneFloor", Role.Stone);
        KayKitMaterial("Dungeon/dungeon_texture.png", "KK_WoodFloor", Role.Wood);
        KayKitMaterial("Furniture/furniturebits_texture.png", "KK_Furniture", Role.Prop);
        KayKitMaterial("Restaurant/restaurantbits_texture.png", "KK_Restaurant", Role.Prop);

        // Crew: skin, cloth and hair.
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project/Art/Materials/Crew" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var role = m.name.Contains("Skin") || m.name.Contains("Arms") ? Role.Skin : m.name.Contains("Hair") ? Role.Hair : Role.Cloth;
            Style(m, role);
            // Clothes render their inside (open collars); keep that.
            if (role == Role.Cloth) m.SetFloat("_Cull", 0f);
        }

        // Greybox materials in the movement test scene.
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project/Art/Materials/Prototype" }))
            Style(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)), Role.Paint);

        OutlineMaterial(CrewOutlinePath, new Color(0.13f, 0.09f, 0.07f), 1.15f, 0f);
        OutlineMaterial(InteractOutlinePath, new Color(1f, 0.82f, 0.45f), 0f, 1f);
        AssetDatabase.SaveAssets();
        Debug.Log("[StylizedLook] Applied");
    }

    static void Load()
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(NoisePath);
        if (ti.sRGBTexture || ti.wrapMode != TextureWrapMode.Repeat || ti.textureCompression != TextureImporterCompression.Uncompressed)
        {
            ti.sRGBTexture = false;   // it's data, not colour
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        noise = AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath);
        stylized = Shader.Find("Port/Stylized");
        outline = Shader.Find("Port/Outline");
    }

    /// <summary>A stylized material for a KayKit model's original material (by its atlas), for the deck builder.</summary>
    public static Material ForKayKit(Material original)
    {
        if (original == null || original.mainTexture == null) return null;
        string tex = original.mainTexture.name;
        string name = tex.Contains("furniture") ? "KK_Furniture" : tex.Contains("restaurant") ? "KK_Restaurant" : "KK_Props";
        return AssetDatabase.LoadAssetAtPath<Material>($"{StylizedFolder}/{name}.mat");
    }

    public static Material Get(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{StylizedFolder}/{name}.mat");

    static void KayKitMaterial(string atlas, string name, Role role)
    {
        string path = $"{StylizedFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(stylized); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{KayKit}/{atlas}"));
        m.SetColor("_BaseColor", Color.white);
        Style(m, role);
    }

    static void OutlineMaterial(string path, Color color, float width, float centerExtrude)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(outline); AssetDatabase.CreateAsset(m, path); }
        m.shader = outline;
        m.SetColor("_OutlineColor", color);
        m.SetFloat("_OutlineWidth", width);
        m.SetFloat("_CenterExtrude", centerExtrude);
        EditorUtility.SetDirty(m);
    }

    /// <summary>
    /// The style guide. Every role shares the same toon shape, shadow tint and noise; they differ in how much
    /// variation, wear and shine they get, so the whole ship reads as one hand-painted set.
    /// </summary>
    public static void Style(Material m, Role role)
    {
        if (m == null) return;
        if (stylized == null) Load();
        bool hadNormal = m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") != null;
        m.shader = stylized;
        m.SetTexture("_NoiseTex", noise);
        if (hadNormal) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");

        // Shared baseline.
        m.SetFloat("_ToonAmount", 0.3f);
        m.SetFloat("_ToonCenter", 0.5f);
        m.SetFloat("_ToonSoftness", 0.14f);
        m.SetColor("_ShadowTint", new Color(0.42f, 0.47f, 0.62f));
        m.SetFloat("_RimStrength", 0.1f);
        m.SetFloat("_NoiseScale", 0.35f);
        m.SetFloat("_CellVariation", 0.04f);
        m.SetVector("_CellSize", new Vector4(1f, 1f, 1f, 0f));
        m.SetFloat("_WoodGrain", 0f);
        m.SetVector("_GrainStretch", new Vector4(0.15f, 1.6f, 0f, 0f));
        m.SetFloat("_TopHighlight", 0.06f);
        m.SetColor("_DirtColor", new Color(0.42f, 0.34f, 0.27f));
        m.SetFloat("_DirtAmount", 0.35f);
        m.SetFloat("_DirtHeight", 0.55f);
        m.SetFloat("_FloorY", 0.05f);
        m.SetColor("_RustColor", new Color(0.45f, 0.24f, 0.12f));
        m.SetFloat("_RustAmount", 0f);
        m.SetFloat("_EdgeWear", 0.06f);
        m.SetFloat("_SpecStrength", 0.4f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_PathWear", 0f);
        m.SetFloat("_PathHalfWidth", 1.6f);
        m.SetFloat("_VertexTint", 0f);
        m.SetFloat("_Cull", 2f);

        switch (role)
        {
            case Role.PaintedIron: // bulkheads, hull: painted plates, a little rust bleeding at the bottom
                Set(m, color: 0.26f, grain: 0.22f, smooth: 0.28f, edgeWear: 0.16f, dirt: 0.55f, dirtHeight: 0.8f);
                m.SetFloat("_RustAmount", 0.14f);
                m.SetVector("_CellSize", new Vector4(4f, 1.4f, 4f, 0f)); // plate to plate
                m.SetFloat("_CellVariation", 0.08f);
                break;
            case Role.Stone:
                Set(m, color: 0.15f, grain: 0.22f, smooth: 0.18f, edgeWear: 0.1f, dirt: 0.25f, dirtHeight: 0.2f);
                m.SetVector("_CellSize", new Vector4(1f, 8f, 1f, 0f)); // tile to tile
                m.SetFloat("_CellVariation", 0.07f);
                m.SetFloat("_TopHighlight", 0.04f);
                m.SetFloat("_PathWear", 0.45f);
                // KayKit's pale stone goes lavender under moonlight; a warm grey keeps it part of the ship.
                if (m.name.StartsWith("KK_")) m.SetColor("_BaseColor", new Color(0.8f, 0.76f, 0.7f));
                break;
            case Role.Wood:
                Set(m, color: 0.12f, grain: 0.14f, smooth: 0.24f, edgeWear: 0.12f, dirt: 0.2f, dirtHeight: 0.3f);
                m.SetFloat("_WoodGrain", 0.16f);
                m.SetVector("_CellSize", new Vector4(0.5f, 8f, 2f, 0f)); // plank to plank
                m.SetFloat("_CellVariation", 0.1f);
                m.SetFloat("_PathWear", 0.55f);
                // KayKit's wood swatch is very orange; pull it towards a weathered deck brown.
                if (m.name.StartsWith("KK_")) m.SetColor("_BaseColor", new Color(0.72f, 0.64f, 0.6f));
                break;
            case Role.Brass: // warm metal, green patina in the hollows
                Set(m, color: 0.08f, grain: 0.08f, smooth: 0.62f, edgeWear: 0.14f, dirt: 0.15f, dirtHeight: 0.3f);
                m.SetFloat("_Metallic", 0.8f);
                m.SetFloat("_SpecStrength", 1.0f);
                m.SetColor("_RustColor", new Color(0.32f, 0.46f, 0.38f));
                m.SetFloat("_RustAmount", 0.14f);
                break;
            case Role.Iron: // dark iron: duller, rust patches
                Set(m, color: 0.1f, grain: 0.12f, smooth: 0.4f, edgeWear: 0.16f, dirt: 0.3f, dirtHeight: 0.5f);
                m.SetFloat("_Metallic", 0.65f);
                m.SetFloat("_SpecStrength", 0.8f);
                m.SetFloat("_RustAmount", 0.3f);
                break;
            case Role.Coal:
                Set(m, color: 0.1f, grain: 0.3f, smooth: 0.35f, edgeWear: 0f, dirt: 0f, dirtHeight: 0.1f);
                m.SetFloat("_TopHighlight", 0.12f);
                break;
            case Role.Paint: // painted wood and metal: clean but hand-made
                Set(m, color: 0.1f, grain: 0.12f, smooth: 0.3f, edgeWear: 0.12f, dirt: 0.3f, dirtHeight: 0.4f);
                break;
            case Role.Cloth:
                Set(m, color: 0.06f, grain: 0.08f, smooth: 0.12f, edgeWear: 0f, dirt: 0.25f, dirtHeight: 0.35f);
                m.SetFloat("_ToonAmount", 0.38f);
                m.SetFloat("_RimStrength", 0.18f);
                m.SetFloat("_NoiseScale", 1.2f);
                break;
            case Role.Skin:
                Set(m, color: 0.02f, grain: 0.02f, smooth: 0.3f, edgeWear: 0f, dirt: 0f, dirtHeight: 0.1f);
                m.SetFloat("_ToonAmount", 0.4f);
                m.SetFloat("_RimStrength", 0.2f);
                m.SetColor("_ShadowTint", new Color(0.62f, 0.42f, 0.42f)); // warm shadows on skin
                m.SetFloat("_TopHighlight", 0f);
                break;
            case Role.Hair:
                Set(m, color: 0.04f, grain: 0.1f, smooth: 0.45f, edgeWear: 0f, dirt: 0f, dirtHeight: 0.1f);
                m.SetFloat("_ToonAmount", 0.4f);
                m.SetFloat("_RimStrength", 0.2f);
                m.SetFloat("_NoiseScale", 3f);
                break;
            default: // Prop: KayKit props and furniture
                Set(m, color: 0.12f, grain: 0.14f, smooth: 0.25f, edgeWear: 0.1f, dirt: 0.3f, dirtHeight: 0.4f);
                m.SetFloat("_WoodGrain", 0.08f);
                break;
        }
        EditorUtility.SetDirty(m);
    }

    static void Set(Material m, float color, float grain, float smooth, float edgeWear, float dirt, float dirtHeight)
    {
        m.SetFloat("_ColorVariation", color);
        m.SetFloat("_Grain", grain);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_EdgeWear", edgeWear);
        m.SetFloat("_DirtAmount", dirt);
        m.SetFloat("_DirtHeight", dirtHeight);
    }
}
