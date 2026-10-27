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

    void OnGUI()
    {
        if (carriedItem == null && NearestStation == null) return;
        var cam = Camera.main;
        if (cam == null) return;

        var screen = cam.WorldToScreenPoint(transform.position + Vector3.up * 2.2f);
        if (screen.z <= 0f) return;

        string text;
        if (carriedItem != null) text = "[Interact] Drop";
        else if (NearestStation is Wheel w && w.SteeringOpen)
            text = w.IsBroken ? "[Hold Interact] Repair Wheel" : "[Hold Interact + Left/Right] Steer";
        else if (NearestStation is Doors d && d.CanUnlock) text = $"[Hold Interact] Reopen lifeboat store {d.ActiveHolders}/{d.NeededHolders}";
        else if (NearestStation is Station s && s.NeededHolders > 1) text = $"[Hold Interact] {s.Type} {s.ActiveHolders}/{s.NeededHolders}";
        else text = $"[Hold Interact] {NearestStation.Type}";
        GUI.Label(new Rect(screen.x - 80, Screen.height - screen.y, 220, 20), text);
    }
}
