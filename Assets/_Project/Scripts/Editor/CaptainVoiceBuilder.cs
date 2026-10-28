using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Captain's recorded voice, both ends of it. Owner: Samprity.
///   Port > Samprity > Export Captain Lines      writes Tools/captain_voice/lines.json: every text box he can say
///                                               (CaptainScript, the twelve orders, his small talk face to face and
///                                               his "Grey Cove" slip), its mood 0-4, and what the voice should say.
///   Tools/make_captain_voice.py                 records them with ElevenLabs into Audio/Voice/Captain (offline).
///   Port > Samprity > Build Captain Voice Bank  gathers the recordings into Resources/CaptainVoiceBank for
///                                               CaptainNarrator.
///   Headless: -executeMethod CaptainVoiceBuilder.ExportBatch / BuildBankBatch -quit
/// </summary>
public static class CaptainVoiceBuilder
{
    public const string Dir = "Tools/captain_voice";
    const string LinesJson = Dir + "/lines.json";
    const string ClipFolder = "Assets/_Project/Audio/Voice/Captain";
    const string BankPath = "Assets/_Project/Audio/Resources/CaptainVoiceBank.asset";

    [System.Serializable]
    public class Line
    {
        public string id;      // file name, stable across exports
        public string key;     // the text box as it shows (a template with {seat} when variant is "*")
        public string speak;   // what the voice says
        public int mood;       // 0 Part 1 .. 3 drunk, 4 the cold truth
        public string variant; // "" plain, a crew name, or "*" for any name a player typed
        public string source;  // where it comes from (a CaptainScript field, "Order", "Talk", "Slip"): what to record first
    }

    [System.Serializable]
    class LineList { public List<Line> lines = new List<Line>(); }

    // Not said in the dialog box: the telegram text, the removed muster vote, the results screen's last word.
    static readonly string[] Skip = { "IceWarningText", "VoteCalled", "VoteNobody", "VoteWrong", "VoteRight", "EpilogueShipSaved", "EpilogueManySaved", "EpilogueShipLost" };

    // ---------- Export ----------

    [MenuItem("Port/Samprity/Export Captain Lines")]
    public static void Export()
    {
        var pages = new List<(string text, int mood, string source)>();
        void Add(string line, int mood, string source)
        {
            if (string.IsNullOrEmpty(line)) return;
            foreach (var page in line.Split('\n'))
            {
                string t = FinalText(page).Trim();
                if (t.Length > 0) pages.Add((t, Mathf.Clamp(mood, 0, 4), source));
            }
        }

        // Everything in CaptainScript, read by reflection so new lines are picked up.
        foreach (var f in typeof(CaptainScript).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (System.Array.IndexOf(Skip, f.Name) >= 0) continue;
            var v = f.GetValue(null);
            if (v is string s) Add(s, MoodFor(f.Name, -1), f.Name);
            else if (v is string[] arr) for (int i = 0; i < arr.Length; i++) Add(arr[i], MoodFor(f.Name, f.Name == "MoodStart" ? i : -1), f.Name);
            else if (v is string[][] tiers) for (int m = 0; m < tiers.Length; m++) foreach (var l in tiers[m]) Add(l, m, f.Name);
        }
        // The twelve orders, by part: friendly, then drunk, then the last cold one.
        foreach (var o in Resources.LoadAll<OrderData>("Orders"))
            Add(o.captainLine, o.part <= 1 ? 0 : o.part == 2 ? 2 : 4, "Order");
        foreach (var l in CaptainTalk.PartOneTalk) Add(l, 0, "Talk");
        Add("Midnight, and we'll make Grey Cove right on time.", 2, "Slip"); // the slip (ShipClues)

        var names = Enumerable.Range(1, 4).Select(CrewRegistry.DefaultSeatName).ToArray();
        var list = new LineList();
        var seen = new HashSet<string>();
        foreach (var (text, mood, source) in pages)
        {
            if (!text.Contains("{seat}"))
            {
                Emit(list, seen, text, Speakable(text), mood, "", source);
                continue;
            }
            foreach (var n in names) Emit(list, seen, text.Replace("{seat}", n), Speakable(text.Replace("{seat}", n)), mood, n, source);
            Emit(list, seen, text, Speakable(Sailor(text)), mood, "*", source);
        }

        System.IO.Directory.CreateDirectory(Dir);
        System.IO.File.WriteAllText(LinesJson, JsonUtility.ToJson(list, true));
        int plain = list.lines.Where(l => l.variant == "").Sum(l => l.speak.Length);
        int generic = list.lines.Where(l => l.variant == "*").Sum(l => l.speak.Length);
        int named = list.lines.Where(l => l.variant != "" && l.variant != "*").Sum(l => l.speak.Length);
        Debug.Log($"[CaptainVoice] Exported {list.lines.Count} recordings to {LinesJson}: {plain} characters without names, " +
                  $"{generic} for the 'sailor' versions, {named} more for the four crew names (total {plain + generic + named}).");
    }

