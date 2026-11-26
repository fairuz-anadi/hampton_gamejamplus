using UnityEngine;

/// <summary>
/// Dresses a crew member (a player or a bot) in its seat's crew model, so Ship shows people instead of capsules.
/// Owner: Samprity. Visual only: the capsule's renderer is hidden, its collider, controller and AI are untouched.
/// Bots that already are crew models (CrewRoster spawns them from the crew prefabs) keep their own model; this only
/// gives them the animator with the work loop. The model walks and idles from how fast the actor moves, and plays
/// its work loop while a player holds Interact or a bot works a station. GameplayCameraRig adds this to everyone.
/// The work loop fits the job (WorkAnimations): shovelling coal at the Boiler (with a shovel in hand), hauling a lever
/// at the Doors, hand over hand at the Wheel, tapping the key at the Wireless, kneeling to fix anything broken, and
/// the bots' chores. It plays in the animator's "Push" state through a per-actor override, so the shared controller
/// is untouched; without the WorkAnimations asset it stays the old push.
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

    // The job's own work loop, swapped into the "Push" state for this actor only.
    AnimatorOverrideController overrides;
    AnimationClip pushClip, shownClip;
    Interactor interactor;
    Transform shovel;

    // Where the borrowed shovel sits in the right hand (hand bone space). Tune here if the grip looks off.
    static readonly Vector3 ShovelGrip = new Vector3(0f, 0.04f, 0.02f);
    static readonly Vector3 ShovelGripEuler = new Vector3(0f, 0f, -90f);
    const float ShovelGripUpHandle = 0.95f; // how far up the handle (from the blade end) the hand holds it

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
            v.SetUpWorkLoop();
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
        visual.SetUpWorkLoop();

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
        // The loop that fits the job; a different job while already working (a station broke) switches over.
        bool newJob = work && ShowWorkLoop(WorkClip());
        ShowShovel(work && shownClip != null && shownClip == WorkAnimations.Load()?.shovel);
        if (work && working && newJob)
        {
            // Already in the work state: crossfading to it again would be ignored, so restart it on the new loop.
            if (animator.HasState(0, WorkHash)) animator.Play(WorkHash, 0, 0f);
            return;
        }
        if (work == working) return;
        working = work;
        if (animator.HasState(0, work ? WorkHash : LocomotionHash))
            animator.CrossFadeInFixedTime(work ? WorkHash : LocomotionHash, 0.2f);
    }

    void OnDestroy()
    {
        if (shovel != null) Destroy(shovel.gameObject);
    }

    // ---------- The job's work loop ----------

    void SetUpWorkLoop()
    {
        interactor = GetComponent<Interactor>();
        if (animator == null || animator.runtimeAnimatorController == null || WorkAnimations.Load() == null) return;
        overrides = new AnimatorOverrideController(animator.runtimeAnimatorController);
        foreach (var clip in overrides.animationClips)
            if (clip != null && clip.name.EndsWith("Push_Loop")) { pushClip = clip; break; }
        if (pushClip == null) { overrides = null; return; }
        animator.runtimeAnimatorController = overrides;
        shownClip = pushClip;
    }

    /// <summary>What this actor is working on right now, as a clip; null leaves the push.</summary>
    AnimationClip WorkClip()
    {
        var set = WorkAnimations.Load();
        if (set == null) return null;
        if (player != null) return set.For(interactor != null ? interactor.NearestStation as Station : null) ?? set.tap;
        if (bot != null)
        {
            if (bot.WorkingAt != null) return set.For(bot.WorkingAt);
            var spot = BotChores.SpotOf(bot);
            if (spot != null) return set.ForChore(spot.name);
        }
        return set.tap;
    }

    /// <summary>Puts clip in the work state. True when it changed (the state then restarts on the new loop).</summary>
    bool ShowWorkLoop(AnimationClip clip)
    {
        if (overrides == null) return false;
        if (clip == null) clip = pushClip;
        if (clip == shownClip) return false;
        overrides[pushClip] = clip;
        shownClip = clip;
        return true;
    }

    /// <summary>At the coal: one of the boiler room's shovels, copied into the right hand while they work.</summary>
    void ShowShovel(bool show)
    {
        if (!show)
        {
            if (shovel != null) shovel.gameObject.SetActive(false);
            return;
        }
        if (shovel == null)
        {
            var hand = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            var source = hand != null ? FindShovel() : null;
            if (source == null) return;
            shovel = Instantiate(source).transform;
            shovel.name = "HeldShovel";
            foreach (var c in shovel.GetComponentsInChildren<Collider>()) Destroy(c);
            PlaceShovel(shovel, hand, source.transform.lossyScale);
        }
        shovel.gameObject.SetActive(true);
    }

    /// <summary>
    /// Puts a shovel prop in a right hand (also used by WorkAnimationsPreview, so the preview shows the real grip).
    /// Placed in world space, then parented, so a scaled rig (bones in centimetres) can't shrink or blow it up.
    /// </summary>
    public static void PlaceShovel(Transform shovel, Transform hand, Vector3 worldScale)
    {
        var rot = hand.rotation * Quaternion.Euler(ShovelGripEuler);
        // The prop's origin is the blade end; slide it so the hand closes round the handle.
        var pos = hand.position + hand.rotation * ShovelGrip - rot * (Vector3.up * ShovelGripUpHandle);
        shovel.SetParent(null, true);
        shovel.SetPositionAndRotation(pos, rot);
        shovel.localScale = worldScale;
        shovel.SetParent(hand, true);
    }

    static GameObject shovelSource;

    /// <summary>The deck's own shovel prop (ShipDeckBuilder leans two in the boiler room).</summary>
    static GameObject FindShovel()
    {
        if (shovelSource != null) return shovelSource;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            if (t.name == "Shovel" && t.GetComponentInChildren<Renderer>() != null) { shovelSource = t.gameObject; break; }
        return shovelSource;
    }
}
