using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// The Captain in person, on the bridge. Owner: Samprity. Spawns itself in Ship (no scene edit) and only listens to
/// the game: it never issues orders, moves meters or changes a rule. The voice is still CaptainDirector's; this is
/// the man behind it.
///   Orders: when an order is issued he goes to the bridge voice pipe and talks into it while his line types out.
///   Between orders he paces the bridge, looks out of the windows and walks aft to look in on the wireless room
///   (in Part 1 also the passenger quarters behind it).
///   Part 2: the drunk act, worse with each of CaptainDirector's moods: he weaves, sways, stumbles, swigs the bottle.
///   Part 3: the act is over. He stands at the voice pipe, upright and still, and says his piece.
///   The iceberg: he stands aside at the port window and watches the bow. The wheel is yours.
/// Players talk to him with Interact when nothing else is in reach (CaptainTalk).
/// </summary>
public class CaptainOnDeck : MonoBehaviour
{
    public static CaptainOnDeck Instance { get; private set; }

    [Tooltip("Walking speed sober, and at full drunkenness.")]
    public Vector2 walkSpeed = new Vector2(1.35f, 0.85f);
    [Tooltip("Seconds he lingers at each spot on his rounds.")]
    public Vector2 dwell = new Vector2(5f, 10f);
    [Tooltip("Seconds between swigs: just drunk, and at worst.")]
    public Vector2 sipEvery = new Vector2(22f, 9f);
    [Tooltip("Seconds between stumbles while walking: just drunk, and at worst.")]
    public Vector2 stumbleEvery = new Vector2(16f, 6f);
    [Tooltip("A spot on his rounds must be this far (m) from every station, so whoever talks to him isn't also at a station.")]
    public float clearOfStations = 4.2f;

    /// <summary>The Captain is standing on the deck and can be found.</summary>
    public bool Ready => body != null;
    public Vector3 Position => transform.position;
    /// <summary>Just above his cap, for prompts and the speech bubble.</summary>
    public Vector3 HeadTop => head != null ? head.position + Vector3.up * 0.42f : transform.position + Vector3.up * 2.25f;
    /// <summary>Busy at the voice pipe, or watching the bow: not one for a chat.</summary>
    public bool Available => Ready && mode != Mode.Watch && mode != Mode.ToWatch && !Speaking && !goingToPipe;
    public bool InConversation => partner != null;
    // For CaptainChecks.
    public string ModeName => mode.ToString();
    public int RoundCount => rounds.Count;
    public Vector3 PipeSpot => pipeSpot;
    public Vector3 WatchSpot => watchSpot;
    public float Drunkenness => drunk;
    public CaptainBody Body => body;

    enum Mode { Waiting, ToPipe, AtPipe, Walking, Dwelling, ToWatch, Watch }

    class Spot
    {
        public Vector3 pos;
        public Vector3 facing;
        public bool part1Only;
    }

    Mode mode = Mode.Waiting;
    CaptainBody body;
    Transform head;
    NavMeshAgent agent;
    bool useNavMesh;
    Vector3 pipeSpot, pipeFacing, watchSpot, watchFacing;
    readonly List<Spot> rounds = new List<Spot>();
    Spot current;
    Vector3 facing;
    float until;
    bool goingToPipe;
    float drunk;
    float nextSip = float.MaxValue, nextStumble = float.MaxValue, steadyUntil;
    Component partner;
    Vector3 lastPos;

