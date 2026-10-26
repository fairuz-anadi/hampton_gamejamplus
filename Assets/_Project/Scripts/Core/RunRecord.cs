using System.Collections.Generic;
using UnityEngine;

/// <summary>One line on the results screen: who got which order and what it cost.</summary>
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
}

/// <summary>
/// Everything about the current run that must survive loading the Results scene.
/// Filled in by OrderDirector and GameManager, read by the results screen (Samprity) and the Captain (Riham).
/// </summary>
public static class RunRecord
{
    public static readonly List<ChoiceEntry> Choices = new List<ChoiceEntry>(16);

    public static int FinalCaptainTrust;
    public static int FinalShipSafety;
    public static int FinalCrewTrust;

    /// <summary>Angle between heading and iceberg at impact. NaN until the iceberg scene reports it.</summary>
    public static float ImpactAngle = float.NaN;
    public static bool LifeboatStoreLocked;
    public static int StokerSeat;
    public static float RunStartTime;

    public static void Clear()
    {
        Choices.Clear();
        FinalCaptainTrust = FinalShipSafety = FinalCrewTrust = 0;
        ImpactAngle = float.NaN;
        LifeboatStoreLocked = false;
        StokerSeat = 0;
        RunStartTime = Time.time;
    }

    /// <summary>The most recent choice a given seat made, or null.</summary>
    public static ChoiceEntry LastChoiceBy(int seat)
    {
        for (int i = Choices.Count - 1; i >= 0; i--)
            if (Choices[i].seat == seat) return Choices[i];
        return null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Choices.Clear(); ImpactAngle = float.NaN; }
}
