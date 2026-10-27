using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Every sound the voyage uses, plus the mixer and its groups. Owner: Samprity.
/// Filled by Port > Samprity > Build Audio (AudioBuilder) from the files in Audio/; AudioManager loads it from Resources.
/// </summary>
public class AudioBank : ScriptableObject
{
    public const string ResourcePath = "AudioBank";

    [Header("Mixer")]
    public AudioMixer mixer;
    public AudioMixerGroup music, ambience, sfx, stations, footsteps, voice;

    [Header("Ambience loops")]
    public AudioClip seaHull;
    public AudioClip seaStorm;
    public AudioClip wind;
    public AudioClip rigging;
    public AudioClip hullCreak;
    public AudioClip deckAmbience;
    public AudioClip engine;
    public AudioClip boilerRoar;
    public AudioClip passengers;
    public AudioClip ballroom;
    public AudioClip pumps;

    [Header("Ambience one-shots")]
    public AudioClip[] gulls = new AudioClip[0];
    public AudioClip horn;

    [Header("Footsteps (single steps)")]
    public AudioClip[] stepsWood = new AudioClip[0];
    public AudioClip[] stepsMetal = new AudioClip[0];

    [Header("Stations")]
    public AudioClip shovelLoop;
    public AudioClip wheelCreak;
    public AudioClip morseLoop;
    public AudioClip morseBurst;
    public AudioClip lever;
    public AudioClip doorClose;
    public AudioClip paper;
    public AudioClip clank;

    [Header("Signals")]
    public AudioClip orderBell;
    public AudioClip tick;
    public AudioClip success;
    public AudioClip refuseHm;
    public AudioClip sabotageHiss;
    public AudioClip brokenAlarm;
    public AudioClip helpWhistle;
    public AudioClip musterBell;

    [Header("Captain's speaker")]
    public AudioClip speakerOn;
    public AudioClip speakerOff;
    public AudioClip speakerCrackle;
    public AudioClip staticBurst;

    [Header("Finale")]
    public AudioClip lookoutBell;
    public AudioClip drone;
    public AudioClip impactBoom;
    public AudioClip impactWood;
    public AudioClip impactIce;
    public AudioClip flood;

    [Header("Music")]
    public AudioClip waltz;
    public AudioClip sustained;

    public static AudioBank Load() => Resources.Load<AudioBank>(ResourcePath);
}
