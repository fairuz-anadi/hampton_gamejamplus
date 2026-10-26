using System.Collections.Generic;
using UnityEngine;

/// <summary>One line on the results screen: who got which order and what it cost. This is the ChoiceLog.</summary>
public class ChoiceEntry
{
    public int orderNumber;
    public OrderData order;
    public int seat;          // 0 if nobody was assigned
    public bool byBot;
    public bool byStoker;
    public Choice choice;
    public MeterDelta delta;
    public float timeInRun;
    public bool fakeNoticed;  // set later if the Captain caught a fake
    public bool fakeWitnessed; // a crewmate in the room saw the red light

    /// <summary>"#3 Close door four · Number Two (bot) · faked it, the Captain noticed · Captain +2, Safety -4"</summary>
    public string Describe()
    {
        string task = order != null ? order.resultsLine : "?";
        string who = seat > 0 ? CrewRegistry.SeatName(seat) + (byBot ? " (bot)" : "") : "nobody";
        string what = choice switch
        {
            Choice.Obey => "obeyed",
            Choice.Fake => "faked it",
            Choice.Refuse => "refused",
            _ => "let it run out",
        };
        if (fakeNoticed) what += ", the Captain noticed";
        if (fakeWitnessed) what += ", a crewmate saw";
        return $"#{orderNumber} {task} · {who} · {what} · {delta}";
    }
}

/// <summary>
/// Everything about the current run that must survive loading the Results scene.
/// Filled in by OrderDirector, GameManager and EndingEvaluator, read by the results screen (Samprity) and the Captain (Riham).
/// </summary>
public static class RunRecord
{
    public static readonly List<ChoiceEntry> Choices = new List<ChoiceEntry>(16);

    public static int FinalCaptainTrust;
    public static int FinalShipSafety;
    public static int FinalCrewTrust;

    /// <summary>Angle between heading and iceberg at impact. NaN until the iceberg scene reports it.</summary>
    public static float ImpactAngle = float.NaN;
    public static ImpactKind Impact;
    /// <summary>Set by obeying order 9. Rabib's Door levers set it back to false when two crew reopen the store.</summary>
    public static bool LifeboatStoreLocked;
    public static int StokerSeat;
    public static float RunStartTime;

    public static int IceWarningsDelivered;
    public static int IceWarningsLost;
    public static int IceWarningsHidden;

    /// <summary>False until the iceberg has been hit and the ending decided.</summary>
    public static bool HasEnding;
    public static Ending Ending;

    public static void Clear()
    {
        Choices.Clear();
        FinalCaptainTrust = FinalShipSafety = FinalCrewTrust = 0;
        ImpactAngle = float.NaN;
        Impact = ImpactKind.HeadOn;
        LifeboatStoreLocked = false;
        StokerSeat = 0;
        RunStartTime = Time.time;
        IceWarningsDelivered = IceWarningsLost = IceWarningsHidden = 0;
        HasEnding = false;
        Ending = Ending.CaptainWins;
    }

    /// <summary>The most recent choice a given seat made, or null.</summary>
    public static ChoiceEntry LastChoiceBy(int seat)
    {
        for (int i = Choices.Count - 1; i >= 0; i--)
            if (Choices[i].seat == seat) return Choices[i];
        return null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Choices.Clear(); ImpactAngle = float.NaN; HasEnding = false; }
}
