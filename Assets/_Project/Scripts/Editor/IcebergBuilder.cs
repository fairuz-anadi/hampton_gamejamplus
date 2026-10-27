using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the finale's iceberg: a faceted mass of ice several storeys high, a foam collar where it meets the sea and
/// a cold light that picks it out at night. Owner: Samprity. Saved as Resources/Iceberg.prefab, which
/// IcebergPresentation places from Rabib's IcebergSequence (heading and countdown) during the finale.
///   In the Editor:  Port > Samprity > Build Iceberg      (Build Ship Deck also runs it)
/// The prefab's origin sits on the waterline.
/// </summary>
public static class IcebergBuilder
{
    const string PrefabPath = "Assets/_Project/Prefabs/Environment/Resources/Iceberg.prefab";
    const string MeshFolder = "Assets/_Project/Art/Models/Generated";
    const string IceMaterialPath = "Assets/_Project/Art/Materials/Ship/Ship_Ice.mat";
    const string FoamMaterialPath = "Assets/_Project/Art/Materials/Ship/Ship_Foam.mat";

    [MenuItem("Port/Samprity/Build Iceberg")]
    public static void Build()
    {
        MovementPrototypeBuilder.EnsureFolder(MeshFolder);
        MovementPrototypeBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));

        var ice = Save(IceMesh(), "Iceberg");
        var collar = Save(CollarMesh(), "IcebergCollar");

        var root = new GameObject("Iceberg");
        var body = new GameObject("Ice", typeof(MeshFilter), typeof(MeshRenderer));
        body.transform.SetParent(root.transform, false);
        body.GetComponent<MeshFilter>().sharedMesh = ice;
        body.GetComponent<MeshRenderer>().sharedMaterial = IceMaterial();

        var foam = new GameObject("FoamCollar", typeof(MeshFilter), typeof(MeshRenderer));
        foam.transform.SetParent(root.transform, false);
        foam.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        foam.GetComponent<MeshFilter>().sharedMesh = collar;
        var fr = foam.GetComponent<MeshRenderer>();
        fr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(FoamMaterialPath);
        fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var scroll = foam.AddComponent<FoamScroll>();
        scroll.speed = 0.8f;
        scroll.metresPerRepeat = 6f;

        // Moonlight off the ice: a wide, cold light on the side facing the ship (IcebergPresentation keeps it there).
        var light = new GameObject("IceLight", typeof(Light)).GetComponent<Light>();
        light.transform.SetParent(root.transform, false);
        light.transform.localPosition = new Vector3(0f, 14f, -26f);
        light.type = LightType.Point;
        light.color = new Color(0.62f, 0.78f, 1f);
        light.range = 70f;
        light.intensity = 3f;
        light.shadows = LightShadows.None;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Iceberg] Built {PrefabPath}");
    }

    static Mesh Save(Mesh m, string name)
    {
        string path = $"{MeshFolder}/{name}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    /// <summary>
    /// Three overlapping lobes (the main mass and two peaks) on a sphere of directions, roughened with noise, its
    /// underwater part pulled in, then flat-shaded so every face reads as a sheared plane of ice.
    /// </summary>
    static Mesh IceMesh()
    {
        const int lat = 16, lon = 26;
        var lobes = new (Vector3 centre, Vector3 radii)[]
        {
            (new Vector3(0f, 3f, 0f), new Vector3(15f, 10f, 12f)),
            (new Vector3(-4f, 11f, 1.5f), new Vector3(7f, 10f, 6.5f)),
            (new Vector3(5.5f, 8f, -2f), new Vector3(5f, 7f, 5f)),
        };
        var grid = new Vector3[lat + 1, lon];
        for (int i = 0; i <= lat; i++)
        for (int j = 0; j < lon; j++)
        {
            float theta = Mathf.PI * i / lat, phi = 2f * Mathf.PI * j / lon;
            var dir = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
            // Furthest surface of any lobe along this direction from the berg's centre.
            float r = 0f;
            foreach (var (c, s) in lobes) r = Mathf.Max(r, LobeReach(c, s, dir));
            float n = Mathf.PerlinNoise(dir.x * 2.1f + 7.3f, dir.z * 2.1f + dir.y * 1.7f + 3.1f) - 0.5f;
            float n2 = Mathf.PerlinNoise(dir.x * 5.3f + 1.1f, dir.y * 5.3f + dir.z * 3.1f + 9.7f) - 0.5f;
            r *= 1f + n * 0.32f + n2 * 0.14f;
            var p = dir * r;
            // Below the water it bulges out a little and then tucks under: only the top is seen.
            if (p.y < 0f) { p.x *= 1.08f; p.z *= 1.08f; p.y = Mathf.Max(p.y, -7f); }
            grid[i, j] = p;
        }

        var verts = new List<Vector3>();
        var tris = new List<int>();
        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            int k = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
        }
        for (int i = 0; i < lat; i++)
        for (int j = 0; j < lon; j++)
        {
            int jn = (j + 1) % lon;
            var a = grid[i, j]; var b = grid[i, jn]; var c = grid[i + 1, j]; var d = grid[i + 1, jn];
            if (i > 0) Tri(a, b, c);
            if (i < lat - 1) Tri(b, d, c);
        }
        var m = new Mesh { name = "Iceberg", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        // Make sure the faces point outwards.
        var nrm = m.normals;
        int outward = 0;
        for (int i = 0; i < verts.Count; i += 3) if (Vector3.Dot(nrm[i], verts[i]) > 0f) outward++;
        if (outward < verts.Count / 6) { tris.Reverse(); m.SetTriangles(tris, 0); m.RecalculateNormals(); }
        m.RecalculateBounds();
        return m;
    }

    /// <summary>How far from the origin, along dir, the surface of an ellipsoid (centre c, radii s) reaches; 0 if it misses.</summary>
    static float LobeReach(Vector3 c, Vector3 s, Vector3 dir)
    {
        // Ray from the origin against the ellipsoid, in the ellipsoid's unit-sphere space.
        var o = new Vector3(-c.x / s.x, -c.y / s.y, -c.z / s.z);
        var d = new Vector3(dir.x / s.x, dir.y / s.y, dir.z / s.z);
        float a = Vector3.Dot(d, d), b = 2f * Vector3.Dot(o, d), cc = Vector3.Dot(o, o) - 1f;
        float disc = b * b - 4f * a * cc;
        if (disc < 0f) return 0f;
        return Mathf.Max(0f, (-b + Mathf.Sqrt(disc)) / (2f * a));
    }

    /// <summary>A ragged ring of foam round the waterline; V runs outwards so FoamScroll pushes it away from the ice.</summary>
    static Mesh CollarMesh()
    {
        const int seg = 48;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var cols = new List<Color>();
        var tris = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = 2f * Mathf.PI * i / seg;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float inner = 14.5f + Mathf.PerlinNoise(i * 0.31f, 0.5f) * 2.5f;
            float outer = inner + 4f + Mathf.PerlinNoise(i * 0.27f, 4.1f) * 4f;
            verts.Add(dir * inner); verts.Add(dir * outer);
            uvs.Add(new Vector2(i * 4f / seg, 0f)); uvs.Add(new Vector2(i * 4f / seg, 1f));
            cols.Add(new Color(1f, 1f, 1f, 0.95f)); cols.Add(new Color(1f, 1f, 1f, 0f));
            if (i == 0) continue;
            int k = i * 2;
            tris.AddRange(new[] { k - 2, k - 1, k, k - 1, k + 1, k });
        }
        var m = new Mesh { name = "IcebergCollar" };
        m.SetVertices(verts); m.SetUVs(0, uvs); m.SetColors(cols); m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        if (m.normals[0].y < 0f) { tris.Reverse(); m.SetTriangles(tris, 0); m.RecalculateNormals(); }
        m.RecalculateBounds();
        return m;
    }

    /// <summary>Blue-white ice on the painterly shader: deep blue shadows, a strong cold rim, glassy highlights.</summary>
    static Material IceMaterial()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(IceMaterialPath);
        if (m == null) { m = new Material(Shader.Find("Port/Stylized")); AssetDatabase.CreateAsset(m, IceMaterialPath); }
        StylizedLook.Style(m, StylizedLook.Role.Paint);
        m.SetColor("_BaseColor", new Color(0.86f, 0.93f, 0.98f));
        m.SetColor("_ShadowTint", new Color(0.35f, 0.55f, 0.9f));
        m.SetFloat("_ToonAmount", 0.5f);
        m.SetFloat("_RimStrength", 0.55f);
        m.SetFloat("_Smoothness", 0.6f);
        m.SetFloat("_SpecStrength", 0.9f);
        m.SetFloat("_ColorVariation", 0.06f);
        m.SetFloat("_Grain", 0.06f);
        m.SetFloat("_EdgeWear", 0.18f);
        m.SetFloat("_DirtAmount", 0f);
        m.SetFloat("_RustAmount", 0f);
        m.SetFloat("_TopHighlight", 0.25f);
        m.SetFloat("_NoiseScale", 0.12f);
        EditorUtility.SetDirty(m);
        return m;
    }
}
