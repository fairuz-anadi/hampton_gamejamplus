using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// A top-down deck map drawn as UI icons on Samprity's existing HUD canvas (no second camera). Shows crew by seat
/// colour, stations as neutral squares, waiting ice warnings and the bridge marker. Owner: Rabib.
/// Spawns itself from Resources/MiniMap in gameplay scenes, like HUD, so nobody has to edit Ship.unity.
/// Built from Rabib &gt; Build MiniMap (MiniMapBuilder.cs), not hand-edited.
/// </summary>
public class MiniMap : MonoBehaviour
{
    [SerializeField] bool showBots = false;

    [SerializeField] GameObject panel;
    [SerializeField] RectTransform mapArea;
    [SerializeField] Image bridgeIcon;
    [SerializeField] Image[] stationIcons = new Image[4];
    [SerializeField] Image highlightRing;
    [SerializeField] Image[] iceWarningIcons = new Image[3];
    [SerializeField] Image[] crewIcons = new Image[CrewRoster.SeatCount];

    static readonly StationType[] StationTypes = (StationType[])System.Enum.GetValues(typeof(StationType));

    float minX, maxX, minZ, maxZ;
    bool boundsReady;
    bool hiddenForState;
    Vector3 bridgeWorldPos;
    bool hasBridge;
    bool loggedHiddenReason;

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
        FindDeckBounds();
        FindBridgeMarker();
        // Deferred to Start(), not Awake() or OnSceneLoaded: every object's Awake() (including HUD's, which sets
        // HUD.Instance) finishes before any Start() runs that frame, regardless of sceneLoaded subscriber order.
        bool parented = HUD.Instance != null;
        if (parented) transform.SetParent(HUD.Instance.transform, false);
        else Debug.LogWarning("[MiniMap] HUD.Instance is null in Start() — staying unparented, off any Canvas, so nothing will render.");
        GameEvents.StateChanged += OnStateChanged;
        hiddenForState = GameManager.Instance != null && GameManager.Instance.State == GameState.Iceberg;
        Debug.Log($"[MiniMap] Start: boundsReady={boundsReady} hasBridge={hasBridge} parentedToHud={parented} hiddenForState={hiddenForState}");
    }

    void OnDestroy() => GameEvents.StateChanged -= OnStateChanged;

    void OnStateChanged(GameState s) => hiddenForState = s == GameState.Iceberg;

    void FindDeckBounds()
    {
        var col = GameObject.Find("DeckCollider")?.GetComponent<BoxCollider>();
        boundsReady = col != null;
        if (!boundsReady) { if (panel != null) panel.SetActive(false); return; }
        var b = col.bounds;
        minX = b.min.x; maxX = b.max.x; minZ = b.min.z; maxZ = b.max.z;
    }

    /// <summary>Anadi's deck marks the real drop point as DropPoint_Bridge; the sandbox has none, so this falls
    /// back to the Wheel station there instead (same fallback IceWarningCarry uses). Static, so placed once.</summary>
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
        if (bridgeIcon == null) return;
        bridgeIcon.gameObject.SetActive(hasBridge);
        if (hasBridge) bridgeIcon.rectTransform.anchoredPosition = ToLocal(bridgeWorldPos);
    }

    Vector2 ToLocal(Vector3 world)
    {
        float u = Mathf.InverseLerp(minX, maxX, world.x);
        float v = Mathf.InverseLerp(minZ, maxZ, world.z);
        var size = mapArea.rect.size;
        const float pad = 12f;
        return new Vector2(Mathf.Lerp(pad, size.x - pad, u), Mathf.Lerp(pad, size.y - pad, v));
    }

    void LateUpdate()
    {
        bool visible = boundsReady && !hiddenForState;
        if (panel.activeSelf != visible) panel.SetActive(visible);
        if (!visible)
        {
            if (!loggedHiddenReason)
            {
                loggedHiddenReason = true;
                Debug.Log($"[MiniMap] Hidden in LateUpdate: boundsReady={boundsReady} hiddenForState={hiddenForState}");
            }
            return;
        }
        loggedHiddenReason = false;

        PlaceStationIcons();
        PlaceCrewIcons();
        PlaceIceWarningIcons();
    }

    /// <summary>Neutral squares always, regardless of IsDone/IsBroken; only the active order's station lights up,
    /// and only in solo play (no single "local human" exists once 2+ humans share the screen).</summary>
    void PlaceStationIcons()
    {
        var human = SoloHuman();
        var order = human != null && OrderDirector.Instance != null ? OrderDirector.Instance.GetOrderFor(human) : null;
        StationType? highlighted = order?.Data.station;

        for (int i = 0; i < StationTypes.Length && i < stationIcons.Length; i++)
        {
            var st = StationRegistry.Get(StationTypes[i]);
            var icon = stationIcons[i];
            if (icon == null) continue;
            icon.gameObject.SetActive(st != null);
            if (st != null) icon.rectTransform.anchoredPosition = ToLocal(st.Position);
        }

        var highlightStation = highlighted.HasValue ? StationRegistry.Get(highlighted.Value) : null;
        if (highlightRing == null) return;
        highlightRing.gameObject.SetActive(highlightStation != null);
        if (highlightStation != null) highlightRing.rectTransform.anchoredPosition = ToLocal(highlightStation.Position);
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

    void PlaceCrewIcons()
    {
        var crew = CrewRegistry.All;
        int used = 0;
        for (int i = 0; i < crew.Count && used < crewIcons.Length; i++)
        {
            var c = crew[i];
            if (c.IsBot && !showBots) continue;
            var icon = crewIcons[used++];
            if (icon == null) continue;
            icon.gameObject.SetActive(true);
            icon.color = CrewRoster.SeatColors[Mathf.Clamp(c.Seat, 0, CrewRoster.SeatColors.Length - 1)];
            icon.rectTransform.anchoredPosition = ToLocal(c.Position);
        }
        for (int i = used; i < crewIcons.Length; i++)
            if (crewIcons[i] != null) crewIcons[i].gameObject.SetActive(false);
    }

    void PlaceIceWarningIcons()
    {
        var warnings = IceWarningCarry.All;
        int used = 0;
        for (int i = 0; i < warnings.Count && used < iceWarningIcons.Length; i++)
        {
            var w = warnings[i];
            if (w == null || !w.IsWaiting) continue;
            var icon = iceWarningIcons[used++];
            if (icon == null) continue;
            icon.gameObject.SetActive(true);
            icon.rectTransform.anchoredPosition = ToLocal(w.Position);
        }
        for (int i = used; i < iceWarningIcons.Length; i++)
            if (iceWarningIcons[i] != null) iceWarningIcons[i].gameObject.SetActive(false);
    }
}
