using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Builds the game's opening into MainMenu.unity: the ship deck (prefab instance, so deck rebuilds flow in), the
/// cinematic camera and its route, scenery crew, the Captain's lines, ambience and the menu. Owner: Samprity.
///   In the Editor:  Port > Samprity > Build Opening
///   Headless:       Unity.exe -batchmode -quit -projectPath . -executeMethod OpeningSceneBuilder.BuildBatch
/// Also makes CrewCinematic.controller, Resources/CrewVisualSet (crew models in Ship) and Resources/UiTheme.
/// Rebuilding overwrites MainMenu.unity; tune the route and lines here, not in the scene.
///
/// Port > Samprity > Play From Opening (on by default on the machine that built it) makes pressing Play in Ship.unity
/// start at MainMenu, like the real game. Off: Play in Ship goes straight to gameplay as before.
/// </summary>
public static class OpeningSceneBuilder
{
    public const string MenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    public const string ShipScenePath = "Assets/_Project/Scenes/Ship.unity";
    const string ControllerPath = "Assets/_Project/Art/Animations/CrewCinematic.controller";
    const string AnimFbx = "Assets/_Project/Art/ThirdParty/Quaternius/Animations/UAL1_Standard.fbx";
    const string CrewPrefabs = "Assets/_Project/Prefabs/Crew";
    const string CrewResources = "Assets/_Project/Prefabs/Crew/Resources";
    const string UiResources = "Assets/_Project/Prefabs/UI/Resources";
    const string Ui = "Assets/_Project/Art/UI";
    const string Audio = "Assets/_Project/Audio";
    const string PlayFromOpeningKey = "Port.PlayFromOpening";

    /// <summary>Where seat 1 starts in Ship (the deck's Spawn_1) and which way they face: at the furnace.</summary>
    static readonly Vector3 Spawn1 = new Vector3(-0.7f, 0.05f, 6.2f);
    const float SpawnYaw = 180f;

    [MenuItem("Port/Samprity/Build Opening")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var controller = BuildController();
        BuildResources(controller);
        ImportAudio();

        var scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);

