using UnityEngine;
using UnityEngine.UI;

/// <summary>One meter row: icon, bar, number, and a "+5" / "-12" that pops when it changes.</summary>
public class MeterBar : MonoBehaviour
{
    public Image fill;
    public Text valueText;
    public Text deltaText;
    public Color color = Color.white;
    [Tooltip("At or below this the bar turns red and pulses.")]
    public int dangerBelow = 25;
    public Color dangerColor = new Color(0.95f, 0.3f, 0.25f);
    [Min(0.01f)] public float fillSpeed = 2.5f;

    int value = -1;
    float shown;
    float deltaTime;

    /// <summary>Sets the meter. The first call snaps; later calls animate and show the change.</summary>
    public void SetValue(int v)
    {
        v = Mathf.Clamp(v, 0, 100);
        if (value < 0) { shown = v; }
        else if (v != value && deltaText != null)
        {
            int d = v - value;
            deltaText.text = d > 0 ? "+" + d : d.ToString();
            deltaText.color = d > 0 ? new Color(0.7f, 1f, 0.75f) : new Color(1f, 0.55f, 0.5f);
            deltaTime = 1.8f;
        }
        value = v;
        if (valueText != null) valueText.text = v.ToString();
        Apply();
    }

    void Update()
    {
        if (value < 0) return;
        shown = Mathf.MoveTowards(shown, value, fillSpeed * 100f * Time.unscaledDeltaTime * 0.4f);
        if (deltaTime > 0f) deltaTime -= Time.unscaledDeltaTime;
        Apply();
    }

    void Apply()
    {
        if (fill != null)
        {
            fill.fillAmount = shown / 100f;
            bool danger = value <= dangerBelow;
            float pulse = danger ? 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f) : 1f;
            fill.color = (danger ? dangerColor : color) * new Color(pulse, pulse, pulse, 1f);
        }
        if (deltaText != null)
        {
            var c = deltaText.color;
            c.a = Mathf.Clamp01(deltaTime / 0.6f);
            deltaText.color = c;
        }
    }
}
