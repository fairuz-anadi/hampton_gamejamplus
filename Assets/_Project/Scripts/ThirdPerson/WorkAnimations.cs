using UnityEngine;

/// <summary>
/// The work loop for each job, so crew don't push an invisible cart at every station. Loaded from
/// Resources/WorkAnimations; built by Port > Build Work Animations (WorkAnimationsBuilder), which makes the clips from
/// the Quaternius library the crew already use, and runs by itself the first time the editor opens without it.
/// CrewVisual plays the right one in the crew animator's work state while someone holds Interact. Owner: Rabib.
/// </summary>
public class WorkAnimations : ScriptableObject
{
    public const string ResourcePath = "WorkAnimations";

    [Header("Stations")]
    [Tooltip("Boiler: scoop coal off the floor and throw it into the firebox. A shovel goes in their hands.")]
    public AnimationClip shovel;
    [Tooltip("Doors: reach up to the lever, haul it down, let it up again.")]
    public AnimationClip lever;
    [Tooltip("Wheel: both hands on the spokes, hand over hand.")]
    public AnimationClip wheel;
    [Tooltip("Wireless: bent over the desk, tapping the Morse key.")]
    public AnimationClip morse;
    [Tooltip("Any station while it's broken: kneel and fix it.")]
    public AnimationClip repair;

    [Header("Bots' chores")]
    [Tooltip("Wiping, stirring, sorting: a hand on the work.")]
    public AnimationClip tap;
    [Tooltip("Lifting things off a shelf or out of a locker.")]
    public AnimationClip pickUp;

    static WorkAnimations cached;
    public static WorkAnimations Load() => cached != null ? cached : (cached = Resources.Load<WorkAnimations>(ResourcePath));

    /// <summary>The loop for working this station now (repair while it's broken). Null: keep the old push.</summary>
    public AnimationClip For(Station station)
    {
        if (station == null) return null;
        if (station.IsBroken) return repair;
        return station.Type switch
        {
            StationType.Boiler => shovel,
            StationType.Doors => lever,
            StationType.Wheel => wheel,
            StationType.Wireless => morse,
            _ => tap,
        };
    }

    /// <summary>The loop for a bot's chore, from its Chore_* marker's name.</summary>
    public AnimationClip ForChore(string spot)
    {
        if (string.IsNullOrEmpty(spot)) return tap;
        if (spot.Contains("Coal")) return shovel;
        if (spot.Contains("Rope")) return lever;   // hauling on a line
        if (spot.Contains("Bunk")) return repair;  // down on one knee, making the bunk
        if (spot.Contains("Shelf") || spot.Contains("Stores") || spot.Contains("Locker") || spot.Contains("Buckets")) return pickUp;
        return tap;                                // stove, sink, table
    }
}
