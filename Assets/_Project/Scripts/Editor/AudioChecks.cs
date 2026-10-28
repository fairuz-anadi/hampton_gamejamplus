using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Plays a voyage headless and checks the sound wiring. Owner: Samprity.
///   Unity.exe -batchmode -projectPath . -executeMethod AudioChecks.RunBatch      (no -quit: it exits itself)
/// Checks: the bank and mixer are complete, AudioManager spawns in Ship and replaces the placeholder tones, the
/// ambience and waltz play, the Captain's voice goes through the Voice group, parts switch snapshots, the crew's
/// footsteps play, the finale's drone rises and the impact drops to the Aftermath, and Results stays near silent.
/// Writes Logs/AudioChecks.txt.
/// </summary>
public static class AudioChecks
{
    static readonly StringBuilder report = new StringBuilder();
    static readonly List<string> errors = new List<string>();
    static bool ok = true, finaleChecked;
    static int step;
    static float stepStart;
    static EnterPlayModeOptions savedOptions;
    static bool savedEnabled;

    public static void RunBatch()
    {
        report.Clear().Append("[Audio checks]\n");
        var bank = AssetDatabase.LoadAssetAtPath<AudioBank>("Assets/_Project/Audio/Resources/AudioBank.asset");
        Check(bank != null, "AudioBank exists in Resources");
        if (bank != null)
        {
            var empty = typeof(AudioBank).GetFields()
                .Where(f => (f.FieldType == typeof(AudioClip) && f.GetValue(bank) == null)
                         || (f.FieldType == typeof(AudioClip[]) && ((AudioClip[])f.GetValue(bank)).Length == 0)
                         || (f.FieldType == typeof(AudioMixerGroup) && f.GetValue(bank) == null)
                         || (f.FieldType == typeof(AudioMixer) && f.GetValue(bank) == null))
                .Select(f => f.Name).ToList();
            Check(empty.Count == 0, empty.Count == 0 ? "Every bank slot is filled" : "Empty bank slots: " + string.Join(", ", empty));
            Check(bank.mixer != null && AudioBuilder.Snapshots.All(s => bank.mixer.FindSnapshot(s) != null), "Mixer has Calm, Off, Truth, Iceberg, Aftermath snapshots");
            Check(bank.voice != null && bank.voice.name == "Voice", "Mixer has a Voice group");
            Check(bank.stepsWood.Length >= 4 && bank.stepsMetal.Length >= 4, $"Footsteps: {bank.stepsWood.Length} wood, {bank.stepsMetal.Length} metal");
        }

        savedEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        savedOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.OpenScene(OpeningSceneBuilder.ShipScenePath, OpenSceneMode.Single);
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void OnLog(string message, string stack, LogType type)
    {
        if (stack != null && stack.Contains("UnityEditor.Search")) return;
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors.Add($"{type}: {message.Split('\n')[0]}");
    }

    static void Check(bool pass, string what)
    {
        ok &= pass;
        report.Append($"  {(pass ? "PASS" : "FAIL")}  {what}\n");
    }

    static void Next() { step++; stepStart = Time.realtimeSinceStartup; }

    static AudioSource Layer(string name) => AudioManager.Instance != null ? AudioManager.Instance.transform.Find(name)?.GetComponent<AudioSource>() : null;

    /// <summary>The loop is missing, stopped, or faded right down.</summary>
    static bool Silent(string name)
    {
        var src = Layer(name);
        return src == null || !src.isPlaying || src.volume < 0.01f;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        float inStep = Time.realtimeSinceStartup - stepStart;
        var audio = AudioManager.Instance;

        switch (step)
        {
            case 0:
                if (GameManager.Instance == null || audio == null) { if (inStep > 20f) Fail("AudioManager never spawned in Ship"); return; }
                IntroCard.Instance?.Dismiss();
                Check(!StationSfx.Enabled, "Placeholder station tones are switched off");
                Next();
                break;

            case 1: // Part 1 settles
                if (inStep < 3f) return;
                Check(audio.CurrentSnapshot == "Calm", $"Part 1 uses the Calm snapshot ({audio.CurrentSnapshot})");
                foreach (var name in new[] { "Sea", "Deck", "Engine", "HullCreak", "Passengers" })
                {
                    var src = Layer(name);
                    Check(src != null && src.isPlaying && src.clip != null, $"{name} loop is playing");
                }
                Check(Silent("Waltz"), "No music in Part 1 (the waltz stays silent)");
                var captain = CaptainDirector.Instance;
                var blip = captain != null ? captain.GetComponents<AudioSource>() : new AudioSource[0];
                Check(blip.Length > 0 && blip.All(s => s.outputAudioMixerGroup != null && s.outputAudioMixerGroup.name == "Voice"), "The Captain's voice goes through the Voice group");
                // No typing voice (her call, 28 Oct): the Captain speaks with his recorded voice instead.
                Check(blip.Length > 0 && blip.All(s => s.mute), "The per-letter typing voice is off");
                GameManager.Instance.EnterPart(2);
                Next();
                break;

            case 2: // Part 2: the off snapshot, detuned waltz, still footsteps from the crew walking about
                if (inStep < 2f) return;
                if (audio.StepsPlayed == 0 && inStep < 25f) return;
                Check(audio.CurrentSnapshot == "Off", $"Part 2 switches to the Off snapshot ({audio.CurrentSnapshot})");
                Check(Silent("Waltz"), "No music in Part 2");
                Check(audio.StepsPlayed > 0, $"Crew footsteps play ({audio.StepsPlayed} so far)");
                GameManager.Instance.EnterPart(3);
                Next();
                break;

            case 3:
                if (inStep < 2f) return;
                Check(audio.CurrentSnapshot == "Truth", $"Part 3 switches to the Truth snapshot ({audio.CurrentSnapshot})");
                Check(Silent("Sustained") && Silent("Waltz"), "No music in Part 3 (no held cello note)");
                GameManager.Instance.StartIceberg();
                Next();
                break;

            case 4: // The finale: drone rising, then the hit
                if (inStep < 5f) return;
                if (!finaleChecked)
                {
                    finaleChecked = true;
                    Check(audio.CurrentSnapshot == "Iceberg", $"The finale uses the Iceberg snapshot ({audio.CurrentSnapshot})");
                    Check(Layer("Drone") != null && Layer("Drone").isPlaying, "The low drone plays during the finale");
                }
                if (!audio.Impacted) { if (inStep > 45f) Fail("no impact heard"); return; }
                Check(audio.CurrentSnapshot == "Aftermath", "The impact drops into the Aftermath snapshot");
                Next();
                break;

            case 5: // Results: a fresh, near-silent manager
                if (audio == null || !audio.ResultsOnly) { if (inStep > 15f) Fail("no Results audio"); return; }
                if (inStep < 1f) return;
                Check(Layer("Sea") != null && Layer("Sea").isPlaying, "Results keeps only the far-off sea");
                Check(Layer("Waltz") == null, "Results has no music");
                Check(errors.Count == 0, errors.Count == 0 ? "No errors or exceptions logged" : $"{errors.Count} errors: " + string.Join(" | ", errors.Distinct().Take(6)));
                Finish();
                break;
        }
    }

    static void Fail(string why) { Check(false, "Timed out: " + why); Finish(); }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        report.Append(ok ? "ALL PASSED" : "SOME CHECKS FAILED");
        System.IO.File.WriteAllText("Logs/AudioChecks.txt", report.ToString());
        Debug.Log(report.ToString());
        EditorSettings.enterPlayModeOptionsEnabled = savedEnabled;
        EditorSettings.enterPlayModeOptions = savedOptions;
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
    }
}
