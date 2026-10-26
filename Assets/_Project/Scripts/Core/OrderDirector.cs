using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Issues the Captain's orders, runs their timers and resolves the three choices. Owner: Anadi.
///
/// How other systems use it:
///   Players and bots:  OrderDirector.Instance.Report(actor)   // "done": honest if the station is green, fake if not
///                      OrderDirector.Instance.Refuse(actor)
///                      OrderDirector.Instance.GetOrderFor(actor)
///   Captain / UI:      listen to GameEvents.OrderIssued / OrderResolved / FakeNoticed
/// </summary>
public class OrderDirector : MonoBehaviour
{
    public static OrderDirector Instance { get; private set; }

    [Tooltip("Leave empty to load every OrderData in Resources/Orders, sorted by file name.")]
    [SerializeField] List<OrderData> orders = new List<OrderData>();

    [Header("Pacing (plan: Part 1 ends ~2:30, Part 2 ~6:30)")]
    [Tooltip("Seconds before a Part 1 or Part 3 order.")]
    [SerializeField] Vector2 issueInterval = new Vector2(25f, 30f);
    [Tooltip("Seconds before a Part 2 order. Longer, so the 3 ice warnings (3:20, 4:30, 5:40) land in Part 2.")]
    [SerializeField] Vector2 offPartInterval = new Vector2(35f, 45f);
    [SerializeField] float firstOrderDelay = 5f;
    [SerializeField, Range(1, 4)] int maxActive = 2;

    [Header("Fakes")]
    [SerializeField, Range(0f, 1f)] float fakeNoticeChance = 0.5f;
    [SerializeField] Vector2 fakeNoticeDelay = new Vector2(15f, 40f);
    [SerializeField] MeterDelta fakeNoticedPenalty = new MeterDelta(-10, 0, 0);
    [Tooltip("A crewmate in the same room sees the red light when someone fakes a report.")]
    [SerializeField] MeterDelta fakeWitnessedPenalty = new MeterDelta(0, 0, -5);

    readonly List<ActiveOrder> active = new List<ActiveOrder>(4);
    int nextIndex;
    int nextId = 1;
    int lastSeat;
    float nextIssueAt;
    bool running;

    public IReadOnlyList<OrderData> Orders => orders;
    public IReadOnlyList<ActiveOrder> Active => active;
    public int NextIndex => nextIndex;
    public bool IsRunning => running;
    public bool AllOrdersDone => nextIndex >= orders.Count && active.Count == 0;
    /// <summary>Seconds until the next order is issued (for debug/UI).</summary>
    public float SecondsToNext => running ? Mathf.Max(0f, nextIssueAt - Time.time) : 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (orders.Count == 0)
            orders = Resources.LoadAll<OrderData>("Orders").OrderBy(o => o.name).ToList();
        if (orders.Count == 0)
            Debug.LogWarning("[OrderDirector] No OrderData found in Resources/Orders.");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Begin()
    {
        StopAllCoroutines(); // no "fake noticed" left over from a previous run
        active.Clear();
        nextIndex = 0;
        nextId = 1;
        lastSeat = 0;
        running = true;
        nextIssueAt = Time.time + firstOrderDelay;
    }

    public void Stop() => running = false;

    void Update()
    {
        if (!running) return;

        // Tick timers. Iterate backwards because resolving removes from the list.
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var o = active[i];
            o.TimeLeft -= Time.deltaTime;
            if (o.TimeLeft <= 0f) Resolve(o, Choice.Expired);
        }

