using UnityEngine;

/// <summary>
/// Room questions the bots and the Stoker ask, answered from Samprity's RoomVolumes by position. Owner: Riham.
/// Works out everyone's room from where they stand, so it doesn't depend on how a player fills RoomId.
/// Without any RoomVolumes in the scene, "same room" falls back to "within 6 m".
/// </summary>
public static class Rooms
{
    public const float NoRoomsRadius = 6f;

    public static bool HaveRooms => RoomVolume.All.Count > 0;

    /// <summary>The room at a position, or "" when the scene has no rooms.</summary>
    public static string At(Vector3 p) => HaveRooms ? RoomVolume.RoomAt(p) : "";

    public static RoomVolume Find(string id)
    {
        foreach (var r in RoomVolume.All) if (r.roomId == id) return r;
        return null;
    }

    /// <summary>True when a human player is in the same room as this spot.</summary>
    public static bool HumanNear(Vector3 spot)
    {
        string room = At(spot);
        bool useRoom = room != "" && room != "Deck";
        foreach (var c in CrewRegistry.All)
        {
            if (c.IsBot) continue;
            if (useRoom ? At(c.Position) == room : BotMover.FlatDistance(c.Position, spot) < NoRoomsRadius) return true;
        }
        return false;
    }
}
