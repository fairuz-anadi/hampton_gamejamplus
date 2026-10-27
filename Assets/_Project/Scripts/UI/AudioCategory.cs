using UnityEngine;

/// <summary>
/// Marks an AudioSource as music or an effect, so the Music and Effects sliders scale it. Owner: Samprity.
/// Set the source's volume as usual (or through BaseVolume); this multiplies it by the player's setting.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioCategory : MonoBehaviour
{
    public enum Kind { Music, Effects }

    public Kind kind = Kind.Effects;
    [Range(0f, 1f)] public float baseVolume = 1f;

    AudioSource source;

    /// <summary>The volume before the player's setting; fades change this.</summary>
    public float BaseVolume { get => baseVolume; set { baseVolume = value; Refresh(); } }

    void Awake() => source = GetComponent<AudioSource>();

    void OnEnable()
    {
        GameSettings.Changed += Refresh;
        Refresh();
    }

    void OnDisable() => GameSettings.Changed -= Refresh;

    void Refresh()
    {
        if (source == null) source = GetComponent<AudioSource>();
        source.volume = baseVolume * GameSettings.Volume(kind);
    }
}
