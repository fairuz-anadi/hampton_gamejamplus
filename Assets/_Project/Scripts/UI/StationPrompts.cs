using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows a prompt over the station nearest each human crew member. Owner: Samprity.
/// Uses Rabib's stations (StationRegistry) when there are any; until then, the ShipDeck's Station_* markers,
/// so the prompts already work on the deck. Shows "Repair" on broken stations and "Done" on green ones.
/// </summary>
public class StationPrompts : MonoBehaviour
{
    public StationPromptUI[] prompts = new StationPromptUI[0];
    public RectTransform canvasRect;
    [Tooltip("How close a crew member must be to a station, in metres.")]
    public float range = 2.8f;
    [Tooltip("How high above the station the prompt floats, in metres.")]
    public float heightAbove = 1.7f;
    [Tooltip("Canvas units kept clear at the top for the order cards.")]
    public float keepClearTop = 370f;
    [Tooltip("Canvas units kept clear at the bottom for the Captain's subtitles.")]
    public float keepClearBottom = 210f;
    public Color normalTint = new Color(0.95f, 0.88f, 0.7f);
    public Color repairTint = new Color(1f, 0.45f, 0.35f);
    public Color doneTint = new Color(0.55f, 0.95f, 0.6f);

    struct Spot { public Vector3 pos; public StationType type; public IStation station; }

    readonly List<Spot> spots = new List<Spot>(8);
    readonly List<Transform> markers = new List<Transform>(4);
    readonly List<(Transform t, Component c)> humans = new List<(Transform, Component)>(3);

    void Start()
    {
        foreach (StationType t in System.Enum.GetValues(typeof(StationType)))
        {
            var go = GameObject.Find("Station_" + (t == StationType.Doors ? "Doors" : t.ToString()));
            if (go != null) markers.Add(go.transform);
        }
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        GatherSpots();
        GatherHumans();

        int used = 0;
        foreach (var (who, actor) in humans)
        {
            if (used >= prompts.Length || cam == null) break;

            var interactor = who.GetComponent<Interactor>();
            string key = HudText.KeyFor(HudText.Key.Interact, actor);

            // Carrying something: the prompt belongs to the carrier, not any station spot.
            if (interactor != null && interactor.IsCarrying)
            {
                Vector3 carryScreen = cam.WorldToScreenPoint(who.position + Vector3.up * heightAbove);
                if (carryScreen.z > 0f)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, carryScreen, null, out var carryLocal);
                    prompts[used].Show(KeepOnScreen(carryLocal), "Carrying", interactor.PromptText, key, normalTint, 0f);
                    used++;
                }
                continue;
            }

            int best = -1;
            float bestDist = range * range;
            for (int i = 0; i < spots.Count; i++)
            {
                Vector3 d = spots[i].pos - who.position;
                d.y = 0f;
                if (d.sqrMagnitude < bestDist) { bestDist = d.sqrMagnitude; best = i; }
            }
            if (best < 0) continue;

            var s = spots[best];
            Vector3 screen = cam.WorldToScreenPoint(s.pos + Vector3.up * heightAbove);
            if (screen.z <= 0f) continue;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
            local = KeepOnScreen(local);

            // When this human's own Interactor is the one holding this exact station, it already knows every
            // special case (steer hint, lifeboat reopen, holder counts) — defer to it instead of guessing here.
            bool owned = interactor != null && s.station != null && ReferenceEquals(interactor.NearestStation, s.station);
            string action; Color tint;
            if (owned && !string.IsNullOrEmpty(interactor.PromptText))
            {
                action = interactor.PromptText;
                tint = s.station.IsBroken ? repairTint : s.station.IsDone ? doneTint : normalTint;
            }
            else if (s.station != null && s.station.IsBroken) { action = "Hold to repair"; tint = repairTint; }
            else if (s.station != null && s.station.IsDone) { action = "Done"; tint = doneTint; }
            else { action = "Hold  ·  " + HudText.StationVerb(s.type); tint = normalTint; }

            float progress01 = owned ? interactor.HoldProgress01 : StationProgress01(s.station);
            prompts[used].Show(local, HudText.StationName(s.type), action, key, tint, progress01);
            used++;
        }
        for (int i = used; i < prompts.Length; i++) prompts[i].Hide();
    }

    /// <summary>Progress for a station nobody's own Interactor is currently credited with holding (bots, far spots).</summary>
    static float StationProgress01(IStation station) =>
        station is Station s && !(s is Wheel w && w.SteeringOpen)
            ? (s.IsDone && !s.IsBroken ? 1f : s.Progress01)
            : 0f;

    /// <summary>Keeps a prompt (pivot at its bottom centre) on screen, below the order cards and above the subtitles.</summary>
    public Vector2 KeepOnScreen(Vector2 local)
    {
        Vector2 half = canvasRect.rect.size * 0.5f;
        local.x = Mathf.Clamp(local.x, -half.x + 170f, half.x - 170f);
        local.y = Mathf.Clamp(local.y, -half.y + keepClearBottom, half.y - keepClearTop - 80f);
        return local;
    }

    void GatherSpots()
    {
        spots.Clear();
        foreach (var st in StationRegistry.All)
            spots.Add(new Spot { pos = st.Position, type = st.Type, station = st });
        if (spots.Count > 0) return;
        foreach (var m in markers)
        {
            if (m == null) continue;
            System.Enum.TryParse(m.name.Substring("Station_".Length), out StationType t);
            spots.Add(new Spot { pos = m.position, type = t });
        }
    }

    void GatherHumans()
    {
        humans.Clear();
        foreach (var c in CrewRegistry.All)
            if (!c.IsBot && c is Component comp) humans.Add((comp.transform, comp));
        if (humans.Count > 0) return;
        // Sandbox scenes: no registered crew, just someone walking around.
        foreach (var m in ThirdPersonMover.All) humans.Add((m.transform, m));
    }
}
