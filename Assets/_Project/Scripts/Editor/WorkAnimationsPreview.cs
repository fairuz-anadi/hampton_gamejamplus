using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Photographs the work loops on a crew model, so they can be checked without pressing Play. Owner: Rabib.
///   Port > Preview Work Animations   (also runs by itself once after a change to Version)
/// Writes Logs/WorkPreview/&lt;clip&gt;.png: six frames across the loop, each seen from the front-right and from the side,
/// with the shovel in hand for the shovel loop. Also renders library clips that could be borrowed from, and writes
/// Logs/WorkPreview/muscles.txt: the arm and spine muscle values of each clip at those frames.
/// </summary>
public static class WorkAnimationsPreview
{
    /// <summary>Bump to have the editor render the previews again on its next reload.</summary>
    const int Version = 1;
    const string AnimFbx = "Assets/_Project/Art/ThirdParty/Quaternius/Animations/UAL1_Standard.fbx";
    const string OutDir = "Logs/WorkPreview";
    const int Frames = 6, CellW = 220, CellH = 300;

    static readonly string[] Library =
    {
        "Idle_Loop", "Push_Loop", "PickUp_Table", "Interact", "Sword_Attack", "Spell_Simple_Shoot", "Punch_Cross",
        "Fixing_Kneeling", "Driving_Loop", "Crouch_Idle_Loop",
    };

    static readonly string[] Muscles =
    {
        "Spine Front-Back", "Spine Twist Left-Right", "Chest Front-Back", "Chest Twist Left-Right", "UpperChest Front-Back",
        "Head Nod Down-Up",
        "Left Shoulder Down-Up", "Left Shoulder Front-Back", "Left Arm Down-Up", "Left Arm Front-Back", "Left Arm Twist In-Out",
        "Left Forearm Stretch", "Left Forearm Twist In-Out", "Left Hand Down-Up",
        "Right Shoulder Down-Up", "Right Shoulder Front-Back", "Right Arm Down-Up", "Right Arm Front-Back", "Right Arm Twist In-Out",
        "Right Forearm Stretch", "Right Forearm Twist In-Out", "Right Hand Down-Up",
        "Left Upper Leg Front-Back", "Left Lower Leg Stretch", "Right Upper Leg Front-Back", "Right Lower Leg Stretch",
    };

