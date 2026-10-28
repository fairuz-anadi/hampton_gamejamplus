using UnityEngine;

/// <summary>
/// Mouse look needs the cursor out of the way: locked and hidden while a voyage is being played, free whenever a
/// screen wants the mouse (how-to-play card, pause menu, muster vote, results). Spawned by GameSystems in Ship.
/// Owner: Anadi.
/// </summary>
public class CursorLock : MonoBehaviour
{
    void Update()
    {
        var gm = GameManager.Instance;
        bool playing = gm != null && gm.State >= GameState.Part1 && gm.State <= GameState.Iceberg;
        bool screenUp = IntroCard.IsShowing || PartCard.IsShowing || (PauseMenu.Instance != null && PauseMenu.Instance.IsPaused)
                        || (MusterVote.Instance != null && MusterVote.Instance.IsRunning) || !Application.isFocused;
        Set(playing && !screenUp);
    }

    void OnDisable() => Set(false);

    static void Set(bool locked)
    {
        var mode = locked ? CursorLockMode.Locked : CursorLockMode.None;
        if (Cursor.lockState != mode) Cursor.lockState = mode;
        if (Cursor.visible == locked) Cursor.visible = !locked;
    }
}
