/// <summary>2-person wheel. The steady-heading minigame and iceberg finale arrive in Step 4. Owner: Rabib.</summary>
public class Wheel : Station
{
    public override StationType Type => StationType.Wheel;
    protected override int RequiredHolders => 2;
}
