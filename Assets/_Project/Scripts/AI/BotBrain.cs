using UnityEngine;

/// <summary>
/// A bot crew member. Owner: Riham.
/// It is an ICrewActor like a player, and it uses stations through the same ICrewWorkStation methods.
///
/// What it does, most important first:
///   1. Locked out by a vote: wander, touch nothing.
///   2. Its own order: think for a moment, then obey, refuse or (Stoker only) fake it.
///      Honest bots refuse "off" orders with a chance equal to Crew Trust %. The Stoker always obeys off orders
///      and fakes about half of the normal ones.
///   3. The Stoker's sabotage (StokerBrain).
///   4. A help or repair job handed out by CrewRoster.
///   5. Idle around its home room.
/// </summary>
[RequireComponent(typeof(BotMover))]
public class BotBrain : MonoBehaviour, ICrewActor
{
    [SerializeField] float thinkInterval = 0.2f;
    [Tooltip("Pause after an order arrives before the bot starts moving, so it reads like it's listening.")]
    [SerializeField] Vector2 reactDelay = new Vector2(0.6f, 1.6f);
    [Tooltip("How long a bot 'considers' an order before refusing it out loud.")]
    [SerializeField] Vector2 refuseDelay = new Vector2(2.5f, 4.5f);
    [SerializeField] Vector2 fakeDelay = new Vector2(2f, 4f);
    [SerializeField, Range(0f, 1f)] float stokerFakeChance = 0.5f;
    [Tooltip("Time spent at the pick-up station on carry orders.")]
    [SerializeField] float pickUpSeconds = 1.2f;
    [Tooltip("Used when the station doesn't implement ICrewWorkStation yet: work this long, then report.")]
    [SerializeField] float fallbackWorkSeconds = 3f;
    [SerializeField] float defaultInteractRange = 1.6f;
    [SerializeField] float helpJobTimeout = 25f;

    // ---------- ICrewActor ----------
    public int Seat { get; private set; }
    public bool IsBot => true;
    public bool IsStoker { get; private set; }
    public Vector3 Position => transform.position;
    public string RoomId => RoomZone.IdAt(transform.position);
    public bool IsLockedOut => Time.time < lockedOutUntil;

    public BotMover Mover { get; private set; }
    public StokerBrain Stoker { get; private set; }
    public Vector3 Home { get; private set; }
    public string HomeRoom { get; private set; } = "";

    /// <summary>Free for a help or repair job: no order, no job, not locked out, not sabotaging.</summary>
    public bool IsFree => !IsLockedOut && CurrentOrder == null && job == null && (Stoker == null || !Stoker.IsBusy);

    public ActiveOrder CurrentOrder => OrderDirector.Instance != null ? OrderDirector.Instance.GetOrderFor(this) : null;

    /// <summary>Short text for debugging: what the bot is up to.</summary>
    public string Activity { get; private set; } = "idle";

    enum Plan { Obey, Fake, Refuse }

    public class Job
    {
        public StationType station;
        public bool repair;
        public ICrewActor forWhom;
        public float startedAt;
    }

    float lockedOutUntil;
    float nextThink;
    Job job;

    int planOrderId = -1;
    Plan plan;
    float planReadyAt;
    int carryStage;
    float fakeAt;
    float nextHelpAskAt;

    // Where we are walking / working.
    StationType? goal;
    Vector3 goalSpot;
    float arrivedAt = -1f;
    ICrewWorkStation workingAt;

    float nextWanderAt;

    // ---------- Setup ----------

    void Awake() => Mover = GetComponent<BotMover>();

    public void Setup(int seat, Vector3 home)
    {
        Seat = seat;
        Home = home;
        HomeRoom = RoomZone.IdAt(home);
        name = $"Bot {seat} ({CrewRegistry.SeatName(seat)})";
        CrewRegistry.Register(this);
    }

    public void SetStoker(bool stoker)
    {
        IsStoker = stoker;
        Stoker = GetComponent<StokerBrain>();
        if (stoker && Stoker == null) Stoker = gameObject.AddComponent<StokerBrain>();
        if (Stoker != null) Stoker.enabled = stoker;
        if (!stoker) Stoker = null;
    }

