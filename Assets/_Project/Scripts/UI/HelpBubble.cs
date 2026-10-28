using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A "!" bubble floats above whoever calls for help (GameEvents.HelpCalled) for about 2 s, billboarded to
/// Camera.main. Self-spawning, gameplay scenes only, like MastSway/MiniMap. Owner: Rabib.
///
/// Fixed pool of 4 world-space bubbles (built once here, never Instantiated again); the oldest is reused if all
/// four are busy at once. Reads only the calling actor's own transform — never IsStoker or a bot's sabotage
/// target, so a call is exactly as visible whoever makes it. Hidden during GameState.Iceberg.
///
/// Skipped: the "coming" bubble over a bot dispatched to help. CrewRoster.Dispatch() (AI/CrewRoster.cs) decides
/// which bot answers a request internally and raises nothing public when it does — GameEvents has HelpCalled
/// (the request) but no "a bot is on its way" event. To add that bubble, Riham would need one new event, e.g.
/// GameEvents.HelpDispatched(ICrewActor bot, StationType station), raised from inside Dispatch() at the point it
/// picks a bot for a request; this script would then subscribe to it exactly like HelpCalled.
/// </summary>
public class HelpBubble : MonoBehaviour
{
    [SerializeField] bool effectEnabled = true;
    [SerializeField] float showSeconds = 2f;
    [SerializeField] float headHeight = 2.1f;
    [SerializeField] float worldSize = 0.45f;
    [SerializeField] Color backColor = new Color(0.95f, 0.78f, 0.42f, 0.92f);
    [SerializeField] Color textColor = new Color(0.12f, 0.08f, 0.04f);

    struct Slot
    {
        public Transform root;
        public Transform follow;
        public float hideAt;
    }

    Slot[] slots = new Slot[4];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || FindAnyObjectByType<HelpBubble>() != null || !GameSystems.IsGameplayScene(scene.name)) return;
        new GameObject("[HelpBubble]").AddComponent<HelpBubble>();
    }

    void Awake()
    {
        var backMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { color = backColor };
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        for (int i = 0; i < slots.Length; i++)
        {
            var root = new GameObject("Bubble_" + i).transform;
            root.SetParent(transform, false);

            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            back.name = "Back";
            Destroy(back.GetComponent<Collider>());
            back.transform.SetParent(root, false);
            back.transform.localScale = Vector3.one * worldSize;
            var backRenderer = back.GetComponent<MeshRenderer>();
            backRenderer.sharedMaterial = backMat;
            backRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            backRenderer.receiveShadows = false;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(root, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            var tm = textGo.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = "!";
            tm.characterSize = worldSize * 0.22f;
            tm.fontSize = 64;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = textColor;
            var tmRenderer = textGo.GetComponent<MeshRenderer>();
            tmRenderer.sharedMaterial = font.material;
            tmRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tmRenderer.receiveShadows = false;

            root.gameObject.SetActive(false);
            slots[i] = new Slot { root = root };
        }
    }

    void Start() => GameEvents.HelpCalled += OnHelpCalled;

    void OnDestroy() => GameEvents.HelpCalled -= OnHelpCalled;

    void OnHelpCalled(ICrewActor who, StationType station)
    {
        if (!effectEnabled || who == null || !(who is Component c)) return;
        bool iceberg = GameManager.Instance != null && GameManager.Instance.State == GameState.Iceberg;
        if (iceberg) return;

        int slot = -1;
        float oldest = float.MaxValue;
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].root.gameObject.activeSelf) { slot = i; break; }
            if (slots[i].hideAt < oldest) { oldest = slots[i].hideAt; slot = i; }
        }

        slots[slot].follow = c.transform;
        slots[slot].hideAt = Time.time + showSeconds;
        slots[slot].root.gameObject.SetActive(true);
    }

    void LateUpdate()
    {
        bool hidden = !effectEnabled || (GameManager.Instance != null && GameManager.Instance.State == GameState.Iceberg);
        var cam = Camera.main;

        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            if (!s.root.gameObject.activeSelf) continue;
            if (hidden || Time.time >= s.hideAt || s.follow == null) { s.root.gameObject.SetActive(false); continue; }

            s.root.position = s.follow.position + Vector3.up * headHeight;
            if (cam != null) s.root.forward = (s.root.position - cam.transform.position).normalized;
        }
    }
}
