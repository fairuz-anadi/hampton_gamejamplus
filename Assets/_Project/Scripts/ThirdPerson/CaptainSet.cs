using UnityEngine;

/// <summary>
/// The Captain's body for Ship: his prefab (built by CaptainCharacterBuilder) with its own animator, loaded from
/// Resources/CaptainSet so Ship needs no scene edit. Owner: Samprity.
/// The animator's states, played by name: Formal (sober walk), Locomotion (ordinary walk), Talk, Stumble, Flinch.
/// </summary>
[CreateAssetMenu(menuName = "Port/Captain Set")]
public class CaptainSet : ScriptableObject
{
    public GameObject prefab;

    static CaptainSet cached;
    public static CaptainSet Load() => cached != null ? cached : (cached = Resources.Load<CaptainSet>("CaptainSet"));
}
