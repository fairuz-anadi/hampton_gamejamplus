using UnityEngine;

/// <summary>
/// Turns the iceberg impact and the meters into one of the three endings. Owner: Anadi.
///   Rare "what if": head-on and Safety >= 60.
///   Many saved:     Safety >= 35, or head-on. Drops to Captain wins while the lifeboat store is locked.
///   Captain wins:   everything else.
/// </summary>
public static class EndingEvaluator
{
    public const float HeadOnMaxAngle = 10f;
    public const float GlancingMaxAngle = 45f;
    public const int WhatIfMinSafety = 60;
    public const int ManySavedMinSafety = 35;

    const string FoundKey = "Port.EndingsFound";

    /// <summary>Degrees between the ship's heading and the iceberg, 0 = straight on. No angle reported counts as glancing.</summary>
    public static ImpactKind Classify(float impactAngle)
    {
        if (float.IsNaN(impactAngle)) return ImpactKind.Glancing;
        float a = Mathf.Abs(Mathf.DeltaAngle(0f, impactAngle));
        if (a > 90f) a = 180f - a;
        if (a <= HeadOnMaxAngle) return ImpactKind.HeadOn;
        return a <= GlancingMaxAngle ? ImpactKind.Glancing : ImpactKind.Scrape;
    }

    /// <summary>Ship Safety lost at impact, applied before the ending check.</summary>
    public static int ImpactSafetyCost(ImpactKind impact) => impact switch
    {
        ImpactKind.Glancing => -5,
        ImpactKind.Scrape => -10,
        _ => 0,
    };

    public static Ending Decide(int shipSafety, ImpactKind impact, bool lifeboatsLocked)
    {
        bool headOn = impact == ImpactKind.HeadOn;
        if (headOn && shipSafety >= WhatIfMinSafety) return Ending.WhatIf;
        if ((shipSafety >= ManySavedMinSafety || headOn) && !lifeboatsLocked) return Ending.ManySaved;
        return Ending.CaptainWins;
    }

    /// <summary>Called by GameManager at impact: applies the impact cost, freezes the meters and decides the ending.</summary>
    public static Ending Resolve(float impactAngle)
    {
        var impact = Classify(impactAngle);
        RunRecord.ImpactAngle = impactAngle;
        RunRecord.Impact = impact;

        var meters = MeterSystem.Instance;
        if (meters != null)
        {
            meters.Apply(new MeterDelta(0, ImpactSafetyCost(impact), 0));
            meters.SnapshotToRecord();
        }

        RunRecord.Ending = Decide(RunRecord.FinalShipSafety, impact, RunRecord.LifeboatStoreLocked);
        RunRecord.HasEnding = true;
        MarkFound(RunRecord.Ending);
        return RunRecord.Ending;
    }

    // ---------- "Endings found: 1/3", saved between sessions ----------

    public static int EndingCount => System.Enum.GetValues(typeof(Ending)).Length;

    public static bool IsFound(Ending e) => (PlayerPrefs.GetInt(FoundKey, 0) & (1 << (int)e)) != 0;

    public static int FoundCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < EndingCount; i++) if (IsFound((Ending)i)) n++;
            return n;
        }
    }

    static void MarkFound(Ending e)
    {
        PlayerPrefs.SetInt(FoundKey, PlayerPrefs.GetInt(FoundKey, 0) | (1 << (int)e));
        PlayerPrefs.Save();
    }
}
