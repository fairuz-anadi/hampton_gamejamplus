using UnityEngine;

/// <summary>
/// The ship's length along Z (bow is +Z). The deck was laid out stern to bow in 36 m (Boiler 0-8, Corridor 8-16,
/// Wireless 16-24, Bridge 24-32, Bow 32-36); a 40 m promenade now sits between the Corridor and the Wireless room,
/// so the ship is about twice as long. Anything placed by hand in the old layout goes through Z() or P() to land
/// in the same spot on the longer ship. Owner: Anadi (layout), Samprity (the deck itself).
/// </summary>
public static class ShipLayout
{
    /// <summary>Where the promenade starts: the old Corridor/Wireless bulkhead.</summary>
    public const float InsertAt = 16f;
    /// <summary>How long the promenade is: 5 bays of 8 m, so the side walls keep their window rhythm.</summary>
    public const float Extra = 40f;
    /// <summary>The deck runs from the stern (z 0) to the bow railing.</summary>
    public const float DeckLength = 36f + Extra;

    /// <summary>A Z from the old 36 m layout, moved to the same spot on the longer ship.</summary>
    public static float Z(float oldZ) => oldZ >= InsertAt ? oldZ + Extra : oldZ;

    /// <summary>A position from the old 36 m layout, moved to the same spot on the longer ship.</summary>
    public static Vector3 P(Vector3 old) => new Vector3(old.x, old.y, Z(old.z));
}