    public static void ExportBatch() => Export();

    static void Emit(LineList list, HashSet<string> seen, string key, string speak, int mood, string variant, string source)
    {
        if (!seen.Add(key + "|" + variant)) return;
        list.lines.Add(new Line { id = Id(key + "|" + variant), key = key, speak = speak, mood = mood, variant = variant, source = source });
    }

    /// <summary>How he should sound for a line: by its mood tier where it has one, else by what it is.</summary>
    static int MoodFor(string field, int index)
    {
        if (index >= 0) return index;
        if (field.StartsWith("Tutorial") || field == "Part1Start") return 0;
        if (field == "Part2Start" || field == "ReportDone") return 1;
        if (field.StartsWith("DoorFour") || field == "PrivateLines") return 2;
        if (field == "SuspiciousLines") return 3;
        // Serious at every mood: caught fakes, the reveal, the final speech, the bell.
        if (field == "FakeNoticed" || field == "RevealLine" || field.StartsWith("Monologue") || field == "IcebergCall") return 4;
        return 1;
    }

    /// <summary>{wrong|right} reads as right in the end, as CaptainDirector types it.</summary>
    static string FinalText(string text) => Regex.Replace(text, @"\{([^{}|]*)\|([^{}]*)\}", "$2");

    /// <summary>What the voice says: stage directions become sounds or go, and nothing is left in braces.</summary>
    static string Speakable(string text)
    {
        text = Regex.Replace(text, @"\*hic\*", "hic!", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\*[^*]+\*", "");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    /// <summary>For a name a player typed: he says "sailor" instead.</summary>
    static string Sailor(string text) => (text.StartsWith("{seat}") ? "Sailor" + text.Substring(6) : text).Replace("{seat}", "sailor");

    /// <summary>A short stable file name from the text (FNV-1a).</summary>
    static string Id(string s)
    {
        uint h = 2166136261;
        foreach (char c in s) { h ^= c; h *= 16777619; }
        return "cap_" + h.ToString("x8");
    }

    // ---------- Bank ----------

    [MenuItem("Port/Samprity/Build Captain Voice Bank")]
    public static void BuildBank()
    {
        if (!System.IO.File.Exists(LinesJson)) { Debug.LogWarning("[CaptainVoice] Export the lines first"); return; }
        var list = JsonUtility.FromJson<LineList>(System.IO.File.ReadAllText(LinesJson));
        AssetDatabase.Refresh();
        var bank = AssetDatabase.LoadAssetAtPath<CaptainVoiceBank>(BankPath);
        if (bank == null)
        {
            MovementPrototypeBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(BankPath).Replace('\\', '/'));
            bank = ScriptableObject.CreateInstance<CaptainVoiceBank>();
            AssetDatabase.CreateAsset(bank, BankPath);
        }
        bank.entries.Clear();
        int missing = 0;
        foreach (var l in list.lines)
        {
            string path = $"{ClipFolder}/{l.id}.mp3";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) { missing++; continue; }
            // Mono and compressed in memory: a voice doesn't need stereo, and there are a lot of them.
            var imp = (AudioImporter)AssetImporter.GetAtPath(path);
            if (imp != null && !imp.forceToMono) { imp.forceToMono = true; imp.SaveAndReimport(); }
            bank.entries.Add(new CaptainVoiceBank.Entry { text = l.key, clip = clip, template = l.variant == "*" });
        }
        string voiceFile = Dir + "/voice.txt";
        bank.voiceName = System.IO.File.Exists(voiceFile) ? System.IO.File.ReadAllText(voiceFile).Trim() : "";
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CaptainVoice] Bank: {bank.entries.Count} recordings ({missing} not recorded yet), voice '{bank.voiceName}'");
    }

    public static void BuildBankBatch() => BuildBank();
}
