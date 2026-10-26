using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Spawns the core systems (GameManager, MeterSystem, OrderDirector, debug tools) when a gameplay
/// scene loads, so nobody has to edit a scene to add them. That keeps Ship.unity merge-free.
///
/// Gameplay scenes: "Ship" and any sandbox ending in "_Systems".
/// In your own sandbox, call GameSystems.Ensure() from any script if you want orders and meters running.
/// </summary>
public static class GameSystems
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Entering Play mode in the Editor reports an internal mode, not Single, so only skip additive loads.
        if (mode == LoadSceneMode.Additive) return;
        if (IsGameplayScene(scene.name)) Ensure();
        else if (scene.name == "Results" && Object.FindAnyObjectByType<ResultsScreen>() == null)
            new GameObject("[Results]").AddComponent<ResultsScreen>();
        else if (scene.name == "MainMenu" && Object.FindAnyObjectByType<MainMenuScreen>() == null)
            new GameObject("[MainMenu]").AddComponent<MainMenuScreen>();
    }

    public static bool IsGameplayScene(string sceneName) =>
        sceneName == "Ship" || sceneName.EndsWith("_Systems");

    public static GameObject Ensure()
    {
        if (GameManager.Instance != null) return GameManager.Instance.gameObject;

        var go = new GameObject("[GameSystems]");
        go.AddComponent<MeterSystem>();
        go.AddComponent<OrderDirector>();
        go.AddComponent<GameManager>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        go.AddComponent<DebugTools>();
#endif
        return go;
    }
}
