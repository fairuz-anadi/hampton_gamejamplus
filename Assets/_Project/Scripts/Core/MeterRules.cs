using UnityEngine;

/// <summary>
/// What the meters do to the rest of the game. Owner: Anadi. Read these instead of hard-coding thresholds.
///   Riham:    StokerCooldown, BotHelpDelay, BotIgnoresHelpChance, BotRefusesOffOrderChance, CaptainSharesPrivateLines, CaptainSuspicious
///   Samprity: ShipLeaking (small leaks on the HUD, strained pumps)
/// Without a MeterSystem in the scene, the start values are used.
/// </summary>
public static class MeterRules
{
    public const int CaptainPrivateLinesAt = 75;
    public const int CaptainSuspiciousAt = 25;
    public const int SafetyLeaksBelow = 50;
    public const int CrewEagerAt = 70;
    public const int CrewReluctantAt = 30;

    public const float StokerCooldownNormal = 40f;
    public const float StokerCooldownSuspicious = 30f;
    public const float SlowestHelpDelay = 6f;
    public const float ReluctantIgnoreChance = 0.25f;

    // ---------- Plan numbers for systems built by others, so they're tuned in one place ----------

    // Ice warnings (applied by IceWarnings)
    public static readonly MeterDelta IceWarningDelivered = new MeterDelta(0, 5, 0);
    public static readonly MeterDelta IceWarningLost = new MeterDelta(0, -5, 0);

    // The Stoker (Riham). Inactive in Part 1, one sabotage per cooldown, only when no human is in the room.
    public static readonly MeterDelta SabotageJamStation = new MeterDelta(0, -6, 0);
    public static readonly MeterDelta SabotageHideIceWarning = new MeterDelta(0, -8, 0); // IceWarnings applies this one
    public static readonly MeterDelta SabotageFalseReport = new MeterDelta(0, 0, -6);
    public const int StokerFirstPart = 2;
    public const int StokerSabotagesPerRun = 3;
    public const float StokerStaticWarningSeconds = 2.5f; // speaker static before each sabotage
    public const float RepairSeconds = 3f;

    // Muster vote (Riham), first thing to cut
    public static readonly MeterDelta VoteCaughtStoker = new MeterDelta(0, 0, 10);
    public static readonly MeterDelta VoteWrong = new MeterDelta(0, 0, -15);
    public const float VoteSeconds = 15f;
    public const float LockoutSeconds = 60f;
    public const float VoteCooldownSeconds = 90f;

    /// <summary>True while the Stoker may sabotage: Part 2 and Part 3.</summary>
    public static bool StokerActive
    {
        get
        {
            int part = GameManager.Instance != null ? GameManager.Instance.Part : 0;
            return part >= StokerFirstPart && part <= 3;
        }
    }

    static int Captain => MeterSystem.Instance != null ? MeterSystem.Instance.CaptainTrust : 50;
    static int Safety => MeterSystem.Instance != null ? MeterSystem.Instance.ShipSafety : 80;
    static int Crew => MeterSystem.Instance != null ? MeterSystem.Instance.CrewTrust : 60;

    /// <summary>At 75+ the Captain shares "private" lines he shouldn't know.</summary>
    public static bool CaptainSharesPrivateLines => Captain >= CaptainPrivateLinesAt;

    /// <summary>At 25 or below the Captain gets suspicious ("I'm watching you, Ezra").</summary>
    public static bool CaptainSuspicious => Captain <= CaptainSuspiciousAt;

    /// <summary>Below 50 the HUD shows small leaks and the pumps sound strained.</summary>
    public static bool ShipLeaking => Safety < SafetyLeaksBelow;

    /// <summary>Seconds between Stoker sabotages: 40, or 30 while the Captain is suspicious.</summary>
    public static float StokerCooldown => CaptainSuspicious ? StokerCooldownSuspicious : StokerCooldownNormal;

    /// <summary>How long an honest bot waits before answering a help call: 0 s at 70+ Crew Trust, 6 s at 30 or below.</summary>
    public static float BotHelpDelay =>
        Mathf.Lerp(SlowestHelpDelay, 0f, Mathf.InverseLerp(CrewReluctantAt, CrewEagerAt, Crew));

    /// <summary>At 30 or below, bots sometimes don't come at all.</summary>
    public static float BotIgnoresHelpChance => Crew <= CrewReluctantAt ? ReluctantIgnoreChance : 0f;

    /// <summary>Honest bots refuse "off" orders with a chance equal to Crew Trust %.</summary>
    public static float BotRefusesOffOrderChance => Crew / 100f;
}
