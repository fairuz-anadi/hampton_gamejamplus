using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds Samprity's movement test scene: a greybox deck, a narrow corridor, ship-sized obstacles,
/// a KayKit crew member with ThirdPersonMover, and the AutoFollowCamera. Owner: Samprity.
///   In the Editor:  Port > Samprity > Build Movement Prototype
///   Headless:       Unity.exe -batchmode -projectPath . -executeMethod MovementPrototypeBuilder.BuildBatch -quit
/// Running it again rebuilds the scene from scratch. It never touches Ship.unity.
/// </summary>
public static class MovementPrototypeBuilder
{
    public const string ScenePath = "Assets/_Project/Scenes/Sandbox/Samprity_Movement.unity";
    const string CharacterFbx = "Assets/_Project/Art/ThirdParty/KayKit/Characters/Rogue.fbx";
    const string ControllerPath = "Assets/_Project/Art/Animations/CrewLocomotion.controller";
    const string MaterialFolder = "Assets/_Project/Art/Materials/Prototype";
    const float CrewHeight = 1.7f;

    [MenuItem("Port/Samprity/Build Movement Prototype")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        SetUpCharacterImport();
        var controller = BuildLocomotionController();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildLighting();
        BuildGreybox();
        var player = BuildPlayer(controller);
        BuildCamera(player);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[MovementPrototype] Built {ScenePath}");
    }

    public static void BuildBatch() => Build();

    // ---------- Character import and animation ----------

    static bool IsLoopClip(string name)
    {
        string n = name.ToLowerInvariant();
        return n.Contains("idle") || n.Contains("walk") || n.Contains("run");
    }

    /// <summary>Marks idle/walk/run clips as looping. KayKit ships them non-looping.</summary>
    internal static void SetUpCharacterImport()
    {
        var importer = AssetImporter.GetAtPath(CharacterFbx) as ModelImporter;
        if (importer == null) { Debug.LogError($"[MovementPrototype] Missing {CharacterFbx}"); return; }

        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;

        bool changed = false;
        foreach (var c in clips)
        {
            bool loop = IsLoopClip(c.name);
            if (c.loopTime != loop) { c.loopTime = loop; changed = true; }
        }
        if (changed || importer.clipAnimations.Length == 0)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
        Debug.Log("[MovementPrototype] Character clips: " + string.Join(", ", clips.Select(c => c.name)));
    }

