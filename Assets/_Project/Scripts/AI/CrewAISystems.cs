using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Spawns the crew and the Captain (CrewRoster, SuspicionTracker, CaptainDirector, OffMoments, MusterVote,
/// TutorialHints) in gameplay scenes,
/// the same way GameSystems spawns the core systems, so nobody has to edit Ship.unity. Owner: Riham.
/// Riham_AI.unity also counts as a gameplay scene, with the core systems running.
/// </summary>
public static class CrewAISystems
{
    public const string SandboxScene = "Riham_AI";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        if (scene.name == SandboxScene) GameSystems.Ensure();
        if (scene.name == SandboxScene || GameSystems.IsGameplayScene(scene.name)) Ensure();
    }

    public static GameObject Ensure()
    {
        var roster = Object.FindAnyObjectByType<CrewRoster>();
        var go = roster != null ? roster.gameObject : new GameObject("[CrewAI]");
        if (roster == null)
        {
            roster = go.AddComponent<CrewRoster>();
            // The AI sandbox has no players: let bots crew all four seats.
            roster.reserveHumanSeats = SceneManager.GetActiveScene().name != SandboxScene;
        }
        if (Object.FindAnyObjectByType<SuspicionTracker>() == null) go.AddComponent<SuspicionTracker>();
        if (Object.FindAnyObjectByType<CaptainDirector>() == null) go.AddComponent<CaptainDirector>();
        if (Object.FindAnyObjectByType<OffMoments>() == null) go.AddComponent<OffMoments>();
        if (Object.FindAnyObjectByType<MusterVote>() == null) go.AddComponent<MusterVote>();
        if (Object.FindAnyObjectByType<TutorialHints>() == null) go.AddComponent<TutorialHints>();
        return go;
    }
}
