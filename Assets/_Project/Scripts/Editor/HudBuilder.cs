using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the HUD prefab (Prefabs/UI/Resources/HUD.prefab): meters, order cards, station prompts. Owner: Samprity.
///   In the Editor:  Port > Samprity > Build HUD
///   Headless:       Unity.exe -batchmode -projectPath . -executeMethod HudBuilder.BuildBatch -quit
///   Preview:        Unity.exe -batchmode -projectPath . -executeMethod HudBuilder.PreviewBatch   (Logs/HudPreview_*.png)
/// The layout is code so it can be rebuilt; colours and sizes can still be tweaked on the prefab afterwards.
/// </summary>
public static class HudBuilder
{
    public const string PrefabPath = "Assets/_Project/Prefabs/UI/Resources/HUD.prefab";
    const string UI = "Assets/_Project/Art/UI";

    static readonly Color Navy = new Color(0.05f, 0.08f, 0.15f, 0.86f);
    static readonly Color Brass = new Color(0.86f, 0.68f, 0.34f);
    static readonly Color Cream = new Color(0.96f, 0.92f, 0.82f);
    static readonly Color Muted = new Color(0.68f, 0.74f, 0.85f);

    static Font regular, bold, italic;
    static Sprite panel, outline, bar, ring, disc, keycap, iconCaptain, iconSafety, iconCrew, iconSpeaker;

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
        regular = AssetDatabase.LoadAssetAtPath<Font>($"{UI}/Fonts/AlegreyaSans-Regular.ttf");
        bold = AssetDatabase.LoadAssetAtPath<Font>($"{UI}/Fonts/AlegreyaSans-Bold.ttf");
        italic = AssetDatabase.LoadAssetAtPath<Font>($"{UI}/Fonts/AlegreyaSans-Italic.ttf");
        panel = Sprite("panel", 18);
        outline = Sprite("panel_outline", 18);
        bar = Sprite("bar", 10);
        keycap = Sprite("keycap", 13);
        ring = Sprite("ring", 0);
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
        BuildOrders(root.transform);
        BuildPrompts(root.transform, (RectTransform)root.transform);
        BuildSubtitles(root.transform);
        return root;
    }

    /// <summary>The Captain's line, bottom centre. CaptainSubtitles sizes the panel to each line.</summary>
    static void BuildSubtitles(Transform parent)
    {
        var box = Box(parent, "CaptainSubtitles", new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(1200f, 220f), new Vector2(0.5f, 0f));
        var group = box.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        var panelRt = Box(box, "Panel", new Vector2(0.5f, 0f), Vector2.zero, new Vector2(760f, 112f), new Vector2(0.5f, 0f));
        Img(panelRt, "Background", panel, new Color(0.04f, 0.06f, 0.11f, 0.9f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Sliced);
        var trim = Img(panelRt, "Trim", outline, Brass, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Sliced);
        var horn = Img(panelRt, "Speaker", iconSpeaker, Brass, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(58f, 0f), new Vector2(64f, 64f), Image.Type.Simple);
        var label = Txt(panelRt, "Label", bold, 16, Brass, "THE  CAPTAIN", new Vector2(0f, 1f), new Vector2(112f, -14f), new Vector2(400f, 22f), TextAnchor.UpperLeft);
        var line = Txt(panelRt, "Line", italic, 30, Cream, "", new Vector2(0f, 1f), new Vector2(112f, -40f), new Vector2(600f, 40f), TextAnchor.UpperLeft);
        line.horizontalOverflow = HorizontalWrapMode.Wrap;
        line.lineSpacing = 0.95f;
        // Invisible twin of the line, used only to measure the finished line before it types out.
        var sizer = Txt(panelRt, "Sizer", italic, 30, new Color(0f, 0f, 0f, 0f), "", new Vector2(0f, 1f), new Vector2(112f, -40f), new Vector2(600f, 40f), TextAnchor.UpperLeft);
        sizer.horizontalOverflow = HorizontalWrapMode.Wrap;
        sizer.lineSpacing = 0.95f;
        Object.DestroyImmediate(sizer.GetComponent<Shadow>());
        var staticText = Txt(panelRt, "Static", bold, 20, new Color(0.8f, 0.82f, 0.86f), "~  k s s s h h h t  ~", new Vector2(0.5f, 1f), new Vector2(0f, 30f), new Vector2(500f, 26f), TextAnchor.LowerCenter);
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
    }

    static void BuildMeters(Transform parent)
    {
        var box = Box(parent, "Meters", new Vector2(0f, 1f), new Vector2(36f, -30f), new Vector2(440f, 208f), new Vector2(0f, 1f));
        Img(box, "Background", panel, Navy, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Sliced);
        Img(box, "Trim", outline, new Color(Brass.r, Brass.g, Brass.b, 0.55f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Sliced);
        var panelComp = box.gameObject.AddComponent<MeterPanel>();
        panelComp.captain = MeterRow(box, 0, "CAPTAIN'S  TRUST", iconCaptain, Brass, 0, new[] { 25, 75 });
        panelComp.safety = MeterRow(box, 1, "SHIP  SAFETY", iconSafety, new Color(0.48f, 0.74f, 0.98f), 30, new[] { 50 });
        panelComp.crew = MeterRow(box, 2, "CREW  TRUST", iconCrew, new Color(0.52f, 0.84f, 0.6f), 30, new[] { 30, 70 });
    }

    static MeterBar MeterRow(RectTransform box, int i, string label, Sprite icon, Color color, int danger, int[] ticks)
    {
        var row = Box(box, "Meter_" + label.Replace(" ", "").Replace("'", ""), new Vector2(0f, 1f), new Vector2(18f, -18f - i * 62f), new Vector2(404f, 56f), new Vector2(0f, 1f));
        Img(row, "Icon", icon, color, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(46f, 46f), Image.Type.Simple);
        Txt(row, "Label", bold, 16, Muted, label, new Vector2(0f, 1f), new Vector2(60f, -2f), new Vector2(260f, 22f), TextAnchor.UpperLeft);

        var track = Box(row, "Track", new Vector2(0f, 0f), new Vector2(60f, 10f), new Vector2(268f, 16f), new Vector2(0f, 0f));
        Img(track, "Back", bar, new Color(1f, 1f, 1f, 0.12f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Sliced);
        var fill = Img(track, "Fill", bar, color, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Filled);
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        foreach (int t in ticks)
            Img(track, "Tick_" + t, null, new Color(1f, 1f, 1f, 0.45f), new Vector2(t / 100f, 0f), new Vector2(t / 100f, 1f), Vector2.zero, new Vector2(2f, 6f), Image.Type.Simple);

        var value = Txt(row, "Value", bold, 26, Cream, "50", new Vector2(0f, 0f), new Vector2(338f, 2f), new Vector2(54f, 34f), TextAnchor.LowerRight);
        var delta = Txt(row, "Delta", bold, 18, Cream, "", new Vector2(0f, 1f), new Vector2(338f, 0f), new Vector2(64f, 22f), TextAnchor.UpperRight);
        delta.color = new Color(1f, 1f, 1f, 0f);

        var mb = row.gameObject.AddComponent<MeterBar>();
        mb.fill = fill;
        mb.valueText = value;
        mb.deltaText = delta;
        mb.color = color;
        mb.dangerBelow = danger;
        return mb;
    }

    static void BuildOrders(Transform parent)
    {
        var box = Box(parent, "Orders", new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(780f, 330f), new Vector2(0.5f, 1f));
        var cards = box.gameObject.AddComponent<OrderCards>();
        cards.cards = new[] { Card(box, 0), Card(box, 1) };
    }

    static OrderCardUI Card(RectTransform box, int i)
    {
        var slot = Box(box, "Card_" + (i + 1), new Vector2(0.5f, 1f), new Vector2(0f, -i * 166f), new Vector2(780f, 154f), new Vector2(0.5f, 1f));
        var group = slot.gameObject.AddComponent<CanvasGroup>();
        var body = Box(slot, "Body", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.sizeDelta = Vector2.zero;
        Img(body, "Background", panel, Navy, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Sliced);
        Img(body, "Trim", outline, new Color(Brass.r, Brass.g, Brass.b, 0.7f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Sliced);

        var timer = Box(body, "Timer", new Vector2(0f, 0.5f), new Vector2(78f, 0f), new Vector2(112f, 112f), new Vector2(0.5f, 0.5f));
        Img(timer, "RingBack", ring, new Color(1f, 1f, 1f, 0.13f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Simple);
        var fill = Img(timer, "RingFill", ring, Cream, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Filled);
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = false;
        var seconds = Txt(timer, "Seconds", bold, 38, Cream, "30", Vector2.zero, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, true);

        var header = Txt(body, "Header", bold, 17, Brass, "ORDER 1", new Vector2(0f, 1f), new Vector2(156f, -14f), new Vector2(600f, 22f), TextAnchor.UpperLeft);
        var line = Txt(body, "CaptainLine", italic, 25, Cream, "\u201CMorning, crew.\u201D", new Vector2(0f, 1f), new Vector2(156f, -38f), new Vector2(600f, 66f), TextAnchor.UpperLeft);
        line.horizontalOverflow = HorizontalWrapMode.Wrap;
        line.lineSpacing = 0.95f;
        var where = Txt(body, "Where", regular, 19, Muted, "Boiler", new Vector2(0f, 0f), new Vector2(156f, 14f), new Vector2(330f, 26f), TextAnchor.LowerLeft);

        var hints = Box(body, "Hints", new Vector2(1f, 0f), new Vector2(-20f, 12f), new Vector2(300f, 34f), new Vector2(1f, 0f));
        var reportKey = KeyHint(hints, "Report", "R", "Report done", new Vector2(-160f, 0f));
        var refuseKey = KeyHint(hints, "Refuse", "X", "Refuse", new Vector2(0f, 0f));
        var botNote = Txt(body, "BotNote", italic, 19, Muted, "Number Three is on it", new Vector2(1f, 0f), new Vector2(-22f, 14f), new Vector2(300f, 26f), TextAnchor.LowerRight);

        var stamp = Txt(body, "Stamp", bold, 40, Cream, "REPORTED DONE", new Vector2(1f, 0.5f), new Vector2(-40f, 4f), new Vector2(440f, 60f), TextAnchor.MiddleRight);
        stamp.rectTransform.pivot = new Vector2(1f, 0.5f);
        stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 6f);
        var o = stamp.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.8f);
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
        return card;
    }

    /// <summary>[R] Report done : a key cap with a letter, and a label to its right.</summary>
    static Text KeyHint(RectTransform parent, string name, string key, string label, Vector2 rightEdge)
    {
        var g = Box(parent, name, new Vector2(1f, 0.5f), rightEdge, new Vector2(150f, 34f), new Vector2(1f, 0.5f));
        var cap = Img(g, "KeyCap", keycap, Cream, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(17f, 0f), new Vector2(34f, 34f), Image.Type.Sliced);
        var k = Txt(cap.rectTransform, "Key", bold, 20, Cream, key, Vector2.zero, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, true);
        k.rectTransform.offsetMin = new Vector2(0f, 2f);
        Txt(g, "Label", regular, 20, Cream, label, new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(110f, 30f), TextAnchor.MiddleLeft).rectTransform.pivot = new Vector2(0f, 0.5f);
        return k;
    }

    static void BuildPrompts(Transform parent, RectTransform canvasRect)
    {
        var layer = Box(parent, "StationPrompts", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.sizeDelta = Vector2.zero;
        var sp = layer.gameObject.AddComponent<StationPrompts>();
        sp.canvasRect = canvasRect;
        sp.prompts = new StationPromptUI[3];
        for (int i = 0; i < 3; i++)
        {
            // Anchored at the canvas centre so ScreenPointToLocalPointInRectangle positions map straight on.
            var p = Box(layer, "Prompt_" + (i + 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 64f), new Vector2(0.5f, 0f));
            var g = p.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            Img(p, "Background", panel, Navy, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Sliced);
            var cap = Img(p, "KeyCap", keycap, Cream, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(40f, 40f), Image.Type.Sliced);
            var k = Txt(cap.rectTransform, "Key", bold, 24, Cream, "E", Vector2.zero, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, true);
            k.rectTransform.offsetMin = new Vector2(0f, 2f);
            var title = Txt(p, "Title", bold, 15, Brass, "BOILER", new Vector2(0f, 1f), new Vector2(66f, -8f), new Vector2(224f, 20f), TextAnchor.UpperLeft);
            var action = Txt(p, "Action", regular, 21, Cream, "Hold  ·  Shovel coal", new Vector2(0f, 0f), new Vector2(66f, 8f), new Vector2(224f, 28f), TextAnchor.LowerLeft);
            // Small pointer under the box.
            var tip = Img(p, "Tip", disc, Navy, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -6f), new Vector2(12f, 12f), Image.Type.Simple);
            tip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

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
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
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

        void Shot(string name, int part, int ct, int ss, int crew, int ctWas, (OrderData d, int n, int seat, bool human, float left)[] orders, Vector3 at, float yaw, string station, string verb, string subtitle, bool withStatic, string timeOfDay)
        {
            if (mood.cycle != null) mood.Preview(part, mood.cycle.PhaseOf(timeOfDay)); else mood.ApplyImmediate(part);
            subs.Preview(subtitle, part, withStatic);
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
            prompts.prompts[0].Show(local, HudText.StationName((StationType)System.Enum.Parse(typeof(StationType), station.Substring(8))), "Hold  ·  " + verb, "E", prompts.normalTint, 0f);
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
