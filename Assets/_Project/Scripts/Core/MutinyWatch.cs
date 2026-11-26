using UnityEngine;

/// <summary>
/// The Captain's patience. Every player keeps their own count of reasonable orders said no to in a row: refusing a
/// normal order, or letting one run out, adds one; an order reported done (honest or faked) puts it back to nothing.
/// Saying no to an "off" order (one that's wrong to obey) neither counts nor resets the count, so standing up to his
/// bad orders never gets anyone thrown off. Owner: sloth-262.
///   At 2 (Warning) the Captain warns that player by name (CaptainDirector picks the line).
///   At 3 (Limit) he throws them overboard (GameManager.ThrowOverboard). Only that player is out: their crewmates sail
///   on. When the last player aboard goes over the side, the voyage ends with the Overboard ending and its film.
/// Any time in Parts 1-3; never at the iceberg. Bots never count. The streak is read straight from RunRecord, so it
/// doesn't matter which listener hears an answer first.
/// </summary>
public static class MutinyWatch
{
    public const int Warning = 2;
    public const int Limit = 3;

    /// <summary>A player goes over the side: their seat, and where they stood at the rail. Fired as they hit the water.</summary>
    public static event System.Action<int, Vector3> ThrownOverboard;

    public static void RaiseThrownOverboard(int seat, Vector3 at) => ThrownOverboard?.Invoke(seat, at);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Hook()
    {
        GameEvents.OrderResolved -= OnResolved;
        GameEvents.OrderResolved += OnResolved;
        ThrownOverboard = null;
    }

    /// <summary>
    /// True while saying no counts: during the story parts. (Not tied to GameManager.Overboard, so whichever listener
    /// hears the third "no" first, the others still see it as the last straw.)
    /// </summary>
    public static bool Active
    {
        get
        {
            var gm = GameManager.Instance;
            return gm != null && gm.State >= GameState.Part1 && gm.State < GameState.Iceberg;
        }
    }

    /// <summary>A "no" to an order that was wrong to obey: it doesn't count against anyone.</summary>
    static bool Excused(OrderData order, Choice choice) =>
        order != null && order.IsOff && (choice == Choice.Refuse || choice == Choice.Expired);

    /// <summary>How many reasonable orders in a row this seat has refused or let run out, counting back from its latest answer.</summary>
    public static int Streak(int seat)
    {
        if (seat <= 0) return 0;
        int n = 0;
        for (int i = RunRecord.Choices.Count - 1; i >= 0; i--)
        {
            var c = RunRecord.Choices[i];
            if (c.seat != seat || c.byBot || Excused(c.order, c.choice)) continue;
            if (c.choice == Choice.Refuse || c.choice == Choice.Expired) n++;
            else break;
        }
        return n;
    }

    /// <summary>This answer is the one that has the Captain warn the player (see CaptainDirector).</summary>
    public static bool IsWarning(ActiveOrder o, Choice choice) => Counts(o, choice) && Streak(o.Assignee.Seat) == Warning;

    /// <summary>This answer is the last straw: over the side they go.</summary>
    public static bool IsLastStraw(ActiveOrder o, Choice choice) => Counts(o, choice) && Streak(o.Assignee.Seat) >= Limit;

    /// <summary>Nobody else is still aboard to play on: throwing this seat over ends the voyage.</summary>
    public static bool LastAboard(int seat)
    {
        foreach (var c in CrewRegistry.All)
        {
            if (c.IsBot || c.Seat == seat) continue;
            if (c is PlayerController p && p.IsOverboard) continue;
            return false;
        }
        return true;
    }

    static bool Counts(ActiveOrder o, Choice choice) =>
        Active && o != null && o.Assignee != null && !o.Assignee.IsBot
        && (choice == Choice.Refuse || choice == Choice.Expired) && !Excused(o.Data, choice);

    static void OnResolved(ActiveOrder o, Choice choice)
    {
        if (IsLastStraw(o, choice)) GameManager.Instance.ThrowOverboard(o.Assignee.Seat);
    }
}
