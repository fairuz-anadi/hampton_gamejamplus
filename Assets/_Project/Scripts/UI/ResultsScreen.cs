using UnityEngine;

/// <summary>
/// Results screen: ending, impact, final meters, the Stoker reveal, what each choice cost, and Play again.
/// Works with mouse, keyboard (left/right + Enter) and gamepad (d-pad + A, B for the main menu).
/// Spawned by GameSystems when the Results scene loads. Owner: Anadi.
/// Drawn with IMGUI on a dark card in Samprity's type and colours (UiTheme), so it reads over any sky.
/// </summary>
public class ResultsScreen : MonoBehaviour
{
    [SerializeField] string playScene = "Ship";
    [SerializeField] string menuScene = "MainMenu";

    const float RefHeight = 1080f;
    const float CardWidth = 1320f;

    EndingData endingData;
    Vector2 scroll;
    int selected; // 0 Play again, 1 Main menu
    GUIStyle eyebrow, title, body, italic, stat, row, stoker, button, buttonSelected;
    Texture2D shadeTex, cardTex, brassTex, rowTex, buttonTex, buttonHoverTex, selectedTex;

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

    void OnDestroy()
    {
        foreach (var t in new[] { shadeTex, cardTex, brassTex, rowTex, buttonTex, buttonHoverTex, selectedTex })
            if (t != null) Destroy(t);
    }

