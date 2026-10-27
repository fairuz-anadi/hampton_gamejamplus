using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ship's camera and crew presentation, added at runtime so Ship.unity stays untouched. Owner: Samprity.
///   One human player: the third-person follow camera behind them (AutoFollowCamera), W/A/S/D relative to it.
///   Two or three humans on one screen: the scene's fixed overview camera, so everyone stays in view.
///   Everyone (players and bots) wears their seat's crew model instead of a capsule (CrewVisual).
///   Coming from Begin Voyage: holds black until the camera is in place, then fades in.
/// </summary>
public class GameplayCameraRig : MonoBehaviour
{
    public static GameplayCameraRig Instance { get; private set; }

    Camera cam;
    Vector3 overviewPos;
    Quaternion overviewRot;
    float overviewFov;
    AutoFollowCamera follow;
    PlayerController followed;
    CrewVisualSet visuals;
    readonly HashSet<ICrewActor> dressed = new HashSet<ICrewActor>();
    readonly Dictionary<ICrewActor, float> firstSeen = new Dictionary<ICrewActor, float>();
    readonly List<PlayerController> humans = new List<PlayerController>();
    float nextScan;
    float started;

    Image fade;
    float fadeFrom = -1f;

    public bool IsFollowing => follow != null && follow.enabled;
    public PlayerController Followed => followed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive || scene.name != "Ship" || Instance != null) return;
        new GameObject("[GameplayCamera]").AddComponent<GameplayCameraRig>();
    }

    void Awake()
    {
        Instance = this;
        visuals = CrewVisualSet.Load();
        if (OpeningDirector.HandoffPending) BuildFade();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        GameSettings.Changed -= ApplySettings;
    }

    void Start()
    {
        cam = Camera.main;
        if (cam != null)
        {
            overviewPos = cam.transform.position;
            overviewRot = cam.transform.rotation;
            overviewFov = cam.fieldOfView;
        }
        started = Time.unscaledTime;
        GameSettings.Changed += ApplySettings;
    }

    void Update()
    {
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.25f;
            Scan();
        }
        TickFade();
    }

    void Scan()
    {
        humans.Clear();
        foreach (var actor in CrewRegistry.All)
        {
            if (actor is PlayerController pc) humans.Add(pc);
            if (dressed.Contains(actor) || !(actor is Component c)) continue;
            // Give the actor a moment to finish its own setup (seat colour) before swapping its look.
            if (!firstSeen.TryGetValue(actor, out float seen))
            {
                firstSeen[actor] = Time.unscaledTime;
                FaceSpawn(c.transform, actor.Seat); // before the camera snaps behind them below
                continue;
            }
            if (Time.unscaledTime - seen < 0.2f) continue;
            CrewVisual.Attach(c.gameObject, actor.Seat, visuals, !actor.IsBot);
            dressed.Add(actor);
        }
        humans.Sort((a, b) => a.Seat.CompareTo(b.Seat));

        if (humans.Count == 1) Follow(humans[0]);
        else if (humans.Count > 1) Overview();

        // Whatever happens, never leave the screen black for long.
        if (fade != null && fadeFrom < 0f && (follow != null || humans.Count > 1 || Time.unscaledTime - started > 2.5f))
            fadeFrom = Time.unscaledTime;
    }

    /// <summary>
    /// Players and bots appear on their Spawn_N marker facing +Z; turn them to the marker's facing (the furnace), as the
    /// opening shows them. Only while they're still standing on it, so nobody already walking gets spun round.
    /// </summary>
    static void FaceSpawn(Transform actor, int seat)
    {
        var marker = GameObject.Find($"Spawn_{seat}");
        if (marker == null) return;
        var d = actor.position - marker.transform.position;
        d.y = 0f;
        if (d.magnitude > 0.4f) return;
        float yaw = marker.transform.eulerAngles.y;
        if (actor.TryGetComponent(out ThirdPersonMover mover)) mover.Teleport(actor.position, yaw);
        else actor.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    void Follow(PlayerController player)
    {
        if (cam == null) return;
        if (follow != null && follow.enabled && followed == player) return;
        followed = player;
        if (follow == null && !cam.TryGetComponent(out follow)) follow = cam.gameObject.AddComponent<AutoFollowCamera>();
        follow.enabled = true;
        follow.target = player.transform;
        follow.mover = player.GetComponent<ThirdPersonMover>();
        if (follow.mover != null) follow.mover.followCamera = follow;
        ApplySettings();
        follow.SnapToTarget();
    }

    void Overview()
    {
        if (cam == null || follow == null || !follow.enabled) return;
        follow.enabled = false;
        foreach (var h in humans)
            if (h.TryGetComponent(out ThirdPersonMover m)) m.followCamera = null;
        followed = null;
        cam.transform.SetPositionAndRotation(overviewPos, overviewRot);
        cam.fieldOfView = overviewFov;
    }

    void ApplySettings() => GameSettings.ApplyTo(follow);

    // ---------- Fade in from Begin Voyage ----------

    void BuildFade()
    {
        var canvas = new GameObject("HandoffFade", typeof(Canvas)).GetComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        fade = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        fade.transform.SetParent(canvas.transform, false);
        fade.color = Color.black;
        fade.raycastTarget = false;
        var r = fade.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    void TickFade()
    {
        if (fade == null || fadeFrom < 0f) return;
        // Unscaled: the how-to-play card freezes time underneath.
        float a = 1f - Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - fadeFrom) / 1.2f);
        fade.color = new Color(0f, 0f, 0f, a);
        if (a > 0f) return;
        Destroy(fade.canvas.gameObject);
        fade = null;
        OpeningDirector.HandoffPending = false;
    }
}
