using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The humans who joined on the main menu, in seat order (index 0 = seat 1). Owner: Anadi.
///   Rabib: in Ship, spawn one player per device:
///          PlayerInput.Instantiate(prefab, playerIndex: i, pairWithDevice: PlayerSlots.Devices[i]), seat = i + 1.
///   Riham: CrewRoster gives seats PlayerSlots.HumanCount + 1 .. 4 to bots.
/// Survives scene loads, so Play again keeps the same players. Pressing Play straight in a scene means nobody
/// joined: HumanCount is then 1 and FirstDevice falls back to the keyboard.
/// </summary>
public static class PlayerSlots
{
    public const int MaxPlayers = 3;
    public const int Seats = 4;

    static readonly List<InputDevice> devices = new List<InputDevice>(MaxPlayers);

    public static IReadOnlyList<InputDevice> Devices => devices;

    /// <summary>Humans aboard, never 0, so testing a scene directly still gets one player.</summary>
    public static int HumanCount => Mathf.Max(1, devices.Count);

    public static int BotCount => Seats - HumanCount;

    /// <summary>The device for seat 1, or the keyboard when nobody joined through the menu.</summary>
    public static InputDevice FirstDevice => devices.Count > 0 ? devices[0] : Keyboard.current;

    public static bool IsJoined(InputDevice device) => devices.Contains(device);

    /// <summary>Adds the device to the next free seat. False if it's already in or all 3 human seats are taken.</summary>
    public static bool Join(InputDevice device)
    {
        if (device == null || devices.Contains(device) || devices.Count >= MaxPlayers) return false;
        devices.Add(device);
        return true;
    }

    public static bool Leave(InputDevice device) => devices.Remove(device);

    public static void Clear() => devices.Clear();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => devices.Clear();
}
