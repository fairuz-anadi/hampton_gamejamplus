/// <summary>2-person door levers (door four, lifeboat store, etc). Owner: Rabib.</summary>
public class Doors : Station
{
    public override StationType Type => StationType.Doors;
    protected override int RequiredHolders => 2;
}
