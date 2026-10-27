using UnityEngine;

/// <summary>
/// Third-person follow camera that swings itself behind the character, or orbits under mouse/right-stick look
/// input while the player's giving it (added by Rabib: PlayerController reads the Look action and calls
/// SetLookInput each frame). Owner: Samprity.
/// Put it on the Camera. It runs in LateUpdate, after the mover has moved.
///
/// Tuning cheat sheet (all live in Play mode):
///   distance / height           where the camera sits behind and above the focus point
///   pitch / pitchMin / pitchMax how far up/down it can look; also where the orbit arm sits
///   rotationSmoothTime          how lazily it swings round after a turn (bigger = lazier)
///   recenterSpeed               the fastest it may swing, in degrees per second
///   mouseLookSensitivity / stickLookSensitivity  how fast look input turns the camera
/// </summary>
[DisallowMultipleComponent]
public class AutoFollowCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    [Tooltip("Optional. Lets the camera know when the character is moving and which way it faces.")]
    public ThirdPersonMover mover;
    [Tooltip("Point on the character the camera orbits around, in metres above its feet.")]
    public float focusHeight = 1.0f;

    [Header("Framing")]
    [Tooltip("Metres behind the focus point.")]
    [Min(0.5f)] public float distance = 4.8f;
    [Tooltip("Metres above the focus point.")]
    public float height = 2.2f;
    [Tooltip("Degrees the camera looks down. Also the orbit arm's starting elevation; look input and pitchMin/pitchMax move it from here.")]
    [Range(-30f, 89f)] public float pitch = 22f;
    [Tooltip("Vertical field of view. 0 leaves the Camera's own value alone.")]
    [Range(0f, 100f)] public float fieldOfView = 45f;

    [Header("Look input")]
    [Tooltip("Degrees the camera turns per pixel of mouse movement.")]
    public float mouseLookSensitivity = 0.12f;
    [Tooltip("Degrees per second at full gamepad right-stick deflection.")]
    public float stickLookSensitivity = 120f;
    [Tooltip("How far up the player can look.")]
    public float pitchMin = -10f;
    [Tooltip("How far down the player can look.")]
    public float pitchMax = 70f;
    [Tooltip("Seconds of no look input before the camera settles back behind the character.")]
    public float lookIdleBeforeRecenter = 1.5f;

    [Header("Follow")]
    [Tooltip("Seconds to catch up with the character's position. Small = tight, big = floaty.")]
    [Range(0f, 1f)] public float followSmoothTime = 0.12f;

    [Header("Automatic rotation")]
    [Tooltip("Seconds to swing behind the character after it turns. Bigger = lazier.")]
    [Range(0.01f, 3f)] public float rotationSmoothTime = 0.55f;
    [Tooltip("Fastest the camera may swing round, in degrees per second.")]
    [Min(1f)] public float recenterSpeed = 160f;
    [Tooltip("Keep settling behind the character after it stops. Off = the camera coasts to a stop where it is.")]
    public bool recenterWhenIdle = true;

    [Header("Collision")]
    [Tooltip("Layers that block the camera (walls, machinery). The character's own colliders are always ignored.")]
    public LayerMask collisionMask = ~0;
    [Tooltip("Comfort zone round the camera. Walls entering it ease the camera in.")]
    [Range(0.05f, 1f)] public float collisionRadius = 0.45f;
    [Tooltip("Hard limit round the camera, about the size of its near plane. Walls entering it push the camera in at once.")]
    [Range(0.05f, 0.5f)] public float hardRadius = 0.12f;
    [Tooltip("Gap kept between the camera and whatever blocked it.")]
    [Range(0f, 1f)] public float collisionPadding = 0.15f;
    [Tooltip("Closest the camera may get to the focus point.")]
    [Min(0.1f)] public float minDistance = 0.9f;
    [Tooltip("Seconds to ease in when a wall enters the comfort zone.")]
    [Range(0.01f, 1f)] public float pullInSmoothTime = 0.1f;
    [Tooltip("Seconds to ease back out once the wall is gone.")]
    [Range(0.01f, 2f)] public float returnSmoothTime = 0.35f;
    [Tooltip("Other crew members (CharacterControllers) never push the camera in.")]
    public bool ignoreOtherCrew = true;

    [Header("Rise over walls")]
    [Tooltip("When a wall is right behind the character, the camera may rise this many degrees to look over it " +
             "instead of pushing in to the back of the head. Ship rooms have open tops, so this usually works. 0 = off.")]
    [Range(0f, 60f)] public float maxLift = 60f;
    [Tooltip("Seconds to rise or settle back down.")]
    [Range(0.01f, 2f)] public float liftSmoothTime = 0.15f;

    readonly RaycastHit[] hits = new RaycastHit[16];
    Vector3 focus, focusVelocity;
    float yaw, yawVelocity;
    float arm, armVelocity;
    float lift, liftVelocity;
    int turnSign;
    bool initialised;
    float defaultPitch;
    Vector2 lookThisFrame;
    float lastLookTime = -999f;

    /// <summary>The camera's facing around Y, 0-360. The mover measures W/A/S/D against this.</summary>
    public float Yaw => Mathf.Repeat(yaw, 360f);
    /// <summary>Current camera arm length; shorter than the full arm when a wall pushed it in.</summary>
    public float CurrentArm => arm;
    public float FullArm => new Vector2(distance, height).magnitude;

    /// <summary>
    /// PlayerController calls this once a frame with degrees to turn this frame — already scaled for mouse vs
    /// gamepad and, for the stick, by deltaTime, so this just adds it to yaw/pitch.
    /// </summary>
    public void SetLookInput(Vector2 degreesThisFrame) => lookThisFrame = degreesThisFrame;

    void Start()
    {
        if (mover == null && target != null) mover = target.GetComponent<ThirdPersonMover>();
        if (target == null && mover != null) target = mover.transform;
        defaultPitch = pitch;
        ApplyFieldOfView();
        SnapToTarget();
    }

    void OnValidate() => ApplyFieldOfView();

    void ApplyFieldOfView()
    {
        if (fieldOfView > 0f && TryGetComponent(out Camera cam)) cam.fieldOfView = fieldOfView;
    }

    void LateUpdate() => Tick(Time.deltaTime);

    /// <summary>Jump straight behind the target with no smoothing. Use after teleporting the character.</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        focus = FocusPoint();
        focusVelocity = Vector3.zero;
        yaw = TargetYaw();
        yawVelocity = 0f;
        turnSign = 0;
        pitch = defaultPitch;
        lastLookTime = -999f;
        arm = FullArm;
        armVelocity = 0f;
        lift = liftVelocity = 0f;
        initialised = true;
        Tick(0f);
    }

    /// <summary>One camera step. LateUpdate calls this; tests call it directly with a fixed dt.</summary>
    public void Tick(float dt)
    {
        if (target == null) return;
        if (!initialised) { SnapToTarget(); return; }

        if (dt > 0f)
        {
            focus = followSmoothTime > 0f
                ? Vector3.SmoothDamp(focus, FocusPoint(), ref focusVelocity, followSmoothTime, Mathf.Infinity, dt)
                : FocusPoint();
            ApplyLook();
            UpdateYaw(dt);
        }

        float fullArm = FullArm;
        if (dt > 0f && maxLift > 0f)
            lift = Mathf.SmoothDamp(lift, ChooseLift(fullArm), ref liftVelocity, liftSmoothTime, Mathf.Infinity, dt);
        Vector3 armDir = ArmDirection(lift);

        // Two casts: the wide one eases the camera in when it brushes a corner, the thin one is a hard
        // limit so the lens never ends up inside a wall. One wide hard cast made corners snap the camera in.
        float comfort = Mathf.Max(minDistance, ClearDistance(focus, armDir, fullArm, collisionRadius));
        float hard = Mathf.Max(minDistance, ClearDistance(focus, armDir, fullArm, hardRadius));
        if (dt > 0f)
        {
            float smooth = comfort < arm ? pullInSmoothTime : returnSmoothTime;
            arm = Mathf.SmoothDamp(arm, comfort, ref armVelocity, smooth, Mathf.Infinity, dt);
        }
        if (arm > hard) { arm = hard; armVelocity = Mathf.Min(armVelocity, 0f); }

        Vector3 camPos = focus + armDir * arm;

        // Up close (pushed in by a wall) blend towards looking straight at the character so it stays in frame.
        var framed = Quaternion.Euler(Mathf.Min(89f, pitch + lift), yaw, 0f);
        Vector3 toFocus = focus - camPos;
        var rot = toFocus.sqrMagnitude > 1e-4f
            ? Quaternion.Slerp(Quaternion.LookRotation(toFocus, Vector3.up), framed, Mathf.Clamp01(arm / fullArm))
            : framed;

        transform.SetPositionAndRotation(camPos, rot);
    }

    /// <summary>Applies this frame's look input to yaw/pitch, unless the iceberg finale owns the camera.</summary>
    void ApplyLook()
    {
        bool icebergActive = IcebergPresentation.Instance != null && IcebergPresentation.Instance.Active;
        if (!icebergActive && lookThisFrame.sqrMagnitude > 0.0000001f)
        {
            yaw = Mathf.Repeat(yaw + lookThisFrame.x, 360f);
            pitch = Mathf.Clamp(pitch - lookThisFrame.y, pitchMin, pitchMax); // moving the mouse up looks up
            lastLookTime = Time.time;
        }
        lookThisFrame = Vector2.zero;
    }

    void UpdateYaw(float dt)
    {
        // The player's actively orbiting: leave yaw alone, don't fight them with the auto-recenter below.
        if (Time.time - lastLookTime < lookIdleBeforeRecenter)
        {
            yawVelocity = 0f;
            turnSign = 0;
            return;
        }

        bool moving = mover == null || mover.IsMoving;
        if (!moving && !recenterWhenIdle)
        {
            // Coast to a stop instead of drifting on.
            yawVelocity = Mathf.MoveTowards(yawVelocity, 0f, recenterSpeed * 4f * dt);
            yaw += yawVelocity * dt;
            return;
        }

        float delta = Mathf.DeltaAngle(yaw, TargetYaw());

        // Near 180 degrees the shortest way round can flip sides every frame. Pick a side and keep it.
        if (Mathf.Abs(delta) > 170f)
        {
            if (turnSign == 0) turnSign = Mathf.Abs(yawVelocity) > 1f ? (int)Mathf.Sign(yawVelocity) : (delta >= 0f ? 1 : -1);
            if (Mathf.Sign(delta) != turnSign) delta += 360f * turnSign;
        }
        else if (Mathf.Abs(delta) < 150f)
        {
            turnSign = 0;
        }

        yaw = Mathf.SmoothDamp(yaw, yaw + delta, ref yawVelocity, rotationSmoothTime, recenterSpeed, dt);
        yaw = Mathf.Repeat(yaw, 360f);
    }

    /// <summary>The arm from the focus to the camera, tilted up by extra degrees of lift. Elevation follows
    /// pitch, so the camera orbits to where it's looking instead of sitting at a fixed height while the view tilts.</summary>
    Vector3 ArmDirection(float extraLift)
    {
        float elevation = Mathf.Clamp(pitch + extraLift, -80f, 85f);
        float e = elevation * Mathf.Deg2Rad;
        return Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, Mathf.Sin(e), -Mathf.Cos(e));
    }

    /// <summary>Lowest lift (in 15 degree steps) that leaves most of the arm clear. If none does, the clearest one.</summary>
    float ChooseLift(float fullArm)
    {
        float bestLift = 0f, bestClear = -1f;
        for (float l = 0f; l <= maxLift + 0.01f; l += 15f)
        {
            float clear = ClearDistance(focus, ArmDirection(l), fullArm, collisionRadius);
            if (clear >= fullArm * 0.75f) return l;
            if (clear > bestClear + 0.05f) { bestClear = clear; bestLift = l; }
        }
        return bestLift;
    }

    float TargetYaw() => mover != null ? mover.FacingYaw : target.eulerAngles.y;

    Vector3 FocusPoint() => target.position + Vector3.up * focusHeight;

    /// <summary>How far the camera can go from the focus along the arm before it hits something.</summary>
    float ClearDistance(Vector3 origin, Vector3 dir, float maxDist, float radius)
    {
        int count = Physics.SphereCastNonAlloc(origin, radius, dir, hits, maxDist + collisionPadding,
            collisionMask, QueryTriggerInteraction.Ignore);
        float best = maxDist;
        var root = target.root;
        for (int i = 0; i < count; i++)
        {
            var h = hits[i];
            // distance 0 means the sphere started inside it (the character hugging a wall): skip it.
            if (h.distance <= 0f) continue;
            var col = h.collider;
            if (col.transform.IsChildOf(root)) continue;
            if (ignoreOtherCrew && col is CharacterController) continue;
            best = Mathf.Min(best, h.distance - collisionPadding);
        }
        return Mathf.Max(0f, best);
    }

    void OnDrawGizmosSelected()
    {
        if (target == null) return;
        Gizmos.color = Color.cyan;
        Vector3 f = Application.isPlaying ? focus : FocusPoint();
        Gizmos.DrawWireSphere(f, 0.1f);
        Gizmos.DrawLine(f, transform.position);
        Gizmos.DrawWireSphere(transform.position, collisionRadius);
    }
}
