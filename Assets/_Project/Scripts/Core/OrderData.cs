using UnityEngine;

/// <summary>
/// One Captain order. The 12 orders live in Assets/_Project/Data/Resources/Orders and are
/// issued in file-name order (01_..., 02_...). Owner: Anadi. Text is written by Riham.
/// </summary>
[CreateAssetMenu(menuName = "Port/Order", fileName = "00_NewOrder")]
public class OrderData : ScriptableObject
{
    [Tooltip("1 = Calm, 2 = Off, 3 = Truth")]
    [Range(1, 3)] public int part = 1;

    [TextArea(2, 4)] public string captainLine;

    [Tooltip("Optional recorded line. Subtitles still use captainLine.")]
    public AudioClip voice;

    [Tooltip("Where the work happens. For carry orders this is where the item is picked up.")]
    public StationType station;

    [Tooltip("Carry orders: pick up at 'station', deliver to 'deliverTo'.")]
    public bool isCarry;
    public StationType deliverTo;

    public OrderKind kind;

    [Tooltip("Wheel, coal chute and door levers need two crew.")]
    public bool needsTwo;

    [Min(5f)] public float timer = 30f;

    [Tooltip("Obeying locks the lifeboat store (order 9). Any two crew can reopen it at the Door levers.")]
    public bool locksLifeboats;

    [Header("Meter changes per choice")]
    public MeterDelta obey;
    public MeterDelta fake;
    public MeterDelta refuse;
    public MeterDelta expired;

    [Tooltip("Short description for the results screen, e.g. 'File the ice warning unread'.")]
    public string resultsLine;

    /// <summary>The station that must be green for a report to count as honest.</summary>
    public StationType CheckStation => isCarry ? deliverTo : station;

    public bool IsOff => kind != OrderKind.Normal;

    public MeterDelta DeltaFor(Choice choice) => choice switch
    {
        Choice.Obey => obey,
        Choice.Fake => fake,
        Choice.Refuse => refuse,
        _ => expired,
    };
}
