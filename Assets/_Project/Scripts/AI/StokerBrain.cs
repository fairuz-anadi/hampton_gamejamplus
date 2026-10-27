using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The imposter. Lives on one bot, picked at random each run by CrewRoster. Owner: Riham.
///
/// Rules (numbers live in MeterRules):
///   - Only in Part 2 and 3. A 40 s cooldown (30 s while the Captain is suspicious), plus a little jitter.
///   - Only acts when no human is in the same room. 3 sabotages per run.
///   - A burst of speaker static 2.5 s before each one (StaticBurst), the learnable clue.
///   Jam a station:        Station.Break(); it stays broken until someone repairs it. Safety -6.
///   Hide an ice warning:  IceWarnings.HideNext(); the warning never arrives (IceWarnings charges Safety -8 then).
///   False report:         reports someone else's red station as done, so it counts as their fake. Crew -6.
/// </summary>
public class StokerBrain : MonoBehaviour
{
    [SerializeField] Vector2 firstDelayInPart2 = new Vector2(12f, 25f);
    [SerializeField] Vector2 cooldownJitter = new Vector2(0f, 8f);
    [SerializeField] float giveUpAfter = 20f;

    public enum SabotageKind { None, Jam, HideWarning, FalseReport }

    /// <summary>Speaker static just before a sabotage. Samprity: play the static sound here.</summary>
    public static event System.Action<StokerBrain> StaticBurst;
    /// <summary>A sabotage happened. The int is the station type, warning number or framed seat.</summary>
    public static event System.Action<StokerBrain, SabotageKind, int> Sabotaged;

    /// <summary>Seat that's being framed right now, while OrderDirector resolves the false report. 0 otherwise.</summary>
    public static int FramingSeat { get; private set; }

    /// <summary>What the Stoker did this run, in plain words, for the results screen.</summary>
    public static readonly List<string> Log = new List<string>(8);

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
        FramingSeat = 0;
    }

    /// <summary>A correct muster vote stops sabotage for a while.</summary>
    public void PauseFor(float seconds)
    {
        pausedUntil = Time.time + seconds;
        current = SabotageKind.None;
        burstAt = -1f;
    }

    // ---------- Called by BotBrain when the bot has no order of its own ----------

    /// <summary>Returns true while the Stoker is using the bot this tick.</summary>
    public bool TickSabotage()
    {
        if (!MeterRules.StokerActive || Time.time < pausedUntil || bot.IsLockedOut)
        {
            current = SabotageKind.None;
            burstAt = -1f;
            if (!MeterRules.StokerActive) nextActionAt = -1f;
            return false;
        }
        if (uses >= MeterRules.StokerSabotagesPerRun) return false;

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
        if (current != SabotageKind.FalseReport && !bot.ReachStation(target)) return true;

        if (Rooms.HumanNear(bot.Position))
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
        if (Time.time - burstAt < MeterRules.StokerStaticWarningSeconds) return true;

        Execute();
        return false;
    }

    bool Choose()
    {
        var options = new List<(SabotageKind kind, StationType station, float weight)>(6);

        foreach (var st in StationRegistry.All)
        {
            if (st.IsBroken || !(st is Station)) continue;
            if (Rooms.HumanNear(st.Position)) continue;
            options.Add((SabotageKind.Jam, st.Type, 1f));
        }

        if (WarningLeftToHide())
            options.Add((SabotageKind.HideWarning, StationType.Wireless, 1.2f));

        if (FindVictim() != null)
            options.Add((SabotageKind.FalseReport, StationType.Boiler, 1.2f));

        if (options.Count == 0) return false;

        float total = 0f;
        foreach (var o in options) total += o.weight;
        float pick = Random.value * total;
        var chosen = options[options.Count - 1];
        foreach (var o in options)
        {
            pick -= o.weight;
            if (pick <= 0f) { chosen = o; break; }
        }
        current = chosen.kind;
        target = chosen.station;
        return true;
    }

    static bool WarningLeftToHide()
    {
        var iw = IceWarnings.Instance;
        if (iw == null) return false;
        for (int n = 1; n <= iw.Count; n++)
            if (iw.StatusOf(n) == IceWarnings.Status.Pending) return true;
        return false;
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
                if (!(StationRegistry.Get(target) is Station st) || st.IsBroken) { Abort(3f); return; }
                bot.StopWorking();
                st.Break();
                meters?.Apply(MeterRules.SabotageJamStation);
                Log.Add($"Jammed the {target}");
                break;

            case SabotageKind.HideWarning:
                detail = IceWarnings.Instance != null ? IceWarnings.Instance.HideNext() : 0;
                if (detail == 0) { Abort(3f); return; }
                Log.Add($"Pocketed ice warning MSG {detail} before it reached the desk");
                break;

            case SabotageKind.FalseReport:
                var victim = FindVictim();
                if (victim == null) { Abort(3f); return; }
                detail = victim.Assignee.Seat;
                FramingSeat = detail;
                try { OrderDirector.Instance.Report(victim.Assignee); }
                finally { FramingSeat = 0; }
                meters?.Apply(MeterRules.SabotageFalseReport);
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
