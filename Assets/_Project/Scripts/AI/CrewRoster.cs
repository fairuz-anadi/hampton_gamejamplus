using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Always 4 crew seats: humans keep the seats they joined with, bots fill the rest, and one random bot is the Stoker.
/// Also hands out help and repair jobs to free bots. Owner: Riham.
///
/// Spawned automatically in gameplay scenes by CrewAISystems. Anadi can also drop one into Ship.unity
/// to set spawn points and a bot prefab.
/// Rabib: players just register with CrewRegistry using seats 1-3. The roster notices within half a second
/// and removes the bot that held that seat. Call CrewRoster.RequestHelp(player, station, true) when Q is
/// pressed, or raise GameEvents.HelpCalled, either works.
/// </summary>
public class CrewRoster : MonoBehaviour
{
    public const int SeatCount = 4;

    public static CrewRoster Instance { get; private set; }

    [Tooltip("Optional. Needs a NavMeshAgent; BotMover and BotBrain are added if missing. Empty = coloured capsule.")]
    [SerializeField] GameObject botPrefab;
    [Tooltip("Optional, one per seat (index 0 = seat 1). Empty = around this object.")]
    [SerializeField] Transform[] spawnPoints = new Transform[0];
    [Tooltip("Bakes a NavMesh from the scene's colliders at start if there isn't one yet.")]
    [SerializeField] bool bakeNavMeshIfMissing = true;

    [Header("Helping")]
    [Tooltip("A human holding a two-person station for this long gets a bot without pressing Q (solo play).")]
    [SerializeField] float autoHelpAfter = 2f;
    [SerializeField] float requestLifetime = 15f;
    [SerializeField] Vector2 repairDelay = new Vector2(3f, 6f);

    /// <summary>Seat colours, index = seat. Rabib: use these for player capsules so seats match.</summary>
    public static readonly Color[] SeatColors =
    {
        Color.gray,
        new Color(0.85f, 0.25f, 0.22f), // 1 red
        new Color(0.22f, 0.5f, 0.9f),   // 2 blue
        new Color(0.95f, 0.75f, 0.2f),  // 3 yellow
        new Color(0.3f, 0.75f, 0.4f),   // 4 green
    };

    class HelpRequest
    {
        public ICrewActor asker;
        public StationType station;
        public bool repair;
        public float notBefore;
        public float expiresAt;
    }

    readonly BotBrain[] bots = new BotBrain[SeatCount + 1];
    readonly List<HelpRequest> requests = new List<HelpRequest>(8);
    readonly Dictionary<int, float> humanAtTwoPersonSince = new Dictionary<int, float>();
    float nextRefill;
    bool started;

    public IEnumerable<BotBrain> Bots
    {
        get { foreach (var b in bots) if (b != null) yield return b; }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        GameEvents.StateChanged += OnStateChanged;
        GameEvents.HelpCalled += OnHelpCalled;
        GameEvents.StationBroken += OnStationBroken;
    }

    void OnDisable()
    {
        GameEvents.StateChanged -= OnStateChanged;
        GameEvents.HelpCalled -= OnHelpCalled;
        GameEvents.StationBroken -= OnStationBroken;
    }

    void Start()
    {
        if (bakeNavMeshIfMissing) EnsureNavMesh();
        StokerBrain.ResetRun();
        Refill();
        started = true;
    }

    void Update()
    {
        if (started && Time.time >= nextRefill)
        {
            nextRefill = Time.time + 0.5f;
            Refill();
        }
        AutoHelpHumans();
        Dispatch();
    }

    // ---------- Seats ----------

    /// <summary>Spawns bots for empty seats, removes bots whose seat a human took, and makes sure there's a Stoker.</summary>
    public void Refill()
    {
        for (int seat = 1; seat <= SeatCount; seat++)
        {
            bool human = HumanInSeat(seat);
            if (human && bots[seat] != null)
            {
                Destroy(bots[seat].gameObject);
                bots[seat] = null;
            }
            else if (!human && bots[seat] == null)
            {
                bots[seat] = SpawnBot(seat);
            }
        }

        bool hasStoker = false;
        foreach (var b in Bots) if (b.IsStoker) hasStoker = true;
        if (!hasStoker) RerollStoker();
    }

