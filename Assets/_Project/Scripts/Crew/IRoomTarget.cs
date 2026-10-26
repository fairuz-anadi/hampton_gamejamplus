/// <summary>
/// Anything that can be told which room it's standing in. Not part of ICrewActor (RoomId there is
/// read-only), so RoomZone uses this to drive it from either side. Owner: Rabib.
/// </summary>
public interface IRoomTarget
{
    void SetRoom(string roomId);
}
