using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Human-controlled crew member: joining, seat, Report/Refuse/CallHelp. Owner: Rabib.
/// Walking is done by ThirdPersonMover (Samprity): this feeds it the Move action. The mover keeps the camera's facing
/// fixed while a direction is held, so the follow camera turning behind the player doesn't bend the path.
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class PlayerController : MonoBehaviour, ICrewActor, IInteractSource, IRoomTarget
{
    PlayerInput playerInput;
    InputAction moveAction, interactAction, reportAction, refuseAction, callHelpAction;
    ThirdPersonMover mover;
    Interactor interactor;

    public int Seat => Mathf.Max(1, playerInput.playerIndex + 1);
    public bool IsBot => false;
    public bool IsStoker => false;
    public Vector3 Position => transform.position;
    /// <summary>Samprity's deck room volumes when the scene has them (Ship.unity), else the last RoomZone entered.</summary>
    public string RoomId => RoomVolume.All.Count > 0 ? RoomVolume.RoomAt(transform.position) : zoneRoom;
    string zoneRoom = "Unknown";
    public bool IsLockedOut { get; private set; }
    public bool InteractHeld => interactAction.IsPressed();
    public float SteerInput => moveAction.ReadValue<Vector2>().x;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        interactAction = playerInput.actions["Interact"];
        reportAction = playerInput.actions["Report"];
        refuseAction = playerInput.actions["Refuse"];
        callHelpAction = playerInput.actions["CallHelp"];
        interactor = GetComponent<Interactor>();

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
        // While steering the Wheel, the Move axis turns the ship instead of walking.
        bool steering = interactor != null && interactor.IsSteering;
        mover.SetMoveInput(steering ? Vector2.zero : moveAction.ReadValue<Vector2>());
        if (reportAction.WasPerformedThisFrame()) OrderDirector.Instance?.Report(this);
        if (refuseAction.WasPerformedThisFrame()) OrderDirector.Instance?.Refuse(this);
        if (callHelpAction.WasPerformedThisFrame() && interactor != null) interactor.CallHelp();
    }

    public void SetRoom(string roomId) => zoneRoom = roomId;
    public void SetLockedOut(bool locked) => IsLockedOut = locked;

    void ApplySeatColor()
    {
        var renderer = GetComponentInChildren<Renderer>();
        if (renderer == null) return;
        // CrewRoster.SeatColors is indexed by seat (index 0 is unused grey), so a seat matches its bot colour.
        var color = CrewRoster.SeatColors[Mathf.Clamp(Seat, 1, CrewRoster.SeatColors.Length - 1)];
        var mat = renderer.material;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;
    }
}
