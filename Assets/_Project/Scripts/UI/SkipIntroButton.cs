using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The intro's skip button (Anadi's approved design, 28 Oct): a dark pill with a brass edge, the key to press,
/// "SKIP INTRO" in spaced Bungee letters and three arrows that run left to right, with a thin brass line along the
/// bottom that fills as the intro plays. Hovered or selected, the pill turns brass. Built by OpeningMenu.BuildSkip.
/// Owner: Anadi.
/// </summary>
public class SkipIntroButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    const float Wd = 360f, Ht = 66f;
    static readonly Color Cream = new Color(0.97f, 0.93f, 0.84f), Brass = new Color(0.86f, 0.68f, 0.34f), Ink = new Color(0.12f, 0.09f, 0.07f);

    System.Action onClick;
    RawImage pill;
    Texture2D pillOff, pillOn;
    Text key;
    Image keyFrame;
    readonly System.Collections.Generic.List<Text> letters = new System.Collections.Generic.List<Text>();
    readonly Image[] chevrons = new Image[3];
    RectTransform progressFill;
    Image progressTrack, progressImg;
    OpeningDirector director;
    bool hover;

    public static SkipIntroButton Build(RectTransform parent, Font keyFont, System.Action onClick)
    {
        var go = new GameObject("SkipIntro", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(Wd, Ht);
        var b = go.AddComponent<SkipIntroButton>();
        b.onClick = onClick;
        b.Make(r, keyFont);
        return b;
    }

    void Make(RectTransform r, Font keyFont)
    {
        pillOff = PaintPill(new Color(0.055f, 0.08f, 0.13f, 0.88f));
        pillOn = PaintPill(new Color(0.77f, 0.57f, 0.24f, 1f));
        pill = new GameObject("Pill", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        pill.transform.SetParent(r, false);
        pill.texture = pillOff;
        Stretch(pill.rectTransform);

        // The key to press, in a little keycap.
        keyFrame = Box(r, "Keycap", new Vector2(46f - Wd / 2, 0f), new Vector2(60f, 34f));
        keyFrame.sprite = UiTheme.Load() != null ? UiTheme.Load().keycap : null;
        keyFrame.type = keyFrame.sprite != null && keyFrame.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        key = Label(r, keyFont, 20, "Esc", new Vector2(46f - Wd / 2, 1f), new Vector2(60f, 34f), TextAnchor.MiddleCenter);

        // SKIP INTRO, letter by letter so the letters can be spaced out.
        var bungee = Resources.Load<Font>("Fonts/Bungee-Regular") ?? keyFont;
        const int size = 22; const float track = 3f;
        bungee.RequestCharactersInTexture("SKIP INTRO", size);
        float x = 92f - Wd / 2;
        foreach (char ch in "SKIP INTRO")
        {
            float adv = bungee.GetCharacterInfo(ch, out var info, size) ? info.advance : size * 0.6f;
            if (ch != ' ') letters.Add(Label(r, bungee, size, ch.ToString(), new Vector2(x + adv * 0.5f, 0f), new Vector2(adv + 4f, 34f), TextAnchor.MiddleCenter));
            x += adv + track;
        }

        // Three arrows that light up in turn.
        var chevTex = PaintChevron();
        var chevSprite = Sprite.Create(chevTex, new Rect(0, 0, chevTex.width, chevTex.height), new Vector2(0.5f, 0.5f));
        for (int i = 0; i < 3; i++)
        {
            chevrons[i] = Box(r, "Arrow", new Vector2(Wd / 2 - 70f + i * 15f, 0f), new Vector2(14f, 24f));
            chevrons[i].sprite = chevSprite;
        }

        // The intro's progress along the bottom edge.
        progressTrack = Box(r, "Track", new Vector2(0f, -Ht / 2 + 6f), new Vector2(Wd - 60f, 3f));
        progressImg = Box(r, "Fill", new Vector2(-(Wd - 60f) / 2, -Ht / 2 + 6f), new Vector2(0f, 3f));
        progressFill = progressImg.rectTransform;
        progressFill.pivot = new Vector2(0f, 0.5f);
        Recolor();
    }

    void Update()
    {
        if (director == null) director = FindAnyObjectByType<OpeningDirector>();
        float t = Time.unscaledTime;
        for (int i = 0; i < 3; i++)
        {
            float phase = Mathf.Repeat(t * 2.2f - i * 0.33f, 1f);
            float a = 0.3f + 0.7f * Mathf.Clamp01(1f - Mathf.Abs(phase - 0.5f) * 3f);
            var c = hover ? Ink : Cream;
            chevrons[i].color = new Color(c.r, c.g, c.b, a);
        }
        float p = director != null && director.menuAt > 0f ? Mathf.Clamp01(director.IntroTime / director.menuAt) : 0f;
        progressFill.sizeDelta = new Vector2((Wd - 60f) * p, 3f);
        key.text = Gamepad.current != null ? "Start" : "Esc";
    }

    public void OnPointerEnter(PointerEventData e) { hover = true; Recolor(); }
    public void OnPointerExit(PointerEventData e) { hover = false; Recolor(); }
    public void OnPointerClick(PointerEventData e) => onClick?.Invoke();

    void Recolor()
    {
        pill.texture = hover ? pillOn : pillOff;
        var c = hover ? Ink : Cream;
        key.color = c;
        keyFrame.color = c;
        foreach (var l in letters) l.color = c;
        progressTrack.color = hover ? new Color(0f, 0f, 0f, 0.25f) : new Color(1f, 1f, 1f, 0.2f);
        progressImg.color = hover ? Ink : Brass;
    }

    void OnDestroy()
    {
        if (pillOff != null) Destroy(pillOff);
        if (pillOn != null) Destroy(pillOn);
    }

    static Texture2D PaintPill(Color fill)
    {
        // Painted at twice the size for clean edges.
        var p = new EndingPainter((int)(Wd * 2), (int)(Ht * 2), new Color(0, 0, 0, 0));
        p.RoundRect(2, 2, Wd * 2 - 2, Ht * 2 - 2, Ht - 2, fill);
        p.RoundRectOutline(2, 2, Wd * 2 - 2, Ht * 2 - 2, Ht - 2, 4, Brass);
        var t = p.ToTexture();
        t.filterMode = FilterMode.Bilinear;
        return t;
    }

    static Texture2D PaintChevron()
    {
        var p = new EndingPainter(28, 48, new Color(0, 0, 0, 0));
        p.Polygon(new[] { new Vector2(2, 2), new Vector2(26, 24), new Vector2(2, 46), new Vector2(10, 24) }, Color.white);
        return p.ToTexture();
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static Image Box(RectTransform parent, string name, Vector2 pos, Vector2 size)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.raycastTarget = false;
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = size;
        return img;
    }

    static Text Label(RectTransform parent, Font font, int size, string s, Vector2 pos, Vector2 box, TextAnchor align)
    {
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = font; t.fontSize = size; t.text = s; t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = box;
        return t;
    }
}
