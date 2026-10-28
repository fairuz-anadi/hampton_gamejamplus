using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The ending screen (Anadi's approved design, 28 Oct): the ending's title on a ribbon, the Voyage Chart (a sea map
/// with every order as a badge along the route that bent north to Grey Cove), the three meters as ship's dials, a
/// WANTED poster for the Stoker, the endings found as ships in bottles, and wooden-sign buttons. Artwork is painted
/// once from this run's RunRecord (EndingPainter); text is uGUI in Pirata One and Patrick Hand.
/// Keyboard (left/right + Enter), gamepad (d-pad + A, B for the main menu) and mouse. Spawned by GameSystems when
/// the Results scene loads. Owner: Anadi.
/// </summary>
public class ResultsScreen : MonoBehaviour
{
    [SerializeField] string playScene = "Ship";
    [SerializeField] string menuScene = "MainMenu";

    const int W = 1920, H = 1080;
    static readonly Color Cream = C(247, 236, 214), Ink = C(58, 40, 28), Brass = C(219, 173, 87), Navy = C(14, 26, 44);
    static readonly Color Green = C(84, 170, 96), Red = C(205, 72, 58), Purple = C(140, 96, 190), Grey = C(140, 140, 140);
    static readonly Color Rope = C(150, 46, 38);

    EndingData endingData;
    int selected; // 0 Sail again, 1 Main menu
    Font titleFont, bodyFont;
    RectTransform root;
    readonly RawImage[] signs = new RawImage[2];
    readonly Texture2D[] signOn = new Texture2D[2], signOff = new Texture2D[2];
    readonly List<Texture2D> textures = new List<Texture2D>();
    GameObject canvasGo;

    static Color C(int r, int g, int b, float a = 1f) => new Color(r / 255f, g / 255f, b / 255f, a);

    void Start()
    {
        if (RunRecord.HasEnding) endingData = EndingData.For(RunRecord.Ending);
        titleFont = Resources.Load<Font>("Fonts/PirataOne-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bodyFont = Resources.Load<Font>("Fonts/PatrickHand-Regular") ?? titleFont;
        Build();
        Select(0);
    }

    void OnDestroy()
    {
        foreach (var t in textures) if (t != null) Destroy(t);
        if (canvasGo != null) Destroy(canvasGo);
    }

    void Update()
    {
        int was = selected;
        selected = MenuControls.Move(selected, 2, vertical: false);
        // The mouse picks whichever sign it's over; a click chooses it.
        var mouse = Mouse.current;
        if (mouse != null)
            for (int i = 0; i < 2; i++)
                if (signs[i] != null && RectTransformUtility.RectangleContainsScreenPoint(signs[i].rectTransform, mouse.position.ReadValue(), null))
                {
                    selected = i;
                    if (mouse.leftButton.wasPressedThisFrame) { Choose(i); return; }
                }
        if (selected != was) Select(selected);
        if (MenuControls.Confirm()) Choose(selected);
        else if (MenuControls.Back()) Choose(1);
    }

    // Loading Ship again re-spawns the crew, so Sail again always picks a new Stoker.
    void Choose(int item) => GameManager.LoadScene(item == 0 ? playScene : menuScene);

    void Select(int i)
    {
        selected = i;
        for (int k = 0; k < 2; k++) if (signs[k] != null) signs[k].texture = k == i ? signOn[k] : signOff[k];
    }

    // ---------- Build ----------

    void Build()
    {
        canvasGo = new GameObject("[Results]", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(W, H);
        scaler.matchWidthOrHeight = 0.5f;
        var fill = new GameObject("Backdrop", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        fill.transform.SetParent(canvasGo.transform, false);
        fill.color = C(8, 16, 30);
        Stretch(fill.rectTransform);

        var art = Raw(canvasGo.transform, PaintScene(), new Vector2(W * 0.5f, H * 0.5f), new Vector2(W, H));
        root = art.rectTransform;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;

        if (!RunRecord.HasEnding)
        {
            Txt("No voyage yet", titleFont, 92, Cream, new Vector2(W / 2, 106), TextAnchor.MiddleCenter, 900);
            Txt("Play a voyage from the Ship to reach an ending.", bodyFont, 34, Cream, new Vector2(W / 2, 210), TextAnchor.MiddleCenter, 1500);
            BuildSigns();
            return;
        }
        BuildTexts();
        BuildPoster();
        BuildSigns();
    }

    void BuildTexts()
    {
        string title = endingData != null ? endingData.title : RunRecord.Ending.ToString();
        Txt(title, titleFont, 92, Cream, new Vector2(W / 2, 106), TextAnchor.MiddleCenter, 760);
        if (endingData != null) Txt(endingData.body, bodyFont, 30, Cream, new Vector2(W / 2, 204), TextAnchor.MiddleCenter, 1560);

        Txt("The Voyage Chart", titleFont, 54, Ink, new Vector2(110, 300), TextAnchor.MiddleLeft, 600);
        Txt("Home", bodyFont, 28, Ink, new Vector2(270, 781), TextAnchor.MiddleLeft, 200);
        Txt("The Promised Port", bodyFont, 30, Ink, new Vector2(1030, 727), TextAnchor.MiddleRight, 300);
        Txt("Grey Cove", titleFont, 46, Red, new Vector2(920, 396), TextAnchor.MiddleLeft, 260);
        Txt("N", titleFont, 30, Rope, new Vector2(190, 358), TextAnchor.MiddleCenter, 60);
        string[] legend = { "obeyed", "refused", "faked", "ran out" };
        for (int j = 0; j < 4; j++) Txt(legend[j], bodyFont, 28, Ink, new Vector2(396 + j * 190, 816), TextAnchor.MiddleLeft, 160);
        var route = RoutePoints();
        for (int i = 0; i < route.Count && RunRecord.Choices.Count > 0; i++) Txt((i + 1).ToString(), bodyFont, 26, Ink, route[i] + new Vector2(-22, -38), TextAnchor.MiddleRight, 60);

        Txt("How the night ended", titleFont, 40, Ink, new Vector2(1260, 294), TextAnchor.MiddleLeft, 560);
        int[] vals = { RunRecord.FinalCaptainTrust, RunRecord.FinalShipSafety, RunRecord.FinalCrewTrust };
        string[] names = { "Captain's Trust", "Ship Safety", "Crew Trust" };
        for (int k = 0; k < 3; k++)
        {
            float cx = 1350 + k * 190, cy = 400;
            Txt(vals[k].ToString(), titleFont, 40, Ink, new Vector2(cx, cy + 40), TextAnchor.MiddleCenter, 140);
            Txt(names[k], bodyFont, 28, Ink, new Vector2(cx, cy + 104), TextAnchor.MiddleCenter, 190);
        }

        Txt("Endings found", titleFont, 40, Ink, new Vector2(1705, 596), TextAnchor.MiddleCenter, 280);
        Txt($"{EndingEvaluator.FoundCount} of {EndingEvaluator.EndingCount}", bodyFont, 34, Ink, new Vector2(1705, 841), TextAnchor.MiddleCenter, 200);
        var all = (Ending[])System.Enum.GetValues(typeof(Ending));
        for (int k = 0; k < all.Length && k < 3; k++)
            if (!EndingEvaluator.IsFound(all[k])) Txt("?", titleFont, 54, C(150, 140, 120), new Vector2(1615 + k * 90, 730), TextAnchor.MiddleCenter, 60);

        if (endingData != null && !string.IsNullOrEmpty(endingData.closingLine))
            Txt($"\"{endingData.closingLine}\"", bodyFont, 32, C(210, 220, 230), new Vector2(W / 2, 1030), TextAnchor.MiddleCenter, 1700);
    }

    /// <summary>The WANTED poster, tilted on its own.</summary>
    void BuildPoster()
    {
        var p = new EndingPainter(340, 365, new Color(0, 0, 0, 0));
        Paper(p, 20, 20, 320, 345, C(242, 224, 186));
        int seat = RunRecord.StokerSeat;
        var col = seat > 0 && seat < CrewRoster.SeatColors.Length ? CrewRoster.SeatColors[seat] : Grey;
        float sx = 170, sy = 20 + 152;
        p.Circle(sx, sy - 31, 28, col);
        p.RoundRect(sx - 46, sy, sx + 46, sy + 64, 22, col);
        p.RoundRectOutline(50, 345 - 62, 290, 345 - 22, 8, 4, Red);
        var poster = Raw(root, Track(p.ToTexture()), new Vector2(1380, 722), new Vector2(340, 365));
        poster.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 3f);
        var pr = poster.rectTransform;
        Txt("WANTED", titleFont, 60, Rope, new Vector2(170, 62), TextAnchor.MiddleCenter, 300, pr);
        Txt("the Captain's man", bodyFont, 28, Ink, new Vector2(170, 106), TextAnchor.MiddleCenter, 300, pr);
        Txt(seat > 0 ? CrewRegistry.CrewNameForSeat(seat) : "nobody", titleFont, 40, Ink, new Vector2(170, 260), TextAnchor.MiddleCenter, 300, pr);
        int n = StokerBrain.Log.Count;
        Txt(n == 1 ? "sabotaged once" : $"sabotaged {n} times", bodyFont, 30, Red, new Vector2(170, 303), TextAnchor.MiddleCenter, 260, pr);
    }

    void BuildSigns()
    {
        string[] labels = { "Sail again", "Main menu" };
        for (int k = 0; k < 2; k++)
        {
            signOn[k] = Track(PaintSign(true));
            signOff[k] = Track(PaintSign(false));
            signs[k] = Raw(root, signOff[k], new Vector2(W / 2 + (k == 0 ? -190 : 190), 945), new Vector2(320, 100));
            Txt(labels[k], titleFont, 46, Cream, new Vector2(160, 48), TextAnchor.MiddleCenter, 300, signs[k].rectTransform);
        }
    }

    // ---------- Painting ----------

    Texture2D PaintScene()
    {
        var p = new EndingPainter(W, H, Navy);
        var rng = new System.Random(7);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        p.VerticalGradient(C(22, 38, 66), C(8, 16, 30));
        for (int i = 0; i < 140; i++) p.Circle(R(0, W), R(0, 420), rng.Next(4) == 0 ? 2f : 1.2f, C(255, 250, 230));
        for (int row = 0; row < 6; row++)
        {
            int rr = row;
            p.FillBelow(x => 860 + rr * 40 + 10 * Mathf.Sin(x / 70f + rr), C(20 + row * 4, 50 + row * 6, 80 + row * 8));
        }

        // Ribbon banner in the ending's colour.
        var ribbon = RunRecord.Ending switch { Ending.WhatIf => C(200, 150, 56), Ending.CaptainWins => C(70, 74, 96), _ => C(196, 64, 50) };
        var tail = ribbon * 0.75f; tail.a = 1f;
        float bx = W / 2f, by = 70, bw = 760;
        p.Polygon(new[] { V(bx - bw / 2 - 70, by + 10), V(bx - bw / 2, by - 4), V(bx - bw / 2, by + 84), V(bx - bw / 2 - 70, by + 98), V(bx - bw / 2 - 40, by + 54) }, tail);
        p.Polygon(new[] { V(bx + bw / 2 + 70, by + 10), V(bx + bw / 2, by - 4), V(bx + bw / 2, by + 84), V(bx + bw / 2 + 70, by + 98), V(bx + bw / 2 + 40, by + 54) }, tail);
        p.RoundRect(bx - bw / 2, by - 14, bx + bw / 2, by + 90, 10, ribbon);
        p.RoundRectOutline(bx - bw / 2, by - 14, bx + bw / 2, by + 90, 10, 5, tail * 0.8f + new Color(0, 0, 0, 0.2f));

        if (!RunRecord.HasEnding) return Track(p.ToTexture());

        PaintChart(p, rng);
        PaintDials(p);
        PaintBottles(p);
        return Track(p.ToTexture());
    }

    static Vector2 V(float x, float y) => new Vector2(x, y);

    void Paper(EndingPainter p, float x0, float y0, float x1, float y1, Color fill)
    {
        p.RoundRect(x0 + 8, y0 + 10, x1 + 8, y1 + 10, 18, new Color(0, 0, 0, 0.42f), 14f);
        p.RoundRect(x0, y0, x1, y1, 18, fill);
        p.RoundRectOutline(x0, y0, x1, y1, 18, 4, C(196, 160, 110));
        var rng = new System.Random((int)(x0 * 7 + y0));
        for (int i = 0; i < 60; i++)
            p.Circle(x0 + 14 + (float)rng.NextDouble() * (x1 - x0 - 28), y0 + 14 + (float)rng.NextDouble() * (y1 - y0 - 28), 1.5f, C(222, 204, 170, 0.6f));
    }

    // The bezier route, 12 stops evenly along it: home, east, then bending north to Grey Cove.
    static readonly Vector2 Home = V(220, 735), Cove = V(880, 400), Promised = V(1070, 685);
    static List<Vector2> RouteCurve()
    {
        Vector2 P0 = Home, P1 = V(590, 820), P2 = V(630, 550), P3 = Cove;
        var s = new List<Vector2>();
        for (int k = 0; k <= 400; k++)
        {
            float t = k / 400f, u = 1 - t;
            s.Add(u * u * u * P0 + 3 * u * u * t * P1 + 3 * u * t * t * P2 + t * t * t * P3);
        }
        return s;
    }

    static List<Vector2> RoutePoints()
    {
        var s = RouteCurve();
        var lens = new List<float> { 0f };
        for (int k = 1; k < s.Count; k++) lens.Add(lens[k - 1] + Vector2.Distance(s[k - 1], s[k]));
        var pts = new List<Vector2>();
        var seen = new HashSet<int>();
        foreach (var c in RunRecord.Choices) seen.Add(c.orderNumber);
        int n = Mathf.Max(1, seen.Count);
        for (int i = 0; i < n; i++)
        {
            float target = lens[lens.Count - 1] * (0.07f + i * (0.825f / Mathf.Max(1, n - 1)));
            int k = lens.FindIndex(L => L >= target);
            pts.Add(s[Mathf.Clamp(k, 0, s.Count - 1)]);
        }
        return pts;
    }

    void PaintChart(EndingPainter p, System.Random rng)
    {
        Paper(p, 70, 250, 1180, 860, Cream);
        var seaInk = C(176, 196, 200);
        for (int i = 0; i < 38; i++)
        {
            float wx = 130 + (float)rng.NextDouble() * 980, wy = 360 + (float)rng.NextDouble() * 410;
            p.Arc(wx + 11, wy + 6, 11, 3, 200, 340, seaInk);
            p.Arc(wx + 29, wy + 6, 11, 3, 200, 340, seaInk);
        }
        var land = C(214, 190, 140);
        Blob(p, rng, 155, 790, 70, 45, land);
        Blob(p, rng, 1110, 685, 45, 95, land);
        Blob(p, rng, 540, 470, 50, 30, land);
        Blob(p, rng, 400, 600, 34, 22, land);
        // Compass rose.
        float rx = 190, ry = 420;
        for (int a = 0; a < 360; a += 90)
        {
            float len = a == 0 ? 46 : 34, ang = (a - 90) * Mathf.Deg2Rad, side = a * Mathf.Deg2Rad;
            var tip = V(rx + Mathf.Cos(ang) * len, ry + Mathf.Sin(ang) * len);
            p.Polygon(new[] { tip, V(rx + Mathf.Cos(side) * 9, ry + Mathf.Sin(side) * 9), V(rx - Mathf.Cos(side) * 9, ry - Mathf.Sin(side) * 9) }, a == 0 ? Rope : Ink);
        }
        // The course everyone was told, and the Promised Port.
        for (int k = 0; k < 40; k += 2)
            p.Line(Vector2.Lerp(Home, Promised, k / 40f), Vector2.Lerp(Home, Promised, (k + 1) / 40f), 3, C(150, 130, 100));
        p.Circle(Promised.x, Promised.y, 16, Cream);
        p.Ring(Promised.x, Promised.y, 14, 4, Ink);
        // The route that really happened: dashed, bending north.
        var curve = RouteCurve();
        for (int k = 0; k + 10 < curve.Count; k += 20) p.Line(curve[k], curve[k + 10], 6, Rope);
        // Grey Cove: the X and the iceberg.
        p.Line(Cove + V(-22, -22), Cove + V(22, 22), 9, Red);
        p.Line(Cove + V(-22, 22), Cove + V(22, -22), 9, Red);
        var ice = Cove + V(115, 45);
        p.Polygon(new[] { ice + V(-40, 30), ice + V(-18, -18), ice + V(-2, 2), ice + V(14, -30), ice + V(44, 30) }, C(226, 242, 250));
        PaintFinish(p);

        // Every order the player's crew got, as a badge along the route.
        var stops = RoutePoints();
        var byNumber = new Dictionary<int, Choice>();
        foreach (var c in RunRecord.Choices) byNumber[c.orderNumber] = c.choice;
        var numbers = new List<int>(byNumber.Keys);
        numbers.Sort();
        for (int i = 0; i < numbers.Count && i < stops.Count; i++) Badge(p, stops[i].x, stops[i].y, byNumber[numbers[i]], 24);
        Choice[] legend = { Choice.Obey, Choice.Refuse, Choice.Fake, Choice.Expired };
        for (int j = 0; j < 4; j++) Badge(p, 370 + j * 190, 816, legend[j], 16);
    }

    /// <summary>How the voyage ends on the chart: the ship turns for port, the boats row away, or she goes down.</summary>
    void PaintFinish(EndingPainter p)
    {
        if (RunRecord.Ending == Ending.WhatIf)
        {
            // Dawn: a green line from the iceberg back down to the Promised Port, the ship on its way.
            for (int k = 0; k < 20; k += 2) p.Line(Vector2.Lerp(Cove, Promised, k / 20f), Vector2.Lerp(Cove, Promised, (k + 1) / 20f), 5, Green);
            Ship(p, Vector2.Lerp(Cove, Promised, 0.55f), 0f);
        }
        else if (RunRecord.Ending == Ending.ManySaved)
        {
            Ship(p, Cove + V(40, 85), 25f); // listing, going down
            for (int k = 0; k < 4; k++) p.Ellipse(Cove.x + 10 + k * 24, Cove.y + 132 + (k % 2) * 10, 9, 5, C(150, 110, 70)); // the lifeboats
        }
        else Ship(p, Cove + V(40, 85), 55f); // gone down at his X
    }

    void Ship(EndingPainter p, Vector2 at, float tiltDeg)
    {
        Vector2 Rot(float x, float y)
        {
            float a = tiltDeg * Mathf.Deg2Rad;
            return at + V(x * Mathf.Cos(a) - y * Mathf.Sin(a), x * Mathf.Sin(a) + y * Mathf.Cos(a));
        }
        p.Polygon(new[] { Rot(-26, 0), Rot(26, 0), Rot(16, 14), Rot(-16, 14) }, Ink);
        p.Polygon(new[] { Rot(-4, -26), Rot(2, -26), Rot(2, 0), Rot(-4, 0) }, Ink);
        p.Polygon(new[] { Rot(2, -24), Rot(2, -4), Rot(20, -8) }, Cream);
    }

    void Blob(EndingPainter p, System.Random rng, float cx, float cy, float rx, float ry, Color c)
    {
        var pts = new Vector2[16];
        for (int k = 0; k < 16; k++)
        {
            float a = k / 16f * Mathf.PI * 2f, r = 1f + ((float)rng.NextDouble() * 2f - 1f) * 0.22f;
            pts[k] = V(cx + Mathf.Cos(a) * rx * r, cy + Mathf.Sin(a) * ry * r);
        }
        p.Polygon(pts, c);
    }

    static void Badge(EndingPainter p, float x, float y, Choice kind, float r)
    {
        var col = kind switch { Choice.Obey => Green, Choice.Refuse => Red, Choice.Fake => Purple, _ => Grey };
        p.Circle(x, y, r + 3, Cream);
        p.Circle(x, y, r, col);
        float s = r / 24f;
        switch (kind)
        {
            case Choice.Obey:
                p.Line(V(x - 11 * s, y + 1 * s), V(x - 3 * s, y + 10 * s), 6 * s, Cream);
                p.Line(V(x - 3 * s, y + 10 * s), V(x + 12 * s, y - 9 * s), 6 * s, Cream);
                break;
            case Choice.Refuse:
                p.Line(V(x - 9 * s, y - 9 * s), V(x + 9 * s, y + 9 * s), 6 * s, Cream);
                p.Line(V(x - 9 * s, y + 9 * s), V(x + 9 * s, y - 9 * s), 6 * s, Cream);
                break;
            case Choice.Fake: // a little mask
                p.RoundRect(x - 14 * s, y - 7 * s, x + 14 * s, y + 7 * s, 6 * s, Cream);
                p.Circle(x - 6 * s, y, 3 * s, col);
                p.Circle(x + 6 * s, y, 3 * s, col);
                break;
            default: // a clock: ran out of time
                p.Ring(x, y, 11 * s, 3 * s, Cream);
                p.Line(V(x, y), V(x, y - 7 * s), 3 * s, Cream);
                p.Line(V(x, y), V(x + 6 * s, y), 3 * s, Cream);
                break;
        }
    }

    void PaintDials(EndingPainter p)
    {
        Paper(p, 1230, 250, 1850, 530, Cream);
        int[] vals = { RunRecord.FinalCaptainTrust, RunRecord.FinalShipSafety, RunRecord.FinalCrewTrust };
        Color[] cols = { C(214, 160, 60), C(88, 150, 214), Green };
        for (int k = 0; k < 3; k++)
        {
            float cx = 1350 + k * 190, cy = 400, v = Mathf.Clamp(vals[k], 0, 100);
            p.Circle(cx, cy, 78, C(250, 244, 230));
            p.Ring(cx, cy, 74, 8, Brass);
            p.Arc(cx, cy, 62, 14, 135, 405, C(220, 210, 190));
            if (v > 0) p.Arc(cx, cy, 62, 14, 135, 135 + 270 * v / 100f, cols[k]);
            float ang = (135 + 270 * v / 100f) * Mathf.Deg2Rad;
            p.Line(V(cx, cy), V(cx + 48 * Mathf.Cos(ang), cy + 48 * Mathf.Sin(ang)), 6, Ink);
            p.Circle(cx, cy, 9, Brass);
        }
    }

    void PaintBottles(EndingPainter p)
    {
        Paper(p, 1560, 560, 1850, 885, Cream);
        var all = (Ending[])System.Enum.GetValues(typeof(Ending));
        for (int k = 0; k < 3 && k < all.Length; k++)
        {
            bool found = EndingEvaluator.IsFound(all[k]);
            float bx = 1615 + k * 90, by = 650;
            p.RoundRect(bx - 30, by, bx + 30, by + 150, 26, found ? C(210, 232, 236) : C(230, 226, 216));
            p.RoundRectOutline(bx - 30, by, bx + 30, by + 150, 26, 4, C(120, 150, 160));
            p.RoundRect(bx - 10, by - 24, bx + 10, by + 4, 3, C(160, 120, 80));
            if (found)
            {
                p.Polygon(new[] { V(bx - 20, by + 110), V(bx + 20, by + 110), V(bx + 12, by + 124), V(bx - 12, by + 124) }, C(90, 60, 40));
                p.Polygon(new[] { V(bx, by + 60), V(bx, by + 108), V(bx + 18, by + 104) }, Cream);
            }
        }
    }

    Texture2D PaintSign(bool on)
    {
        var p = new EndingPainter(320, 100, new Color(0, 0, 0, 0));
        p.RoundRect(16, 18, 316, 96, 14, new Color(0, 0, 0, 0.35f), 8f);
        p.RoundRect(10, 11, 310, 89, 14, on ? C(196, 140, 72) : C(120, 82, 48));
        p.RoundRectOutline(10, 11, 310, 89, 14, 5, C(70, 46, 26));
        var grain = on ? C(110, 74, 42) : C(96, 64, 36);
        for (int k = 0; k < 3; k++) p.Line(V(24, 34 + k * 16), V(296, 34 + k * 16), 2, grain);
        p.Circle(26, 50, 6, C(70, 46, 26));
        p.Circle(294, 50, 6, C(70, 46, 26));
        return p.ToTexture();
    }

    // ---------- uGUI helpers ----------

    Texture2D Track(Texture2D t) { textures.Add(t); return t; }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    /// <summary>A picture at a position given like the mockup: centre in top-left pixel coordinates.</summary>
    RawImage Raw(Transform parent, Texture tex, Vector2 centreTopLeft, Vector2 size)
    {
        var img = new GameObject("Art", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        img.transform.SetParent(parent, false);
        img.texture = tex;
        img.raycastTarget = false;
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(centreTopLeft.x, -centreTopLeft.y);
        r.sizeDelta = size;
        return img;
    }

    /// <summary>Text at a point given like the mockup (top-left pixel coordinates of the parent), anchored by `align`.</summary>
    Text Txt(string s, Font font, int size, Color color, Vector2 at, TextAnchor align, float width, RectTransform parent = null)
    {
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent != null ? parent : root, false);
        t.font = font; t.fontSize = size; t.color = color; t.text = s; t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        float px = align == TextAnchor.MiddleLeft || align == TextAnchor.UpperLeft ? 0f : align == TextAnchor.MiddleRight ? 1f : 0.5f;
        r.pivot = new Vector2(px, 0.5f);
        r.anchoredPosition = new Vector2(at.x, -at.y);
        r.sizeDelta = new Vector2(width, size * 1.6f);
        return t;
    }
}
