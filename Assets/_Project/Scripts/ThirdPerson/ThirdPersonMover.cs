using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Camera-relative walking on the deck plane (XZ). No jumping, sprinting or crouching. Owner: Samprity (prototype).
/// Rabib: this is meant to fold into PlayerController. Feed it from PlayerInput with SetMoveInput and turn off readOwnInput.
///
/// Why the "reference yaw": movement is camera-relative and the camera turns itself to sit behind the player.
/// If W/A/S/D were read against the live camera every frame, holding A would curve forever and the camera would chase it.
/// So the camera's yaw is captured when the input direction changes and held while the same keys stay down.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ThirdPersonMover : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Top walking speed in metres per second.")]
    [Min(0f)] public float moveSpeed = 3.4f;
    [Tooltip("How fast the player reaches top speed (m/s per second).")]
    [Min(0.01f)] public float acceleration = 18f;
    [Tooltip("How fast the player stops when keys are released (m/s per second).")]
    [Min(0.01f)] public float deceleration = 24f;
    [Tooltip("How fast the character turns to face where it walks, in degrees per second.")]
    [Min(1f)] public float rotationSpeed = 720f;
    [Min(0f)] public float gravity = 20f;

    [Header("Camera-relative input")]
    [Tooltip("The camera whose facing W points along. Falls back to Camera.main.")]
    public AutoFollowCamera followCamera;
    [Tooltip("Re-read the camera's facing when the stick or keys change direction by more than this many degrees.")]
    [Range(1f, 90f)] public float relatchAngle = 20f;
    [Range(0f, 0.5f)] public float deadZone = 0.15f;

    [Header("Input")]
    [Tooltip("Off when something else (PlayerInput, bots, tests) calls SetMoveInput.")]
    public bool readOwnInput = true;
#if ENABLE_INPUT_SYSTEM
    [Tooltip("Optional. Leave empty to use WASD, arrow keys and the left stick.")]
    public InputActionReference moveAction;
    InputAction ownMove;
#endif

    [Header("Animation (optional)")]
    public Animator animator;
    [Tooltip("Float parameter that receives the walking speed in m/s.")]
    public string speedParameter = "Speed";

    CharacterController controller;
    Vector2 moveInput;
    Vector3 moveDir = Vector3.forward;
    float speed;
    float verticalVelocity;
    bool latched;
    float latchedInputAngle;
    float referenceYaw;
    int speedHash;
    bool hasSpeedParam;

    /// <summary>Horizontal velocity on the deck plane.</summary>
    public Vector3 Velocity => moveDir * speed;
    public float Speed => speed;
    public bool IsMoving => speed > 0.05f;
    /// <summary>Which way the character faces, in degrees around Y.</summary>
    public float FacingYaw => transform.eulerAngles.y;
    /// <summary>The camera yaw that W/A/S/D are currently measured against.</summary>
    public float ReferenceYaw => referenceYaw;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        moveDir = Flat(transform.forward);
        if (animator == null) animator = GetComponentInChildren<Animator>();
        CacheAnimatorParameter();
    }

#if ENABLE_INPUT_SYSTEM
    void OnEnable()
    {
        if (!readOwnInput) return;
        if (moveAction != null && moveAction.action != null) { moveAction.action.Enable(); return; }
        ownMove ??= CreateDefaultMoveAction();
        ownMove.Enable();
    }

    void OnDisable() => ownMove?.Disable();

    static InputAction CreateDefaultMoveAction()
    {
        var a = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        a.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        a.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        a.AddBinding("<Gamepad>/leftStick");
        return a;
    }
#endif

    /// <summary>x = right, y = forward, each -1..1. Use this from PlayerInput, bots or tests.</summary>
    public void SetMoveInput(Vector2 input) => moveInput = input;

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (readOwnInput)
        {
            var action = moveAction != null && moveAction.action != null ? moveAction.action : ownMove;
            if (action != null) moveInput = action.ReadValue<Vector2>();
        }
#endif
        Tick(Time.deltaTime);
    }

    /// <summary>One movement step. Update calls this; tests call it directly with a fixed dt.</summary>
    public void Tick(float dt)
    {
        if (dt <= 0f) return;
        if (controller == null) controller = GetComponent<CharacterController>();

        // Diagonals are clamped so W+D is no faster than W.
        Vector2 input = Vector2.ClampMagnitude(moveInput, 1f);
        bool hasInput = input.magnitude > deadZone;

        float targetSpeed = 0f;
        if (hasInput)
        {
            float inputAngle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;
            if (!latched || Mathf.Abs(Mathf.DeltaAngle(inputAngle, latchedInputAngle)) > relatchAngle)
            {
                referenceYaw = CurrentCameraYaw();
                latchedInputAngle = inputAngle;
                latched = true;
            }

            Vector3 wanted = Quaternion.Euler(0f, referenceYaw + inputAngle, 0f) * Vector3.forward;
            // Turning the travel direction at the same rate as the body keeps reversals from snapping.
            moveDir = Vector3.RotateTowards(moveDir, wanted, rotationSpeed * Mathf.Deg2Rad * dt, 0f);
            moveDir = Flat(moveDir);
            targetSpeed = moveSpeed * Mathf.InverseLerp(deadZone, 1f, input.magnitude);
        }
        else
        {
            latched = false;
        }

        float rate = targetSpeed > speed ? acceleration : deceleration;
        speed = Mathf.MoveTowards(speed, targetSpeed, rate * dt);

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        else verticalVelocity -= gravity * dt;

        controller.Move((moveDir * speed + Vector3.up * verticalVelocity) * dt);

        if (hasInput)
        {
            var face = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, face, rotationSpeed * dt);
        }

        if (hasSpeedParam) animator.SetFloat(speedHash, speed, 0.08f, dt);
    }

    float CurrentCameraYaw()
    {
        if (followCamera != null) return followCamera.Yaw;
        var cam = Camera.main;
        return cam != null ? cam.transform.eulerAngles.y : transform.eulerAngles.y;
    }

    /// <summary>Puts the character at a spot facing a direction, with no leftover speed. For respawns and tests.</summary>
    public void Teleport(Vector3 position, float yaw)
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        controller.enabled = false;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        controller.enabled = true;
        moveDir = Flat(transform.forward);
        speed = 0f;
        verticalVelocity = 0f;
        latched = false;
        moveInput = Vector2.zero;
    }

    void CacheAnimatorParameter()
    {
        hasSpeedParam = false;
        if (animator == null || string.IsNullOrEmpty(speedParameter) || animator.runtimeAnimatorController == null) return;
        speedHash = Animator.StringToHash(speedParameter);
        foreach (var p in animator.parameters)
            if (p.nameHash == speedHash && p.type == AnimatorControllerParameterType.Float) { hasSpeedParam = true; break; }
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }
}