        var deckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShipDeckBuilder.PrefabPath);
        var deck = (GameObject)PrefabUtility.InstantiatePrefab(deckPrefab, scene);
        deck.GetComponent<PartMood>()?.ApplyImmediate(1);

        var crewRoot = new GameObject("CinematicCrew").transform;
        BuildCrew(crewRoot, controller);

        var cam = BuildCamera();
        var director = new GameObject("Opening");
        var path = director.AddComponent<CinematicPath>();
        path.keys = Route(FinalPose());
        var menu = director.AddComponent<OpeningMenu>();
        menu.titleFont = Font("CrimsonText-Bold");
        menu.bodyFont = Font("CrimsonText-SemiBold");
        menu.italicFont = Font("CrimsonText-SemiBoldItalic");
        menu.diamond = Sprite("diamond");
        menu.speakerIcon = Sprite("icon_speaker");
        menu.titleLogo = TitleLogo();
        menu.keycap = Sprite("keycap");
        menu.disc = Sprite("disc");
        menu.hoverSound = Clip("SFX/UI_Hover");
        menu.confirmSound = Clip("SFX/UI_Confirm");

        var d = director.AddComponent<OpeningDirector>();
        d.path = path;
        d.cam = cam;
        d.menu = menu;
        d.lines = Lines();
        d.ocean = Source(director.transform, "Ocean", Clip("SFX/Intro_Ocean"), AudioCategory.Kind.Effects);
        d.music = Source(director.transform, "Music", Clip("Music/Intro_Theme"), AudioCategory.Kind.Music);
        d.engine = Source(director.transform, "EngineHum", Clip("SFX/Intro_EngineHum"), AudioCategory.Kind.Effects);
        d.footsteps = Source(director.transform, "Footsteps", Clip("SFX/Intro_Footsteps"), AudioCategory.Kind.Effects);
        d.voice = Source(director.transform, "Voice", null, AudioCategory.Kind.Effects);
        d.oneShots = Source(director.transform, "OneShots", null, AudioCategory.Kind.Effects);
        d.horn = Clip("SFX/Intro_Horn");
        d.crackle = Clip("SFX/Intro_SpeakerCrackle");
        d.menuAt = path.Duration + 0.5f;
        d.letterboxOutAt = path.Duration - 1.5f;

        // Start the camera where the shot starts, so the saved scene isn't framed on nothing.
        path.Evaluate(0f, out var p0, out var r0, out var f0);
        cam.transform.SetPositionAndRotation(p0, r0);
        cam.fieldOfView = f0;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, MenuScenePath);
        EnsureBuildOrder();
        if (!EditorPrefs.HasKey(PlayFromOpeningKey)) EditorPrefs.SetBool(PlayFromOpeningKey, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Opening] Built {MenuScenePath}: {path.keys.Count} camera keys, {path.Duration:0.0}s, {d.lines.Count} lines");
    }

    /// <summary>Rebuilds the deck (so the prow, wake and foam exist) and then the opening.</summary>
    public static void BuildBatch()
    {
        ShipDeckBuilder.Build();
        Build();
    }

    // ---------- The route ----------

    /// <summary>The pose Ship's follow camera snaps to for seat 1 standing on Spawn_1, found by asking it.</summary>
    static (Vector3 pos, Quaternion rot, float fov) FinalPose()
    {
        Physics.SyncTransforms();
        var target = new GameObject("FinalPoseTarget");
        target.transform.SetPositionAndRotation(Spawn1, Quaternion.Euler(0f, SpawnYaw, 0f));
        var camGo = new GameObject("FinalPoseCamera", typeof(Camera));
        var follow = camGo.AddComponent<AutoFollowCamera>();
        follow.target = target.transform;
        follow.SnapToTarget();
        var pose = (camGo.transform.position, camGo.transform.rotation, follow.fieldOfView);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(target);
        return pose;
    }

    static CinematicPath.Key K(float t, Vector3 pos, Vector3 look, float fov, float roll = 0f, bool hold = false) =>
        new CinematicPath.Key { time = t, position = pos, lookAt = look, fov = fov, roll = roll, hold = hold };

    /// <summary>
    /// Bow is +Z. A: drone high off the port quarter, closing on the ship. B: down past the funnel and the mast to
    /// ahead of the bow. C: onto the foredeck at head height and aft through the bridge and wireless room doorways.
    /// D: down the corridor, slowing as it comes up behind the newcomer at the furnace, into the follow camera's pose.
    /// </summary>
    static List<CinematicPath.Key> Route((Vector3 pos, Quaternion rot, float fov) final)
    {
        var finalLook = final.pos + final.rot * Vector3.forward * 6f;
        // The ship is twice its first length (ShipLayout): the aerial shots aim half the promenade further forward, the
        // fly-through keeps its rooms (P moves them forward) and gets a run down the promenade before the corridor.
        var mid = new Vector3(0f, 0f, ShipLayout.Extra * 0.5f);
        Vector3 P(Vector3 v) => ShipLayout.P(v);
        return new List<CinematicPath.Key>
        {
            // A: a voyage begins
            K(0f, new Vector3(-58f, 48f, -72f) + mid, new Vector3(0f, 0f, 26f) + mid, 40f),
            K(6f, new Vector3(-48f, 40f, -46f) + mid, new Vector3(0f, 0f, 22f) + mid, 41f, -3f),
            K(12f, new Vector3(-38f, 31f, -20f) + mid, new Vector3(0f, 0.5f, 19f) + mid, 42f, -4f),
            // B: the ship comes alive
            K(17f, new Vector3(-27f, 20f, 1f) + mid, new Vector3(0f, 0.5f, 17f) + mid, 44f, -2.5f),
            K(21f, P(new Vector3(-13f, 13f, 23f)), P(new Vector3(0f, 0.5f, 24f)), 46f, -1f),
            K(25f, P(new Vector3(2.5f, 8.5f, 47f)), P(new Vector3(0f, 1.5f, 32f)), 48f, 1.5f),
            // C: entering the ship
            K(29f, P(new Vector3(1.4f, 3.1f, 39f)), P(new Vector3(0.9f, 1.8f, 28f)), 50f),
            // Starboard of the wheel: clear of its status lamp (x 0, 2.35 m up) and above the telegraph (x 2.2).
            K(32.5f, P(new Vector3(1.25f, 2.45f, 33f)), P(new Vector3(0.7f, 1.8f, 24f)), 52f),
            K(35.5f, P(new Vector3(1.4f, 2.6f, 27.5f)), P(new Vector3(0.6f, 1.8f, 18f)), 52f),
            K(38.5f, P(new Vector3(1.0f, 2.3f, 21f)), new Vector3(0.3f, 1.8f, 30f), 51f),
            // Down the promenade toward the corridor.
            K(42f, new Vector3(0.8f, 2.4f, ShipLayout.InsertAt + ShipLayout.Extra * 0.5f), new Vector3(0f, 1.6f, 12f), 50f),
            K(46f, new Vector3(0.6f, 2.4f, 16.2f), new Vector3(-0.3f, 1.5f, 6f), 49f),
            // D: the player is introduced
            K(50f, new Vector3(0.0f, 2.7f, 13.6f), new Vector3(-0.6f, 1.3f, 5.6f), 47f),
            K(54f, new Vector3(-0.5f, 3.05f, 11.9f), Vector3.Lerp(new Vector3(-0.7f, 1.2f, 5.4f), finalLook, 0.6f), 45.5f),
            K(57f, final.pos, finalLook, final.fov, 0f, true),
        };
    }

    static List<OpeningDirector.Line> Lines()
    {
        const string captain = "THE CAPTAIN";
        const string speaker = "THE CAPTAIN  ·  SHIP'S SPEAKER";
        var lines = new List<OpeningDirector.Line>
        {
            L("A1", 2.6f, captain, "Every great voyage begins with a promise."),
            L("A2", 6.8f, captain, "A promise of discovery. Of glory.\nOf reaching somewhere no one has reached before."),
            L("B1", 13.6f, captain, "Welcome aboard. The finest vessel ever built."),
            L("B2", 18.2f, captain, "A ship that fears neither storm nor sea."),
            L("C1", 26.2f, captain, "Every hand has a purpose. Every order has a reason."),
            L("C2", 31.6f, captain, "And every one of you has a part to play."),
            L("D1", 44.4f, speaker, "One last thing, newcomer.\nWhen the time comes, I expect you to trust me.", true),
            L("D2", 50.4f, speaker, "Even when the others don't.", true),
        };
        return lines;
    }

    static OpeningDirector.Line L(string id, float start, string speaker, string text, bool overSpeaker = false)
    {
        // A recording dropped in Audio/Voice as Intro_<id> (wav/ogg/mp3) is picked up on the next build.
        var clip = AssetDatabase.FindAssets($"Intro_{id} t:AudioClip", new[] { $"{Audio}/Voice" })
            .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
        return new OpeningDirector.Line { id = id, start = start, speaker = speaker, text = text, clip = clip, overSpeaker = overSpeaker };
    }

    // ---------- Scenery crew ----------

    static void BuildCrew(Transform root, AnimatorController controller)
    {
        // Seat 1 (the newcomer) and the three crewmates exactly where Ship spawns the player and the bots.
        Actor(root, 1, "Newcomer", Spawn1, SpawnYaw, controller, "Locomotion");
        Actor(root, 2, "Crewmate_2", new Vector3(-2.4f, 0.05f, 6.3f), SpawnYaw, controller, "Talking");
        Actor(root, 3, "Crewmate_3", new Vector3(0.95f, 0.05f, 6.0f), SpawnYaw, controller, "Locomotion");
        Actor(root, 4, "Crewmate_4", new Vector3(2.6f, 0.05f, 6.3f), SpawnYaw, controller, "Locomotion");

        // The working ship: what the drone sees from above and the camera passes on its way through.
        Actor(root, 3, "Stoker", new Vector3(4.6f, 0.05f, 4.2f), -51f, controller, "Push");
        Actor(root, 2, "Rigger", new Vector3(-3.0f, 0.05f, 13.9f), 200f, controller, "Fixing");
        var porter = Actor(root, 4, "Porter", new Vector3(3.3f, 0.05f, 9.6f), 0f, controller, "Locomotion");
        Walk(porter, 1.2f, 0.4f, new Vector3(3.3f, 0.05f, 9.6f), new Vector3(3.3f, 0.05f, 15.2f), new Vector3(2.4f, 0.05f, 19f));
        Crate(porter);
        // Forward of the promenade: same spots in their rooms, moved forward with them (ShipLayout).
        Actor(root, 1, "Operator", ShipLayout.P(new Vector3(-3.7f, 0.05f, 21.3f)), -90f, controller, "Interact");
        Actor(root, 4, "Navigator", ShipLayout.P(new Vector3(-3.25f, 0.05f, 26.1f)), -90f, controller, "Interact");
        Actor(root, 2, "Helmsman", ShipLayout.P(new Vector3(0f, 0.05f, 28.25f)), 0f, controller, "Locomotion");
        var officer = Actor(root, 3, "Officer", ShipLayout.P(new Vector3(-3.7f, 0.05f, 33.0f)), 90f, controller, "Locomotion");
        Walk(officer, 1.0f, 2.5f, ShipLayout.P(new Vector3(-3.7f, 0.05f, 33.0f)), ShipLayout.P(new Vector3(-1.3f, 0.05f, 33.0f)));
        Actor(root, 1, "Lookout", ShipLayout.P(new Vector3(2.7f, 0.05f, 35.1f)), 10f, controller, "Torch");
    }

    static CinematicCrew Actor(Transform root, int seat, string name, Vector3 pos, float yaw, AnimatorController controller, string action)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CrewPrefabs}/Crew_{seat}.prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        var anim = go.GetComponentInChildren<Animator>();
        if (anim != null) { anim.runtimeAnimatorController = controller; anim.applyRootMotion = false; }
        var crew = go.AddComponent<CinematicCrew>();
        crew.action = action;
        return crew;
    }

    static void Walk(CinematicCrew crew, float speed, float offset, params Vector3[] points)
    {
        crew.waypoints = points;
        crew.walkSpeed = speed;
        crew.timeOffset = offset;
    }

    /// <summary>A small crate on the right shoulder: supplies going forward.</summary>
    static void Crate(CinematicCrew crew)
    {
        var anim = crew.GetComponentInChildren<Animator>();
        var shoulder = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightShoulder) : null;
        var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(crate.GetComponent<Collider>());
        crate.name = "Crate";
        crate.transform.SetParent(shoulder != null ? shoulder : crew.transform, false);
        crate.transform.position = crew.transform.position + crew.transform.rotation * new Vector3(0.12f, 1.72f, -0.02f);
        crate.transform.rotation = crew.transform.rotation * Quaternion.Euler(0f, 8f, 4f);
        crate.transform.localScale = Vector3.one;
        var s = crate.transform.lossyScale;
        crate.transform.localScale = new Vector3(0.42f / s.x, 0.3f / s.y, 0.5f / s.z);
        crate.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Stylized/KK_Props.mat");
    }

    // ---------- Camera, sound ----------

    static Camera BuildCamera()
    {
        var go = new GameObject("OpeningCamera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
        var cam = go.GetComponent<Camera>();
        cam.nearClipPlane = 0.08f;
        cam.farClipPlane = 1500f;
        cam.fieldOfView = 40f;
        var urp = cam.GetUniversalAdditionalCameraData();
        urp.renderPostProcessing = true;
        urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        return cam;
    }

    static AudioSource Source(Transform parent, string name, AudioClip clip, AudioCategory.Kind kind)
    {
        var go = new GameObject(name, typeof(AudioSource));
        go.transform.SetParent(parent, false);
        var src = go.GetComponent<AudioSource>();
        src.clip = clip;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.volume = 0f;
        var cat = go.AddComponent<AudioCategory>();
        cat.kind = kind;
        cat.baseVolume = name == "Voice" || name == "OneShots" ? 1f : 0f;
        return src;
    }

    static void ImportAudio()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { $"{Audio}/Music", $"{Audio}/SFX" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var s = importer.defaultSampleSettings;
            bool music = path.Contains("/Music/");
            bool longLoop = music || path.Contains("Ocean") || path.Contains("EngineHum");
            var want = longLoop ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            if (s.loadType == want && s.compressionFormat == AudioCompressionFormat.Vorbis) continue;
            s.loadType = want;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.6f;
            importer.defaultSampleSettings = s;
            importer.SaveAndReimport();
        }
    }

    // ---------- Assets ----------

    /// <summary>The crew's locomotion blend plus looping work states, for the opening's crew and Ship's crew models.</summary>
    static AnimatorController BuildController()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(AnimFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        // Exact take name: "Idle_Loop" must not pick up Crouch_Idle_Loop or Spell_Simple_Idle_Loop.
        AnimationClip Clip(string n) => clips.FirstOrDefault(c => c.name == n || c.name.EndsWith("|" + n));

        MovementPrototypeBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(ControllerPath).Replace('\\', '/'));
        AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var sm = controller.layers[0].stateMachine;

        var loco = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(Clip("Idle_Loop"), 0f);
        tree.AddChild(Clip("Walk_Loop"), 1.4f);
        tree.AddChild(Clip("Jog_Fwd_Loop"), 3.4f);
        sm.defaultState = loco;

        foreach (var (state, clip) in new[] { ("Push", "Push_Loop"), ("Fixing", "Fixing_Kneeling"), ("Interact", "Interact"),
                     ("Talking", "Idle_Talking_Loop"), ("Torch", "Idle_Torch_Loop") })
        {
            var c = Clip(clip);
            if (c == null) { Debug.LogWarning($"[Opening] Clip {clip} not found"); continue; }
            var s = sm.AddState(state);
            s.motion = c;
            // One-shot clips (kneeling repair, reaching for a dial) loop by replaying themselves.
            if (!c.isLooping)
            {
                var again = s.AddTransition(s);
                again.hasExitTime = true;
                again.exitTime = 0.96f;
                again.duration = 0.2f;
            }
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void BuildResources(AnimatorController controller)
    {
        MovementPrototypeBuilder.EnsureFolder(CrewResources);
        MovementPrototypeBuilder.EnsureFolder(UiResources);

        var set = LoadOrCreate<CrewVisualSet>($"{CrewResources}/CrewVisualSet.asset");
        set.crew = Enumerable.Range(1, 4).Select(i => AssetDatabase.LoadAssetAtPath<GameObject>($"{CrewPrefabs}/Crew_{i}.prefab")).ToArray();
        set.controller = controller;
        set.seatRing = Sprite("ring_thin");
        EditorUtility.SetDirty(set);

        var theme = LoadOrCreate<UiTheme>($"{UiResources}/UiTheme.asset");
        theme.title = Font("CrimsonText-Bold");
        theme.body = Font("CrimsonText-SemiBold");
        theme.italic = Font("CrimsonText-SemiBoldItalic");
        theme.typewriter = Font("SpecialElite-Regular");
        theme.diamond = Sprite("diamond");
        theme.keycap = Sprite("keycap");
        EditorUtility.SetDirty(theme);
    }

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a != null) return a;
        a = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(a, path);
        return a;
    }

    /// <summary>The title lockup from Tools/make_title_logo.py, imported as a crisp UI sprite.</summary>
    static Sprite TitleLogo()
    {
        const string path = Ui + "/Title/TitleLogo.png";
        if (AssetImporter.GetAtPath(path) is TextureImporter ti &&
            (ti.textureType != TextureImporterType.Sprite || ti.mipmapEnabled || ti.maxTextureSize < 2048))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Font Font(string name) => AssetDatabase.LoadAssetAtPath<Font>($"{Ui}/Fonts/{name}.ttf");
    static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Ui}/Sprites/{name}.png");
    static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Audio}/{name}.wav");

    /// <summary>MainMenu first, so a build opens on the opening.</summary>
    static void EnsureBuildOrder()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        int i = scenes.FindIndex(s => s.path == MenuScenePath);
        if (i == 0) return;
        if (i > 0) scenes.RemoveAt(i);
        scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------- Play from the opening ----------

    [MenuItem("Port/Samprity/Play From Opening")]
    static void TogglePlayFromOpening() => EditorPrefs.SetBool(PlayFromOpeningKey, !EditorPrefs.GetBool(PlayFromOpeningKey, false));

    [MenuItem("Port/Samprity/Play From Opening", true)]
    static bool TogglePlayFromOpeningValidate()
    {
        Menu.SetChecked("Port/Samprity/Play From Opening", EditorPrefs.GetBool(PlayFromOpeningKey, false));
        return true;
    }

    [InitializeOnLoadMethod]
    static void HookPlayButton()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingEditMode) return;
        // Only when pressing Play in Ship.unity: sandboxes and other scenes play as they are.
        bool fromShip = EditorSceneManager.GetActiveScene().path == ShipScenePath;
        bool on = EditorPrefs.GetBool(PlayFromOpeningKey, false) && fromShip && !Application.isBatchMode;
        EditorSceneManager.playModeStartScene = on ? AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) : null;
        if (on) Debug.Log("[Opening] Playing from MainMenu (Port > Samprity > Play From Opening to turn off).");
    }
}
