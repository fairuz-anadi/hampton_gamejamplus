using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Captain's recorded voice: one clip per text box he can say, keyed by the box's exact text. Owner: Samprity.
/// Recorded with ElevenLabs by Tools/make_captain_voice.py (offline: the API key never goes in the game) and
/// collected by CaptainVoiceBuilder (Port > Samprity > Build Captain Voice Bank) into Resources/CaptainVoiceBank.
/// A box with a name in it ("Good work, {seat}") has a clip per crew name, plus one that says "sailor" for any name a
/// player typed.
/// </summary>
public class CaptainVoiceBank : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("The text box exactly as it shows; a template with {seat} for a typed name.")]
        public string text;
        public AudioClip clip;
        [Tooltip("text is a template: {seat} matches any name.")]
        public bool template;
    }

    public string voiceName;
    public List<Entry> entries = new List<Entry>();

    static CaptainVoiceBank cached;
    public static CaptainVoiceBank Load() => cached != null ? cached : (cached = Resources.Load<CaptainVoiceBank>("CaptainVoiceBank"));
}
