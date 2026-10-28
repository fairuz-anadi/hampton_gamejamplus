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
    [Tooltip("The Captain's bottle as a clue: clear glass, empty, the cork still sealed. ClueCloseUp dresses the bridge bottle with it.")]
    public GameObject clueBottle;
    [Tooltip("His face for the dialog boxes: a round badge rendered from the prefab (Port > Samprity > Render Captain Portrait).")]
    public Sprite portrait;

    static CaptainSet cached;
    public static CaptainSet Load() => cached != null ? cached : (cached = Resources.Load<CaptainSet>("CaptainSet"));
}
