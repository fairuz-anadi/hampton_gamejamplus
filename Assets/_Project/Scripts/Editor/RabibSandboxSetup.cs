using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Builds Rabib_Stations.unity from code so nobody hand-edits scene YAML. Owner: Rabib.
/// Safe to run again: it only creates what's missing.
///   In the Editor:  Rabib > Build Sandbox
///   Headless:       Unity.exe -batchmode -projectPath . -executeMethod RabibSandboxSetup.BuildBatch -quit
/// </summary>
public static class RabibSandboxSetup
{
    const string ScenePath = "Assets/_Project/Scenes/Sandbox/Rabib_Stations.unity";

    static readonly (string name, Vector3 pos)[] Rooms =
    {
        ("Boiler",   new Vector3(-7f, 0f, 0f)),
        ("Doors",    new Vector3(-2f, 0f, 0f)),
        ("Wireless", new Vector3(2f, 0f, -1.5f)),
        ("Wheel",    new Vector3(6f, 0f, 0f)),
    };

    [MenuItem("Rabib/Build Sandbox")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EnsureBootstrap();
        EnsureFloor();
        EnsureRoomAnchors();
        EnsureSpawnPoint();
        EnsureStations();
        EnsureCarryItems();
        EnsureRoomZones();
        EnsurePlayerPrefab();
        EnsurePlayerJoin();
        EnsureDebugSecondPlayer();

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[RabibSandboxSetup] Sandbox ready: bootstrap, deck, rooms, spawn point, 4 stations, 2 carry items, 4 room zones, player join, debug keyboard P2.");
    }

    /// <summary>Same as Build(), callable headlessly.</summary>
    public static void BuildBatch() => Build();

    static void EnsureBootstrap()
    {
        if (Object.FindAnyObjectByType<SandboxBootstrap>() != null) return;
        new GameObject("[Bootstrap]").AddComponent<SandboxBootstrap>();
    }

    static void EnsureFloor()
    {
        if (GameObject.Find("DeckFloor_Placeholder") != null) return;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "DeckFloor_Placeholder";
        // Default Plane is 10x10; stern-to-bow runs along X, so stretch it to ~20x10.
        floor.transform.localScale = new Vector3(2f, 1f, 1f);
    }

    /// <summary>Empty transforms marking where each station belongs. Stations snap to these in Step 3.</summary>
    static void EnsureRoomAnchors()
    {
        var parentGO = GameObject.Find("Rooms");
        if (parentGO == null) parentGO = new GameObject("Rooms");

        foreach (var (name, pos) in Rooms)
        {
            if (parentGO.transform.Find(name) != null) continue;
            var anchor = new GameObject(name);
            anchor.transform.SetParent(parentGO.transform);
            anchor.transform.position = pos;
        }
    }

    static void EnsureSpawnPoint()
    {
        if (GameObject.Find("PlayerSpawn") != null) return;
        var spawn = new GameObject("PlayerSpawn");
        spawn.transform.position = new Vector3(0f, 0.1f, 3f);
    }

    const string PlayerPrefabPath = "Assets/_Project/Prefabs/Crew/Player.prefab";
    const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
    const string OrdersPath = "Assets/_Project/Data/Resources/Orders";
    const int MaxPlayers = 3;

    static void EnsureStations()
    {
        EnsureStation<Boiler>("Boiler");
        EnsureStation<Doors>("Doors");
        EnsureStation<Wheel>("Wheel");
        EnsureStation<Wireless>("Wireless");
    }

    static void EnsureStation<T>(string roomName) where T : Station
    {
        string goName = typeof(T).Name + "Station";
        if (GameObject.Find(goName) != null) return;

        var go = new GameObject(goName);
        go.AddComponent<T>();

        var room = GameObject.Find("Rooms")?.transform.Find(roomName);
        go.transform.position = room != null ? room.position : Vector3.zero;

        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        visual.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
        Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
    }

    static void EnsureCarryItems()
    {
        EnsureCarryItem("PassengerList", $"{OrdersPath}/02_PassengerList.asset", "Wireless");
        EnsureCarryItem("CaptainsLog", $"{OrdersPath}/10_CaptainsLog.asset", "Wheel");
    }

