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

    // ---------- The levers move ----------
    // Each lever (the deck's Lever_A / Lever_B) swings on its base: hauled down toward whoever's pulling it, then let
    // back up, again and again while they hold Interact, in time with their lever-pull loop (WorkAnimations.lever).

    [Header("Lever motion")]
    [Tooltip("Degrees a lever swings down toward the crew on each pull.")]
    [SerializeField] float pullAngle = 55f;
    [Tooltip("Seconds for one pull and release. Match the lever-pull animation's length (1 s).")]
    [SerializeField] float pullSeconds = 1f;
    [Tooltip("Metres from a lever within which a crew member working the Doors counts as pulling that one.")]
    [SerializeField] float leverReach = 1.6f;

    class Lever
    {
        public Transform pivot;
        public Quaternion rest;
        public float angle, phase;
    }

    Lever[] levers = new Lever[0];
    readonly ICrewActor[] pullers = new ICrewActor[4];

    void Start() => FindLevers();

    /// <summary>Puts each lever on a hinge at its base, so it swings from there instead of round its middle.</summary>
    void FindLevers()
    {
        var found = new System.Collections.Generic.List<Lever>();
        foreach (var name in new[] { "Lever_A", "Lever_B" })
        {
            var lever = transform.Find(name);
            if (lever == null) continue;
            // A unit cube scaled up its local Y: the base is half its height below its centre, along its own up.
            float half = lever.localScale.y * 0.5f;
            var hinge = new GameObject(name + "_Hinge").transform;
            hinge.SetParent(transform, false);
            hinge.localPosition = lever.localPosition - lever.localRotation * (Vector3.up * half);
            hinge.localRotation = lever.localRotation;
            lever.SetParent(hinge, true);
            found.Add(new Lever { pivot = hinge, rest = hinge.localRotation });
        }
        levers = found.ToArray();
    }

    void LateUpdate()
    {
        if (levers.Length == 0) return;
        int n = GetFreshHolders(pullers);
        foreach (var l in levers)
        {
            bool pulled = false;
            for (int i = 0; i < n; i++)
                if (pullers[i] != null && ClosestLever(pullers[i].Position) == l) { pulled = true; break; }
            if (pulled)
            {
                l.phase += Time.deltaTime / Mathf.Max(0.2f, pullSeconds);
                // Haul down over the first 45%, ease back up over the rest.
                float p = Mathf.Repeat(l.phase, 1f);
                float down = p < 0.45f ? Mathf.SmoothStep(0f, 1f, p / 0.45f) : Mathf.SmoothStep(1f, 0f, (p - 0.45f) / 0.55f);
                l.angle = down * pullAngle;
            }
            else
            {
                l.phase = 0f; // the next pull starts from the top, in step with the animation
                l.angle = Mathf.MoveTowards(l.angle, 0f, pullAngle * 3f * Time.deltaTime);
            }
            // The levers lean toward the corridor; pulling tips them further that way (round the bank's long axis).
            l.pivot.localRotation = l.rest * Quaternion.Euler(0f, 0f, l.angle);
        }
    }

    Lever ClosestLever(Vector3 world)
    {
        Lever best = null;
        float bestD = leverReach * leverReach;
        foreach (var l in levers)
        {
            var d = l.pivot.position - world;
            d.y = 0f;
            if (d.sqrMagnitude < bestD) { bestD = d.sqrMagnitude; best = l; }
        }
        return best;
    }

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
