using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds the 4 crew prefabs from Quaternius' CC0 kits: a Peasant outfit (shirt, trousers, boots), a head cut out of the
/// base body, eyes, eyebrows and a hairstyle, all on one humanoid skeleton, animated by the Universal Animation Library.
/// Owner: Samprity.
///   In the Editor:  Port > Samprity > Build Crew Characters
///   Headless:       Unity.exe -batchmode -projectPath . -executeMethod CrewCharacterBuilder.BuildBatch -quit
/// Output: Prefabs/Crew/Crew_1..4.prefab and Art/Animations/CrewLocomotion_Human.controller ("Speed" float in m/s).
/// </summary>
public static class CrewCharacterBuilder
{
    const string Q = "Assets/_Project/Art/ThirdParty/Quaternius";
    const string GeneratedFolder = "Assets/_Project/Art/Characters/Generated";
    const string MaterialFolder = "Assets/_Project/Art/Materials/Crew";
    const string PrefabFolder = "Assets/_Project/Prefabs/Crew";
    public const string ControllerPath = "Assets/_Project/Art/Animations/CrewLocomotion_Human.controller";
    const string AnimFbx = Q + "/Animations/UAL1_Standard.fbx";

    struct Look
    {
        public bool female;
        public bool shirtB;
        public string hair, extraHair;
        public Color hairColor;
    }

    // Seat 1-4. Two shirt colours, mixed hair, so crew are told apart at a glance.
    static readonly Look[] Looks =
    {
        new Look { female = false, shirtB = false, hair = "Hair_SimpleParted", hairColor = new Color(0.30f, 0.19f, 0.11f) },
        new Look { female = true, shirtB = false, hair = "Hair_Buns", hairColor = new Color(0.55f, 0.24f, 0.12f) },
        new Look { female = false, shirtB = true, hair = "Hair_Buzzed", extraHair = "Hair_Beard", hairColor = new Color(0.10f, 0.08f, 0.07f) },
        new Look { female = true, shirtB = true, hair = "Hair_BuzzedFemale", hairColor = new Color(0.72f, 0.58f, 0.36f) },
    };

    public static string CrewPrefabPath(int seat) => $"{PrefabFolder}/Crew_{seat}.prefab";

    [MenuItem("Port/Samprity/Build Crew Characters")]
    public static void Build()
    {
        ConfigureImports();
        var controller = BuildController();
        var mats = BuildMaterials();
        var heads = new Dictionary<bool, Mesh>
        {
            [false] = CutHead(Q + "/Characters/Superhero_Male_FullBody.fbx", "Head_Male"),
            [true] = CutHead(Q + "/Characters/Superhero_Female_FullBody.fbx", "Head_Female"),
        };
        MovementPrototypeBuilder.EnsureFolder(PrefabFolder);
        for (int i = 0; i < Looks.Length; i++) BuildCrew(i + 1, Looks[i], controller, mats, heads);
        AssetDatabase.SaveAssets();
        Debug.Log("[Crew] Built 4 crew prefabs in " + PrefabFolder);
    }

    public static void BuildBatch() => Build();