        if (Time.time >= nextIssueAt) TryIssueNext();
    }

    void TryIssueNext()
    {
        if (nextIndex >= orders.Count || active.Count >= maxActive) return;

        var data = orders[nextIndex];

        // The final order starts Part 3, so let the earlier ones finish first.
        if (data.kind == OrderKind.Final && active.Count > 0) return;

        var assignee = PickAssignee();
        if (assignee == null && CrewRegistry.All.Count > 0) return; // everyone is busy, try again next frame

        if (GameManager.Instance != null) GameManager.Instance.EnterPart(data.part);

        var order = new ActiveOrder(nextId++, nextIndex + 1, data, assignee);
        nextIndex++;
        active.Add(order);

        StationRegistry.Get(data.station)?.PrepareForOrder(order);
        if (data.isCarry && data.deliverTo != data.station)
            StationRegistry.Get(data.deliverTo)?.PrepareForOrder(order);

        GameEvents.RaiseOrderIssued(order);
        nextIssueAt = Time.time + NextInterval();
    }

    /// <summary>The gap before the next order depends on which part that order belongs to.</summary>
    float NextInterval()
    {
        var range = nextIndex < orders.Count && orders[nextIndex].part == 2 ? offPartInterval : issueInterval;
        return Random.Range(range.x, range.y);
    }

    /// <summary>Round-robin over seats that have no active order and aren't locked out. Copes with gaps in seat numbers.</summary>
    ICrewActor PickAssignee()
    {
        var crew = CrewRegistry.All; // sorted by seat
        if (crew.Count == 0) return null;
        int start = 0;
        while (start < crew.Count && crew[start].Seat <= lastSeat) start++;
        for (int i = 0; i < crew.Count; i++)
        {
            var c = crew[(start + i) % crew.Count];
            if (c.IsLockedOut || GetOrderFor(c) != null) continue;
            lastSeat = c.Seat;
            return c;
        }
        return null;
    }

    public ActiveOrder GetOrderFor(ICrewActor actor)
    {
        if (actor == null) return null;
        foreach (var o in active) if (o.Assignee == actor) return o;
        return null;
    }

    /// <summary>The assignee says "done". Honest if the station is green, otherwise a fake.</summary>
    public Choice? Report(ICrewActor actor)
    {
        var o = GetOrderFor(actor);
        if (o == null) return null;
        var choice = IsStationDone(o) ? Choice.Obey : Choice.Fake;
        Resolve(o, choice, witnessed: choice == Choice.Fake && HasWitness(actor));
        return choice;
    }

    /// <summary>True when another crew member is in the same room as the faker.</summary>
    static bool HasWitness(ICrewActor faker)
    {
        if (string.IsNullOrEmpty(faker.RoomId)) return false;
        foreach (var c in CrewRegistry.All)
            if (c != faker && c.RoomId == faker.RoomId) return true;
        return false;
    }

    public bool Refuse(ICrewActor actor)
    {
        var o = GetOrderFor(actor);
        if (o == null) return false;
        Resolve(o, Choice.Refuse);
        return true;
    }

    /// <summary>True when the order's station shows green. With no station in the scene, assume done (test scenes).</summary>
    public bool IsStationDone(ActiveOrder o)
    {
        var st = StationRegistry.Get(o.Data.CheckStation);
        return st == null || (st.IsDone && !st.IsBroken);
    }

    /// <summary>
    /// Resolves an order with a specific choice. Use Report/Refuse in gameplay; this is for debug and bots.
    /// witnessed: a crewmate saw the red light during a fake, which also costs Crew Trust.
    /// </summary>
    public void Resolve(ActiveOrder o, Choice choice, bool witnessed = false)
    {
        if (o == null || o.IsResolved) return;
        o.IsResolved = true;
        o.Result = choice;
        active.Remove(o);

        var delta = o.Data.DeltaFor(choice);
        if (witnessed) delta += fakeWitnessedPenalty;
        MeterSystem.Instance?.Apply(delta);

        var entry = new ChoiceEntry
        {
            orderNumber = o.Number,
            order = o.Data,
            seat = o.Assignee?.Seat ?? 0,
            byBot = o.Assignee?.IsBot ?? false,
            byStoker = o.Assignee?.IsStoker ?? false,
            choice = choice,
            delta = delta,
            timeInRun = Time.time - RunRecord.RunStartTime,
            fakeWitnessed = witnessed,
        };
        RunRecord.Choices.Add(entry);

        if (choice == Choice.Obey && o.Data.locksLifeboats) RunRecord.LifeboatStoreLocked = true;

        if (choice == Choice.Fake && Random.value < fakeNoticeChance)
            StartCoroutine(NoticeFakeLater(o, entry));

        GameEvents.RaiseOrderResolved(o, choice);

        if (AllOrdersDone)
        {
            running = false;
            if (GameManager.Instance != null) GameManager.Instance.OnAllOrdersDone();
        }
    }

    IEnumerator NoticeFakeLater(ActiveOrder o, ChoiceEntry entry)
    {
        yield return new WaitForSeconds(Random.Range(fakeNoticeDelay.x, fakeNoticeDelay.y));
        if (GameManager.Instance != null && GameManager.Instance.State >= GameState.Iceberg) yield break;
        entry.fakeNoticed = true;
        entry.delta += fakeNoticedPenalty;
        MeterSystem.Instance?.Apply(fakeNoticedPenalty);
        GameEvents.RaiseFakeNoticed(o);
    }

    /// <summary>Debug: drop everything active and jump to the first order of a part.</summary>
    public void SkipToPart(int part)
    {
        foreach (var o in active.ToArray()) Resolve(o, Choice.Expired);
        int idx = orders.FindIndex(o => o.part >= part);
        nextIndex = idx < 0 ? orders.Count : idx;
        running = nextIndex < orders.Count;
        nextIssueAt = Time.time + 1f;
        if (!running && GameManager.Instance != null) GameManager.Instance.OnAllOrdersDone();
    }
}
