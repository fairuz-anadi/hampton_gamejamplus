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

    /// <summary>"Number One" for seat 1, and so on. The Captain only ever uses these.</summary>
    public static string SeatName(int seat) =>
        seat >= 0 && seat < SeatNames.Length ? SeatNames[seat] : "Number " + seat;

    // The crew's own names, per seat; the Captain never uses them, the crew do (name tags, whispers, results).
    static readonly string[] CrewNames = { "", "Arthur", "Ezra", "Mabel", "Jonah" };

    /// <summary>A bot's name ("Mabel"); a human player is "You" (solo) or "Player N".</summary>
    public static string CrewName(ICrewActor c)
    {
        if (c == null) return "";
        if (!c.IsBot)
        {
            int humans = 0;
            foreach (var m in crew) if (!m.IsBot) humans++;
            return humans <= 1 ? "You" : "Player " + c.Seat;
        }
        return c.Seat >= 0 && c.Seat < CrewNames.Length ? CrewNames[c.Seat] : SeatName(c.Seat);
    }

    /// <summary>A seat's crew name regardless of who sits there now (for the results, after the crew are gone).</summary>
    public static string CrewNameForSeat(int seat) =>
        seat >= 0 && seat < CrewNames.Length && CrewNames[seat] != "" ? CrewNames[seat] : SeatName(seat);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => crew.Clear();
}
