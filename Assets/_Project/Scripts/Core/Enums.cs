// Shared types for the whole team. Owner: Anadi.
// Changing anything in here affects everyone, so post in Discord before you edit it.

public enum StationType { Boiler, Wheel, Doors, Wireless }

public enum OrderKind { Normal, Off, Final }

public enum Choice { Obey, Fake, Refuse, Expired }

public enum GameState { Menu, Part1, Part2, Part3, Iceberg, Results }

/// <summary>How much one choice moves each meter. Values are added, then clamped to 0..100.</summary>
[System.Serializable]
public struct MeterDelta
{
    public int captainTrust;
    public int shipSafety;
    public int crewTrust;

    public MeterDelta(int captainTrust, int shipSafety, int crewTrust)
    {
        this.captainTrust = captainTrust;
        this.shipSafety = shipSafety;
        this.crewTrust = crewTrust;
    }

    public bool IsZero => captainTrust == 0 && shipSafety == 0 && crewTrust == 0;

    public static MeterDelta operator +(MeterDelta a, MeterDelta b) =>
        new MeterDelta(a.captainTrust + b.captainTrust, a.shipSafety + b.shipSafety, a.crewTrust + b.crewTrust);

    /// <summary>Short text for the results screen, e.g. "Captain +5, Safety -12".</summary>
    public override string ToString()
    {
        var parts = new System.Collections.Generic.List<string>(3);
        if (captainTrust != 0) parts.Add($"Captain {Signed(captainTrust)}");
        if (shipSafety != 0) parts.Add($"Safety {Signed(shipSafety)}");
        if (crewTrust != 0) parts.Add($"Crew {Signed(crewTrust)}");
        return parts.Count == 0 ? "no change" : string.Join(", ", parts);
    }

    static string Signed(int v) => v > 0 ? "+" + v : v.ToString();
}
