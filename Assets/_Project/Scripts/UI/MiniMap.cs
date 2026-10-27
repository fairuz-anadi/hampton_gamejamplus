using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// A top-down deck map drawn as UI icons on Samprity's existing HUD canvas (no second camera). Owner: Rabib.
/// Spawns itself from Resources/MiniMap in gameplay scenes, like HUD, so nobody has to edit Ship.unity.
/// Built from Rabib &gt; Build MiniMap (MiniMapBuilder.cs), not hand-edited.
/// Static things (deck bounds, bridge marker, room zones, station glyph positions) are placed once in Start();
/// only crew, ice warnings and the highlight ring move, so those are the only things LateUpdate ever touches.
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

    [SerializeField] CanvasGroup fadeGroup;
    [SerializeField] RectTransform mapArea;
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

        FindDeckBounds();
        if (boundsReady)
        {
            FindBridgeMarker();
            PlaceRoomZones();
            PlaceStationGlyphs();
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
    /// back to the Wheel station there instead (same fallback IceWarningCarry uses). Static, placed once.</summary>
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
        if (bridgeMarker == null) return;
        bridgeMarker.gameObject.SetActive(hasBridge);
        if (hasBridge) bridgeMarker.anchoredPosition = ToLocal(bridgeWorldPos);
    }

    Vector2 ToLocal(Vector3 world, float pad = 12f)
    {
        float u = Mathf.InverseLerp(minX, maxX, world.x);
        float v = Mathf.InverseLerp(minZ, maxZ, world.z);
        var size = mapArea.rect.size;
        return new Vector2(Mathf.Lerp(pad, size.x - pad, u), Mathf.Lerp(pad, size.y - pad, v));
    }

    /// <summary>Room footprints never move, so this runs once from Start(), not every frame. A label that would
    /// overlap one already placed stays hidden — its zone tint still shows, just without the unreadable text.</summary>
    void PlaceRoomZones()
    {
        if (roomZones == null) return;
        var placedLabels = new List<Rect>(RoomIds.Length);

        for (int i = 0; i < RoomIds.Length && i < roomZones.Length; i++)
        {
            var zone = roomZones[i];
            var label = roomLabels != null && i < roomLabels.Length ? roomLabels[i] : null;
            if (zone == null) continue;

            RoomVolume found = null;
            for (int j = 0; j < RoomVolume.All.Count; j++)
                if (RoomVolume.All[j] != null && RoomVolume.All[j].roomId == RoomIds[i]) { found = RoomVolume.All[j]; break; }

            var col = found != null ? found.GetComponent<BoxCollider>() : null;
            if (col == null)
            {
                zone.gameObject.SetActive(false);
                if (label != null) label.gameObject.SetActive(false);
                continue;
            }

            var b = col.bounds;
            Vector2 c0 = ToLocal(new Vector3(b.min.x, 0f, b.min.z), 0f);
            Vector2 c1 = ToLocal(new Vector3(b.max.x, 0f, b.max.z), 0f);
            zone.gameObject.SetActive(true);
            zone.anchoredPosition = (c0 + c1) * 0.5f;
            zone.sizeDelta = new Vector2(Mathf.Abs(c1.x - c0.x), Mathf.Abs(c1.y - c0.y));

            if (label == null) continue;
            label.gameObject.SetActive(true);
            Rect labelRect = LabelRect(zone, label);
            bool overlaps = false;
            for (int j = 0; j < placedLabels.Count; j++)
                if (labelRect.Overlaps(placedLabels[j])) { overlaps = true; break; }
            if (overlaps) label.gameObject.SetActive(false);
            else placedLabels.Add(labelRect);
        }
    }

    /// <summary>The label's AABB in mapArea-local space. Both `zone` and `label` are point-anchored (never
    /// stretched) by MiniMapBuilder, so this reads their real anchor/pivot/size instead of assuming numbers that
    /// could silently drift out of sync with the builder.</summary>
    static Rect LabelRect(RectTransform zone, RectTransform label)
    {
        Vector2 anchorInZone = new Vector2(
            (label.anchorMin.x - 0.5f) * zone.sizeDelta.x,
            (label.anchorMin.y - 0.5f) * zone.sizeDelta.y);
        Vector2 centreInZone = anchorInZone + label.anchoredPosition + new Vector2(
            label.sizeDelta.x * (0.5f - label.pivot.x),
            label.sizeDelta.y * (0.5f - label.pivot.y));
        Vector2 centre = zone.anchoredPosition + centreInZone;
        return new Rect(centre - label.sizeDelta * 0.5f, label.sizeDelta);
    }

    /// <summary>Stations don't move either, so their glyphs are positioned once. Only the highlight ring is dynamic.</summary>
    void PlaceStationGlyphs()
    {
        if (stationGlyphs == null) return;
        for (int i = 0; i < StationTypes.Length && i < stationGlyphs.Length; i++)
        {
            var glyph = stationGlyphs[i];
            if (glyph == null || glyph.root == null) continue;
            var st = StationRegistry.Get(StationTypes[i]);
            glyph.root.gameObject.SetActive(st != null);
            if (st != null) glyph.root.anchoredPosition = ToLocal(st.Position);
        }
    }

    void LateUpdate()
    {
        bool hidden = !boundsReady || hiddenForState;
        if (fadeGroup != null)
            fadeGroup.alpha = Mathf.MoveTowards(fadeGroup.alpha, hidden ? 0f : 1f, Time.unscaledDeltaTime / fadeSeconds);
        if (!boundsReady) return;

        var solo = SoloHuman();
        UpdateHighlightRing(solo);
        PlaceCrewIcons(solo);
        PlaceIceWarningIcons();
    }

    /// <summary>The one human playing, only when nobody else is — matches GameplayCameraRig's own solo/overview split.</summary>
    static ICrewActor SoloHuman()
    {
        var crew = CrewRegistry.All;
        ICrewActor found = null;
        int humans = 0;
        for (int i = 0; i < crew.Count; i++)
        {
            if (crew[i].IsBot) continue;
            humans++;
            found = crew[i];
            if (humans > 1) return null;
        }
        return humans == 1 ? found : null;
    }

    void UpdateHighlightRing(ICrewActor solo)
    {
        if (highlightRing == null) return;
        var order = solo != null && OrderDirector.Instance != null ? OrderDirector.Instance.GetOrderFor(solo) : null;
        var station = order != null ? StationRegistry.Get(order.Data.station) : null;

        bool show = station != null;
        highlightRing.gameObject.SetActive(show);
        if (!show) return;

        highlightRing.rectTransform.anchoredPosition = ToLocal(station.Position);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * HighlightPulseCycles * Mathf.PI * 2f);
        highlightRing.rectTransform.localScale = Vector3.one * (1f + HighlightPulseGrow * pulse);
        var c = HighlightColor; c.a = Mathf.Lerp(0.55f, 1f, pulse);
        highlightRing.color = c;
    }

    void PlaceCrewIcons(ICrewActor solo)
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
            icon.root.anchoredPosition = ToLocal(c.Position);
            if (icon.dot != null) icon.dot.color = CrewRoster.SeatColors[Mathf.Clamp(c.Seat, 0, CrewRoster.SeatColors.Length - 1)];

            if (icon.arrow != null)
            {
                var actorTransform = (c as Component)?.transform;
                Vector3 fwd = actorTransform != null ? actorTransform.forward : Vector3.forward;
                float angle = Mathf.Atan2(-fwd.x, fwd.z) * Mathf.Rad2Deg;
                icon.arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            bool isSolo = solo != null && c == solo;
            if (icon.dot != null) icon.dot.rectTransform.localScale = Vector3.one * (isSolo ? 1.3f : 1f);
            if (icon.ring != null) icon.ring.gameObject.SetActive(isSolo);
        }
        for (int i = used; i < crewIcons.Length; i++)
            if (crewIcons[i] != null && crewIcons[i].root != null) crewIcons[i].root.gameObject.SetActive(false);
    }

    void PlaceIceWarningIcons()
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

            if (present) icon.root.anchoredPosition = ToLocal(warnings[i].Position);
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
