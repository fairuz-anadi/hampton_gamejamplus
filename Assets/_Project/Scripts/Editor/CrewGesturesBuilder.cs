using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Builds Art/Animations/Resources/CrewGestures.asset from the Quaternius animation library. Owner: Anadi.</summary>
public static class CrewGesturesBuilder
{
    const string AnimFbx = "Assets/_Project/Art/ThirdParty/Quaternius/Animations/UAL1_Standard.fbx";
    const string AssetPath = "Assets/_Project/Art/Animations/Resources/CrewGestures.asset";

    [MenuItem("Port/Build Crew Gestures")]
    public static void Build()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(AnimFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        AnimationClip Clip(string n) => clips.FirstOrDefault(c => c.name == n || c.name.EndsWith("|" + n));
        MovementPrototypeBuilder.EnsureFolder("Assets/_Project/Art/Animations/Resources");
        var g = AssetDatabase.LoadAssetAtPath<CrewGestures>(AssetPath);
        if (g == null) { g = ScriptableObject.CreateInstance<CrewGestures>(); AssetDatabase.CreateAsset(g, AssetPath); }
        g.tap = Clip("Interact");
        g.talk = Clip("Idle_Talking_Loop");
        g.startle = Clip("Hit_Chest");
        EditorUtility.SetDirty(g);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CrewGestures] tap {g.tap?.name}, talk {g.talk?.name}, startle {g.startle?.name}");
    }

    public static void BuildBatch() => Build();
}
