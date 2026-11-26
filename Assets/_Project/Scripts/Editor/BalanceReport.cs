using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Balance pass helper. Owner: Anadi. Plays the 12 OrderData assets through simple strategies on paper and shows
/// the final meters and the ending for each kind of impact, so we can check every ending is reachable on purpose.
///   In the Editor:  Port > Balance Report (prints to the Console)
///   Headless:       -executeMethod BalanceReport.RunBatch [-balanceOut path.md]
/// A "rough" voyage has the Stoker jam 2 stations and hide 1 ice warning, and the other 2 warnings get lost.
/// A "clean" one delivers all 3 ice warnings. Every second fake gets noticed (Captain -10). Numbers come from MeterRules.
/// Solo voyage: every order goes to the one player, so MutinyWatch's "no" streak runs over all 12 in order (refusing
/// an off order is excused); a strategy that reaches MutinyWatch.Limit ends Overboard there, whatever the impact.
/// </summary>
public static class BalanceReport
{
    struct Strategy
    {
        public string name;
        public Func<OrderData, Choice> pick;
    }

    static readonly Strategy[] Strategies =
    {
        new Strategy { name = "Obey everything", pick = o => Choice.Obey },
        new Strategy { name = "Obey normal, refuse off", pick = o => o.IsOff ? Choice.Refuse : Choice.Obey },
        new Strategy { name = "Obey normal, fake off", pick = o => o.IsOff ? Choice.Fake : Choice.Obey },
        new Strategy { name = "Fake everything", pick = o => Choice.Fake },
        new Strategy { name = "Let every timer run out", pick = o => Choice.Expired },
        new Strategy { name = "Refuse everything", pick = o => Choice.Refuse },
    };

    static readonly ImpactKind[] Impacts = { ImpactKind.HeadOn, ImpactKind.Glancing, ImpactKind.Scrape };

    static readonly MeterDelta RoughVoyage =
        MeterRules.SabotageJamStation + MeterRules.SabotageJamStation + MeterRules.SabotageHideIceWarning
        + MeterRules.IceWarningLost + MeterRules.IceWarningLost;
    static readonly MeterDelta CleanVoyage =
        MeterRules.IceWarningDelivered + MeterRules.IceWarningDelivered + MeterRules.IceWarningDelivered;

    [MenuItem("Port/Balance Report")]
    public static void Run() => Debug.Log(Build(out _));

    public static void RunBatch()
    {
        string report = Build(out bool allReachable);
        string path = ArgValue("-balanceOut");
        if (path != null) File.WriteAllText(path, report);
        Debug.Log(report);
        EditorApplication.Exit(allReachable ? 0 : 1);
    }

    /// <summary>Returns the report as Markdown. allReachable is true when every ending shows up somewhere.</summary>
    public static string Build(out bool allReachable)
    {
        var orders = Resources.LoadAll<OrderData>("Orders").OrderBy(o => o.name).ToList();
        var reached = new HashSet<Ending>();
        var sb = new StringBuilder();
        sb.AppendLine($"# Balance report ({orders.Count} orders)");
        sb.AppendLine();
        sb.AppendLine("| Strategy | Voyage | Captain | Safety | Crew | Lifeboats | Head-on | Glancing | Scrape |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|");

        foreach (var s in Strategies)
        {
            foreach (bool rough in new[] { false, true })
            {
                Simulate(orders, s, rough, out int captain, out int safety, out int crew, out bool locked, out int overboardAt);
                sb.Append($"| {s.name} | {(rough ? "rough" : "clean")} | {captain} | {safety} | {crew} | {(locked ? "locked" : "open")} |");
                if (overboardAt > 0)
                {
                    reached.Add(Ending.Overboard);
                    foreach (var _ in Impacts) sb.Append($" Overboard (order {overboardAt}) |");
                    sb.AppendLine();
                    continue;
                }
                foreach (var impact in Impacts)
                {
                    int finalSafety = Mathf.Clamp(safety + EndingEvaluator.ImpactSafetyCost(impact), 0, 100);
                    var ending = EndingEvaluator.Decide(finalSafety, impact, locked);
                    reached.Add(ending);
                    sb.Append($" {ending} ({finalSafety}) |");
                }
                sb.AppendLine();
            }
        }

        allReachable = reached.Count == EndingEvaluator.EndingCount;
        sb.AppendLine();
        sb.AppendLine(allReachable
            ? $"All {EndingEvaluator.EndingCount} endings are reachable."
            : "Not reachable: " + string.Join(", ", Enum.GetValues(typeof(Ending)).Cast<Ending>().Where(e => !reached.Contains(e))));
        return sb.ToString();
    }

    static void Simulate(List<OrderData> orders, Strategy s, bool rough, out int captain, out int safety, out int crew, out bool locked, out int overboardAt)
    {
        captain = 50; safety = 80; crew = 60; locked = false; overboardAt = 0;
        int fakes = 0, streak = 0;
        foreach (var o in orders)
        {
            var choice = s.pick(o);
            // MutinyWatch: a "no" to an off order is excused (no change); to a normal order it counts; done resets.
            bool no = choice == Choice.Refuse || choice == Choice.Expired;
            if (!no) streak = 0;
            else if (!o.IsOff) streak++;
            Apply(o.DeltaFor(choice), ref captain, ref safety, ref crew);
            if (choice == Choice.Fake && ++fakes % 2 == 0) Apply(new MeterDelta(-10, 0, 0), ref captain, ref safety, ref crew);
            if (choice == Choice.Obey && o.locksLifeboats) locked = true;
            if (streak >= MutinyWatch.Limit) { overboardAt = orders.IndexOf(o) + 1; return; } // sorted 01_..12_

            // Part 2 events, applied once after order 11 so they land where they would in a real run.
            if (o.part == 2 && o == orders.Last(x => x.part == 2))
                Apply(rough ? RoughVoyage : CleanVoyage, ref captain, ref safety, ref crew);
        }
    }

    static void Apply(MeterDelta d, ref int captain, ref int safety, ref int crew)
    {
        captain = Mathf.Clamp(captain + d.captainTrust, 0, 100);
        safety = Mathf.Clamp(safety + d.shipSafety, 0, 100);
        crew = Mathf.Clamp(crew + d.crewTrust, 0, 100);
    }

    static string ArgValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
