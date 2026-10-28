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

    // ---------- Names (typed on the menu, remembered between sessions) ----------

    public const int MaxNameLength = 14;

    static readonly string[] names = new string[MaxPlayers + 1];

    /// <summary>The name a human in this seat typed, or "" if none (CrewRegistry then uses the seat's crew name).</summary>
    public static string NameFor(int seat)
    {
        if (seat < 1 || seat > MaxPlayers) return "";
        names[seat] ??= PlayerPrefs.GetString("PlayerName" + seat, "");
        return names[seat];
    }

    public static void SetName(int seat, string name)
    {
        if (seat < 1 || seat > MaxPlayers) return;
        names[seat] = Clean(name);
        PlayerPrefs.SetString("PlayerName" + seat, names[seat]);
    }

    /// <summary>Trimmed, one line, and without the characters the Captain's typewriter treats as markup ({ | }).</summary>
    public static string Clean(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (char c in raw.Trim())
            if (!char.IsControl(c) && c != '{' && c != '}' && c != '|') sb.Append(c);
        string s = sb.ToString().Trim();
        return s.Length > MaxNameLength ? s.Substring(0, MaxNameLength).TrimEnd() : s;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        devices.Clear();
        System.Array.Clear(names, 0, names.Length);
    }
}
