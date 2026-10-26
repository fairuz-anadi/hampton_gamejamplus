using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The run's state machine: Menu -> Part1 -> Part2 -> Part3 -> Iceberg -> Results. Owner: Anadi.
///
/// Hooks for teammates:
///   Riham:    GameEvents.TruthStarted fires when the 12 orders are done. Set truthSeconds to the monologue length.
///   Rabib:    IcebergSequence sets GameManager.Instance.IcebergHandledExternally = true in Awake,
///             listens for GameState.Iceberg, and calls FinishIceberg(angle) at impact.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] bool startRunOnPlay = true;
    [Tooltip("How long Part 3's monologue runs before the iceberg scene.")]
    [SerializeField] float truthSeconds = 8f;
    [Tooltip("Used only while there is no IcebergSequence in the scene.")]
    [SerializeField] float placeholderIcebergSeconds = 5f;
    [SerializeField] string resultsScene = "Results";

    public GameState State { get; private set; } = GameState.Menu;

    /// <summary>1-3 during the story parts, 4 at the iceberg, 0 otherwise.</summary>
    public int Part => State switch
    {
        GameState.Part1 => 1,
        GameState.Part2 => 2,
        GameState.Part3 => 3,
        GameState.Iceberg => 4,
        _ => 0,
    };

    /// <summary>Rabib's IcebergSequence sets this so the placeholder timer doesn't run.</summary>
    public bool IcebergHandledExternally { get; set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (startRunOnPlay) StartRun();
    }

    public void StartRun()
    {
        StopAllCoroutines();
        RunRecord.Clear();
        MeterSystem.Instance?.ResetMeters();
        var stoker = CrewRegistry.Stoker;
        RunRecord.StokerSeat = stoker != null ? stoker.Seat : 0;
        SetState(GameState.Part1);
        OrderDirector.Instance?.Begin();
    }

    /// <summary>Called by OrderDirector before it issues an order. Only ever moves forward.</summary>
    public void EnterPart(int part)
    {
        var target = part switch { 1 => GameState.Part1, 2 => GameState.Part2, _ => GameState.Part3 };
        if (target > State) SetState(target);
    }

    public void OnAllOrdersDone()
    {
        if (State >= GameState.Iceberg) return;
        StopAllCoroutines();
        StartCoroutine(TruthThenIceberg());
    }

    IEnumerator TruthThenIceberg()
    {
        if (State < GameState.Part3) SetState(GameState.Part3);
        GameEvents.RaiseTruthStarted();
        yield return new WaitForSeconds(truthSeconds);
        StartIceberg();
    }

    public void StartIceberg()
    {
        if (State >= GameState.Iceberg) return;
        StopAllCoroutines();
        OrderDirector.Instance?.Stop();
        SetState(GameState.Iceberg);
        if (!IcebergHandledExternally) StartCoroutine(PlaceholderIceberg());
    }

    IEnumerator PlaceholderIceberg()
    {
        yield return new WaitForSeconds(placeholderIcebergSeconds);
        FinishIceberg(0f);
    }

    /// <summary>Rabib's IcebergSequence calls this at impact with the angle in degrees.</summary>
    public void FinishIceberg(float impactAngle)
    {
        if (State == GameState.Results) return;
        RunRecord.ImpactAngle = impactAngle;
        MeterSystem.Instance?.SnapshotToRecord();
        var stoker = CrewRegistry.Stoker;
        if (stoker != null) RunRecord.StokerSeat = stoker.Seat;
        SetState(GameState.Results);

        if (Application.CanStreamedLevelBeLoaded(resultsScene))
            SceneManager.LoadScene(resultsScene);
        else
            Debug.Log("[GameManager] Results scene isn't in Build Settings yet, staying here.");
    }

    void SetState(GameState next)
    {
        if (next == State) return;
        State = next;
        GameEvents.RaiseStateChanged(next);
        if (Part > 0) GameEvents.RaisePartChanged(Part);
    }

    // ---------- Debug helpers (used by DebugTools) ----------

    public void DebugSkipPart()
    {
        switch (State)
        {
            case GameState.Part1: OrderDirector.Instance?.SkipToPart(2); break;
            case GameState.Part2: OrderDirector.Instance?.SkipToPart(3); break;
            case GameState.Part3: StartIceberg(); break;
            case GameState.Iceberg: FinishIceberg(0f); break;
        }
    }
}
