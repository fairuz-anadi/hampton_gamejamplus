using UnityEngine;

/// <summary>
/// A bot crew member. Owner: Riham.
/// It is an ICrewActor like a player, and it works stations through the same Station.Hold call the Interactor uses.
///
/// What it does, most important first:
///   1. Locked out by a vote: wander, touch nothing.
///   2. Its own order: think for a moment, then obey, refuse or (Stoker only) fake it.
///      Honest bots refuse "off" orders with a chance equal to Crew Trust %. The Stoker always obeys off orders
///      and fakes about half of the normal ones.
///   3. The Stoker's sabotage (StokerBrain).
///   4. A help or repair job handed out by CrewRoster.
///   5. Idle near where it spawned.
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
    [Tooltip("Time spent at the pick-up station on carry orders when there's no CarryItem to grab.")]
    [SerializeField] float pickUpSeconds = 1.2f;
    [Tooltip("Used when there's no real Station in the scene: work this long, then report.")]
    [SerializeField] float fallbackWorkSeconds = 3f;
    [Tooltip("Flat distance from a station's position that counts as 'at the station'.")]
    [SerializeField] float stationRange = 2.2f;
    [SerializeField] float helpJobTimeout = 25f;
    [SerializeField] float wanderRadius = 2.5f;

    // ---------- ICrewActor ----------
    public int Seat { get; private set; }
    public bool IsBot => true;
    public bool IsStoker { get; private set; }
    public Vector3 Position => transform.position;
    public string RoomId => Rooms.At(transform.position);
    public bool IsLockedOut => Time.time < lockedOutUntil;

    public BotMover Mover { get; private set; }
    /// <summary>Holding Interact at a station right now (Samprity's crew models play their work animation).</summary>
    public bool IsWorking => workingAt != null;
    public StokerBrain Stoker { get; private set; }
    public Vector3 Home { get; private set; }

    /// <summary>Free for a help or repair job: no order, no job, not locked out, not sabotaging.</summary>
    public bool IsFree => !IsLockedOut && CurrentOrder == null && job == null && (Stoker == null || !Stoker.IsBusy);

    public ActiveOrder CurrentOrder => OrderDirector.Instance != null ? OrderDirector.Instance.GetOrderFor(this) : null;

    /// <summary>Short text for debugging: what the bot is up to.</summary>
    public string Activity { get; private set; } = "idle";

    public float StationRange => stationRange;

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
    float lastProgress;
    float progressAt;
    CarryItem carrying;
    CarryItem fetching;

    // Where we are walking / working.
    StationType? goal;
    Vector3 goalSpot;
    float arrivedAt = -1f;
    Station workingAt;

    float nextWanderAt;

    // ---------- Setup ----------

    void Awake() => Mover = GetComponent<BotMover>();

    public void Setup(int seat, Vector3 home)
    {
        Seat = seat;
        Home = home;
        name = $"Bot {seat} ({CrewRegistry.SeatName(seat)})";
        CrewRegistry.Register(this);
    }

    public void SetStoker(bool stoker)
    {
        IsStoker = stoker;
        var brain = GetComponent<StokerBrain>();
        if (stoker && brain == null) brain = gameObject.AddComponent<StokerBrain>();
        if (brain != null) brain.enabled = stoker;
        Stoker = stoker ? brain : null;
    }

    /// <summary>Clears everything for a fresh run and goes back to the spawn point.</summary>
    public void ResetForRun()
    {
        StopWorking();
        DropCarried();
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
        DropCarried();
        job = null;
    }

    void OnEnable()
    {
        if (Seat > 0) CrewRegistry.Register(this);
    }

    void OnDisable()
    {
        StopWorking();
        DropCarried();
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
        // Stations only count holders who refresh every frame, like a player holding Interact.
        if (workingAt != null) workingAt.Hold(this, true);

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
        if (planOrderId != -1)
        {
            planOrderId = -1;
            DropCarried();
        }

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
            lastProgress = 0f;
            progressAt = 0f;
            fetching = null;
            goal = null;
            StopWorking();
            DropCarried();
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
        if (pickingUp && TryFetchCarryItem(o)) return;

        var target = pickingUp ? data.station : data.CheckStation;
        Activity = pickingUp ? $"picking up for #{o.Number}" : $"working #{o.Number} at the {target}";

        if (!ReachStation(target)) return;

        float worked = Time.time - arrivedAt;
        var st = StationRegistry.Get(target) as Station;

        if (st == null)
        {
            // No real station in this scene: pretend to work, then report.
            if (worked < (pickingUp ? pickUpSeconds : fallbackWorkSeconds)) return;
            if (pickingUp) { carryStage = 1; goal = null; return; }
            OrderDirector.Instance.Report(this);
            return;
        }

        if (pickingUp)
        {
            if (!st.IsBroken && worked >= pickUpSeconds) { carryStage = 1; goal = null; }
            return;
        }

        StartWorking(st);

        if (st.IsDone && !st.IsBroken)
        {
            StopWorking();
            OrderDirector.Instance.Report(this);
            DropCarried();
            return;
        }

        // Not moving forward and nobody else here (a two-person chore, or the Wheel on a delivery):
        // ask for a hand, again every few seconds.
        if (st.Progress01 > lastProgress + 0.001f) { lastProgress = st.Progress01; progressAt = Time.time; }
        bool stalled = Time.time - Mathf.Max(progressAt, arrivedAt) > 1f;
        if (stalled && OthersAt(st) == 0 && Time.time >= nextHelpAskAt)
        {
            nextHelpAskAt = Time.time + 6f;
            CrewRoster.RequestHelp(this, target, false);
        }
    }

    /// <summary>
    /// Carry orders with a real CarryItem in the scene: walk to this order's item and grab it.
    /// Returns true while fetching. Without an item, the bot just works the pick-up station for a moment.
    /// </summary>
    bool TryFetchCarryItem(ActiveOrder o)
    {
        if (carrying != null) { carryStage = 1; goal = null; return false; }

        if (fetching == null)
        {
            foreach (var item in FindObjectsByType<CarryItem>())
                if (!item.IsCarried && item.Order == o.Data) { fetching = item; break; }
            if (fetching == null) return false; // nothing to carry, use the plain station route
        }

        Activity = $"fetching the item for #{o.Number}";
        goal = null;
        var at = fetching.transform.position;
        if (BotMover.FlatDistance(transform.position, at) > 1.2f)
        {
            Mover.GoTo(BotMover.OnNavMesh(at), 0.8f);
            return true;
        }

        Mover.Stop();
        if (fetching.TryPickUp(this, transform)) carrying = fetching;
        fetching = null;
        carryStage = 1; // CarryItem now holds the delivery station for us when we get there
        return false;
    }

    void DropCarried()
    {
        fetching = null;
        if (carrying == null) return;
        carrying.Drop();
        carrying = null;
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
        if (StationRegistry.Get(job.station) is Station st) StartWorking(st);
    }

    // ---------- Movement and stations ----------

    /// <summary>Walks to a station. Returns true once standing at it.</summary>
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
        bool there = BotMover.FlatDistance(transform.position, goalSpot) <= 0.5f ||
                     (st != null && BotMover.FlatDistance(transform.position, st.Position) <= stationRange);
        if (st == null && goalSpot == transform.position) there = true;

        if (there)
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

    /// <summary>
    /// Where to stand: the station's WorkSpot_A / WorkSpot_B markers (from Samprity's deck), split by seat,
    /// otherwise a spot around the station spread out by seat so bots don't stack up.
    /// </summary>
    Vector3 StationSpot(StationType type)
    {
        var st = StationRegistry.Get(type);
        if (st == null) return transform.position;

        if (st is Component c)
        {
            var a = c.transform.Find("WorkSpot_A");
            var b = c.transform.Find("WorkSpot_B");
            var mine = Seat % 2 == 1 ? a ?? b : b ?? a;
            // Take the other spot if someone's already standing on ours.
            var other = mine == a ? b : a;
            if (mine != null && other != null && SomeoneOn(mine.position)) mine = other;
            if (mine != null) return BotMover.OnNavMesh(mine.position);
        }

        float angle = Seat * 90f + 45f;
        var offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (stationRange * 0.6f);
        return BotMover.OnNavMesh(st.Position + offset, stationRange);
    }

    bool SomeoneOn(Vector3 spot)
    {
        foreach (var c in CrewRegistry.All)
            if (c != (ICrewActor)this && BotMover.FlatDistance(c.Position, spot) < 0.6f) return true;
        return false;
    }

    /// <summary>Other crew standing at a station right now.</summary>
    int OthersAt(IStation st)
    {
        int n = 0;
        foreach (var c in CrewRegistry.All)
            if (c != (ICrewActor)this && BotMover.FlatDistance(c.Position, st.Position) <= stationRange + 0.3f) n++;
        return n;
    }

    /// <summary>Starts holding Interact at a station. Update keeps the hold fresh every frame until StopWorking.</summary>
    void StartWorking(Station st)
    {
        if (workingAt == st) return;
        StopWorking();
        workingAt = st;
        st.Hold(this, true);
    }

    public void StopWorking()
    {
        if (workingAt == null) return; // also true once the station is destroyed
        workingAt.Hold(this, false);
        workingAt = null;
    }

    void Wander()
    {
        goal = null;
        if (Time.time < nextWanderAt) return;
        nextWanderAt = Time.time + Random.Range(4f, 9f);
        var p = BotMover.OnNavMesh(Home + new Vector3(Random.Range(-wanderRadius, wanderRadius), 0f, Random.Range(-wanderRadius, wanderRadius)));
        if (Mathf.Abs(p.y - Home.y) < 0.5f) Mover.GoTo(p, 0.4f); // stay on the deck, not on top of a crate
    }

    void Face(Vector3 point)
    {
        var dir = point - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 540f * thinkInterval);
    }
}
