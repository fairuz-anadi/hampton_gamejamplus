/// <summary>
/// The part of a station that crew actually use. Players and bots call exactly the same methods (the golden rule).
/// Rabib: please implement this on the Station base class next to IStation. Until then, bots walk to stations and
/// wait there, and SandboxStation stands in for testing.
///
///   BeginWork / EndWork: the actor started / stopped holding Interact in range. The station counts its workers,
///                        moves progress only while enough are working (2 for needsTwo orders), and turns green
///                        when done. While broken, working repairs it instead (3 s), then raise StationRepaired.
///   Sabotage:            Stoker only. Set IsBroken and raise GameEvents.StationBroken(type, actor).
///                        Don't touch the meters; the Stoker applies its own Safety cost.
/// </summary>
public interface ICrewWorkStation : IStation
{
    /// <summary>How close (flat distance from Position) a crew member must stand to work.</summary>
    float InteractRange { get; }
    /// <summary>How many crew are working it right now.</summary>
    int WorkerCount { get; }
    void BeginWork(ICrewActor actor);
    void EndWork(ICrewActor actor);
    void Sabotage(ICrewActor actor);
}
