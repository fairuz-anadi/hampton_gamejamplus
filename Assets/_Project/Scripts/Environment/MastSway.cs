using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sways Samprity's foremast — and the crow's nest and rigging riding on it, since they're all its children — with
/// a slow roll and pitch, so the ship reads as easing on the sea instead of standing dead still. Owner: Rabib.
/// Spawns itself in gameplay scenes, like HUD/MiniMap/PortholeFlicker, so nobody has to edit Ship.unity or
/// ShipDeckBuilder.cs.
/// </summary>
public class MastSway : MonoBehaviour
{
    [Tooltip("Degrees either side of upright: roll (around Z) and pitch (around X).")]
    public Vector2 amount = new Vector2(1.1f, 0.6f);
    [Tooltip("Full sway cycles per second, roll and pitch — off exact multiples of each other so the motion doesn't repeat on a short loop.")]
    public Vector2 speed = new Vector2(0.11f, 0.17f);

    Transform mast;
    Quaternion baseRotation;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || FindAnyObjectByType<MastSway>() != null || !GameSystems.IsGameplayScene(scene.name)) return;
        new GameObject("[MastSway]").AddComponent<MastSway>();
    }

    void Start()
    {
        mast = GameObject.Find("Foremast")?.transform;
        if (mast == null) { enabled = false; return; }
        baseRotation = mast.localRotation;
    }

    void Update()
    {
        float t = Time.time;
        float roll = Mathf.Sin(t * speed.x * Mathf.PI * 2f) * amount.x;
        float pitch = Mathf.Sin(t * speed.y * Mathf.PI * 2f + 1.7f) * amount.y;
        mast.localRotation = baseRotation * Quaternion.Euler(pitch, 0f, roll);
    }
}
