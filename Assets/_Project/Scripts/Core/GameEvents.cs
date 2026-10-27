using System;
using UnityEngine;

/// <summary>
/// Game-wide events. Subscribe in OnEnable, unsubscribe in OnDisable.
/// Only the owner of a system raises its events (use the Raise methods).
/// UI and audio (Samprity) and the Captain (Riham) mostly just listen.
/// </summary>
public static class GameEvents
{
    // Flow (raised by GameManager)
    public static event Action<GameState> StateChanged;
    /// <summary>1, 2, 3 for the story parts, 4 for the iceberg.</summary>
    public static event Action<int> PartChanged;
    /// <summary>All 12 orders are done. Riham plays the Truth monologue here.</summary>
    public static event Action TruthStarted;

    // Orders (raised by OrderDirector)
    public static event Action<ActiveOrder> OrderIssued;
    public static event Action<ActiveOrder, Choice> OrderResolved;
    /// <summary>The Captain "noticed" an earlier fake report. Riham: the Captain names the faker.</summary>
    public static event Action<ActiveOrder> FakeNoticed;

    // Meters (raised by MeterSystem)
    public static event Action MetersChanged;

    // Stations (raised by Rabib's stations / Riham's Stoker)
    public static event Action<StationType, ICrewActor> StationBroken;
    public static event Action<StationType, ICrewActor> StationRepaired;
    public static event Action<ICrewActor, StationType> HelpCalled;

    // Ice warnings (raised by IceWarnings), argument = warning number 1-3
    public static event Action<int> IceWarningArrived;
    public static event Action<int> IceWarningDelivered;
    public static event Action<int> IceWarningLost;
    /// <summary>The Stoker hid this warning: it never showed up. Players only notice the gap in the numbering.</summary>
    public static event Action<int> IceWarningHidden;

    /// <summary>A player obeyed with time to spare (OrderDirector's speed reward). Second argument = their streak so far.</summary>
    public static event Action<ActiveOrder, int> SmartlyDone;

    public static void RaiseStateChanged(GameState s) => StateChanged?.Invoke(s);
    public static void RaisePartChanged(int part) => PartChanged?.Invoke(part);
    public static void RaiseTruthStarted() => TruthStarted?.Invoke();
    public static void RaiseOrderIssued(ActiveOrder o) => OrderIssued?.Invoke(o);
    public static void RaiseOrderResolved(ActiveOrder o, Choice c) => OrderResolved?.Invoke(o, c);
    public static void RaiseFakeNoticed(ActiveOrder o) => FakeNoticed?.Invoke(o);
    public static void RaiseMetersChanged() => MetersChanged?.Invoke();
    public static void RaiseStationBroken(StationType t, ICrewActor by) => StationBroken?.Invoke(t, by);
    public static void RaiseStationRepaired(StationType t, ICrewActor by) => StationRepaired?.Invoke(t, by);
    public static void RaiseHelpCalled(ICrewActor who, StationType t) => HelpCalled?.Invoke(who, t);
    public static void RaiseIceWarningArrived(int n) => IceWarningArrived?.Invoke(n);
    public static void RaiseIceWarningDelivered(int n) => IceWarningDelivered?.Invoke(n);
    public static void RaiseIceWarningLost(int n) => IceWarningLost?.Invoke(n);
    public static void RaiseIceWarningHidden(int n) => IceWarningHidden?.Invoke(n);
    public static void RaiseSmartlyDone(ActiveOrder o, int streak) => SmartlyDone?.Invoke(o, streak);

    /// <summary>Clears every listener when play mode starts, in case domain reload is turned off.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        StateChanged = null; PartChanged = null; TruthStarted = null;
        OrderIssued = null; OrderResolved = null; FakeNoticed = null;
        MetersChanged = null;
        StationBroken = null; StationRepaired = null; HelpCalled = null;
        IceWarningArrived = null; IceWarningDelivered = null; IceWarningLost = null; IceWarningHidden = null;
        SmartlyDone = null;
    }
}
