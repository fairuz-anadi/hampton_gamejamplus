using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The Captain, speaking his lines out loud. Owner: Samprity. Spawns itself in Ship when Resources/CaptainVoiceBank
/// has clips. Every time a text box starts (CaptainDirector.PageStarted) the matching recording plays through the
/// Voice group, so it sounds like the ship's speaker; the next box (Space skipping ahead included) cuts it off and
/// plays its own. While he speaks, the per-letter typing voice is muted, so the two never talk over each other.
/// Lines said face to face (CaptainTalk) and his slip (CaptainSubtitles.ShowAside) play the same way, the
/// face-to-face ones dry, without the speaker's filter. Boxes with no recording just keep the typing voice.
/// </summary>
public class CaptainNarrator : MonoBehaviour
{
    public static CaptainNarrator Instance { get; private set; }

    [Range(0f, 1f)] public float volume = 1f;

    /// <summary>A recording is playing right now.</summary>
    public bool Speaking => (speaker != null && speaker.isPlaying) || (inPerson != null && inPerson.isPlaying);
    /// <summary>For checks: how many text boxes have a recording.</summary>
    public int ClipCount => exact.Count + patterns.Count;
    /// <summary>For checks: the last text box that found its recording.</summary>
    public static string LastSpoken { get; private set; }

    AudioSource speaker, inPerson;
    readonly Dictionary<string, AudioClip> exact = new Dictionary<string, AudioClip>();
    readonly List<(Regex pattern, AudioClip clip)> patterns = new List<(Regex, AudioClip)>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive || !GameSystems.IsGameplayScene(scene.name) || Instance != null) return;
        var bank = CaptainVoiceBank.Load();
        if (bank == null || bank.entries.Count == 0) return;
        new GameObject("[CaptainNarrator]").AddComponent<CaptainNarrator>();
    }

    void Awake()
    {
        Instance = this;
        var audio = AudioBank.Load();
        speaker = Source("Speaker", audio != null ? audio.voice : null);
        inPerson = Source("InPerson", audio != null ? audio.sfx : null);
        var bank = CaptainVoiceBank.Load();
        if (bank != null)
            foreach (var e in bank.entries)
                if (e != null) Register(e.text, e.clip, e.template);
    }

    /// <summary>Adds a recording for a text box (a template matches any name in place of {seat}).</summary>
    public void Register(string text, AudioClip clip, bool template = false)
    {
        if (clip == null || string.IsNullOrEmpty(text)) return;
        if (template) patterns.Add((new Regex("^" + Regex.Escape(Normal(text)).Replace("\\{seat}", "(.+?)") + "$"), clip));
        else exact[Normal(text)] = clip;
    }

    AudioSource Source(string name, UnityEngine.Audio.AudioMixerGroup group)
    {
        var go = new GameObject(name, typeof(AudioSource));
        go.transform.SetParent(transform, false);
        var s = go.GetComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.outputAudioMixerGroup = group;
        var cat = go.AddComponent<AudioCategory>();
        cat.kind = AudioCategory.Kind.Effects;
        cat.BaseVolume = volume;
        return s;
    }

    void OnEnable() => CaptainDirector.PageStarted += OnPage;
    void OnDisable() => CaptainDirector.PageStarted -= OnPage;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        MuteTyping(false);
    }

    void OnPage(string text) => Play(speaker, text);

    /// <summary>A line said over the ship's speaker (the slip, for one).</summary>
    public static void Say(string text) { if (Instance != null) Instance.Play(Instance.speaker, text); }

    /// <summary>A line said face to face: the same voice, without the speaker's filter.</summary>
    public static void SayInPerson(string text) { if (Instance != null) Instance.Play(Instance.inPerson, text); }

    /// <summary>The face-to-face voice stops (the conversation ended); a speaker line carries on.</summary>
    public static void StopInPerson()
    {
        if (Instance != null) Instance.inPerson.Stop();
    }

    void Play(AudioSource source, string text)
    {
        speaker.Stop();
        inPerson.Stop();
        var clip = Find(text);
        if (clip == null) return;
        source.clip = clip;
        source.Play();
        LastSpoken = text;
    }

    AudioClip Find(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        string key = Normal(text);
        if (exact.TryGetValue(key, out var clip)) return clip;
        foreach (var (pattern, c) in patterns) if (pattern.IsMatch(key)) return c;
        return null;
    }

    void Update()
    {
        MuteTyping(Speaking);
        // A card or the pause menu stops time: his voice waits with his text box, and picks up where it stopped.
        bool paused = Time.timeScale == 0f;
        if (paused == heldForPause) return;
        heldForPause = paused;
        foreach (var s in new[] { speaker, inPerson })
            if (s != null) { if (paused) s.Pause(); else s.UnPause(); }
    }

    bool heldForPause;

    /// <summary>The per-letter typing voice lives on CaptainDirector's object; hush it while the recording talks.</summary>
    static void MuteTyping(bool mute)
    {
        var captain = CaptainDirector.Instance;
        if (captain == null) return;
        bool alwaysMuted = AudioManager.Instance != null && AudioManager.Instance.muteCaptainBlip;
        foreach (var src in captain.GetComponents<AudioSource>()) src.mute = mute || alwaysMuted;
    }

    /// <summary>The same text however it was spaced or wrapped.</summary>
    public static string Normal(string s) => Regex.Replace(s ?? "", @"\s+", " ").Trim();
}
