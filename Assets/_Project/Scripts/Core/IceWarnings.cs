using UnityEngine;

/// <summary>
/// The 3 numbered ice warnings ("MSG 1, 2, 3"). Owner: Anadi. Spawned with the core systems.
/// They arrive at the Wireless desk 3:20, 4:30 and 5:40 into the run. Carrying one to the Bridge within 40 s is
/// Safety +5; late or filed unread is Safety -5. The Stoker can hide the next one: it never arrives, Safety -8 when it
/// would have, and the wireless log skips its number.
///   Rabib (Wireless desk, carrying): show a warning on GameEvents.IceWarningArrived(n), call Deliver(n) at the Bridge,
///          FileUnread(n) if someone files it at the desk. TimeLeft(n) drives a countdown.
///   Riham (Stoker): HideNext() returns the hidden warning's number, or 0 when there's none left to hide.
/// </summary>
public class IceWarnings : MonoBehaviour
{
    public enum Status { Pending, Waiting, Delivered, Lost, Hidden }

    public static IceWarnings Instance { get; private set; }

    [Tooltip("Seconds into the run when each warning arrives (plan: 3:20, 4:30, 5:40).")]
    [SerializeField] float[] arrivalSeconds = { 200f, 270f, 340f };
    [SerializeField] float deliverWindow = 40f;

    Status[] status;
    float[] arrivedAt;
    bool[] hiddenCharged;

    /// <summary>Warnings are numbered 1..Count.</summary>
    public int Count => arrivalSeconds.Length;

    public Status StatusOf(int n) => Valid(n) ? status[n - 1] : Status.Pending;

    /// <summary>Seconds left to deliver warning n, or 0 when it isn't waiting at the desk.</summary>
    public float TimeLeft(int n) =>
        Valid(n) && status[n - 1] == Status.Waiting ? Mathf.Max(0f, deliverWindow - (Time.time - arrivedAt[n - 1])) : 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        ResetAll();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Called by GameManager.StartRun.</summary>
    public void ResetAll()
    {
        status = new Status[Count];
        arrivedAt = new float[Count];
        hiddenCharged = new bool[Count];
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Part < 1 || gm.Part > 3) return; // only during the story parts
        float runTime = Time.time - RunRecord.RunStartTime;

        for (int i = 0; i < Count; i++)
        {
            int n = i + 1;
            switch (status[i])
            {
                case Status.Pending when runTime >= arrivalSeconds[i]:
                    status[i] = Status.Waiting;
                    arrivedAt[i] = Time.time;
                    GameEvents.RaiseIceWarningArrived(n);
                    break;
                case Status.Waiting when Time.time - arrivedAt[i] > deliverWindow:
                    Lose(n);
                    break;
                case Status.Hidden when !hiddenCharged[i] && runTime >= arrivalSeconds[i]:
                    hiddenCharged[i] = true;
                    RunRecord.IceWarningsHidden++;
                    MeterSystem.Instance?.Apply(MeterRules.SabotageHideIceWarning);
                    GameEvents.RaiseIceWarningHidden(n);
                    break;
            }
        }
    }

    /// <summary>Someone carried warning n to the Bridge in time. False if it wasn't waiting.</summary>
    public bool Deliver(int n)
    {
        if (!Valid(n) || status[n - 1] != Status.Waiting) return false;
        status[n - 1] = Status.Delivered;
        RunRecord.IceWarningsDelivered++;
        MeterSystem.Instance?.Apply(MeterRules.IceWarningDelivered);
        GameEvents.RaiseIceWarningDelivered(n);
        return true;
    }

    /// <summary>Warning n was filed unread at the desk (order 7, or a crew choice). Counts as lost.</summary>
    public bool FileUnread(int n)
    {
        if (!Valid(n) || status[n - 1] != Status.Waiting) return false;
        Lose(n);
        return true;
    }

    /// <summary>The Stoker hides the next warning that hasn't arrived yet. Returns its number, or 0.</summary>
    public int HideNext()
    {
        for (int i = 0; i < Count; i++)
        {
            if (status[i] != Status.Pending) continue;
            status[i] = Status.Hidden;
            return i + 1;
        }
        return 0;
    }

    void Lose(int n)
    {
        status[n - 1] = Status.Lost;
        RunRecord.IceWarningsLost++;
        MeterSystem.Instance?.Apply(MeterRules.IceWarningLost);
        GameEvents.RaiseIceWarningLost(n);
    }

    bool Valid(int n) => status != null && n >= 1 && n <= Count;
}