    /// <summary>Clears everything for a fresh run and walks home.</summary>
    public void ResetForRun()
    {
        StopWorking();
        job = null;
        planOrderId = -1;
        goal = null;
        lockedOutUntil = 0f;
        Mover.Teleport(Home);
        Stoker?.ResetForRun();
    }

    public void LockOut(float seconds)
    {
        lockedOutUntil = Time.time + seconds;
        StopWorking();
        job = null;
    }

    void OnEnable()
    {
        if (Seat > 0) CrewRegistry.Register(this);
    }

    void OnDisable()
    {
        StopWorking();
        CrewRegistry.Unregister(this);
    }

    // ---------- Jobs from CrewRoster ----------

    public void AssignJob(Job j)
    {
        j.startedAt = Time.time;
        job = j;
        goal = null;
    }

    // ---------- Main loop ----------

    void Update()
    {
        if (Time.time < nextThink) return;
        nextThink = Time.time + thinkInterval;
        Think();
    }

    void Think()
    {
        var gm = GameManager.Instance;
        if (gm != null && (gm.State == GameState.Iceberg || gm.State == GameState.Results))
        {
            StopWorking();
            Activity = "watching the bow";
            return;
        }

        if (IsLockedOut)
        {
            StopWorking();
            Activity = "locked out";
            Wander();
            return;
        }

        var order = CurrentOrder;
        if (order != null)
        {
            job = null;
            TickOrder(order);
            return;
        }
        planOrderId = -1;

        if (Stoker != null && Stoker.TickSabotage())
        {
            Activity = "up to something";
            return;
        }

        if (job != null)
        {
            TickJob();
            return;
        }

        StopWorking();
        Activity = "idle";
        Wander();
    }

    // ---------- Own orders ----------

    Plan Decide(ActiveOrder o)
    {
        if (IsStoker)
            return o.Data.IsOff || Random.value >= stokerFakeChance ? Plan.Obey : Plan.Fake;
        if (o.Data.IsOff && Random.value < MeterRules.BotRefusesOffOrderChance)
            return Plan.Refuse;
        return Plan.Obey;
    }

    void TickOrder(ActiveOrder o)
    {
        if (o.Id != planOrderId)
        {
            planOrderId = o.Id;
            plan = Decide(o);
            var delay = plan == Plan.Refuse ? refuseDelay : reactDelay;
            planReadyAt = Time.time + Random.Range(delay.x, delay.y);
            carryStage = 0;
            fakeAt = -1f;
            nextHelpAskAt = 0f;
            goal = null;
            StopWorking();
        }

        if (Time.time < planReadyAt)
        {
            Mover.Stop();
            Activity = $"thinking about order #{o.Number}";
            return;
        }

        var od = OrderDirector.Instance;
        switch (plan)
        {
            case Plan.Refuse:
                Activity = "refusing";
                od.Refuse(this);
                return;

            case Plan.Fake:
                Activity = $"pretending to do #{o.Number}";
                if (!ReachStation(o.Data.CheckStation)) return;
                if (fakeAt < 0f) fakeAt = Time.time + Random.Range(fakeDelay.x, fakeDelay.y);
                if (Time.time >= fakeAt) od.Report(this);
                return;

            default:
                Obey(o);
                return;
        }
    }

