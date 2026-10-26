using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A stand-in station for testing bots before Rabib's real stations exist. Owner: Riham.
/// Implements the same ICrewWorkStation contract: work moves progress (two workers for needsTwo orders),
/// working a broken station repairs it, and the cube turns green when done, red when not, dark red when broken.
/// Don't use it in Ship.unity.
/// </summary>
public class SandboxStation : MonoBehaviour, ICrewWorkStation
{
    [SerializeField] StationType type;
    [SerializeField] float workSeconds = 4f;
    [SerializeField] float repairSeconds = 3f;
    [SerializeField] float interactRange = 1.8f;

    readonly HashSet<ICrewActor> workers = new HashSet<ICrewActor>();
    float progress;
    float repairProgress;
    bool needsTwo;
    Renderer rend;

    public StationType Type => type;
    public bool IsDone { get; private set; } = true;
    public bool IsBroken { get; private set; }
    public Vector3 Position => transform.position;
    public float InteractRange => interactRange;
    public int WorkerCount => workers.Count;

    public void Configure(StationType t) => type = t;

    void Awake() => rend = GetComponent<Renderer>();

    void OnEnable() => StationRegistry.Register(this);

    void OnDisable() => StationRegistry.Unregister(this);

    public void PrepareForOrder(ActiveOrder order)
    {
        IsDone = false;
        progress = 0f;
        needsTwo = order.Data.needsTwo;
    }

    public void BeginWork(ICrewActor actor) => workers.Add(actor);

    public void EndWork(ICrewActor actor) => workers.Remove(actor);

    public void Sabotage(ICrewActor actor)
    {
        if (IsBroken) return;
        IsBroken = true;
        repairProgress = 0f;
        GameEvents.RaiseStationBroken(type, actor);
    }

    void Update()
    {
        // Drop anyone who walked away without calling EndWork.
        workers.RemoveWhere(w => (w is Object o && o == null) || BotMover.FlatDistance(w.Position, Position) > interactRange + 0.5f);

        if (IsBroken)
        {
            if (workers.Count > 0) repairProgress += Time.deltaTime;
            if (repairProgress >= repairSeconds)
            {
                IsBroken = false;
                ICrewActor by = null;
                foreach (var w in workers) { by = w; break; }
                GameEvents.RaiseStationRepaired(type, by);
            }
        }
        else if (!IsDone)
        {
            int needed = needsTwo ? 2 : 1;
            if (workers.Count >= needed) progress += Time.deltaTime;
            if (progress >= workSeconds) IsDone = true;
        }

        if (rend != null)
            rend.material.color = IsBroken ? new Color(0.35f, 0.05f, 0.05f) : IsDone ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.9f, 0.2f, 0.2f);
    }
}
