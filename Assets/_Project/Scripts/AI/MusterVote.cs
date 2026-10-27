using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The muster vote. Owner: Riham. First thing on the cut list, so nothing else depends on it.
///
///   Calling:  a human presses Vote (F / RB) at the MusterBell during Part 2 or 3. Bots call one too when someone
///             looks clearly guilty to them (SuspicionTracker), so solo players see votes happen.
///   Voting:   15 s. Humans press Vote to cycle their pick through the other seats. Bots vote for whoever looks
///             most suspicious to them, with some randomness. Votes are shown as they come in.
///   Result:   most votes, the caller's pick on a tie. That seat is locked out of the stations for 60 s.
///             Caught the Stoker: sabotage stops for 60 s, Crew +10. Wrong: Crew -15 and a worker short.
///             90 s cooldown between votes. Numbers live in MeterRules.
/// </summary>
public class MusterVote : MonoBehaviour
{
    public static MusterVote Instance { get; private set; }

    /// <summary>Simple IMGUI vote panel until the real one exists.</summary>
    public static bool DrawFallbackPanel = true;

    public static event System.Action<int> VoteStarted;            // caller seat
    public static event System.Action<int, int> VoteCast;          // voter seat, target seat
    public static event System.Action<int, bool> VoteEnded;        // locked-out seat (0 = nobody), was it the Stoker

    /// <summary>One line per vote for the results screen.</summary>
    public static readonly List<string> History = new List<string>(4);

    [SerializeField] float bellRange = 2.5f;
    [Tooltip("Bots only call a vote when someone's suspicion score is at least this.")]
    [SerializeField] float botCallScore = 5f;
    [SerializeField, Range(0f, 1f)] float botCallChance = 0.15f;
    [SerializeField] float botCallCheckEvery = 6f;
    [SerializeField] Vector2 botVoteDelay = new Vector2(2f, 9f);
    [SerializeField] float botVoteRandomness = 3f;
    [SerializeField] float resultBannerSeconds = 5f;

    public bool IsRunning { get; private set; }
    public float SecondsLeft => IsRunning ? Mathf.Max(0f, endsAt - Time.time) : 0f;
    public int CallerSeat { get; private set; }
    /// <summary>Voter seat -> picked seat. Only seats that have voted.</summary>
    public IReadOnlyDictionary<int, int> Votes => votes;

    readonly Dictionary<int, int> votes = new Dictionary<int, int>();
    readonly Dictionary<int, float> botVoteAt = new Dictionary<int, float>();
    float endsAt;
    float nextAllowedAt;
    float nextBotCheck;
    Transform bell;
    string banner;
    float bannerUntil;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable() => GameEvents.StateChanged += OnStateChanged;
    void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

    void Start()
    {
        var go = GameObject.Find("MusterBell");
        bell = go != null ? go.transform : null;
    }

    void OnStateChanged(GameState s)
    {
        if (s == GameState.Part1)
        {
            History.Clear();
            IsRunning = false;
            votes.Clear();
            nextAllowedAt = 0f;
        }
        if (s >= GameState.Iceberg && IsRunning)
        {
            IsRunning = false;
            votes.Clear();
        }
    }

    static bool VotingAllowed
    {
        get
        {
            var gm = GameManager.Instance;
            return gm != null && (gm.State == GameState.Part2 || gm.State == GameState.Part3);
        }
    }

    // ---------- Main loop ----------

    void Update()
    {
        ReadHumanInput();

        if (IsRunning)
        {
            BotsVote();
            if (Time.time >= endsAt) Finish();
            return;
        }

        if (Time.time >= nextBotCheck)
        {
            nextBotCheck = Time.time + botCallCheckEvery;
            MaybeBotCalls();
        }
    }

    void ReadHumanInput()
    {
        foreach (var pi in PlayerInput.all)
        {
            var action = pi.actions != null ? pi.actions.FindAction("Vote") : null;
            if (action == null || !action.WasPressedThisFrame()) continue;
            var actor = pi.GetComponent<ICrewActor>();
            if (actor == null) continue;

            if (IsRunning) CyclePick(actor.Seat);
            else if (AtBell(actor) && !actor.IsLockedOut) TryCall(actor.Seat);
        }
    }

