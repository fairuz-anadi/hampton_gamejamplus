using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the MiniMap prefab (Prefabs/UI/MiniMap/Resources/MiniMap.prefab): a bottom-right deck map that self-spawns
/// onto Samprity's HUD canvas at runtime (see MiniMap.cs). Doesn't touch HUD.prefab, HudBuilder.cs or any of her
/// files — a separate Resources folder, merged into the same runtime namespace by Unity. Owner: Rabib.
///   In the Editor:  Rabib > Build MiniMap
///   Headless:       Unity.exe -batchmode -projectPath . -executeMethod MiniMapBuilder.BuildBatch -quit
/// Safe to run again: fully rebuilds the prefab from scratch each time.
/// </summary>
public static class MiniMapBuilder
{
    public const string PrefabPath = "Assets/_Project/Prefabs/UI/MiniMap/Resources/MiniMap.prefab";
    const string UI = "Assets/_Project/Art/UI/Sprites";

    static readonly Color PanelColor = new Color(0.02f, 0.03f, 0.06f, 0.78f);
    static readonly Color StationNeutral = new Color(0.75f, 0.75f, 0.8f, 0.9f);
    static readonly Color HighlightColor = new Color(0.95f, 0.78f, 0.42f, 1f);
    static readonly Color IceWarnColor = new Color(0.95f, 0.85f, 0.3f, 1f);
    static readonly Color BridgeColor = new Color(0.9f, 0.95f, 1f, 1f);

    [MenuItem("Rabib/Build MiniMap")]
    public static void Build()
    {
        var root = BuildMiniMap();
        MovementPrototypeBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[MiniMap] Built " + PrefabPath);
    }

    public static void BuildBatch() => Build();

    static GameObject BuildMiniMap()
    {
        var root = new GameObject("MiniMap", typeof(RectTransform));
        var rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(1f, 0f);
        rootRect.anchoredPosition = new Vector2(-24f, 232f);
        rootRect.sizeDelta = new Vector2(170f, 260f);

        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        Stretch((RectTransform)panel.transform);
        var bg = panel.AddComponent<Image>();
        bg.sprite = LoadSprite("panel");
        bg.color = PanelColor;
        bg.raycastTarget = false;

        var mapAreaGo = new GameObject("MapArea", typeof(RectTransform));
        mapAreaGo.transform.SetParent(panel.transform, false);
        var mapRect = Stretch((RectTransform)mapAreaGo.transform);

        var bridgeIcon = Icon(mapAreaGo.transform, "Bridge", LoadSprite("disc"), BridgeColor, 10f);

        var stationIcons = new Image[4];
        for (int i = 0; i < stationIcons.Length; i++)
            stationIcons[i] = Icon(mapAreaGo.transform, "Station_" + (StationType)i, null, StationNeutral, 14f);

        var highlightRing = Icon(mapAreaGo.transform, "Highlight", LoadSprite("ring"), HighlightColor, 22f);

        var iceWarningIcons = new Image[3];
        for (int i = 0; i < iceWarningIcons.Length; i++)
            iceWarningIcons[i] = Icon(mapAreaGo.transform, "IceWarning_" + (i + 1), LoadSprite("diamond"), IceWarnColor, 12f);

        var crewIcons = new Image[CrewRoster.SeatCount];
        for (int i = 0; i < crewIcons.Length; i++)
            crewIcons[i] = Icon(mapAreaGo.transform, "Crew_" + (i + 1), LoadSprite("disc"), Color.white, 12f);

        var mini = root.AddComponent<MiniMap>();
        var so = new SerializedObject(mini);
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("mapArea").objectReferenceValue = mapRect;
        so.FindProperty("bridgeIcon").objectReferenceValue = bridgeIcon;
        SetArray(so, "stationIcons", stationIcons);
        so.FindProperty("highlightRing").objectReferenceValue = highlightRing;
        SetArray(so, "iceWarningIcons", iceWarningIcons);
        SetArray(so, "crewIcons", crewIcons);
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    static RectTransform Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static Image Icon(Transform parent, string name, Sprite sprite, Color color, float size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        go.SetActive(false);
        return img;
    }

    static void SetArray(SerializedObject so, string name, Image[] values)
    {
        var prop = so.FindProperty(name);
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static Sprite LoadSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UI}/{name}.png");
}
