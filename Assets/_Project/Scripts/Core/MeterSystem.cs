using UnityEngine;

/// <summary>The three meters, 0-100. Owner: Anadi.</summary>
public class MeterSystem : MonoBehaviour
{
    public static MeterSystem Instance { get; private set; }

    [SerializeField, Range(0, 100)] int startCaptainTrust = 50;
    [SerializeField, Range(0, 100)] int startShipSafety = 80;
    [SerializeField, Range(0, 100)] int startCrewTrust = 60;

    public int CaptainTrust { get; private set; }
    public int ShipSafety { get; private set; }
    public int CrewTrust { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        ResetMeters();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ResetMeters() => Set(startCaptainTrust, startShipSafety, startCrewTrust);

    public void Apply(MeterDelta d)
    {
        if (d.IsZero) return;
        Set(CaptainTrust + d.captainTrust, ShipSafety + d.shipSafety, CrewTrust + d.crewTrust);
    }

    public void Set(int captainTrust, int shipSafety, int crewTrust)
    {
        CaptainTrust = Mathf.Clamp(captainTrust, 0, 100);
        ShipSafety = Mathf.Clamp(shipSafety, 0, 100);
        CrewTrust = Mathf.Clamp(crewTrust, 0, 100);
        GameEvents.RaiseMetersChanged();
    }

    /// <summary>Copies the meters into RunRecord so the Results scene can read them.</summary>
    public void SnapshotToRecord()
    {
        RunRecord.FinalCaptainTrust = CaptainTrust;
        RunRecord.FinalShipSafety = ShipSafety;
        RunRecord.FinalCrewTrust = CrewTrust;
    }
}
