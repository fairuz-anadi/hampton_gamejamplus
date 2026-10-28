using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// A rotating top-down radar drawn as UI icons on Samprity's existing HUD canvas (no second camera). Owner: Rabib.
/// Spawns itself from Resources/MiniMap in gameplay scenes, like HUD, so nobody has to edit Ship.unity.
/// Built from Rabib &gt; Build MiniMap (MiniMapBuilder.cs), not hand-edited.
///
/// Centred on the followed human (lowest seat among humans; the deck's bounds centre when there's none) and, with
/// rotateWithCamera on, spun so the gameplay camera's facing is always up (rotatingRoot does the spinning; the
/// followed human's own position is always the rotation's pivot, so their dot never leaves the panel centre).
/// Everyone/everything else is positioned relative to that same anchor every frame, scaled by viewRadiusMetres, and
/// clipped to the panel by the RectMask2D MiniMapBuilder puts on HullArea. Room labels, the title strip and the BOW
/// tick stay upright: the title/BOW are built outside the rotating hierarchy entirely, and each room label carries
/// its own counter-rotation.
/// Discovery (which rooms/stations exist, their world-space geometry) happens once in Start(); their panel
/// position/rotation is recomputed every frame since the followed human moves. No per-frame allocations.
/// </summary>
public class MiniMap : MonoBehaviour
{
    [System.Serializable]
    class StationGlyph { public RectTransform root; }

    [System.Serializable]
    class CrewIcon { public RectTransform root; public Image dot; public RectTransform arrow; public Image ring; }

    [System.Serializable]
    class IceIcon { public RectTransform root; public Image core; public Image glow; }

    [SerializeField] bool showBots = false;
    [SerializeField] float fadeSeconds = 0.3f;
    [Tooltip("Spin the map so the gameplay camera's facing is always up. Off: frozen north-up, still centred on the followed human.")]
    [SerializeField] bool rotateWithCamera = true;
    [Tooltip("Metres from the followed human to the edge of the panel's shorter side.")]
    [SerializeField] float viewRadiusMetres = 14f;

    [SerializeField] CanvasGroup fadeGroup;
    [SerializeField] RectTransform mapArea;
    [Tooltip("Rotated every frame to put the camera's facing up. Outer/Fill and everything parented under Fill ride along with it.")]
    [SerializeField] RectTransform rotatingRoot;
    [SerializeField] RectTransform[] roomZones;
    [SerializeField] RectTransform[] roomLabels;
    [SerializeField] StationGlyph[] stationGlyphs;
    [SerializeField] Image highlightRing;
    [SerializeField] RectTransform bridgeMarker;
    [SerializeField] CrewIcon[] crewIcons;
    [SerializeField] IceIcon[] iceWarningIcons;

    static readonly StationType[] StationTypes = (StationType[])System.Enum.GetValues(typeof(StationType));
    static readonly string[] RoomIds = { "Boiler", "Corridor", "Wireless", "Bridge", "Bow" };
    static readonly Color HighlightColor = new Color(0.95f, 0.78f, 0.42f, 1f);

    const float HighlightPulseCycles = 0.9f, HighlightPulseGrow = 0.18f;
    const float IceWarningPulseCycles = 1.4f;

    float minX, maxX, minZ, maxZ;
    bool boundsReady;
    bool hiddenForState;
    Vector3 bridgeWorldPos;
    bool hasBridge;
    float[] iceFade;

