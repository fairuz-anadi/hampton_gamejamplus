using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The in-game HUD: meters (top left), order cards (top centre), station prompts (over stations). Owner: Samprity.
/// Spawns itself from Resources/HUD in gameplay scenes, like GameSystems, so nobody has to edit Ship.unity.
/// Visible during the story parts and the iceberg; hidden on the menu and results. Never drives game logic.
/// </summary>
public class HUD : MonoBehaviour
{
    public static HUD Instance { get; private set; }

    public CanvasGroup root;
    [Min(0.01f)] public float fadeSeconds = 0.4f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        bool show = gm == null || (gm.State >= GameState.Part1 && gm.State <= GameState.Iceberg);
        if (PauseMenu.Instance != null && PauseMenu.Instance.IsPaused) show = false; // nothing peeks round the pause card
        root.alpha = Mathf.MoveTowards(root.alpha, show ? 1f : 0f, Time.unscaledDeltaTime / fadeSeconds);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || Instance != null || !GameSystems.IsGameplayScene(scene.name)) return;
        var prefab = Resources.Load<GameObject>("HUD");
        if (prefab != null) Instantiate(prefab).name = "HUD";
    }
}
