using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// DEBUG ONLY: a second crew member driven by raw keys on the same keyboard (arrows to move,
/// Right Ctrl/Shift/Enter/Alt for Interact/Report/Refuse/CallHelp), so 2-player flow is testable without a
/// second device. Reads Keyboard.current directly, bypassing PlayerInput entirely, so it can't
/// fight the real P1 Keyboard / P2-P3 Gamepad control schemes over device pairing. Self-destructs
/// outside debug builds, and goes inactive entirely once PlayerSlots has real players (it must not
/// take a seat from them). Walking goes through ThirdPersonMover, same as every real player, fed by
/// the same raw arrow-key input. Takes the highest free seat (counting down from 4) so it stays
/// clear of PlayerSlots humans, who always claim seats starting at 1. Owner: Rabib.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class DebugSecondPlayer : MonoBehaviour, ICrewActor, IInteractSource, IRoomTarget
{
    ThirdPersonMover mover;
    int seat;

    public int Seat => seat;
    public bool IsBot => false;
    public bool IsStoker => false;
    public Vector3 Position => transform.position;
    /// <summary>Samprity's deck room volumes when the scene has them (Ship.unity), else the last RoomZone entered.</summary>
    public string RoomId => RoomVolume.All.Count > 0 ? RoomVolume.RoomAt(transform.position) : zoneRoom;
    string zoneRoom = "Unknown";
    public bool IsLockedOut { get; private set; }

    public bool InteractHeld => Keyboard.current != null && Keyboard.current.rightCtrlKey.isPressed;

    void Awake()
    {
        if (!Debug.isDebugBuild) { Destroy(this); return; }
        if (PlayerSlots.Devices.Count > 0) { gameObject.SetActive(false); return; }

        if (!TryGetComponent(out mover)) mover = gameObject.AddComponent<ThirdPersonMover>();
        mover.UseExternalInput();
    }

    void Start()
    {
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

        Vector2 input = Vector2.zero;
        if (kb.upArrowKey.isPressed) input.y += 1f;
        if (kb.downArrowKey.isPressed) input.y -= 1f;
        if (kb.rightArrowKey.isPressed) input.x += 1f;
        if (kb.leftArrowKey.isPressed) input.x -= 1f;
        mover.SetMoveInput(input);

        if (kb.rightShiftKey.wasPressedThisFrame) OrderDirector.Instance?.Report(this);
        if (kb.enterKey.wasPressedThisFrame) OrderDirector.Instance?.Refuse(this);
        if (kb.rightAltKey.wasPressedThisFrame && TryGetComponent(out Interactor interactor)) interactor.CallHelp();
    }

    public void SetRoom(string roomId) => zoneRoom = roomId;
    public void SetLockedOut(bool locked) => IsLockedOut = locked;

    static int NextFreeSeat()
    {
        for (int s = 4; s >= 1; s--)
            if (CrewRegistry.BySeat(s) == null) return s;
        return 1;
    }

    void ApplyDebugColor()
    {
        var renderer = GetComponentInChildren<Renderer>();
        if (renderer == null) return;
        // Same seat colours as the bots and real players (CrewRoster.SeatColors is indexed by seat).
        var color = CrewRoster.SeatColors[Mathf.Clamp(seat, 1, CrewRoster.SeatColors.Length - 1)];
        var mat = renderer.material;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;
    }
}
