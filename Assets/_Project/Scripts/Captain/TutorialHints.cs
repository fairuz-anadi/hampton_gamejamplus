using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Onboarding without a manual. Owner: Riham.
/// On the first voyage of the session, the Captain reacts on the speaker when a human gets stuck on a Part 1 order:
/// standing still, not working the station, alone at a two-person job, sitting on a green light, holding a carry item.
/// Each line plays once. Nothing starts until the Captain's opening line has been said.
/// On screen, a small key prompt over the player's head covers what Samprity's station prompts don't:
/// move, pick up a carry item, call for help when alone at a two-person job, report on green, and report/refuse
/// on the first off order. (Her StationPrompts already show the Interact key at each station.)
/// Also one line right after the first off order a human gets. Lines live in CaptainScript (Tutorial*).
/// Samprity: draw the key prompt from PromptFor(seat) / PromptChanged, then set DrawFallbackPrompts = false.
/// </summary>
public class TutorialHints : MonoBehaviour
{
    public static TutorialHints Instance { get; private set; }

    /// <summary>Simple IMGUI key prompt over the player's head until the real one exists.</summary>
    public static bool DrawFallbackPrompts = true;

    /// <summary>A human's key prompt changed: their seat and the key ("" when it clears).</summary>
    public static event System.Action<int, string> PromptChanged;

    static bool doneThisSession;
    static readonly HashSet<string> said = new HashSet<string>();

    [SerializeField] float stationRange = 2.5f;
    [SerializeField] float idleSeconds = 7f;
    [SerializeField] float atStationSeconds = 3f;
    [SerializeField] float aloneSeconds = 2.5f;
    [SerializeField] float greenSeconds = 3f;
    [SerializeField] float pickUpSeconds = 3f;

    class Watch
    {
        public int orderId = -1;
        public Vector3 stillAt;
        public float stillSince, idleAtStationSince = -1f, aloneSince = -1f, greenSince = -1f, atPickUpSince = -1f;
        public bool wasCarrying;
    }

