using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Phase 0 project setup. Owner: Anadi. Safe to run again: it only creates what's missing.
///   In the Editor:  Port > Run Project Setup
///   Headless:       Unity.exe -batchmode -projectPath . -executeMethod ProjectSetup.AddPackagesBatch
///                   Unity.exe -batchmode -projectPath . -executeMethod ProjectSetup.RunBatch -quit
/// </summary>
public static class ProjectSetup
{
    static readonly string[] Packages =
    {
        "com.unity.probuilder",
        "com.unity.ai.navigation",
        "com.unity.cinemachine",
        "com.unity.inputsystem",
    };

    static readonly string[] Folders =
    {
        "Art/Models", "Art/Materials", "Art/Textures", "Art/Animations", "Art/UI", "Art/VFX",
        "Audio/Music", "Audio/SFX", "Audio/Voice", "Audio/Mixer",
        "Data/Resources/Orders", "Data/Endings",
        "Prefabs/Crew", "Prefabs/Stations", "Prefabs/UI",
        "Scenes/Sandbox",
        "Scripts/Core", "Scripts/Crew", "Scripts/Stations", "Scripts/AI", "Scripts/Captain",
        "Scripts/UI", "Scripts/Audio", "Scripts/Debug", "Scripts/Editor",
        "Settings/URP", "Settings/Input",
    };

    // MainMenu first so it's build index 0.
    static readonly string[] MainScenes = { "MainMenu", "Ship", "Results" };
    static readonly string[] Sandboxes = { "Anadi_Systems", "Rabib_Stations", "Riham_AI", "Samprity_Art" };

    const string Root = "Assets/_Project";

    [MenuItem("Port/Run Project Setup")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ApplySettings();
        CreateFolders();
        TidyTemplate();
        CreateScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] Done. Restart Unity if it asks to, so the Input System setting takes effect.");
    }

    public static void RunBatch() => Run();

    // ---------- Packages ----------

    static AddAndRemoveRequest packageRequest;

    [MenuItem("Port/Add Team Packages")]
    public static void AddPackages()
    {
        packageRequest = Client.AddAndRemove(Packages);
        EditorApplication.update += WaitForPackages;
    }

    /// <summary>Adds the packages, then quits. Run this before RunBatch.</summary>
    public static void AddPackagesBatch() => AddPackages();

    static void WaitForPackages()
    {
        if (!packageRequest.IsCompleted) return;
        EditorApplication.update -= WaitForPackages;

        bool ok = packageRequest.Status == StatusCode.Success;
        if (ok) Debug.Log("[ProjectSetup] Packages added: " + string.Join(", ", Packages));
        else Debug.LogError("[ProjectSetup] Adding packages failed: " + packageRequest.Error?.message);

        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

    // ---------- Settings ----------

    static void ApplySettings()
    {
        EditorSettings.serializationMode = SerializationMode.ForceText;
        VersionControlSettings.mode = "Visible Meta Files";

        PlayerSettings.productName = "The Promised Port";
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

        // Input System only (old Input Manager off). There's no public API for this one.
        var playerSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (playerSettings.Length > 0)
        {
            var so = new SerializedObject(playerSettings[0]);
            var handler = so.FindProperty("activeInputHandler");
            if (handler != null && handler.intValue != 1)
            {
                handler.intValue = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    // ---------- Folders ----------

    static void CreateFolders()
    {
        foreach (var f in Folders) EnsureFolder($"{Root}/{f}");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    /// <summary>Moves the URP template's settings into our folders and removes its sample content.</summary>
    static void TidyTemplate()
    {
        if (AssetDatabase.IsValidFolder("Assets/Settings"))
        {
            foreach (var guid in AssetDatabase.FindAssets("", new[] { "Assets/Settings" }))
            {
                string from = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(from)) continue;
                string to = $"{Root}/Settings/URP/{Path.GetFileName(from)}";
                string err = AssetDatabase.MoveAsset(from, to);
                if (!string.IsNullOrEmpty(err)) Debug.LogWarning($"[ProjectSetup] Couldn't move {from}: {err}");
            }
            if (AssetDatabase.FindAssets("", new[] { "Assets/Settings" }).Length == 0)
                AssetDatabase.DeleteAsset("Assets/Settings");
        }

        AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
        if (AssetDatabase.IsValidFolder("Assets/Scenes") && AssetDatabase.FindAssets("", new[] { "Assets/Scenes" }).Length == 0)
            AssetDatabase.DeleteAsset("Assets/Scenes");
        AssetDatabase.DeleteAsset("Assets/TutorialInfo");
        AssetDatabase.DeleteAsset("Assets/Readme.asset");
    }

    // ---------- Scenes ----------

    static void CreateScenes()
    {
        foreach (var name in MainScenes) EnsureScene($"{Root}/Scenes/{name}.unity", name != "MainMenu" && name != "Results");
        foreach (var name in Sandboxes) EnsureScene($"{Root}/Scenes/Sandbox/{name}.unity", true);

        var build = new EditorBuildSettingsScene[MainScenes.Length];
        for (int i = 0; i < MainScenes.Length; i++)
            build[i] = new EditorBuildSettingsScene($"{Root}/Scenes/{MainScenes[i]}.unity", true);
        EditorBuildSettings.scenes = build;
    }

    /// <summary>Creates a scene with a camera and a light. Deck scenes get the 2.5D camera (Samprity tunes the final angle).</summary>
    static void EnsureScene(string path, bool deckCamera)
    {
        if (File.Exists(path)) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        if (deckCamera && Camera.main != null)
        {
            var cam = Camera.main;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 24f, -19f), Quaternion.Euler(52f, 0f, 0f));
            cam.fieldOfView = 30f;
        }
        EditorSceneManager.SaveScene(scene, path);
    }
}
