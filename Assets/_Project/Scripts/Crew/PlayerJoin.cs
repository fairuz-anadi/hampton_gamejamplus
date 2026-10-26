using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Local co-op join: keyboard + up to 2 gamepads. Owner: Rabib.
/// Coming from the main menu, the players who took a seat there (PlayerSlots) are spawned straight away, each on
/// their own device, in seat order, and join-by-button is switched off. Playing a scene directly (nobody joined
/// on the menu) keeps join-by-button for testing.
/// </summary>
[RequireComponent(typeof(PlayerInputManager))]
public class PlayerJoin : MonoBehaviour
{
    [SerializeField] GameObject playerPrefab;
    [SerializeField] Vector3 spawnOffset = new Vector3(1.2f, 0f, 0f);

    PlayerInputManager manager;

    void Awake()
    {
        manager = GetComponent<PlayerInputManager>();
        manager.playerPrefab = playerPrefab;
        manager.joinBehavior = PlayerJoinBehavior.JoinPlayersWhenButtonIsPressed;
        manager.onPlayerJoined += OnPlayerJoined;
    }

    void OnDestroy()
    {
        if (manager != null) manager.onPlayerJoined -= OnPlayerJoined;
    }

    void Start()
    {
        if (PlayerSlots.Devices.Count == 0) return;
        manager.DisableJoining();
        for (int i = 0; i < PlayerSlots.Devices.Count; i++)
        {
            var device = PlayerSlots.Devices[i];
            string scheme = device is Keyboard ? "Keyboard" : "Gamepad";
            PlayerInput.Instantiate(playerPrefab, playerIndex: i, controlScheme: scheme, pairWithDevice: device);
        }
    }

    void OnPlayerJoined(PlayerInput input)
    {
        // Seat N stands on the deck's Spawn_N marker when there is one, otherwise beside PlayerSpawn.
        var marker = GameObject.Find($"Spawn_{input.playerIndex + 1}");
        Vector3 position;
        if (marker != null) position = marker.transform.position;
        else
        {
            var spawn = GameObject.Find("PlayerSpawn");
            position = (spawn != null ? spawn.transform.position : Vector3.zero) + spawnOffset * input.playerIndex;
        }

        // A CharacterController ignores direct moves while enabled, so switch it off for the teleport.
        var controller = input.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        input.transform.position = position;
        if (controller != null) controller.enabled = true;
    }
}
