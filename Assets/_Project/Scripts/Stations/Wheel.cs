using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2-person wheel. Owner: Rabib.
/// For orders it's a normal two-holder station. During the iceberg finale IcebergSequence opens steering:
/// anyone holding Interact here steers, one person is enough, and this runs apart from the order's hold count.
/// </summary>
public class Wheel : Station
{
    // Actor -> (last frame they steered, their input). Same "fresh this frame or last" rule as Station.Hold.
    readonly Dictionary<ICrewActor, (int frame, float input)> steerers = new Dictionary<ICrewActor, (int, float)>();

    [Header("Iceberg wheel spin (visual only)")]
    [SerializeField] bool spinEnabled = true;
    [Tooltip("The rim/hub/spokes to rotate. Found automatically as the child named \"Wheel\" (ShipDeckBuilder's " +
             "Station_Wheel/Wheel). Set this only if that search fails for a particular scene.")]
    [SerializeField] Transform wheelMeshOverride;
    [Tooltip("Degrees the wheel visually turns at full steering input.")]
    [SerializeField] float maxSpinDegrees = 120f;
    [SerializeField] float turnSmoothTime = 0.12f;
    [SerializeField] float restSmoothTime = 0.5f;

    bool wheelMeshReady;
    Transform wheelMesh;
    Quaternion wheelMeshRest = Quaternion.identity;
    float spinAngle, spinVelocity;

    public override StationType Type => StationType.Wheel;
    protected override int RequiredHolders => 2;

    /// <summary>Set by IcebergSequence while the finale runs. Interactor only steers when this is on.</summary>
    public bool SteeringOpen { get; set; }

    /// <summary>Interactor calls this every frame a crew member holds Interact here while steering is open.</summary>
    public void Steer(ICrewActor actor, float input)
    {
        if (actor == null || !SteeringOpen || IsBroken) return;
        steerers[actor] = (Time.frameCount, Mathf.Clamp(input, -1f, 1f));
    }

    /// <summary>Sum of everyone's steering this frame (inputs inside the deadzone count as 0), and how many are at the wheel.</summary>
    public float SteerSum(float deadzone, out int count)
    {
        float sum = 0f;
        count = 0;
        if (IsBroken) return 0f;
        int frame = Time.frameCount;
        foreach (var s in steerers.Values)
        {
            if (frame - s.frame > 1) continue;
            count++;
            if (Mathf.Abs(s.input) > deadzone) sum += s.input;
        }
        return sum;
    }

    public void ClearSteering() => steerers.Clear();

    // Station.Awake() isn't virtual (it finds the status light), so this deliberately avoids its own Awake() —
    // declaring one here would hide the base one and silently skip that setup. The mesh lookup is lazy instead,
    // on first Update(), which IS virtual and already chains to base.Update() below.
    protected override void Update()
    {
        base.Update();
        if (!spinEnabled) return;

        if (!wheelMeshReady)
        {
            wheelMeshReady = true;
            wheelMesh = wheelMeshOverride != null ? wheelMeshOverride : transform.Find("Wheel");
            if (wheelMesh != null) wheelMeshRest = wheelMesh.localRotation;
        }
        if (wheelMesh == null) return;

        float input = Mathf.Clamp(SteerSum(0.05f, out _), -1f, 1f);
        float target = input * maxSpinDegrees;
        float smooth = Mathf.Abs(input) > 0.01f ? turnSmoothTime : restSmoothTime;
        spinAngle = Mathf.SmoothDampAngle(spinAngle, target, ref spinVelocity, smooth);
        wheelMesh.localRotation = wheelMeshRest * Quaternion.Euler(0f, 0f, spinAngle);
    }
}
