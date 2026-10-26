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
    CarryItem carriedItem;
    bool wasHeld;

    public IStation NearestStation { get; private set; }

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

        if (justPressed)
        {
            var item = FindNearestCarryItem();
            if (item != null && item.TryPickUp(actor, transform))
                carriedItem = item;
        }
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

    CarryItem FindNearestCarryItem()
    {
        CarryItem best = null;
        float bestDist = range;
        foreach (var item in Object.FindObjectsByType<CarryItem>())
        {
            if (item.IsCarried) continue;
            float dist = Vector3.Distance(transform.position, item.transform.position);
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
        else if (NearestStation is Station s && s.NeededHolders > 1) text = $"[Hold Interact] {s.Type} {s.ActiveHolders}/{s.NeededHolders}";
        else text = $"[Hold Interact] {NearestStation.Type}";
        GUI.Label(new Rect(screen.x - 80, Screen.height - screen.y, 220, 20), text);
    }
}
