using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Puts the team's prefabs into Ship.unity, so only this tool (Anadi) edits the main scene. Owner: Anadi.
///   In the Editor:  Port > Assemble Ship Scene
///   Headless:       Unity.exe -batchmode -quit -projectPath . -executeMethod ShipSceneAssembler.AssembleBatch
/// Safe to run again after Samprity rebuilds the deck: it only adds what's missing and re-frames the camera.
///   Samprity's ShipDeck prefab (rooms, stations, lights, moods, markers)
///   A fixed camera that shows the whole deck, as the plan asks for 1-3 players on one screen (Samprity tunes it)
///   Rabib's player join on PlayerSpawn = the deck's Spawn_1 marker
///   Rabib's Boiler/Doors/Wheel/Wireless scripts on the deck's Station_* objects (status light = their StatusLight bulb)
///   Rabib's carry items for orders 2 and 10, at the Wireless desk and the Wheel
///   Rabib's ice warnings MSG 1-3 on the deck's PickUp_IceWarning marker (they only show while waiting)
///   Rabib's IcebergSequence (the steering finale), which replaces GameManager's placeholder iceberg
/// </summary>
public static class ShipSceneAssembler
{
    const string ShipScene = "Assets/_Project/Scenes/Ship.unity";
    const string DeckPrefab = "Assets/_Project/Prefabs/Environment/ShipDeck.prefab";
    const string PlayerPrefab = "Assets/_Project/Prefabs/Crew/Player.prefab";

    // The deck runs stern to bow along +Z from 0 to ShipLayout.DeckLength and is 12 m wide.
    static readonly Vector3 DeckCentre = new Vector3(0f, 0f, ShipLayout.DeckLength * 0.5f);
    // 55° down as the plan suggests; far and wide enough that stern and bow both fit on screen (50 m back per 36 m of deck).
    const float CameraPitch = 55f, CameraFov = 45f;
    static readonly float CameraDistance = 50f * ShipLayout.DeckLength / 36f;

