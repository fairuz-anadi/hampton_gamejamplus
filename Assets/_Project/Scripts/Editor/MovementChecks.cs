using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Plays the 8 movement test cases in the prototype scene with scripted input at a fixed 60 fps and checks the numbers.
/// It is not a replacement for playing it, just a guard against curving, spinning, snapping and clipping. Owner: Samprity.
///   In the Editor:  Port > Samprity > Run Movement Checks
///   Headless:       Unity.exe -batchmode -projectPath . -executeMethod MovementChecks.RunBatch
/// </summary>
public static class MovementChecks
{
    const float Dt = 1f / 60f;
    static readonly Vector2 W = new Vector2(0, 1), S = new Vector2(0, -1), A = new Vector2(-1, 0), D = new Vector2(1, 0), None = Vector2.zero;

    internal struct Frame
    {
        public Vector3 pos;
        public float speed, facing, camYaw, arm;
        public Vector3 camPos;
        public bool camInWall, playerHidden;
    }

    [MenuItem("Port/Samprity/Run Movement Checks")]
    public static void Run()
    {
        bool ok = RunAll(out string report);
        if (ok) Debug.Log(report); else Debug.LogWarning(report);
    }

    public static void RunBatch()
    {
        bool ok = RunAll(out string report);
        Debug.Log(report);
        System.IO.File.WriteAllText("Logs/MovementChecks.txt", report);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool RunAll(out string report)
    {
        var sb = new StringBuilder("[MovementChecks]\n");
        var scene = EditorSceneManager.OpenScene(MovementPrototypeBuilder.ScenePath, OpenSceneMode.Single);
        ThirdPersonMover mover = null;
        AutoFollowCamera cam = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (mover == null) mover = root.GetComponentInChildren<ThirdPersonMover>();
            if (cam == null) cam = root.GetComponentInChildren<AutoFollowCamera>();
        }
        if (mover == null || cam == null)
        {
            report = sb.Append("FAIL: scene has no ThirdPersonMover or AutoFollowCamera. Run Port > Samprity > Build Movement Prototype first.").ToString();
            return false;
        }

        mover.readOwnInput = false;
        float maxYawSpeed = cam.recenterSpeed * 1.05f;
        bool all = true;

        // 1. Hold W: straight line, camera stays put.
        {
            var f = Sim(mover, cam, new Vector3(0, 0, -12), 0, (5f, W));
            float drift = Max(f, x => Mathf.Abs(x.pos.x));
            float camDrift = Max(f, x => Mathf.Abs(Mathf.DeltaAngle(x.camYaw, 0)));
            all &= Report(sb, "1 Hold W", drift < 0.05f && camDrift < 0.5f,
                $"sideways drift {drift:0.000} m, camera turned {camDrift:0.0} deg, travelled {f[f.Count - 1].pos.z + 12:0.0} m");
        }

        // 2. Left and right: camera follows smoothly and ends behind.
        {
            var f = Sim(mover, cam, new Vector3(4, 0, 10), 0, (1.2f, A), (1.2f, D), (1.2f, A), (1.2f, D), (2.5f, None));
            float ys = MaxYawSpeed(f);
            float behind = Mathf.Abs(Mathf.DeltaAngle(f[f.Count - 1].camYaw, f[f.Count - 1].facing));
            all &= Report(sb, "2 Left/right turns", ys <= maxYawSpeed && behind < 3f,
                $"fastest camera swing {ys:0} deg/s (limit {cam.recenterSpeed:0}), ends {behind:0.0} deg off behind");
        }

        // 3. Reverse: no snapping, ends behind, walks straight after the turn.
        {
            var f = Sim(mover, cam, new Vector3(12, 0, -13), 0, (3f, W), (3f, S));
            float ys = MaxYawSpeed(f);
            float behind = Mathf.Abs(Mathf.DeltaAngle(f[f.Count - 1].camYaw, f[f.Count - 1].facing));
            float wobble = 0f;
            for (int i = f.Count - 90; i < f.Count; i++) wobble = Mathf.Max(wobble, Mathf.Abs(f[i].pos.x - f[f.Count - 1].pos.x));
            all &= Report(sb, "3 Reverse direction", ys <= maxYawSpeed && behind < 5f && wobble < 0.1f,
                $"fastest camera swing {ys:0} deg/s, ends {behind:0.0} deg off behind, sideways wobble {wobble:0.000} m");
        }

        // 4. Diagonal: no faster than straight.
        {
            var f = Sim(mover, cam, new Vector3(12, 0, -13), 0, (2.5f, new Vector2(-1, 1)));
            float top = Max(f, x => x.speed);
            float traveled = 0f;
            for (int i = f.Count - 60; i < f.Count; i++) traveled += Vector3.Distance(Flat(f[i].pos), Flat(f[i - 1].pos));
            all &= Report(sb, "4 Diagonal W+A", top <= mover.moveSpeed * 1.01f && Mathf.Abs(traveled - mover.moveSpeed) < 0.1f,
                $"top speed {top:0.00} m/s, last second covered {traveled:0.00} m (target {mover.moveSpeed:0.00})");
        }

        // 5. Stop: camera settles, no drift.
        {
            var f = Sim(mover, cam, new Vector3(12, 0, -13), 0, (2f, W), (1f, A), (3f, None));
            int last = f.Count - 1, secondAgo = f.Count - 61;
            float yawMove = Mathf.Abs(Mathf.DeltaAngle(f[secondAgo].camYaw, f[last].camYaw));
            float posMove = Vector3.Distance(f[secondAgo].camPos, f[last].camPos);
            all &= Report(sb, "5 Stop", yawMove < 0.2f && posMove < 0.01f && f[last].speed == 0f,
                $"camera moved {posMove:0.000} m and {yawMove:0.00} deg in the final second");
        }

        // 6. Narrow corridor: the camera never ends up inside a wall.
        {
            var f = Sim(mover, cam, new Vector3(8, 0, -6), 0, (2.2f, W), (0.6f, A), (1.2f, D), (1.5f, W), (1.5f, S));
            int inWall = Count(f, x => x.camInWall);
            float minArm = Max(f, x => -x.arm) * -1f;
            float jump = 0f;
            int jumpAt = 0;
            for (int i = 1; i < f.Count; i++)
            {
                float j = Mathf.Abs(f[i].arm - f[i - 1].arm);
                if (j > jump) { jump = j; jumpAt = i; }
            }
            all &= Report(sb, "6 Narrow corridor", inWall == 0 && jump < 1f,
                $"frames with camera in a wall: {inWall}, closest arm {minArm:0.00} m (full {cam.FullArm:0.00}), " +
                $"biggest one-frame pull-in {jump:0.00} m at {jumpAt * Dt:0.00}s (player {f[jumpAt].pos.x:0.0},{f[jumpAt].pos.z:0.0} facing {f[jumpAt].facing:0}, camera yaw {f[jumpAt].camYaw:0})");
        }

        // 7. Around a big obstacle: the player stays visible.
        {
            var f = Sim(mover, cam, new Vector3(-7, 0, -6.5f), 0, (1.2f, A), (2.5f, W), (1.4f, D), (1.0f, S), (1f, None));
            int hidden = Count(f, x => x.playerHidden);
            int run = LongestRun(f, x => x.playerHidden);
            all &= Report(sb, "7 Around the boiler", run <= 3 && Count(f, x => x.camInWall) == 0,
                $"frames with the player hidden: {hidden} (longest run {run}), frames in a wall: {Count(f, x => x.camInWall)}");
        }

        // 8. Rapid direction changes: no spinning or jitter.
        {
            var rng = new System.Random(7);
            Vector2[] dirs = { W, S, A, D, new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1), None };
            var steps = new List<(float, Vector2)>();
            for (int i = 0; i < 40; i++) steps.Add((0.15f, dirs[rng.Next(dirs.Length)]));
            steps.Add((2f, None));
            var f = Sim(mover, cam, new Vector3(1, 0, 0), 0, steps.ToArray());
            float ys = MaxYawSpeed(f), total = 0f;
            int flips = 0;
            float prevStep = 0f;
            for (int i = 1; i < f.Count; i++)
            {
                float step = Mathf.DeltaAngle(f[i - 1].camYaw, f[i].camYaw);
                total += Mathf.Abs(step);
                if (Mathf.Abs(step) > 0.01f && Mathf.Abs(prevStep) > 0.01f && Mathf.Sign(step) != Mathf.Sign(prevStep)) flips++;
                if (Mathf.Abs(step) > 0.01f) prevStep = step;
            }
            all &= Report(sb, "8 Rapid changes", ys <= maxYawSpeed && total < 720f && flips <= 40,
                $"fastest swing {ys:0} deg/s, total camera turning {total:0} deg in 8 s, direction reversals {flips}");
        }