    /// <summary>Renders the 4 crew side by side to Logs/CrewLineup.png (bind pose; animators don't run in edit mode).</summary>
    public static void PreviewBatch()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.3f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
        RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.transform.localScale = Vector3.one * 2f;
        for (int seat = 1; seat <= 4; seat++)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(CrewPrefabPath(seat));
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
            go.transform.position = new Vector3((seat - 2.5f) * 1.6f, 0f, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }
        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.transform.position = new Vector3(0f, 1.35f, -5.6f);
        cam.transform.LookAt(new Vector3(0f, 1.0f, 0f));
        cam.fieldOfView = 40f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.14f, 0.2f);
        var rt = new RenderTexture(1600, 900, 24);
        cam.targetTexture = rt;
        cam.Render();
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes("Logs/CrewLineup.png", tex.EncodeToPNG());

        // Close-up of seat 1's head to check the neck seam and hair fit.
        cam.transform.position = new Vector3(-2.4f, 1.62f, -1.1f);
        cam.transform.LookAt(new Vector3(-2.4f, 1.55f, 0f));
        cam.Render();
        cam.Render();
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes("Logs/CrewHead.png", tex.EncodeToPNG());
        EditorApplication.Exit(0);
    }

    // ---------- Import settings (Quaternius' Unity setup: bake axis conversion, Humanoid, loop *_Loop) ----------

    static void ConfigureImports()
    {
        var humanoids = new[]
        {
            Q + "/Outfits/Male_Peasant.fbx", Q + "/Outfits/Female_Peasant.fbx",
            Q + "/Characters/Superhero_Male_FullBody.fbx", Q + "/Characters/Superhero_Female_FullBody.fbx", AnimFbx,
        };
        foreach (var path in humanoids)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            bool dirty = false;
            if (!imp.bakeAxisConversion) { imp.bakeAxisConversion = true; dirty = true; }
            if (imp.animationType != ModelImporterAnimationType.Human)
            {
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                dirty = true;
            }
            if (path == AnimFbx)
            {
                var clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
                foreach (var c in clips)
                {
                    bool loop = c.name.EndsWith("_Loop");
                    if (c.loopTime != loop) { c.loopTime = loop; dirty = true; }
                    // Keep the character's own height and facing; the mover drives position.
                    c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true;
                    c.keepOriginalOrientation = c.keepOriginalPositionY = c.keepOriginalPositionXZ = true;
                }
                imp.clipAnimations = clips;
                dirty = true;
            }
            if (dirty) imp.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            Debug.Log($"[Crew] {System.IO.Path.GetFileName(path)}: avatar {(avatar != null && avatar.isValid && avatar.isHuman ? "OK" : "INVALID")}");
        }

        foreach (var guid in AssetDatabase.FindAssets("_Normal t:Texture2D", new[] { Q + "/Textures" }))
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            if (ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
        }
    }

    static AnimatorController BuildController()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(AnimFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        AnimationClip Clip(string n) => clips.FirstOrDefault(c => c.name.EndsWith(n));
        var idle = Clip("Idle_Loop");
        var walk = Clip("Walk_Loop");
        var jog = Clip("Jog_Fwd_Loop");
        Debug.Log($"[Crew] Locomotion clips: idle={idle?.name} walk={walk?.name} jog={jog?.name}");

        MovementPrototypeBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(ControllerPath).Replace('\\', '/'));
        AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        if (idle != null) tree.AddChild(idle, 0f);
        if (walk != null) tree.AddChild(walk, 1.4f);
        if (jog != null) tree.AddChild(jog, 3.4f);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    // ---------- Materials ----------

    static Dictionary<string, Material> BuildMaterials()
    {
        Texture2D T(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Q}/Textures/{n}.png");
        return new Dictionary<string, Material>
        {
            ["skinMale"] = Mat("Crew_Skin_Male", T("T_Superhero_Male_Dark"), T("T_Superhero_Male_Normal"), 0.35f),
            ["skinFemale"] = Mat("Crew_Skin_Female", T("T_Superhero_Female_Dark_BaseColor"), T("T_Superhero_Female_Normal"), 0.35f),
            ["armsMale"] = Mat("Crew_Arms_Male", T("T_Regular_Male_Dark_BaseColor"), T("T_Regular_Male_Normal"), 0.35f),
            ["shirtA"] = TwoSided(Mat("Crew_Peasant_A", T("T_Peasant_BaseColor"), T("T_Peasant_Normal"), 0.2f)),
            ["shirtB"] = TwoSided(Mat("Crew_Peasant_B", T("T_Peasant_2_BaseColor"), T("T_Peasant_Normal"), 0.2f)),
            ["hair1"] = Mat("Crew_Hair_1", T("T_Hair_1_BaseColor"), T("T_Hair_1_Normal"), 0.3f),
            ["hair2"] = Mat("Crew_Hair_2", T("T_Hair_2_BaseColor"), T("T_Hair_2_Normal"), 0.3f),
        };
    }

    /// <summary>Clothes show their inside through the open collar and cuffs instead of a see-through hole.</summary>
    static Material TwoSided(Material m)
    {
        m.SetFloat("_Cull", 0f);
        m.doubleSidedGI = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Mat(string name, Texture2D baseMap, Texture2D normal, float smoothness)
    {
        MovementPrototypeBuilder.EnsureFolder(MaterialFolder);
        string path = $"{MaterialFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", baseMap);
        m.SetColor("_BaseColor", Color.white);
        if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- Head ----------

    /// <summary>Copies the body mesh keeping only triangles that follow the head and neck bones.</summary>
    static Mesh CutHead(string fbxPath, string name)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        var body = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .OrderByDescending(s => s.sharedMesh.vertexCount).First();
        var src = body.sharedMesh;
        var headBones = new HashSet<int>();
        for (int i = 0; i < body.bones.Length; i++)
        {
            string n = body.bones[i].name.ToLowerInvariant();
            if (n == "head" || n.StartsWith("neck")) headBones.Add(i); // the base chest is bigger than the shirts and pokes through
        }

        var weights = src.boneWeights;
        bool Keep(int v)
        {
            var w = weights[v];
            int top = w.boneIndex0; float best = w.weight0;
            if (w.weight1 > best) { top = w.boneIndex1; best = w.weight1; }
            if (w.weight2 > best) { top = w.boneIndex2; best = w.weight2; }
            if (w.weight3 > best) top = w.boneIndex3;
            return headBones.Contains(top);
        }

        var mesh = Object.Instantiate(src);
        mesh.name = name;
        int kept = 0;
        for (int sub = 0; sub < src.subMeshCount; sub++)
        {
            var tris = src.GetTriangles(sub);
            var keep = new List<int>(tris.Length / 4);
            for (int t = 0; t < tris.Length; t += 3)
                if (Keep(tris[t]) && Keep(tris[t + 1]) && Keep(tris[t + 2]))
                    keep.AddRange(new[] { tris[t], tris[t + 1], tris[t + 2] });
            mesh.SetTriangles(keep, sub);
            kept += keep.Count / 3;
        }
        mesh.RecalculateBounds();

        MovementPrototypeBuilder.EnsureFolder(GeneratedFolder);
        string path = $"{GeneratedFolder}/{name}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        Debug.Log($"[Crew] {name}: kept {kept} head triangles (bones: {string.Join(",", headBones.Select(i => body.bones[i].name))})");
        return mesh;
    }

    // ---------- Assembly ----------

    static void BuildCrew(int seat, Look look, AnimatorController controller, Dictionary<string, Material> mats, Dictionary<bool, Mesh> heads)
    {
        string outfitPath = $"{Q}/Outfits/{(look.female ? "Female" : "Male")}_Peasant.fbx";
        string bodyPath = $"{Q}/Characters/Superhero_{(look.female ? "Female" : "Male")}_FullBody.fbx";

        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(outfitPath));
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = $"Crew_{seat}";
        var bonesByName = new Dictionary<string, Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (!bonesByName.ContainsKey(t.name)) bonesByName[t.name] = t;

        // Clothes.
        var shirt = look.shirtB ? mats["shirtB"] : mats["shirtA"];
        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var slots = smr.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
                slots[i] = slots[i] != null && slots[i].name.Contains("Regular") ? mats["armsMale"] : shirt;
            smr.sharedMaterials = slots;
        }

        // The hair textures are grey masks meant to be tinted; one tinted copy per crew member.
        var hairBase = look.female ? mats["hair2"] : mats["hair1"];
        var hairMat = Mat($"Crew_Hair_Seat{seat}", (Texture2D)hairBase.GetTexture("_BaseMap"), (Texture2D)hairBase.GetTexture("_BumpMap"), 0.3f);
        hairMat.SetColor("_BaseColor", look.hairColor * 1.7f);

        // Head, eyes and eyebrows from the base body, rebound to the outfit's skeleton.
        var body = AssetDatabase.LoadAssetAtPath<GameObject>(bodyPath);
        foreach (var src in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            bool isBody = src.sharedMesh.vertexCount > 3000;
            var go = new GameObject(isBody ? "Head" : src.name);
            go.transform.SetParent(root.transform, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = isBody ? heads[look.female] : src.sharedMesh;
            smr.bones = src.bones.Select(b => bonesByName.TryGetValue(b.name, out var t) ? t : null).ToArray();
            smr.rootBone = src.rootBone != null && bonesByName.TryGetValue(src.rootBone.name, out var rb) ? rb : root.transform;
            smr.sharedMaterials = isBody
                ? new[] { look.female ? mats["skinFemale"] : mats["skinMale"] }
                : src.sharedMaterials.Select(m => m != null && m.name.Contains("Hair") ? hairMat : m).ToArray();
            smr.updateWhenOffscreen = false;
        }

        // Hair is a static mesh modelled in place: parent it to the head bone where it sits.
        var head = bonesByName.TryGetValue("Head", out var h) ? h : root.transform;
        foreach (var hairName in new[] { look.hair, look.extraHair })
        {
            if (string.IsNullOrEmpty(hairName)) continue;
            var hairModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{Q}/Hair/{hairName}.fbx");
            if (hairModel == null) continue;
            var hair = (GameObject)Object.Instantiate(hairModel, root.transform, false);
            hair.name = hairName;
            hair.transform.SetParent(head, true);
            foreach (var r in hair.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = r.sharedMaterials.Select(_ => hairMat).ToArray();
        }

        if (!root.TryGetComponent(out Animator animator)) animator = root.AddComponent<Animator>();
        // Same skeleton as the outfit; the female outfit's own avatar fails validation, the base body's doesn't.
        animator.avatar = AssetDatabase.LoadAllAssetsAtPath(bodyPath).OfType<Avatar>().FirstOrDefault();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        PrefabUtility.SaveAsPrefabAsset(root, CrewPrefabPath(seat));
        Object.DestroyImmediate(root);
    }
}
