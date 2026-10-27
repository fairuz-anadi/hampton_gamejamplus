using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-right sun/moon dial: the sun and moon ride round a ring as the 5-minute day passes, the face shifts from sky
/// blue to night navy, and the name of the time of day sits underneath. Owner: Samprity. Reads DayNightCycle only.
/// </summary>
public class TimeOfDayDial : MonoBehaviour
{
    public CanvasGroup group;
    public RectTransform orbit;
    public RectTransform sun, moon;
    public Image face;
    public Text phaseName;
    public Color dayFace = new Color(0.36f, 0.56f, 0.82f, 0.85f);
    public Color duskFace = new Color(0.72f, 0.46f, 0.36f, 0.85f);
    public Color nightFace = new Color(0.07f, 0.1f, 0.2f, 0.85f);

    void Update() => Show(DayNightCycle.Current);

    /// <summary>Draws the dial for a clock. Also used by the editor preview.</summary>
    public void Show(DayNightCycle cycle, float? phaseOverride = null)
    {
        if (cycle == null) { group.alpha = 0f; return; }
        group.alpha = 1f;
        float phase = phaseOverride ?? cycle.Phase;
        // Noon (middle of the day) sits at the top of the ring; midnight at the bottom brings the moon up top.
        float noon = cycle.dayShare * 0.5f;
        float angle = -(phase - noon) * 360f;
        orbit.localRotation = Quaternion.Euler(0f, 0f, angle);
        sun.localRotation = moon.localRotation = Quaternion.Euler(0f, 0f, -angle); // keep the icons upright

        var tod = cycle.Sample(phase);
        float dusk = tod.daylight > 0.05f && tod.daylight < 0.9f ? 1f - Mathf.Abs(tod.daylight - 0.5f) * 2f : 0f;
        face.color = Color.Lerp(Color.Lerp(nightFace, dayFace, tod.daylight), duskFace, dusk * 0.7f);
        phaseName.text = cycle.NameOf(phase).ToUpperInvariant();
    }
}
