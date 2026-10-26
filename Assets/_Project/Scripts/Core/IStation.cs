using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the order system needs to know about a station. Rabib's Station base class implements this.
/// A station is "done" (green light) once the current order's work there is finished.
/// </summary>
public interface IStation
{
    StationType Type { get; }
    /// <summary>Green light: the work for the current order is finished.</summary>
    bool IsDone { get; }
    /// <summary>Jammed by the Stoker, needs a repair.</summary>
    bool IsBroken { get; }
    Vector3 Position { get; }
    /// <summary>Called by OrderDirector when an order for this station is issued. Turn the light red and reset progress.</summary>
    void PrepareForOrder(ActiveOrder order);
}

/// <summary>Stations register themselves here in OnEnable and unregister in OnDisable.</summary>
public static class StationRegistry
{
    static readonly Dictionary<StationType, IStation> stations = new Dictionary<StationType, IStation>();

    public static void Register(IStation station)
    {
        if (station != null) stations[station.Type] = station;
    }

    public static void Unregister(IStation station)
    {
        if (station != null && stations.TryGetValue(station.Type, out var s) && s == station)
            stations.Remove(station.Type);
    }

    public static IStation Get(StationType type) => stations.TryGetValue(type, out var s) ? s : null;

    public static IEnumerable<IStation> All => stations.Values;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => stations.Clear();
}
