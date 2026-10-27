using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The three meters, top left: Captain's Trust, Ship Safety, Crew Trust. Owner: Samprity.
/// Listens to GameEvents.MetersChanged and reads MeterSystem; never changes a meter.
/// </summary>
public class MeterPanel : MonoBehaviour
{
    public MeterBar captain, safety, crew;

    void OnEnable()
    {
        GameEvents.MetersChanged += Refresh;
        Refresh();
    }

    void OnDisable() => GameEvents.MetersChanged -= Refresh;

    void Refresh()
    {
        var m = MeterSystem.Instance;
        if (m == null) return;
        captain.SetValue(m.CaptainTrust);
        safety.SetValue(m.ShipSafety);
        crew.SetValue(m.CrewTrust);
    }
}
