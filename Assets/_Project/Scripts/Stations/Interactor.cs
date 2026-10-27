using UnityEngine;

/// <summary>
/// Finds the nearest station or carry item in range, holds the station while Interact is held,
/// and toggles carry pickup/drop on press. Works off ICrewActor + IInteractSource, so it drives
/// identically for PlayerController and the debug keyboard P2. Owner: Rabib.
/// </summary>
public class Interactor : MonoBehaviour
{
    [SerializeField] float range = 2.5f;

    ICrewActor actor;
    IInteractSource interactSource;
    Station currentStation;
    ICarryItem carriedItem;
    bool wasHeld;

    public IStation NearestStation { get; private set; }
    /// <summary>Holding the Wheel during the iceberg finale. The Move axis steers then, so the owner stops walking.</summary>
    public bool IsSteering { get; private set; }
    /// <summary>True while a CarryItem/IceWarningCarry is in hand.</summary>
    public bool IsCarrying => carriedItem != null;

    /// <summary>
    /// The short action phrase for whatever's nearest right now ("Drop", "Repair Wheel", "Boiler 1/2", ...),
    /// or "" when there's nothing to interact with. One source of truth for every prompt UI (Samprity's HUD,
    /// or a sandbox fallback) instead of each one re-deriving it from station state.
    /// </summary>
    public string PromptText
    {
        get
        {
            if (carriedItem != null) return "Drop";
            if (NearestStation == null) return "";
            if (NearestStation is Wheel w && w.SteeringOpen)
                return w.IsBroken ? "Repair Wheel" : "Steer  ·  hold + Left/Right";
            if (NearestStation is Doors d && d.CanUnlock) return $"Reopen lifeboat store {d.ActiveHolders}/{d.NeededHolders}";
            if (NearestStation is Station s && s.NeededHolders > 1)
                return $"Hold  ·  {HudText.StationVerb(s.Type)}  {s.ActiveHolders}/{s.NeededHolders}";
            return "Hold  ·  " + HudText.StationVerb(NearestStation.Type);
        }
    }

    /// <summary>
    /// 0..1 progress of the station currently nearest (repair progress while broken, 1 once done),
    /// or 0 while carrying, steering, or near nothing. One source of truth for a UI progress bar.
    /// </summary>
    public float HoldProgress01
    {
        get
        {
            if (!(NearestStation is Station s)) return 0f;
            if (s is Wheel w && w.SteeringOpen) return 0f;
            return s.IsDone && !s.IsBroken ? 1f : s.Progress01;
        }
    }

    void Awake()
    {
        actor = GetComponent<ICrewActor>();
        interactSource = GetComponent<IInteractSource>();
    }

    void Update()
    {
        // Locked out by a muster vote: can still walk, but can't work any station for 60 s (plan).
        bool lockedOut = actor != null && actor.IsLockedOut;
        bool holding = !lockedOut && interactSource != null && interactSource.InteractHeld;
        bool justPressed = holding && !wasHeld;
        wasHeld = holding;
        IsSteering = false;

        // The item went home on its own (its order ended), so we're empty-handed again.
        if (carriedItem != null && !carriedItem.IsCarried) carriedItem = null;

        if (carriedItem != null)
        {
            if (justPressed)
            {
                carriedItem.Drop();
                carriedItem = null;
            }
            ReleaseStation();
            NearestStation = null;
            return;
        }

        var nearestStation = FindNearestStation();
        if (nearestStation != currentStation)
        {
            currentStation?.Hold(actor, false);
            currentStation = nearestStation;
        }
        currentStation?.Hold(actor, holding);
        NearestStation = nearestStation;

        // Steering is separate from the order hold: one person is enough, the 2-holder count doesn't matter.
        if (holding && currentStation is Wheel wheel && wheel.SteeringOpen && !wheel.IsBroken)
        {
            IsSteering = true;
            wheel.Steer(actor, interactSource.SteerInput);
        }

        if (justPressed)
        {
            var item = FindNearestCarryItem();
            if (item != null && item.TryPickUp(actor, transform))
                carriedItem = item;
        }
    }

    /// <summary>
    /// CallHelp (Q / gamepad X): asks for a hand at the station in range, else at this actor's active order's station.
    /// CrewRoster turns it into a bot help job. Returns false when there's nowhere to call help to.
    /// </summary>
    public bool CallHelp()
    {
        StationType station;
        if (NearestStation != null) station = NearestStation.Type;
        else if (OrderDirector.Instance?.GetOrderFor(actor) is ActiveOrder order) station = order.Data.CheckStation;
        else return false;

        Debug.Log($"[Interactor] {CrewRegistry.SeatName(actor?.Seat ?? 0)} called for help at the {station}.");
        GameEvents.RaiseHelpCalled(actor, station);
        return true;
    }

    void ReleaseStation()
    {
        currentStation?.Hold(actor, false);
        currentStation = null;
    }

    Station FindNearestStation()
    {
        Station best = null;
        float bestDist = range;
        foreach (var s in StationRegistry.All)
        {
            if (!(s is Station station)) continue;
            float dist = Vector3.Distance(transform.position, station.Position);
            if (dist <= bestDist)
            {
                bestDist = dist;
                best = station;
            }
        }
        return best;
    }

    ICarryItem FindNearestCarryItem()
    {
        ICarryItem best = null;
        float bestDist = range;
        foreach (var item in Object.FindObjectsByType<CarryItem>())
        {
            if (item.IsCarried) continue;
            float dist = Vector3.Distance(transform.position, item.Position);
            if (dist <= bestDist)
            {
                bestDist = dist;
                best = item;
            }
        }
        foreach (var item in Object.FindObjectsByType<IceWarningCarry>())
        {
            if (item.IsCarried) continue;
            float dist = Vector3.Distance(transform.position, item.Position);
            if (dist <= bestDist)
            {
                bestDist = dist;
                best = item;
            }
        }
        return best;
    }
}
