using UnityEngine;

/// <summary>
/// Wireless desk: solo hold-to-complete for orders 4 and 7, plus the two real ice-warning hooks
/// IceWarnings actually exposes. Order 4 (weather report) is plain flavor, no ice-warning tie-in.
/// Order 7 ("File it away. Unread.") is obeyed by holding here like any other order; when it
/// resolves as Obey, that's also the moment the oldest waiting warning gets filed unread, mirroring
/// DebugTools.DeliverWaitingWarning()'s own "try n = 1..Count, stop at the first that succeeds"
/// pattern. While someone holds Interact here and a warning is waiting, that's logged as reading it
/// (no state change IceWarnings doesn't already expose). Owner: Rabib.
///
/// Carrying a warning to the Bridge (Deliver(n)) is NOT built here: it isn't tied to any OrderData,
/// but CarryItem.TryPickUp requires the carrier to have an active order matching a specific OrderData
/// reference, which a free-floating ice warning never has. That needs its own small pickup, not a
/// reuse of CarryItem. Flagging per instructions, not building it.
/// </summary>
public class Wireless : Station
{
    static OrderData fileUnreadOrder;

    bool wasReading;

    public override StationType Type => StationType.Wireless;

    protected override void OnEnable()
    {
        base.OnEnable(); // registers the desk with StationRegistry
        if (fileUnreadOrder == null) fileUnreadOrder = Resources.Load<OrderData>("Orders/07_FileIceWarning");
        GameEvents.OrderResolved += OnOrderResolved;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        GameEvents.OrderResolved -= OnOrderResolved;
    }

    protected override void Update()
    {
        base.Update();

        bool reading = ActiveHolders > 0 && AnyWaiting();
        if (reading == wasReading) return;
        wasReading = reading;
        if (reading) Debug.Log("[Wireless] Reading a waiting ice warning.");
    }

    void OnOrderResolved(ActiveOrder active, Choice choice)
    {
        if (active.Data != fileUnreadOrder || choice != Choice.Obey) return;
        FileWaitingWarning();
    }

    static bool AnyWaiting()
    {
        var ice = IceWarnings.Instance;
        if (ice == null) return false;
        for (int n = 1; n <= ice.Count; n++)
            if (ice.StatusOf(n) == IceWarnings.Status.Waiting) return true;
        return false;
    }

    static void FileWaitingWarning()
    {
        var ice = IceWarnings.Instance;
        if (ice == null) return;
        for (int n = 1; n <= ice.Count; n++)
            if (ice.FileUnread(n)) return;
    }
}
