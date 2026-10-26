using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Human-controlled crew member: camera-relative movement, Report/Refuse. Owner: Rabib.</summary>
[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class PlayerController : MonoBehaviour, ICrewActor
{
    static readonly Color[] SeatColors =
    {
        Color.red, new Color(0.25f, 0.55f, 1f), Color.green, Color.yellow,
    };

    [SerializeField] float moveSpeed = 4f;
    [SerializeField] float turnSpeed = 12f;

    CharacterController controller;
    PlayerInput playerInput;
    InputAction moveAction, reportAction, refuseAction;
    Camera deckCamera;

    public int Seat => Mathf.Max(1, playerInput.playerIndex + 1);
    public bool IsBot => false;
    public bool IsStoker => false;
    public Vector3 Position => transform.position;
    public string RoomId { get; private set; } = "Unknown";
    public bool IsLockedOut { get; private set; }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        reportAction = playerInput.actions["Report"];
        refuseAction = playerInput.actions["Refuse"];
    }

    void Start()
    {
        deckCamera = Camera.main;
        ApplySeatColor();
    }

    void OnEnable() => CrewRegistry.Register(this);
    void OnDisable() => CrewRegistry.Unregister(this);

    void Update()
    {
        Move();
        if (reportAction.WasPerformedThisFrame()) OrderDirector.Instance?.Report(this);
        if (refuseAction.WasPerformedThisFrame()) OrderDirector.Instance?.Refuse(this);
    }

    void Move()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
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

    void ApplySeatColor()
    {
        var renderer = GetComponentInChildren<Renderer>();
        if (renderer == null) return;
        var color = SeatColors[Mathf.Clamp(Seat - 1, 0, SeatColors.Length - 1)];
        var mat = renderer.material;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;
    }
}
