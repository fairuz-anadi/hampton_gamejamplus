using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pause menu for gameplay scenes: Esc or Start on any gamepad. Resume, Restart (new Stoker), Main menu, Quit.
/// Spawned with the core systems by GameSystems. Owner: Anadi.
/// Rabib: don't bind Esc/Start to your own pause; call PauseMenu.Instance.Toggle() if your Pause action needs it.
/// Samprity: placeholder drawn with IMGUI; restyle freely, the logic lives in Toggle/Resume and GameManager.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    [SerializeField] string menuScene = "MainMenu";

    const float RefHeight = 1080f;

    public bool IsPaused { get; private set; }

    GUIStyle title, button;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        if (IsPaused) Time.timeScale = 1f;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) Toggle();
        foreach (var pad in Gamepad.all)
            if (pad.startButton.wasPressedThisFrame) { Toggle(); break; }
    }

    public void Toggle()
    {
        if (IsPaused) Resume();
        else if (IntroCard.IsShowing) return;
        else if (GameManager.Instance == null || GameManager.Instance.State != GameState.Results) Pause();
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
    }

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnGUI()
    {
        if (!IsPaused) return;
        float s = Screen.height / RefHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s;
        title ??= new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        button ??= new GUIStyle(GUI.skin.button) { fontSize = 30 };

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0f, 0f, w, RefHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(w * 0.5f - 200f, 300f, 400f, 560f));
        GUILayout.Label("Paused", title);
        GUILayout.Space(24f);
        if (GUILayout.Button("Resume", button, GUILayout.Height(68f))) Resume();
        GUILayout.Space(10f);
        if (GUILayout.Button("Restart", button, GUILayout.Height(68f))) GameManager.Restart();
        GUILayout.Space(10f);
        if (GUILayout.Button("Main menu", button, GUILayout.Height(68f))) GameManager.LoadScene(menuScene);
        GUILayout.Space(10f);
        if (GUILayout.Button("Quit", button, GUILayout.Height(68f))) Quit();
        GUILayout.EndArea();
    }
}
