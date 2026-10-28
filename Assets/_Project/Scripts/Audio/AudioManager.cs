using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The voyage's sound: ship ambience, music per part, footsteps, station and signal sounds, the Captain's speaker
/// and the finale. Owner: Samprity. Spawns itself in Ship (and gameplay sandboxes) and Results, listens to
/// GameEvents and the Captain/Stoker/muster events, and never drives game logic.
///   Clips and mixer come from Resources/AudioBank (Port > Samprity > Build Audio). Without the bank nothing spawns
///   and Rabib's StationSfx placeholder tones keep playing.
///   Mixer snapshots: Calm / Off / Truth per part, Iceberg for the finale, Aftermath after the impact and in Results.
///   Volumes still follow the Music / Effects sliders through AudioCategory, Master through the AudioListener.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Tooltip("Seconds for ambience and music to settle into a new part.")]
    [Min(0.1f)] public float partFade = 4f;
    [Tooltip("Music and passengers drop to this while the Captain speaks.")]
    [Range(0f, 1f)] public float duckUnderVoice = 0.6f;
    [Tooltip("Silences CaptainDirector's beep-per-letter voice; the speaker crackle still plays while he talks.")]
    public bool muteCaptainBlip = false;

    AudioBank bank;
    bool resultsOnly;

    /// <summary>A looping source whose volume eases toward a target.</summary>
    class Layer
    {
        public AudioSource source;
        public AudioCategory category;
        public float target, current, rate = 0.25f;
        public bool started;
    }

    Layer sea, deck, storm, wind, rigging, creak, engine, boiler, passengers, ballroom, pumps, flood;
    Layer waltz, sustained, drone, crackle;
    Layer shovel, wheel, morse;
    readonly List<Layer> layers = new List<Layer>(24);

    AudioSource[] pool;
    int poolNext;
    AudioSource ui2D, voice2D;

    int part = 1;
    GameState state = GameState.Menu;
    bool speaking, impacted;
    float nextGull, nextRepairClank, iceFrom = -1f;
    int lastTickSecond = -1;
    readonly Dictionary<ICrewActor, (Vector3 pos, float travelled)> walkers = new Dictionary<ICrewActor, (Vector3, float)>();
    readonly Dictionary<StationType, (bool done, int holders)> stationsSeen = new Dictionary<StationType, (bool, int)>();
    IcebergSequence iceberg;
    readonly HashSet<AudioSource> routedCaptain = new HashSet<AudioSource>();

    // ---------- Spawning ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        bool results = scene.name == "Results";
        if (!results && !GameSystems.IsGameplayScene(scene.name)) return;
        if (FindAnyObjectByType<AudioManager>() != null) return;
        var bank = AudioBank.Load();
        if (bank == null) return;
        new GameObject("[Audio]").AddComponent<AudioManager>().Setup(bank, results);
    }

    void Setup(AudioBank bank, bool resultsOnly)
    {
        this.bank = bank;
        this.resultsOnly = resultsOnly;
        StationSfx.Enabled = false; // the real sounds replace the placeholder tones

        pool = new AudioSource[16];
        for (int i = 0; i < pool.Length; i++)
        {
            var go = new GameObject("OneShot3D");
            go.transform.SetParent(transform, false);
            pool[i] = go.AddComponent<AudioSource>();
            pool[i].playOnAwake = false;
            pool[i].rolloffMode = AudioRolloffMode.Linear;
            pool[i].minDistance = 2f;
            pool[i].maxDistance = 30f;
            pool[i].dopplerLevel = 0f;
        }
        ui2D = Source2D("Signals", bank.sfx);
        voice2D = Source2D("Speaker", bank.voice);

        sea = Loop("Sea", bank.seaHull, bank.ambience);
        if (resultsOnly)
        {
            // Near silence for the ending: only the sea, far off.
            Snapshot("Aftermath", 0f);
            sea.target = 0.5f;
            return;
        }

        deck = Loop("Deck", bank.deckAmbience, bank.ambience);
        storm = Loop("Storm", bank.seaStorm, bank.ambience);
        wind = Loop("Wind", bank.wind, bank.ambience);
        rigging = Loop("Rigging", bank.rigging, bank.ambience);
        creak = Loop("HullCreak", bank.hullCreak, bank.ambience);
        passengers = Loop("Passengers", bank.passengers, bank.ambience);
        ballroom = Loop("Ballroom", bank.ballroom, bank.ambience);
        flood = Loop("Flood", bank.flood, bank.sfx);
        engine = Loop("Engine", bank.engine, bank.ambience, spatial: 0.7f, maxDistance: 45f);
        boiler = Loop("BoilerRoar", bank.boilerRoar, bank.stations, spatial: 1f, maxDistance: 22f);
        pumps = Loop("Pumps", bank.pumps, bank.stations, spatial: 1f, maxDistance: 25f);
        shovel = Loop("Shovel", bank.shovelLoop, bank.stations, spatial: 1f, maxDistance: 20f);
        wheel = Loop("Wheel", bank.wheelCreak, bank.stations, spatial: 1f, maxDistance: 20f);
        morse = Loop("Morse", bank.morseLoop, bank.stations, spatial: 1f, maxDistance: 18f);
        crackle = Loop("SpeakerCrackle", bank.speakerCrackle, bank.voice);
        waltz = Loop("Waltz", bank.waltz, bank.music, AudioCategory.Kind.Music);
        sustained = Loop("Sustained", bank.sustained, bank.music, AudioCategory.Kind.Music);
        drone = Loop("Drone", bank.drone, bank.music, AudioCategory.Kind.Music);

        foreach (var l in new[] { shovel, wheel, morse, crackle }) l.rate = 2f; // work sounds follow the hands quickly
        flood.rate = 0.6f;

        nextGull = Time.time + Random.Range(6f, 14f);
        ApplyPart(GameManager.Instance != null && GameManager.Instance.Part > 0 ? GameManager.Instance.Part : 1, 0f);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        StationSfx.Enabled = true;
    }

    void OnEnable()
    {
        GameEvents.StateChanged += OnStateChanged;
        GameEvents.PartChanged += OnPartChanged;
        GameEvents.OrderIssued += OnOrderIssued;
        GameEvents.OrderResolved += OnOrderResolved;
        GameEvents.FakeNoticed += OnFakeNoticed;
        GameEvents.StationBroken += OnStationBroken;
        GameEvents.StationRepaired += OnStationRepaired;
        GameEvents.HelpCalled += OnHelpCalled;
        GameEvents.IceWarningArrived += OnIceWarningArrived;
        GameEvents.IceWarningDelivered += OnIceWarningDelivered;
        StokerBrain.StaticBurst += OnStaticBurst;
        StokerBrain.Sabotaged += OnSabotaged;
        MusterVote.VoteStarted += OnVoteStarted;
    }

    void OnDisable()
    {
        GameEvents.StateChanged -= OnStateChanged;
        GameEvents.PartChanged -= OnPartChanged;
        GameEvents.OrderIssued -= OnOrderIssued;
        GameEvents.OrderResolved -= OnOrderResolved;
        GameEvents.FakeNoticed -= OnFakeNoticed;
        GameEvents.StationBroken -= OnStationBroken;
        GameEvents.StationRepaired -= OnStationRepaired;
        GameEvents.HelpCalled -= OnHelpCalled;
        GameEvents.IceWarningArrived -= OnIceWarningArrived;
        GameEvents.IceWarningDelivered -= OnIceWarningDelivered;
        StokerBrain.StaticBurst -= OnStaticBurst;
        StokerBrain.Sabotaged -= OnSabotaged;
        MusterVote.VoteStarted -= OnVoteStarted;
    }

    /// <summary>True while the real speaker static plays, so CaptainSubtitles skips its placeholder crackle.</summary>
    public bool HandlesStatic => bank != null && bank.staticBurst != null;

    // For AudioChecks.
    public bool ResultsOnly => resultsOnly;
    public bool Impacted => impacted;
    public int StepsPlayed { get; private set; }
    public string CurrentSnapshot { get; private set; }

    // ---------- Story beats ----------

    void OnPartChanged(int p)
    {
        if (p <= 3) ApplyPart(p, partFade); // 4 = the iceberg, which OnStateChanged sets up
    }

    void OnStateChanged(GameState s)
    {
        state = s;
        if (s == GameState.Part1) { impacted = false; iceFrom = -1f; ApplyPart(1, 1f); }
        if (s == GameState.Iceberg) BeginIceberg();
    }

    /// <summary>The ambience and music for a story part (1 Calm, 2 Off, 3 Truth).</summary>
    void ApplyPart(int p, float fade)
    {
        if (resultsOnly || sea == null) return;
        part = Mathf.Clamp(p, 1, 3);
        Snapshot(part == 1 ? "Calm" : part == 2 ? "Off" : "Truth", fade);
        flood.target = 0f;
        drone.target = 0f;
        // No music in the story parts (her call, 28 Oct): no waltz, no Part 3 cello. Only the ship's own sounds.
        waltz.target = 0f;
        sustained.target = 0f;
        storm.target = part == 1 ? 0f : part == 2 ? 0.12f : 0.35f;
        passengers.target = part == 1 ? 0.3f : part == 2 ? 0.15f : 0f; // the passengers fall silent by Part 3
        ballroom.target = part == 1 ? 0.14f : part == 2 ? 0.06f : 0f;
        creak.target = part == 1 ? 0.35f : part == 2 ? 0.45f : 0.55f;
        deck.target = 0.4f;
        engine.target = 0.75f;
        boiler.target = 0.35f;
        // "Keep full speed": the engine works harder each part.
        engine.source.pitch = part == 1 ? 1f : part == 2 ? 1.05f : 1.1f;
        if (fade > 0f && p == 2) PlayLater(bank.horn, 2.5f, 0.45f);
    }

    void BeginIceberg()
    {
        if (resultsOnly) return;
        iceberg = FindAnyObjectByType<IcebergSequence>();
        iceFrom = Time.time;
        Snapshot("Iceberg", 1.5f);
        PlayUi(bank.lookoutBell, 0.9f);
        PlayLater(bank.horn, 2.2f, 0.7f);
        waltz.target = 0f;
        sustained.target = 0.15f;
        passengers.target = 0f;
        ballroom.target = 0f;
        storm.target = 0.6f;
        drone.target = 0.2f;
        drone.rate = 1f;
        drone.source.pitch = 0.85f;
    }

    void Impact(float damage)
    {
        impacted = true;
        float k = Mathf.Lerp(0.6f, 1f, damage);
        PlayUi(bank.impactBoom, k);
        PlayUi(bank.impactWood, k * 0.9f);
        PlayUi(bank.impactIce, k * 0.8f);
        // Then near silence: everything drops away except the water coming in.
        foreach (var l in layers) { l.target = 0f; l.rate = 0.5f; }
        flood.target = 0.35f + 0.4f * damage;
        sea.target = 0.25f;
        Snapshot("Aftermath", 2f);
    }

    // ---------- Orders and signals ----------

    void OnOrderIssued(ActiveOrder o) => PlayUi(bank.orderBell, 0.55f);

    void OnOrderResolved(ActiveOrder o, Choice choice)
    {
        bool human = o.Assignee != null && !o.Assignee.IsBot;
        if (choice == Choice.Obey) PlayUi(bank.success, human ? 0.6f : 0.3f);
        else if (choice == Choice.Refuse) PlayVoice(bank.refuseHm, 0.8f); // the Captain's displeased "hm"
    }

    void OnFakeNoticed(ActiveOrder o) => PlayVoice(bank.refuseHm, 0.9f, 0.9f);

    void OnStationBroken(StationType t, ICrewActor by)
    {
        var at = StationPos(t);
        PlayAt(bank.brokenAlarm, at, 0.7f, bank.sfx, spatial: 0.6f);
        PlayAt(bank.sabotageHiss, at, 0.6f, bank.stations);
    }

    void OnStationRepaired(StationType t, ICrewActor by)
    {
        var at = StationPos(t);
        PlayAt(bank.clank, at, 0.8f, bank.stations);
        PlayAt(bank.success, at, 0.45f, bank.sfx, spatial: 0.5f, pitch: 1.12f);
    }

    void OnHelpCalled(ICrewActor who, StationType t) =>
        PlayAt(bank.helpWhistle, who != null ? who.Position : StationPos(t), 0.75f, bank.sfx, spatial: 0.55f);

    void OnIceWarningArrived(int n) => PlayAt(bank.morseBurst, StationPos(StationType.Wireless), 0.85f, bank.stations, spatial: 0.5f);

    void OnIceWarningDelivered(int n) => PlayUi(bank.paper, 0.6f);

    void OnStaticBurst(StokerBrain _) => PlayVoice(bank.staticBurst, 0.75f);

    void OnSabotaged(StokerBrain stoker, StokerBrain.SabotageKind kind, int detail)
    {
        if (kind == StokerBrain.SabotageKind.Jam && stoker != null) PlayAt(bank.sabotageHiss, stoker.transform.position, 0.8f, bank.stations);
    }

    void OnVoteStarted(int callerSeat)
    {
        var bell = GameObject.Find("MusterBell");
        if (bell != null) PlayAt(bank.musterBell, bell.transform.position, 0.9f, bank.sfx, spatial: 0.4f);
        else PlayUi(bank.musterBell, 0.8f);
    }

    // ---------- Every frame ----------

    void Update()
    {
        if (bank == null) return;
        if (!resultsOnly)
        {
            RouteCaptain();
            Speaker();
            RoomMix();
            Stations();
            Footsteps();
            Timers();
            Gulls();
            Finale();
            // Part 2's "off" waltz: the same loop, slightly flat and wavering like a warped record.
            waltz.source.pitch = part == 2 ? 0.965f + 0.008f * Mathf.Sin(Time.time * 0.7f) : 1f;
        }
        foreach (var l in layers) Ease(l);
    }

    /// <summary>
    /// Anything on the Captain's object goes through the Voice group, his per-letter blip included (back on, and
    /// louder, so the typing is heard over the ship; muteCaptainBlip silences it again).
    /// </summary>
    void RouteCaptain()
    {
        var captain = CaptainDirector.Instance;
        if (captain == null) return;
        foreach (var src in captain.GetComponents<AudioSource>())
        {
            if (!routedCaptain.Add(src)) continue;
            src.outputAudioMixerGroup = bank.voice;
            src.mute = muteCaptainBlip;
        }
    }

    /// <summary>The speaker clicks on and hisses while he talks, and clicks off after.</summary>
    void Speaker()
    {
        bool now = CaptainDirector.Instance != null && CaptainDirector.Instance.IsSpeaking;
        if (now != speaking)
        {
            speaking = now;
            PlayVoice(now ? bank.speakerOn : bank.speakerOff, 0.7f);
        }
        crackle.target = speaking ? (part == 1 ? 0.25f : part == 2 ? 0.33f : 0.42f) : 0f;
    }

    /// <summary>Wind and sea come up outdoors, the engine and furnace own the stokehold.</summary>
    void RoomMix()
    {
        if (impacted) return;
        string room = "Corridor";
        foreach (var c in CrewRegistry.All)
            if (!c.IsBot) { room = RoomVolume.RoomAt(c.Position); break; }
        bool outside = room == "Bow" || room == "Deck";
        bool stokehold = room == "Boiler";

        float weather = state == GameState.Iceberg ? 0.6f : part == 1 ? 0.2f : part == 2 ? 0.32f : 0.45f;
        wind.target = weather * (outside ? 1.5f : stokehold ? 0.35f : 0.8f);
        rigging.target = weather * (outside ? 0.9f : 0.3f);
        sea.target = outside ? 0.75f : stokehold ? 0.35f : 0.55f;

        var boilerStation = StationRegistry.Get(StationType.Boiler);
        if (boilerStation != null)
        {
            engine.source.transform.position = boilerStation.Position + Vector3.down * 0.5f;
            boiler.source.transform.position = boilerStation.Position;
            pumps.source.transform.position = boilerStation.Position + Vector3.forward * 3f;
        }
        // Below half Safety the pumps strain, so players hear the ship is in trouble.
        pumps.target = MeterSystem.Instance != null && MeterSystem.Instance.ShipSafety < 50 ? 0.55f : 0f;
    }

    /// <summary>Work sounds while hands are on a station, plus the clunk, door and paper when it's finished.</summary>
    void Stations()
    {
        shovel.target = wheel.target = morse.target = 0f;
        bool repairing = false;
        foreach (var s in StationRegistry.All)
        {
            if (!(s is Station st)) continue;
            int holders = st.ActiveHolders;
            bool working = holders > 0 && !st.IsDone && !st.IsBroken;
            repairing |= holders > 0 && st.IsBroken;

            switch (st.Type)
            {
                case StationType.Boiler:
                    if (working) { shovel.target = 0.8f; shovel.source.transform.position = st.Position; }
                    boiler.target = working ? 0.8f : impacted ? 0f : 0.35f;
                    break;
                case StationType.Wheel:
                    int steerers = 0;
                    float steer = st is Wheel w && w.SteeringOpen ? Mathf.Abs(w.SteerSum(0.2f, out steerers)) : 0f;
                    if (working || steer > 0f)
                    {
                        wheel.target = 0.7f;
                        wheel.source.transform.position = st.Position;
                        wheel.source.pitch = 1f + 0.15f * Mathf.Clamp01(steer);
                    }
                    break;
                case StationType.Wireless:
                    if (working) { morse.target = 0.5f; morse.source.transform.position = st.Position; }
                    break;
            }

            stationsSeen.TryGetValue(st.Type, out var before);
            if (holders > 0 && before.holders == 0 && st.Type == StationType.Doors && !st.IsDone && !st.IsBroken)
                PlayAt(bank.lever, st.Position, 0.8f, bank.stations);
            if (st.IsDone && !before.done)
            {
                if (st.Type == StationType.Doors) PlayAt(bank.doorClose, st.Position, 0.9f, bank.stations);
                else if (st.Type == StationType.Wireless) PlayAt(bank.paper, st.Position, 0.7f, bank.stations);
                else if (st.Type == StationType.Boiler) PlayAt(bank.clank, st.Position, 0.7f, bank.stations, pitch: 0.8f);
                PlayAt(bank.success, st.Position, 0.35f, bank.sfx, spatial: 0.6f, pitch: 1.2f);
            }
            stationsSeen[st.Type] = (st.IsDone, holders);

            if (repairing && Time.time >= nextRepairClank && holders > 0 && st.IsBroken)
            {
                nextRepairClank = Time.time + Random.Range(0.55f, 0.9f);
                PlayAt(bank.clank, st.Position, 0.6f, bank.stations, pitch: Random.Range(0.85f, 1.15f));
            }
        }
    }

    /// <summary>Every crew member's feet, from how far they've moved: wood on deck, metal plates in the stokehold.</summary>
    void Footsteps()
    {
        foreach (var c in CrewRegistry.All)
        {
            var pos = c.Position;
            if (!walkers.TryGetValue(c, out var w)) { walkers[c] = (pos, 0f); continue; }
            var d = pos - w.pos;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist > 3f) dist = 0f; // a teleport, not a walk
            float speed = dist / Mathf.Max(Time.deltaTime, 0.0001f);
            float travelled = speed > 0.4f ? w.travelled + dist : 0f;
            float stride = Mathf.Lerp(0.75f, 1.05f, Mathf.InverseLerp(1.5f, 3.4f, speed));
            if (travelled >= stride)
            {
                travelled -= stride;
                var clips = RoomVolume.RoomAt(pos) == "Boiler" ? bank.stepsMetal : bank.stepsWood;
                StepsPlayed++;
                if (clips.Length > 0)
                    PlayAt(clips[Random.Range(0, clips.Length)], pos, c.IsBot ? 0.5f : 0.6f, bank.footsteps,
                        spatial: c.IsBot ? 1f : 0.8f, pitch: Random.Range(0.92f, 1.08f));
            }
            walkers[c] = (pos, travelled);
        }
    }

    /// <summary>A clock tick for each of the last five seconds of a player's order.</summary>
    void Timers()
    {
        var director = OrderDirector.Instance;
        int second = -1;
        if (director != null)
            foreach (var o in director.Active)
                if (!o.IsResolved && o.Assignee != null && !o.Assignee.IsBot && o.TimeLeft > 0f && o.TimeLeft <= 5f)
                    second = Mathf.Max(second, Mathf.CeilToInt(o.TimeLeft));
        if (second > 0 && second != lastTickSecond) PlayUi(bank.tick, 0.6f, second <= 2 ? 1.15f : 1f);
        lastTickSecond = second;
    }

    void Gulls()
    {
        if (Time.time < nextGull) return;
        nextGull = Time.time + Random.Range(14f, 30f);
        if (part != 1 || state == GameState.Iceberg || bank.gulls.Length == 0) return;
        var listener = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        var at = listener + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * 14f + Vector3.up * 8f;
        PlayAt(bank.gulls[Random.Range(0, bank.gulls.Length)], at, 0.35f, bank.ambience, spatial: 0.7f, pitch: Random.Range(0.95f, 1.05f));
    }

    /// <summary>The drone rises as the iceberg closes in; the hit lands when the sequence starts shaking.</summary>
    void Finale()
    {
        if (state != GameState.Iceberg || iceFrom < 0f || impacted) return;
        if (iceberg == null) iceberg = FindAnyObjectByType<IcebergSequence>();
        float left = iceberg != null ? iceberg.SecondsLeft : Mathf.Max(0f, 30f - (Time.time - iceFrom));
        float k = 1f - Mathf.Clamp01(left / 30f);
        drone.target = Mathf.Lerp(0.25f, 1f, k);
        drone.source.pitch = Mathf.Lerp(0.85f, 1.12f, k);
        if (iceberg != null && iceberg.ImpactShake > 0f) Impact(iceberg.ImpactShake);
    }

    // ---------- Helpers ----------

    void Ease(Layer l)
    {
        float duck = speaking && (l == waltz || l == sustained || l == passengers || l == ballroom) ? duckUnderVoice : 1f;
        l.current = Mathf.MoveTowards(l.current, l.target * duck, l.rate * Time.unscaledDeltaTime);
        if (l.current > 0.001f && !l.source.isPlaying)
        {
            if (!l.started && l.source.clip != null) l.source.time = Random.Range(0f, l.source.clip.length * 0.8f);
            l.started = true;
            l.source.Play();
        }
        else if (l.current <= 0.001f && l.target <= 0f && l.source.isPlaying) l.source.Pause();
        l.category.BaseVolume = l.current;
    }

    Layer Loop(string name, AudioClip clip, UnityEngine.Audio.AudioMixerGroup group,
        AudioCategory.Kind kind = AudioCategory.Kind.Effects, float spatial = 0f, float maxDistance = 30f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.outputAudioMixerGroup = group;
        src.spatialBlend = spatial;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 3f;
        src.maxDistance = maxDistance;
        src.dopplerLevel = 0f;
        var cat = go.AddComponent<AudioCategory>();
        cat.kind = kind;
        cat.BaseVolume = 0f;
        var layer = new Layer { source = src, category = cat };
        layers.Add(layer);
        return layer;
    }

    AudioSource Source2D(string name, UnityEngine.Audio.AudioMixerGroup group)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.outputAudioMixerGroup = group;
        return src;
    }

    static float Effects => GameSettings.Volume(AudioCategory.Kind.Effects);

    void PlayUi(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null) return;
        if (Mathf.Approximately(pitch, 1f)) { ui2D.PlayOneShot(clip, volume * Effects); return; }
        PlayAt(clip, Vector3.zero, volume, bank.sfx, spatial: 0f, pitch: pitch);
    }

    void PlayVoice(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null) return;
        voice2D.pitch = pitch;
        voice2D.PlayOneShot(clip, volume * Effects);
    }

    void PlayLater(AudioClip clip, float delay, float volume)
    {
        if (clip != null) StartCoroutine(Later());
        System.Collections.IEnumerator Later()
        {
            yield return new WaitForSeconds(delay);
            if (!impacted || clip == bank.horn) PlayUi(clip, volume);
        }
    }

    /// <summary>A one-shot from the 3D pool. spatial 1 = fully placed in the world, 0 = heard the same everywhere.</summary>
    public void PlayAt(AudioClip clip, Vector3 position, float volume, UnityEngine.Audio.AudioMixerGroup group,
        float spatial = 1f, float pitch = 1f)
    {
        if (clip == null || pool == null) return;
        AudioSource src = null;
        for (int i = 0; i < pool.Length && src == null; i++)
        {
            var s = pool[(poolNext + i) % pool.Length];
            if (!s.isPlaying) src = s;
        }
        src ??= pool[poolNext];
        poolNext = (poolNext + 1) % pool.Length;
        src.transform.position = position;
        src.clip = clip;
        src.outputAudioMixerGroup = group;
        src.spatialBlend = spatial;
        src.pitch = pitch;
        src.volume = volume * Effects;
        src.Play();
    }

    void Snapshot(string name, float seconds)
    {
        var snap = bank.mixer != null ? bank.mixer.FindSnapshot(name) : null;
        if (snap == null) return;
        snap.TransitionTo(seconds);
        CurrentSnapshot = name;
    }

    static Vector3 StationPos(StationType t)
    {
        var s = StationRegistry.Get(t);
        return s != null ? s.Position : Vector3.zero;
    }
}
