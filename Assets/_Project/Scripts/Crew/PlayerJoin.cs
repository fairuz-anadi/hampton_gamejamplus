using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Local co-op join. If players already joined through the main menu (PlayerSlots), spawns exactly
/// those devices with PlayerInput.Instantiate and disables further joining. Otherwise (playing a
/// scene directly, nobody joined) falls back to join-by-button so the sandbox still works alone.
/// Owner: Rabib.
/// </summary>
[RequireComponent(typeof(PlayerInputManager))]
public class PlayerJoin : MonoBehaviour
{
    [SerializeField] GameObject playerPrefab;
    [SerializeField] Vector3 spawnOffset = new Vector3(1.2f, 0f, 0f);

    void Awake()
    {
        var manager = GetComponent<PlayerInputManager>();
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

    void SpawnFromSlot(int playerIndex, InputDevice device)
    {
        string scheme = device is Keyboard ? "Keyboard" : "Gamepad";
        // PlayerInput.Instantiate bypasses PlayerInputManager's own join flow, so onPlayerJoined never fires for it.
        var input = PlayerInput.Instantiate(playerPrefab, playerIndex: playerIndex, controlScheme: scheme, pairWithDevice: device);
        PositionAt(input, playerIndex);
    }

    void OnPlayerJoined(PlayerInput input) => PositionAt(input, input.playerIndex);

    void PositionAt(PlayerInput input, int playerIndex)
    {
        var spawn = GameObject.Find("PlayerSpawn");
        Vector3 origin = spawn != null ? spawn.transform.position : Vector3.zero;
        input.transform.position = origin + spawnOffset * playerIndex;
    }
}
