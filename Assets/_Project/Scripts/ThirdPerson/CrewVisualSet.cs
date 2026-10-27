using UnityEngine;

/// <summary>
/// The crew models for seats 1-4 and their animator, loaded from Resources/CrewVisualSet so Ship needs no scene edit.
/// Owner: Samprity. Made by OpeningSceneBuilder.
/// </summary>
[CreateAssetMenu(menuName = "Port/Crew Visual Set")]
public class CrewVisualSet : ScriptableObject
{
    public GameObject[] crew = new GameObject[4];
    public RuntimeAnimatorController controller;
    public Sprite seatRing;

    static CrewVisualSet cached;
    public static CrewVisualSet Load() => cached != null ? cached : (cached = Resources.Load<CrewVisualSet>("CrewVisualSet"));
}
