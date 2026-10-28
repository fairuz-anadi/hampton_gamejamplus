using UnityEngine;

/// <summary>
/// The Captain's body language, on top of his animator. Owner: Samprity. Added to his model by CaptainOnDeck.
///   Drunk (0-1): the upper body rolls and droops, the head lolls, the walk weaves side to side, and he can stumble
///   (a lurch and a flinch) or take a swig (IK brings the bottle hand to his mouth and tips it up, head back).
///   Stiff: the Part 3 man who was never drunk. Dead straight, chin up, no sway at all.
/// Everything is relative to the character's own axes, so it never depends on how the bones are oriented.
/// </summary>
[RequireComponent(typeof(Animator))]
public class CaptainBody : MonoBehaviour
{
    [Tooltip("Degrees the upper body rolls at full drunkenness.")]
    public float swayRoll = 9f;
    [Tooltip("How far (m) the walk weaves side to side at full drunkenness.")]
    public float weave = 0.12f;

    /// <summary>0 sober .. 1 as drunk as the act gets. Set by CaptainOnDeck every frame.</summary>
    public float Drunk { get; set; }
    /// <summary>The Part 3 posture: upright and still.</summary>
    public bool Stiff { get; set; }
    /// <summary>Walking speed in m/s, for the animator and the weave.</summary>
    public float Speed { get; set; }
    public bool Talking { get; set; }
    public bool IsSipping => sipLeft > 0f;
    public bool IsStumbling => stumbleLeft > 0f;

    static readonly int SpeedHash = Animator.StringToHash("Speed");

    Animator animator;
    Transform hips, spine, chest, neck, head, hand, finger, bottle;
    Vector3 homePos;
    string playing;
    float seed;
    float sipLeft, sipLength;
    float stumbleLeft;
    Vector3 lurch, lurchVel;
    float weavePhase;

    void Awake()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // the swig's IK has to run off screen too
        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.UpperChest);
        neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        head = animator.GetBoneTransform(HumanBodyBones.Head);
        hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        finger = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        if (hand != null)
            foreach (Transform c in hand) if (c.name == "Bottle") bottle = c;
        homePos = transform.localPosition;
        seed = Random.value * 100f;
    }

    // ---------- Actions ----------

    /// <summary>A swig from the bottle, lasting `seconds`.</summary>
    public void Sip(float seconds = 1.9f)
    {
        if (IsSipping || bottle == null || !bottle.gameObject.activeInHierarchy) return;
        sipLength = sipLeft = seconds;
    }

    /// <summary>A lurch to one side and a flinch, like missing a step.</summary>
    public void Stumble(Vector3 sideways)
    {
        if (IsStumbling) return;
        stumbleLeft = 0.9f;
        lurchVel += sideways.normalized * 0.9f;
        Play("Stumble", 0.12f);
    }

    // ---------- Animation ----------

    void Update()
    {
        float dt = Time.deltaTime;
        animator.SetFloat(SpeedHash, Speed < 0.08f ? 0f : Speed, 0.1f, dt);
        sipLeft = Mathf.Max(0f, sipLeft - dt);
        if (stumbleLeft > 0f && (stumbleLeft -= dt) <= 0f) playing = null;

        // The lurch is a little spring: kicked by a stumble, it swings out and settles back under him.
        lurchVel += (-lurch * 28f - lurchVel * 7f) * dt;
        lurch += lurchVel * dt;

        if (!IsStumbling)
        {
            string want = Talking && Speed < 0.2f ? "Talk" : Drunk > 0.15f ? "Locomotion" : "Formal";
            Play(want, 0.25f);
        }
    }

    void Play(string state, float fade)
    {
        if (state == playing) return;
        playing = state;
        int hash = Animator.StringToHash(state);
        if (animator.HasState(0, hash)) animator.CrossFadeInFixedTime(hash, fade);
    }

    float SipWeight
    {
        get
        {
            if (sipLeft <= 0f) return 0f;
            float t = sipLength - sipLeft;
            return Mathf.SmoothStep(0f, 1f, Mathf.Min(t / 0.45f, sipLeft / 0.45f));
        }
    }

    void OnAnimatorIK(int layer)
    {
        float w = SipWeight;
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
        animator.SetLookAtWeight(w * 0.6f, 0.2f, 0.9f);
        if (w <= 0f || head == null) return;
        var root = transform.parent != null ? transform.parent : transform;
        // The fist just in front of his mouth, a touch to the right; he tips his head back to drink.
        Vector3 mouth = head.position + Vector3.up * 0.04f + root.forward * 0.14f + root.right * 0.03f;
        animator.SetIKPosition(AvatarIKGoal.RightHand, mouth);
        animator.SetLookAtPosition(head.position + root.forward + Vector3.up * 0.9f);
    }

    void LateUpdate()
    {
        var root = transform.parent;
        if (root == null) return;
        float t = Time.time + seed;
        float d = Stiff ? 0f : Mathf.Clamp01(Drunk);

        // The weave: walking, he drifts side to side, more the drunker he is.
        float walking = Mathf.Clamp01(Speed / 1.2f);
        weavePhase += Time.deltaTime * (1.6f + Speed);
        float side = Mathf.Sin(weavePhase) * weave * d * walking;
        transform.localPosition = homePos + Vector3.right * side + root.InverseTransformVector(lurch);
        transform.localRotation = Quaternion.Euler(0f, 0f, -side * 40f - Vector3.Dot(lurch, root.right) * 18f);

        if (Stiff)
        {
            // Parade-ground straight: chin up, shoulders back.
            Rotate(chest, root.right, -2.5f);
            Rotate(head, root.right, -4f);
        }
        else if (d > 0f)
        {
            float roll = (Mathf.Sin(t * 1.15f) * 0.7f + (Mathf.PerlinNoise(t * 0.45f, 3.1f) - 0.5f) * 0.9f) * swayRoll * d;
            float pitch = (2.5f + Mathf.Sin(t * 0.73f) * 2.5f) * d;
            Rotate(spine, root.forward, roll * 0.55f);
            Rotate(spine, root.right, pitch);
            Rotate(chest, root.forward, roll * 0.45f);
            Rotate(neck, root.forward, -roll * 0.3f);
            // The head lolls: drooping, wandering, catching itself.
            Rotate(head, root.right, (5f + Mathf.Sin(t * 0.9f) * 4f) * d * (1f - SipWeight));
            Rotate(head, Vector3.up, Mathf.Sin(t * 0.5f) * 10f * d);
        }

        // Tip the bottle up: fist at the mouth, the body of the bottle pointing up and forward.
        float w = SipWeight;
        if (w > 0f && hand != null && finger != null)
        {
            Vector3 along = (finger.position - hand.position).normalized;
            Vector3 want = (root.forward * 0.45f + Vector3.up).normalized;
            var turn = Quaternion.FromToRotation(along, want);
            hand.rotation = Quaternion.Slerp(Quaternion.identity, turn, w) * hand.rotation;
        }
    }

    static void Rotate(Transform bone, Vector3 axis, float degrees)
    {
        if (bone != null && degrees != 0f) bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
    }
}
