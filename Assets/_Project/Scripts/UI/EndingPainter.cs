using UnityEngine;

/// <summary>
/// A small CPU painter for the ending screen's artwork: anti-aliased circles, rings, arcs, thick lines, polygons and
/// rounded cards drawn once into a texture (coordinates top-left, like the approved mockup), with text laid over it
/// in uGUI. Owner: Anadi.
/// </summary>
public class EndingPainter
{
    public readonly int W, H;
    readonly Color[] px;

    public EndingPainter(int w, int h, Color clear)
    {
        W = w; H = h;
        px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = clear;
    }

    public Texture2D ToTexture()
    {
        var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    void Blend(int x, int y, Color c, float a)
    {
        if (x < 0 || y < 0 || x >= W || y >= H || a <= 0f) return;
        int i = (H - 1 - y) * W + x;
        a *= c.a;
        var d = px[i];
        float outA = a + d.a * (1f - a);
        if (outA <= 0f) return;
        px[i] = new Color((c.r * a + d.r * d.a * (1f - a)) / outA, (c.g * a + d.g * d.a * (1f - a)) / outA,
                          (c.b * a + d.b * d.a * (1f - a)) / outA, outA);
    }

    static float Cover(float signedDist, float soft = 1f) => Mathf.Clamp01(0.5f - signedDist / soft);

    // ---------- shapes ----------

    public void VerticalGradient(Color top, Color bottom)
    {
        for (int y = 0; y < H; y++)
        {
            var c = Color.Lerp(top, bottom, y / (float)H);
            for (int x = 0; x < W; x++) px[(H - 1 - y) * W + x] = c;
        }
    }

    public void Circle(float cx, float cy, float r, Color c)
    {
        int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1), y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + r + 1);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            Blend(x, y, c, Cover(Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) - r));
    }

    public void Ellipse(float cx, float cy, float rx, float ry, Color c)
    {
        int x0 = Mathf.FloorToInt(cx - rx - 1), x1 = Mathf.CeilToInt(cx + rx + 1), y0 = Mathf.FloorToInt(cy - ry - 1), y1 = Mathf.CeilToInt(cy + ry + 1);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
            float k = Mathf.Sqrt(dx * dx + dy * dy);
            Blend(x, y, c, Cover((k - 1f) * Mathf.Min(rx, ry)));
        }
    }

    public void Ring(float cx, float cy, float r, float width, Color c) => Arc(cx, cy, r, width, 0f, 360f, c);

    /// <summary>Angles in degrees, 0 = right, growing clockwise on screen (y down), like the mockup.</summary>
    public void Arc(float cx, float cy, float r, float width, float from, float to, Color c)
    {
        float half = width * 0.5f;
        int x0 = Mathf.FloorToInt(cx - r - half - 1), x1 = Mathf.CeilToInt(cx + r + half + 1);
        int y0 = Mathf.FloorToInt(cy - r - half - 1), y1 = Mathf.CeilToInt(cy + r + half + 1);
        bool full = to - from >= 359.9f;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
            float a = Cover(Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r) - half);
            if (a <= 0f) continue;
            if (!full)
            {
                float ang = Mathf.Repeat(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg - from, 360f);
                if (ang > to - from) continue;
            }
            Blend(x, y, c, a);
        }
    }

    public void Line(Vector2 a, Vector2 b, float width, Color c)
    {
        float half = width * 0.5f;
        int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - half - 1), x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + half + 1);
        int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - half - 1), y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + half + 1);
        Vector2 ab = b - a;
        float len2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            Blend(x, y, c, Cover(Vector2.Distance(p, a + ab * t) - half));
        }
    }

    public void Polygon(Vector2[] pts, Color c)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in pts) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
        for (int y = Mathf.FloorToInt(minY) - 1; y <= Mathf.CeilToInt(maxY) + 1; y++)
        for (int x = Mathf.FloorToInt(minX) - 1; x <= Mathf.CeilToInt(maxX) + 1; x++)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            bool inside = false;
            float edge = float.MaxValue;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                var pi = pts[i]; var pj = pts[j];
                if ((pi.y > p.y) != (pj.y > p.y) && p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y) + pi.x) inside = !inside;
                var e = pj - pi;
                float t = Mathf.Clamp01(Vector2.Dot(p - pi, e) / Mathf.Max(1e-4f, e.sqrMagnitude));
                edge = Mathf.Min(edge, Vector2.Distance(p, pi + e * t));
            }
            Blend(x, y, c, Cover(inside ? -edge : edge));
        }
    }

    /// <summary>A rounded card; soft &gt; 1 blurs the edge (for drop shadows).</summary>
    public void RoundRect(float x0, float y0, float x1, float y1, float r, Color c, float soft = 1f)
    {
        float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f, hx = (x1 - x0) * 0.5f, hy = (y1 - y0) * 0.5f;
        int pad = Mathf.CeilToInt(soft) + 1;
        for (int y = Mathf.FloorToInt(y0) - pad; y <= Mathf.CeilToInt(y1) + pad; y++)
        for (int x = Mathf.FloorToInt(x0) - pad; x <= Mathf.CeilToInt(x1) + pad; x++)
        {
            float qx = Mathf.Abs(x + 0.5f - cx) - hx + r, qy = Mathf.Abs(y + 0.5f - cy) - hy + r;
            float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
            Blend(x, y, c, Cover(d, soft));
        }
    }

    public void RoundRectOutline(float x0, float y0, float x1, float y1, float r, float width, Color c)
    {
        float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f, hx = (x1 - x0) * 0.5f, hy = (y1 - y0) * 0.5f;
        for (int y = Mathf.FloorToInt(y0) - 2; y <= Mathf.CeilToInt(y1) + 2; y++)
        for (int x = Mathf.FloorToInt(x0) - 2; x <= Mathf.CeilToInt(x1) + 2; x++)
        {
            float qx = Mathf.Abs(x + 0.5f - cx) - hx + r, qy = Mathf.Abs(y + 0.5f - cy) - hy + r;
            float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
            Blend(x, y, c, Cover(Mathf.Abs(d + width * 0.5f) - width * 0.5f));
        }
    }

    /// <summary>Fills from a curve y(x) down to the bottom of the picture (the sea's wave rows).</summary>
    public void FillBelow(System.Func<float, float> yOf, Color c)
    {
        for (int x = 0; x < W; x++)
        {
            float top = yOf(x + 0.5f);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(top));
            for (int y = y0; y < H; y++) Blend(x, y, c, Mathf.Clamp01(y + 1f - top));
        }
    }
}
