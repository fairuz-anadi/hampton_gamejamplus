using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Local co-op join. If players already joined through the main menu (PlayerSlots), spawns exactly
/// those devices with PlayerInput.Instantiate, each on its own device in seat order, and disables
/// further joining. Otherwise (playing a scene directly, nobody joined) falls back to join-by-button
/// so the sandbox still works alone. Owner: Rabib.
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

        if (PlayerSlots.Devices.Count == 0)
        {
            manager.joinBehavior = PlayerJoinBehavior.JoinPlayersWhenButtonIsPressed;
            manager.onPlayerJoined += OnPlayerJoined;
            return;
        }

        manager.DisableJoining();
        for (int i = 0; i < PlayerSlots.Devices.Count; i++)
            SpawnFromSlot(i, PlayerSlots.Devices[i]);
    }

    void OnDestroy()
    {
        if (manager != null) manager.onPlayerJoined -= OnPlayerJoined;
    }

    void SpawnFromSlot(int playerIndex, InputDevice device)
    {
        string scheme = device is Keyboard ? "Keyboard" : "Gamepad";
        // PlayerInput.Instantiate bypasses PlayerInputManager's own join flow, so onPlayerJoined never fires for it.
        var input = PlayerInput.Instantiate(playerPrefab, playerIndex: playerIndex, controlScheme: scheme, pairWithDevice: device);
        PlaceAt(input, playerIndex);
    }

    void OnPlayerJoined(PlayerInput input) => PlaceAt(input, input.playerIndex);

    /// <summary>Seat N stands on the deck's Spawn_N marker when there is one, otherwise beside PlayerSpawn.</summary>
    void PlaceAt(PlayerInput input, int playerIndex)
    {
        var marker = GameObject.Find($"Spawn_{playerIndex + 1}");
        Vector3 position;
        if (marker != null) position = marker.transform.position;
        else
        {
            var spawn = GameObject.Find("PlayerSpawn");
            position = (spawn != null ? spawn.transform.position : Vector3.zero) + spawnOffset * playerIndex;
        }

        // A CharacterController ignores direct moves while enabled, so switch it off for the teleport.
        var controller = input.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        input.transform.position = position;
        if (controller != null) controller.enabled = true;
    }
}
