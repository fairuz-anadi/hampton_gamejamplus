using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// The game's opening in MainMenu.unity: a cinematic of under a minute, then the title and menu over the same 3D
/// shot, then Begin Voyage hands over to Ship.unity's third-person camera. Owner: Samprity.
///
///   Boot -> Intro -> Menu -> Starting
///
/// Nothing from the game runs here: GameSystems only spawns the GameManager, orders, meters and bots in Ship, so the
/// intro and menu can't issue orders, start timers or touch the meters. Begin Voyage loads Ship, where the run starts
/// as it always did. The intro plays once per session; coming back to the menu (Results, pause menu) goes straight
/// to the menu shot. Skip intro lands on the same menu shot with nothing started.
///
/// The camera's last key is the exact pose Ship's follow camera starts in (seat 1 on Spawn_1, built by
/// OpeningSceneBuilder), so the fade into gameplay hides a scene load rather than a jump.
/// </summary>
public class OpeningDirector : MonoBehaviour
{
    public enum Phase { Boot, Intro, Menu, Starting }

    [System.Serializable]
    public struct Line
    {
        public float start;
        public string speaker;
        [TextArea] public string text;
        [Tooltip("Optional recording. Drop a clip named Intro_<id> in Audio/Voice and rebuild the opening.")]
        public AudioClip clip;
        public string id;
        [Tooltip("Comes through the ship's speaker: crackle and the speaker icon.")]
        public bool overSpeaker;
    }

    [Header("Scene")]
    public CinematicPath path;
    public Camera cam;
    public OpeningMenu menu;
    [Tooltip("Scene Begin Voyage loads.")]
    public string playScene = "Ship";

    [Header("Timing (seconds)")]
    public float fadeInEnd = 3.2f;
    public float letterboxOutAt = 51.5f;
    public float menuAt = 53f;
    public List<Line> lines = new List<Line>();
    [Tooltip("Lines end this long after the next starts, or after their own reading time.")]
    public float readingSecondsPerWord = 0.36f;

    [Header("Sound")]
    public AudioSource ocean;
    public AudioSource music;
    public AudioSource engine;
    public AudioSource footsteps;
    public AudioSource voice;
    public AudioSource oneShots;
    public AudioClip horn;
    public AudioClip crackle;
    public float[] hornAt = { 1.8f, 15.5f };
    [Tooltip("When the camera goes indoors: ocean drops, the engine room's hum rises.")]
    public float indoorsFrom = 27f, indoorsTo = 33f;

    public static bool IntroSeenThisSession { get; private set; }
    /// <summary>True between Begin Voyage and Ship's camera taking over, so Ship fades in from black.</summary>
    public static bool HandoffPending { get; set; }

    public Phase State { get; private set; } = Phase.Boot;
    public float IntroTime => introTime;

    float introTime;
    float menuShownAt;
    int nextLine;
    int nextHorn;
    float subtitleOffAt = -1f;
    bool begun;

    void Start()
    {
        if (cam == null) cam = Camera.main;
        FreezeClock();
        if (menu != null)
        {
            menu.BeginVoyage += Begin;
            menu.Quit += PauseMenu.Quit;
            menu.SkipIntro += Skip;
        }
        foreach (var src in new[] { ocean, music, engine, footsteps })
            if (src != null) { src.loop = true; SetBase(src, 0f); src.Play(); }

        if (IntroSeenThisSession) { EnterMenu(fromSkip: true); return; }
        IntroSeenThisSession = true;
        State = Phase.Intro;
        introTime = 0f;
        menu?.SetFade(1f);
        menu?.SetLetterbox(1f);
        menu?.ShowSkip(true);
        Pose(0f);
    }

    /// <summary>The run's start time of day, held still: Ship begins from the same light.</summary>
    void FreezeClock()
    {
        var cycle = FindAnyObjectByType<DayNightCycle>();
        if (cycle == null) return;
        cycle.SetPhase(cycle.RunStartPhase);
        cycle.enabled = false;
    }

    void Update()
    {
        switch (State)
        {
            case Phase.Intro: TickIntro(Time.deltaTime); break;
            case Phase.Menu: TickMenu(); break;
        }
    }

    // ---------- Intro ----------

