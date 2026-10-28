using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the MiniMap prefab (Prefabs/UI/MiniMap/Resources/MiniMap.prefab): a bottom-right deck map that self-spawns
/// onto Samprity's HUD canvas at runtime (see MiniMap.cs). Doesn't touch HUD.prefab, HudBuilder.cs or any of her
/// files. Every sprite it uses (hull outline, circle, ring, triangle, rounded rect) is generated procedurally below
/// and saved as its own asset under Art/UI/MiniMap/Generated — nothing is read from Samprity's Art/UI/Sprites pool.
/// Palette and font files ARE read (not written) from her HUD/HudBuilder conventions, for a matching look.
/// Owner: Rabib.
///   In the Editor:  Rabib > Build MiniMap
///   Headless:       Unity.exe -batchmode -projectPath . -executeMethod MiniMapBuilder.BuildBatch -quit
/// Safe to run again: fully rebuilds the prefab and all generated sprites from scratch each time.
/// </summary>
public static class MiniMapBuilder
{
    public const string PrefabPath = "Assets/_Project/Prefabs/UI/MiniMap/Resources/MiniMap.prefab";
    const string SpriteFolder = "Assets/_Project/Art/UI/MiniMap/Generated";
    const string FontFolder = "Assets/_Project/Art/UI/Fonts";

