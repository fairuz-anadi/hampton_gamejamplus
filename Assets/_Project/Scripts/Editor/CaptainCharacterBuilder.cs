using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the Captain as a crew-style character: Quaternius' CC0 Ranger outfit (hood and shoulder guard left off),
/// dyed into a navy coat with brass by Tools/make_captain_textures.py, the crew's male head, a grey beard, a peaked
/// captain's cap modelled here, and a KayKit bottle in his right hand. Same skeleton and animator as the crew, so
/// every crew animation plays on him. Owner: Samprity.
///   In the Editor:  Port > Samprity > Build Captain  /  Port > Samprity > Preview Captain
///   Headless:       Unity.exe -batchmode -projectPath . -executeMethod CaptainCharacterBuilder.BuildBatch -quit
///                   Unity.exe -batchmode -projectPath . -executeMethod CaptainCharacterBuilder.PreviewBatch  (no -quit: it exits itself)
/// Output: Prefabs/Crew/Captain.prefab. Preview renders Logs/CaptainPreview_*.png in play mode, so he stands in his
/// idle animation instead of the bind pose.
/// </summary>
public static class CaptainCharacterBuilder
{
    const string Q = "Assets/_Project/Art/ThirdParty/Quaternius";
    const string OutfitPath = Q + "/Outfits/Male_Ranger.fbx";
    const string BodyPath = Q + "/Characters/Superhero_Male_FullBody.fbx";
    const string HeadMeshPath = "Assets/_Project/Art/Characters/Generated/Head_Male.asset";
    const string BottlePath = "Assets/_Project/Art/ThirdParty/KayKit/Dungeon/bottle_A_labeled_brown.fbx";
    const string GeneratedFolder = "Assets/_Project/Art/Characters/Generated";
    const string MaterialFolder = "Assets/_Project/Art/Materials/Captain";
    const string CrewMaterials = "Assets/_Project/Art/Materials/Crew";
    public const string PrefabPath = "Assets/_Project/Prefabs/Crew/Captain.prefab";
    const string SetPath = "Assets/_Project/Prefabs/Crew/Resources/CaptainSet.asset";
    const string ControllerPath = "Assets/_Project/Art/Animations/Captain.controller";
    const string AnimFbx = Q + "/Animations/UAL1_Standard.fbx";

    static readonly Color HairGrey = new Color(0.46f, 0.44f, 0.42f);
    // Colours are given in sRGB; very dark values go muddy brown under the toon shader's warm key light.
    static readonly Color CapNavy = new Color(0.17f, 0.22f, 0.4f);
    static readonly Color VisorBlack = new Color(0.07f, 0.075f, 0.09f);
    static readonly Color Brass = new Color(0.98f, 0.76f, 0.32f);

    [MenuItem("Port/Samprity/Build Captain")]
    public static void Build()
    {
        ConfigureImports();
        var mats = BuildMaterials();
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(OutfitPath));
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = "Captain";
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var bonesByName = new Dictionary<string, Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (!bonesByName.ContainsKey(t.name)) bonesByName[t.name] = t;

