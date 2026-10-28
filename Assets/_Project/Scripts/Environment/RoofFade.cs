using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Part of the ship you see from outside that never blocks the view: while a player stands under it, or it sits
/// between a player and the camera, or the camera is over it, it hides. keepShadow hides it as shadow-only (it
/// vanishes but still shades what's under it, like a roof); otherwise it goes completely, so you can look down into
/// the floor beneath. Bounds are in the ship's local X/Z, widened by margin. Built by ShipDeckBuilder. Owner: Anadi.
/// </summary>
public class RoofFade : MonoBehaviour
{
    public Vector2 roomMin, roomMax;
    [Tooltip("Metres of slack round the bounds (doorway flicker, or opening up the neighbouring bays too).")]
    public float margin = 0.6f;
    [Tooltip("Hidden as shadow-only, so what's under it stays shaded. Off: hidden completely.")]
    public bool keepShadow = true;
    [Tooltip("A camera higher than this above the ship (the opening's aerial shots) sees the whole thing.")]
    public float cameraAbove = 14f;

    Renderer[] renderers;
    Transform ship;
    bool hidden;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        var mood = GetComponentInParent<PartMood>();
        ship = mood != null ? mood.transform : transform.root; // the ShipDeck root, whatever the scene parents it under
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        bool useCam = cam != null && Local(cam.transform.position).y < cameraAbove;
        Vector3 camLocal = useCam ? Local(cam.transform.position) : Vector3.zero;

        bool hide = useCam && Over(camLocal, camLocal);
        if (!hide)
            foreach (var c in CrewRegistry.All)
                if (!c.IsBot && Covers(Local(c.Position), useCam, camLocal)) { hide = true; break; }
        if (!hide)
            foreach (var m in ThirdPersonMover.All)
                if (m != null && Covers(Local(m.transform.position), useCam, camLocal)) { hide = true; break; }
        if (hide == hidden) return;
        hidden = hide;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.enabled = !hidden || keepShadow;
            r.shadowCastingMode = hidden ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }
    }

    /// <summary>Over the player, or anywhere on the line from the player to the camera.</summary>
    bool Covers(Vector3 player, bool useCam, Vector3 cam) => useCam ? Over(player, cam) : Over(player, player);

    bool Over(Vector3 a, Vector3 b)
    {
        float zMin = Mathf.Min(a.z, b.z), zMax = Mathf.Max(a.z, b.z);
        float xMin = Mathf.Min(a.x, b.x), xMax = Mathf.Max(a.x, b.x);
        return zMax > roomMin.y - margin && zMin < roomMax.y + margin && xMax > roomMin.x - margin && xMin < roomMax.x + margin;
    }

    Vector3 Local(Vector3 world) => ship.InverseTransformPoint(world);
}
