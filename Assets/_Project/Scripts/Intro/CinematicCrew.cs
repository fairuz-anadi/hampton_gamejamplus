using UnityEngine;

/// <summary>
/// A crew member who is only scenery in the opening: works at a spot on a looping animation, or walks back and forth
/// between waypoints. Owner: Samprity. No AI, no physics, no gameplay; it never registers as crew.
/// Animator states come from CrewCinematic.controller (Locomotion with a Speed float, plus work loops).
/// </summary>
public class CinematicCrew : MonoBehaviour
{
    [Tooltip("Animator state to loop while standing (Locomotion = idle). Ignored while walking.")]
    public string action = "Locomotion";
    [Tooltip("World positions to walk between, in order and back. Fewer than two = stand still.")]
    public Vector3[] waypoints = new Vector3[0];
    [Min(0.1f)] public float walkSpeed = 1.3f;
    [Tooltip("Seconds to pause at each end of the walk.")]
    [Min(0f)] public float pauseSeconds = 1.2f;
    [Tooltip("Start this far (seconds) into the walk, so walkers don't move in step.")]
    public float timeOffset;

    Animator animator;
    int target = 1, direction = 1;
    float pause, speed;
    static readonly int SpeedHash = Animator.StringToHash("Speed");

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            if (waypoints.Length < 2 && animator.HasState(0, Animator.StringToHash(action)))
                animator.Play(action, 0, Random.value);
        }
        if (waypoints.Length >= 2)
        {
            transform.position = waypoints[0];
            Face(waypoints[1] - waypoints[0], 1f);
            for (float t = 0f; t < timeOffset; t += 0.1f) Step(0.1f);
        }
    }

    void Update() => Step(Time.deltaTime);

    void Step(float dt)
    {
        if (waypoints.Length < 2) { SetSpeed(0f, dt); return; }
        if (pause > 0f) { pause -= dt; SetSpeed(0f, dt); return; }

        var to = waypoints[target] - transform.position;
        to.y = 0f;
        float d = to.magnitude;
        if (d < 0.05f)
        {
            if (target + direction < 0 || target + direction >= waypoints.Length) direction = -direction;
            target += direction;
            pause = pauseSeconds;
            return;
        }
        // Ease into the stop over the last half metre.
        float want = walkSpeed * Mathf.Clamp01(d / 0.5f + 0.25f);
        speed = Mathf.MoveTowards(speed, want, dt * 3f);
        transform.position += to / d * Mathf.Min(d, speed * dt);
        Face(to, dt * 6f);
        SetSpeed(speed, dt);
    }

    void Face(Vector3 dir, float t)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Mathf.Clamp01(t));
    }

    void SetSpeed(float s, float dt)
    {
        if (waypoints.Length < 2) return;
        if (s <= 0.01f) speed = Mathf.MoveTowards(speed, 0f, dt * 4f);
        if (animator != null) animator.SetFloat(SpeedHash, s > 0.01f ? s : speed, 0.1f, dt);
    }
}