    // Cached once in Start() from the same discovery the old static placement used; only panel position/rotation
    // moves after that, every frame, since the followed human (the anchor) moves.
    Vector3[] roomWorldMin, roomWorldMax;   // per RoomIds slot, valid only where roomZones[i] is active
    Vector3[] stationWorldPos;              // per StationTypes slot, valid only where stationGlyphs[i] is active
    readonly Rect[] labelRects = new Rect[5]; // reused every frame for the overlap test; RoomIds.Length, fixed size, never reallocated

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || FindAnyObjectByType<MiniMap>() != null || !GameSystems.IsGameplayScene(scene.name)) return;
        var prefab = Resources.Load<GameObject>("MiniMap");
        if (prefab == null)
        {
            Debug.LogWarning("[MiniMap] Hidden: Resources.Load(\"MiniMap\") found nothing. Run Rabib > Build MiniMap first.");
            return;
        }
        Instantiate(prefab).name = "MiniMap";
    }

    void Start()
    {
        iceFade = new float[iceWarningIcons != null ? iceWarningIcons.Length : 0];
        roomWorldMin = new Vector3[RoomIds.Length];
        roomWorldMax = new Vector3[RoomIds.Length];
        stationWorldPos = new Vector3[StationTypes.Length];

        FindDeckBounds();
        if (boundsReady)
        {
            FindBridgeMarker();
            FindRoomZones();
            FindStationGlyphs();
        }

        // Deferred to Start(), not Awake() or OnSceneLoaded: every object's Awake() (including HUD's, which sets
        // HUD.Instance) finishes before any Start() runs that frame, regardless of sceneLoaded subscriber order.
        bool parented = HUD.Instance != null;
        if (parented) transform.SetParent(HUD.Instance.transform, false);
        else Debug.LogWarning("[MiniMap] HUD.Instance is null in Start() — staying unparented, off any Canvas, so nothing will render.");

        GameEvents.StateChanged += OnStateChanged;
        hiddenForState = GameManager.Instance != null && GameManager.Instance.State == GameState.Iceberg;
        if (fadeGroup != null) fadeGroup.alpha = 0f;

        Debug.Log($"[MiniMap] Start: boundsReady={boundsReady} hasBridge={hasBridge} parentedToHud={parented} hiddenForState={hiddenForState}");
    }

    void OnDestroy() => GameEvents.StateChanged -= OnStateChanged;

    void OnStateChanged(GameState s) => hiddenForState = s == GameState.Iceberg;

    void FindDeckBounds()
    {
        var col = GameObject.Find("DeckCollider")?.GetComponent<BoxCollider>();
        boundsReady = col != null;
        if (!boundsReady) return;
        var b = col.bounds;
        minX = b.min.x; maxX = b.max.x; minZ = b.min.z; maxZ = b.max.z;
    }

    /// <summary>Anadi's deck marks the real drop point as DropPoint_Bridge; the sandbox has none, so this falls
    /// back to the Wheel station there instead (same fallback IceWarningCarry uses). Discovery only: panel
    /// position is set every frame in PositionBridgeMarker, since it now moves with the followed human.</summary>
    void FindBridgeMarker()
    {
        var marker = GameObject.Find("DropPoint_Bridge");
        if (marker != null) { bridgeWorldPos = marker.transform.position; hasBridge = true; }
        else
        {
            var wheel = StationRegistry.Get(StationType.Wheel);
            hasBridge = wheel != null;
            if (hasBridge) bridgeWorldPos = wheel.Position;
        }
        if (bridgeMarker != null) bridgeMarker.gameObject.SetActive(hasBridge);
    }

    /// <summary>Which rooms exist and their world-space AABB. Room footprints never move, so this is discovery
    /// only; PositionRoomZones recomputes their panel position (and the label overlap test) every frame.</summary>
    void FindRoomZones()
    {
        if (roomZones == null) return;
        for (int i = 0; i < RoomIds.Length && i < roomZones.Length; i++)
        {
            var zone = roomZones[i];
            var label = roomLabels != null && i < roomLabels.Length ? roomLabels[i] : null;
            if (zone == null) continue;

            RoomVolume found = null;
            for (int j = 0; j < RoomVolume.All.Count; j++)
                if (RoomVolume.All[j] != null && RoomVolume.All[j].roomId == RoomIds[i]) { found = RoomVolume.All[j]; break; }

            var col = found != null ? found.GetComponent<BoxCollider>() : null;
            bool exists = col != null;
            zone.gameObject.SetActive(exists);
            if (label != null) label.gameObject.SetActive(exists);
            if (!exists) continue;

            var b = col.bounds;
            roomWorldMin[i] = b.min;
            roomWorldMax[i] = b.max;
        }
    }

    /// <summary>Which stations exist and their world position. Stations don't move, so this is discovery only;
    /// PositionStationGlyphs recomputes their panel position every frame.</summary>
    void FindStationGlyphs()
    {
        if (stationGlyphs == null) return;
        for (int i = 0; i < StationTypes.Length && i < stationGlyphs.Length; i++)
        {
            var glyph = stationGlyphs[i];
            if (glyph == null || glyph.root == null) continue;
            var st = StationRegistry.Get(StationTypes[i]);
            glyph.root.gameObject.SetActive(st != null);
            if (st != null) stationWorldPos[i] = st.Position;
        }
    }

    void LateUpdate()
    {
        bool hidden = !boundsReady || hiddenForState;
        if (fadeGroup != null)
            fadeGroup.alpha = Mathf.MoveTowards(fadeGroup.alpha, hidden ? 0f : 1f, Time.unscaledDeltaTime / fadeSeconds);
        if (!boundsReady) return;

        var followed = FollowedHuman();
        // No human in the scene (bot-only sandbox): centre on the deck bounds instead, rotation frozen.
        Vector3 anchor = followed != null ? followed.Position : new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        float yaw = rotateWithCamera && followed != null ? CameraYaw() : 0f;
        if (rotatingRoot != null) rotatingRoot.localRotation = Quaternion.Euler(0f, 0f, yaw);
        float pxPerMetre = Mathf.Min(mapArea.rect.width, mapArea.rect.height) * 0.5f / Mathf.Max(0.01f, viewRadiusMetres);

        PositionRoomZones(anchor, pxPerMetre, yaw);
        PositionStationGlyphs(anchor, pxPerMetre);
        PositionBridgeMarker(anchor, pxPerMetre);
        UpdateHighlightRing(followed, anchor, pxPerMetre);
        PlaceCrewIcons(followed, anchor, pxPerMetre);
        PlaceIceWarningIcons(anchor, pxPerMetre);
    }

    /// <summary>The human the minimap follows: lowest seat among humans, whatever the player count (not just solo).</summary>
    static ICrewActor FollowedHuman()
    {
        var crew = CrewRegistry.All; // sorted by seat
        for (int i = 0; i < crew.Count; i++)
            if (!crew[i].IsBot) return crew[i];
        return null;
    }

    /// <summary>The gameplay camera's facing, in degrees. Prefers the live AutoFollowCamera when it's actually
    /// driving the camera; falls back to the camera's own transform otherwise (shared-screen overview, etc.).</summary>
    static float CameraYaw()
    {
        var cam = Camera.main;
        if (cam == null) return 0f;
        return cam.TryGetComponent(out AutoFollowCamera follow) && follow.enabled ? follow.Yaw : cam.transform.eulerAngles.y;
    }

    /// <summary>World position to a panel-local offset from the anchor (the followed human, or the deck centre),
    /// in rotatingRoot's local (pre-rotation) space — rotatingRoot.localRotation does the actual spinning.</summary>
    static Vector2 ToPanel(Vector3 world, Vector3 anchor, float pxPerMetre) =>
        new Vector2(world.x - anchor.x, world.z - anchor.z) * pxPerMetre;

    /// <summary>Room tint boxes rotate with the map (no counter-rotation); their labels counter-rotate to stay
    /// upright. Because the label's own orientation stays level while its position orbits with the map, which
    /// labels overlap genuinely changes as yaw changes, so this re-tests every frame — using the fixed-size
    /// labelRects field, never a per-frame allocation.</summary>
    void PositionRoomZones(Vector3 anchor, float pxPerMetre, float yaw)
    {
        if (roomZones == null) return;
        for (int i = 0; i < RoomIds.Length && i < roomZones.Length; i++)
        {
            var zone = roomZones[i];
            if (zone == null || !zone.gameObject.activeSelf) continue;

            Vector2 c0 = ToPanel(roomWorldMin[i], anchor, pxPerMetre);
            Vector2 c1 = ToPanel(roomWorldMax[i], anchor, pxPerMetre);
            zone.anchoredPosition = (c0 + c1) * 0.5f;
            zone.sizeDelta = new Vector2(Mathf.Abs(c1.x - c0.x), Mathf.Abs(c1.y - c0.y));

            var label = roomLabels != null && i < roomLabels.Length ? roomLabels[i] : null;
            if (label == null) continue;
            label.localRotation = Quaternion.Euler(0f, 0f, -yaw);

            labelRects[i] = LabelRect(zone, label, yaw);
            bool overlaps = false;
            for (int j = 0; j < i; j++)
            {
                var otherZone = j < roomZones.Length ? roomZones[j] : null;
                var otherLabel = roomLabels != null && j < roomLabels.Length ? roomLabels[j] : null;
                if (otherZone == null || !otherZone.gameObject.activeSelf || otherLabel == null || !otherLabel.gameObject.activeSelf) continue;
                if (labelRects[i].Overlaps(labelRects[j])) { overlaps = true; break; }
            }
            label.gameObject.SetActive(!overlaps);
        }
    }

    /// <summary>The label's AABB in rotatingRoot-local (screen-equivalent) space: its position is rotated by the
    /// current yaw (it orbits with the map), but its box stays the label's own upright size, matching how it
    /// actually looks on screen once its own counter-rotation cancels the map's spin.</summary>
    static Rect LabelRect(RectTransform zone, RectTransform label, float yaw)
    {
        Vector2 anchorInZone = new Vector2(
            (label.anchorMin.x - 0.5f) * zone.sizeDelta.x,
            (label.anchorMin.y - 0.5f) * zone.sizeDelta.y);
        Vector2 centreInZone = anchorInZone + label.anchoredPosition + new Vector2(
            label.sizeDelta.x * (0.5f - label.pivot.x),
            label.sizeDelta.y * (0.5f - label.pivot.y));
        Vector2 centre = zone.anchoredPosition + centreInZone;

        float rad = yaw * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        Vector2 rotated = new Vector2(centre.x * cos - centre.y * sin, centre.x * sin + centre.y * cos);
        return new Rect(rotated - label.sizeDelta * 0.5f, label.sizeDelta);
    }

    void PositionStationGlyphs(Vector3 anchor, float pxPerMetre)
    {
        if (stationGlyphs == null) return;
        for (int i = 0; i < StationTypes.Length && i < stationGlyphs.Length; i++)
        {
            var glyph = stationGlyphs[i];
            if (glyph == null || glyph.root == null || !glyph.root.gameObject.activeSelf) continue;
            glyph.root.anchoredPosition = ToPanel(stationWorldPos[i], anchor, pxPerMetre);
        }
    }

    void PositionBridgeMarker(Vector3 anchor, float pxPerMetre)
    {
        if (bridgeMarker == null || !hasBridge) return;
        bridgeMarker.anchoredPosition = ToPanel(bridgeWorldPos, anchor, pxPerMetre);
    }

    /// <summary>Highlights the followed human's own active order station — pickup before they've picked up a
    /// carry item, delivery after — reading only their own order and their own Interactor, never station state,
    /// breakage or bot data.</summary>
    void UpdateHighlightRing(ICrewActor followed, Vector3 anchor, float pxPerMetre)
    {
        if (highlightRing == null) return;
        var order = followed != null && OrderDirector.Instance != null ? OrderDirector.Instance.GetOrderFor(followed) : null;

        StationType? target = null;
        if (order != null)
        {
            if (!order.Data.isCarry) target = order.Data.station;
            else
            {
                var interactor = (followed as Component)?.GetComponent<Interactor>();
                target = interactor != null && interactor.IsCarrying ? order.Data.deliverTo : order.Data.station;
            }
        }
        var station = target.HasValue ? StationRegistry.Get(target.Value) : null;

        bool show = station != null;
        highlightRing.gameObject.SetActive(show);
        if (!show) return;

        highlightRing.rectTransform.anchoredPosition = ToPanel(station.Position, anchor, pxPerMetre);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * HighlightPulseCycles * Mathf.PI * 2f);
        highlightRing.rectTransform.localScale = Vector3.one * (1f + HighlightPulseGrow * pulse);
        var c = HighlightColor; c.a = Mathf.Lerp(0.55f, 1f, pulse);
        highlightRing.color = c;
    }

    void PlaceCrewIcons(ICrewActor followed, Vector3 anchor, float pxPerMetre)
    {
        if (crewIcons == null) return;
        var crew = CrewRegistry.All;
        int used = 0;
        for (int i = 0; i < crew.Count && used < crewIcons.Length; i++)
        {
            var c = crew[i];
            if (c.IsBot && !showBots) continue;
            var icon = crewIcons[used++];
            if (icon == null || icon.root == null) continue;

            icon.root.gameObject.SetActive(true);
            icon.root.anchoredPosition = ToPanel(c.Position, anchor, pxPerMetre);
            if (icon.dot != null) icon.dot.color = CrewRoster.SeatColors[Mathf.Clamp(c.Seat, 0, CrewRoster.SeatColors.Length - 1)];

            if (icon.arrow != null)
            {
                var actorTransform = (c as Component)?.transform;
                Vector3 fwd = actorTransform != null ? actorTransform.forward : Vector3.forward;
                float angle = Mathf.Atan2(-fwd.x, fwd.z) * Mathf.Rad2Deg;
                icon.arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            bool isFollowed = followed != null && c == followed;
            if (icon.dot != null) icon.dot.rectTransform.localScale = Vector3.one * (isFollowed ? 1.3f : 1f);
            if (icon.ring != null) icon.ring.gameObject.SetActive(isFollowed);
        }
        for (int i = used; i < crewIcons.Length; i++)
            if (crewIcons[i] != null && crewIcons[i].root != null) crewIcons[i].root.gameObject.SetActive(false);
    }

    void PlaceIceWarningIcons(Vector3 anchor, float pxPerMetre)
    {
        if (iceWarningIcons == null) return;
        var warnings = IceWarningCarry.All;
        for (int i = 0; i < iceWarningIcons.Length; i++)
        {
            var icon = iceWarningIcons[i];
            if (icon == null || icon.root == null) continue;

            bool present = i < warnings.Count && warnings[i] != null && warnings[i].IsWaiting;
            float target = present ? 1f : 0f;
            iceFade[i] = Mathf.MoveTowards(iceFade[i], target, Time.unscaledDeltaTime / fadeSeconds);

            bool active = iceFade[i] > 0.01f;
            icon.root.gameObject.SetActive(active);
            if (!active) continue;

            if (present) icon.root.anchoredPosition = ToPanel(warnings[i].Position, anchor, pxPerMetre);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * IceWarningPulseCycles * Mathf.PI * 2f + i * 2.1f);
            SetAlpha(icon.core, iceFade[i]);
            SetAlpha(icon.glow, iceFade[i] * Mathf.Lerp(0.4f, 0.85f, pulse));
            if (icon.glow != null) icon.glow.rectTransform.localScale = Vector3.one * (1f + 0.2f * pulse);
        }
    }

    static void SetAlpha(Image img, float a)
    {
        if (img == null) return;
        var c = img.color; c.a = a; img.color = c;
    }
}
