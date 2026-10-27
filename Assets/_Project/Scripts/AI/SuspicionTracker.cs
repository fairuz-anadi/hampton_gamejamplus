using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Remembers who looked guilty: who was near a station when it broke, and who reported done on a red station.
/// Bots use it to vote and to accuse; the muster vote UI can list the clues. Owner: Riham.
/// It sees what the crew could see, not the truth, so a framed player looks guilty too.
/// </summary>
public class SuspicionTracker : MonoBehaviour
{
    public static SuspicionTracker Instance { get; private set; }

    [SerializeField] float nearRadius = 4f;
    [SerializeField] float nearBrokenWeight = 2f;
    [SerializeField] float fakeReportWeight = 1.5f;
    [Tooltip("Reported done while standing far from the station. Odd, so it counts for less.")]
    [SerializeField] float farReportWeight = 0.5f;
    [SerializeField] float farDistance = 8f;

    public struct Clue
    {
        public float time;
        public int seat;
        public float weight;
        public string text;
    }

    readonly List<Clue> clues = new List<Clue>(32);
    readonly float[] scores = new float[CrewRoster.SeatCount + 1];

    [Tooltip("Suspicion fades: a score halves over this many seconds, so old clues matter less than fresh ones.")]
    [SerializeField] float halfLife = 75f;

    public IReadOnlyList<Clue> Clues => clues;

    public static event System.Action<Clue> ClueAdded;

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
        GameEvents.StationBroken += OnStationBroken;
        GameEvents.OrderResolved += OnOrderResolved;
        GameEvents.StateChanged += OnStateChanged;
    }

    void OnDisable()
    {
        GameEvents.StationBroken -= OnStationBroken;
        GameEvents.OrderResolved -= OnOrderResolved;
        GameEvents.StateChanged -= OnStateChanged;
    }

    void Update()
    {
        float keep = Mathf.Pow(0.5f, Time.deltaTime / halfLife);
        for (int i = 0; i < scores.Length; i++) scores[i] *= keep;
    }

    void OnStateChanged(GameState s)
    {
        if (s != GameState.Part1) return;
        clues.Clear();
        System.Array.Clear(scores, 0, scores.Length);
    }

    void OnStationBroken(StationType type, ICrewActor by)
    {
        var st = StationRegistry.Get(type);
        if (st == null) return;
        string room = Rooms.At(st.Position);
        bool useRoom = room != "" && room != "Deck";
        foreach (var c in CrewRegistry.All)
        {
            bool near = (useRoom && Rooms.At(c.Position) == room) ||
                        BotMover.FlatDistance(c.Position, st.Position) <= nearRadius;
            if (near) Add(c.Seat, nearBrokenWeight, $"{CrewRegistry.SeatName(c.Seat)} was near the {type} when it broke");
        }
    }

    void OnOrderResolved(ActiveOrder o, Choice choice)
    {
        if (choice != Choice.Fake || o.Assignee == null) return;
        var st = StationRegistry.Get(o.Data.CheckStation);
        float dist = st != null ? BotMover.FlatDistance(o.Assignee.Position, st.Position) : 0f;
        string seat = CrewRegistry.SeatName(o.Assignee.Seat);
        if (dist > farDistance)
            Add(o.Assignee.Seat, farReportWeight, $"{seat} reported the {o.Data.CheckStation} done from across the ship");
        else
            Add(o.Assignee.Seat, fakeReportWeight, $"{seat} reported done, but the {o.Data.CheckStation} light was red");
    }

    public void Add(int seat, float weight, string text)
    {
        if (seat <= 0 || seat >= scores.Length) return;
        scores[seat] += weight;
        var clue = new Clue { time = Time.time, seat = seat, weight = weight, text = text };
        clues.Add(clue);
        ClueAdded?.Invoke(clue);
    }

    public float Score(int seat) => seat > 0 && seat < scores.Length ? scores[seat] : 0f;

    /// <summary>
    /// The crewmate a bot would point at. Randomness keeps bots from being perfect detectives.
    /// Returns 0 if nobody looks suspicious at all.
    /// </summary>
    public int MostSuspicious(int excludeSeat, float randomness = 1f)
    {
        int best = 0;
        float bestScore = 0.01f;
        for (int seat = 1; seat < scores.Length; seat++)
        {
            if (seat == excludeSeat || CrewRegistry.BySeat(seat) == null) continue;
            float s = scores[seat] + Random.Range(0f, randomness);
            if (s > bestScore) { bestScore = s; best = seat; }
        }
        return best;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => ClueAdded = null;
}