        // Coat, trousers and boots. A captain wears no hood or ranger's shoulder guard.
        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.name.Contains("Hood") || smr.name.Contains("Pauldron")) { Object.DestroyImmediate(smr.gameObject); continue; }
            var slots = smr.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
                slots[i] = slots[i] != null && slots[i].name.Contains("Regular") ? mats["arms"] : mats["coat"];
            smr.sharedMaterials = slots;
        }

        // Head, eyes and eyebrows from the base body, rebound to the outfit's skeleton (as the crew builder does).
        var body = AssetDatabase.LoadAssetAtPath<GameObject>(BodyPath);
        var headMesh = AssetDatabase.LoadAssetAtPath<Mesh>(HeadMeshPath);
        SkinnedMeshRenderer headRenderer = null;
        foreach (var src in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            bool isBody = src.sharedMesh.vertexCount > 3000;
            var go = new GameObject(isBody ? "Head" : src.name);
            go.transform.SetParent(root.transform, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = isBody ? headMesh : src.sharedMesh;
            smr.bones = src.bones.Select(b => bonesByName.TryGetValue(b.name, out var t) ? t : null).ToArray();
            smr.rootBone = src.rootBone != null && bonesByName.TryGetValue(src.rootBone.name, out var rb) ? rb : root.transform;
            smr.sharedMaterials = isBody
                ? new[] { mats["skin"] }
                : src.sharedMaterials.Select(m => m != null && m.name.Contains("Hair") ? mats["hair"] : m).ToArray();
            smr.updateWhenOffscreen = false;
            if (isBody) headRenderer = smr;
        }

        // Short grey hair under the cap and a full grey beard: static meshes modelled in place, parented to the head bone.
        var head = bonesByName.TryGetValue("Head", out var h) ? h : root.transform;
        foreach (var hairName in new[] { "Hair_Buzzed", "Hair_Beard" })
        {
            var hairModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{Q}/Hair/{hairName}.fbx");
            if (hairModel == null) continue;
            var hair = (GameObject)Object.Instantiate(hairModel, root.transform, false);
            hair.name = hairName;
            hair.transform.SetParent(head, true);
            foreach (var r in hair.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = r.sharedMaterials.Select(_ => mats["hair"]).ToArray();
        }

        var animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
        animator.avatar = AssetDatabase.LoadAllAssetsAtPath(BodyPath).OfType<Avatar>().FirstOrDefault();
        animator.runtimeAnimatorController = BuildController();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        BuildCap(root, head, headRenderer, mats);
        var bottle = BuildBottle(root, animator, bonesByName);

        // The crew's thin warm-dark outline on every part, cap included (the bottle is a prop, like the deck's).
        var outline = AssetDatabase.LoadAssetAtPath<Material>(StylizedLook.CrewOutlinePath);
        if (outline != null)
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (bottle != null && r.transform.IsChildOf(bottle)) continue;
                var slots = r.sharedMaterials;
                if (System.Array.IndexOf(slots, outline) >= 0) continue;
                var withOutline = new Material[slots.Length + 1];
                slots.CopyTo(withOutline, 0);
                withOutline[slots.Length] = outline;
                r.sharedMaterials = withOutline;
            }

        MovementPrototypeBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        // What Ship loads at runtime.
        var set = AssetDatabase.LoadAssetAtPath<CaptainSet>(SetPath);
        if (set == null) { set = ScriptableObject.CreateInstance<CaptainSet>(); AssetDatabase.CreateAsset(set, SetPath); }
        set.prefab = prefab;
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Debug.Log("[Captain] Built " + PrefabPath);
    }

    public static void BuildBatch() => Build();

    /// <summary>
    /// His own animator, so the crew's stays untouched: a "Speed" float drives two walks (Formal: the upright sober
    /// walk; Locomotion: the crew's ordinary one), plus Talk, Stumble and Flinch, crossfaded to by name from
    /// CaptainBody. IK is on for the base layer: that's how the bottle reaches his mouth.
    /// </summary>
    static RuntimeAnimatorController BuildController()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(AnimFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        AnimationClip Clip(string n) => clips.FirstOrDefault(c => c.name == n || c.name.EndsWith("|" + n));

        MovementPrototypeBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(ControllerPath).Replace('\\', '/'));
        AssetDatabase.DeleteAsset(ControllerPath);
        var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        void Walk(string state, string walk)
        {
            controller.CreateBlendTreeInController(state, out var tree, 0);
            tree.blendType = UnityEditor.Animations.BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Clip("Idle_Loop"), 0f);
            tree.AddChild(Clip(walk), 1.4f);
            tree.AddChild(Clip("Jog_Fwd_Loop"), 3.4f);
        }
        Walk("Formal", "Walk_Formal_Loop");
        Walk("Locomotion", "Walk_Loop");

        var machine = controller.layers[0].stateMachine;
        foreach (var (state, clip) in new[] { ("Talk", "Idle_Talking_Loop"), ("Stumble", "Hit_Chest"), ("Flinch", "Hit_Head") })
            machine.AddState(state).motion = Clip(clip);
        machine.defaultState = machine.states.First(s => s.state.name == "Formal").state;

        var layers = controller.layers;
        layers[0].iKPass = true;
        controller.layers = layers;
        EditorUtility.SetDirty(controller);
        Debug.Log("[Captain] Animator: " + string.Join(", ", machine.states.Select(s => s.state.name)));
        return controller;
    }

    // ---------- Imports and materials ----------

    static void ConfigureImports()
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(OutfitPath);
        if (!imp.bakeAxisConversion || imp.animationType != ModelImporterAnimationType.Human)
        {
            imp.bakeAxisConversion = true;
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.SaveAndReimport();
        }
        var normal = (TextureImporter)AssetImporter.GetAtPath(Q + "/Textures/T_Ranger_Normal.png");
        if (normal != null && normal.textureType != TextureImporterType.NormalMap) { normal.textureType = TextureImporterType.NormalMap; normal.SaveAndReimport(); }
    }

    static Dictionary<string, Material> BuildMaterials()
    {
        Texture2D T(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Q}/Textures/{n}.png");
        var coat = Mat("Captain_Coat", T("T_Captain_BaseColor"), T("T_Ranger_Normal"), Color.white, StylizedLook.Role.Cloth);
        coat.SetFloat("_Cull", 0f); // the inside shows through the open collar and cuffs
        coat.doubleSidedGI = true;
        var hairBase = AssetDatabase.LoadAssetAtPath<Material>($"{CrewMaterials}/Crew_Hair_1.mat");
        return new Dictionary<string, Material>
        {
            ["coat"] = coat,
            ["skin"] = AssetDatabase.LoadAssetAtPath<Material>($"{CrewMaterials}/Crew_Skin_Male.mat"),
            ["arms"] = AssetDatabase.LoadAssetAtPath<Material>($"{CrewMaterials}/Crew_Arms_Male.mat"),
            // The hair textures are grey masks meant to be tinted, like the crew's.
            ["hair"] = Mat("Captain_Hair", (Texture2D)hairBase.GetTexture("_BaseMap"), (Texture2D)hairBase.GetTexture("_BumpMap"), HairGrey * 1.7f, StylizedLook.Role.Hair),
            ["capCloth"] = Mat("Captain_CapCloth", null, null, CapNavy, StylizedLook.Role.Cloth),
            ["visor"] = Mat("Captain_Visor", null, null, VisorBlack, StylizedLook.Role.Paint, 0.7f),
            ["brass"] = Mat("Captain_Brass", null, null, Brass, StylizedLook.Role.Brass),
        };
    }

    static Material Mat(string name, Texture2D baseMap, Texture2D normal, Color color, StylizedLook.Role role, float smoothness = -1f)
    {
        MovementPrototypeBuilder.EnsureFolder(MaterialFolder);
        string path = $"{MaterialFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", baseMap);
        m.SetTexture("_BumpMap", normal);
        StylizedLook.Style(m, role);
        m.SetColor("_BaseColor", color);
        if (smoothness >= 0f) m.SetFloat("_Smoothness", smoothness);
        // Plain colours (the cap) stay clean: the style guide's grime turns dark navy brown.
        if (baseMap == null) { m.SetFloat("_DirtAmount", 0f); m.SetFloat("_EdgeWear", 0f); m.SetFloat("_ColorVariation", 0.03f); }
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- The cap ----------

    /// <summary>
    /// A peaked officer's cap fitted to the head: a dark band, a navy crown that flares out to a flat top, a glossy
    /// black peak, a brass chin cord across the front and a brass badge. Measured off the head mesh in the bind pose,
    /// then parented to the head bone.
    /// </summary>
    static void BuildCap(GameObject root, Transform head, SkinnedMeshRenderer headRenderer, Dictionary<string, Material> mats)
    {
        // Where the skull is: the top of the head mesh, and its width just under the crown.
        var baked = new Mesh();
        headRenderer.BakeMesh(baked, true);
        var tr = headRenderer.transform;
        var verts = baked.vertices.Select(v => tr.position + tr.rotation * v).ToArray();
        Object.DestroyImmediate(baked);
        float top = verts.Max(v => v.y);
        var crown = verts.Where(v => v.y > top - 0.1f).ToArray();
        float minX = crown.Min(v => v.x), maxX = crown.Max(v => v.x), minZ = crown.Min(v => v.z), maxZ = crown.Max(v => v.z);
        // The band sits low on the brow; the flat top clears the hair (which stands ~1 cm proud of the skull).
        var centre = new Vector3((minX + maxX) * 0.5f, top - 0.062f, (minZ + maxZ) * 0.5f - 0.004f);
        float r = Mathf.Max(maxX - minX, maxZ - minZ) * 0.5f * 1.07f;
        Debug.Log($"[Captain] Head top {top:0.000}, cap radius {r:0.000}");

        var cap = new GameObject("Cap").transform;
        cap.SetParent(root.transform, false);
        cap.position = centre;
        cap.rotation = Quaternion.Euler(-4f, 0f, 0f); // sits a touch back on the head, peak raised

        // Low and wide: a short band, then a crown that flares out so the flat top overhangs it all round.
        const float band = 0.034f, rise = 0.04f, flare = 1.34f;
        Part(cap, "Band", Frustum("CaptainCap_Band", r, r * 1.01f, band, false), mats["visor"], Vector3.zero, Quaternion.identity, Vector3.one);
        Part(cap, "Crown", Frustum("CaptainCap_Crown", r * 1.01f, r * flare, rise, true), mats["capCloth"], new Vector3(0f, band, 0f), Quaternion.identity, Vector3.one);
        // A rolled edge where the crown meets the flat top.
        Part(cap, "Piping", Frustum("CaptainCap_Piping", r * (flare + 0.008f), r * (flare + 0.008f), 0.008f, false), mats["capCloth"], new Vector3(0f, band + rise - 0.008f, 0f), Quaternion.identity, Vector3.one);
        // Tilted down 22° about the cap's centre, then lifted so its front edge meets the bottom of the band.
        Part(cap, "Peak", Peak("CaptainCap_Peak", r * 0.98f, 0.068f, 0.007f), mats["visor"], new Vector3(0f, r * Mathf.Sin(22f * Mathf.Deg2Rad) + 0.002f, 0f), Quaternion.Euler(22f, 0f, 0f), Vector3.one);

        // Brass chin cord across the front of the band, and the badge above it.
        var cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        Part(cap, "Cord", cylinder, mats["brass"], new Vector3(0f, band * 0.3f, r * 1.005f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.006f, r * 0.6f, 0.006f));
        // The badge on the front of the crown, just above the band, tilted to lie on the flare.
        float badgeY = 0.016f;
        float slope = Mathf.Atan2(r * (flare - 1.01f), rise) * Mathf.Rad2Deg;
        Part(cap, "Badge", sphere, mats["brass"], new Vector3(0f, band + badgeY, r * (1.01f + (flare - 1.01f) * badgeY / rise) + 0.002f), Quaternion.Euler(-slope, 0f, 0f), new Vector3(0.026f, 0.02f, 0.008f));
        cap.SetParent(head, true);
    }

    static void Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    /// <summary>An open cone section from radius r0 at the bottom to r1 at height h, with an optional flat lid.</summary>
    static Mesh Frustum(string name, float r0, float r1, float h, bool lid, int n = 40)
    {
        var v = new List<Vector3>();
        var nm = new List<Vector3>();
        var uv = new List<Vector2>();
        var tri = new List<int>();
        float slope = (r0 - r1) / h;
        for (int i = 0; i <= n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            var normal = new Vector3(dir.x, slope, dir.z).normalized;
            v.Add(dir * r0); nm.Add(normal); uv.Add(new Vector2(i / (float)n, 0f));
            v.Add(dir * r1 + Vector3.up * h); nm.Add(normal); uv.Add(new Vector2(i / (float)n, 1f));
            if (i < n)
            {
                int b = i * 2;
                tri.AddRange(new[] { b, b + 2, b + 1, b + 2, b + 3, b + 1 }); // clockwise seen from outside
            }
        }
        if (lid)
        {
            int c = v.Count;
            v.Add(Vector3.up * h); nm.Add(Vector3.up); uv.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                v.Add(dir * r1 + Vector3.up * h); nm.Add(Vector3.up); uv.Add(new Vector2(0.5f + dir.x * 0.5f, 0.5f + dir.z * 0.5f));
                if (i < n) tri.AddRange(new[] { c, c + 1 + i, c + 2 + i });
            }
        }
        return Save(name, v, nm, uv, tri);
    }

    /// <summary>The peak: a crescent slab hugging the front of the band and reaching `reach` forward, `thick` thick.</summary>
    static Mesh Peak(string name, float r, float reach, float thick, int n = 24)
    {
        var outline = new List<Vector2>(); // x, z: the inner edge along the band, then the outer edge back
        const float spread = 78f * Mathf.Deg2Rad;
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Lerp(-spread, spread, i / (float)n);
            outline.Add(new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r));
        }
        for (int i = n; i >= 0; i--)
        {
            float a = Mathf.Lerp(-spread, spread, i / (float)n);
            float c = Mathf.Cos(a);
            outline.Add(new Vector2(Mathf.Sin(a) * r * 1.02f, Mathf.Cos(a) * r + reach * Mathf.Pow(c, 1.6f)));
        }

        var v = new List<Vector3>();
        var nm = new List<Vector3>();
        var uv = new List<Vector2>();
        var tri = new List<int>();
        // Top and bottom faces: a strip between the inner point i and the outer point opposite it.
        for (int side = 0; side < 2; side++)
        {
            float y = side == 0 ? thick : 0f;
            int b = v.Count;
            foreach (var p in outline) { v.Add(new Vector3(p.x, y, p.y)); nm.Add(side == 0 ? Vector3.up : Vector3.down); uv.Add(p); }
            for (int i = 0; i < n; i++)
            {
                int inA = b + i, inB = b + i + 1, outA = b + 2 * n + 1 - i, outB = b + 2 * n - i;
                if (side == 0) tri.AddRange(new[] { inA, outA, inB, inB, outA, outB });
                else tri.AddRange(new[] { inA, inB, outA, inB, outB, outA });
            }
        }
        // The outer rim.
        int rim = v.Count;
        for (int i = n + 1; i < outline.Count; i++)
        {
            var p = outline[i];
            var o = new Vector3(p.x, 0f, p.y - r * 0.5f).normalized;
            v.Add(new Vector3(p.x, 0f, p.y)); nm.Add(o); uv.Add(new Vector2(i, 0f));
            v.Add(new Vector3(p.x, thick, p.y)); nm.Add(o); uv.Add(new Vector2(i, 1f));
        }
        for (int i = 0; i < n; i++)
        {
            int b = rim + i * 2;
            tri.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
        }
        return Save(name, v, nm, uv, tri);
    }

    static Mesh Save(string name, List<Vector3> v, List<Vector3> nm, List<Vector2> uv, List<int> tri)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(v);
        mesh.SetNormals(nm);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        MovementPrototypeBuilder.EnsureFolder(GeneratedFolder);
        string path = $"{GeneratedFolder}/{name}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // ---------- The bottle ----------

    /// <summary>
    /// A brown bottle held by the neck in his right fist, the body hanging past the fingers. In the bind pose the
    /// fingers point along the arm, so "past the fingers" is the bone's own direction and follows any animation.
    /// </summary>
    static Transform BuildBottle(GameObject root, Animator animator, Dictionary<string, Transform> bonesByName)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(BottlePath);
        Transform hand = animator.avatar != null && animator.avatar.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        if (hand == null) hand = bonesByName.Values.FirstOrDefault(t => t.name.ToLowerInvariant() is "hand_r" or "hand.r" or "righthand");
        if (model == null || hand == null) { Debug.LogWarning("[Captain] No bottle or right hand found"); return null; }

        // Fingers: from the hand towards its middle finger (else its first child).
        var finger = hand.Cast<Transform>().FirstOrDefault(c => c.name.ToLowerInvariant().Contains("middle")) ?? (hand.childCount > 0 ? hand.GetChild(0) : null);
        Vector3 along = finger != null ? (finger.position - hand.position).normalized : -hand.up;

        var bottle = (GameObject)Object.Instantiate(model, root.transform, false);
        bottle.name = "Bottle";
        foreach (var c in bottle.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        foreach (var r in bottle.GetComponentsInChildren<Renderer>())
            r.sharedMaterials = r.sharedMaterials.Select(m => StylizedLook.ForKayKit(m) ?? m).ToArray();

        // 26 cm tall, its base-to-neck axis pointing back up the arm from the fist.
        var b = bottle.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
        float scale = 0.26f / Mathf.Max(0.01f, b.size.y);
        bottle.transform.localScale = Vector3.one * scale;
        bottle.transform.rotation = Quaternion.FromToRotation(Vector3.up, -along);
        // The neck (top of the bottle) sits in the fist, a little past the knuckles.
        bottle.transform.position = hand.position + along * 0.075f - bottle.transform.up * (0.26f * 0.88f);
        bottle.transform.SetParent(hand, true);
        return bottle.transform;
    }

    // ---------- Preview ----------

    [MenuItem("Port/Samprity/Preview Captain")]
    public static void PreviewMenu() => PreviewBatch();

    static int previewStep;
    static float previewAt;
    static GameObject previewCaptain;
    static readonly List<GameObject> previewCrew = new List<GameObject>();
    static bool exitWhenDone;
    static EnterPlayModeOptions savedOptions;
    static bool savedOptionsEnabled;

    /// <summary>Plays an empty studio scene with the Captain idling between two crew, and photographs him.</summary>
    public static void PreviewBatch()
    {
        exitWhenDone = Application.isBatchMode;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        savedOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        savedOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        previewStep = 0;
        EditorApplication.update += PreviewTick;
        EditorApplication.EnterPlaymode();
    }

    static void PreviewTick()
    {
        if (!EditorApplication.isPlaying) return;
        float now = Time.realtimeSinceStartup;
        switch (previewStep)
        {
            case 0:
                Stage();
                previewAt = now;
                previewStep = 1;
                break;
            case 1:
                if (now - previewAt < 2.5f) return; // let the idle settle in
                System.IO.Directory.CreateDirectory("Logs");
                var headBone = previewCaptain.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
                var face = headBone != null ? headBone.position + Vector3.up * 0.06f : new Vector3(0f, 1.7f, 0f);
                Shot("1_lineup", new Vector3(0f, 1.0f, 0f), 0f, 5.4f, 0.35f, 38f);
                foreach (var c in previewCrew) c.SetActive(false);
                Shot("2_front", new Vector3(0f, 0.95f, 0f), 0f, 3.1f, 0.25f, 40f);
                Shot("3_three_quarter", new Vector3(0f, 0.95f, 0f), 35f, 3.1f, 0.25f, 40f);
                Shot("4_side", new Vector3(0f, 0.95f, 0f), 90f, 3.1f, 0.25f, 40f);
                Shot("5_back", new Vector3(0f, 0.95f, 0f), 180f, 3.1f, 0.25f, 40f);
                Shot("6_face", face, 20f, 0.95f, 0.02f, 32f);
                Shot("7_face_side", face, 70f, 0.95f, 0.02f, 32f);
                Debug.Log("[Captain] Preview written to Logs/CaptainPreview_*.png");
                EditorApplication.update -= PreviewTick;
                EditorSettings.enterPlayModeOptionsEnabled = savedOptionsEnabled;
                EditorSettings.enterPlayModeOptions = savedOptions;
                EditorApplication.ExitPlaymode();
                if (exitWhenDone) EditorApplication.delayCall += () => EditorApplication.Exit(0);
                break;
        }
    }

    static void Stage()
    {
        var key = new GameObject("Key").AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 1.5f;
        key.color = new Color(1f, 0.95f, 0.86f);
        key.shadows = LightShadows.Soft;
        key.transform.rotation = Quaternion.Euler(32f, 145f, 0f);
        var fill = new GameObject("Fill").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.45f;
        fill.color = new Color(0.62f, 0.72f, 1f);
        fill.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.Euler(18f, -130f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.36f, 0.37f, 0.42f);
        RenderSettings.fog = false;

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.transform.localScale = Vector3.one * 2f;
        var deck = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Ship/Ship_DarkWood.mat");
        if (deck != null) floor.GetComponent<Renderer>().sharedMaterial = deck;

        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.11f, 0.13f, 0.19f);
        cam.nearClipPlane = 0.05f;

        previewCaptain = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        previewCaptain.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        previewCrew.Clear();
        foreach (var (seat, x) in new[] { (1, -1.15f), (3, 1.15f) })
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(CrewCharacterBuilder.CrewPrefabPath(seat));
            if (p == null) continue;
            var c = (GameObject)Object.Instantiate(p, new Vector3(x, 0f, 0.25f), Quaternion.Euler(0f, 180f, 0f));
            previewCrew.Add(c);
        }
    }

    /// <summary>Orbits the camera `yaw` degrees around the target (0 = in front of him) and saves Logs/CaptainPreview_name.png.</summary>
    static void Shot(string name, Vector3 target, float yaw, float distance, float height, float fov)
    {
        var cam = Camera.main;
        cam.fieldOfView = fov;
        cam.transform.position = target + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, height, -distance);
        cam.transform.LookAt(target);
        var rt = new RenderTexture(1600, 900, 24);
        cam.targetTexture = rt;
        cam.Render();
        cam.Render();
        cam.targetTexture = null;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes($"Logs/CaptainPreview_{name}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }
}
