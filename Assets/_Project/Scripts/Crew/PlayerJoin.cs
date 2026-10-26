using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Local co-op join: keyboard + up to 2 gamepads. Owner: Rabib.</summary>
[RequireComponent(typeof(PlayerInputManager))]
public class PlayerJoin : MonoBehaviour
{
    [SerializeField] GameObject playerPrefab;
    [SerializeField] Vector3 spawnOffset = new Vector3(1.2f, 0f, 0f);

    void Awake()
    {
        var manager = GetComponent<PlayerInputManager>();
        manager.playerPrefab = playerPrefab;
        manager.joinBehavior = PlayerJoinBehavior.JoinPlayersWhenButtonIsPressed;
        manager.onPlayerJoined += OnPlayerJoined;
    }

    void OnPlayerJoined(PlayerInput input)
    {
        var spawn = GameObject.Find("PlayerSpawn");
        Vector3 origin = spawn != null ? spawn.transform.position : Vector3.zero;
        input.transform.position = origin + spawnOffset * input.playerIndex;
    }
}
