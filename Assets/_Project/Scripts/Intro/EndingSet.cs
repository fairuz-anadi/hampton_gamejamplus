using UnityEngine;

/// <summary>
/// Everything the ending cinematic needs, loaded from Resources/EndingSet so the Results scene needs no edit.
/// Owner: Samprity. Made by EndingStageBuilder (Port > Samprity > Build Ending Stage).
/// </summary>
[CreateAssetMenu(menuName = "Port/Ending Set")]
public class EndingSet : ScriptableObject
{
    public GameObject deck;
    public GameObject lifeboat;
    public GameObject wreckerBoat;
    public GameObject cove;
    public GameObject bottle;
    public GameObject iceberg;

    [Header("Overboard: the throw (from the Quaternius UAL1 animation library, swapped in for that film only)")]
    [Tooltip("The Captain's two-handed shove.")]
    public AnimationClip shove;
    [Tooltip("The player taking the shove in the chest.")]
    public AnimationClip struck;
    [Tooltip("The player flailing on the way down.")]
    public AnimationClip falling;
    [Tooltip("The player treading water in the ship's wake.")]
    public AnimationClip treading;

    static EndingSet cached;
    public static EndingSet Load() => cached != null ? cached : (cached = Resources.Load<EndingSet>("EndingSet"));
}
