using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trigger volume that sets a crew member's RoomId (PlayerController, DebugSecondPlayer, and later
/// Riham's bots, via IRoomTarget) while they're inside. Tracks each actor's currently-overlapping
/// zones so leaving one zone falls back to another still-overlapping zone instead of going blank,
/// and so overlapping zones resolve to whichever was entered most recently. A CharacterController
/// already overlapping a zone when it spawns still fires OnTriggerEnter on the next physics step,
/// so spawning inside a zone is handled without extra code. Owner: Rabib.
/// </summary>
[RequireComponent(typeof(Collider))]
public class RoomZone : MonoBehaviour
{
    [SerializeField] string roomId = "Unknown";

    static readonly Dictionary<IRoomTarget, List<string>> occupied = new Dictionary<IRoomTarget, List<string>>();

    void Reset() => GetComponent<Collider>().isTrigger = true;

    void OnTriggerEnter(Collider other)
    {
        var target = other.GetComponent<IRoomTarget>();
        if (target != null) Enter(target);
    }

    void OnTriggerExit(Collider other)
    {
        var target = other.GetComponent<IRoomTarget>();
        if (target != null) Exit(target);
    }

    void Enter(IRoomTarget target)
    {
        if (!occupied.TryGetValue(target, out var stack))
        {
            stack = new List<string>();
            occupied[target] = stack;
        }
        stack.Remove(roomId);
        stack.Add(roomId);
        target.SetRoom(roomId);
    }

    void Exit(IRoomTarget target)
    {
        if (!occupied.TryGetValue(target, out var stack)) return;
        stack.Remove(roomId);
        target.SetRoom(stack.Count > 0 ? stack[stack.Count - 1] : "Unknown");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => occupied.Clear();
}
