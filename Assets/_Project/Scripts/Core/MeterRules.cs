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

    static int Captain => MeterSystem.Instance != null ? MeterSystem.Instance.CaptainTrust : 50;
    static int Safety => MeterSystem.Instance != null ? MeterSystem.Instance.ShipSafety : 80;
    static int Crew => MeterSystem.Instance != null ? MeterSystem.Instance.CrewTrust : 60;

    /// <summary>At 75+ the Captain shares "private" lines he shouldn't know.</summary>
    public static bool CaptainSharesPrivateLines => Captain >= CaptainPrivateLinesAt;

    /// <summary>At 25 or below the Captain gets suspicious ("I'm watching you, Number Two").</summary>
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
