using System;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Lets a command line open a scene and press Play once the Editor has loaded. Owner: Samprity.
///   Unity.exe -projectPath . -portScene Assets/_Project/Scenes/Sandbox/Samprity_Art.unity -portPlay
/// Does nothing when the flags aren't there.
/// </summary>
[InitializeOnLoad]
static class SandboxLauncher
{
    const string HandledKey = "Port.SandboxLauncher.Handled";

    static SandboxLauncher()
    {
        // Script reloads re-run this; only act once per Editor session.
        if (SessionState.GetBool(HandledKey, false)) return;
        string scene = null;
        bool play = false;
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-portScene" && i + 1 < args.Length) scene = args[i + 1];
            if (args[i] == "-portPlay") play = true;
        }
        if (scene == null && !play) return;
        SessionState.SetBool(HandledKey, true);

        EditorApplication.delayCall += () =>
        {
            if (scene != null) EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            if (play) EditorApplication.EnterPlaymode();
        };
    }
}
