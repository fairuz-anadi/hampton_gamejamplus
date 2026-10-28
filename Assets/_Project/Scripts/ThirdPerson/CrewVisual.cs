using UnityEngine;

/// <summary>
/// Dresses a crew member (a player or a bot) in its seat's crew model, so Ship shows people instead of capsules.
/// Owner: Samprity. Visual only: the capsule's renderer is hidden, its collider, controller and AI are untouched.
/// Bots that already are crew models (CrewRoster spawns them from the crew prefabs) keep their own model; this only
/// gives them the animator with the work loop. The model walks and idles from how fast the actor moves, and plays
/// its work loop while a player holds Interact or a bot works a station. GameplayCameraRig adds this to everyone.
/// </summary>
public class CrewVisual : MonoBehaviour
{
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int WorkHash = Animator.StringToHash("Push");
    static readonly int LocomotionHash = Animator.StringToHash("Locomotion");

    Animator animator;
    PlayerController player;
    BotBrain bot;
    bool driveSpeed = true;
    Vector3 lastPos;
    float speed;
    bool working;

    /// <summary>Puts seat N's crew model on the actor. Does nothing if it already has one or the set is missing.</summary>
    public static CrewVisual Attach(GameObject actor, int seat, CrewVisualSet set, bool human)
    {
        if (actor == null || set == null || set.crew == null || set.crew.Length == 0) return null;
        if (actor.TryGetComponent(out CrewVisual existing)) return existing;

        // Already a crew model (a bot spawned from Crew_N): keep it, swap in the animator that has the work loop.
        var own = actor.GetComponentInChildren<Animator>();
        if (own != null && actor.GetComponentInChildren<SkinnedMeshRenderer>() != null)
        {
            var v = actor.AddComponent<CrewVisual>();
            v.animator = own;
            if (set.controller != null) own.runtimeAnimatorController = set.controller;
            own.applyRootMotion = false;
            v.player = actor.GetComponent<PlayerController>();
            v.bot = actor.GetComponent<BotBrain>();
            v.driveSpeed = actor.GetComponent<BotMover>() == null; // BotMover already feeds Speed
            v.lastPos = actor.transform.position;
            return v;
        }

        var prefab = set.crew[Mathf.Abs(seat - 1) % set.crew.Length];
        if (prefab == null) return null;

        // Feet: the bottom of whatever drew the capsule, else the CharacterController's.
        float feet = 0f;
        var renderers = actor.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            feet = b.min.y - actor.transform.position.y;
        }
        else if (actor.TryGetComponent(out CharacterController cc))
            feet = cc.center.y - cc.height * 0.5f;
        foreach (var r in renderers) r.enabled = false;

        var model = Instantiate(prefab, actor.transform);
        model.name = "CrewModel";
        model.transform.localPosition = new Vector3(0f, feet, 0f);
        model.transform.localRotation = Quaternion.identity;
        foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);

        var visual = actor.AddComponent<CrewVisual>();
        visual.animator = model.GetComponentInChildren<Animator>();
        if (visual.animator != null)
        {
            if (set.controller != null) visual.animator.runtimeAnimatorController = set.controller;
            visual.animator.applyRootMotion = false;
            visual.animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }
        visual.player = actor.GetComponent<PlayerController>();
        visual.bot = actor.GetComponent<BotBrain>();
        visual.lastPos = actor.transform.position;

        // A soft ring in the seat's colour under human players, so each player finds themselves at a glance.
        if (human && set.seatRing != null)
        {
            var ring = new GameObject("SeatRing", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            ring.transform.SetParent(actor.transform, false);
            ring.transform.localPosition = new Vector3(0f, feet + 0.09f, 0f); // clear of the floor tiles' bevels
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = Vector3.one * 0.7f;
            ring.sprite = set.seatRing;
            var c = CrewRoster.SeatColors[Mathf.Clamp(seat, 1, CrewRoster.SeatColors.Length - 1)];
            ring.color = new Color(c.r, c.g, c.b, 0.55f);
        }
        return visual;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || animator == null) return;
        var delta = transform.position - lastPos;
        delta.y = 0f;
        lastPos = transform.position;
        speed = Mathf.Lerp(speed, delta.magnitude / dt, 1f - Mathf.Exp(-dt * 12f));
        if (driveSpeed) animator.SetFloat(SpeedHash, speed < 0.08f ? 0f : speed, 0.08f, dt);

        // Holding Interact (or a bot working a station) while standing still: put your back into it.
        bool work = ((player != null && player.InteractHeld) || (bot != null && (bot.IsWorking || BotChores.IsDoingChore(bot)))) && speed < 0.3f;
        if (work == working) return;
        working = work;
        if (animator.HasState(0, work ? WorkHash : LocomotionHash))
            animator.CrossFadeInFixedTime(work ? WorkHash : LocomotionHash, 0.2f);
    }
}