    // Samprity's HUD palette (HudBuilder.cs: Cream, Gold), read-only, reused for a matching look.
    static readonly Color Cream = new Color(0.96f, 0.92f, 0.82f);
    static readonly Color Gold = new Color(0.95f, 0.78f, 0.42f);
    static readonly Color Navy = new Color(0.13f, 0.18f, 0.28f, 0.95f);
    static readonly Color GlyphGold = new Color(0.88f, 0.72f, 0.4f, 0.95f);
    static readonly Color DarkOutline = new Color(0.03f, 0.04f, 0.07f, 0.9f);
    static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.55f);
    static readonly Color IceWarnColor = new Color(0.95f, 0.78f, 0.25f, 1f);

    // Order here must match MiniMap.RoomIds exactly. Alpha raised so tints are distinguishable against the
    // lightened hull fill below; always these fixed per-room values, never state-based.
    static readonly (string id, Color tint)[] RoomTints =
    {
        ("Boiler",   new Color(0.6f, 0.28f, 0.17f, 0.42f)),
        ("Corridor", new Color(0.52f, 0.52f, 0.58f, 0.3f)),
        ("Wireless", new Color(0.26f, 0.42f, 0.62f, 0.38f)),
        ("Bridge",   new Color(0.72f, 0.6f, 0.32f, 0.4f)),
        ("Bow",      new Color(0.78f, 0.86f, 0.92f, 0.32f)),
    };

    const float RootWidth = 184f, RootHeight = 284f;
    const float Margin = 6f, TitleHeight = 22f, Gap = 4f, BowTickHeight = 20f;

    static Font regular, bold;

    struct CrewPart { public RectTransform root; public Image dot; public RectTransform arrow; public Image ring; }
    struct IcePart { public RectTransform root; public Image core; public Image glow; }
    struct RoomPart { public RectTransform root; public RectTransform label; }
    struct PingPart { public RectTransform root; public Image ring; }

    [MenuItem("Rabib/Build MiniMap")]
    public static void Build()
    {
        regular = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/CrimsonText-SemiBold.ttf");
        bold = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/CrimsonText-Bold.ttf");

        var root = BuildMiniMap();
        MovementPrototypeBuilder.EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[MiniMap] Built " + PrefabPath);
    }

    public static void BuildBatch() => Build();

    static GameObject BuildMiniMap()
    {
        var circle = MakeCircle("minimap_circle", 64);
        var ring = MakeRing("minimap_ring", 64, 0.2f);
        var triangle = MakeTriangle("minimap_triangle", 64);
        var roundedRect = MakeRoundedRect("minimap_roundedrect", 64, 64, 0.3f);
        var hullOuter = MakeHull("minimap_hull_outer", 128, 208, 0f);
        var hullInner = MakeHull("minimap_hull_inner", 128, 208, 4f);

        var root = new GameObject("MiniMap", typeof(RectTransform));
        var rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(1f, 0f);
        rootRect.anchoredPosition = new Vector2(-24f, 226f);
        rootRect.sizeDelta = new Vector2(RootWidth, RootHeight);
        var fadeGroup = root.AddComponent<CanvasGroup>();
        fadeGroup.interactable = false;
        fadeGroup.blocksRaycasts = false;
        fadeGroup.alpha = 0f;

        var title = Box(root.transform, "TitleStrip", new Vector2(0.5f, 1f), new Vector2(0f, -Margin), new Vector2(RootWidth - Margin * 2f, TitleHeight), new Vector2(0.5f, 1f));
        Img(title, "Pill", roundedRect, new Color(Navy.r, Navy.g, Navy.b, 0.75f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Simple);
        Txt(title, "Label", bold, 13, Gold, Spaced("DECK"), true);

        var bowTick = Box(root.transform, "BowTick", new Vector2(0.5f, 1f), new Vector2(0f, -(Margin + TitleHeight + Gap)), new Vector2(60f, BowTickHeight), new Vector2(0.5f, 1f));
        Img(bowTick, "Arrow", triangle, Cream, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(10f, 10f), Image.Type.Simple);
        Txt(bowTick, "Label", regular, 10, Cream, "BOW", new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(60f, 12f), TextAnchor.LowerCenter);

        float hullWidth = RootWidth - Margin * 2f;
        float hullHeight = RootHeight - Margin - TitleHeight - Gap - BowTickHeight - Gap - Margin;
        var hullArea = Box(root.transform, "HullArea", new Vector2(0.5f, 0f), new Vector2(0f, Margin), new Vector2(hullWidth, hullHeight), new Vector2(0.5f, 0f));
        hullArea.gameObject.AddComponent<RectMask2D>(); // clips the rotating content to the panel's rectangle

        // Rotated every frame to put the camera's facing up (MiniMap.rotatingRoot). Outer/Fill, and everything
        // parented under Fill, ride along with it, clipped by HullArea's mask above.
        var hull = Box(hullArea, "Hull", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(hullWidth, hullHeight), new Vector2(0.5f, 0.5f));

        Img(hull, "Outer", hullOuter, Gold, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Simple);
        var fill = Img(hull, "Fill", hullInner, Navy, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Simple);
        var fillShadow = fill.gameObject.AddComponent<Shadow>();
        fillShadow.effectColor = ShadowColor;
        fillShadow.effectDistance = new Vector2(3f, -4f);
        fillShadow.useGraphicAlpha = false;

        // Room zones (pool order must match MiniMap.RoomIds).
        var roomParts = new RoomPart[RoomTints.Length];
        for (int i = 0; i < RoomTints.Length; i++)
            roomParts[i] = BuildRoomZone(fill.transform, RoomTints[i].id, RoomTints[i].tint, roundedRect);

        // Station glyphs (pool order must match the StationType enum: Boiler, Wheel, Doors, Wireless).
        var stationGlyphRoots = new RectTransform[4];
        stationGlyphRoots[0] = BuildGlyphBoiler(fill.transform, triangle);
        stationGlyphRoots[1] = BuildGlyphWheel(fill.transform, ring, circle);
        stationGlyphRoots[2] = BuildGlyphDoors(fill.transform, roundedRect);
        stationGlyphRoots[3] = BuildGlyphWireless(fill.transform, circle);

        // Point-anchored (not stretched) and a fixed small size: this sits ON the station glyph it highlights,
        // not stretched to fill the whole hull — that was the earlier "huge ring outside the hull" bug.
        var highlightRing = Img(fill.transform, "Highlight", ring, Gold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f), Image.Type.Simple);
        highlightRing.gameObject.SetActive(false);

        var bridgeRoot = BuildBridgeMarker(fill.transform, triangle);

        var crewParts = new CrewPart[CrewRoster.SeatCount];
        for (int i = 0; i < crewParts.Length; i++)
            crewParts[i] = BuildCrewIcon(fill.transform, "Crew_" + (i + 1), circle, ring, triangle);

        var iceParts = new IcePart[3];
        for (int i = 0; i < iceParts.Length; i++)
            iceParts[i] = BuildIceIcon(fill.transform, "IceWarning_" + (i + 1), circle, ring);

        var pingParts = new PingPart[6];
        for (int i = 0; i < pingParts.Length; i++)
            pingParts[i] = BuildPing(fill.transform, "Ping_" + (i + 1), ring);

        // Outside Hull (not Fill), so it sweeps on its own instead of spinning with the camera's yaw; still a
        // child of HullArea, so it's still clipped by the mask.
        var sweep = Img(hullArea, "Sweep", triangle, new Color(Cream.r, Cream.g, Cream.b, 0.14f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, hullHeight * 0.95f), Image.Type.Simple);
        sweep.rectTransform.pivot = new Vector2(0.5f, 0f);
        sweep.rectTransform.anchoredPosition = Vector2.zero;

        var mini = root.AddComponent<MiniMap>();
        WireMiniMap(mini, fadeGroup, fill.rectTransform, hull, highlightRing, bridgeRoot, roomParts, stationGlyphRoots, crewParts, iceParts, pingParts, sweep.rectTransform);

        return root;
    }

    static void WireMiniMap(MiniMap mini, CanvasGroup fadeGroup, RectTransform mapArea, RectTransform rotatingRoot, Image highlightRing, RectTransform bridgeRoot,
        RoomPart[] roomParts, RectTransform[] stationGlyphRoots, CrewPart[] crewParts, IcePart[] iceParts, PingPart[] pingParts, RectTransform sweepLine)
    {
        var so = new SerializedObject(mini);
        so.FindProperty("fadeGroup").objectReferenceValue = fadeGroup;
        so.FindProperty("mapArea").objectReferenceValue = mapArea;
        so.FindProperty("rotatingRoot").objectReferenceValue = rotatingRoot;
        so.FindProperty("highlightRing").objectReferenceValue = highlightRing;
        so.FindProperty("bridgeMarker").objectReferenceValue = bridgeRoot;
        so.FindProperty("sweepLine").objectReferenceValue = sweepLine;

        var roomProp = so.FindProperty("roomZones");
        roomProp.arraySize = roomParts.Length;
        var roomLabelProp = so.FindProperty("roomLabels");
        roomLabelProp.arraySize = roomParts.Length;
        for (int i = 0; i < roomParts.Length; i++)
        {
            roomProp.GetArrayElementAtIndex(i).objectReferenceValue = roomParts[i].root;
            roomLabelProp.GetArrayElementAtIndex(i).objectReferenceValue = roomParts[i].label;
        }

        var stationProp = so.FindProperty("stationGlyphs");
        stationProp.arraySize = stationGlyphRoots.Length;
        for (int i = 0; i < stationGlyphRoots.Length; i++)
            stationProp.GetArrayElementAtIndex(i).FindPropertyRelative("root").objectReferenceValue = stationGlyphRoots[i];

        var crewProp = so.FindProperty("crewIcons");
        crewProp.arraySize = crewParts.Length;
        for (int i = 0; i < crewParts.Length; i++)
        {
            var el = crewProp.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("root").objectReferenceValue = crewParts[i].root;
            el.FindPropertyRelative("dot").objectReferenceValue = crewParts[i].dot;
            el.FindPropertyRelative("arrow").objectReferenceValue = crewParts[i].arrow;
            el.FindPropertyRelative("ring").objectReferenceValue = crewParts[i].ring;
        }

        var iceProp = so.FindProperty("iceWarningIcons");
        iceProp.arraySize = iceParts.Length;
        for (int i = 0; i < iceParts.Length; i++)
        {
            var el = iceProp.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("root").objectReferenceValue = iceParts[i].root;
            el.FindPropertyRelative("core").objectReferenceValue = iceParts[i].core;
            el.FindPropertyRelative("glow").objectReferenceValue = iceParts[i].glow;
        }

        var pingProp = so.FindProperty("pings");
        pingProp.arraySize = pingParts.Length;
        for (int i = 0; i < pingParts.Length; i++)
        {
            var el = pingProp.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("root").objectReferenceValue = pingParts[i].root;
            el.FindPropertyRelative("ring").objectReferenceValue = pingParts[i].ring;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- Composite icons ----------

    static RoomPart BuildRoomZone(Transform parent, string roomId, Color tint, Sprite roundedRect)
    {
        var root = Box(parent, "Room_" + roomId, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f), new Vector2(0.5f, 0.5f));
        Img(root, "Tint", roundedRect, tint, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Simple);

        // Left-edge anchored (not centred/stretched) and sized to its own text, not the zone, so MiniMap.cs can
        // test real label rects against each other and hide whichever one would overlap a neighbour.
        var label = Txt(root, "Label", regular, 15, Cream, Spaced(roomId.ToUpperInvariant()),
            new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(80f, 20f), TextAnchor.MiddleLeft);
        var outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = DarkOutline;
        outline.effectDistance = new Vector2(1.3f, -1.3f);

        root.gameObject.SetActive(false);
        return new RoomPart { root = root, label = label.rectTransform };
    }

    static RectTransform BuildGlyphBoiler(Transform parent, Sprite triangle)
    {
        var root = Box(parent, "Glyph_Boiler", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f), new Vector2(0.5f, 0.5f));
        Img(root, "Flame", triangle, GlyphGold, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-5f, -3f), Image.Type.Simple);
        root.gameObject.SetActive(false);
        return root;
    }

    static RectTransform BuildGlyphWheel(Transform parent, Sprite ring, Sprite circle)
    {
        var root = Box(parent, "Glyph_Wheel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f), new Vector2(0.5f, 0.5f));
        Img(root, "Rim", ring, GlyphGold, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Simple);
        var spokeA = Img(root, "SpokeA", null, GlyphGold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(19f, 3f), Image.Type.Simple);
        spokeA.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        var spokeB = Img(root, "SpokeB", null, GlyphGold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(19f, 3f), Image.Type.Simple);
        spokeB.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Img(root, "Hub", circle, GlyphGold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7f, 7f), Image.Type.Simple);
        root.gameObject.SetActive(false);
        return root;
    }

    static RectTransform BuildGlyphDoors(Transform parent, Sprite roundedRect)
    {
        var root = Box(parent, "Glyph_Doors", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 22f), new Vector2(0.5f, 0.5f));
        Img(root, "Body", roundedRect, GlyphGold, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Simple);
        Img(root, "Seam", null, DarkOutline, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 18f), Image.Type.Simple);
        root.gameObject.SetActive(false);
        return root;
    }

    static RectTransform BuildGlyphWireless(Transform parent, Sprite circle)
    {
        var root = Box(parent, "Glyph_Wireless", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 22f), new Vector2(0.5f, 0.5f));
        Img(root, "Pole", null, GlyphGold, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8.5f), new Vector2(2.5f, 17f), Image.Type.Simple);
        Img(root, "Node", circle, GlyphGold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -3.5f), new Vector2(7f, 7f), Image.Type.Simple);
        root.gameObject.SetActive(false);
        return root;
    }

    static RectTransform BuildBridgeMarker(Transform parent, Sprite triangle)
    {
        var root = Box(parent, "Bridge", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 16f), new Vector2(0.5f, 0.5f));
        Img(root, "Pole", null, Cream, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 5f), new Vector2(1.5f, 10f), Image.Type.Simple);
        var flag = Img(root, "Flag", triangle, Gold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(3f, -1f), new Vector2(8f, 8f), Image.Type.Simple);
        flag.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
        var outline = flag.gameObject.AddComponent<Outline>();
        outline.effectColor = DarkOutline;
        outline.effectDistance = new Vector2(0.8f, -0.8f);
        root.gameObject.SetActive(false);
        return root;
    }

    static CrewPart BuildCrewIcon(Transform parent, string name, Sprite circle, Sprite ring, Sprite triangle)
    {
        var root = Box(parent, name, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f), new Vector2(0.5f, 0.5f));
        var ringImg = Img(root, "Ring", ring, new Color(Cream.r, Cream.g, Cream.b, 0.9f), Vector2.zero, Vector2.one, Vector2.zero, new Vector2(6f, 6f), Image.Type.Simple);
        ringImg.gameObject.SetActive(false);
        var dot = Img(root, "Dot", circle, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Simple);
        var outline = dot.gameObject.AddComponent<Outline>();
        outline.effectColor = DarkOutline;
        outline.effectDistance = new Vector2(1f, -1f);
        var arrow = Box(root, "Arrow", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7f, 9f), new Vector2(0.5f, 0.2f));
        Img(arrow, "Tip", triangle, Cream, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Simple);
        root.gameObject.SetActive(false);
        return new CrewPart { root = root, dot = dot, arrow = arrow, ring = ringImg };
    }

    static IcePart BuildIceIcon(Transform parent, string name, Sprite circle, Sprite ring)
    {
        var root = Box(parent, name, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f), new Vector2(0.5f, 0.5f));
        var glow = Img(root, "Glow", ring, IceWarnColor, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(8f, 8f), Image.Type.Simple);
        var core = Img(root, "Core", circle, IceWarnColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Simple);
        var outline = core.gameObject.AddComponent<Outline>();
        outline.effectColor = DarkOutline;
        outline.effectDistance = new Vector2(1f, -1f);
        root.gameObject.SetActive(false);
        return new IcePart { root = root, core = core, glow = glow };
    }

    /// <summary>A ripple: just a ring that MiniMap.cs scales up and fades out at a world position.</summary>
    static PingPart BuildPing(Transform parent, string name, Sprite ring)
    {
        var root = Box(parent, name, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f), new Vector2(0.5f, 0.5f));
        var ringImg = Img(root, "Ring", ring, Gold, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Image.Type.Simple);
        root.gameObject.SetActive(false);
        return new PingPart { root = root, ring = ringImg };
    }

    // ---------- Small layout helpers (own copies — HudBuilder's Box/Img/Txt are private to her class) ----------

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
        img.type = type;
        img.raycastTarget = false;
        return img;
    }

    static Text Txt(Transform parent, string name, Font font, int size, Color color, string text, bool stretch) =>
        Txt(parent, name, font, size, color, text, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, stretch);

    static Text Txt(Transform parent, string name, Font font, int size, Color color, string text, Vector2 anchor, Vector2 pos, Vector2 boxSize, TextAnchor align, bool stretch = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        if (stretch)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
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
        shadow.effectDistance = new Vector2(1.2f, -1.5f);
        return t;
    }

    static string Spaced(string s) => string.Join(" ", s.ToCharArray());

    // ---------- Procedural sprite generation ----------

    static Sprite MakeCircle(string name, int size) => BuildSprite(name, size, size, (x, y) =>
    {
        float cx = size * 0.5f, cy = size * 0.5f, r = size * 0.46f;
        float dx = x - cx, dy = y - cy;
        return dx * dx + dy * dy <= r * r;
    });

    static Sprite MakeRing(string name, int size, float thicknessFrac) => BuildSprite(name, size, size, (x, y) =>
    {
        float cx = size * 0.5f, cy = size * 0.5f, rOuter = size * 0.46f, rInner = rOuter * (1f - thicknessFrac);
        float d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
        return d2 <= rOuter * rOuter && d2 >= rInner * rInner;
    });

    static Sprite MakeTriangle(string name, int size) => BuildSprite(name, size, size, (x, y) =>
    {
        var apex = new Vector2(size * 0.5f, size * 0.92f);
        var baseL = new Vector2(size * 0.12f, size * 0.1f);
        var baseR = new Vector2(size * 0.88f, size * 0.1f);
        return PointInTriangle(new Vector2(x, y), apex, baseL, baseR);
    });

    static Sprite MakeRoundedRect(string name, int w, int h, float cornerFrac) => BuildSprite(name, w, h, (x, y) =>
    {
        float cx = w * 0.5f, cy = h * 0.5f;
        float hw = w * 0.5f, hh = h * 0.5f;
        float rr = Mathf.Min(hw, hh) * cornerFrac;
        float dx = Mathf.Abs(x - cx) - (hw - rr);
        float dy = Mathf.Abs(y - cy) - (hh - rr);
        if (dx <= 0f || dy <= 0f) return Mathf.Abs(x - cx) <= hw && Mathf.Abs(y - cy) <= hh;
        return dx * dx + dy * dy <= rr * rr;
    });

    const float HullHalfWidthFrac = 0.3f, HullBowTaperFrac = 0.36f;

    static Sprite MakeHull(string name, int w, int h, float shrink) => BuildSprite(name, w, h, (x, y) => HullContains(x, y, w, h, shrink));

    /// <summary>Pointed bow at the top (+Z, high y), rounded stern at the bottom (low y). Pixel-space, so a
    /// non-square texture never distorts the roundness of the stern cap.</summary>
    static bool HullContains(float x, float y, int w, int h, float shrink)
    {
        float cx = w * 0.5f;
        float hw = Mathf.Max(0f, w * HullHalfWidthFrac - shrink);
        float capR = hw;
        float bowTaperH = Mathf.Max(1f, h * HullBowTaperFrac - shrink * 1.6f);
        float bowStartY = h - bowTaperH;

        if (y <= capR)
        {
            float dx = x - cx, dy = y - capR;
            return dx * dx + dy * dy <= capR * capR;
        }
        if (y < bowStartY) return Mathf.Abs(x - cx) <= hw;

        float t = Mathf.InverseLerp(bowStartY, h, y);
        float hwAtY = hw * (1f - t);
        return Mathf.Abs(x - cx) <= hwAtY;
    }

    static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
        bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNeg && hasPos);
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    /// <summary>Renders a boolean shape test into a white-with-alpha mask PNG, 4x4 supersampled for antialiasing,
    /// and saves it as a real Sprite asset (procedurally generated, not hand-drawn, not Samprity's).</summary>
    static Sprite BuildSprite(string name, int w, int h, System.Func<float, float, bool> containsPx)
    {
        MovementPrototypeBuilder.EnsureFolder(SpriteFolder);
        string path = $"{SpriteFolder}/{name}.png";

        const int SS = 4;
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < SS; sy++)
                    for (int sx = 0; sx < SS; sx++)
                    {
                        float px = x + (sx + 0.5f) / SS;
                        float py = y + (sy + 0.5f) / SS;
                        if (containsPx(px, py)) hits++;
                    }
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)(255 * hits / (SS * SS)));
            }
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear;
        ti.spritePixelsPerUnit = 100f;
        EditorUtility.SetDirty(ti);
        ti.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
