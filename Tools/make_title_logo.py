"""Renders the title logotype for The Promised Port. Owner: Samprity.

    python -I Tools/make_title_logo.py <path to Cinzel[wght].ttf> [preview.png]

A lockup in the spirit of classic adventure-game titles, kept plainly readable: a small spaced "THE", PROMISED set
large with a taller first letter, and PORT right-aligned beneath it after a brass rule and a diamond. Ivory letters
with a soft top-to-bottom gradient, a thin dark outline and a drop shadow, so it reads over any part of the scene.
Cinzel is by Natanael Gama, SIL Open Font License (a copy sits next to the PNG).
Writes Assets/_Project/Art/UI/Title/TitleLogo.png.
"""
import os
import sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
OUT = os.path.join(ROOT, "Assets", "_Project", "Art", "UI", "Title", "TitleLogo.png")

W, H = 2000, 900
IVORY_TOP, IVORY_BOTTOM = (252, 246, 232), (226, 211, 180)
OUTLINE = (24, 18, 14)
BRASS = (205, 165, 92)


def font(path, size, weight):
    f = ImageFont.truetype(path, size)
    try:
        f.set_variation_by_axes([weight])
    except Exception:
        pass  # a static file: use as is
    return f


def word(draw, x, baseline, text, f_big, f_first, tracking):
    """Draws a word into a mask with a taller first letter sharing the baseline. Returns the end x."""
    for i, ch in enumerate(text):
        f = f_first if i == 0 else f_big
        ascent = f.getmetrics()[0]
        draw.text((x, baseline - ascent), ch, font=f, fill=255)
        x += f.getlength(ch) + tracking
    return x - tracking


def measure(text, f_big, f_first, tracking):
    return sum((f_first if i == 0 else f_big).getlength(ch) for i, ch in enumerate(text)) + tracking * (len(text) - 1)


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    path = sys.argv[1]
    black = lambda s: font(path, s, 900)
    bold = lambda s: font(path, s, 700)

    mask = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(mask)

    left = 60
    # PROMISED: big, with a taller P.
    big, first = black(250), black(330)
    promised_base = 520
    promised_end = word(d, left, promised_base, "PROMISED", big, first, 8)

    # THE: small and spaced, sitting over PROMISED after its tall P.
    the = bold(96)
    the_x = left + first.getlength("P") + 26
    word(d, the_x, promised_base - 250 + 8, "THE", the, the, 34)

    # PORT: right-aligned under PROMISED, its own taller P.
    pbig, pfirst = black(205), black(265)
    port_w = measure("PORT", pbig, pfirst, 10)
    port_x = promised_end - port_w
    port_base = promised_base + 255
    word(d, port_x, port_base, "PORT", pbig, pfirst, 10)

    # Letters: ivory gradient inside, dark outline round them, shadow under everything.
    grad = Image.new("RGB", (W, H))
    gd = ImageDraw.Draw(grad)
    for y in range(H):
        t = y / (H - 1)
        gd.line([(0, y), (W, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(IVORY_TOP, IVORY_BOTTOM)))
    outline = mask.filter(ImageFilter.MaxFilter(9))

    logo = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    shadow = outline.filter(ImageFilter.GaussianBlur(14)).point(lambda v: int(v * 0.55))
    shadow_layer = Image.new("RGBA", (W, H), (0, 0, 0, 255))
    logo.paste(shadow_layer, (8, 14), shadow)
    logo.paste(Image.new("RGBA", (W, H), OUTLINE + (255,)), (0, 0), outline)
    logo.paste(grad.convert("RGBA"), (0, 0), mask)

    # The brass rule and diamond leading into PORT, outlined like the letters.
    orn = Image.new("L", (W, H), 0)
    od = ImageDraw.Draw(orn)
    rule_y = port_base - 92
    rule_x0, rule_x1 = left + 40, port_x - 70
    od.rectangle([rule_x0, rule_y - 4, rule_x1 - 30, rule_y + 4], fill=255)
    cx = rule_x1 - 4
    od.polygon([(cx - 26, rule_y), (cx, rule_y - 26), (cx + 26, rule_y), (cx, rule_y + 26)], fill=255)
    # A small diamond at the far end of the rule too.
    od.polygon([(rule_x0 - 14, rule_y), (rule_x0, rule_y - 14), (rule_x0 + 14, rule_y), (rule_x0, rule_y + 14)], fill=255)
    orn_outline = orn.filter(ImageFilter.MaxFilter(7))
    logo.paste(Image.new("RGBA", (W, H), OUTLINE + (255,)), (0, 0), orn_outline)
    logo.paste(Image.new("RGBA", (W, H), BRASS + (255,)), (0, 0), orn)

    logo = logo.crop(logo.getbbox())
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    logo.save(OUT)
    print("wrote", os.path.normpath(OUT), logo.size)

    if len(sys.argv) > 2:
        # Preview over a mid-dark busy background, to judge readability.
        bg = Image.new("RGB", (logo.width + 160, logo.height + 160), (58, 66, 74))
        bd = ImageDraw.Draw(bg)
        for i in range(0, bg.width, 40):
            bd.line([(i, 0), (i - 300, bg.height)], fill=(96, 84, 70), width=14)
        bg.paste(logo, (80, 80), logo)
        bg.save(sys.argv[2])


if __name__ == "__main__":
    main()
