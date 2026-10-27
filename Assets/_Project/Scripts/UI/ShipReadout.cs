using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The ship's instruments in the HUD, right-aligned beside the time-of-day dial: compass, wheel, ship's clock and the
/// ETA, plus the wireless message when an ice warning comes in. Riham's OffMoments makes the numbers drift; this only
/// draws them, and turns OffMoments' plain fallback box off so the two never overlap. Built by HudBuilder.
/// </summary>
public class ShipReadout : MonoBehaviour
{
    public CanvasGroup group;
    public Text compass, wheel, clock, eta;
    public CanvasGroup wirelessGroup;
    public Text wireless;
    [Min(1f)] public float wirelessSeconds = 8f;

    float wirelessLeft;

    void OnEnable()
    {
        OffMoments.DrawFallbackHud = false;
        OffMoments.WirelessMessage += OnWireless;
    }

    void OnDisable()
    {
        OffMoments.DrawFallbackHud = true;
        OffMoments.WirelessMessage -= OnWireless;
    }

    void OnWireless(int number, string text)
    {
        wireless.text = text;
        wirelessLeft = wirelessSeconds;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        var off = OffMoments.Instance;
        bool show = off != null && gm != null && gm.Part >= 1 && gm.Part <= 3;
        float dt = Time.unscaledDeltaTime;
        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, dt * 3f);

        wirelessLeft -= dt;
        wirelessGroup.alpha = Mathf.MoveTowards(wirelessGroup.alpha, show && wirelessLeft > 0f ? 1f : 0f, dt * 3f);
        if (!show) return;

        float c = off.CompassHeading, w = off.WheelHeading;
        compass.text = $"{OffMoments.PointName(c)} {c:000}°";
        wheel.text = $"{OffMoments.PointName(w)} {w:000}°";
        clock.text = off.ClockText;
        eta.text = off.EtaText.Replace("Promised Port: ", "");
    }
}
