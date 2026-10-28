using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// A bright title card at the start of each part (Anadi's approved designs, 28 Oct): the deck closes in a circle, the
/// card opens with its ship bobbing on drifting waves and the part's ribbon dropping in with a bounce, then the
/// circle closes on the card and opens back onto the deck. Time is frozen meanwhile and the Captain waits.
/// Enter / Space / A skips once it's up. Art is in Resources/PartCards (layers: bg, waves, ship, ribbon), drawn by
/// Anadi's partlayers.py. Built in code; spawned by GameSystems in Ship. Owner: Anadi.
/// </summary>
public class PartCard : MonoBehaviour
{
    public static bool IsShowing { get; private set; }

    [Min(0.5f)] public float holdSeconds = 2.8f;
    [Min(0.1f)] public float irisSeconds = 0.55f;

    // Where each part's cropped layers sit, in 1920x1080 top-left pixels (printed by partlayers.py).
    static readonly RectInt[] ShipRects = { new RectInt(752, 481, 417, 344), new RectInt(753, 501, 414, 342), new RectInt(492, 480, 416, 366) };
    static readonly RectInt[] RibbonRects = { new RectInt(420, 349, 1081, 210), new RectInt(330, 349, 1261, 210), new RectInt(310, 349, 1301, 210) };
    static readonly float[] ShipTilt = { 0f, -6f, 4f }; // already drawn in; the bob rocks around it

    enum Step { Idle, Waiting, Closing, Opening, Holding, ClosingCard, OpeningGame }

    Step step;
    int part = -1, pending = -1;
    float t, prevTimeScale = 1f;
    Canvas canvas;
    RectTransform cardRoot, ship, waves, ribbon;
    RawImage bg, wavesImg, shipImg, ribbonImg, iris;
    Texture2D irisTex;
    readonly Texture2D[,] layers = new Texture2D[3, 4];

    void OnEnable() => GameEvents.StateChanged += OnState;
    void OnDisable() => GameEvents.StateChanged -= OnState;

    void Start()
    {
        Build();
        // The run may already be in Part 1 by the time this exists.
        var gm = GameManager.Instance;
        if (gm != null) OnState(gm.State);
    }

    void OnDestroy()
    {
        if (IsShowing) { IsShowing = false; Time.timeScale = prevTimeScale; }
        if (canvas != null) Destroy(canvas.gameObject);
        if (irisTex != null) Destroy(irisTex);
    }

    void OnState(GameState s)
    {
        int p = s == GameState.Part1 ? 0 : s == GameState.Part2 ? 1 : s == GameState.Part3 ? 2 : -1;
        if (p < 0 || p == part) return;
        pending = p;
        if (step == Step.Idle) step = Step.Waiting;
    }

    void Update()
    {
        if (canvas == null || step == Step.Idle) return;
        float dt = Time.unscaledDeltaTime;
        t += dt;
        switch (step)
        {
            case Step.Waiting:
                // Not over the fade in from the menu, and not on top of the pause menu.
                if (OpeningDirector.HandoffPending || (PauseMenu.Instance != null && PauseMenu.Instance.IsPaused)) return;
                part = pending;
                Show(part);
                IsShowing = true;
                prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                Go(Step.Closing);
                break;
            case Step.Closing:
                SetIris(1f - Ease(t / irisSeconds));
                if (t >= irisSeconds) { cardRoot.gameObject.SetActive(true); Go(Step.Opening); }
                break;
            case Step.Opening:
                SetIris(Ease(t / irisSeconds));
                Animate(t);
                if (t >= irisSeconds) Go(Step.Holding);
                break;
            case Step.Holding:
                Animate(t + irisSeconds);
                if (t >= holdSeconds || (t > 0.6f && SkipPressed())) Go(Step.ClosingCard);
                break;
            case Step.ClosingCard:
                Animate(t + irisSeconds + holdSeconds);
                SetIris(1f - Ease(t / irisSeconds));
                if (t >= irisSeconds) { cardRoot.gameObject.SetActive(false); Go(Step.OpeningGame); }
                break;
            case Step.OpeningGame:
                SetIris(Ease(t / irisSeconds));
                if (t >= irisSeconds)
                {
                    iris.enabled = false;
                    IsShowing = false;
                    Time.timeScale = prevTimeScale;
                    // A part can change while the card is up (debug skips); show the newer one next.
                    step = pending != part ? Step.Waiting : Step.Idle;
                }
                break;
        }
    }

    void Go(Step s) { step = s; t = 0f; }

