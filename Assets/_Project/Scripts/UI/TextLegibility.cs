using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps light text readable over a moving, bright film (playtest: "the Captain's lines blend into the background").
/// Three layers: a soft dark backing behind the line, a thin dark outline round every letter, and a drop shadow.
/// Used by the opening and ending cinematics. Owner: Samprity.
/// </summary>
public static class TextLegibility
{
    static Texture2D softTex;

    /// <summary>A dark outline and a drop shadow on the text itself.</summary>
    public static void Ink(Text t, float outline = 1.8f, float shadowAlpha = 0.75f)
    {
        if (t == null) return;
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0.02f, 0.02f, 0.04f, 0.9f);
        o.effectDistance = new Vector2(outline, -outline);
        o.useGraphicAlpha = true;
        var s = t.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, shadowAlpha);
        s.effectDistance = new Vector2(3f, -3f);
        s.useGraphicAlpha = true;
    }

    /// <summary>
    /// A soft-edged dark band put behind the text (first in its parent), so it fades in and out with the text's group.
    /// Call Fit after the text changes to size it to the words.
    /// </summary>
    public static RawImage Backing(Text t, float alpha = 0.55f)
    {
        if (t == null) return null;
        var img = new GameObject("Backing", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        img.transform.SetParent(t.transform.parent, false);
        img.transform.SetAsFirstSibling(); // under everything else in the group (the speaker's name and icon too)
        img.texture = Soft();
        img.color = new Color(0.01f, 0.015f, 0.03f, alpha);
        img.raycastTarget = false;
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        Fit(img, t);
        return img;
    }

    /// <summary>Sizes and places the backing round the text as it now reads (blank text hides it).</summary>
    /// <param name="extraTop">More room above the words, e.g. for a speaker's name over the line.</param>
    public static void Fit(RawImage backing, Text t, float padX = 90f, float padY = 34f, float extraTop = 0f)
    {
        if (backing == null || t == null) return;
        backing.enabled = !string.IsNullOrEmpty(t.text);
        if (!backing.enabled) return;
        var tr = t.rectTransform;
        float maxW = tr.rect.width > 1f ? tr.rect.width : 1600f;
        float w = Mathf.Min(t.preferredWidth, maxW);
        var gen = t.cachedTextGeneratorForLayout;
        var settings = t.GetGenerationSettings(new Vector2(maxW, 0f));
        float h = gen.GetPreferredHeight(t.text, settings) / t.pixelsPerUnit;

        // The text's visible block, in the parent's space: where its anchor and alignment put it.
        var parent = (RectTransform)tr.parent;
        Vector3 local = parent.InverseTransformPoint(tr.TransformPoint(tr.rect.center));
        float cx = local.x, cy = local.y;
        var a = t.alignment;
        bool top = a == TextAnchor.UpperLeft || a == TextAnchor.UpperCenter || a == TextAnchor.UpperRight;
        bool bottom = a == TextAnchor.LowerLeft || a == TextAnchor.LowerCenter || a == TextAnchor.LowerRight;
        if (top) cy = local.y + tr.rect.height * 0.5f - h * 0.5f;
        else if (bottom) cy = local.y - tr.rect.height * 0.5f + h * 0.5f;

        // Anchored at the parent's pivot, which is also the origin of the parent's local space.
        var r = backing.rectTransform;
        r.anchorMin = r.anchorMax = parent.pivot;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(cx, cy + extraTop * 0.5f);
        r.sizeDelta = new Vector2(w + padX * 2f, h + padY * 2f + extraTop);
    }

    /// <summary>White in the middle, fading to clear at every edge: a soft pill once stretched.</summary>
    static Texture2D Soft()
    {
        if (softTex != null) return softTex;
        const int W = 128, H = 64;
        softTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SoftBacking" };
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float u = Mathf.Abs(x / (W - 1f) * 2f - 1f), v = Mathf.Abs(y / (H - 1f) * 2f - 1f);
            float a = (1f - Mathf.SmoothStep(0.55f, 1f, u)) * (1f - Mathf.SmoothStep(0.35f, 1f, v));
            softTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        softTex.Apply();
        return softTex;
    }
}
