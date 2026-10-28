using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the props for the ending cinematic and the Resources/EndingSet that points at them. Owner: Samprity.
///   In the Editor:  Port > Samprity > Build Ending Stage
///   Headless:       Unity.exe -batchmode -projectPath . -executeMethod EndingStageBuilder.BuildBatch -quit
/// Lifeboat (white, a lantern on a pole at the stern), the wreckers' boat (the same, tarred black), a corked bottle,
/// and Grey Cove: a crescent of dark rock at the edge of the ice with the wreckers' lanterns on it.
/// The ship itself is the ShipDeck prefab, used as it is.
/// </summary>
public static class EndingStageBuilder
{
    const string PrefabFolder = "Assets/_Project/Prefabs/Environment/Ending";
    const string SetPath = "Assets/_Project/Prefabs/Environment/Resources/EndingSet.asset";
    const string MaterialFolder = "Assets/_Project/Art/Materials/Ending";
    const string MeshFolder = "Assets/_Project/Art/Models/Generated";
    const string IcebergPath = "Assets/_Project/Prefabs/Environment/Resources/Iceberg.prefab";
    const string BottleFbx = "Assets/_Project/Art/ThirdParty/KayKit/Dungeon/bottle_A_labeled_brown.fbx";
    /// <summary>The sea sits this far below the deck (ShipDeckBuilder puts the sea plane at y -2.2).</summary>
    public const float SeaY = -2.2f;

    static Material white, tar, wood, glow, rock, ice, iron, brass;

    [MenuItem("Port/Samprity/Build Ending Stage")]
    public static void Build()
    {
        MovementPrototypeBuilder.EnsureFolder(PrefabFolder);
        MovementPrototypeBuilder.EnsureFolder(MaterialFolder);
        MovementPrototypeBuilder.EnsureFolder(MeshFolder);
        white = Mat("Ending_BoatWhite", new Color(0.86f, 0.84f, 0.78f), StylizedLook.Role.Paint, twoSided: true);
        // The wreckers' boat: dark, weathered, unpainted wood, still readable against black water.
        tar = Mat("Ending_BoatTar", new Color(0.36f, 0.29f, 0.22f), StylizedLook.Role.Wood, twoSided: true);
        rock = Mat("Ending_Rock", new Color(0.3f, 0.31f, 0.33f), StylizedLook.Role.Stone, twoSided: false);
        // Wet basalt: strong painterly variation, pale worn edges, a damp sheen.
        rock.SetFloat("_ColorVariation", 0.22f);
        rock.SetFloat("_Grain", 0.32f);
        rock.SetFloat("_EdgeWear", 0.3f);
        rock.SetFloat("_Smoothness", 0.42f);
        rock.SetVector("_CellSize", new Vector4(3f, 2f, 3f, 0f));
        rock.SetFloat("_CellVariation", 0.12f);
        rock.SetFloat("_NoiseScale", 0.6f);
        rock.SetFloat("_TopHighlight", 0.4f); // frost on every upward face
        EditorUtility.SetDirty(rock);
        const string Ship = "Assets/_Project/Art/Materials/Ship";
        wood = AssetDatabase.LoadAssetAtPath<Material>($"{Ship}/Ship_DarkWood.mat");
        ice = AssetDatabase.LoadAssetAtPath<Material>($"{Ship}/Ship_Ice.mat");
        iron = AssetDatabase.LoadAssetAtPath<Material>($"{Ship}/Ship_Steel.mat");
        brass = AssetDatabase.LoadAssetAtPath<Material>($"{Ship}/Ship_Brass.mat");
        glow = GlowMat();

        var hull = HullMesh();
        var lifeboat = Boat("Ending_Lifeboat", hull, white);
        var wrecker = Boat("Ending_WreckerBoat", hull, tar);
        var bottle = Bottle();
        var cove = Cove(wrecker);

        var set = AssetDatabase.LoadAssetAtPath<EndingSet>(SetPath);
        if (set == null) { set = ScriptableObject.CreateInstance<EndingSet>(); AssetDatabase.CreateAsset(set, SetPath); }
        set.deck = AssetDatabase.LoadAssetAtPath<GameObject>(ShipDeckBuilder.PrefabPath);
        set.iceberg = AssetDatabase.LoadAssetAtPath<GameObject>(IcebergPath);
        set.lifeboat = lifeboat;
        set.wreckerBoat = wrecker;
        set.bottle = bottle;
        set.cove = cove;
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Debug.Log("[Ending] Built the ending stage and " + SetPath);
    }

