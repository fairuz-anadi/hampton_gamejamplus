using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Keyboard and gamepad navigation for the placeholder IMGUI menus, so every menu works without a mouse. Owner: Anadi.
///   Move:    arrows or W/S (A/D across), d-pad on any gamepad
///   Confirm: Enter, or A / ✕
///   Back:    Esc, or B / ○
/// </summary>
public static class MenuControls
{
    /// <summary>-1, 0 or +1 this frame. Vertical uses up/down, otherwise left/right.</summary>
    public static int Step(bool vertical = true)
    {
        int d = 0;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (vertical)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) d--;
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) d++;
            }
            else
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) d--;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) d++;
            }
        }
        foreach (var pad in Gamepad.all)
        {
            if (vertical)
            {
                if (pad.dpad.up.wasPressedThisFrame) d--;
                if (pad.dpad.down.wasPressedThisFrame) d++;
            }
            else
            {
                if (pad.dpad.left.wasPressedThisFrame) d--;
                if (pad.dpad.right.wasPressedThisFrame) d++;
            }
        }
        return System.Math.Sign(d);
    }

    /// <summary>Moves a selection index through count items, wrapping at the ends.</summary>
    public static int Move(int index, int count, bool vertical = true) =>
        count <= 0 ? 0 : ((index + Step(vertical)) % count + count) % count;

    public static bool Confirm()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) return true;
        foreach (var pad in Gamepad.all) if (pad.buttonSouth.wasPressedThisFrame) return true;
        return false;
    }

    public static bool Back()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
        foreach (var pad in Gamepad.all) if (pad.buttonEast.wasPressedThisFrame) return true;
        return false;
    }

    /// <summary>A menu button that shows when it's the keyboard/gamepad selection. Returns true when clicked.</summary>
    public static bool Button(string text, GUIStyle style, bool selected, params GUILayoutOption[] options)
    {
        var old = GUI.backgroundColor;
        if (selected) GUI.backgroundColor = new Color(1f, 0.82f, 0.45f);
        bool clicked = GUILayout.Button(selected ? $"> {text} <" : text, style, options);
        GUI.backgroundColor = old;
        return clicked;
    }
}
