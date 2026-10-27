using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Port > Riham > Build AI Sandbox: rebuilds Riham_AI.unity from Samprity's ShipDeck prefab, puts Rabib's four
/// stations on its Station_* markers, adds the carry items, ice warnings and iceberg finale the same way
/// ShipSceneAssembler does for Ship.unity, and an overhead camera. Press Play and 4 bots run the whole voyage
/// on their own (the NavMesh is baked at start). Owner: Riham. Only touches Riham_AI.unity.
/// </summary>
public static class AISandboxBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/Sandbox/Riham_AI.unity";
    const string DeckPrefab = "Assets/_Project/Prefabs/Environment/ShipDeck.prefab";
    const string OldMaterials = "Assets/_Project/Scenes/Sandbox/Riham_AI_Materials";

    [MenuItem("Port/Riham/Build AI Sandbox")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
        if (AssetDatabase.IsValidFolder(OldMaterials)) AssetDatabase.DeleteAsset(OldMaterials);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeckPrefab);
        if (prefab == null)
        {
            Debug.LogError($"[AISandboxBuilder] {DeckPrefab} is missing. Pull main first.");
            return;
        }
        var deck = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        AddStation<Boiler>(deck, "Station_Boiler");
        AddStation<Doors>(deck, "Station_Doors");
        AddStation<Wireless>(deck, "Station_Wireless");
        AddStation<Wheel>(deck, "Station_Wheel");

        // Same props as Ship.unity (see ShipSceneAssembler), so the bots meet the real thing.
        AddCarryItem(deck, "PassengerList", "02_PassengerList", "Station_Wireless");
        AddCarryItem(deck, "CaptainsLog", "10_CaptainsLog", "Station_Wheel");
        for (int n = 1; n <= 3; n++) AddIceWarning(deck, n);
        new GameObject("[IcebergSequence]").AddComponent<IcebergSequence>();

        // Overhead camera that sees the whole deck, stern at the bottom of the screen and bow at the top.
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        camGo.transform.position = new Vector3(0f, 32f, -6f);
        camGo.transform.LookAt(new Vector3(0f, 0f, 15f));
        camGo.AddComponent<AudioListener>();

        var lightGo = new GameObject("Work Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.8f;
        lightGo.transform.rotation = Quaternion.Euler(60f, -30f, 0f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[AISandboxBuilder] Riham_AI.unity rebuilt on the ShipDeck. Press Play to watch 4 bots run the voyage.");
    }

    static void AddStation<T>(GameObject deck, string markerName) where T : Station
    {
        var marker = FindDeep(deck.transform, markerName);
        if (marker == null)
        {
            Debug.LogWarning($"[AISandboxBuilder] No {markerName} marker on the deck.");
            return;
        }
        if (marker.GetComponent<T>() == null) marker.gameObject.AddComponent<T>();
    }

    static void AddCarryItem(GameObject deck, string itemName, string orderAsset, string pickupStation)
    {
        var station = FindDeep(deck.transform, pickupStation);
        var spot = station != null ? station.Find("WorkSpot_A") : null;
        var order = AssetDatabase.LoadAssetAtPath<OrderData>($"Assets/_Project/Data/Resources/Orders/{orderAsset}.asset");
        if (station == null || order == null) return;

        var go = Slip(itemName, new Vector3(0.3f, 0.04f, 0.4f));
        go.transform.position = (spot != null ? spot.position : station.position) + new Vector3(0f, 0.9f, 0f);
        var so = new SerializedObject(go.AddComponent<CarryItem>());
        so.FindProperty("order").objectReferenceValue = order;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AddIceWarning(GameObject deck, int number)
    {
        var marker = FindDeep(deck.transform, "PickUp_IceWarning");
        var desk = FindDeep(deck.transform, "Station_Wireless");
        if (marker == null && desk == null) return;

        var go = Slip("IceWarning_" + number, new Vector3(0.22f, 0.02f, 0.3f));
        var at = marker != null ? marker.position : desk.position + Vector3.up * 0.9f;
        go.transform.position = at + new Vector3(0.28f * (number - 2), 0f, 0f);
        var so = new SerializedObject(go.AddComponent<IceWarningCarry>());
        so.FindProperty("warningNumber").intValue = number;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>A thin paper-like cube with no collider.</summary>
    static GameObject Slip(string name, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        go.transform.localScale = size;
        return go;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform child in t)
        {
            var hit = FindDeep(child, name);
            if (hit != null) return hit;
        }
        return null;
    }
}
