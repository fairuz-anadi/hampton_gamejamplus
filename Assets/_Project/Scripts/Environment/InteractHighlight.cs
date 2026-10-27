using UnityEngine;

/// <summary>
/// Gives a station a soft gold outline that fades in while a player is within reach, so interactable things stand
/// out without outlining the whole ship. Owner: Samprity. Purely visual; it never changes gameplay.
/// Sits on the deck's Station_* objects; the outline is an extra Port/Outline material on their meshes.
/// </summary>
public class InteractHighlight : MonoBehaviour
{
    public Material outlineTemplate;
    [Tooltip("How close a player must be for the outline to show, in metres.")]
    public float range = 2.8f;
    [Tooltip("Outline thickness when fully shown.")]
    public float width = 2.2f;
    [Min(0.01f)] public float fadeSeconds = 0.25f;

    Material outline;
    float shown;

    void Start()
    {
        if (outlineTemplate == null) return;
        outline = new Material(outlineTemplate);
        outline.SetFloat("_OutlineWidth", 0f);
        foreach (var r in GetComponentsInChildren<MeshRenderer>())
        {
            if (r.name == "StatusLight") continue; // the bulb already glows
            var mats = r.sharedMaterials;
            var withOutline = new Material[mats.Length + 1];
            mats.CopyTo(withOutline, 0);
            withOutline[mats.Length] = outline;
            r.sharedMaterials = withOutline;
        }
    }

    void OnDestroy()
    {
        if (outline != null) Destroy(outline);
    }

    void Update()
    {
        if (outline == null) return;
        bool near = NearestHumanDistance() < range;
        shown = Mathf.MoveTowards(shown, near ? 1f : 0f, Time.unscaledDeltaTime / fadeSeconds);
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f);
        outline.SetFloat("_OutlineWidth", width * Mathf.SmoothStep(0f, 1f, shown) * pulse);
    }

    float NearestHumanDistance()
    {
        float best = float.MaxValue;
        Vector3 p = transform.position;
        bool anyCrew = false;
        foreach (var c in CrewRegistry.All)
        {
            if (c.IsBot) continue;
            anyCrew = true;
            best = Mathf.Min(best, Flat(c.Position - p));
        }
        if (!anyCrew)
            foreach (var m in ThirdPersonMover.All) best = Mathf.Min(best, Flat(m.transform.position - p));
        return best;
    }

    static float Flat(Vector3 d) { d.y = 0f; return d.magnitude; }
}
