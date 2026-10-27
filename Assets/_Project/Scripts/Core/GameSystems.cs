using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Spawns the core systems (GameManager, MeterSystem, OrderDirector, pause menu, debug tools) when a gameplay
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
        Time.timeScale = 1f; // never carry a pause into the next scene
        if (IsGameplayScene(scene.name)) Ensure(scene.name);
        else if (scene.name == "Results" && Object.FindAnyObjectByType<ResultsScreen>() == null)
            new GameObject("[Results]").AddComponent<ResultsScreen>();
        // MainMenu.unity now carries Samprity's 3D opening (OpeningDirector); the IMGUI menu is the fallback without it.
        else if (scene.name == "MainMenu" && Object.FindAnyObjectByType<MainMenuScreen>() == null && Object.FindAnyObjectByType<OpeningDirector>() == null)
            new GameObject("[MainMenu]").AddComponent<MainMenuScreen>();
    }

    public static bool IsGameplayScene(string sceneName) =>
        sceneName == "Ship" || sceneName.EndsWith("_Systems");

    /// <summary>Spawns the systems once. The "how to play" card only appears in Ship, never in sandboxes.</summary>
    public static GameObject Ensure(string sceneName = null)
    {
        if (GameManager.Instance != null) return GameManager.Instance.gameObject;

        var go = new GameObject("[GameSystems]");
        go.AddComponent<MeterSystem>();
        go.AddComponent<OrderDirector>();
        go.AddComponent<IceWarnings>();
        go.AddComponent<GameManager>();
        go.AddComponent<PauseMenu>();
        if ((sceneName ?? SceneManager.GetActiveScene().name) == "Ship") go.AddComponent<IntroCard>();
        // Debug keys exist in the Editor and Development builds only, never in the release zip.
        if (Debug.isDebugBuild) go.AddComponent<DebugTools>();
        return go;
    }
}
