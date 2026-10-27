/// <summary>Anything that can tell a Station or CarryItem whether Interact is currently held,
/// regardless of whether it comes from a PlayerInput action or a raw debug key. Owner: Rabib.</summary>
public interface IInteractSource
{
    bool InteractHeld { get; }
    /// <summary>Left/right steering, -1..1 (the Move action's x, or the debug arrows). Only read while steering the Wheel.</summary>
    float SteerInput { get; }
}
