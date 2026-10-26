using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Placeholder main menu: Start (or Enter) loads the ship, Quit closes the game.
/// Spawned by GameSystems when the MainMenu scene loads. Owner: Anadi.
/// Phase 3 replaces this with the real menu (join slots for 1-3 players, "bots fill the rest").
/// </summary>
public class MainMenuScreen : MonoBehaviour
{
    [SerializeField] string playScene = "Ship";

    const float RefHeight = 1080f;

    GUIStyle title, body, button;

#if ENABLE_INPUT_SYSTEM
    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) StartRun();
        var pad = Gamepad.current;
        if (pad != null && pad.startButton.wasPressedThisFrame) StartRun();
    }
#endif

    void OnGUI()
    {
        float s = Screen.height / RefHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s;
        EnsureStyles();

        GUILayout.BeginArea(new Rect(w * 0.5f - 500f, 260f, 1000f, 600f));
        GUILayout.Label("The Promised Port", title);
        GUILayout.Label("Follow the Captain's orders. Bots fill any empty crew seats.", body);
        GUILayout.Space(40f);
        if (GUILayout.Button("Start  (Enter)", button, GUILayout.Width(320f), GUILayout.Height(72f))) StartRun();
        GUILayout.Space(16f);
        if (GUILayout.Button("Quit", button, GUILayout.Width(320f), GUILayout.Height(72f))) Quit();
        GUILayout.EndArea();
    }

    void StartRun()
    {
        if (Application.CanStreamedLevelBeLoaded(playScene)) SceneManager.LoadScene(playScene);
        else Debug.LogWarning($"[MainMenuScreen] Scene '{playScene}' isn't in Build Settings.");
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void EnsureStyles()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { fontSize = 80, fontStyle = FontStyle.Bold };
        body = new GUIStyle(GUI.skin.label) { fontSize = 30, wordWrap = true };
        button = new GUIStyle(GUI.skin.button) { fontSize = 30 };
    }
}
