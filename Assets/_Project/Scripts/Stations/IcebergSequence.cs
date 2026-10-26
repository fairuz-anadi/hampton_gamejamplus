using System.Collections;
using UnityEngine;

/// <summary>
/// Stub finale. Tells GameManager the iceberg is handled externally so its placeholder timer doesn't
/// also run, then logs the Iceberg state and finishes with a straight hit after a short delay, so the
/// flow reaches Results. Owner: Rabib.
/// </summary>
public class IcebergSequence : MonoBehaviour
{
    [SerializeField] float delaySeconds = 5f;

    void Awake() => ClaimIceberg();

    // GameManager.Instance may not exist yet when this Awake() runs (scene object order isn't
    // guaranteed), so claim again once every Awake in the scene has finished.
    void Start() => ClaimIceberg();

    static void ClaimIceberg()
    {
        if (GameManager.Instance != null) GameManager.Instance.IcebergHandledExternally = true;
    }

    void OnEnable() => GameEvents.StateChanged += OnStateChanged;
    void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

    void OnStateChanged(GameState state)
    {
        if (state != GameState.Iceberg) return;
        Debug.Log($"[IcebergSequence] Iceberg reached (stub) — finishing head-on in {delaySeconds:0}s.");
        StartCoroutine(FinishAfterDelay());
    }

    IEnumerator FinishAfterDelay()
    {
        yield return new WaitForSeconds(delaySeconds);
        GameManager.Instance?.FinishIceberg(0f);
    }
}
