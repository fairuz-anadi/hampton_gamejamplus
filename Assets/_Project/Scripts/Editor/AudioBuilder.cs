using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

/// <summary>
/// Port > Samprity > Build Audio. Owner: Samprity.
///   1. Import settings for everything in Audio/ (loops compressed in memory, short hits decompressed on load).
///   2. Audio/Mixer/PortMixer.mixer: Master > Music, Ambience, SFX, Stations, Footsteps, Voice, and one snapshot per
///      story beat (Calm, Off, Truth, Iceberg, Aftermath). The Voice group's filters get stronger every part: thinner,
///      grittier, echoing and a touch slower. The ambience is muffled more and more, and nearly gone in the Aftermath.
///   3. Audio/Resources/AudioBank.asset with every clip AudioManager plays.
/// Unity has no public API for making mixers, so step 2 drives the internal UnityEditor.Audio classes by reflection.
/// Rebuilding replaces the mixer. The WAVs come from Tools/make_game_audio.py.
///   Unity.exe -batchmode -projectPath . -executeMethod AudioBuilder.BuildBatch -quit
/// </summary>
public static class AudioBuilder
{
    const string Root = "Assets/_Project/Audio";
    const string MixerPath = Root + "/Mixer/PortMixer.mixer";
    const string BankPath = Root + "/Resources/AudioBank.asset";

    public static readonly string[] Snapshots = { "Calm", "Off", "Truth", "Iceberg", "Aftermath" };

    static readonly StringBuilder log = new StringBuilder();

    [MenuItem("Port/Samprity/Build Audio")]
    public static void Build()
    {
        log.Clear().Append("[Audio build]\n");
        ConfigureImports();
        var mixer = BuildMixer();
        BuildBank(mixer);
        AssetDatabase.SaveAssets();
        System.IO.File.WriteAllText("Logs/AudioBuild.txt", log.ToString());
        Debug.Log(log.ToString());
    }

    public static void BuildBatch()
    {
        try { Build(); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    // ---------- Import settings ----------

    static void ConfigureImports()
    {
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var file = System.IO.Path.GetFileName(path);
            if (file.StartsWith("Intro_") || file.StartsWith("UI_")) continue; // the opening's own sounds keep their settings
            if (!(AssetImporter.GetAtPath(path) is AudioImporter imp)) continue;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            bool longClip = clip != null && clip.length > 6f;
            var s = imp.defaultSampleSettings;
            s.loadType = longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = longClip ? 0.55f : 0.7f;
            s.preloadAudioData = true;
            imp.defaultSampleSettings = s;
            imp.loadInBackground = longClip;
            imp.forceToMono = false;
            imp.SaveAndReimport();
            n++;
        }
        log.Append($"  Import settings on {n} clips\n");
    }

    // ---------- Mixer (internal editor API) ----------

    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Assembly EditorAsm = typeof(Editor).Assembly;
    static Type T(string name) => EditorAsm.GetType("UnityEditor.Audio." + name, true);

    static object Call(object target, string method, params object[] args)
    {
        var type = target as Type ?? target.GetType();
        var m = type.GetMethods(Any).First(x => x.Name == method && x.GetParameters().Length == args.Length
            && (target is Type) == x.IsStatic);
        return m.Invoke(target is Type ? null : target, args);
    }

    static object Get(object o, string prop) => o.GetType().GetProperty(prop, Any).GetValue(o);

    /// <summary>Per snapshot (Calm, Off, Truth, Iceberg, Aftermath).</summary>
    struct Row
    {
        public string group, effect, param;
        public float[] values;
        public Row(string group, string effect, string param, params float[] values) { this.group = group; this.effect = effect; this.param = param; this.values = values; }
    }

    static readonly string[] Groups = { "Music", "Ambience", "SFX", "Stations", "Footsteps", "Voice" };

    static readonly (string group, string effect)[] Effects =
    {
        ("Ambience", "Lowpass Simple"),
        ("Voice", "Highpass Simple"),
        ("Voice", "Lowpass Simple"),
        ("Voice", "Distortion"),
        ("Voice", "Echo"),
        ("Voice", "SFX Reverb"),
    };

    static readonly Row[] Rows =
    {
        //                                                   Calm     Off      Truth   Iceberg  Aftermath
        new Row("Music",     null, "Volume",                    0f,     -1f,     -2f,     0f,    -12f),
        new Row("Ambience",  null, "Volume",                    0f,      0f,      0f,    -2f,    -14f),
        new Row("SFX",       null, "Volume",                    0f,      0f,      0f,     0f,     -6f),
        new Row("Stations",  null, "Volume",                    0f,      0f,      0f,     0f,    -16f),
        new Row("Footsteps", null, "Volume",                    0f,      0f,      0f,    -4f,    -20f),
        new Row("Voice",     null, "Volume",                    0f,      0f,      0f,    -3f,     -8f),
        new Row("Voice",     null, "Pitch",                     1f,   0.97f,   0.93f,  0.93f,   0.93f),
        new Row("Ambience", "Lowpass Simple", "Cutoff freq", 22000f, 15000f,   9000f,  9000f,   1200f),
        new Row("Voice", "Highpass Simple", "Cutoff freq",      90f,   300f,    650f,   650f,    650f),
        new Row("Voice", "Lowpass Simple", "Cutoff freq",    14000f,  6500f,   3800f,  3800f,   3800f),
        new Row("Voice", "Distortion", "Level",                 0f,   0.18f,   0.42f,  0.42f,   0.42f),
        new Row("Voice", "Echo", "Delay",                     120f,   160f,    240f,   240f,    240f),
        new Row("Voice", "Echo", "Decay",                     0.1f,  0.25f,    0.4f,   0.4f,    0.4f),
        new Row("Voice", "Echo", "Drymix",                      1f,     1f,      1f,     1f,      1f),
        new Row("Voice", "Echo", "Wetmix",                      0f,  0.12f,   0.28f,  0.28f,   0.28f),
        new Row("Voice", "SFX Reverb", "Dry Level",             0f,     0f,      0f,     0f,      0f),
        new Row("Voice", "SFX Reverb", "Room",              -10000f, -1800f,  -700f,  -700f,   -700f),
        new Row("Voice", "SFX Reverb", "Decay Time",          0.8f,   1.6f,      3f,     3f,      3f),
        new Row("Voice", "SFX Reverb", "Reverb",            -10000f,  -600f,   200f,   200f,    200f),
    };

    static AudioMixer BuildMixer()
    {
        var tCtrl = T("AudioMixerController");
        var tEffect = T("AudioMixerEffectController");
        var tDefs = T("MixerEffectDefinitions");

        if (AssetDatabase.LoadAssetAtPath<Object>(MixerPath) != null) AssetDatabase.DeleteAsset(MixerPath);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MixerPath));
        var ctrl = Call(tCtrl, "CreateMixerControllerAtPath", MixerPath);
        var master = Get(ctrl, "masterGroup");

