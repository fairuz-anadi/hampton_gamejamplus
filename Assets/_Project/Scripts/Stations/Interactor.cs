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
        bool holding = interactSource != null && interactSource.InteractHeld;
        bool justPressed = holding && !wasHeld;
        wasHeld = holding;

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
        foreach (var item in Object.FindObjectsByType<CarryItem>(FindObjectsSortMode.None))
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

        string text = carriedItem != null ? "[Interact] Drop" : $"[Hold Interact] {NearestStation.Type}";
        GUI.Label(new Rect(screen.x - 50, Screen.height - screen.y, 160, 20), text);
    }
}
