/// <summary>An order that has been issued and is waiting for its crew member to act.</summary>
public class ActiveOrder
{
    /// <summary>Unique per run, starting at 1.</summary>
    public int Id { get; }
    /// <summary>1-12, the order's position in the voyage.</summary>
    public int Number { get; }
    public OrderData Data { get; }
    /// <summary>The crew member who must choose. Can be null in test scenes with no crew.</summary>
    public ICrewActor Assignee { get; }
    public float TimeLeft { get; internal set; }
    public bool IsResolved { get; internal set; }
    public Choice? Result { get; internal set; }

    public float Progress01 => Data.timer <= 0f ? 1f : 1f - TimeLeft / Data.timer;

    internal ActiveOrder(int id, int number, OrderData data, ICrewActor assignee)
    {
        Id = id;
        Number = number;
        Data = data;
        Assignee = assignee;
        TimeLeft = data.timer;
    }

    public override string ToString() =>
        $"#{Number} {Data.name} -> {(Assignee != null ? "Seat " + Assignee.Seat : "nobody")} ({TimeLeft:0}s)";
}
