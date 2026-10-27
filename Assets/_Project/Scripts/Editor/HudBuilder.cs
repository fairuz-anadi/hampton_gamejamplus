using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the HUD prefab (Prefabs/UI/Resources/HUD.prefab): meters, sun/moon dial, orders, station prompts, subtitles.
/// Owner: Samprity. Styled after AFK Journey's soft painterly HUD (our own art and layout).
///   In the Editor:  Port > Samprity > Build HUD
///   Headless:       Unity.exe -batchmode -projectPath . -executeMethod HudBuilder.BuildBatch -quit
///   Preview:        Unity.exe -batchmode -projectPath . -executeMethod HudBuilder.PreviewBatch   (Logs/HudPreview_*.png)
/// The layout is code so it can be rebuilt; colours and sizes can still be tweaked on the prefab afterwards.
/// </summary>
public static class HudBuilder
{
    public const string PrefabPath = "Assets/_Project/Prefabs/UI/Resources/HUD.prefab";
    const string UI = "Assets/_Project/Art/UI";

    static readonly Color Cream = new Color(0.96f, 0.92f, 0.82f);
    static readonly Color Gold = new Color(0.95f, 0.78f, 0.42f);

    static Font regular, bold, italic;
    static Sprite pill, banner, band, softDisc, ringThin, dialTicks, diamond, sunIcon, moonIcon, keyRound, disc, iconCaptain, iconSafety, iconCrew, iconSpeaker;

    [MenuItem("Port/Samprity/Build HUD")]
    public static void Build()
    {
        LoadArt();
        var root = BuildHud();
        MovementPrototypeBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AddToSandbox();
        AssetDatabase.SaveAssets();
        Debug.Log("[HUD] Built " + PrefabPath);
    }

    public static void BuildBatch() => Build();

