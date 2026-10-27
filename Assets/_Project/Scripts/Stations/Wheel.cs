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
}
