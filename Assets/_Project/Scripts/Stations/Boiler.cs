/// <summary>2-person coal chute. Owner: Rabib.</summary>
public class Boiler : Station
{
    public override StationType Type => StationType.Boiler;
    protected override int RequiredHolders => 2;
}
