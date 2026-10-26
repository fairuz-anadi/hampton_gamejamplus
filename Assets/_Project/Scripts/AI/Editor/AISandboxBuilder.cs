using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Port > Riham > Build AI Sandbox: rebuilds Riham_AI.unity as a greybox deck with 4 rooms, room zones,
/// stand-in stations, a 2.5D camera and a light. Press Play and 4 bots run the whole voyage on their own.
/// Owner: Riham. Only touches Riham_AI.unity.
/// </summary>
public static class AISandboxBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/Sandbox/Riham_AI.unity";

    [MenuItem("Port/Riham/Build AI Sandbox")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);

        var deck = new GameObject("Deck");

        // Floor, stern (left, -x) to bow (right, +x).
        Block(deck, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(32f, 0.2f, 11f), new Color(0.55f, 0.45f, 0.35f));

        // Outer walls.
        var wall = new Color(0.25f, 0.3f, 0.4f);
        Block(deck, "Wall North", new Vector3(0f, 0.5f, 5.6f), new Vector3(32f, 1f, 0.2f), wall);
        Block(deck, "Wall South", new Vector3(0f, 0.5f, -5.6f), new Vector3(32f, 1f, 0.2f), wall);
        Block(deck, "Wall Stern", new Vector3(-16f, 0.5f, 0f), new Vector3(0.2f, 1f, 11f), wall);
        Block(deck, "Wall Bow", new Vector3(16f, 0.5f, 0f), new Vector3(0.2f, 1f, 11f), wall);

        // Room dividers with a 3 m doorway in the middle.
        foreach (float x in new[] { -6f, 1f, 7f })
        {
            Block(deck, $"Divider {x}", new Vector3(x, 0.5f, 3.6f), new Vector3(0.2f, 1f, 4f), wall);
            Block(deck, $"Divider {x} b", new Vector3(x, 0.5f, -3.6f), new Vector3(0.2f, 1f, 4f), wall);
        }

        var rooms = new GameObject("Rooms");
        Zone(rooms, "Boiler", -11f, 10f);
        Zone(rooms, "Corridor", -2.5f, 7f);
        Zone(rooms, "Wireless", 4f, 6f);
        Zone(rooms, "Bridge", 11.5f, 9f);

        var stations = new GameObject("Stations");
        Station(stations, StationType.Boiler, new Vector3(-13f, 0.5f, 2.5f));
        Station(stations, StationType.Doors, new Vector3(-2.5f, 0.5f, 4f));
        Station(stations, StationType.Wireless, new Vector3(4f, 0.5f, 4f));
        Station(stations, StationType.Wheel, new Vector3(13f, 0.5f, 0f));

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 35f;
        camGo.transform.position = new Vector3(0f, 22f, -19f);
        camGo.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
        camGo.AddComponent<AudioListener>();

        var lightGo = new GameObject("Sun");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[AISandboxBuilder] Riham_AI.unity rebuilt. Press Play: bots spawn, a NavMesh is baked at start, and orders begin.");
    }

    static GameObject Block(GameObject parent, string name, Vector3 pos, Vector3 size, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform);
        go.transform.position = pos;
        go.transform.localScale = size;
        go.isStatic = true;
        Paint(go, color);
        return go;
    }

    static void Zone(GameObject parent, string id, float centerX, float width)
    {
        var go = new GameObject($"Room {id}");
        go.transform.SetParent(parent.transform);
        go.transform.position = new Vector3(centerX, 1f, 0f);
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(width, 2f, 11f);
        box.isTrigger = true;
        go.AddComponent<RoomZone>().roomId = id;
    }

    static void Station(GameObject parent, StationType type, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"{type} (sandbox)";
        go.transform.SetParent(parent.transform);
        go.transform.position = pos;
        go.AddComponent<SandboxStation>().Configure(type);
    }

    const string MaterialFolder = "Assets/_Project/Scenes/Sandbox/Riham_AI_Materials";

    /// <summary>Materials are saved as assets next to the scene, otherwise the scene loses them on reload.</summary>
    static void Paint(GameObject go, Color color)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;

        string path = $"{MaterialFolder}/Greybox_{ColorUtility.ToHtmlStringRGB(color)}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Scenes/Sandbox", "Riham_AI_Materials");
            mat = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(mat, path);
        }
        r.sharedMaterial = mat;
    }
}