    static bool HumanInSeat(int seat)
    {
        foreach (var c in CrewRegistry.All)
            if (!c.IsBot && c.Seat == seat) return true;
        return false;
    }

    /// <summary>Picks a new random Stoker among the bots. Called at the start of every run.</summary>
    public void RerollStoker()
    {
        var list = new List<BotBrain>(Bots);
        if (list.Count == 0) return;
        var chosen = list[Random.Range(0, list.Count)];
        foreach (var b in list) b.SetStoker(b == chosen);
        RunRecord.StokerSeat = chosen.Seat;
    }

    BotBrain SpawnBot(int seat)
    {
        var pos = SpawnPosition(seat);
        GameObject go;
        if (botPrefab != null)
        {
            go = Instantiate(botPrefab, pos, Quaternion.identity, transform);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.SetParent(transform);
            go.transform.position = pos;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = SeatColors[seat];
            var agentSetup = go.AddComponent<NavMeshAgent>();
            agentSetup.baseOffset = 1f;
            agentSetup.height = 2f;
            agentSetup.radius = 0.4f;
        }

        if (go.GetComponent<NavMeshAgent>() == null) go.AddComponent<NavMeshAgent>();
        if (go.GetComponent<BotMover>() == null) go.AddComponent<BotMover>();
        var brain = go.GetComponent<BotBrain>();
        if (brain == null) brain = go.AddComponent<BotBrain>();
        brain.Setup(seat, pos);
        return brain;
    }

    Vector3 SpawnPosition(int seat)
    {
        if (seat - 1 < spawnPoints.Length && spawnPoints[seat - 1] != null)
            return BotMover.OnNavMesh(spawnPoints[seat - 1].position);

        // Default: each bot starts in a different room, falling back to a small circle around the roster.
        string[] rooms = { "Boiler", "Corridor", "Wireless", "Bridge" };
        var zone = RoomZone.Find(rooms[(seat - 1) % rooms.Length]);
        if (zone != null) return BotMover.OnNavMesh(zone.RandomPoint(1f), 3f);
        var offset = Quaternion.Euler(0f, seat * 90f, 0f) * Vector3.forward * 2f;
        return BotMover.OnNavMesh(transform.position + offset, 3f);
    }

    void OnStateChanged(GameState s)
    {
        if (s != GameState.Part1 || !started) return; // the very first run is set up in Start
        // A new run (first start, restart or Play again): fresh Stoker, everyone back home.
        requests.Clear();
        StokerBrain.ResetRun();
        Refill();
        foreach (var b in Bots) b.ResetForRun();
        RerollStoker();
    }

    // ---------- NavMesh ----------

    void EnsureNavMesh()
    {
        if (NavMesh.SamplePosition(transform.position, out _, 50f, NavMesh.AllAreas)) return;
        if (FindAnyObjectByType<NavMeshSurface>() != null) return;

        var surfaceGo = new GameObject("[Runtime NavMesh]");
        var surface = surfaceGo.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~(1 << RoomZone.IgnoreRaycastLayer);
        surface.BuildNavMesh();
        Debug.Log("[CrewRoster] No NavMesh in the scene, so I baked one from the colliders. Bake a NavMeshSurface on the deck for the real thing.");
    }

    // ---------- Help and repair ----------

    /// <summary>
    /// Ask for a bot to come and work a station with you.
    /// fromHuman = a player pressed Q: uses Crew Trust for the delay and may be ignored at low trust.
    /// </summary>
    public static void RequestHelp(ICrewActor asker, StationType station, bool fromHuman)
    {
        if (Instance == null) return;
        Instance.AddRequest(asker, station, false, fromHuman);
    }

