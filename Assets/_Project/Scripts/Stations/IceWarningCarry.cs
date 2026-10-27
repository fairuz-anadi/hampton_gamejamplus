using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A floating ice warning (MSG 1-3): one instance per number, visible and pickupable only while
/// IceWarnings reports it Waiting. Anyone can pick it up, no OrderData involved — unlike CarryItem,
/// which requires the carrier to hold a matching order. Carrying it near the Bridge and letting go
/// (pressing Interact again, same as any carried item, via Drop()) calls IceWarnings.Instance.Deliver(n).
/// "Near the Bridge" is Anadi's DropPoint_Bridge deck marker when it exists, else the Wheel station's
/// own position (the sandbox has no marker). Letting go anywhere else just abandons it in place. If the 40 s
/// window runs out while it's being carried, IceWarnings marks it Lost on its own; Update() notices
/// next frame and releases it the same way CarryItem notices an order resolving out from under it.
/// Static TryPickUpWarning/TryDeliverWarning let Riham's bots and the Stoker use this without going
/// through Interactor at all. The Stoker hides a warning with IceWarnings.Instance.HideNext() — that
/// already exists and needs nothing new here. Owner: Rabib.
/// </summary>
public class IceWarningCarry : MonoBehaviour, ICarryItem
{
    [SerializeField] int warningNumber = 1;
    [SerializeField] float pickupRange = 1.5f;
    [SerializeField] float deliverRange = 2.5f;

    static readonly List<IceWarningCarry> all = new List<IceWarningCarry>();

    Vector3 homePosition;
    Renderer visual;
    ICrewActor carrierActor;

    public bool IsCarried => carrierActor != null;
    public Vector3 Position => transform.position;

    /// <summary>Read-only view for UI (e.g. the minimap). Iterate by index; entries are never null, but check anyway.</summary>
    public static IReadOnlyList<IceWarningCarry> All => all;

    /// <summary>True while this warning is sitting at the desk or being carried, i.e. visible and pickupable.</summary>
    public bool IsWaiting => IceWarnings.Instance != null && IceWarnings.Instance.StatusOf(warningNumber) == IceWarnings.Status.Waiting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => all.Clear();

    void Awake()
    {
        homePosition = transform.position;
        visual = GetComponentInChildren<Renderer>();
    }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    void Update()
    {
        var ice = IceWarnings.Instance;
        var status = ice != null ? ice.StatusOf(warningNumber) : IceWarnings.Status.Pending;

        // Timed out or otherwise resolved while we were carrying it: let go without ceremony.
        if (carrierActor != null && status != IceWarnings.Status.Waiting) carrierActor = null;

        transform.position = carrierActor != null ? carrierActor.Position + Vector3.up * 1.2f : homePosition;
        if (visual != null) visual.enabled = status == IceWarnings.Status.Waiting;
    }

    public bool TryPickUp(ICrewActor actor, Transform actorTransform)
    {
        if (actor == null || actor.IsLockedOut || IsCarried) return false;
        if (IceWarnings.Instance?.StatusOf(warningNumber) != IceWarnings.Status.Waiting) return false;
        if (Vector3.Distance(transform.position, actor.Position) > pickupRange) return false;

        carrierActor = actor;
        return true;
    }

    /// <summary>Interactor calls this on press: delivers if at the Bridge right now, otherwise just lets go.</summary>
    public void Drop()
    {
        AttemptDelivery();
        carrierActor = null;
    }

    /// <summary>For bots/the Stoker: delivers only if actually at the Bridge right now, keeps carrying otherwise.</summary>
    public bool TryDeliver()
    {
        if (!AttemptDelivery()) return false;
        carrierActor = null;
        return true;
    }

    bool AttemptDelivery()
    {
        if (carrierActor == null) return false;
        if (!TryGetBridgePosition(out var bridgePos)) return false;
        if (Vector3.Distance(carrierActor.Position, bridgePos) > deliverRange) return false;
        return IceWarnings.Instance != null && IceWarnings.Instance.Deliver(warningNumber);
    }

    /// <summary>Anadi's deck marks the real drop point as DropPoint_Bridge; the sandbox has none, so this
    /// falls back to the Wheel station there instead.</summary>
    static bool TryGetBridgePosition(out Vector3 position)
    {
        var marker = GameObject.Find("DropPoint_Bridge");
        if (marker != null) { position = marker.transform.position; return true; }

        var wheel = StationRegistry.Get(StationType.Wheel);
        if (wheel != null) { position = wheel.Position; return true; }

        position = Vector3.zero;
        return false;
    }

    /// <summary>Picks up the nearest waiting warning in range, if the actor isn't already carrying one.</summary>
    public static bool TryPickUpWarning(ICrewActor actor)
    {
        if (actor == null || actor.IsLockedOut) return false;
        foreach (var w in all) if (w.carrierActor == actor) return false;

        IceWarningCarry best = null;
        float bestDist = float.MaxValue;
        foreach (var w in all)
        {
            if (w.IsCarried) continue;
            if (IceWarnings.Instance?.StatusOf(w.warningNumber) != IceWarnings.Status.Waiting) continue;
            float dist = Vector3.Distance(actor.Position, w.transform.position);
            if (dist <= w.pickupRange && dist < bestDist) { bestDist = dist; best = w; }
        }
        if (best == null) return false;
        best.carrierActor = actor;
        return true;
    }

    /// <summary>Delivers whatever this actor is carrying, if they're at the Bridge right now.</summary>
    public static bool TryDeliverWarning(ICrewActor actor)
    {
        foreach (var w in all)
            if (w.carrierActor == actor) return w.TryDeliver();
        return false;
    }
}