        var groups = new Dictionary<string, object>();
        foreach (var name in Groups)
        {
            var g = Call(ctrl, "CreateNewGroup", name, false);
            Call(ctrl, "AddChildToParent", g, master);
            // Only the Audio Mixer window's group views; a fresh asset made headless has none yet.
            try { Call(ctrl, "AddGroupToCurrentView", g); } catch (TargetInvocationException) { }
            groups[name] = g;
        }

        var effects = new Dictionary<(string, string), object>();
        foreach (var (group, name) in Effects)
        {
            var defs = (Array)Call(tDefs, "GetEffectParameters", name);
            log.Append($"  {name}: ").Append(string.Join(", ", defs.Cast<object>().Select(d => d.GetType().GetField("name", Any)?.GetValue(d)))).Append('\n');
            var e = Activator.CreateInstance(tEffect, Any, null, new object[] { name }, null);
            Call(ctrl, "AddNewSubAsset", e, false);
            Call(e, "PreallocateGUIDs");
            var g = groups[group];
            int before = ((Array)Get(g, "effects")).Length; // keep the fader (Attenuation) last
            Call(g, "InsertEffect", e, before - 1);
            effects[(group, name)] = e;
        }

        // Snapshots: the asset starts with one ("Snapshot"); clone it for the rest.
        var snaps = new List<object> { Get(ctrl, "TargetSnapshot") };
        for (int i = 1; i < Snapshots.Length; i++)
        {
            Call(ctrl, "CloneNewSnapshotFromTarget", false);
            var all = (Array)Get(ctrl, "snapshots");
            snaps.Add(all.GetValue(all.Length - 1));
        }
        for (int i = 0; i < snaps.Count; i++) ((Object)snaps[i]).name = Snapshots[i];

        foreach (var row in Rows)
        {
            for (int i = 0; i < snaps.Count; i++)
            {
                var g = groups[row.group];
                float v = row.values[i];
                if (row.effect == null && row.param == "Volume") Call(g, "SetValueForVolume", ctrl, snaps[i], v);
                else if (row.effect == null && row.param == "Pitch") Call(g, "SetValueForPitch", ctrl, snaps[i], v);
                else Call(effects[(row.group, row.effect)], "SetValueForParameter", ctrl, snaps[i], row.param, v);
            }
        }
        log.Append($"  Voice pitch read back (Calm/Truth): {Call(groups["Voice"], "GetValueForPitch", ctrl, snaps[0])} / {Call(groups["Voice"], "GetValueForPitch", ctrl, snaps[2])}\n");

        Call(ctrl, "OnSubAssetChanged");
        EditorUtility.SetDirty((Object)ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(MixerPath);

        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        log.Append($"  Mixer: groups {string.Join(", ", mixer.FindMatchingGroups("").Select(g => g.name))}; snapshots {string.Join(", ", Snapshots.Where(s => mixer.FindSnapshot(s) != null))}\n");
        return mixer;
    }

