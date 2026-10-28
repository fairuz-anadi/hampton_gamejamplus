using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Words and key labels the HUD shows. One place, so wording stays consistent. Owner: Samprity.</summary>
public static class HudText
{
    public static string Seat(int seat) => seat > 0 ? CrewRegistry.SeatName(seat) : "Nobody";

    public static string StationName(StationType t) => t switch
    {
        StationType.Boiler => "Boiler",
        StationType.Wheel => "Wheel",
        StationType.Doors => "Door levers",
        StationType.Wireless => "Wireless desk",
        _ => t.ToString(),
    };

    public static string StationVerb(StationType t) => t switch
    {
        StationType.Boiler => "Shovel coal",
        StationType.Wheel => "Hold the wheel",
        StationType.Doors => "Pull levers",
        StationType.Wireless => "Use the wireless",
        _ => "Work",
    };

    /// <summary>"Boiler · needs 2 crew" or "Wireless desk → Wheel" for carry orders.</summary>
    public static string Where(OrderData d)
    {
        string where = d.isCarry && d.deliverTo != d.station
            ? $"{StationName(d.station)}  →  {DeliverName(d.deliverTo)}"
            : StationName(d.station);
        return d.needsTwo ? where + "  ·  needs 2 crew" : where;
    }

    // Carry orders deliver to the bridge, where the wheel is.
    static string DeliverName(StationType t) => t == StationType.Wheel ? "Bridge" : StationName(t);

    public static string ResultStamp(Choice c) => c switch
    {
        // Honest and fake reports look the same here on purpose; the Captain finds out later.
        Choice.Obey or Choice.Fake => "REPORTED DONE",
        Choice.Refuse => "REFUSED",
        _ => "TIME'S UP",
    };

    public enum Key { Interact, Report, Refuse, CallHelp }

    /// <summary>Key or button name for a crew member's own device (keyboard by default).</summary>
    public static string KeyFor(Key key, Component actor)
    {
        bool pad = false;
        if (actor != null && actor.TryGetComponent(out PlayerInput input)) pad = input.currentControlScheme == "Gamepad";
        return key switch
        {
            Key.Interact => pad ? "A" : "E",
            Key.Report => pad ? "Y" : "R",
            Key.Refuse => pad ? "B" : "X",
            _ => pad ? "X" : "Q",
        };
    }
}