    [MenuItem("Port/Assemble Ship Scene")]
    public static void Assemble()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ShipScene, OpenSceneMode.Single);

        var deck = EnsureDeck();
        if (deck == null) return;
        TurnOffDefaultSun(deck);
        FrameCamera();
        EnsurePlayerJoin(deck);
        EnsureStations(deck);
        EnsureCarryItem(deck, "PassengerList", "02_PassengerList", "Station_Wireless");
        EnsureCarryItem(deck, "CaptainsLog", "10_CaptainsLog", "Station_Wheel");
        for (int n = 1; n <= 3; n++) EnsureIceWarning(deck, n);
        EnsureIcebergSequence();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ShipSceneAssembler] Ship.unity has the deck, a deck camera, player join, 4 stations, 2 carry items, 3 ice warnings and the iceberg finale.");
    }

    public static void AssembleBatch() => Assemble();

    static GameObject EnsureDeck()
    {
        var existing = GameObject.Find("ShipDeck");
        if (existing != null) return existing;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeckPrefab);
        if (prefab == null)
        {
            Debug.LogError($"[ShipSceneAssembler] {DeckPrefab} is missing. Run Port > Samprity > Build Ship Deck first.");
            return null;
        }
        var deck = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        deck.name = "ShipDeck";
        deck.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return deck;
    }

    /// <summary>The deck brings its own moonlight, so the scene's default sun is switched off rather than doubled.</summary>
    static void TurnOffDefaultSun(GameObject deck)
    {
        bool deckHasSun = false;
        foreach (var l in deck.GetComponentsInChildren<Light>(true)) deckHasSun |= l.type == LightType.Directional;
        var sun = GameObject.Find("Directional Light");
        if (deckHasSun && sun != null) sun.SetActive(false);
    }

    static void FrameCamera()
    {
        var cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (cam == null) return;
        var rot = Quaternion.Euler(CameraPitch, 0f, 0f);
        cam.transform.SetPositionAndRotation(DeckCentre - rot * Vector3.forward * CameraDistance, rot);
        cam.fieldOfView = CameraFov;
        cam.farClipPlane = Mathf.Max(cam.farClipPlane, CameraDistance * 3f);
    }

    static void EnsureStations(GameObject deck)
    {
        AddStation<Boiler>(deck, "Station_Boiler");
        AddStation<Doors>(deck, "Station_Doors");
        AddStation<Wheel>(deck, "Station_Wheel");
        AddStation<Wireless>(deck, "Station_Wireless");
    }

    static void AddStation<T>(GameObject deck, string name) where T : Station
    {
        var t = FindDeep(deck.transform, name);
        if (t == null) { Debug.LogWarning($"[ShipSceneAssembler] The deck has no {name}."); return; }
        if (t.GetComponent<T>() == null) t.gameObject.AddComponent<T>();
    }

    /// <summary>The item for a carry order, waiting beside the station where it's picked up.</summary>
    /// <summary>
    /// The finale. With no bow point set it places the iceberg 7 m ahead of the Wheel, which is the bow rail on
    /// Samprity's deck, inside the whole-deck camera.
    /// </summary>
    static void EnsureIcebergSequence()
    {
        if (Object.FindAnyObjectByType<IcebergSequence>() != null) return;
        new GameObject("[IcebergSequence]").AddComponent<IcebergSequence>();
    }

    /// <summary>One message slip per warning, side by side on the desk's pick-up marker.</summary>
    static void EnsureIceWarning(GameObject deck, int number)
    {
        string name = "IceWarning_" + number;
        var marker = FindDeep(deck.transform, "PickUp_IceWarning");
        var desk = FindDeep(deck.transform, "Station_Wireless");
        if (marker == null && desk == null) { Debug.LogWarning("[ShipSceneAssembler] Couldn't place the ice warnings."); return; }
        var at = marker != null ? marker.position : desk.position + Vector3.up * 0.9f;
        // Already placed: follow the marker, in case the deck changed (the ship got longer).
        var existing = GameObject.Find(name);
        if (existing != null) { existing.transform.position = at + new Vector3(0.28f * (number - 2), 0f, 0f); return; }

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        go.transform.localScale = new Vector3(0.22f, 0.02f, 0.3f);
        go.transform.position = at + new Vector3(0.28f * (number - 2), 0f, 0f);

        var warn = go.AddComponent<IceWarningCarry>();
        var so = new SerializedObject(warn);
        so.FindProperty("warningNumber").intValue = number;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsureCarryItem(GameObject deck, string itemName, string orderAsset, string pickupStation)
    {
        var station = FindDeep(deck.transform, pickupStation);
        var spot = station != null ? station.Find("WorkSpot_A") : null;
        var order = AssetDatabase.LoadAssetAtPath<OrderData>($"Assets/_Project/Data/Resources/Orders/{orderAsset}.asset");
        if (station == null || order == null) { Debug.LogWarning($"[ShipSceneAssembler] Couldn't place {itemName}."); return; }
        // Already placed: follow its station, in case the deck changed (the ship got longer).
        var existing = GameObject.Find(itemName);
        if (existing != null) { existing.transform.position = (spot != null ? spot.position : station.position) + new Vector3(0f, 0.9f, 0f); return; }

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = itemName;
        Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        go.transform.localScale = new Vector3(0.3f, 0.04f, 0.4f);
        go.transform.position = (spot != null ? spot.position : station.position) + new Vector3(0f, 0.9f, 0f);

        var item = go.AddComponent<CarryItem>();
        var so = new SerializedObject(item);
        so.FindProperty("order").objectReferenceValue = order;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    static void EnsurePlayerJoin(GameObject deck)
    {
        var spawn = GameObject.Find("PlayerSpawn") ?? new GameObject("PlayerSpawn");
        var marker = deck.transform.Find("Markers/Spawn_1");
        spawn.transform.position = marker != null ? marker.position : DeckCentre;

        if (GameObject.Find("[PlayerJoin]") != null) return;
        var go = new GameObject("[PlayerJoin]");
        var manager = go.AddComponent<PlayerInputManager>();
        var join = go.AddComponent<PlayerJoin>();

        var managerSO = new SerializedObject(manager);
        managerSO.FindProperty("m_MaxPlayerCount").intValue = PlayerSlots.MaxPlayers;
        managerSO.ApplyModifiedPropertiesWithoutUndo();

        var joinSO = new SerializedObject(join);
        joinSO.FindProperty("playerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        joinSO.ApplyModifiedPropertiesWithoutUndo();
    }
}
