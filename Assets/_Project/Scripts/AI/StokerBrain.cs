using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The imposter. Lives on one bot, picked at random each run by CrewRoster. Owner: Riham.
///
/// Rules (from the plan):
///   - Does nothing in Part 1. A 40 s cooldown (30 s while the Captain is suspicious), plus a little jitter.
///   - Only acts when no human is in the same room. 3 sabotages per run.
///   - A burst of speaker static 2.5 s before each one (StaticBurst), the learnable clue.
///   Jam a station:        it breaks until someone repairs it. Safety -6.
///   Hide an ice warning:  the Wireless desk asks ConsumeHiddenWarning() when a warning is due. Safety -8 then.
///   False report:         reports someone else's red station as done, so it counts as their fake. Crew -6.
/// </summary>
public class StokerBrain : MonoBehaviour
{
    [SerializeField, Tooltip("0 = no limit")] int maxSabotages = 3;
    [SerializeField] Vector2 firstDelayInPart2 = new Vector2(12f, 25f);
    [SerializeField] Vector2 cooldownJitter = new Vector2(0f, 8f);
    [SerializeField] float staticLead = 2.5f;
    [SerializeField] float giveUpAfter = 20f;

    [SerializeField] MeterDelta jamCost = new MeterDelta(0, -6, 0);
    [SerializeField] MeterDelta falseReportCost = new MeterDelta(0, 0, -6);

    public enum SabotageKind { None, Jam, HideWarning, FalseReport }

    /// <summary>Speaker static just before a sabotage. Samprity: play the static sound here.</summary>
    public static event System.Action<StokerBrain> StaticBurst;
    /// <summary>A sabotage happened. Riham's SuspicionTracker and the results screen read this.</summary>
    public static event System.Action<StokerBrain, SabotageKind, int> Sabotaged; // int = station type or victim seat

    /// <summary>Seat that's being framed right now, while OrderDirector resolves the false report. 0 otherwise.</summary>
    public static int FramingSeat { get; private set; }

    /// <summary>What the Stoker did this run, in plain words, for the results screen.</summary>
    public static readonly List<string> Log = new List<string>(8);

    static readonly MeterDelta HiddenWarningCost = new MeterDelta(0, -8, 0);

    static int hiddenPending;
    static int warningsSeen;

    BotBrain bot;
    int uses;
    float nextActionAt = -1f;
    float pausedUntil;

    SabotageKind current;
    StationType target;
    float startedAt;
    float burstAt = -1f;

    /// <summary>True while it's in the middle of a sabotage, so it won't take help jobs.</summary>
    public bool IsBusy => current != SabotageKind.None;
    public int Uses => uses;

    void Awake() => bot = GetComponent<BotBrain>();

    void OnEnable()
    {
        GameEvents.IceWarningArrived += OnWarningSeen;
        GameEvents.IceWarningLost += OnWarningSeen;
    }

    void OnDisable()
    {
        GameEvents.IceWarningArrived -= OnWarningSeen;
        GameEvents.IceWarningLost -= OnWarningSeen;
    }

    void OnWarningSeen(int n) => warningsSeen = Mathf.Max(warningsSeen, n);

    public void ResetForRun()
    {
        uses = 0;
        nextActionAt = -1f;
        pausedUntil = 0f;
        current = SabotageKind.None;
        burstAt = -1f;
    }

    public static void ResetRun()
    {
        Log.Clear();
        hiddenPending = 0;
        warningsSeen = 0;
        FramingSeat = 0;
    }

    /// <summary>A correct muster vote stops sabotage for a while.</summary>
    public void PauseFor(float seconds)
    {
        pausedUntil = Time.time + seconds;
        current = SabotageKind.None;
        burstAt = -1f;
    }

    /// <summary>
    /// Rabib's Wireless desk calls this when ice warning n is about to arrive.
    /// Returns true if the Stoker hid it: don't show it, the numbering will have a gap.
    /// </summary>
    public static bool ConsumeHiddenWarning(int number)
    {
        warningsSeen = Mathf.Max(warningsSeen, number);
        if (hiddenPending <= 0) return false;
        hiddenPending--;
        MeterSystem.Instance?.Apply(HiddenWarningCost);
        Log.Add($"Hid ice warning MSG {number}");
        return true;
    }

    // ---------- Called by BotBrain when the bot has no order of its own ----------

