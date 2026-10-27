using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared behavior for all 4 stations: registration, status light, hold-based work progress,
/// broken/repair. Concrete stations only declare their Type and, if needed, RequiredHolders.
/// Owner: Rabib.
/// </summary>
public abstract class Station : MonoBehaviour, IStation
{
    [SerializeField] float workDuration = 3f;
    [SerializeField] float repairDuration = 3f;
    [Tooltip("The bulb that shows green/red. Left empty, a child named \"StatusLight\" is used (Samprity's deck), else the first renderer.")]
    [SerializeField] Renderer statusLight;

    // Actor -> last frame they held Interact here. Anyone who stops refreshing (left, was disabled,
    // got locked out) stops counting by the next frame, so one person can never pass a two-person chore.
    readonly Dictionary<ICrewActor, int> holders = new Dictionary<ICrewActor, int>();
    readonly Dictionary<ICrewActor, int> deliverers = new Dictionary<ICrewActor, int>();
    float workProgress;
    float repairProgress;
    Color shownColor;

    public abstract StationType Type { get; }
    protected virtual int RequiredHolders => 1;
    protected virtual int RepairHolders => 1;

    public bool IsDone { get; private set; }
    public bool IsBroken { get; private set; }
    public Vector3 Position => transform.position;
    public float Progress01 => IsBroken ? SafeDiv(repairProgress, repairDuration) : SafeDiv(workProgress, workDuration);
    /// <summary>How many crew are working it right now (for prompts like "1/2").</summary>
    public int ActiveHolders => CountFresh(holders);
    public int NeededHolders => RequiredHolders;

    void Awake()
    {
        if (statusLight == null)
        {
            var bulb = transform.Find("StatusLight");
            statusLight = bulb != null ? bulb.GetComponent<Renderer>() : GetComponentInChildren<Renderer>();
        }
    }

    // Protected virtual so a station that needs its own OnEnable/OnDisable (Wireless) overrides these and calls
    // base. A plain "void OnEnable()" in a subclass would replace them silently and the station would never register.
    protected virtual void OnEnable()
    {
        StationRegistry.Register(this);
        GameEvents.StateChanged += OnStateChanged;
    }

    protected virtual void OnDisable()
    {
        StationRegistry.Unregister(this);
        GameEvents.StateChanged -= OnStateChanged;
    }

    // A fresh run (first start, or GameManager.Restart()'s in-place fallback for scenes that aren't in Build
    // Settings) only reloads the scene for Ship; a sandbox restart reuses this same Station instance, so without
    // this it could stay broken or mid-repair from the previous run.
    void OnStateChanged(GameState s)
    {
        if (s != GameState.Part1) return;
        IsBroken = false;
        IsDone = false;
        workProgress = 0f;
        repairProgress = 0f;
        holders.Clear();
        deliverers.Clear();
    }

    public virtual void PrepareForOrder(ActiveOrder order)
    {
        IsDone = false;
        workProgress = 0f;
    }

    /// <summary>Interactor calls this every frame an actor is in range.</summary>
    public void Hold(ICrewActor actor, bool holding) => Mark(holders, actor, holding);

    /// <summary>
    /// CarryItem calls this while its carrier stands here with the item. A delivery only needs the carrier,
    /// even at a two-person station (order 2 delivers the passenger list to the Wheel on the bridge).
    /// </summary>
    public void HoldDelivery(ICrewActor actor, bool inRange) => Mark(deliverers, actor, inRange);

    protected virtual void Update()
    {
        if (IsBroken) TickRepair();
        else TickWork();
        UpdateLight();
    }

    void TickWork()
    {
        if (IsDone) return;
        if (CountFresh(holders) < RequiredHolders && CountFresh(deliverers) == 0) return;

        workProgress += Time.deltaTime;
        if (workProgress >= workDuration)
        {
            IsDone = true;
            OnWorkComplete();
        }
    }

    void TickRepair()
    {
        if (CountFresh(holders) < RepairHolders) return;

        repairProgress += Time.deltaTime;
        if (repairProgress >= repairDuration) Repair();
    }

    protected virtual void OnWorkComplete() { }

    public void Break()
    {
        if (IsBroken) return;
        IsBroken = true;
        repairProgress = 0f;
        holders.Clear();
        deliverers.Clear();
        GameEvents.RaiseStationBroken(Type, null);
    }

    public void Repair()
    {
        if (!IsBroken) return;
        IsBroken = false;
        repairProgress = 0f;
        holders.Clear();
        GameEvents.RaiseStationRepaired(Type, null);
    }

    static void Mark(Dictionary<ICrewActor, int> set, ICrewActor actor, bool on)
    {
        if (actor == null) return;
        if (on) set[actor] = Time.frameCount;
        else set.Remove(actor);
    }

    /// <summary>Counts actors who refreshed this frame or the last one (Update order between scripts varies).</summary>
    static int CountFresh(Dictionary<ICrewActor, int> set)
    {
        int n = 0, frame = Time.frameCount;
        foreach (var lastFrame in set.Values) if (frame - lastFrame <= 1) n++;
        return n;
    }

    void UpdateLight()
    {
        if (statusLight == null) return;
        var color = IsBroken ? new Color(0.25f, 0.08f, 0.08f) : (IsDone ? Color.green : Color.red);
        if (color == shownColor) return;
        shownColor = color;
        var mat = statusLight.material;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * 2f);
    }

    static float SafeDiv(float a, float b) => b <= 0f ? 1f : Mathf.Clamp01(a / b);
}
