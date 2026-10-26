using UnityEngine;

/// <summary>
/// Anything Interactor can pick up, carry and drop. CarryItem (order-tied) and IceWarningCarry
/// (order-less) both implement this; Interactor only ever needs these three members. Owner: Rabib.
/// </summary>
public interface ICarryItem
{
    bool IsCarried { get; }
    Vector3 Position { get; }
    bool TryPickUp(ICrewActor actor, Transform actorTransform);
    void Drop();
}
