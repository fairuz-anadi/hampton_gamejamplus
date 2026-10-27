using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A camera move as timed keys: where the camera is, what it looks at, its field of view and a little roll for
/// banking. Owner: Samprity. Positions and look targets run through a cubic Hermite spline whose tangents come from
/// the neighbouring keys and their times (Catmull-Rom in time), so speed stays continuous across keys; the first and
/// last keys have zero velocity, which gives a natural ease in and out. Keys marked hold also stop there.
/// Evaluate is pure maths, so editor checks and screenshots can scrub the move without Play mode.
/// </summary>
public class CinematicPath : MonoBehaviour
{
    [System.Serializable]
    public struct Key
    {
        public float time;
        public Vector3 position;
        public Vector3 lookAt;
        [Range(10f, 90f)] public float fov;
        [Tooltip("Degrees of roll, for banking on turns.")]
        public float roll;
        [Tooltip("Come to rest at this key (zero velocity) instead of flowing through it.")]
        public bool hold;
    }

    public List<Key> keys = new List<Key>();

    public float Duration => keys.Count > 0 ? keys[keys.Count - 1].time : 0f;

    public void Evaluate(float t, out Vector3 position, out Quaternion rotation, out float fov)
    {
        if (keys.Count == 0) { position = transform.position; rotation = transform.rotation; fov = 45f; return; }
        if (keys.Count == 1 || t <= keys[0].time) { Pose(keys[0], out position, out rotation, out fov); return; }
        if (t >= Duration) { Pose(keys[keys.Count - 1], out position, out rotation, out fov); return; }

        int i = 0;
        while (i < keys.Count - 2 && t > keys[i + 1].time) i++;
        var a = keys[i];
        var b = keys[i + 1];
        float dt = Mathf.Max(1e-4f, b.time - a.time);
        float u = (t - a.time) / dt;

        position = Hermite(a.position, b.position, Velocity(i, k => k.position) * dt, Velocity(i + 1, k => k.position) * dt, u);
        var look = Hermite(a.lookAt, b.lookAt, Velocity(i, k => k.lookAt) * dt, Velocity(i + 1, k => k.lookAt) * dt, u);
        float s = u * u * (3f - 2f * u);
        fov = Mathf.Lerp(a.fov, b.fov, s);
        float roll = Mathf.Lerp(a.roll, b.roll, s);
        var forward = look - position;
        rotation = forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, roll) : Quaternion.identity;
    }

    static void Pose(Key k, out Vector3 position, out Quaternion rotation, out float fov)
    {
        position = k.position;
        var forward = k.lookAt - k.position;
        rotation = forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, k.roll) : Quaternion.identity;
        fov = k.fov;
    }

    /// <summary>Velocity at key i in units per second; zero at the ends and at holds.</summary>
    Vector3 Velocity(int i, System.Func<Key, Vector3> value)
    {
        if (i <= 0 || i >= keys.Count - 1 || keys[i].hold) return Vector3.zero;
        var prev = keys[i - 1];
        var next = keys[i + 1];
        return (value(next) - value(prev)) / Mathf.Max(1e-4f, next.time - prev.time);
    }

    static Vector3 Hermite(Vector3 p0, Vector3 p1, Vector3 m0, Vector3 m1, float u)
    {
        float u2 = u * u, u3 = u2 * u;
        return (2f * u3 - 3f * u2 + 1f) * p0 + (u3 - 2f * u2 + u) * m0 + (-2f * u3 + 3f * u2) * p1 + (u3 - u2) * m1;
    }

    void OnDrawGizmos()
    {
        if (keys.Count < 2) return;
        Gizmos.color = new Color(1f, 0.8f, 0.3f);
        Evaluate(0f, out var prev, out _, out _);
        for (float t = 0.25f; t <= Duration; t += 0.25f)
        {
            Evaluate(t, out var p, out _, out _);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
        Gizmos.color = Color.cyan;
        foreach (var k in keys) Gizmos.DrawWireSphere(k.position, 0.3f);
    }
}