    // ---------- Bank ----------

    static void BuildBank(AudioMixer mixer)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(BankPath));
        var bank = AssetDatabase.LoadAssetAtPath<AudioBank>(BankPath);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<AudioBank>();
            AssetDatabase.CreateAsset(bank, BankPath);
        }

        bank.mixer = mixer;
        AudioMixerGroup G(string n) => mixer.FindMatchingGroups("Master/" + n).FirstOrDefault(g => g.name == n);
        bank.music = G("Music"); bank.ambience = G("Ambience"); bank.sfx = G("SFX");
        bank.stations = G("Stations"); bank.footsteps = G("Footsteps"); bank.voice = G("Voice");

        bank.seaHull = Clip("Ambience/Amb_SeaHull");
        bank.seaStorm = Clip("Ambience/Amb_SeaStorm");
        bank.wind = Clip("Ambience/Amb_Wind");
        bank.rigging = Clip("Ambience/Amb_Rigging");
        bank.hullCreak = Clip("Ambience/Amb_HullCreak");
        bank.deckAmbience = Clip("Ambience/Amb_Deck");
        bank.engine = Clip("Ambience/Amb_Engine");
        bank.boilerRoar = Clip("Ambience/Amb_BoilerRoar");
        bank.passengers = Clip("Ambience/Amb_Passengers");
        bank.ballroom = Clip("Ambience/Amb_Ballroom");
        bank.pumps = Clip("Ambience/Amb_Pumps");
        bank.gulls = Clips("Ambience", "Amb_Gull_");
        bank.horn = Clip("Ambience/Amb_Horn");

        bank.stepsWood = Clips("SFX/Steps", "Step_Wood_");
        bank.stepsMetal = Clips("SFX/Steps", "Step_Metal_");

        bank.shovelLoop = Clip("SFX/Stations/Sta_ShovelLoop");
        bank.wheelCreak = Clip("SFX/Stations/Sta_WheelCreak");
        bank.morseLoop = Clip("SFX/Stations/Sta_MorseLoop");
        bank.morseBurst = Clip("SFX/Stations/Sta_MorseBurst");
        bank.lever = Clip("SFX/Stations/Sta_Lever");
        bank.doorClose = Clip("SFX/Stations/Sta_DoorClose");
        bank.paper = Clip("SFX/Stations/Sta_Paper");
        bank.clank = Clip("SFX/Stations/Sta_Clank");

        bank.orderBell = Clip("SFX/Signals/Sig_OrderBell");
        bank.tick = Clip("SFX/Signals/Sig_Tick");
        bank.success = Clip("SFX/Signals/Sig_Success");
        bank.refuseHm = Clip("SFX/Signals/Sig_Hm");
        bank.sabotageHiss = Clip("SFX/Signals/Sig_Hiss");
        bank.brokenAlarm = Clip("SFX/Signals/Sig_Alarm");
        bank.helpWhistle = Clip("SFX/Signals/Sig_Whistle");
        bank.musterBell = Clip("SFX/Signals/Sig_Muster");

        bank.speakerOn = Clip("SFX/Captain/Cap_SpeakerOn");
        bank.speakerOff = Clip("SFX/Captain/Cap_SpeakerOff");
        bank.speakerCrackle = Clip("SFX/Captain/Cap_Crackle");
        bank.staticBurst = Clip("SFX/Captain/Cap_Static");

        bank.lookoutBell = Clip("SFX/Finale/Fin_LookoutBell");
        bank.drone = Clip("SFX/Finale/Fin_Drone");
        bank.impactBoom = Clip("SFX/Finale/Fin_ImpactBoom");
        bank.impactWood = Clip("SFX/Finale/Fin_ImpactWood");
        bank.impactIce = Clip("SFX/Finale/Fin_ImpactIce");
        bank.flood = Clip("SFX/Finale/Fin_Flood");

        bank.waltz = Clip("Music/Mus_Waltz");
        bank.sustained = Clip("Music/Mus_Sustained");

        EditorUtility.SetDirty(bank);
        var missing = typeof(AudioBank).GetFields()
            .Where(f => (f.FieldType == typeof(AudioClip) && f.GetValue(bank) == null)
                     || (f.FieldType == typeof(AudioClip[]) && ((AudioClip[])f.GetValue(bank)).Length == 0)
                     || (f.FieldType == typeof(AudioMixerGroup) && f.GetValue(bank) == null))
            .Select(f => f.Name).ToList();
        log.Append(missing.Count == 0 ? "  Bank: every slot filled\n" : "  Bank MISSING: " + string.Join(", ", missing) + "\n");
    }

    static AudioClip Clip(string rel) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/{rel}.wav");

    static AudioClip[] Clips(string folder, string prefix) =>
        AssetDatabase.FindAssets(prefix + " t:AudioClip", new[] { $"{Root}/{folder}" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => System.IO.Path.GetFileName(p).StartsWith(prefix))
            .OrderBy(p => p)
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
            .ToArray();
}
