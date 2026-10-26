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

    readonly HashSet<ICrewActor> holders = new HashSet<ICrewActor>();
    Renderer statusLight;
    float workProgress;
    float repairProgress;

    public abstract StationType Type { get; }
    protected virtual int RequiredHolders => 1;
    protected virtual int RepairHolders => 1;

    public bool IsDone { get; private set; }
    public bool IsBroken { get; private set; }
    public Vector3 Position => transform.position;
    public float Progress01 => IsBroken ? SafeDiv(repairProgress, repairDuration) : SafeDiv(workProgress, workDuration);

    void Awake() => statusLight = GetComponentInChildren<Renderer>();
    void OnEnable() => StationRegistry.Register(this);
    void OnDisable() => StationRegistry.Unregister(this);

    public virtual void PrepareForOrder(ActiveOrder order)
    {
        IsDone = false;
        workProgress = 0f;
    }

    /// <summary>Interactor (and CarryItem, for deliveries) call this every frame an actor is in range.</summary>
    public void Hold(ICrewActor actor, bool holding)
    {
        if (holding) holders.Add(actor);
        else holders.Remove(actor);
    }

    protected virtual void Update()
    {
        if (IsBroken) TickRepair();
        else TickWork();
        UpdateLight();
    }

    void TickWork()
    {
        if (IsDone || holders.Count < RequiredHolders) return;

        workProgress += Time.deltaTime;
        if (workProgress >= workDuration)
        {
            IsDone = true;
            OnWorkComplete();
        }
    }

    void TickRepair()
    {
        if (holders.Count < RepairHolders) return;

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

    void UpdateLight()
    {
        if (statusLight == null) return;
        var color = IsBroken ? new Color(0.25f, 0.08f, 0.08f) : (IsDone ? Color.green : Color.red);
        var mat = statusLight.material;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;
    }

    static float SafeDiv(float a, float b) => b <= 0f ? 1f : Mathf.Clamp01(a / b);
}
