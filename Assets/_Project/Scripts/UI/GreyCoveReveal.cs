using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The moment the Captain admits where you're going (Anadi, 28 Oct): the screen darkens and glitches, "THE PROMISED
/// PORT" flickers into "GREY COVE" in big red letters with the minutes left under it, holds, then shrinks away into
/// the readout's PORT row, which carries on counting down. Started by ShipReadout when its reveal fires. Owner: Anadi.
/// </summary>
public class GreyCoveReveal : MonoBehaviour
{
    const float Glitch = 1.6f, Hold = 1.8f, Fly = 0.9f;

    RectTransform canvasRect, title, target;
    Text bigText, subText;
    Image shade;
    CanvasGroup group;
    float started;
    int minutes;

    public static void Play(RectTransform flyTo, int minutes)
    {
        var go = new GameObject("[GreyCoveReveal]");
        var r = go.AddComponent<GreyCoveReveal>();
        r.target = flyTo;
        r.minutes = minutes;
    }

    void Start()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        canvasRect = (RectTransform)transform;

        shade = new GameObject("Shade", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        shade.transform.SetParent(canvasRect, false);
        shade.raycastTarget = false;
        var sr = shade.rectTransform;
        sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = sr.offsetMax = Vector2.zero;

        title = new GameObject("Title", typeof(RectTransform)).GetComponent<RectTransform>();
        title.SetParent(canvasRect, false);
        title.sizeDelta = new Vector2(1600f, 300f);
        var pirata = Resources.Load<Font>("Fonts/PirataOne-Regular");
        var body = Resources.Load<Font>("Fonts/PatrickHand-Regular") ?? pirata;
        bigText = Label(title, pirata, 160, new Vector2(0f, 30f));
        subText = Label(title, body, 46, new Vector2(0f, -90f));
        bigText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        subText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        started = Time.unscaledTime;
    }

    static Text Label(RectTransform parent, Font font, int size, Vector2 pos)
    {
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = new Vector2(1600f, size * 1.4f);
        return t;
    }

    void Update()
    {
        if (bigText == null) return;
        float t = Time.unscaledTime - started;
        var red = new Color(0.95f, 0.32f, 0.26f);
        var cream = new Color(0.97f, 0.93f, 0.84f);

        if (t < Glitch)
        {
            // A bad signal: the lie and the truth fighting, the truth winning.
            bool truth = Mathf.PerlinNoise(t * 16f, 3.1f) > 0.62f - t / Glitch * 0.5f;
            bigText.text = truth ? "GREY COVE" : "THE PROMISED PORT";
            bigText.color = truth ? red : cream;
            subText.text = "";
            title.anchoredPosition = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * (truth ? 14f : 4f);
            shade.color = new Color(0.02f, 0.02f, 0.04f, Mathf.Clamp01(t / 0.3f) * 0.7f);
            title.localScale = Vector3.one;
            return;
        }
        bigText.text = "GREY COVE";
        bigText.color = red;
        subText.text = $"{minutes} MINUTES";
        subText.color = cream;
        if (t < Glitch + Hold)
        {
            title.anchoredPosition = Vector2.zero;
            title.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(t * 6f));
            return;
        }
        // Shrink into the readout's PORT row and let it carry on.
        float k = Mathf.Clamp01((t - Glitch - Hold) / Fly);
        float e = k * k * (3f - 2f * k);
        Vector2 to = Vector2.zero;
        if (target != null)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out to);
        }
        title.anchoredPosition = Vector2.Lerp(Vector2.zero, to, e);
        title.localScale = Vector3.one * Mathf.Lerp(1f, 0.12f, e);
        shade.color = new Color(0.02f, 0.02f, 0.04f, 0.7f * (1f - e));
        group.alpha = 1f - Mathf.Clamp01((k - 0.7f) / 0.3f);
        if (k >= 1f) Destroy(gameObject);
    }
}
