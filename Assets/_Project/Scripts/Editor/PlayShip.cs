using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Opens Ship.unity and presses Play. Menu: Port > Play Ship. Command line: -executeMethod PlayShip.Run</summary>
public static class PlayShip
{
    [MenuItem("Port/Play Ship %#p")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Ship.unity");
        EditorApplication.EnterPlaymode();
    }
}
