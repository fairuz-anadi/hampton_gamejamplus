using UnityEngine;

/// <summary>
/// Working results screen: ending, impact, final meters, the Stoker reveal, what each choice cost, and Play again.
/// Works with mouse, keyboard (left/right + Enter) and gamepad (d-pad + A, B for the main menu).
/// Spawned by GameSystems when the Results scene loads. Owner: Anadi.
/// Samprity: this is a placeholder drawn with IMGUI. Build the styled ResultsUI from the same RunRecord data,
/// then remove the Results line in GameSystems.OnSceneLoaded.
/// </summary>
public class ResultsScreen : MonoBehaviour
{
    [SerializeField] string playScene = "Ship";
    [SerializeField] string menuScene = "MainMenu";

    const float RefHeight = 1080f;

    EndingData endingData;
    Vector2 scroll;
    int selected; // 0 Play again, 1 Main menu
    GUIStyle title, body, small, button;

    void Start()
    {
        if (RunRecord.HasEnding) endingData = EndingData.For(RunRecord.Ending);
    }

    void Update()
    {
        selected = MenuControls.Move(selected, 2, vertical: false);
        if (MenuControls.Confirm()) Choose(selected);
        else if (MenuControls.Back()) Choose(1);
    }

    // Loading Ship again re-spawns the crew, so Play again always picks a new Stoker.
    void Choose(int item) => GameManager.LoadScene(item == 0 ? playScene : menuScene);

    void OnGUI()
    {
        float s = Screen.height / RefHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s;
        EnsureStyles();

        GUILayout.BeginArea(new Rect(w * 0.5f - 600f, 60f, 1200f, RefHeight - 120f));

        if (!RunRecord.HasEnding)
        {
            GUILayout.Label("No voyage recorded yet", title);
            GUILayout.Label("Play from the Ship scene to reach the results.", body);
            GUILayout.FlexibleSpace();
            Buttons();
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label(endingData != null ? endingData.title : RunRecord.Ending.ToString(), title);
        if (endingData != null) GUILayout.Label(endingData.body, body);
        GUILayout.Space(16f);

        GUILayout.Label($"Impact: {ImpactText()}    ·    Captain's Trust {RunRecord.FinalCaptainTrust}  ·  Ship Safety {RunRecord.FinalShipSafety}  ·  Crew Trust {RunRecord.FinalCrewTrust}", small);
        GUILayout.Label($"Ice warnings: {RunRecord.IceWarningsDelivered} reached the bridge, {RunRecord.IceWarningsLost} lost, {RunRecord.IceWarningsHidden} never arrived.", small);
        if (RunRecord.LifeboatStoreLocked) GUILayout.Label("The lifeboat store was still locked.", small);
        GUILayout.Label(RunRecord.StokerSeat > 0
            ? $"The Stoker was {CrewRegistry.SeatName(RunRecord.StokerSeat)}."
            : "There was no Stoker aboard this time.", body);
        GUILayout.Space(12f);

        GUILayout.Label("What each choice cost", small);
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        if (RunRecord.Choices.Count == 0) GUILayout.Label("No orders were resolved.", small);
        foreach (var c in RunRecord.Choices) GUILayout.Label(c.Describe(), small);
        GUILayout.EndScrollView();

        GUILayout.Space(12f);
        if (endingData != null && !string.IsNullOrEmpty(endingData.closingLine)) GUILayout.Label(endingData.closingLine, body);
        GUILayout.Label($"Endings found: {EndingEvaluator.FoundCount}/{EndingEvaluator.EndingCount}", small);
        Buttons();

        GUILayout.EndArea();
    }

    void Buttons()
    {
        GUILayout.BeginHorizontal();
        if (MenuControls.Button("Play again", button, selected == 0, GUILayout.Width(280f), GUILayout.Height(64f))) Choose(0);
        GUILayout.Space(20f);
        if (MenuControls.Button("Main menu", button, selected == 1, GUILayout.Width(280f), GUILayout.Height(64f))) Choose(1);
        GUILayout.EndHorizontal();
    }

    static string ImpactText()
    {
        string angle = float.IsNaN(RunRecord.ImpactAngle) ? "" : $" ({Mathf.Abs(RunRecord.ImpactAngle):0}°)";
        return RunRecord.Impact switch
        {
            ImpactKind.HeadOn => "head-on" + angle,
            ImpactKind.Glancing => "glancing" + angle,
            _ => "a long scrape down the side" + angle,
        };
    }

    void EnsureStyles()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, wordWrap = true };
        body = new GUIStyle(GUI.skin.label) { fontSize = 28, wordWrap = true };
        small = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true };
        button = new GUIStyle(GUI.skin.button) { fontSize = 28 };
    }
}