    static void EnsureCarryItem(string objectName, string orderAssetPath, string homeRoomName)
    {
        if (GameObject.Find(objectName) != null) return;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = objectName;
        go.transform.localScale = Vector3.one * 0.3f;
        Object.DestroyImmediate(go.GetComponent<SphereCollider>());

        var room = GameObject.Find("Rooms")?.transform.Find(homeRoomName);
        Vector3 origin = room != null ? room.position : Vector3.zero;
        go.transform.position = origin + new Vector3(0f, 0.9f, 0f);

        var item = go.AddComponent<CarryItem>();
        var order = AssetDatabase.LoadAssetAtPath<OrderData>(orderAssetPath);
        var so = new SerializedObject(item);
        so.FindProperty("order").objectReferenceValue = order;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Anchor name -> actual fiction room name (RoomId). Corridor holds the Doors station, Bridge holds the Wheel.</summary>
    static readonly (string anchorName, string roomId)[] RoomZones =
    {
        ("Boiler",   "Boiler"),
        ("Doors",    "Corridor"),
        ("Wireless", "Wireless"),
        ("Wheel",    "Bridge"),
    };

    static void EnsureRoomZones()
    {
        foreach (var (anchorName, roomId) in RoomZones)
            EnsureRoomZone(anchorName, roomId);
    }

    static void EnsureRoomZone(string anchorName, string roomId)
    {
        string goName = "RoomZone_" + roomId;
        if (GameObject.Find(goName) != null) return;

        var anchor = GameObject.Find("Rooms")?.transform.Find(anchorName);
        Vector3 origin = anchor != null ? anchor.position : Vector3.zero;

        var go = new GameObject(goName);
        go.transform.position = origin + new Vector3(0f, 1.5f, 0f);

        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(4f, 3f, 4f);

        var zone = go.AddComponent<RoomZone>();
        var so = new SerializedObject(zone);
        so.FindProperty("roomId").stringValue = roomId;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsurePlayerPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (existing != null)
        {
            PatchPrefabWithInteractor(existing, PlayerPrefabPath);
            return;
        }

        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

        var root = new GameObject("Player");
        var controller = root.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);

        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        visual.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
        Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());

        var input = root.AddComponent<PlayerInput>();
        input.actions = actions;
        input.defaultActionMap = "Player";

        root.AddComponent<PlayerController>();
        root.AddComponent<Interactor>();

        PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        Object.DestroyImmediate(root);
    }

    /// <summary>Adds a missing component to an already-saved prefab asset (can't just AddComponent on the asset object).</summary>
    static void PatchPrefabWithInteractor(GameObject prefabAsset, string prefabPath)
    {
        if (prefabAsset.GetComponent<Interactor>() != null) return;

        var contents = PrefabUtility.LoadPrefabContents(prefabPath);
        if (contents.GetComponent<Interactor>() == null) contents.AddComponent<Interactor>();
        PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        PrefabUtility.UnloadPrefabContents(contents);
    }

    static void EnsurePlayerJoin()
    {
        if (GameObject.Find("[PlayerJoin]") != null) return;

        var go = new GameObject("[PlayerJoin]");
        var manager = go.AddComponent<PlayerInputManager>();
        var join = go.AddComponent<PlayerJoin>();

        var managerSO = new SerializedObject(manager);
        managerSO.FindProperty("m_MaxPlayerCount").intValue = MaxPlayers;
        managerSO.ApplyModifiedPropertiesWithoutUndo();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        var joinSO = new SerializedObject(join);
        joinSO.FindProperty("playerPrefab").objectReferenceValue = prefab;
        joinSO.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>DEBUG ONLY, sandbox-only: lets Rabib test 2-player flow on one keyboard with no gamepad.</summary>
    static void EnsureDebugSecondPlayer()
    {
        const string name = "[Debug] Keyboard P2";
        var go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject(name);
            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());

            go.AddComponent<DebugSecondPlayer>();

            var spawn = GameObject.Find("PlayerSpawn");
            Vector3 origin = spawn != null ? spawn.transform.position : Vector3.zero;
            go.transform.position = origin + new Vector3(-1.2f, 0f, 0f);
        }

        if (go.GetComponent<Interactor>() == null) go.AddComponent<Interactor>();
    }
}
