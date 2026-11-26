using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the crew's work loops (Resources/WorkAnimations) from the Quaternius UAL1 library, which has no shovel,
/// lever, wheel or Morse key animation. Owner: Rabib.
///   Port > Build Work Animations      (also runs by itself the first time the editor opens without the asset)
///   Headless: -executeMethod WorkAnimationsBuilder.BuildBatch
/// Shovel, lever, wheel and Morse are made here: the library's Idle_Loop holds the legs, hips and head steady, and the
/// spine, shoulders, arms and hands are keyed as humanoid muscles (-1..1). Repair, tap and pick-up are the library's
/// Fixing_Kneeling, Interact and PickUp_Table, copied as loops (as imported they play once and freeze).
/// Each pose below is a row of muscle values at a time in the loop, so the motion is easy to tune by eye:
/// change a number, run the menu again, press Play.
/// </summary>
public static class WorkAnimationsBuilder
{
    const string AnimFbx = "Assets/_Project/Art/ThirdParty/Quaternius/Animations/UAL1_Standard.fbx";
    const string ClipFolder = "Assets/_Project/Art/Animations/Work";
    const string AssetPath = "Assets/_Project/Art/Animations/Resources/WorkAnimations.asset";

    [InitializeOnLoadMethod]
    static void BuildIfMissing()
    {
        if (Application.isBatchMode) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<WorkAnimations>(AssetPath) == null) Build();
        };
    }

    public static void BuildBatch() => Build();

    [MenuItem("Port/Build Work Animations")]
    public static void Build()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(AnimFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        AnimationClip Lib(string n) => clips.FirstOrDefault(c => c.name == n || c.name.EndsWith("|" + n));
        var idle = Lib("Idle_Loop");
        if (idle == null) { Debug.LogError("[WorkAnimations] Idle_Loop not found in " + AnimFbx); return; }

        MovementPrototypeBuilder.EnsureFolder(ClipFolder);
        MovementPrototypeBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(AssetPath).Replace('\\', '/'));

        var set = AssetDatabase.LoadAssetAtPath<WorkAnimations>(AssetPath);
        if (set == null) { set = ScriptableObject.CreateInstance<WorkAnimations>(); AssetDatabase.CreateAsset(set, AssetPath); }

        set.shovel = Save(Posed("Work_Shovel", idle, 1.5f, Shovel));
        set.lever = Save(Posed("Work_LeverPull", idle, 1.0f, LeverPull));
        set.wheel = Save(Posed("Work_Wheel", idle, 1.6f, Wheel));
        set.morse = Save(Posed("Work_Morse", idle, 0.8f, Morse));
        set.repair = Save(Looped("Work_Repair", Lib("Fixing_Kneeling")));
        set.tap = Save(Looped("Work_Tap", Lib("Interact")));
        set.pickUp = Save(Looped("Work_PickUp", Lib("PickUp_Table")));

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Debug.Log("[WorkAnimations] Built shovel, lever, wheel, Morse, repair, tap and pick-up loops into " + AssetPath);
    }

    // ---------- The poses ----------
    // Muscle conventions (Unity humanoid): Arm Down-Up + raises the arm, Arm Front-Back + swings it forward,
    // Forearm Stretch 1 = straight elbow (lower = bent), Spine/Chest Front-Back + bends forward, Twist + turns left,
    // Hand Down-Up + bends the wrist back. Each pose: (seconds into the loop, value). The last key repeats the first.

    /// <summary>Boiler: scoop low by the coal, lift, swing it forward into the firebox, back down for the next.</summary>
    static readonly Dictionary<string, (float t, float v)[]> Shovel = new Dictionary<string, (float, float)[]>
    {
        ["Spine Front-Back"] = K((0f, 0.55f), (0.45f, 0.2f), (0.8f, 0.35f), (1.1f, 0.3f), (1.5f, 0.55f)),
        ["Chest Front-Back"] = K((0f, 0.35f), (0.45f, 0.05f), (0.8f, 0.25f), (1.1f, 0.15f), (1.5f, 0.35f)),
        ["Spine Twist Left-Right"] = K((0f, 0.25f), (0.45f, 0.1f), (0.8f, -0.2f), (1.1f, 0f), (1.5f, 0.25f)),
        ["Head Nod Down-Up"] = K((0f, -0.35f), (0.8f, 0f), (1.5f, -0.35f)),
        // Left hand high on the handle, right hand low near the blade.
        ["Left Arm Down-Up"] = K((0f, -0.25f), (0.45f, 0.05f), (0.8f, 0.2f), (1.1f, -0.05f), (1.5f, -0.25f)),
        ["Left Arm Front-Back"] = K((0f, 0.55f), (0.45f, 0.6f), (0.8f, 0.85f), (1.1f, 0.6f), (1.5f, 0.55f)),
        ["Left Forearm Stretch"] = K((0f, 0.35f), (0.45f, 0.2f), (0.8f, 0.55f), (1.1f, 0.35f), (1.5f, 0.35f)),
        ["Right Arm Down-Up"] = K((0f, -0.45f), (0.45f, -0.2f), (0.8f, 0.05f), (1.1f, -0.25f), (1.5f, -0.45f)),
        ["Right Arm Front-Back"] = K((0f, 0.7f), (0.45f, 0.65f), (0.8f, 1f), (1.1f, 0.7f), (1.5f, 0.7f)),
        ["Right Forearm Stretch"] = K((0f, 0.85f), (0.45f, 0.55f), (0.8f, 0.95f), (1.1f, 0.7f), (1.5f, 0.85f)),
    };

    /// <summary>Doors: reach up to the knob, haul it down to the waist leaning into it, let it ride back up.</summary>
    static readonly Dictionary<string, (float t, float v)[]> LeverPull = new Dictionary<string, (float, float)[]>
    {
        ["Spine Front-Back"] = K((0f, 0.05f), (0.45f, 0.4f), (1f, 0.05f)),
        ["Chest Front-Back"] = K((0f, -0.05f), (0.45f, 0.25f), (1f, -0.05f)),
        ["Head Nod Down-Up"] = K((0f, 0.15f), (0.45f, -0.2f), (1f, 0.15f)),
        ["Left Arm Down-Up"] = K((0f, 0.55f), (0.45f, -0.15f), (1f, 0.55f)),
        ["Left Arm Front-Back"] = K((0f, 0.85f), (0.45f, 0.6f), (1f, 0.85f)),
        ["Left Forearm Stretch"] = K((0f, 0.9f), (0.45f, 0.35f), (1f, 0.9f)),
        ["Right Arm Down-Up"] = K((0f, 0.55f), (0.45f, -0.15f), (1f, 0.55f)),
        ["Right Arm Front-Back"] = K((0f, 0.85f), (0.45f, 0.6f), (1f, 0.85f)),
        ["Right Forearm Stretch"] = K((0f, 0.9f), (0.45f, 0.35f), (1f, 0.9f)),
        ["Left Hand Down-Up"] = K((0f, 0.2f), (0.45f, -0.3f), (1f, 0.2f)),
        ["Right Hand Down-Up"] = K((0f, 0.2f), (0.45f, -0.3f), (1f, 0.2f)),
    };

    /// <summary>Wheel: both hands on the spokes at chest height, working it hand over hand, shoulders turning with it.</summary>
    static readonly Dictionary<string, (float t, float v)[]> Wheel = new Dictionary<string, (float, float)[]>
    {
        ["Spine Front-Back"] = K((0f, 0.1f), (1.6f, 0.1f)),
        ["Chest Twist Left-Right"] = K((0f, 0.12f), (0.8f, -0.12f), (1.6f, 0.12f)),
        ["Left Arm Down-Up"] = K((0f, 0.25f), (0.8f, -0.15f), (1.6f, 0.25f)),
        ["Left Arm Front-Back"] = K((0f, 0.75f), (0.8f, 0.7f), (1.6f, 0.75f)),
        ["Left Forearm Stretch"] = K((0f, 0.45f), (0.8f, 0.6f), (1.6f, 0.45f)),
        ["Right Arm Down-Up"] = K((0f, -0.15f), (0.8f, 0.25f), (1.6f, -0.15f)),
        ["Right Arm Front-Back"] = K((0f, 0.7f), (0.8f, 0.75f), (1.6f, 0.7f)),
        ["Right Forearm Stretch"] = K((0f, 0.6f), (0.8f, 0.45f), (1.6f, 0.6f)),
    };

    /// <summary>Wireless: bent over the desk, eyes on the set, the right hand tapping the key, the left resting by it.</summary>
    static readonly Dictionary<string, (float t, float v)[]> Morse = new Dictionary<string, (float, float)[]>
    {
        ["Spine Front-Back"] = K((0f, 0.4f), (0.8f, 0.4f)),
        ["Chest Front-Back"] = K((0f, 0.2f), (0.8f, 0.2f)),
        ["Head Nod Down-Up"] = K((0f, -0.3f), (0.8f, -0.3f)),
        ["Left Arm Down-Up"] = K((0f, -0.3f), (0.8f, -0.3f)),
        ["Left Arm Front-Back"] = K((0f, 0.6f), (0.8f, 0.6f)),
        ["Left Forearm Stretch"] = K((0f, 0.3f), (0.8f, 0.3f)),
        ["Right Arm Down-Up"] = K((0f, -0.25f), (0.8f, -0.25f)),
        ["Right Arm Front-Back"] = K((0f, 0.65f), (0.8f, 0.65f)),
        ["Right Forearm Stretch"] = K((0f, 0.35f), (0.1f, 0.3f), (0.2f, 0.35f), (0.3f, 0.3f), (0.5f, 0.35f), (0.6f, 0.3f), (0.8f, 0.35f)),
        // Dit, dit, dah: the wrist taps down twice quickly, then once held.
        ["Right Hand Down-Up"] = K((0f, 0.3f), (0.08f, -0.4f), (0.16f, 0.3f), (0.24f, -0.4f), (0.32f, 0.3f), (0.42f, -0.4f), (0.62f, -0.4f), (0.7f, 0.3f), (0.8f, 0.3f)),
    };

    static (float, float)[] K(params (float, float)[] keys) => keys;

    // ---------- Making the clips ----------

    /// <summary>The idle's lower body held still (its first frame), with these muscles keyed on top, as a loop.</summary>
    static AnimationClip Posed(string name, AnimationClip idle, float length, Dictionary<string, (float t, float v)[]> pose)
    {
        var clip = new AnimationClip { name = name, frameRate = 30f };
        foreach (var b in AnimationUtility.GetCurveBindings(idle))
        {
            if (pose.ContainsKey(b.propertyName)) continue;
            var src = AnimationUtility.GetEditorCurve(idle, b);
            if (src == null) continue;
            float v = src.Evaluate(0f);
            AnimationUtility.SetEditorCurve(clip, b, new AnimationCurve(new Keyframe(0f, v), new Keyframe(length, v)));
        }
        foreach (var kv in pose)
        {
            var curve = new AnimationCurve(kv.Value.Select(k => new Keyframe(k.t, k.v)).ToArray());
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), kv.Key), curve);
        }
        MakeLoop(clip);
        return clip;
    }

    /// <summary>An exact copy of a library clip, set to loop.</summary>
    static AnimationClip Looped(string name, AnimationClip source)
    {
        if (source == null) { Debug.LogWarning("[WorkAnimations] Missing library clip for " + name); return null; }
        var clip = new AnimationClip { name = name, frameRate = source.frameRate };
        foreach (var b in AnimationUtility.GetCurveBindings(source))
            AnimationUtility.SetEditorCurve(clip, b, AnimationUtility.GetEditorCurve(source, b));
        MakeLoop(clip);
        return clip;
    }

    static void MakeLoop(AnimationClip clip)
    {
        var s = AnimationUtility.GetAnimationClipSettings(clip);
        s.loopTime = true;
        s.loopBlend = true;
        s.keepOriginalOrientation = true;
        s.keepOriginalPositionY = true;
        s.keepOriginalPositionXZ = true;
        s.loopBlendOrientation = true;
        s.loopBlendPositionY = true;
        s.loopBlendPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, s);
    }

    /// <summary>Writes (or overwrites in place, so references survive) the clip as an .anim asset.</summary>
    static AnimationClip Save(AnimationClip clip)
    {
        if (clip == null) return null;
        string path = $"{ClipFolder}/{clip.name}.anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null) { AssetDatabase.CreateAsset(clip, path); return clip; }
        EditorUtility.CopySerialized(clip, existing);
        existing.name = clip.name;
        EditorUtility.SetDirty(existing);
        return existing;
    }
}