    void OnGUI()
    {
        float s = Screen.height / RefHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s;
        EnsureStyles();

        // Shade the whole sky, then a darker card with a brass hairline, like the how-to-play card.
        GUI.DrawTexture(new Rect(0f, 0f, w, RefHeight), shadeTex);
        var card = new Rect(w * 0.5f - CardWidth * 0.5f, 40f, CardWidth, RefHeight - 80f);
        GUI.DrawTexture(card, cardTex);
        Frame(card);

        GUILayout.BeginArea(new Rect(card.x + 56f, card.y + 40f, card.width - 112f, card.height - 76f));

        if (!RunRecord.HasEnding)
        {
            GUILayout.Label("N O   V O Y A G E   Y E T", eyebrow);
            GUILayout.Label("No voyage recorded yet", title);
            GUILayout.Label("Play from the Ship scene to reach the results.", italic);
            GUILayout.FlexibleSpace();
            Buttons();
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label("T H E   V O Y A G E   I S   O V E R", eyebrow);
        GUILayout.Label(endingData != null ? endingData.title : RunRecord.Ending.ToString(), title);
        if (endingData != null) GUILayout.Label(endingData.body, italic);
        GUILayout.Space(14f);
        Rule();
        GUILayout.Space(10f);

        GUILayout.Label($"{Brass("Impact")}  {ImpactText()}      {Brass("Captain's Trust")}  {RunRecord.FinalCaptainTrust}      {Brass("Ship Safety")}  {RunRecord.FinalShipSafety}      {Brass("Crew Trust")}  {RunRecord.FinalCrewTrust}", stat);
        GUILayout.Label($"{Brass("Ice warnings")}  {RunRecord.IceWarningsDelivered} reached the bridge, {RunRecord.IceWarningsLost} lost, {RunRecord.IceWarningsHidden} never arrived" +
                        (RunRecord.LifeboatStoreLocked ? $"      {Brass("Lifeboats")}  the store was still locked" : ""), stat);
        GUILayout.Space(10f);
        GUILayout.Label(RunRecord.StokerSeat > 0
            ? $"The Stoker was {CrewRegistry.SeatName(RunRecord.StokerSeat)}."
            : "There was no Stoker aboard this time.", stoker);
        if (StokerBrain.Log.Count > 0) GUILayout.Label($"{Brass("What the Stoker did")}  {string.Join("  ·  ", StokerBrain.Log)}", stat);
        GUILayout.Space(14f);

        GUILayout.Label("W H A T   E A C H   C H O I C E   C O S T", eyebrow);
        scroll = GUILayout.BeginScrollView(scroll, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
        if (RunRecord.Choices.Count == 0) GUILayout.Label("No orders were resolved.", row);
        for (int i = 0; i < RunRecord.Choices.Count; i++)
        {
            var r = GUILayoutUtility.GetRect(new GUIContent(RunRecord.Choices[i].Describe()), row);
            if (i % 2 == 0) GUI.DrawTexture(r, rowTex);
            GUI.Label(r, RunRecord.Choices[i].Describe(), row);
        }
        GUILayout.EndScrollView();

        GUILayout.Space(12f);
        if (endingData != null && !string.IsNullOrEmpty(endingData.closingLine)) GUILayout.Label(endingData.closingLine, italic);
        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        Buttons();
        GUILayout.FlexibleSpace();
        GUILayout.Label($"{Brass("Endings found")}  {EndingEvaluator.FoundCount} / {EndingEvaluator.EndingCount}", stat, GUILayout.Height(64f));
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    void Buttons()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Play again", selected == 0 ? buttonSelected : button, GUILayout.Width(260f), GUILayout.Height(64f))) Choose(0);
        GUILayout.Space(20f);
        if (GUILayout.Button("Main menu", selected == 1 ? buttonSelected : button, GUILayout.Width(260f), GUILayout.Height(64f))) Choose(1);
        GUILayout.EndHorizontal();
    }

    void Rule()
    {
        var r = GUILayoutUtility.GetRect(1f, 2f, GUILayout.ExpandWidth(true));
        GUI.DrawTexture(new Rect(r.x, r.y, Mathf.Min(r.width, 420f), 2f), brassTex);
    }

    void Frame(Rect r)
    {
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), brassTex);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), brassTex);
        GUI.DrawTexture(new Rect(r.x, r.y, 2f, r.height), brassTex);
        GUI.DrawTexture(new Rect(r.xMax - 2f, r.y, 2f, r.height), brassTex);
    }

    static string Brass(string s) => $"<color=#{ColorUtility.ToHtmlStringRGB(UiTheme.Brass)}>{s}</color>";

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

    static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    void EnsureStyles()
    {
        if (title != null) return;
        var theme = UiTheme.Load();
        Font titleFont = theme ? theme.title : null, bodyFont = theme ? theme.body : null, italicFont = theme ? theme.italic : null;

        shadeTex = Solid(new Color(0.02f, 0.025f, 0.04f, 0.62f));
        cardTex = Solid(new Color(0.03f, 0.035f, 0.05f, 0.88f));
        brassTex = Solid(new Color(UiTheme.Brass.r, UiTheme.Brass.g, UiTheme.Brass.b, 0.6f));
        rowTex = Solid(new Color(1f, 1f, 1f, 0.04f));
        buttonTex = Solid(new Color(0.08f, 0.09f, 0.12f, 0.95f));
        buttonHoverTex = Solid(new Color(0.14f, 0.15f, 0.19f, 0.95f));

        GUIStyle Label(Font f, int size, Color c, FontStyle fs = FontStyle.Normal) => new GUIStyle(GUI.skin.label)
        {
            font = f, fontSize = size, fontStyle = f != null ? FontStyle.Normal : fs, wordWrap = true, richText = true,
            normal = { textColor = c }, hover = { textColor = c },
        };

        eyebrow = Label(bodyFont, 20, UiTheme.Brass);
        title = Label(titleFont, 72, UiTheme.Cream, FontStyle.Bold);
        italic = Label(italicFont, 28, UiTheme.DimCream, FontStyle.Italic);
        stat = Label(bodyFont, 24, UiTheme.Cream);
        stat.alignment = TextAnchor.MiddleLeft;
        stoker = Label(titleFont, 36, UiTheme.Brass, FontStyle.Bold);
        row = Label(bodyFont, 22, UiTheme.Cream);
        row.padding = new RectOffset(12, 12, 5, 5);

        button = new GUIStyle(GUI.skin.button)
        {
            font = titleFont, fontSize = 28, alignment = TextAnchor.MiddleCenter,
            normal = { background = buttonTex, textColor = UiTheme.Cream },
            hover = { background = buttonHoverTex, textColor = UiTheme.Cream },
            active = { background = buttonHoverTex, textColor = UiTheme.Brass },
            border = new RectOffset(0, 0, 0, 0),
        };
        selectedTex = Solid(UiTheme.Brass);
        buttonSelected = new GUIStyle(button)
        {
            normal = { background = selectedTex, textColor = new Color(0.08f, 0.07f, 0.05f) },
            hover = { background = selectedTex, textColor = new Color(0.08f, 0.07f, 0.05f) },
        };
    }
}