    static AnimationClip FindClip(AnimationClip[] clips, params string[] preferred)
    {
        foreach (var want in preferred)
        {
            var exact = clips.FirstOrDefault(c => c.name.Equals(want, System.StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
        }
        foreach (var want in preferred)
        {
            var part = clips.FirstOrDefault(c => c.name.IndexOf(want, System.StringComparison.OrdinalIgnoreCase) >= 0);
            if (part != null) return part;
        }
        return null;
    }

    /// <summary>One blend tree on "Speed" (m/s): idle at 0, walk at 1.5, run at the mover's top speed.</summary>
    internal static AnimatorController BuildLocomotionController()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(CharacterFbx).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToArray();
        var idle = FindClip(clips, "Idle", "Unarmed_Idle");
        var walk = FindClip(clips, "Walking_A", "Walking_B", "Walk");
        var run = FindClip(clips, "Running_A", "Running_B", "Run");
        Debug.Log($"[MovementPrototype] Using idle={idle?.name ?? "none"} walk={walk?.name ?? "none"} run={run?.name ?? "none"}");

        EnsureFolder(System.IO.Path.GetDirectoryName(ControllerPath).Replace('\\', '/'));
        AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        if (idle != null) tree.AddChild(idle, 0f);
        if (walk != null) tree.AddChild(walk, 1.5f);
        if (run != null) tree.AddChild(run, 3.4f);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    // ---------- Scene ----------

    static void BuildLighting()
    {
        var sun = new GameObject("Sun");
        var light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.92f, 0.8f);
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
    }

    static void BuildGreybox()
    {
        var floorMat = Mat("Proto_Floor", new Color(0.86f, 0.80f, 0.68f));    // cream deck
        var wallMat = Mat("Proto_Wall", new Color(0.16f, 0.22f, 0.36f));      // navy
        var machineMat = Mat("Proto_Machine", new Color(0.72f, 0.56f, 0.26f)); // brass
        var crateMat = Mat("Proto_Crate", new Color(0.45f, 0.32f, 0.22f));     // wood

        var root = new GameObject("Greybox").transform;

        // Open deck, 30 x 30 m, with a 3 m wall round the edge.
        Box(root, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(30f, 0.2f, 30f), floorMat);
        Box(root, "Wall_North", new Vector3(0f, 1.5f, 15.25f), new Vector3(30.5f, 3f, 0.5f), wallMat);
        Box(root, "Wall_South", new Vector3(0f, 1.5f, -15.25f), new Vector3(30.5f, 3f, 0.5f), wallMat);
        Box(root, "Wall_East", new Vector3(15.25f, 1.5f, 0f), new Vector3(0.5f, 3f, 30.5f), wallMat);
        Box(root, "Wall_West", new Vector3(-15.25f, 1.5f, 0f), new Vector3(0.5f, 3f, 30.5f), wallMat);

        // Narrow corridor, 2.2 m wide and 12 m long, running north along x = 8.
        Box(root, "Corridor_WallA", new Vector3(6.65f, 1.5f, 2f), new Vector3(0.5f, 3f, 12f), wallMat);
        Box(root, "Corridor_WallB", new Vector3(9.35f, 1.5f, 2f), new Vector3(0.5f, 3f, 12f), wallMat);

        // A small room with a 1.6 m doorway, like a wireless cabin.
        Box(root, "Cabin_Back", new Vector3(-9f, 1.5f, 13f), new Vector3(8f, 3f, 0.5f), wallMat);
        Box(root, "Cabin_Left", new Vector3(-12.75f, 1.5f, 10f), new Vector3(0.5f, 3f, 6f), wallMat);
        Box(root, "Cabin_Right", new Vector3(-5.25f, 1.5f, 10f), new Vector3(0.5f, 3f, 6f), wallMat);
        Box(root, "Cabin_FrontL", new Vector3(-11.1f, 1.5f, 7f), new Vector3(3.8f, 3f, 0.5f), wallMat);
        Box(root, "Cabin_FrontR", new Vector3(-6.9f, 1.5f, 7f), new Vector3(3.8f, 3f, 0.5f), wallMat);

        // Ship equipment stand-ins.
        Box(root, "Boiler", new Vector3(-7f, 1.5f, -2f), new Vector3(3f, 3f, 3f), machineMat);
        Box(root, "Pillar", new Vector3(-2.5f, 1.5f, -5f), new Vector3(0.8f, 3f, 0.8f), wallMat);
        Box(root, "Engine", new Vector3(3.5f, 1.25f, -9f), new Vector3(2f, 2.5f, 4f), machineMat);
        Box(root, "Crate_A", new Vector3(-4f, 0.5f, 3f), Vector3.one, crateMat);
        Box(root, "Crate_B", new Vector3(-3f, 0.5f, 3.4f), Vector3.one, crateMat);
        Box(root, "Crate_C", new Vector3(-3.5f, 1.5f, 3.2f), Vector3.one, crateMat);
    }

    internal static GameObject BuildPlayer(AnimatorController controller)
    {
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 0f, -12f);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFbx);
        var crew = AssetDatabase.LoadAssetAtPath<GameObject>(CrewCharacterBuilder.CrewPrefabPath(1));
        float height = CrewHeight;
        if (crew != null)
        {
            // Quaternius crew at their real (human) height; the controller is sized to fit them.
            var body = (GameObject)PrefabUtility.InstantiatePrefab(crew, player.transform);
            body.name = "Body";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            height = Mathf.Clamp(RendererBounds(body).size.y, 1.5f, 2.1f);
        }
        else if (model != null)
        {
            var body = (GameObject)PrefabUtility.InstantiatePrefab(model, player.transform);
            body.name = "Body";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;

            // Scale the model to crew height so the camera numbers mean the same thing for every model.
            // KayKit bodies carry their weapons as extra meshes (Knife, 1H_Crossbow...). Crew keep only "Rogue_*" parts.
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
                if (!r.name.StartsWith(model.name + "_")) r.gameObject.SetActive(false);

            var b = RendererBounds(body);
            if (b.size.y > 0.01f) body.transform.localScale = Vector3.one * (CrewHeight / b.size.y);
            height = CrewHeight;

            // Not "??": Unity's fake-null objects slip past it.
            if (!body.TryGetComponent(out Animator animator)) animator = body.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
        }
        else
        {
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(capsule.GetComponent<Collider>());
            capsule.transform.SetParent(player.transform, false);
            capsule.transform.localPosition = new Vector3(0f, CrewHeight / 2f, 0f);
            capsule.transform.localScale = new Vector3(0.7f, CrewHeight / 2f, 0.7f);
        }

        var cc = player.AddComponent<CharacterController>();
        cc.height = height;
        cc.radius = 0.32f;
        cc.center = new Vector3(0f, height / 2f, 0f);
        cc.skinWidth = 0.04f;
        cc.stepOffset = 0.3f;

        var mover = player.AddComponent<ThirdPersonMover>();
        mover.animator = player.GetComponentInChildren<Animator>();
        return player;
    }

    internal static void BuildCamera(GameObject player)
    {
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 200f;
        camGo.AddComponent<AudioListener>();
        var urp = cam.GetUniversalAdditionalCameraData();
        urp.renderPostProcessing = true;
        urp.renderShadows = true;

        var follow = camGo.AddComponent<AutoFollowCamera>();
        var mover = player.GetComponent<ThirdPersonMover>();
        follow.target = player.transform;
        follow.mover = mover;
        mover.followCamera = follow;
        follow.SnapToTarget();
    }

    // ---------- Helpers ----------

    static void Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.isStatic = true;
    }

    internal static Material Mat(string name, Color color)
    {
        EnsureFolder(MaterialFolder);
        string path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Bounds RendererBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    internal static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
