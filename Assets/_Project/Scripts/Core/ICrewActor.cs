using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anything that crews the ship: human players (Rabib) and bots (Riham).
/// Both must use the same Station methods, so the game treats them the same way.
/// </summary>
public interface ICrewActor
{
    /// <summary>1-4. CrewRegistry.SeatName gives its name ("Ezra", or what a player typed).</summary>
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

    // The crew's own names, per seat. A human's seat uses the name they typed on the menu instead, if any.
    static readonly string[] CrewNames = { "nobody", "Arthur", "Ezra", "Mabel", "Jonah" };

    /// <summary>Who sits in a seat, by name: what the Captain calls them, and what the HUD, vote and results show.</summary>
    public static string SeatName(int seat)
    {
        if (seat >= 1 && seat <= PlayerSlots.HumanCount)
        {
            string typed = PlayerSlots.NameFor(seat);
            if (typed.Length > 0) return typed;
        }
        return DefaultSeatName(seat);
    }

    /// <summary>The seat's own crew name ("Arthur" for seat 1), ignoring anything a player typed.</summary>
    public static string DefaultSeatName(int seat) =>
        seat >= 0 && seat < CrewNames.Length ? CrewNames[seat] : "Sailor " + seat;

    /// <summary>A name tag: the seat's name, with "(you)" on the player's own when they play alone.</summary>
    public static string CrewName(ICrewActor c)
    {
        if (c == null) return "";
        if (!c.IsBot)
        {
            int humans = 0;
            foreach (var m in crew) if (!m.IsBot) humans++;
            if (humans <= 1) return SeatName(c.Seat) + " (you)";
        }
        return SeatName(c.Seat);
    }

    /// <summary>A seat's name regardless of who sits there now (for the results, after the crew are gone).</summary>
    public static string CrewNameForSeat(int seat) => SeatName(seat);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => crew.Clear();
}
