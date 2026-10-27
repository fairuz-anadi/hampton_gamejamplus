using UnityEngine;

/// <summary>
/// Player settings, saved in PlayerPrefs and applied at launch. Owner: Samprity.
///   Master volume   AudioListener.volume (everything)
///   Music / Effects multiply AudioSources tagged with an AudioCategory
///   Quality         Unity's quality levels (Project Settings > Quality)
///   Fullscreen      Screen.fullScreen (standalone builds)
///   Camera turn     how quickly the third-person camera swings behind the character (there is no mouse look)
/// </summary>
public static class GameSettings
{
    const string Prefix = "Port.Settings.";

    public static event System.Action Changed;

    public static float Master { get => Get("Master", 0.9f); set => Set("Master", value); }
    public static float Music { get => Get("Music", 0.7f); set => Set("Music", value); }
    public static float Effects { get => Get("Effects", 0.9f); set => Set("Effects", value); }
    /// <summary>0 = lazy, 1 = snappy.</summary>
    public static float CameraTurn { get => Get("CameraTurn", 0.5f); set => Set("CameraTurn", value); }

    public static int Quality
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Quality", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
        set { PlayerPrefs.SetInt(Prefix + "Quality", Mathf.Clamp(value, 0, QualitySettings.names.Length - 1)); Apply(); }
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(Prefix + "Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        set { PlayerPrefs.SetInt(Prefix + "Fullscreen", value ? 1 : 0); Apply(); }
    }

    public static float Volume(AudioCategory.Kind kind) => kind == AudioCategory.Kind.Music ? Music : Effects;

    static float Get(string key, float fallback) => PlayerPrefs.GetFloat(Prefix + key, fallback);

    static void Set(string key, float value)
    {
        PlayerPrefs.SetFloat(Prefix + key, Mathf.Clamp01(value));
        Apply();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyAtLaunch() => Apply();

    public static void Apply()
    {
        AudioListener.volume = Master;
        if (QualitySettings.GetQualityLevel() != Quality) QualitySettings.SetQualityLevel(Quality, true);
#if !UNITY_EDITOR
        if (Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
#endif
        Changed?.Invoke();
    }

    public static void Save() => PlayerPrefs.Save();

    /// <summary>The follow camera's swing settings for the current Camera turn value.</summary>
    public static void ApplyTo(AutoFollowCamera cam)
    {
        if (cam == null) return;
        float t = CameraTurn;
        cam.rotationSmoothTime = Mathf.Lerp(1.1f, 0.25f, t);
        cam.recenterSpeed = Mathf.Lerp(90f, 260f, t);
    }
}
