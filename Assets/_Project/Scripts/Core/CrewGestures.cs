using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// A few one-off body gestures for the crew (tap on the shoulder, talking), from the Quaternius animation library,
/// kept in Resources/CrewGestures so scripts can play them on any crew model without touching the crew's animator
/// controller. Built by Port > Build Crew Gestures. Owner: Anadi.
/// </summary>
[CreateAssetMenu(menuName = "Port/Crew Gestures")]
public class CrewGestures : ScriptableObject
{
    public AnimationClip tap;     // Interact: a reach-and-touch
    public AnimationClip talk;    // Idle_Talking_Loop
    public AnimationClip startle; // Hit_Chest: a small flinch, for the one who gets tapped

    static CrewGestures cached;
    public static CrewGestures Load() => cached != null ? cached : (cached = Resources.Load<CrewGestures>("CrewGestures"));

    /// <summary>
    /// Plays a clip on top of an animator, faded in and out, then hands the body back to its controller. Dispose (or
    /// let it end) to stop.
    /// </summary>
    public class Player
    {
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        float started, length, fade;
        bool loop;

        public bool Alive => graph.IsValid();

        public Player(Animator animator, AnimationClip clip, bool loop, float fade = 0.2f)
        {
            if (animator == null || clip == null || animator.runtimeAnimatorController == null) return;
            graph = PlayableGraph.Create("CrewGesture");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Gesture", animator);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            var ctrl = AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController);
            var c = AnimationClipPlayable.Create(graph, clip);
            c.SetApplyFootIK(false);
            graph.Connect(ctrl, 0, mixer, 0);
            graph.Connect(c, 0, mixer, 1);
            output.SetSourcePlayable(mixer);
            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 0f);
            graph.Play();
            started = Time.time;
            length = clip.length;
            this.loop = loop;
            this.fade = Mathf.Max(0.01f, fade);
        }

        /// <summary>Call every frame. Returns false once a one-shot gesture has finished (and cleans up).</summary>
        public bool Tick()
        {
            if (!graph.IsValid()) return false;
            float t = Time.time - started;
            float w = Mathf.Clamp01(t / fade);
            if (!loop) w = Mathf.Min(w, Mathf.Clamp01((length - t) / fade));
            mixer.SetInputWeight(0, 1f - w);
            mixer.SetInputWeight(1, w);
            if (!loop && t >= length) { Dispose(); return false; }
            return true;
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
