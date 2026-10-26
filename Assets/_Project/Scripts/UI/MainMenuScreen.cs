using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Main menu with local join. Spawned by GameSystems when the MainMenu scene loads. Owner: Anadi.
///   Keyboard: Space joins or leaves, Enter starts.
///   Gamepad:  A / ✕ joins, B / ○ leaves, Start starts.
/// 1-3 humans, bots fill the rest of the 4 seats. Starting with nobody joined puts that device in seat 1.
/// Samprity: this draws with IMGUI as a placeholder. Restyle it, or build MenuUI on PlayerSlots and remove the MainMenu line in GameSystems.
/// </summary>
public class MainMenuScreen : MonoBehaviour
{
    [SerializeField] string playScene = "Ship";

    const float RefHeight = 1080f;

    GUIStyle title, body, slot, button;
    TextAsset credits;
    bool showCredits;

    void Start()
    {
        PlayerSlots.Clear();
        credits = Resources.Load<TextAsset>("Credits");
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (showCredits)
        {
            bool back = kb != null && kb.escapeKey.wasPressedThisFrame;
            foreach (var pad in Gamepad.all) back |= pad.buttonEast.wasPressedThisFrame;
            if (back) showCredits = false;
            return;
        }

        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame) Toggle(kb);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) StartRun(kb);
        }

        foreach (var pad in Gamepad.all)
        {
            if (pad.buttonSouth.wasPressedThisFrame) PlayerSlots.Join(pad);
            if (pad.buttonEast.wasPressedThisFrame) PlayerSlots.Leave(pad);
            if (pad.startButton.wasPressedThisFrame) StartRun(pad);
        }
    }

    static void Toggle(InputDevice device)
    {
        if (!PlayerSlots.Leave(device)) PlayerSlots.Join(device);
    }

    void StartRun(InputDevice pressedBy)
    {
        if (PlayerSlots.Devices.Count == 0) PlayerSlots.Join(pressedBy);
        GameManager.LoadScene(playScene);
    }

    void OnGUI()
    {
        float s = Screen.height / RefHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s;
        EnsureStyles();

        GUILayout.BeginArea(new Rect(w * 0.5f - 520f, 140f, 1040f, 840f));
        if (showCredits)
        {
            GUILayout.Label(credits != null ? credits.text : "Credits.txt is missing.", body);
            GUILayout.Space(20f);
            if (GUILayout.Button("Back  (Esc / B)", button, GUILayout.Width(320f), GUILayout.Height(72f))) showCredits = false;
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label("The Promised Port", title);
        GUILayout.Label("Follow the Captain's orders. 1-3 players, bots fill the rest of the crew.", body);
        GUILayout.Space(30f);

        for (int seat = 1; seat <= PlayerSlots.Seats; seat++)
        {
            string who;
            if (seat <= PlayerSlots.Devices.Count) who = PlayerSlots.Devices[seat - 1].displayName;
            else if (seat <= PlayerSlots.MaxPlayers) who = "bot  (press Space or A to join)";
            else who = "bot";
            GUILayout.Label($"{CrewRegistry.SeatName(seat)}:  {who}", slot);
        }

        GUILayout.Space(30f);
        GUILayout.Label("Enter or Start to set sail. B leaves.", body);
        GUILayout.Space(12f);
        if (GUILayout.Button("Start", button, GUILayout.Width(320f), GUILayout.Height(72f))) StartRun(Keyboard.current);
        GUILayout.Space(12f);
        if (GUILayout.Button("Credits", button, GUILayout.Width(320f), GUILayout.Height(72f))) showCredits = true;
        GUILayout.Space(12f);
        if (GUILayout.Button("Quit", button, GUILayout.Width(320f), GUILayout.Height(72f))) PauseMenu.Quit();
        GUILayout.EndArea();
    }

    void EnsureStyles()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { fontSize = 80, fontStyle = FontStyle.Bold };
        body = new GUIStyle(GUI.skin.label) { fontSize = 30, wordWrap = true };
        slot = new GUIStyle(GUI.skin.label) { fontSize = 34 };
        button = new GUIStyle(GUI.skin.button) { fontSize = 30 };
    }
}