    // ---------- Spawning ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive || scene.name != "Ship" || Instance != null) return;
        if (CaptainSet.Load() == null || CaptainSet.Load().prefab == null) return;
        new GameObject("[Captain]").AddComponent<CaptainOnDeck>();
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        GameEvents.OrderIssued += OnOrderIssued;
        GameEvents.TruthStarted += OnTruth;
        GameEvents.StateChanged += OnStateChanged;
        GameEvents.PartChanged += OnPartChanged;
    }

    void OnDisable()
    {
        GameEvents.OrderIssued -= OnOrderIssued;
        GameEvents.TruthStarted -= OnTruth;
        GameEvents.StateChanged -= OnStateChanged;
        GameEvents.PartChanged -= OnPartChanged;
    }

    IEnumerator Start()
    {
        // The deck and the crew's NavMesh (CrewRoster bakes one at Start if the scene has none) come first, so his
        // own collider never ends up baked into it.
        float give = Time.time + 10f;
        GameObject wheel = null;
        while (Time.time < give)
        {
            wheel = GameObject.Find("Station_Wheel");
            if (wheel != null && NavMesh.SamplePosition(wheel.transform.position, out _, 4f, NavMesh.AllAreas)) break;
            yield return null;
        }
        Spawn(wheel);
    }

    void Spawn(GameObject wheel)
    {
        // Spots are measured off the old 36 m deck plan (ShipLayout.P moves them onto the longer ship) and the wheel,
        // so they follow the deck wherever it sits.
        var plannedWheel = ShipLayout.P(new Vector3(0f, 0f, 29f));
        var offset = wheel != null ? wheel.transform.position - plannedWheel : Vector3.zero;
        Vector3 P(float x, float z) => ShipLayout.P(new Vector3(x, 0f, z)) + offset;
        var bow = Vector3.forward;

        var mouth = GameObject.Find("VoicePipe_Mouth");
        pipeSpot = mouth != null ? Flat(mouth.transform.position, offset.y) - bow * 0.62f : P(1.25f, 29.93f);
        pipeFacing = bow;
        watchSpot = P(-4.1f, 31f);
        watchFacing = bow;

        var centre = wheel != null ? wheel.transform.position : plannedWheel + offset;
        void Add(Vector3 p, Vector3? look, bool part1Only = false) =>
            AddSpot(p, look ?? Flat(centre - p, 0f).normalized, part1Only);
        Add(P(4.2f, 31f), bow);                 // starboard bridge window
        Add(P(-4.1f, 31f), bow);                // port bridge window
        Add(P(3.7f, 26.4f), null);              // aft of the bridge, by the cabinet
        Add(P(3.2f, 21.8f), null);              // wireless room
        Add(P(2.4f, 18.3f), null);
        Add(P(-1.4f, 22.7f), Vector3.left);     // looking in on the wireless operator
        Add(new Vector3(2.6f, 0f, ShipLayout.InsertAt + ShipLayout.Extra - 4f) + offset, Vector3.back, true); // quarters

        var root = gameObject;
        root.transform.SetPositionAndRotation(Snap(pipeSpot), Quaternion.LookRotation(pipeFacing));
        var model = Instantiate(CaptainSet.Load().prefab, root.transform);
        model.name = "CaptainModel";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
        body = model.AddComponent<CaptainBody>();
        head = model.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);

        // A body for players to bump into; kept off raycasts so the follow camera doesn't treat him as a wall.
        root.layer = 2;
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 0.9f, 0f);
        capsule.height = 1.8f;
        capsule.radius = 0.3f;

        agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.35f;
        agent.height = 1.8f;
        agent.baseOffset = 0f;
        agent.angularSpeed = 300f;
        agent.acceleration = 5f;
        agent.stoppingDistance = 0.15f;
        agent.avoidancePriority = 90; // gives way to the crew
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        useNavMesh = NavMesh.SamplePosition(root.transform.position, out var hit, 3f, NavMesh.AllAreas);
        if (useNavMesh) agent.Warp(hit.position); else agent.enabled = false;

        gameObject.AddComponent<CaptainTalk>();
        lastPos = transform.position;
        facing = pipeFacing;
        mode = Mode.AtPipe;
        until = Time.time + 4f;
        Debug.Log($"[Captain] On the bridge at {transform.position:F1}, {rounds.Count} spots on his rounds, NavMesh {(useNavMesh ? "yes" : "no")}");
    }

    void AddSpot(Vector3 p, Vector3 look, bool part1Only)
    {
        if (!NavMesh.SamplePosition(p, out var hit, 1.2f, NavMesh.AllAreas)) return;
        foreach (var s in StationRegistry.All)
            if (Flat(s.Position - hit.position, 0f).magnitude < clearOfStations) return;
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(Snap(pipeSpot), hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return;
        rounds.Add(new Spot { pos = hit.position, facing = look, part1Only = part1Only });
    }

    // ---------- Events ----------

    void OnOrderIssued(ActiveOrder o)
    {
        if (Ready) HeadToPipe();
    }

    void OnTruth()
    {
        if (Ready) HeadToPipe();
    }

    void OnStateChanged(GameState s)
    {
        if (!Ready) return;
        if (s == GameState.Iceberg)
        {
            EndConversation();
            goingToPipe = false;
            mode = Mode.ToWatch;
            GoTo(watchSpot);
        }
    }

    void OnPartChanged(int part)
    {
        // "Then small things stop adding up": the first thing Part 2 shows is the bottle coming up.
        if (part == 2) { nextSip = Time.time + 2.5f; nextStumble = Time.time + 12f; }
    }

    // ---------- Conversation (CaptainTalk) ----------

    public void BeginConversation(Component who)
    {
        partner = who;
        Halt();
    }

    public void EndConversation()
    {
        if (partner == null) return;
        partner = null;
        until = Mathf.Max(until, Time.time + 2f);
    }

    // ---------- Behaviour ----------

    bool Speaking => CaptainDirector.Instance != null && CaptainDirector.Instance.IsSpeaking;

    void Update()
    {
        if (!Ready) return;
        float now = Time.time, dt = Time.deltaTime;
        var gm = GameManager.Instance;
        var state = gm != null ? gm.State : GameState.Menu;

        // How drunk he plays it: nothing in Part 1, building with CaptainDirector's mood through Part 2, gone in Part 3.
        int mood = CaptainDirector.Instance != null ? CaptainDirector.Instance.Mood : 0;
        float target = state == GameState.Part2 ? (mood <= 1 ? 0.4f : mood == 2 ? 0.7f : 1f) : 0f;
        // Part 3 drops the act at once: he was never drunk.
        drunk = state >= GameState.Part3 ? 0f : Mathf.MoveTowards(drunk, target, dt * (target > drunk ? 0.2f : 1f));
        body.Drunk = drunk;
        body.Stiff = state >= GameState.Part3;

        if (useNavMesh) agent.speed = state >= GameState.Part3 ? 1.1f : Mathf.Lerp(walkSpeed.x, walkSpeed.y, drunk);
        var step = transform.position - lastPos;
        step.y = 0f;
        lastPos = transform.position;
        float speed = dt > 0f ? step.magnitude / dt : 0f;
        body.Speed = speed;
        body.Talking = Speaking || partner != null;

        if (partner != null)
        {
            if (state >= GameState.Iceberg) EndConversation();
            else { Halt(); Face(Flat(partner.transform.position - transform.position, 0f), 6f); }
        }
        else
        {
            Think(state, now);
            if (!IsMoving) Face(facing, 3f);
        }
        DrunkBits(state, now, speed);
    }

    void Think(GameState state, float now)
    {
        switch (mode)
        {
            case Mode.Waiting:
                break;

            case Mode.ToPipe:
                if (Arrived(pipeSpot)) { mode = Mode.AtPipe; goingToPipe = false; facing = pipeFacing; until = now + Random.Range(3f, 5f); Halt(); }
                break;

            case Mode.AtPipe:
                // Part 3 is said from here, and he stays: the act is over and he isn't going anywhere.
                if (state == GameState.Part3 || state == GameState.Menu || Speaking || now < until) break;
                NextRound(state);
                break;

            case Mode.Walking:
                // Mid-ramble on the speaker he stops where he is; an order sends him to the pipe instead (HeadToPipe).
                if (Speaking || now < steadyUntil) { Halt(); break; }
                if (current != null && Arrived(current.pos))
                {
                    mode = Mode.Dwelling;
                    facing = current.facing;
                    until = now + Random.Range(dwell.x, dwell.y);
                    Halt();
                }
                else if (current != null) GoTo(current.pos);
                break;

            case Mode.Dwelling:
                if (now >= until && !Speaking) NextRound(state);
                break;

            case Mode.ToWatch:
                if (Arrived(watchSpot)) { mode = Mode.Watch; facing = watchFacing; Halt(); }
                break;
        }
    }

    void HeadToPipe()
    {
        if (mode == Mode.ToWatch || mode == Mode.Watch) return;
        goingToPipe = true;
        if (Arrived(pipeSpot)) { mode = Mode.AtPipe; goingToPipe = false; facing = pipeFacing; until = Time.time + Random.Range(3f, 5f); return; }
        mode = Mode.ToPipe;
        GoTo(pipeSpot);
    }

    void NextRound(GameState state)
    {
        var choices = new List<Spot>();
        foreach (var s in rounds)
            if (s != current && (!s.part1Only || state == GameState.Part1)) choices.Add(s);
        if (choices.Count == 0) { mode = Mode.AtPipe; until = Time.time + 6f; return; }
        // Mostly the bridge; drunk, he forgets where he was going and wanders anywhere.
        current = choices[Random.Range(0, choices.Count)];
        mode = Mode.Walking;
        GoTo(current.pos);
    }

    void DrunkBits(GameState state, float now, float speed)
    {
        if (drunk <= 0.05f || state != GameState.Part2) return;
        bool standing = speed < 0.15f;

        if (standing && partner == null && !Speaking && now >= nextSip)
        {
            body.Sip();
            nextSip = now + Mathf.Lerp(sipEvery.x, sipEvery.y, drunk) * Random.Range(0.8f, 1.25f);
        }

        if (!standing && drunk > 0.3f && now >= nextStumble)
        {
            body.Stumble(transform.right * (Random.value < 0.5f ? -1f : 1f));
            // Very drunk, he stops to steady himself after.
            if (drunk > 0.6f) steadyUntil = now + 1.4f;
            nextStumble = now + Mathf.Lerp(stumbleEvery.x, stumbleEvery.y, drunk) * Random.Range(0.8f, 1.3f);
        }
        if (nextSip == float.MaxValue) nextSip = now + 3f;
        if (nextStumble == float.MaxValue) nextStumble = now + 10f;
    }

    // ---------- Movement ----------

    bool IsMoving => useNavMesh ? agent.hasPath && !agent.isStopped && agent.remainingDistance > agent.stoppingDistance : moveTarget.HasValue;

    Vector3? moveTarget;

    void GoTo(Vector3 p)
    {
        if (useNavMesh)
        {
            if (!agent.isOnNavMesh) return;
            agent.isStopped = false;
            if (!agent.hasPath || Flat(agent.destination - p, 0f).sqrMagnitude > 0.01f) agent.SetDestination(p);
        }
        else moveTarget = p;
    }

    void Halt()
    {
        if (useNavMesh) { if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); } }
        else moveTarget = null;
    }

    bool Arrived(Vector3 p) => Flat(p - transform.position, 0f).magnitude <= 0.35f;

    void LateUpdate()
    {
        // No NavMesh (a sandbox): walk straight there.
        if (useNavMesh || !moveTarget.HasValue) return;
        var to = Flat(moveTarget.Value - transform.position, 0f);
        if (to.magnitude < 0.05f) { moveTarget = null; return; }
        transform.position += Vector3.ClampMagnitude(to, walkSpeed.x * Time.deltaTime);
        Face(to, 6f);
    }

    void Face(Vector3 dir, float rate)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-rate * Time.deltaTime));
    }

    Vector3 Snap(Vector3 p) => NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas) ? hit.position : p;

    static Vector3 Flat(Vector3 v, float y) => new Vector3(v.x, y, v.z);
}