        sb.Append(all ? "ALL PASSED" : "SOME CHECKS FAILED");
        report = sb.ToString();
        return all;
    }

    /// <summary>Renders a few moments from the follow camera to Logs/MovementShot_*.png, to eyeball framing.</summary>
    public static void CaptureBatch()
    {
        var scene = EditorSceneManager.OpenScene(MovementPrototypeBuilder.ScenePath, OpenSceneMode.Single);
        ThirdPersonMover mover = null;
        AutoFollowCamera follow = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (mover == null) mover = root.GetComponentInChildren<ThirdPersonMover>();
            if (follow == null) follow = root.GetComponentInChildren<AutoFollowCamera>();
        }
        mover.readOwnInput = false;
        var cam = follow.GetComponent<Camera>();
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.aspect = 1280f / 720f;

        var animator = mover.GetComponentInChildren<Animator>();
        AnimationClip run = null;
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (var c in animator.runtimeAnimatorController.animationClips)
                if (c.name == "Running_A") run = c;

        void Shot(string name, Vector3 start, float yaw, params (float secs, Vector2 input)[] steps)
        {
            Sim(mover, follow, start, yaw, steps);
            if (run != null) run.SampleAnimation(animator.gameObject, 0.3f);
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes($"Logs/MovementShot_{name}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        // The very first headless render comes out with garbage textures, so throw one away.
        cam.Render();
        Shot("1_walking_forward", new Vector3(0, 0, -12), 0, (2f, W));
        Shot("2_mid_turn_left", new Vector3(4, 0, 10), 0, (0.8f, A));
        Shot("3_settled_after_turn", new Vector3(4, 0, 10), 0, (2.5f, A));
        Shot("4_in_corridor", new Vector3(8, 0, -6), 0, (2.6f, W));
        Shot("5_facing_corridor_wall", new Vector3(8, 0, -6), 0, (2.2f, W), (0.6f, A), (1.5f, None));
        Shot("6_by_the_boiler", new Vector3(-7, 0, -6.5f), 0, (1.2f, A), (1.6f, W));

        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        EditorApplication.Exit(0);
    }

    internal static List<Frame> Sim(ThirdPersonMover m, AutoFollowCamera c, Vector3 start, float yaw, params (float secs, Vector2 input)[] steps)
    {
        m.Teleport(start, yaw);
        Physics.SyncTransforms();
        c.SnapToTarget();
        var frames = new List<Frame>(600) { Capture(m, c) };
        foreach (var (secs, input) in steps)
        {
            int n = Mathf.RoundToInt(secs / Dt);
            for (int i = 0; i < n; i++)
            {
                m.SetMoveInput(input);
                m.Tick(Dt);
                Physics.SyncTransforms();
                c.Tick(Dt);
                frames.Add(Capture(m, c));
            }
        }
        return frames;
    }

    static Frame Capture(ThirdPersonMover m, AutoFollowCamera c)
    {
        var root = m.transform;
        Vector3 camPos = c.transform.position;
        Vector3 focus = root.position + Vector3.up * c.focusHeight;

        bool inWall = false;
        foreach (var col in Physics.OverlapSphere(camPos, 0.1f, ~0, QueryTriggerInteraction.Ignore))
            if (!col.transform.IsChildOf(root)) { inWall = true; break; }

        bool hidden = false;
        Vector3 toFocus = focus - camPos;
        foreach (var h in Physics.RaycastAll(camPos, toFocus.normalized, toFocus.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (!h.collider.transform.IsChildOf(root)) { hidden = true; break; }

        return new Frame
        {
            pos = root.position, speed = m.Speed, facing = m.FacingYaw,
            camYaw = c.Yaw, camPos = camPos, arm = c.CurrentArm,
            camInWall = inWall, playerHidden = hidden,
        };
    }

    static float MaxYawSpeed(List<Frame> f)
    {
        float max = 0f;
        for (int i = 1; i < f.Count; i++) max = Mathf.Max(max, Mathf.Abs(Mathf.DeltaAngle(f[i - 1].camYaw, f[i].camYaw)) / Dt);
        return max;
    }

    static float Max(List<Frame> f, System.Func<Frame, float> get)
    {
        float max = float.MinValue;
        foreach (var x in f) max = Mathf.Max(max, get(x));
        return max;
    }

    static int Count(List<Frame> f, System.Func<Frame, bool> test)
    {
        int n = 0;
        foreach (var x in f) if (test(x)) n++;
        return n;
    }

    static int LongestRun(List<Frame> f, System.Func<Frame, bool> test)
    {
        int best = 0, run = 0;
        foreach (var x in f) { run = test(x) ? run + 1 : 0; best = Mathf.Max(best, run); }
        return best;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static bool Report(StringBuilder sb, string name, bool pass, string detail)
    {
        sb.Append(pass ? "  PASS  " : "  FAIL  ").Append(name).Append(": ").Append(detail).Append('\n');
        return pass;
    }
}