    /// <summary>Returns true while the Stoker is using the bot this tick.</summary>
    public bool TickSabotage()
    {
        var gm = GameManager.Instance;
        int part = gm != null ? gm.Part : 2;
        if (part < 2 || part > 3 || Time.time < pausedUntil || bot.IsLockedOut)
        {
            current = SabotageKind.None;
            burstAt = -1f;
            if (part < 2) nextActionAt = -1f;
            return false;
        }
        if (maxSabotages > 0 && uses >= maxSabotages) return false;

        if (nextActionAt < 0f) nextActionAt = Time.time + Random.Range(firstDelayInPart2.x, firstDelayInPart2.y);
        if (current == SabotageKind.None)
        {
            if (Time.time < nextActionAt) return false;
            if (!Choose()) { nextActionAt = Time.time + 5f; return false; }
            startedAt = Time.time;
            burstAt = -1f;
        }

        if (Time.time - startedAt > giveUpAfter) { Abort(6f); return false; }

        // Walk to the scene of the crime (a false report can be done from anywhere).
        string room = bot.RoomId;
        if (current != SabotageKind.FalseReport)
        {
            if (!bot.ReachStation(target)) return true;
            room = RoomZone.RoomOf(target);
        }

        if (room == "" ? HumanClose() : RoomZone.HumanIn(room))
        {
            Abort(6f);
            return false;
        }

        if (burstAt < 0f)
        {
            burstAt = Time.time;
            StaticBurst?.Invoke(this);
            return true;
        }
        if (Time.time - burstAt < staticLead) return true;

        Execute();
        return false;
    }

    /// <summary>Without room zones, treat any human within 6 m as "in the same room".</summary>
    bool HumanClose()
    {
        foreach (var c in CrewRegistry.All)
            if (!c.IsBot && BotMover.FlatDistance(c.Position, bot.Position) < 6f) return true;
        return false;
    }

    bool Choose()
    {
        var options = new List<(SabotageKind action, StationType station, float weight)>(6);

        bool anyStation = false;
        foreach (var st in StationRegistry.All)
        {
            anyStation = true;
            if (st.IsBroken) continue;
            if (RoomZone.HumanIn(RoomZone.RoomOf(st.Type))) continue;
            options.Add((SabotageKind.Jam, st.Type, 1f));
        }
        if (!anyStation)
            options.Add((SabotageKind.Jam, (StationType)Random.Range(0, 4), 1f)); // test scene with no stations

        var gm = GameManager.Instance;
        bool part2 = gm == null || gm.Part == 2;
        if (part2 && hiddenPending == 0 && warningsSeen < 3)
            options.Add((SabotageKind.HideWarning, StationType.Wireless, 1.2f));

        if (FindVictim() != null)
            options.Add((SabotageKind.FalseReport, StationType.Boiler, 1.2f));

        if (options.Count == 0) return false;

        float total = 0f;
        foreach (var o in options) total += o.weight;
        float pick = Random.value * total;
        foreach (var o in options)
        {
            pick -= o.weight;
            if (pick > 0f) continue;
            current = o.action;
            target = o.station;
            return true;
        }
        current = options[0].action;
        target = options[0].station;
        return true;
    }

    /// <summary>Someone else with an active order whose station is still red. Prefers whoever is far from it.</summary>
    ActiveOrder FindVictim()
    {
        var od = OrderDirector.Instance;
        if (od == null) return null;
        ActiveOrder best = null;
        float bestDist = -1f;
        foreach (var o in od.Active)
        {
            if (o.Assignee == null || o.Assignee == (ICrewActor)bot || o.IsResolved) continue;
            if (od.IsStationDone(o)) continue;
            var st = StationRegistry.Get(o.Data.CheckStation);
            float d = st != null ? BotMover.FlatDistance(o.Assignee.Position, st.Position) : 0f;
            if (d > bestDist) { bestDist = d; best = o; }
        }
        return best;
    }

    void Execute()
    {
        var did = current;
        int detail = (int)target;
        var meters = MeterSystem.Instance;

        switch (current)
        {
            case SabotageKind.Jam:
                if (StationRegistry.Get(target) is ICrewWorkStation ws) ws.Sabotage(bot);
                else GameEvents.RaiseStationBroken(target, bot);
                meters?.Apply(jamCost);
                Log.Add($"Jammed the {target}");
                break;

            case SabotageKind.HideWarning:
                hiddenPending++;
                Log.Add("Pocketed the next ice warning");
                break;

            case SabotageKind.FalseReport:
                var victim = FindVictim();
                if (victim == null) { Abort(3f); return; }
                detail = victim.Assignee.Seat;
                FramingSeat = detail;
                try { OrderDirector.Instance.Report(victim.Assignee); }
                finally { FramingSeat = 0; }
                meters?.Apply(falseReportCost);
                Log.Add($"Reported {CrewRegistry.SeatName(detail)}'s order done behind their back");
                break;
        }

        uses++;
        current = SabotageKind.None;
        burstAt = -1f;
        nextActionAt = Time.time + MeterRules.StokerCooldown + Random.Range(cooldownJitter.x, cooldownJitter.y);
        Sabotaged?.Invoke(this, did, detail);
    }

    void Abort(float retryIn)
    {
        current = SabotageKind.None;
        burstAt = -1f;
        nextActionAt = Time.time + retryIn;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        StaticBurst = null;
        Sabotaged = null;
        ResetRun();
    }
}
