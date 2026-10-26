using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anything that crews the ship: human players (Rabib) and bots (Riham).
/// Both must use the same Station methods, so the game treats them the same way.
/// </summary>
public interface ICrewActor
{
    /// <summary>1-4, shown as "Number One" etc.</summary>
    int Seat { get; }
    bool IsBot { get; }
    /// <summary>Never show this in the UI before the results screen.</summary>
    bool IsStoker { get; }
    /// <summary>World position. The deck is the XZ plane.</summary>
    Vector3 Position { get; }
    /// <summary>Which room the actor is in, e.g. "Boiler", "Corridor", "Wireless", "Bridge".</summary>
    string RoomId { get; }
    /// <summary>True while locked out by a muster vote.</summary>
    bool IsLockedOut { get; }
}

/// <summary>
/// Every crew member registers here when it spawns (OnEnable) and unregisters when it goes (OnDisable).
/// OrderDirector uses this list to hand out orders.
/// </summary>
public static class CrewRegistry
{
    static readonly List<ICrewActor> crew = new List<ICrewActor>(4);

    public static IReadOnlyList<ICrewActor> All => crew;

    public static void Register(ICrewActor actor)
    {
        if (actor != null && !crew.Contains(actor)) crew.Add(actor);
        crew.Sort((a, b) => a.Seat.CompareTo(b.Seat));
    }

    public static void Unregister(ICrewActor actor) => crew.Remove(actor);

    public static ICrewActor BySeat(int seat)
    {
        foreach (var c in crew) if (c.Seat == seat) return c;
        return null;
    }

    public static ICrewActor Stoker
    {
        get { foreach (var c in crew) if (c.IsStoker) return c; return null; }
    }

    static readonly string[] SeatNames = { "nobody", "Number One", "Number Two", "Number Three", "Number Four" };

    /// <summary>"Number One" for seat 1, and so on.</summary>
    public static string SeatName(int seat) =>
        seat >= 0 && seat < SeatNames.Length ? SeatNames[seat] : "Number " + seat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => crew.Clear();
}
