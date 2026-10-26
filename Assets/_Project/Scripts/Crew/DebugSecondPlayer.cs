using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
/// <summary>
/// DEBUG ONLY: a second crew member driven by raw keys on the same keyboard (arrows to move,
/// Right Ctrl/Shift/Enter for Interact/Report/Refuse), so 2-player flow is testable without a
/// second device. Reads Keyboard.current directly, bypassing PlayerInput entirely, so it can't
/// fight the real P1 Keyboard / P2-P3 Gamepad control schemes over device pairing. Never compiled
/// into a release build. Owner: Rabib.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class DebugSecondPlayer : MonoBehaviour, ICrewActor, IInteractSource
{
    [SerializeField] float moveSpeed = 4f;
    [SerializeField] float turnSpeed = 12f;

    CharacterController controller;
    Camera deckCamera;
    int seat;

    public int Seat => seat;
    public bool IsBot => false;
    public bool IsStoker => false;
    public Vector3 Position => transform.position;
    public string RoomId { get; private set; } = "Unknown";
    public bool IsLockedOut { get; private set; }

    public bool InteractHeld => Keyboard.current != null && Keyboard.current.rightCtrlKey.isPressed;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {
        deckCamera = Camera.main;
        ApplyDebugColor();
    }

    void OnEnable()
    {
        seat = NextFreeSeat();
        CrewRegistry.Register(this);
    }

    void OnDisable() => CrewRegistry.Unregister(this);

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        Move(kb);
        if (kb.rightShiftKey.wasPressedThisFrame) OrderDirector.Instance?.Report(this);
        if (kb.enterKey.wasPressedThisFrame) OrderDirector.Instance?.Refuse(this);
    }

    void Move(Keyboard kb)
    {
        Vector2 input = Vector2.zero;
        if (kb.upArrowKey.isPressed) input.y += 1f;
        if (kb.downArrowKey.isPressed) input.y -= 1f;
        if (kb.rightArrowKey.isPressed) input.x += 1f;
        if (kb.leftArrowKey.isPressed) input.x -= 1f;

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;
        if (deckCamera != null)
        {
            forward = Vector3.ProjectOnPlane(deckCamera.transform.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(deckCamera.transform.right, Vector3.up).normalized;
        }

        Vector3 moveDir = forward * input.y + right * input.x;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        controller.Move((moveDir * moveSpeed + Vector3.down * 2f) * Time.deltaTime);

        if (moveDir.sqrMagnitude > 0.0001f)
        {
            var targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }
    }

    public void SetRoom(string roomId) => RoomId = roomId;
    public void SetLockedOut(bool locked) => IsLockedOut = locked;

    static int NextFreeSeat()
    {
        for (int s = 1; s <= 4; s++)
            if (CrewRegistry.BySeat(s) == null) return s;
        return 4;
    }

    void ApplyDebugColor()
    {
        var renderer = GetComponentInChildren<Renderer>();
        if (renderer == null) return;
        var color = new Color(1f, 0.5f, 0f);
        var mat = renderer.material;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;
    }
}
#endif