    bool AtBell(ICrewActor actor) =>
        bell == null || BotMover.FlatDistance(actor.Position, bell.position) <= bellRange;

    /// <summary>Starts a vote if it's allowed right now. Returns true if it started.</summary>
    public bool TryCall(int callerSeat)
    {
        if (IsRunning || !VotingAllowed || Time.time < nextAllowedAt) return false;

        IsRunning = true;
        CallerSeat = callerSeat;
        endsAt = Time.time + MeterRules.VoteSeconds;
        votes.Clear();
        botVoteAt.Clear();
        foreach (var c in CrewRegistry.All)
            if (c.IsBot) botVoteAt[c.Seat] = Time.time + Random.Range(botVoteDelay.x, botVoteDelay.y);

        CaptainDirector.Instance?.SayStory(Fill(Pick(CaptainScript.VoteCalled), callerSeat));
        VoteStarted?.Invoke(callerSeat);
        return true;
    }

    void CyclePick(int voter)
    {
        int current = votes.TryGetValue(voter, out var v) ? v : voter;
        for (int step = 1; step <= CrewRoster.SeatCount; step++)
        {
            int seat = (current + step - 1) % CrewRoster.SeatCount + 1;
            if (seat == voter || CrewRegistry.BySeat(seat) == null) continue;
            Cast(voter, seat);
            return;
        }
    }

    void Cast(int voter, int target)
    {
        votes[voter] = target;
        VoteCast?.Invoke(voter, target);
    }

    void BotsVote()
    {
        var tracker = SuspicionTracker.Instance;
        foreach (var c in CrewRegistry.All)
        {
            if (!c.IsBot || votes.ContainsKey(c.Seat)) continue;
            if (!botVoteAt.TryGetValue(c.Seat, out var at) || Time.time < at) continue;
            int pick = tracker != null ? tracker.MostSuspicious(c.Seat, botVoteRandomness) : 0;
            if (pick == 0) { botVoteAt.Remove(c.Seat); continue; } // nobody looks guilty: abstain
            Cast(c.Seat, pick);
        }
    }

    /// <summary>An honest bot rings the bell when one crewmate looks clearly guilty.</summary>
    void MaybeBotCalls()
    {
        if (!VotingAllowed || Time.time < nextAllowedAt) return;
        var tracker = SuspicionTracker.Instance;
        if (tracker == null) return;
        int suspect = tracker.MostSuspicious(0, 0f);
        if (suspect == 0 || tracker.Score(suspect) < botCallScore || Random.value > botCallChance) return;

        foreach (var c in CrewRegistry.All)
            if (c.IsBot && !c.IsStoker && c.Seat != suspect && !c.IsLockedOut)
            {
                TryCall(c.Seat);
                return;
            }
    }

    // ---------- Result ----------

    void Finish()
    {
        IsRunning = false;
        nextAllowedAt = Time.time + MeterRules.VoteCooldownSeconds;

        var tally = new Dictionary<int, int>();
        foreach (var v in votes.Values) tally[v] = tally.TryGetValue(v, out var n) ? n + 1 : 1;

        int top = 0, topVotes = 0;
        bool tie = false;
        foreach (var kv in tally)
        {
            if (kv.Value > topVotes) { top = kv.Key; topVotes = kv.Value; tie = false; }
            else if (kv.Value == topVotes) tie = true;
        }
        if (tie)
        {
            // Tie: the caller's pick wins if it's one of the tied seats, otherwise nobody goes.
            top = votes.TryGetValue(CallerSeat, out var callerPick) && tally.TryGetValue(callerPick, out var cv) && cv == topVotes
                ? callerPick : 0;
        }

        var target = top > 0 ? CrewRegistry.BySeat(top) : null;
        if (target == null)
        {
            CaptainDirector.Instance?.SayStory(CaptainScript.VoteNobody);
            History.Add($"{CrewRegistry.SeatName(CallerSeat)} rang the bell. Nobody was locked out.");
            ShowBanner("No decision. Back to work.");
            VoteEnded?.Invoke(0, false);
            votes.Clear();
            return;
        }

        bool caught = target.IsStoker;
        LockOut(target, MeterRules.LockoutSeconds);
        MeterSystem.Instance?.Apply(caught ? MeterRules.VoteCaughtStoker : MeterRules.VoteWrong);
        if (caught) (target as BotBrain)?.Stoker?.PauseFor(MeterRules.LockoutSeconds);

        string name = CrewRegistry.SeatName(top);
        CaptainDirector.Instance?.SayStory(Fill(Pick(caught ? CaptainScript.VoteRight : CaptainScript.VoteWrong), top));
        History.Add($"{CrewRegistry.SeatName(CallerSeat)} rang the bell. {name} was locked out with {topVotes} vote{(topVotes == 1 ? "" : "s")}" +
                    (caught ? ", and they were the Stoker." : ", but they were innocent."));
        ShowBanner($"{name} is locked out for {MeterRules.LockoutSeconds:0} s. " + (caught ? "The crew feels surer of each other." : "Crew Trust falls."));
        VoteEnded?.Invoke(top, caught);
        votes.Clear();
    }

