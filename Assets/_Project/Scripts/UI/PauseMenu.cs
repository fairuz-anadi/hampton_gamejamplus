using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pause menu for gameplay scenes: Esc or Start on any gamepad. Resume, Restart (new Stoker), Main menu, Quit.
/// Works with mouse, keyboard (arrows + Enter) and gamepad (d-pad + A, B to resume).
/// Spawned with the core systems by GameSystems. Owner: Anadi.
/// Rabib: don't bind Esc/Start to your own pause; call PauseMenu.Instance.Toggle() if your Pause action needs it.
/// Samprity: placeholder drawn with IMGUI; restyle freely, the logic lives in Toggle/Resume and GameManager.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    [SerializeField] string menuScene = "MainMenu";

    const float RefHeight = 1080f;
    static readonly string[] Items = { "Resume", "Restart", "Main menu", "Quit" };

    public bool IsPaused { get; private set; }

    int selected;
    GUIStyle title, button, hint;

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
        bool toggle = kb != null && kb.escapeKey.wasPressedThisFrame;
        foreach (var pad in Gamepad.all) toggle |= pad.startButton.wasPressedThisFrame;
        if (toggle) { Toggle(); return; }
        if (!IsPaused) return;

        foreach (var pad in Gamepad.all)
            if (pad.buttonEast.wasPressedThisFrame) { Resume(); return; }
        selected = MenuControls.Move(selected, Items.Length);
        if (MenuControls.Confirm()) Choose(selected);
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
        selected = 0;
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
    }

    void Choose(int item)
    {
        switch (item)
        {
            case 0: Resume(); break;
            case 1: GameManager.Restart(); break;
            case 2: GameManager.LoadScene(menuScene); break;
            default: Quit(); break;
        }
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
        hint ??= new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0f, 0f, w, RefHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(w * 0.5f - 200f, 280f, 400f, 620f));
        GUILayout.Label("Paused", title);
        GUILayout.Space(24f);
        for (int i = 0; i < Items.Length; i++)
        {
            if (MenuControls.Button(Items[i], button, i == selected, GUILayout.Height(68f))) Choose(i);
            GUILayout.Space(10f);
        }
        GUILayout.Label("Arrows / d-pad to choose, Enter / A to pick, Esc / B to resume", hint);
        GUILayout.EndArea();
    }
}
