using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Walks a bot around the deck with a NavMeshAgent. Owner: Riham.
/// If it makes no progress for 3 s it warps a little toward the target.
/// With no NavMesh in the scene it slides straight at the target, so sandboxes still work.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class BotMover : MonoBehaviour
{
    [SerializeField] float speed = 3.5f;
    [SerializeField] float stuckSeconds = 3f;
    [SerializeField] float warpStep = 1.5f;

    NavMeshAgent agent;
    Vector3 target;
    float stopWithin;
    bool hasTarget;
    bool useNavMesh;

    Vector3 lastSample;
    float sampleAt;
    float stuckFor;

    public bool HasTarget => hasTarget;
    public bool HasArrived => !hasTarget || FlatDistance(transform.position, target) <= stopWithin;
    public Vector3 Target => target;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = speed;
        agent.angularSpeed = 720f;
        agent.acceleration = 20f;
        agent.autoBraking = true;
    }

    void Start() => SnapToNavMesh();

    /// <summary>Puts the agent on the NavMesh, or falls back to straight-line movement if there isn't one.</summary>
    public void SnapToNavMesh()
    {
        if (NavMesh.SamplePosition(transform.position, out var hit, 5f, NavMesh.AllAreas))
        {
            agent.enabled = true;
            agent.Warp(hit.position);
            useNavMesh = true;
        }
        else
        {
            agent.enabled = false;
            useNavMesh = false;
        }
    }

    public void GoTo(Vector3 pos, float within = 0.4f)
    {
        within = Mathf.Max(0.1f, within);
        if (hasTarget && FlatDistance(pos, target) < 0.05f && Mathf.Approximately(within, stopWithin)) return;

        target = pos;
        stopWithin = within;
        hasTarget = true;
        stuckFor = 0f;
        lastSample = transform.position;
        sampleAt = Time.time + 0.5f;

        if (useNavMesh && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.stoppingDistance = within * 0.8f;
            agent.SetDestination(pos);
        }
    }

    public void Stop()
    {
        hasTarget = false;
        if (useNavMesh && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    /// <summary>Teleports, e.g. when a new run starts.</summary>
    public void Teleport(Vector3 pos)
    {
        Stop();
        transform.position = pos;
        SnapToNavMesh();
    }

    void Update()
    {
        if (!hasTarget) return;

        if (HasArrived)
        {
            Stop();
            return;
        }

        if (!useNavMesh)
        {
            var flatTarget = new Vector3(target.x, transform.position.y, target.z);
            transform.position = Vector3.MoveTowards(transform.position, flatTarget, speed * Time.deltaTime);
            Face(flatTarget - transform.position);
            return;
        }

        if (!agent.isOnNavMesh)
        {
            SnapToNavMesh();
            return;
        }

        // Stuck check: sample every half second.
        if (Time.time >= sampleAt)
        {
            if (FlatDistance(transform.position, lastSample) < 0.15f) stuckFor += 0.5f;
            else stuckFor = 0f;
            lastSample = transform.position;
            sampleAt = Time.time + 0.5f;

            if (stuckFor >= stuckSeconds) Unstick();
        }
    }

    void Unstick()
    {
        stuckFor = 0f;
        var dir = target - transform.position;
        dir.y = 0f;
        var nudge = transform.position + Vector3.ClampMagnitude(dir, warpStep);
        if (NavMesh.SamplePosition(nudge, out var hit, 2f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            agent.SetDestination(target);
        }
    }

    void Face(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 720f * Time.deltaTime);
    }

    /// <summary>The closest walkable point to pos, or pos itself if there's no NavMesh nearby.</summary>
    public static Vector3 OnNavMesh(Vector3 pos, float radius = 2f) =>
        NavMesh.SamplePosition(pos, out var hit, radius, NavMesh.AllAreas) ? hit.position : pos;

    public static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