    void LockOut(ICrewActor actor, float seconds)
    {
        switch (actor)
        {
            case BotBrain bot: bot.LockOut(seconds); break;
            case PlayerController pc: StartCoroutine(LockHuman(pc.SetLockedOut, seconds)); break;
            case DebugSecondPlayer p2: StartCoroutine(LockHuman(p2.SetLockedOut, seconds)); break;
        }
    }

    static IEnumerator LockHuman(System.Action<bool> setLocked, float seconds)
    {
        setLocked(true);
        yield return new WaitForSeconds(seconds);
        setLocked(false);
    }

    void ShowBanner(string text)
    {
        banner = text;
        bannerUntil = Time.time + resultBannerSeconds;
    }

    static string Fill(string line, int seat) => line.Replace("{seat}", CrewRegistry.SeatName(seat));
    static string Pick(string[] lines) => lines[Random.Range(0, lines.Length)];

    // ---------- Fallback panel ----------

    GUIStyle panel, row, title;

    void OnGUI()
    {
        if (!DrawFallbackPanel) return;
        bool showBanner = Time.time < bannerUntil;
        if (!IsRunning && !showBanner) return;

        float s = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s;

        panel ??= new GUIStyle(GUI.skin.box) { padding = new RectOffset(20, 20, 14, 14) };
        title ??= new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        row ??= new GUIStyle(GUI.skin.label) { fontSize = 22, richText = true };

        if (!IsRunning)
        {
            GUI.Box(new Rect(w * 0.5f - 400f, 120f, 800f, 70f), "", panel);
            GUI.Label(new Rect(w * 0.5f - 400f, 120f, 800f, 70f), banner, title);
            return;
        }

        var rect = new Rect(w * 0.5f - 330f, 110f, 660f, 330f);
        GUI.Box(rect, "", panel);
        GUILayout.BeginArea(new Rect(rect.x + 20f, rect.y + 12f, rect.width - 40f, rect.height - 24f));
        GUILayout.Label($"MUSTER VOTE   {SecondsLeft:0}s", title);
        GUILayout.Label($"Called by {CrewRegistry.SeatName(CallerSeat)}. Press Vote (F / RB) to change your pick.", row);
        GUILayout.Space(8f);
        for (int seat = 1; seat <= CrewRoster.SeatCount; seat++)
        {
            var c = CrewRegistry.BySeat(seat);
            if (c == null) continue;
            int got = 0;
            foreach (var v in votes.Values) if (v == seat) got++;
            string hex = ColorUtility.ToHtmlStringRGB(CrewRoster.SeatColors[seat]);
            string pick = votes.TryGetValue(seat, out var p) ? $"votes {CrewRegistry.SeatName(p)}" : "thinking...";
            GUILayout.Label($"<color=#{hex}>■</color> {CrewRegistry.SeatName(seat)}{(c.IsBot ? "" : " (you)")}   {new string('●', got)}    <i>{pick}</i>", row);
        }
        GUILayout.EndArea();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        VoteStarted = null;
        VoteCast = null;
        VoteEnded = null;
        DrawFallbackPanel = true;
        History.Clear();
    }
}
