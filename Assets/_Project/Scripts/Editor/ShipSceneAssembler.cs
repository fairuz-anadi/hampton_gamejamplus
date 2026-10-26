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
/// </summary>
public static class ShipSceneAssembler
{
    const string ShipScene = "Assets/_Project/Scenes/Ship.unity";
    const string DeckPrefab = "Assets/_Project/Prefabs/Environment/ShipDeck.prefab";
    const string PlayerPrefab = "Assets/_Project/Prefabs/Crew/Player.prefab";

    // The deck runs stern to bow along +Z from 0 to 36 m and is 12 m wide.
    static readonly Vector3 DeckCentre = new Vector3(0f, 0f, 18f);
    // 55° down as the plan suggests; far and wide enough that stern and bow both fit on screen.
    const float CameraPitch = 55f, CameraDistance = 50f, CameraFov = 45f;

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

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ShipSceneAssembler] Ship.unity has the deck, a deck camera and player join.");
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
        cam.farClipPlane = Mathf.Max(cam.farClipPlane, 200f);
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
