using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[RabibSandboxSetup] Sandbox ready: bootstrap, placeholder deck, 4 room anchors, 1 spawn point.");
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
}
