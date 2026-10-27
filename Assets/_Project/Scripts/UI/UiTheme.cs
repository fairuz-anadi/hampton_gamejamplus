using UnityEngine;

/// <summary>
/// The game's type and ornaments for UI built in code (the how-to-play card), loaded from Resources/UiTheme.
/// Owner: Samprity. Made by OpeningSceneBuilder from the fonts and sprites in Art/UI.
/// </summary>
public class UiTheme : ScriptableObject
{
    public Font title, body, italic, typewriter;
    public Sprite diamond, keycap;

    public static readonly Color Cream = new Color(0.97f, 0.93f, 0.84f);
    public static readonly Color Brass = new Color(0.86f, 0.68f, 0.34f);
    public static readonly Color DimCream = new Color(0.78f, 0.74f, 0.66f, 0.9f);

    static UiTheme cached;
    public static UiTheme Load() => cached != null ? cached : (cached = Resources.Load<UiTheme>("UiTheme"));
}
