using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// A theatrical beat on top of Riham's muster vote: the screen darkens, time holds for a freeze-frame beat, a
/// spotlight finds the accused, then "STOKER!" or "NOT THE STOKER" lands with a sting. Purely presentation —
/// reacts to MusterVote's public static events (VoteEnded), never touches her file. Owner: Rabib.
/// Spawns itself in gameplay scenes, like HUD/MiniMap, so nobody has to edit Ship.unity. Does nothing until
/// something in the scene actually calls a muster vote (MusterVote isn't wired into GameSystems yet — Riham's
/// integration call, not this script's).
/// </summary>
public class MusterSpotlight : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float volume = 0.4f;
    [SerializeField] float slowMoScale = 0.15f;
    [SerializeField] float slowMoSeconds = 0.5f;
    [SerializeField] float revealHoldSeconds = 2.2f;
    [SerializeField] float fadeSeconds = 0.35f;
    [SerializeField] float darkAlpha = 0.65f;

    const int Rate = 44100;

    Image overlayImage;
    Light spotLight;
    TextMesh label;
    AudioSource audioSource;
    AudioClip caughtSting, wrongSting, fizzleSting;
    Coroutine running;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || FindAnyObjectByType<MusterSpotlight>() != null || !GameSystems.IsGameplayScene(scene.name)) return;
        new GameObject("[MusterSpotlight]").AddComponent<MusterSpotlight>();
    }

    void Awake()
    {
        BuildOverlay();
        BuildSpotLight();
        BuildLabel();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        caughtSting = Tones("sting_caught", (440f, 0.1f), (587f, 0.1f), (740f, 0.12f), (988f, 0.3f));
        wrongSting = Slide("sting_wrong", 480f, 160f, 0.6f);
        fizzleSting = Buzz("sting_fizzle", 90f, 0.2f);
    }

    void OnEnable() => MusterVote.VoteEnded += OnVoteEnded;

    void OnDisable()
    {
        MusterVote.VoteEnded -= OnVoteEnded;
        Time.timeScale = 1f; // never leave the game stuck in slow motion
    }

    void OnVoteEnded(int seat, bool wasStoker)
    {
        Time.timeScale = 1f; // a new vote landing mid-reveal must not inherit a half-finished slow-mo
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(seat > 0 ? Reveal(seat, wasStoker) : Fizzle());
    }

    IEnumerator Fizzle()
    {
        Play(fizzleSting);
        yield return Fade(0.2f, fadeSeconds * 0.6f);
        yield return new WaitForSecondsRealtime(0.3f);
        yield return Fade(0f, fadeSeconds);
    }

    IEnumerator Reveal(int seat, bool wasStoker)
    {
        var target = CrewRegistry.BySeat(seat);
        if (target == null) yield break;

        yield return Fade(darkAlpha, fadeSeconds);

        float prevScale = Time.timeScale;
        Time.timeScale = slowMoScale;
        spotLight.gameObject.SetActive(true);
        label.gameObject.SetActive(false);

        float t = 0f;
        while (t < slowMoSeconds)
        {
            t += Time.unscaledDeltaTime;
            spotLight.transform.position = target.Position + Vector3.up * 6f;
            spotLight.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            spotLight.intensity = Mathf.Lerp(0f, 6f, t / slowMoSeconds);
            yield return null;
        }
        Time.timeScale = prevScale;

        label.gameObject.SetActive(true);
        label.text = wasStoker ? "STOKER!" : "NOT THE STOKER";
        label.color = wasStoker ? new Color(1f, 0.35f, 0.25f) : new Color(0.7f, 0.85f, 1f);
        Play(wasStoker ? caughtSting : wrongSting);

        float hold = 0f;
        while (hold < revealHoldSeconds)
        {
            hold += Time.unscaledDeltaTime;
            spotLight.transform.position = target.Position + Vector3.up * 6f;
            label.transform.position = target.Position + Vector3.up * 2.4f;
            if (Camera.main != null)
            {
                // Keeps the floating word legible whether the camera is a close solo follow-cam or the far
                // fixed overview used for 2+ players — world-space text would otherwise read tiny from far away.
                float dist = Vector3.Distance(Camera.main.transform.position, label.transform.position);
                label.transform.localScale = Vector3.one * Mathf.Max(0.3f, dist / 20f);
                label.transform.rotation = Camera.main.transform.rotation;
            }
            yield return null;
        }

        yield return Fade(0f, fadeSeconds);
        spotLight.gameObject.SetActive(false);
        label.gameObject.SetActive(false);
    }

    IEnumerator Fade(float to, float seconds)
    {
        float from = overlayImage.color.a;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            var c = overlayImage.color; c.a = Mathf.Lerp(from, to, t / Mathf.Max(0.01f, seconds)); overlayImage.color = c;
            yield return null;
        }
        var c2 = overlayImage.color; c2.a = to; overlayImage.color = c2;
    }

    void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, volume);
    }

    // ---------- Visuals (built once at Awake, toggled/repositioned per reveal, never recreated) ----------

    void BuildOverlay()
    {
        var canvasGo = new GameObject("[MusterSpotlightOverlay]");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50; // above HUD's own Canvas (sortingOrder 10)
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var imgGo = new GameObject("Dark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imgGo.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
        overlayImage = imgGo.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0f);
        overlayImage.raycastTarget = false;
    }

    void BuildSpotLight()
    {
        var go = new GameObject("[MusterSpotlightLight]");
        go.transform.SetParent(transform, false);
        spotLight = go.AddComponent<Light>();
        spotLight.type = LightType.Spot;
        spotLight.color = new Color(1f, 0.95f, 0.85f);
        spotLight.range = 14f;
        spotLight.spotAngle = 35f;
        spotLight.intensity = 0f;
        spotLight.shadows = LightShadows.None;
        go.SetActive(false);
    }

    void BuildLabel()
    {
        var go = new GameObject("[MusterSpotlightLabel]");
        go.transform.SetParent(transform, false);
        label = go.AddComponent<TextMesh>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.GetComponent<MeshRenderer>().material = label.font.material;
        label.characterSize = 0.4f;
        label.fontSize = 72;
        label.fontStyle = FontStyle.Bold;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.text = "";
        go.SetActive(false);
    }

    // ---------- Tiny synth (same technique StationSfx.cs uses, duplicated here since its helpers are private there) ----------

    static AudioClip Tones(string name, params (float hz, float seconds)[] notes)
    {
        int total = 0;
        foreach (var n in notes) total += Mathf.CeilToInt(n.seconds * Rate);
        var data = new float[total];
        int i = 0;
        foreach (var n in notes)
        {
            int len = Mathf.CeilToInt(n.seconds * Rate);
            for (int s = 0; s < len; s++, i++)
                data[i] = n.hz <= 0f ? 0f : Mathf.Sin(2f * Mathf.PI * n.hz * s / Rate) * Envelope(s, len);
        }
        return Make(name, data);
    }

    static AudioClip Slide(string name, float fromHz, float toHz, float seconds)
    {
        int len = Mathf.CeilToInt(seconds * Rate);
        var data = new float[len];
        float phase = 0f;
        for (int s = 0; s < len; s++)
        {
            float hz = Mathf.Lerp(fromHz, toHz, (float)s / len);
            phase += 2f * Mathf.PI * hz / Rate;
            data[s] = Mathf.Sin(phase) * Envelope(s, len);
        }
        return Make(name, data);
    }

    static AudioClip Buzz(string name, float hz, float seconds)
    {
        int len = Mathf.CeilToInt(seconds * Rate);
        var data = new float[len];
        for (int s = 0; s < len; s++)
        {
            float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * hz * s / Rate));
            data[s] = square * 0.5f * Envelope(s, len);
        }
        return Make(name, data);
    }

    static float Envelope(int s, int len)
    {
        int edge = Mathf.Min(len / 4, Rate / 100);
        if (s < edge) return (float)s / edge;
        if (s > len - edge) return (float)(len - s) / edge;
        return 1f;
    }

    static AudioClip Make(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
