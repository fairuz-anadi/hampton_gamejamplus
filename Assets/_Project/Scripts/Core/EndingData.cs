using UnityEngine;

/// <summary>
/// The text for one ending. The 3 endings live in Assets/_Project/Data/Resources/Endings. Owner: Anadi. Text is written by Riham.
/// </summary>
[CreateAssetMenu(menuName = "Port/Ending", fileName = "NewEnding")]
public class EndingData : ScriptableObject
{
    public Ending ending;
    public string title;
    [TextArea(3, 8)] public string body;
    [Tooltip("The quiet line shown at the very end, e.g. the real history note.")]
    [TextArea(2, 4)] public string closingLine;

    /// <summary>The asset for an ending, or null if it's missing.</summary>
    public static EndingData For(Ending ending)
    {
        foreach (var e in Resources.LoadAll<EndingData>("Endings"))
            if (e.ending == ending) return e;
        return null;
    }
}
