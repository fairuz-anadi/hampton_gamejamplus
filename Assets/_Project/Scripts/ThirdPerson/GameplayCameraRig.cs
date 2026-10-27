using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ship's camera and crew presentation, added at runtime so Ship.unity stays untouched. Owner: Samprity.
///   The iceberg finale: the follow camera eases back for more sea ahead, the overview tilts forward over the bow so
///   everyone sees the berg coming, and both shake as it closes (IcebergPresentation).
///   The third-person follow camera (AutoFollowCamera) always follows the lowest-seat human on screen, 1-3 of them;
///   that seat's mouse/right-stick look (Rabib) orbits it, the others just ride along.
///   Everyone (players and bots) wears their seat's crew model instead of a capsule (CrewVisual).
///   Coming from Begin Voyage: holds black until the camera is in place, then fades in.
///   Locks the cursor the moment someone's actually being followed; PauseMenu and GameSystems let go of it again.
/// </summary>
[DefaultExecutionOrder(100)] // after AutoFollowCamera, so the iceberg shake lands on top of its pose
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
    bool cursorLocked;

    Image fade;
    float fadeFrom = -1f;

    IcebergPresentation iceberg;
    float finale;               // 0 = normal framing, 1 = iceberg framing
    float followDistance = -1f, followHeight, followPitch;

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
        iceberg = gameObject.AddComponent<IcebergPresentation>();
        IcebergSequence.CameraHandledExternally = true; // this rig frames and shakes the finale
    }

    void OnDisable()
    {
        IcebergSequence.CameraHandledExternally = false;
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

        // One screen, 1-3 humans: the lowest seat always owns the camera (and the mouse-look that drives it).
        if (humans.Count > 0) Follow(humans[0]);

        // Whatever happens, never leave the screen black for long.
        if (fade != null && fadeFrom < 0f && (follow != null || Time.unscaledTime - started > 2.5f))
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
        // Switching whose camera this is: the old target's mover must stop pointing at it, or their own leftover
        // look input (still read every frame by their own PlayerController) would fight the new owner's.
        if (followed != null && followed.TryGetComponent(out ThirdPersonMover prevMover) && prevMover.followCamera == follow)
            prevMover.followCamera = null;
        followed = player;
        if (follow == null && !cam.TryGetComponent(out follow)) follow = cam.gameObject.AddComponent<AutoFollowCamera>();
        follow.enabled = true;
        follow.target = player.transform;
        follow.mover = player.GetComponent<ThirdPersonMover>();
        if (follow.mover != null) follow.mover.followCamera = follow;
        ApplySettings();
        follow.SnapToTarget();

        if (!cursorLocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            cursorLocked = true;
        }
    }

    void ApplySettings() => GameSettings.ApplyTo(follow);

    // ---------- Iceberg finale ----------

    void LateUpdate()
    {
        if (cam == null) return;
        bool on = iceberg != null && iceberg.Active;
        finale = Mathf.MoveTowards(finale, on ? 1f : 0f, Time.deltaTime / 2.5f);
        float k = Mathf.SmoothStep(0f, 1f, finale);

        if (IsFollowing)
        {
            // Up a little and level out: the horizon (where the berg comes from) moves down into the frame.
            if (followDistance < 0f) { followDistance = follow.distance; followHeight = follow.height; followPitch = follow.pitch; }
            follow.distance = Mathf.Lerp(followDistance, followDistance * 1.3f, k);
            follow.height = Mathf.Lerp(followHeight, followHeight * 1.45f, k);
            follow.pitch = Mathf.Lerp(followPitch, 8f, k);
        }
        else if (k > 0f || on)
        {
            // Shared screen: from the deck overview to just aft of the bridge, high up: the wheel at the bottom of the
            // frame, the bow and the berg ahead.
            // On the longer ship (ShipLayout) the same view sits the promenade's length further forward.
            var bowView = new Vector3(0f, 19f, 4f + ShipLayout.Extra);
            var look = Quaternion.LookRotation(new Vector3(0f, 0f, ShipLayout.Z(75f)) - bowView);
            cam.transform.SetPositionAndRotation(Vector3.Lerp(overviewPos, bowView, k), Quaternion.Slerp(overviewRot, look, k));
            cam.fieldOfView = Mathf.Lerp(overviewFov, 50f, k);
        }

        float shake = iceberg != null ? iceberg.Shake : 0f;
        if (shake > 0.001f)
        {
            float t = Time.time * 17f;
            var jitter = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * 0.22f * shake;
            cam.transform.position += cam.transform.rotation * jitter;
            cam.transform.rotation *= Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(t * 0.6f, 2.1f) - 0.5f) * 3f * shake);
        }
    }

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