    readonly Dictionary<int, Watch> watches = new Dictionary<int, Watch>();
    readonly Dictionary<int, string> prompts = new Dictionary<int, string>();
    bool offOrderSaid;
    int offOrderId = -1;
    bool active;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        active = !doneThisSession;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        GameEvents.OrderIssued += OnOrderIssued;
        GameEvents.StateChanged += OnStateChanged;
    }

    void OnDisable()
    {
        GameEvents.OrderIssued -= OnOrderIssued;
        GameEvents.StateChanged -= OnStateChanged;
    }

    /// <summary>The key prompt to show over a human's head right now, e.g. "Q  call for help", or "".</summary>
    public string PromptFor(int seat) => prompts.TryGetValue(seat, out var p) ? p : "";

    void OnStateChanged(GameState s)
    {
        if (s >= GameState.Part3 && active)
        {
            active = false;
            doneThisSession = true;
            foreach (var seat in new List<int>(prompts.Keys)) SetPrompt(seat, "");
        }
    }

    void OnOrderIssued(ActiveOrder o)
    {
        if (!active || offOrderSaid || o.Assignee == null || o.Assignee.IsBot || !o.Data.IsOff) return;
        offOrderSaid = true;
        offOrderId = o.Id;
        Say(CaptainScript.TutorialOffOrder, o.Assignee.Seat); // queued right after the order itself
    }

    void Update()
    {
        if (!active) return;
        // The opening line goes first. No tutorial lines or prompts until it's been said.
        var captain = CaptainDirector.Instance;
        if (captain != null && !captain.OpeningDone) return;
        var od = OrderDirector.Instance;
        foreach (var c in CrewRegistry.All)
        {
            if (c.IsBot) continue;
            if (!watches.TryGetValue(c.Seat, out var w)) watches[c.Seat] = w = new Watch { stillAt = c.Position, stillSince = Time.time };
            var o = od != null ? od.GetOrderFor(c) : null;
            string prompt = "";
            if (o != null && o.Data.part == 1) prompt = Tick(c, o, w);
            else if (o != null && o.Id == offOrderId)
                prompt = Key(c, "R", "Y", "report") + "    " + Key(c, "X", "B", "refuse");
            SetPrompt(c.Seat, prompt);
        }
    }

    /// <summary>Watches one human on one order. Returns the key prompt to show, if any.</summary>
    string Tick(ICrewActor c, ActiveOrder o, Watch w)
    {
        if (w.orderId != o.Id)
        {
            w.orderId = o.Id;
            w.idleAtStationSince = w.aloneSince = w.greenSince = w.atPickUpSince = -1f;
            w.wasCarrying = false;
        }

        // Standing still?
        if (BotMover.FlatDistance(c.Position, w.stillAt) > 0.4f) { w.stillAt = c.Position; w.stillSince = Time.time; }
        bool idle = Time.time - w.stillSince >= idleSeconds;

        var data = o.Data;
        var od = OrderDirector.Instance;

        if (od.IsStationDone(o))
        {
            if (Waited(ref w.greenSince, greenSeconds)) Say(CaptainScript.TutorialGreen, c.Seat);
            return Key(c, "R", "Y", "report");
        }
        w.greenSince = -1f;

        bool carryOrder = data.isCarry && data.station != data.deliverTo;
        bool carrying = carryOrder && IsCarrying(data);
        if (carrying && !w.wasCarrying) Say(CaptainScript.TutorialCarrying, c.Seat);
        w.wasCarrying = carrying;

        if (carryOrder && !carrying)
        {
            var pickUp = StationRegistry.Get(data.station);
            bool atPickUp = pickUp != null && BotMover.FlatDistance(c.Position, pickUp.Position) <= stationRange;
            if (atPickUp)
            {
                if (Waited(ref w.atPickUpSince, pickUpSeconds)) Say(CaptainScript.TutorialPickUp, c.Seat);
                return Key(c, "E", "A", "pick up");
            }
            w.atPickUpSince = -1f;
            if (idle) Say(CaptainScript.TutorialIdle, c.Seat);
            return Key(c, "WASD", "Left stick", "move");
        }

        var st = StationRegistry.Get(data.CheckStation) as Station;
        bool near = st != null && BotMover.FlatDistance(c.Position, st.Position) <= stationRange;
        if (!near)
        {
            w.idleAtStationSince = w.aloneSince = -1f;
            if (idle) Say(CaptainScript.TutorialIdle, c.Seat);
            return Key(c, "WASD", "Left stick", "move");
        }

        if (st.ActiveHolders == 0 && !carrying)
        {
            w.aloneSince = -1f;
            if (Waited(ref w.idleAtStationSince, atStationSeconds)) Say(CaptainScript.TutorialAtStation, c.Seat);
            return "";
        }
        w.idleAtStationSince = -1f;

        if (data.needsTwo && st.ActiveHolders < 2)
        {
            if (Waited(ref w.aloneSince, aloneSeconds)) Say(CaptainScript.TutorialAlone, c.Seat);
            return Key(c, "Q", "X", "call for help");
        }
        w.aloneSince = -1f;
        return "";
    }

    /// <summary>Starts a timer the first time it's asked, true once it has run for the given seconds.</summary>
    static bool Waited(ref float since, float seconds)
    {
        if (since < 0f) since = Time.time;
        return Time.time - since >= seconds;
    }

    static bool IsCarrying(OrderData data)
    {
        foreach (var item in FindObjectsByType<CarryItem>())
            if (item.Order == data && item.IsCarried) return true;
        return false;
    }

    /// <summary>"Q  call for help", or the gamepad button, depending on what that player is using.</summary>
    static string Key(ICrewActor c, string keyboard, string gamepad, string verb)
    {
        var pi = c is Component comp ? comp.GetComponent<PlayerInput>() : null;
        string key = pi != null && pi.currentControlScheme == "Gamepad" ? gamepad : keyboard;
        return $"{key}  {verb}";
    }

    /// <summary>Each tutorial line plays once per session.</summary>
    static void Say(string line, int seat)
    {
        if (!said.Add(line)) return;
        CaptainDirector.Instance?.SayTutorial(line.Replace("{seat}", CrewRegistry.SeatName(seat)));
    }

    void SetPrompt(int seat, string key)
    {
        if (PromptFor(seat) == key) return;
        prompts[seat] = key;
        PromptChanged?.Invoke(seat, key);
    }

    // ---------- Fallback key prompt ----------

    GUIStyle style;

    void OnGUI()
    {
        if (!DrawFallbackPrompts || !active) return;
        var cam = Camera.main;
        if (cam == null) return;
        style ??= new GUIStyle(GUI.skin.box) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(10, 10, 4, 4) };

        foreach (var c in CrewRegistry.All)
        {
            if (c.IsBot) continue;
            string key = PromptFor(c.Seat);
            if (key.Length == 0) continue;
            var screen = cam.WorldToScreenPoint(c.Position + Vector3.up * 2.4f);
            if (screen.z <= 0f) continue;
            var size = style.CalcSize(new GUIContent(key));
            GUI.Box(new Rect(screen.x - size.x * 0.5f, Screen.height - screen.y - size.y, size.x, size.y), key, style);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        PromptChanged = null;
        DrawFallbackPrompts = true;
        doneThisSession = false;
        said.Clear();
    }
}