    [InitializeOnLoadMethod]
    static void RenderIfStale()
    {
        if (Application.isBatchMode) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string stamp = Path.Combine(OutDir, "version.txt");
            if (File.Exists(stamp) && File.ReadAllText(stamp).Trim() == Version.ToString()) return;
            if (AssetDatabase.LoadAssetAtPath<WorkAnimations>("Assets/_Project/Art/Animations/Resources/WorkAnimations.asset") == null) return;
            Render();
            File.WriteAllText(stamp, Version.ToString());
        };
    }

    [MenuItem("Port/Preview Work Animations")]
    public static void Render()
    {
        Directory.CreateDirectory(OutDir);
        var crewSet = Resources.Load<CrewVisualSet>("CrewVisualSet");
        var set = Resources.Load<WorkAnimations>(WorkAnimations.ResourcePath);
        if (crewSet == null || crewSet.crew.Length == 0 || crewSet.crew[0] == null) { Debug.LogError("[WorkPreview] No crew model (Resources/CrewVisualSet)."); return; }

        var lib = AssetDatabase.LoadAllAssetsAtPath(AnimFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        AnimationClip Lib(string n) => lib.FirstOrDefault(c => c.name == n || c.name.EndsWith("|" + n));

        var shovelSource = FindDeckShovel();
        var pru = new PreviewRenderUtility();
        var log = new StringBuilder("Muscle values (Unity humanoid, -1..1) at frames 0-5 of each loop\n");
        try
        {
            pru.camera.fieldOfView = 28f;
            pru.camera.nearClipPlane = 0.05f;
            pru.camera.farClipPlane = 50f;
            pru.camera.clearFlags = CameraClearFlags.SolidColor;
            pru.camera.backgroundColor = new Color(0.32f, 0.34f, 0.38f);
            pru.lights[0].intensity = 1.3f;
            pru.lights[0].transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            pru.lights[1].intensity = 0.8f;
            pru.lights[1].transform.rotation = Quaternion.Euler(-20f, 150f, 0f);
            pru.ambientColor = new Color(0.35f, 0.35f, 0.38f);

            var actor = pru.InstantiatePrefabInScene(crewSet.crew[0]);
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var animator = actor.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) { Debug.LogError("[WorkPreview] The crew model has no humanoid Animator."); return; }
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;

            GameObject shovel = null;
            if (shovelSource != null)
            {
                shovel = Object.Instantiate(shovelSource);
                pru.AddSingleGO(shovel);
                shovel.SetActive(false);
            }

            void Sheet(AnimationClip clip, string fileName, bool withShovel)
            {
                if (clip == null) return;
                var sheet = new Texture2D(CellW * Frames, CellH * 2, TextureFormat.RGB24, false);
                var graph = PlayableGraph.Create("WorkPreview");
                var output = AnimationPlayableOutput.Create(graph, "Out", animator);
                var play = AnimationClipPlayable.Create(graph, clip);
                play.SetApplyFootIK(false);
                output.SetSourcePlayable(play);
                log.Append($"\n== {fileName} ({clip.length:0.00} s)\n");
                for (int f = 0; f < Frames; f++)
                {
                    float t = clip.length * f / Frames;
                    play.SetTime(t);
                    graph.Evaluate(0f);
                    if (shovel != null)
                    {
                        shovel.SetActive(withShovel);
                        if (withShovel) CrewVisual.PlaceShovel(shovel.transform, animator.GetBoneTransform(HumanBodyBones.RightHand), shovelSource.transform.lossyScale);
                    }
                    for (int view = 0; view < 2; view++)
                    {
                        // Front-right three-quarter, then straight from the character's right side.
                        var eye = view == 0 ? new Vector3(2.4f, 1.5f, 3.4f) : new Vector3(4.2f, 1.2f, 0f);
                        pru.camera.transform.position = eye;
                        pru.camera.transform.LookAt(new Vector3(0f, 0.95f, 0f));
                        pru.BeginStaticPreview(new Rect(0, 0, CellW, CellH));
                        pru.Render(true);
                        var shot = pru.EndStaticPreview();
                        sheet.SetPixels(f * CellW, (1 - view) * CellH, CellW, CellH, Read(shot));
                        Object.DestroyImmediate(shot);
                    }
                    log.Append($"  f{f} t={t:0.00}: ");
                    foreach (var m in Muscles)
                    {
                        var curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), m));
                        if (curve != null) log.Append($"{m}={curve.Evaluate(t):0.00}  ");
                    }
                    log.Append('\n');
                }
                graph.Destroy();
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(OutDir, fileName + ".png"), sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
            }

            if (set != null)
            {
                Sheet(set.shovel, "Work_Shovel", true);
                Sheet(set.lever, "Work_LeverPull", false);
                Sheet(set.wheel, "Work_Wheel", false);
                Sheet(set.morse, "Work_Morse", false);
                Sheet(set.repair, "Work_Repair", false);
            }
            foreach (var n in Library) Sheet(Lib(n), "Lib_" + n, false);
        }
        finally
        {
            pru.Cleanup();
        }
        File.WriteAllText(Path.Combine(OutDir, "muscles.txt"), log.ToString());
        Debug.Log($"[WorkPreview] Wrote contact sheets and muscles.txt to {Path.GetFullPath(OutDir)}");
    }

    static Color[] Read(Texture tex)
    {
        if (tex is Texture2D t2 && t2.isReadable) return t2.GetPixels();
        var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(tex, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, false);
        copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        copy.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        var px = copy.GetPixels();
        Object.DestroyImmediate(copy);
        return px;
    }

    /// <summary>The boiler room's shovel prop from the ShipDeck prefab, as the game borrows it.</summary>
    static GameObject FindDeckShovel()
    {
        var deck = AssetDatabase.LoadAssetAtPath<GameObject>(ShipDeckBuilder.PrefabPath);
        if (deck == null) return null;
        foreach (var t in deck.GetComponentsInChildren<Transform>(true))
            if (t.name == "Shovel" && t.GetComponentInChildren<Renderer>() != null) return t.gameObject;
        return null;
    }
}