    /// <summary>Puts the HUD and the order/meter systems in Samprity_Art so the HUD can be tried on the deck.</summary>
    public static void AddToSandbox()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return;
        var scene = EditorSceneManager.OpenScene(ShipDeckBuilder.ScenePath, OpenSceneMode.Single);
        bool hasHud = false, hasRunner = false;
        foreach (var go in scene.GetRootGameObjects())
        {
            hasHud |= go.GetComponentInChildren<HUD>(true) != null;
            hasRunner |= go.GetComponentInChildren<SandboxGameRunner>(true) != null;
        }
        if (!hasHud) PrefabUtility.InstantiatePrefab(prefab);
        if (!hasRunner) new GameObject("SandboxGameRunner").AddComponent<SandboxGameRunner>();
        EditorSceneManager.SaveScene(scene);
    }

    // ---------- Art ----------

    static void LoadArt()
    {
        regular = AssetDatabase.LoadAssetAtPath<Font>($"{UI}/Fonts/CrimsonText-SemiBold.ttf");
        bold = AssetDatabase.LoadAssetAtPath<Font>($"{UI}/Fonts/CrimsonText-Bold.ttf");
        italic = AssetDatabase.LoadAssetAtPath<Font>($"{UI}/Fonts/CrimsonText-SemiBoldItalic.ttf");
        pill = Sprite("soft_pill", 30);
        banner = Sprite("soft_banner", 0);
        band = Sprite("soft_band", 0);
        softDisc = Sprite("soft_disc", 0);
        ringThin = Sprite("ring_thin", 0);
        dialTicks = Sprite("dial_ticks", 0);
        diamond = Sprite("diamond", 0);
        sunIcon = Sprite("sun", 0);
        moonIcon = Sprite("moon", 0);
        keyRound = Sprite("key_round", 0);
        disc = Sprite("disc", 0);
        iconCaptain = Sprite("icon_captain", 0);
        iconSafety = Sprite("icon_safety", 0);
        iconCrew = Sprite("icon_crew", 0);
        iconSpeaker = Sprite("icon_speaker", 0);
    }

    /// <summary>Imports a PNG from Art/UI/Sprites as a sprite, with a 9-slice border when it stretches.</summary>
    static Sprite Sprite(string name, int border)
    {
        string path = $"{UI}/Sprites/{name}.png";
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        bool dirty = ti.textureType != TextureImporterType.Sprite || ti.spriteBorder != new Vector4(border, border, border, border) || ti.mipmapEnabled;
        if (dirty)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spriteBorder = new Vector4(border, border, border, border);
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------- Layout ----------
    // Soft and painterly, after AFK Journey: no hard boxes or outlines, translucent dark pills and fades, a classical
    // serif in white and cream with gold accents, round medallions, a sun/moon dial.

    static GameObject BuildHud()
    {
        var root = new GameObject("HUD", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var group = root.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        root.AddComponent<HUD>().root = group;

        BuildMeters(root.transform);
        BuildDial(root.transform);
        BuildOrders(root.transform);
        BuildPrompts(root.transform, (RectTransform)root.transform);
        BuildSubtitles(root.transform);
        return root;
    }

    /// <summary>Three round medallions across the top left: a ring that fills with the meter, the icon, the number.</summary>
    static void BuildMeters(Transform parent)
    {
        var box = Box(parent, "Meters", new Vector2(0f, 1f), new Vector2(34f, -26f), new Vector2(420f, 170f), new Vector2(0f, 1f));
        var panelComp = box.gameObject.AddComponent<MeterPanel>();
        panelComp.captain = Medallion(box, 0, "CAPTAIN", iconCaptain, Gold, 0, new[] { 25, 75 });
        panelComp.safety = Medallion(box, 1, "SAFETY", iconSafety, new Color(0.55f, 0.8f, 1f), 30, new[] { 50 });
        panelComp.crew = Medallion(box, 2, "CREW", iconCrew, new Color(0.6f, 0.9f, 0.62f), 30, new[] { 30, 70 });
    }

    static MeterBar Medallion(RectTransform box, int i, string label, Sprite icon, Color color, int danger, int[] ticks)
    {
        var m = Box(box, "Meter_" + label, new Vector2(0f, 1f), new Vector2(i * 136f, 0f), new Vector2(124f, 170f), new Vector2(0f, 1f));
        var medal = Box(m, "Medallion", new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(96f, 96f), new Vector2(0.5f, 1f));
        Img(medal, "Shade", softDisc, new Color(0f, 0f, 0f, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(132f, 132f), Image.Type.Simple);
        Img(medal, "RingBack", ringThin, new Color(1f, 1f, 1f, 0.16f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Simple);
        var fill = Img(medal, "RingFill", ringThin, color, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Filled);
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = true;
        foreach (int t in ticks)
        {
            // Small pip on the ring where the meter changes behaviour (e.g. the Captain at 25 and 75).
            float a = t / 100f * Mathf.PI * 2f;
            Img(medal, "Pip_" + t, disc, Cream, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 44f, new Vector2(7f, 7f), Image.Type.Simple);
        }
        Img(medal, "Icon", icon, Cream, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50f, 50f), Image.Type.Simple);

        var value = Txt(m, "Value", bold, 34, Color.white, "50", new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(120f, 40f), TextAnchor.UpperCenter);
        Txt(m, "Label", regular, 16, Cream, Spaced(label), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(124f, 22f), TextAnchor.UpperCenter);
        var delta = Txt(m, "Delta", bold, 22, Color.white, "", new Vector2(1f, 1f), new Vector2(-2f, -6f), new Vector2(50f, 26f), TextAnchor.UpperRight);
        delta.color = new Color(1f, 1f, 1f, 0f);

        var mb = m.gameObject.AddComponent<MeterBar>();
        mb.fill = fill;
        mb.valueText = value;
        mb.deltaText = delta;
        mb.color = color;
        mb.dangerBelow = danger;
        return mb;
    }

    /// <summary>Top right: the time of day, as a sun and moon riding round a dial.</summary>
    static void BuildDial(Transform parent)
    {
        var box = Box(parent, "TimeOfDay", new Vector2(1f, 1f), new Vector2(-36f, -24f), new Vector2(170f, 206f), new Vector2(1f, 1f));
        var group = box.gameObject.AddComponent<CanvasGroup>();
        var dial = Box(box, "Dial", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(164f, 164f), new Vector2(0.5f, 1f));
        Img(dial, "Shade", softDisc, new Color(0f, 0f, 0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(214f, 214f), Image.Type.Simple);
        var face = Img(dial, "Face", disc, new Color(0.36f, 0.56f, 0.82f, 0.85f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(138f, 138f), Image.Type.Simple);
        Img(dial, "Ticks", dialTicks, new Color(1f, 0.95f, 0.85f, 0.75f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Simple);
        var orbit = Box(dial, "Orbit", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        var sun = Img(orbit, "Sun", sunIcon, new Color(1f, 0.83f, 0.42f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 42f), new Vector2(46f, 46f), Image.Type.Simple);
        var moon = Img(orbit, "Moon", moonIcon, new Color(0.88f, 0.92f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -42f), new Vector2(40f, 40f), Image.Type.Simple);
        // Thin gold ring over the face edge, like a compass bezel.
        Img(dial, "Bezel", ringThin, new Color(Gold.r, Gold.g, Gold.b, 0.8f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(146f, 146f), Image.Type.Simple);
        var name = Txt(box, "Phase", regular, 20, Cream, Spaced("MORNING"), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(170f, 30f), TextAnchor.LowerCenter);

        var comp = box.gameObject.AddComponent<TimeOfDayDial>();
        comp.group = group;
        comp.orbit = orbit;
        comp.sun = sun.rectTransform;
        comp.moon = moon.rectTransform;
        comp.face = face;
        comp.phaseName = name;
    }

    /// <summary>Right side, under the dial: orders as quest-tracker entries.</summary>
    static void BuildOrders(Transform parent)
    {
        var box = Box(parent, "Orders", new Vector2(1f, 1f), new Vector2(-24f, -252f), new Vector2(620f, 300f), new Vector2(1f, 1f));
        var cards = box.gameObject.AddComponent<OrderCards>();
        cards.cards = new[] { Card(box, 0), Card(box, 1) };
    }

    static OrderCardUI Card(RectTransform box, int i)
    {
        var slot = Box(box, "Order_" + (i + 1), new Vector2(1f, 1f), new Vector2(0f, -i * 148f), new Vector2(620f, 136f), new Vector2(1f, 1f));
        var group = slot.gameObject.AddComponent<CanvasGroup>();
        var body = Box(slot, "Body", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.sizeDelta = Vector2.zero;
        Img(body, "Pill", pill, new Color(0.03f, 0.04f, 0.07f, 0.62f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Sliced);

        // Diamond quest marker with the timer as a ring round it.
        var marker = Box(body, "Marker", new Vector2(0f, 1f), new Vector2(54f, -52f), new Vector2(76f, 76f), new Vector2(0.5f, 0.5f));
        Img(marker, "RingBack", ringThin, new Color(1f, 1f, 1f, 0.14f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Simple);
        var fill = Img(marker, "RingFill", ringThin, Cream, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Filled);
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = false;
        Img(marker, "Diamond", diamond, Gold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f), Image.Type.Simple);
        var seconds = Txt(body, "Seconds", bold, 22, Cream, "30", new Vector2(0f, 1f), new Vector2(34f, -96f), new Vector2(40f, 28f), TextAnchor.UpperCenter);

        var header = Txt(body, "Header", regular, 17, Gold, "ORDER 1", new Vector2(0f, 1f), new Vector2(104f, -10f), new Vector2(490f, 22f), TextAnchor.UpperLeft);
        var line = Txt(body, "CaptainLine", italic, 25, Color.white, "“Morning, crew.”", new Vector2(0f, 1f), new Vector2(104f, -32f), new Vector2(496f, 62f), TextAnchor.UpperLeft);
        line.horizontalOverflow = HorizontalWrapMode.Wrap;
        line.lineSpacing = 0.9f;
        var where = Txt(body, "Where", regular, 19, Cream, "Boiler", new Vector2(0f, 0f), new Vector2(104f, 10f), new Vector2(260f, 26f), TextAnchor.LowerLeft);

        var hints = Box(body, "Hints", new Vector2(1f, 0f), new Vector2(-18f, 8f), new Vector2(300f, 34f), new Vector2(1f, 0f));
        var reportKey = KeyHint(hints, "Report", "R", "Report done", new Vector2(-104f, 0f), 140f);
        var refuseKey = KeyHint(hints, "Refuse", "X", "Refuse", new Vector2(0f, 0f), 92f);
        var botNote = Txt(body, "BotNote", italic, 19, Cream, "Number Three is on it", new Vector2(1f, 0f), new Vector2(-22f, 10f), new Vector2(280f, 26f), TextAnchor.LowerRight);

        var stamp = Txt(body, "Stamp", bold, 34, Cream, "REPORTED DONE", new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(420f, 50f), TextAnchor.MiddleRight);
        stamp.rectTransform.pivot = new Vector2(1f, 0.5f);
        stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 4f);
        var o = stamp.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.75f);
        o.effectDistance = new Vector2(2f, -2f);

        var card = slot.gameObject.AddComponent<OrderCardUI>();
        card.group = group;
        card.body = body;
        card.header = header;
        card.captainLine = line;
        card.where = where;
        card.timerText = seconds;
        card.timerFill = fill;
        card.stamp = stamp;
        card.botNote = botNote;
        card.hints = hints.gameObject;
        card.reportKey = reportKey;
        card.refuseKey = refuseKey;
        card.timerCalm = Cream;
        return card;
    }

    /// <summary>(R) Report done : a round key with its letter, and a label to its right.</summary>
    static Text KeyHint(RectTransform parent, string name, string key, string label, Vector2 rightEdge, float width)
    {
        var g = Box(parent, name, new Vector2(1f, 0.5f), rightEdge, new Vector2(width, 34f), new Vector2(1f, 0.5f));
        var cap = Img(g, "Key", keyRound, Cream, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(15f, 0f), new Vector2(30f, 30f), Image.Type.Simple);
        var k = Txt(cap.rectTransform, "Letter", bold, 18, Cream, key, Vector2.zero, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, true);
        k.rectTransform.offsetMin = new Vector2(0f, 1f);
        Txt(g, "Label", regular, 19, Cream, label, new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(width - 36f, 28f), TextAnchor.MiddleLeft).rectTransform.pivot = new Vector2(0f, 0.5f);
        return k;
    }

    /// <summary>Floating over stations: a round key and a soft fading label, no box.</summary>
    static void BuildPrompts(Transform parent, RectTransform canvasRect)
    {
        var layer = Box(parent, "StationPrompts", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.sizeDelta = Vector2.zero;
        var sp = layer.gameObject.AddComponent<StationPrompts>();
        sp.canvasRect = canvasRect;
        sp.prompts = new StationPromptUI[3];
        sp.normalTint = Cream;
        for (int i = 0; i < 3; i++)
        {
            // Anchored at the canvas centre so ScreenPointToLocalPointInRectangle positions map straight on.
            var p = Box(layer, "Prompt_" + (i + 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 62f), new Vector2(0.5f, 0f));
            var g = p.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            Img(p, "Fade", banner, new Color(0.02f, 0.03f, 0.06f, 0.82f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, 0f), new Vector2(320f, 62f), Image.Type.Simple);
            Img(p, "KeyShade", disc, new Color(0.02f, 0.03f, 0.06f, 0.75f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(50f, 50f), Image.Type.Simple);
            var cap = Img(p, "Key", keyRound, Cream, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(50f, 50f), Image.Type.Simple);
            var k = Txt(cap.rectTransform, "Letter", bold, 26, Cream, "E", Vector2.zero, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, true);
            k.rectTransform.offsetMin = new Vector2(0f, 2f);
            var title = Txt(p, "Title", regular, 16, Gold, "Boiler", new Vector2(0f, 1f), new Vector2(64f, -4f), new Vector2(230f, 22f), TextAnchor.UpperLeft);
            var action = Txt(p, "Action", regular, 23, Color.white, "Hold  ·  Shovel coal", new Vector2(0f, 0f), new Vector2(64f, 6f), new Vector2(230f, 30f), TextAnchor.LowerLeft);

            var ui = p.gameObject.AddComponent<StationPromptUI>();
            ui.group = g;
            ui.rect = p;
            ui.title = title;
            ui.action = action;
            ui.key = k;
            ui.keycap = cap;
            sp.prompts[i] = ui;
        }
    }

    /// <summary>The Captain's line, bottom centre, on a soft band that fades at both ends.</summary>
    static void BuildSubtitles(Transform parent)
    {
        var box = Box(parent, "CaptainSubtitles", new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(1400f, 220f), new Vector2(0.5f, 0f));
        var group = box.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        var panelRt = Box(box, "Panel", new Vector2(0.5f, 0f), Vector2.zero, new Vector2(900f, 120f), new Vector2(0.5f, 0f));
        Img(panelRt, "Band", band, new Color(0.02f, 0.03f, 0.06f, 0.78f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Simple);
        // A hairline of the part's colour across the top of the band.
        var trim = Img(panelRt, "Trim", band, Gold, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -10f), new Vector2(-120f, 2f), Image.Type.Simple);
        var head = Box(panelRt, "Head", new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(240f, 28f), new Vector2(0.5f, 1f));
        var horn = Img(head, "Speaker", iconSpeaker, Gold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(30f, 30f), Image.Type.Simple);
        var label = Txt(head, "Label", regular, 18, Gold, Spaced("THE CAPTAIN"), new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(190f, 26f), TextAnchor.MiddleLeft);
        label.rectTransform.pivot = new Vector2(0f, 0.5f);
        var line = Txt(panelRt, "Line", italic, 32, Color.white, "", new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(700f, 44f), TextAnchor.UpperCenter);
        line.horizontalOverflow = HorizontalWrapMode.Wrap;
        line.lineSpacing = 0.92f;
        // Invisible twin of the line, used only to measure the finished line before it types out.
        var sizer = Txt(panelRt, "Sizer", italic, 32, new Color(0f, 0f, 0f, 0f), "", new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(700f, 44f), TextAnchor.UpperCenter);
        sizer.horizontalOverflow = HorizontalWrapMode.Wrap;
        sizer.lineSpacing = 0.92f;
        Object.DestroyImmediate(sizer.GetComponent<Shadow>());
        var staticText = Txt(panelRt, "Static", regular, 20, new Color(0.82f, 0.84f, 0.88f), Spaced("~ kssshhht ~"), new Vector2(0.5f, 1f), new Vector2(0f, 24f), new Vector2(500f, 26f), TextAnchor.LowerCenter);
        staticText.enabled = false;

        var subs = box.gameObject.AddComponent<CaptainSubtitles>();
        subs.group = group;
        subs.panel = panelRt;
        subs.line = line;
        subs.sizer = sizer;
        subs.label = label;
        subs.staticText = staticText;
        subs.trim = trim;
        subs.horn = horn;
        subs.padding = new Vector2(320f, 74f);
        subs.maxTextWidth = 1000f;
        subs.calm = new CaptainSubtitles.Style { trim = new Color(Gold.r, Gold.g, Gold.b, 0.9f), label = Gold, text = Color.white, tremble = 0f };
        subs.off = new CaptainSubtitles.Style { trim = new Color(0.66f, 0.76f, 0.9f, 0.9f), label = new Color(0.72f, 0.8f, 0.92f), text = new Color(0.92f, 0.95f, 1f), tremble = 0f };
        subs.truth = new CaptainSubtitles.Style { trim = new Color(0.9f, 0.22f, 0.18f, 0.95f), label = new Color(1f, 0.42f, 0.36f), text = new Color(1f, 0.9f, 0.86f), tremble = 1.4f };
    }

    /// <summary>Letter-spaced capitals, the way the reference sets its small labels.</summary>
    static string Spaced(string s) => string.Join(" ", s.ToCharArray()).Replace("   ", "    ");

    // ---------- Helpers ----------

    static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static Image Img(Transform parent, string name, Sprite sprite, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Image.Type type)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = sprite == null && type == Image.Type.Sliced ? Image.Type.Simple : type;
        img.raycastTarget = false;
        return img;
    }

    static Text Txt(Transform parent, string name, Font font, int size, Color color, string text, Vector2 anchor, Vector2 pos, Vector2 boxSize, TextAnchor align, bool stretch = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        if (stretch)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
        }
        else
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = boxSize;
        }
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.text = text;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(1.5f, -2f);
        return t;
    }

    // ---------- Preview ----------

    /// <summary>Renders the HUD over the deck: one calm shot and one late-voyage shot with low meters.</summary>
    public static void PreviewBatch()
    {
        var scene = EditorSceneManager.OpenScene(ShipDeckBuilder.ScenePath, OpenSceneMode.Single);
        ThirdPersonMover mover = null; AutoFollowCamera follow = null; PartMood mood = null; HUD hud = null;
        foreach (var r in scene.GetRootGameObjects())
        {
            if (mover == null) mover = r.GetComponentInChildren<ThirdPersonMover>();
            if (follow == null) follow = r.GetComponentInChildren<AutoFollowCamera>();
            if (mood == null) mood = r.GetComponentInChildren<PartMood>();
            if (hud == null) hud = r.GetComponentInChildren<HUD>(true);
        }
        mover.readOwnInput = false;
        var cam = follow.GetComponent<Camera>();
        var canvas = hud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 0.5f;
        hud.root.alpha = 1f;
        var rt = new RenderTexture(1920, 1080, 24);
        cam.targetTexture = rt;
        cam.aspect = 1920f / 1080f;
        cam.Render();

        var feed = Resources.Load<OrderData>("Orders/01_FeedBoiler");
        var ice = Resources.Load<OrderData>("Orders/07_FileIceWarning");
        var lifeboats = Resources.Load<OrderData>("Orders/09_LockLifeboats");
        var meters = hud.GetComponentInChildren<MeterPanel>(true);
        var cards = hud.GetComponentInChildren<OrderCards>(true).cards;
        var prompts = hud.GetComponentInChildren<StationPrompts>(true);
        var subs = hud.GetComponentInChildren<CaptainSubtitles>(true);
        var dial = hud.GetComponentInChildren<TimeOfDayDial>(true);

        void Shot(string name, int part, int ct, int ss, int crew, int ctWas, (OrderData d, int n, int seat, bool human, float left)[] orders, Vector3 at, float yaw, string station, string verb, string subtitle, bool withStatic, string timeOfDay)
        {
            if (mood.cycle != null) mood.Preview(part, mood.cycle.PhaseOf(timeOfDay)); else mood.ApplyImmediate(part);
            subs.Preview(subtitle, part, withStatic);
            if (mood.cycle != null) dial.Show(mood.cycle, mood.cycle.PhaseOf(timeOfDay));
            MovementChecks.Sim(mover, follow, at, yaw, (1.2f, Vector2.zero));
            meters.captain.SetValue(ctWas); meters.captain.SetValue(ct);
            meters.safety.SetValue(ss);
            meters.crew.SetValue(crew);
            for (int i = 0; i < cards.Length; i++)
            {
                if (i < orders.Length)
                {
                    var o = orders[i];
                    cards[i].Show(o.n, o.d, o.seat, o.human, null);
                    cards[i].SetTimer(o.left, o.d.timer);
                    cards[i].SnapIn();
                }
                else cards[i].group.alpha = 0f;
            }
            foreach (var p in prompts.prompts) p.group.alpha = 0f;
            var marker = GameObject.Find(station);
            Canvas.ForceUpdateCanvases();
            // Same maths as the overlay canvas at runtime; the camera-space canvas used here isn't laid out yet in edit mode.
            Vector3 screen = cam.WorldToScreenPoint(marker.transform.position + Vector3.up * prompts.heightAbove);
            var size = ((RectTransform)hud.transform).rect.size;
            var local = prompts.KeepOnScreen(new Vector2((screen.x / cam.pixelWidth - 0.5f) * size.x, (screen.y / cam.pixelHeight - 0.5f) * size.y));
            prompts.prompts[0].Show(local, HudText.StationName((StationType)System.Enum.Parse(typeof(StationType), station.Substring(8))), "Hold  ·  " + verb, "E", prompts.normalTint);
            prompts.prompts[0].SnapIn();
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes($"Logs/HudPreview_{name}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        Shot("1_calm", 1, 55, 82, 60, 50,
            new[] { (feed, 1, 1, true, 19f), (ice, 2, 3, false, 4f) },
            new Vector3(-0.8f, 0.05f, 6.2f), 180f, "Station_Boiler", "Shovel coal",
            "Good work, lads. We'll make history.", false, "Morning");
        Shot("2_off", 2, 82, 41, 24, 76,
            new[] { (lifeboats, 9, 2, true, 3f) },
            new Vector3(3.9f, 0.05f, 13.4f), 90f, "Station_Doors", "Pull lever",
            "I know the wireless is busy. I know who's at it.", true, "Afternoon");
        Shot("3_truth", 3, 64, 33, 40, 64,
            new (OrderData, int, int, bool, float)[0],
            new Vector3(0f, 0.05f, 26.3f), 0f, "Station_Wheel", "Steer",
            "You want to know why. Fine. No one remembers a careful captain. They remember the fastest crossing, and the name on the front page.", false, "Night");

        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        EditorApplication.Exit(0);
    }
}