    void Obey(ActiveOrder o)
    {
        var data = o.Data;
        bool pickingUp = data.isCarry && carryStage == 0 && data.station != data.deliverTo;
        var target = pickingUp ? data.station : data.CheckStation;
        Activity = pickingUp ? $"picking up for #{o.Number}" : $"working #{o.Number} at the {target}";

        if (!ReachStation(target)) return;

        float worked = Time.time - arrivedAt;
        var ws = StationRegistry.Get(target) as ICrewWorkStation;

        if (ws == null)
        {
            // No real station yet: pretend to work, then report.
            if (worked < (pickingUp ? pickUpSeconds : fallbackWorkSeconds)) return;
            if (pickingUp) { carryStage = 1; goal = null; return; }
            OrderDirector.Instance.Report(this);
            return;
        }

        StartWorking(ws);

        if (pickingUp)
        {
            if (!ws.IsBroken && worked >= pickUpSeconds)
            {
                StopWorking();
                carryStage = 1;
                goal = null;
            }
            return;
        }

        if (ws.IsDone && !ws.IsBroken)
        {
            StopWorking();
            OrderDirector.Instance.Report(this);
            return;
        }

        // Two-person chore and nobody else on it: ask for a hand, again every few seconds.
        if (data.needsTwo && ws.WorkerCount < 2 && worked > 0.75f && Time.time >= nextHelpAskAt)
        {
            nextHelpAskAt = Time.time + 6f;
            CrewRoster.RequestHelp(this, target, false);
        }
    }

    // ---------- Help and repair jobs ----------

    void TickJob()
    {
        if (Time.time - job.startedAt > helpJobTimeout || !CrewRoster.StationNeedsWork(job.station))
        {
            StopWorking();
            job = null;
            goal = null;
            return;
        }

        Activity = job.repair ? $"repairing the {job.station}" : $"helping at the {job.station}";
        if (!ReachStation(job.station)) return;
        if (StationRegistry.Get(job.station) is ICrewWorkStation ws) StartWorking(ws);
    }

    // ---------- Movement and stations ----------

    /// <summary>Walks to a station. Returns true once standing in its work range.</summary>
    public bool ReachStation(StationType type)
    {
        if (goal != type)
        {
            StopWorking();
            goal = type;
            goalSpot = StationSpot(type);
            arrivedAt = -1f;
        }

        var st = StationRegistry.Get(type);
        float range = st is ICrewWorkStation ws ? ws.InteractRange : defaultInteractRange;
        Vector3 center = st != null ? st.Position : goalSpot;

        if (BotMover.FlatDistance(transform.position, center) <= range)
        {
            Mover.Stop();
            if (st != null) Face(st.Position);
            if (arrivedAt < 0f) arrivedAt = Time.time;
            return true;
        }

        arrivedAt = -1f;
        Mover.GoTo(goalSpot, 0.3f);
        return false;
    }

    /// <summary>A spot next to the station, spread out by seat so bots don't stack up.</summary>
    Vector3 StationSpot(StationType type)
    {
        var st = StationRegistry.Get(type);
        if (st == null)
        {
            var zone = RoomZone.Find(RoomZone.DefaultRoom(type));
            return zone != null ? BotMover.OnNavMesh(zone.Center) : transform.position;
        }

        float range = st is ICrewWorkStation ws ? ws.InteractRange : defaultInteractRange;
        float angle = Seat * 90f + 45f;
        var offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (range * 0.7f);
        var spot = BotMover.OnNavMesh(st.Position + offset, range);
        // If the NavMesh snapped us somewhere out of reach (other side of a wall), go straight at it instead.
        return BotMover.FlatDistance(spot, st.Position) <= range ? spot : BotMover.OnNavMesh(st.Position, range);
    }

    void StartWorking(ICrewWorkStation ws)
    {
        if (workingAt == ws) return;
        StopWorking();
        workingAt = ws;
        ws.BeginWork(this);
    }

    public void StopWorking()
    {
        if (workingAt == null) return;
        var ws = workingAt;
        workingAt = null;
        // The station may already be destroyed when a scene unloads.
        if (ws is Object unityObject && unityObject == null) return;
        ws.EndWork(this);
    }

    void Wander()
    {
        goal = null;
        if (Time.time < nextWanderAt) return;
        nextWanderAt = Time.time + Random.Range(4f, 9f);

        var zone = RoomZone.Find(HomeRoom);
        Vector3 p = zone != null ? zone.RandomPoint(1f) : Home + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
        Mover.GoTo(BotMover.OnNavMesh(p), 0.4f);
    }

    void Face(Vector3 point)
    {
        var dir = point - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 540f * thinkInterval);
    }
}
