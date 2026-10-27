using UnityEngine;

/// <summary>
/// 2-person door levers (door four, lifeboat store, etc). Owner: Rabib.
/// Obeying order 9 locks the lifeboat store. While no Doors order is active, two crew holding the levers for
/// unlockSeconds reopen it. This runs on its own timer, so it never touches the order's IsDone / green light.
/// </summary>
public class Doors : Station
{
    [Tooltip("Seconds two crew must hold the levers to reopen the lifeboat store.")]
    [SerializeField] float unlockSeconds = 3f;

    float unlockProgress;
    // The pair who just obeyed order 9 are still on the levers when the lock lands. The unlock only arms once
    // the hold drops below two, so they have to let go and pull again.
    bool armed;
    bool wasLocked;

    public override StationType Type => StationType.Doors;
    protected override int RequiredHolders => 2;

    /// <summary>True when two crew pulling the levers now would reopen the lifeboat store.</summary>
    public bool CanUnlock => RunRecord.LifeboatStoreLocked && armed && !IsBroken && !DoorsOrderActive() && BeforeIceberg();

    protected override void Update()
    {
        base.Update();
        TickUnlock();
    }

    void TickUnlock()
    {
        bool locked = RunRecord.LifeboatStoreLocked;
        if (locked != wasLocked)
        {
            wasLocked = locked;
            armed = false;
            unlockProgress = 0f;
        }
        if (!locked) return;

        if (!armed && ActiveHolders < 2)
        {
            armed = true;
            Debug.Log("[Doors] Lifeboat store locked; unlock armed. Two crew can pull the levers to reopen it.");
        }

        if (!CanUnlock || ActiveHolders < 2)
        {
            unlockProgress = 0f;
            return;
        }

        unlockProgress += Time.deltaTime;
        if (unlockProgress < unlockSeconds) return;

        RunRecord.LifeboatStoreLocked = false;
        Debug.Log("[Doors] Two crew pulled the levers: RunRecord.LifeboatStoreLocked = false (lifeboat store reopened).");
    }

    static bool DoorsOrderActive()
    {
        var od = OrderDirector.Instance;
        if (od == null) return false;
        foreach (var o in od.Active)
            if (o.Data.station == StationType.Doors || o.Data.CheckStation == StationType.Doors) return true;
        return false;
    }

    static bool BeforeIceberg()
    {
        var gm = GameManager.Instance;
        return gm == null || gm.State < GameState.Iceberg;
    }
}
