using UnityEngine;

/// <summary>
/// Placeholder interaction sounds, built from simple tones so the game isn't silent before the real SFX arrive.
/// Owner: Rabib. Spawned with the core systems by GameSystems.
///   Station done (green): two-note chime          Station broken: low buzz
///   Station repaired: rising chime                Call for help: whistle
///   Ice warning arrives at the desk: Morse beeps (plan's sound list)
/// Samprity: when the real sounds exist, switch this off with StationSfx.Enabled = false (or remove it in GameSystems).
/// </summary>
public class StationSfx : MonoBehaviour
{
    public static bool Enabled = true;

    static StationSfx instance;

    const int Rate = 44100;

    [SerializeField, Range(0f, 1f)] float volume = 0.35f;

    AudioSource source;
    AudioClip done, broken, repaired, help, morse;

    void Awake()
    {
        instance = this;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        done = Tones("sfx_done", (880f, 0.11f), (1320f, 0.16f));
        repaired = Tones("sfx_repaired", (660f, 0.09f), (880f, 0.09f), (990f, 0.16f));
        broken = Buzz("sfx_broken", 140f, 0.45f);
        help = Slide("sfx_help", 1200f, 1800f, 0.35f);
        morse = Tones("sfx_morse", (800f, 0.07f), (0f, 0.06f), (800f, 0.07f), (0f, 0.06f), (800f, 0.2f));
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void OnEnable()
    {
        GameEvents.StationBroken += OnBroken;
        GameEvents.StationRepaired += OnRepaired;
        GameEvents.HelpCalled += OnHelp;
        GameEvents.IceWarningArrived += OnIceWarning;
    }

    void OnDisable()
    {
        GameEvents.StationBroken -= OnBroken;
        GameEvents.StationRepaired -= OnRepaired;
        GameEvents.HelpCalled -= OnHelp;
        GameEvents.IceWarningArrived -= OnIceWarning;
    }

    /// <summary>Station calls this when its light turns green.</summary>
    public static void PlayDone() => instance?.Play(instance.done);

    void OnBroken(StationType t, ICrewActor by) => Play(broken);
    void OnRepaired(StationType t, ICrewActor by) => Play(repaired);
    void OnHelp(ICrewActor who, StationType t) => Play(help);
    void OnIceWarning(int n) => Play(morse);

    void Play(AudioClip clip)
    {
        if (Enabled && clip != null && source != null) source.PlayOneShot(clip, volume);
    }

    // ---------- Tiny synth ----------

    /// <summary>A run of sine notes (frequency 0 = a short rest), each with a quick fade in and out.</summary>
    static AudioClip Tones(string name, params (float hz, float seconds)[] notes)
    {
        int total = 0;
        foreach (var n in notes) total += Mathf.CeilToInt(n.seconds * Rate);
        var data = new float[total];
        int i = 0;
        foreach (var n in notes)
        {
            int len = Mathf.CeilToInt(n.seconds * Rate);
            for (int s = 0; s < len; s++, i++)
                data[i] = n.hz <= 0f ? 0f : Mathf.Sin(2f * Mathf.PI * n.hz * s / Rate) * Envelope(s, len);
        }
        return Make(name, data);
    }

    static AudioClip Buzz(string name, float hz, float seconds)
    {
        int len = Mathf.CeilToInt(seconds * Rate);
        var data = new float[len];
        for (int s = 0; s < len; s++)
        {
            float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * hz * s / Rate));
            data[s] = square * 0.5f * Envelope(s, len);
        }
        return Make(name, data);
    }

    static AudioClip Slide(string name, float fromHz, float toHz, float seconds)
    {
        int len = Mathf.CeilToInt(seconds * Rate);
        var data = new float[len];
        float phase = 0f;
        for (int s = 0; s < len; s++)
        {
            float hz = Mathf.Lerp(fromHz, toHz, (float)s / len);
            phase += 2f * Mathf.PI * hz / Rate;
            data[s] = Mathf.Sin(phase) * Envelope(s, len);
        }
        return Make(name, data);
    }

    static float Envelope(int s, int len)
    {
        int edge = Mathf.Min(len / 4, Rate / 100); // ~10 ms fades so notes don't click
        if (s < edge) return (float)s / edge;
        if (s > len - edge) return (float)(len - s) / edge;
        return 1f;
    }

    static AudioClip Make(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
