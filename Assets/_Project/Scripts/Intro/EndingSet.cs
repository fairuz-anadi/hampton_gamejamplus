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

    static EndingSet cached;
    public static EndingSet Load() => cached != null ? cached : (cached = Resources.Load<EndingSet>("EndingSet"));
}