    void TickIntro(float dt)
    {
        introTime += dt;
        Pose(introTime);

        // Black, then the sea.
        menu?.SetFade(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, fadeInEnd, introTime)));
        menu?.SetLetterbox(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(letterboxOutAt, letterboxOutAt + 2f, introTime)));
        Mix(introTime);

        while (nextHorn < hornAt.Length && introTime >= hornAt[nextHorn])
        {
            if (horn != null && oneShots != null) oneShots.PlayOneShot(horn, nextHorn == 0 ? 0.55f : 0.8f);
            nextHorn++;
        }

        while (nextLine < lines.Count && introTime >= lines[nextLine].start) Say(nextLine++);
        if (subtitleOffAt > 0f && introTime >= subtitleOffAt) { menu?.ShowSubtitle("", ""); subtitleOffAt = -1f; }

        if (SkipPressed()) { Skip(); return; }
        if (introTime >= menuAt) EnterMenu(fromSkip: false);
    }

    void Say(int i)
    {
        var l = lines[i];
        bool speakerOpens = l.overSpeaker && (i == 0 || !lines[i - 1].overSpeaker);
        if (speakerOpens && crackle != null && oneShots != null) oneShots.PlayOneShot(crackle, 0.7f);
        if (l.clip != null && voice != null) { voice.Stop(); voice.clip = l.clip; voice.PlayDelayed(l.overSpeaker ? 0.35f : 0f); }
        menu?.ShowSubtitle(l.speaker, l.text, l.overSpeaker);

        // Off when the next line starts, or once there's been time to read it (or hear it).
        float reading = Mathf.Max(2.2f, l.text.Split(' ').Length * readingSecondsPerWord + 1.2f);
        if (l.clip != null) reading = Mathf.Max(reading, l.clip.length + 0.6f);
        float next = i + 1 < lines.Count ? lines[i + 1].start - 0.3f : float.MaxValue;
        subtitleOffAt = Mathf.Min(l.start + reading, next);
    }

    void Pose(float t)
    {
        if (path == null || cam == null) return;
        path.Evaluate(t, out var pos, out var rot, out var fov);
        cam.transform.SetPositionAndRotation(pos, rot);
        cam.fieldOfView = fov;
    }

    /// <summary>Ocean outside, the engine room inside, music under everything.</summary>
    void Mix(float t)
    {
        float inside = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(indoorsFrom, indoorsTo, t));
        float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 4f, t));
        SetBase(ocean, rise * Mathf.Lerp(0.85f, 0.35f, inside));
        SetBase(music, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 9f, t)) * 0.55f);
        SetBase(engine, inside * 0.5f);
        SetBase(footsteps, inside * 0.3f * (1f - Mathf.InverseLerp(48f, 54f, t))); // 4 s later since the promenade run
    }

    static void SetBase(AudioSource src, float v)
    {
        if (src == null) return;
        if (src.TryGetComponent(out AudioCategory c)) c.BaseVolume = v;
        else src.volume = v;
    }

    static bool SkipPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) return true;
        foreach (var pad in Gamepad.all)
            if (pad.startButton.wasPressedThisFrame || pad.buttonSouth.wasPressedThisFrame) return true;
        return false;
    }

    public void Skip()
    {
        if (State != Phase.Intro) return;
        EnterMenu(fromSkip: true);
    }

    // ---------- Menu ----------

    void EnterMenu(bool fromSkip)
    {
        State = Phase.Menu;
        menuShownAt = Time.unscaledTime;
        if (voice != null) voice.Stop();
        Pose(path != null ? path.Duration : 0f);
        Mix(path != null ? path.Duration : 60f);
        SetBase(footsteps, 0f);
        menu?.ShowSkip(false);
        menu?.ShowSubtitle("", "");
        menu?.SetLetterbox(0f);
        if (fromSkip) menu?.FadeTo(0f, 0.9f);
        else menu?.SetFade(0f);
        menu?.ShowMenu(true);
        RefreshCrewStrip();
    }

    void TickMenu()
    {
        // Controllers join with Start (A is "press the button" in the menu); Start again leaves.
        foreach (var pad in Gamepad.all)
            if (pad.startButton.wasPressedThisFrame)
            {
                if (!PlayerSlots.Leave(pad)) PlayerSlots.Join(pad);
                RefreshCrewStrip();
            }
        var kb = Keyboard.current;
        bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || AnyPad(p => p.buttonEast.wasPressedThisFrame);
        if (back) menu?.Back();
    }

    static bool AnyPad(System.Func<Gamepad, bool> pressed)
    {
        foreach (var pad in Gamepad.all) if (pressed(pad)) return true;
        return false;
    }

    void RefreshCrewStrip()
    {
        if (menu == null) return;
        var sb = new System.Text.StringBuilder("CREW    ");
        if (PlayerSlots.Devices.Count == 0) sb.Append(CrewRegistry.SeatName(1)).Append(": you");
        for (int i = 0; i < PlayerSlots.Devices.Count; i++)
        {
            if (i > 0) sb.Append("    ");
            var d = PlayerSlots.Devices[i];
            sb.Append(CrewRegistry.SeatName(i + 1)).Append(": ").Append(d is Keyboard ? "keyboard" : "controller " + (i + 1));
        }
        int free = PlayerSlots.MaxPlayers - Mathf.Max(1, PlayerSlots.Devices.Count);
        if (free > 0) sb.Append("        Friends join with START on a controller");
        sb.Append("    ·    bots fill the other seats");
        menu.SetCrewStrip(sb.ToString());
    }

    // ---------- Begin Voyage ----------

    public void Begin()
    {
        // A short guard so the key that skipped the intro can't also start the voyage.
        if (State != Phase.Menu || begun || Time.unscaledTime - menuShownAt < 0.8f) return;
        begun = true;
        State = Phase.Starting;

        // Whoever pressed Begin plays: the controller if it was one, otherwise the keyboard.
        InputDevice presser = null;
        foreach (var pad in Gamepad.all) if (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame) presser = pad;
        presser ??= Keyboard.current;
        if (presser != null && !PlayerSlots.IsJoined(presser) && PlayerSlots.Devices.Count < PlayerSlots.MaxPlayers) PlayerSlots.Join(presser);

        StartCoroutine(BeginRoutine());
    }

    IEnumerator BeginRoutine()
    {
        menu?.ShowMenu(false);
        yield return new WaitForSecondsRealtime(1.3f); // the deck alone for a moment, menu gone
        var load = SceneManager.LoadSceneAsync(playScene);
        load.allowSceneActivation = false;
        menu?.FadeTo(1f, 0.8f);
        for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / 0.8f;
            SetBase(music, 0.55f * k);
            SetBase(engine, 0.5f * k);
            yield return null;
        }
        HandoffPending = true;
        load.allowSceneActivation = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        IntroSeenThisSession = false;
        HandoffPending = false;
    }

    /// <summary>For checks: jump the intro to a time (Intro phase only).</summary>
    public void DebugSeek(float t)
    {
        if (State != Phase.Intro) return;
        introTime = t;
        while (nextLine < lines.Count && lines[nextLine].start < t) nextLine++;
        while (nextHorn < hornAt.Length && hornAt[nextHorn] < t) nextHorn++;
    }
}