    public static void BuildBatch() => Build();

    // ---------- Materials ----------

    static Material Mat(string name, Color color, StylizedLook.Role role, bool twoSided)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        StylizedLook.Style(m, role);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_DirtAmount", 0f);
        m.SetFloat("_PathWear", 0f);
        if (twoSided)
        {
            m.SetFloat("_Cull", 0f);       // the inside of a boat shows from above
            m.SetFloat("_VertexTint", 1f); // the hull's painted plank laps and sheer strake
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>A lantern flame: unlit and brighter than white, so the bloom picks it up.</summary>
    static Material GlowMat()
    {
        string path = $"{MaterialFolder}/Ending_LanternGlow.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", new Color(2.2f, 1.35f, 0.55f));
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- Boats ----------

    /// <summary>
    /// A double-ended lifeboat hull, 5 m long and 1.5 m wide: U-shaped sections lofted bow to stern. Vertex colours
    /// paint the clinker plank laps (a thin dark line every few rows) and a darker sheer strake under the gunwale;
    /// the boat materials turn on Port/Stylized's vertex tint to show them.
    /// </summary>
    static Mesh HullMesh()
    {
        const int along = 24, round = 29;
        const float length = 5f, beam = 1.5f, depth = 0.65f;
        var v = new Vector3[(along + 1) * round];
        var uv = new Vector2[v.Length];
        var col = new Color[v.Length];
        for (int i = 0; i <= along; i++)
        {
            float t = i / (float)along;               // 0 stern .. 1 bow
            float k = Mathf.Abs(t * 2f - 1f);
            float half = beam * 0.5f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(k, 2.4f)));
            float sheer = 0.12f * k * k;               // the ends rise a little
            float z = (t - 0.5f) * length;
            for (int j = 0; j < round; j++)
            {
                float u = j / (float)(round - 1) * 2f - 1f;   // -1 port gunwale .. 1 starboard gunwale
                float y = -depth * Mathf.Pow(Mathf.Max(0f, 1f - u * u), 0.6f) * (1f - 0.35f * k) + sheer;
                v[i * round + j] = new Vector3(half * u, y, z);
                uv[i * round + j] = new Vector2(j / (float)(round - 1), t * 3f);
                // Rows counted from the gunwale on either side: the top two are the sheer strake, then a lap every 4th.
                int fromTop = Mathf.Min(j, round - 1 - j);
                col[i * round + j] = fromTop <= 1 ? new Color(0.5f, 0.42f, 0.34f) : fromTop % 4 == 0 ? new Color(0.68f, 0.66f, 0.64f) : Color.white;
            }
        }
        var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < along; i++)
            for (int j = 0; j < round - 1; j++)
            {
                int a = i * round + j, b = a + 1, c = a + round, d = c + 1;
                tris.AddRange(new[] { a, c, b, b, c, d });
            }
        var mesh = new Mesh { name = "Ending_Hull", vertices = v, uv = uv, colors = col };
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        string path = $"{MeshFolder}/Ending_Hull.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    /// <summary>
    /// A clinker-built boat floating at sea level: the hull with dark planking strakes and a rubbing band, floorboards,
    /// thwarts, stem and stern posts, a wooden gunwale with rowlocks, two pairs of oars shipped out over the side,
    /// and a hurricane lantern on a pole at the stern.
    /// </summary>
    static GameObject Boat(string name, Mesh hull, Material hullMat)
    {
        var root = new GameObject(name);
        var h = Part(root.transform, "Hull", hull, hullMat, new Vector3(0f, 0.25f, 0f), Vector3.zero, Vector3.one);
        h.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        var t = root.transform;

        // (The planking lines and the sheer band are painted into the hull's vertex colours, so they follow its curve.)
        foreach (float side in new[] { -1f, 1f })
        {
            Part(t, "Gunwale", cube, wood, new Vector3(side * 0.7f, 0.28f, 0f), new Vector3(0f, side * -2f, 0f), new Vector3(0.07f, 0.06f, 3.7f));
            foreach (float z in new[] { -0.55f, 1.1f })
            {
                Part(t, "Rowlock", cylinder, brass, new Vector3(side * 0.73f, 0.34f, z), Vector3.zero, new Vector3(0.04f, 0.05f, 0.04f));
                // The oar rests in its rowlock, loom inboard, blade out over the side and dipping into the water.
                var oar = new GameObject("Oar").transform;
                oar.SetParent(t, false);
                oar.localPosition = new Vector3(side * 0.73f, 0.36f, z);
                oar.localRotation = Quaternion.Euler(14f, side * (z < 0f ? 104f : 76f), 0f);
                Part(oar, "Loom", cylinder, wood, new Vector3(0f, 0f, 0.6f), new Vector3(90f, 0f, 0f), new Vector3(0.045f, 1.35f, 0.045f));
                Part(oar, "Blade", cube, wood, new Vector3(0f, 0f, 1.95f), Vector3.zero, new Vector3(0.14f, 0.025f, 0.55f));
            }
        }
        Part(t, "Floorboards", cube, wood, new Vector3(0f, -0.28f, 0f), Vector3.zero, new Vector3(0.75f, 0.03f, 3.4f));
        foreach (float z in new[] { -0.9f, 0.3f, 1.4f })
            Part(t, "Thwart", cube, wood, new Vector3(0f, 0.04f, z), Vector3.zero, new Vector3(1.32f, 0.05f, 0.24f));
        foreach (float z in new[] { -2.48f, 2.48f })
            Part(t, "Post", cube, wood, new Vector3(0f, 0.2f, z), Vector3.zero, new Vector3(0.07f, 0.42f, 0.09f));
        Part(t, "LanternPole", cube, wood, new Vector3(0f, 0.6f, -2.1f), Vector3.zero, new Vector3(0.05f, 0.85f, 0.05f));
        Lantern(t, new Vector3(0f, 1.03f, -2.1f));
        return Save(root, name);
    }

    /// <summary>
    /// A hurricane lantern: brass base and hood, an iron cage round a glowing glass chimney, a bail handle, and a warm
    /// point light. Its "Glass" and "Light" children are what the film switches off when a lantern goes out.
    /// </summary>
    static void Lantern(Transform parent, Vector3 pos, float scale = 1f)
    {
        var cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var l = new GameObject("Lantern").transform;
        l.SetParent(parent, false);
        l.localPosition = pos;
        l.localScale = Vector3.one * scale;
        Part(l, "Base", cylinder, brass, new Vector3(0f, 0.015f, 0f), Vector3.zero, new Vector3(0.12f, 0.018f, 0.12f));
        var glass = Part(l, "Glass", cylinder, glow, new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(0.075f, 0.065f, 0.075f));
        glass.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (int i = 0; i < 4; i++)
        {
            var a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
            Part(l, "Cage", cube, iron, new Vector3(Mathf.Sin(a) * 0.052f, 0.1f, Mathf.Cos(a) * 0.052f), Vector3.zero, new Vector3(0.008f, 0.15f, 0.008f));
        }
        Part(l, "Hood", cylinder, brass, new Vector3(0f, 0.185f, 0f), Vector3.zero, new Vector3(0.11f, 0.014f, 0.11f));
        Part(l, "Chimney", cylinder, iron, new Vector3(0f, 0.215f, 0f), Vector3.zero, new Vector3(0.05f, 0.025f, 0.05f));
        Part(l, "Bail", cube, iron, new Vector3(0f, 0.27f, 0f), Vector3.zero, new Vector3(0.11f, 0.008f, 0.008f));
        var light = new GameObject("Light", typeof(Light)).GetComponent<Light>();
        light.transform.SetParent(l, false);
        light.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        light.type = LightType.Point;
        light.color = new Color(1f, 0.66f, 0.34f);
        light.range = 8f * scale;
        light.intensity = 2.4f;
        light.shadows = LightShadows.None;
    }

    static GameObject Bottle()
    {
        var root = new GameObject("Ending_Bottle");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BottleFbx));
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.transform.SetParent(root.transform, false);
        foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        foreach (var r in model.GetComponentsInChildren<Renderer>())
            r.sharedMaterials = r.sharedMaterials.Select(m => StylizedLook.ForKayKit(m) ?? m).ToArray();
        // 26 cm tall, standing on its base at the root.
        var b = model.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((x, y) => { x.Encapsulate(y); return x; });
        float s = 0.26f / Mathf.Max(0.01f, b.size.y);
        model.transform.localScale = Vector3.one * s;
        model.transform.localPosition = new Vector3(-b.center.x * s, -b.min.y * s, -b.center.z * s);
        return Save(root, "Ending_Bottle");
    }

    // ---------- Grey Cove ----------

    /// <summary>
    /// A crescent of dark rock round a patch of black water, opening towards -Z (the open sea). Two wreckers' boats
    /// wait inside it and lanterns burn on the rocks. Local origin = the middle of the cove at sea level + 2.2 m,
    /// like the deck, so the water is at SeaY.
    /// </summary>
    static GameObject Cove(GameObject wrecker)
    {
        var root = new GameObject("Ending_GreyCove");
        var berg = AssetDatabase.LoadAssetAtPath<GameObject>(IcebergPath);
        var rockMesh = berg != null ? berg.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).OrderByDescending(m => m.vertexCount).FirstOrDefault() : null;
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        if (rockMesh != null)
        {
            var rnd = new System.Random(7);
            float R() => (float)rnd.NextDouble();
            // The crescent: from the left horn round the back to the right horn.
            for (int i = 0; i < 11; i++)
            {
                float a = Mathf.Lerp(-115f, 115f, i / 10f) * Mathf.Deg2Rad;
                float dist = 30f + R() * 6f;
                // The ice mesh is ~30 m across and ~21 m high: rocks 9-17 m wide, 4-10 m out of the water.
                var p = new Vector3(Mathf.Sin(a) * dist, SeaY - 2f, Mathf.Cos(a) * dist);
                var size = new Vector3(0.3f + R() * 0.25f, 0.2f + R() * 0.3f, 0.3f + R() * 0.25f) * Mathf.Lerp(1.3f, 0.8f, Mathf.Abs(a) / 2f);
                var euler = new Vector3(0f, R() * 360f, 0f);
                Part(root.transform, "Rock", rockMesh, rock, p, euler, size);
                // Old ice packed against the foot of the rock, on the cove side: this is the edge of the ice.
                var foot = new Vector3(Mathf.Sin(a) * (dist - 9f), SeaY - 1.2f, Mathf.Cos(a) * (dist - 9f));
                Part(root.transform, "Growler", rockMesh, ice, foot, new Vector3(0f, R() * 360f, 0f), Vector3.one * (0.06f + R() * 0.07f));
            }
            // A few low rocks breaking the water near the mouth.
            for (int i = 0; i < 5; i++)
                Part(root.transform, "Skerry", rockMesh, rock, new Vector3((R() - 0.5f) * 40f, SeaY - 0.6f, (R() - 0.7f) * 18f),
                    new Vector3(0f, R() * 360f, 0f), new Vector3(0.1f, 0.05f, 0.1f) * (0.7f + R()));
            // Floes of broken ice drifting in the cove and out past its mouth.
            for (int i = 0; i < 12; i++)
                // Slabs about half a metre thick, so they read as ice from low down rather than a white smear.
                Part(root.transform, "Floe", rockMesh, ice, new Vector3((R() - 0.5f) * 46f, SeaY - 0.18f, (R() - 0.55f) * 50f),
                    new Vector3(0f, R() * 360f, 0f), new Vector3(0.05f + R() * 0.05f, 0.022f, 0.04f + R() * 0.05f));
        }

        // The wreckers' signal lanterns on tall posts along the rocks, burning towards the sea.
        foreach (var (x, z) in new[] { (-17f, 19f), (-6f, 25f), (7f, 24f), (18f, 18f) })
        {
            Part(root.transform, "Post", cylinder, wood, new Vector3(x, SeaY + 2.6f, z), Vector3.zero, new Vector3(0.18f, 2.6f, 0.18f));
            Part(root.transform, "Arm", cube, wood, new Vector3(x, SeaY + 5f, z - 0.35f), Vector3.zero, new Vector3(0.1f, 0.1f, 0.8f));
            Lantern(root.transform, new Vector3(x, SeaY + 4.25f, z - 0.7f), 2.6f);
        }
        // The wreckers' boats, waiting in the middle of the cove.
        foreach (var (pos, yaw) in new[] { (new Vector3(-4.5f, SeaY, 10f), 160f), (new Vector3(5f, SeaY, 6f), 200f) })
        {
            var b = (GameObject)PrefabUtility.InstantiatePrefab(wrecker);
            b.transform.SetParent(root.transform, false);
            b.transform.localPosition = pos;
            b.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            b.name = "WreckerBoat";
        }
        return Save(root, "Ending_GreyCove");
    }

    // ---------- Helpers ----------

    static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 euler, Vector3 scale)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject Save(GameObject root, string name)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }
}
