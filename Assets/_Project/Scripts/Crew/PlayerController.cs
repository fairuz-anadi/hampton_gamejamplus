using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Human-controlled crew member: joining, seat, Report/Refuse. Owner: Rabib.
/// Walking is done by ThirdPersonMover (Samprity): this feeds it the Move action. The mover keeps the camera's facing
/// fixed while a direction is held, so the follow camera turning behind the player doesn't bend the path.
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class PlayerController : MonoBehaviour, ICrewActor
{
    static readonly Color[] SeatColors =
    {
        Color.red, new Color(0.25f, 0.55f, 1f), Color.green, Color.yellow,
    };

    PlayerInput playerInput;
    InputAction moveAction, reportAction, refuseAction;
    ThirdPersonMover mover;

    public int Seat => Mathf.Max(1, playerInput.playerIndex + 1);
    public bool IsBot => false;
    public bool IsStoker => false;
    public Vector3 Position => transform.position;
    public string RoomId { get; private set; } = "Unknown";
    public bool IsLockedOut { get; private set; }

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        reportAction = playerInput.actions["Report"];
        refuseAction = playerInput.actions["Refuse"];

        // Each player reads its own device through PlayerInput, so the mover must not read the keyboard itself.
        if (!TryGetComponent(out mover)) mover = gameObject.AddComponent<ThirdPersonMover>();
        mover.UseExternalInput();
    }

    void Start()
    {
        ApplySeatColor();
    }

    void OnEnable() => CrewRegistry.Register(this);
    void OnDisable() => CrewRegistry.Unregister(this);

    void Update()
    {
        mover.SetMoveInput(moveAction.ReadValue<Vector2>());
        if (reportAction.WasPerformedThisFrame()) OrderDirector.Instance?.Report(this);
        if (refuseAction.WasPerformedThisFrame()) OrderDirector.Instance?.Refuse(this);
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