    void AddRequest(ICrewActor asker, StationType station, bool repair, bool fromHuman)
    {
        foreach (var r in requests)
            if (r.station == station && r.repair == repair && r.asker == asker) return;

        if (fromHuman && Random.value < MeterRules.BotIgnoresHelpChance)
        {
            Debug.Log($"[CrewRoster] Nobody answered {CrewRegistry.SeatName(asker?.Seat ?? 0)}'s call (Crew Trust is low).");
            return;
        }

        float delay = repair ? Random.Range(repairDelay.x, repairDelay.y)
                    : fromHuman ? MeterRules.BotHelpDelay
                    : MeterRules.BotHelpDelay * 0.5f;
        requests.Add(new HelpRequest
        {
            asker = asker,
            station = station,
            repair = repair,
            notBefore = Time.time + delay,
            expiresAt = Time.time + delay + requestLifetime,
        });
    }

    void OnHelpCalled(ICrewActor who, StationType station) => AddRequest(who, station, false, who == null || !who.IsBot);

    void OnStationBroken(StationType station, ICrewActor by) => AddRequest(null, station, true, false);

    void Dispatch()
    {
        for (int i = requests.Count - 1; i >= 0; i--)
        {
            var r = requests[i];
            if (Time.time > r.expiresAt || !StationNeedsWork(r.station)) { requests.RemoveAt(i); continue; }
            if (Time.time < r.notBefore) continue;

            var helper = NearestFreeBot(r);
            if (helper == null) continue;
            helper.AssignJob(new BotBrain.Job { station = r.station, repair = r.repair, forWhom = r.asker });
            requests.RemoveAt(i);
        }
    }

    /// <summary>Honest bots first. The Stoker only helps when nobody else can (good cover), and never repairs.</summary>
    BotBrain NearestFreeBot(HelpRequest r)
    {
        var st = StationRegistry.Get(r.station);
        Vector3 at = st != null ? st.Position : r.asker?.Position ?? transform.position;

        BotBrain best = null, bestStoker = null;
        float bestDist = float.MaxValue, stokerDist = float.MaxValue;
        foreach (var b in Bots)
        {
            if (!b.IsFree || (ICrewActor)b == r.asker) continue;
            float d = BotMover.FlatDistance(b.Position, at);
            if (b.IsStoker) { if (d < stokerDist) { stokerDist = d; bestStoker = b; } }
            else if (d < bestDist) { bestDist = d; best = b; }
        }
        if (best != null) return best;
        return r.repair ? null : bestStoker;
    }

    /// <summary>True while a station is broken, or has an active order on it that isn't green yet.</summary>
    public static bool StationNeedsWork(StationType type)
    {
        var st = StationRegistry.Get(type);
        if (st != null && st.IsBroken) return true;
        var od = OrderDirector.Instance;
        if (od == null) return false;
        foreach (var o in od.Active)
        {
            bool here = o.Data.station == type || o.Data.CheckStation == type;
            if (here && !od.IsStationDone(o)) return true;
        }
        return false;
    }

    /// <summary>Solo players shouldn't have to know about Q: a human standing at their two-person station gets a bot.</summary>
    void AutoHelpHumans()
    {
        var od = OrderDirector.Instance;
        if (od == null) return;
        foreach (var c in CrewRegistry.All)
        {
            if (c.IsBot) continue;
            var o = od.GetOrderFor(c);
            var st = o != null && o.Data.needsTwo ? StationRegistry.Get(o.Data.CheckStation) : null;
            bool atStation = st != null && !od.IsStationDone(o) &&
                             BotMover.FlatDistance(c.Position, st.Position) <= (st is ICrewWorkStation ws ? ws.InteractRange : 1.6f) + 0.3f;

            if (!atStation) { humanAtTwoPersonSince.Remove(c.Seat); continue; }
            if (!humanAtTwoPersonSince.TryGetValue(c.Seat, out float since))
            {
                humanAtTwoPersonSince[c.Seat] = Time.time;
                continue;
            }
            if (Time.time - since >= autoHelpAfter)
            {
                AddRequest(c, st.Type, false, false);
                humanAtTwoPersonSince[c.Seat] = Time.time + 6f; // don't ask again straight away
            }
        }
    }
}