    static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    static bool SkipPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) return true;
        foreach (var pad in Gamepad.all) if (pad.buttonSouth.wasPressedThisFrame) return true;
        return false;
    }

    /// <summary>The ship bobs and rocks, the waves drift, the ribbon drops in with a bounce and breathes.</summary>
    void Animate(float time)
    {
        ship.anchoredPosition = shipBase + new Vector2(0f, Mathf.Sin(time * 2.4f) * 9f);
        ship.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.7f) * 3f);
        waves.anchoredPosition = new Vector2(Mathf.Sin(time * 0.9f) * 70f, Mathf.Sin(time * 1.3f) * 6f);
        float drop = Mathf.Clamp01((time - 0.25f) / 0.75f);
        ribbon.anchoredPosition = ribbonBase + new Vector2(0f, (1f - EaseOutBack(drop)) * 520f);
        ribbon.localScale = Vector3.one * (1f + Mathf.Sin(time * 3f) * 0.012f);
        ribbon.localRotation = Quaternion.Euler(0f, 0f, (1f - drop) * -6f);
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    /// <summary>open 0 = black, 1 = the circle is wider than the screen.</summary>
    void SetIris(float open)
    {
        iris.enabled = true;
        // Zooming the texture's UVs: a big UV window makes the hole small; a small one blows it past the corners.
        float s = Mathf.Lerp(40f, 0.3f, Mathf.Pow(open, 0.35f));
        var r = iris.rectTransform.rect;
        float aspect = r.height > 0f ? r.width / r.height : 16f / 9f;
        iris.uvRect = new Rect(0.5f - s * aspect * 0.5f, 0.5f - s * 0.5f, s * aspect, s);
    }

    // ---------- Build ----------

    Vector2 shipBase, ribbonBase;

    void Show(int p)
    {
        bg.texture = layers[p, 0];
        wavesImg.texture = layers[p, 1];
        shipImg.texture = layers[p, 2];
        ribbonImg.texture = layers[p, 3];
        shipBase = Place(ship, ShipRects[p]);
        ribbonBase = Place(ribbon, RibbonRects[p]);
        Animate(0f);
    }

    static Vector2 Place(RectTransform r, RectInt px)
    {
        r.sizeDelta = new Vector2(px.width, px.height);
        var pos = new Vector2(px.x + px.width * 0.5f - 960f, 540f - (px.y + px.height * 0.5f));
        r.anchoredPosition = pos;
        return pos;
    }

    void Build()
    {
        string[] names = { "bg", "waves", "ship", "ribbon" };
        for (int p = 0; p < 3; p++)
            for (int k = 0; k < 4; k++)
            {
                layers[p, k] = Resources.Load<Texture2D>($"PartCards/part{p + 1}_{names[k]}");
                if (layers[p, k] != null) layers[p, k].wrapMode = TextureWrapMode.Clamp;
            }
        if (layers[0, 0] == null) { Debug.LogWarning("[PartCard] Resources/PartCards is missing; no part cards."); enabled = false; return; }

        canvas = new GameObject("[PartCard]", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 130; // over the HUD and the crew's bubbles
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // the whole card always fits
        var root = (RectTransform)canvas.transform;

        cardRoot = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
        cardRoot.SetParent(root, false);
        cardRoot.anchorMin = cardRoot.anchorMax = new Vector2(0.5f, 0.5f);
        cardRoot.sizeDelta = new Vector2(1920f, 1080f);
        bg = Raw(cardRoot, "Sky", new Vector2(1920f, 1080f));
        // Stretch the sky past the 16:9 frame on wider or taller screens.
        bg.rectTransform.sizeDelta = new Vector2(3840f, 1080f * 2f);
        bg.uvRect = new Rect(-0.5f, -0.5f, 2f, 2f);
        wavesImg = Raw(cardRoot, "Waves", new Vector2(2120f, 1080f));
        waves = wavesImg.rectTransform;
        shipImg = Raw(cardRoot, "Ship", Vector2.one);
        ship = shipImg.rectTransform;
        ribbonImg = Raw(cardRoot, "Ribbon", Vector2.one);
        ribbon = ribbonImg.rectTransform;
        cardRoot.gameObject.SetActive(false);

        irisTex = IrisTexture(256);
        iris = Raw(root, "Iris", Vector2.zero);
        iris.rectTransform.anchorMin = Vector2.zero;
        iris.rectTransform.anchorMax = Vector2.one;
        iris.rectTransform.sizeDelta = Vector2.zero;
        iris.texture = irisTex;
        iris.enabled = false;
    }

    static RawImage Raw(RectTransform parent, string name, Vector2 size)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        img.transform.SetParent(parent, false);
        img.raycastTarget = false;
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        img.rectTransform.sizeDelta = size;
        return img;
    }

    /// <summary>Black with a soft-edged round hole in the middle; clamped, so outside the texture is black too.</summary>
    static Texture2D IrisTexture(int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[n * n];
        float r = n * 0.42f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                byte a = (byte)(Mathf.Clamp01((d - r) / 1.5f) * 255f);
                px[y * n + x] = new Color32(8, 10, 16, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
