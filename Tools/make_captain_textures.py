"""Brings the Captain's outfit into the project and dyes it. Owner: Samprity.

    python -I Tools/make_captain_textures.py "<unzipped Modular Character Outfits - Fantasy[Standard] folder>"

Takes the free Ranger outfit from Quaternius' CC0 Modular Character Outfits - Fantasy pack: copies Male_Ranger.fbx
into Art/ThirdParty/Quaternius/Outfits and writes 1024 px textures beside the crew's (the same downscale as the rest
of the Quaternius textures). The base colour is re-dyed into a ship's captain's coat: the green cloth turns deep
navy, the steel buckles and studs turn brass, the leather stays brown.
"""
import colorsys
import os
import shutil
import sys
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
Q = os.path.join(ROOT, "Assets", "_Project", "Art", "ThirdParty", "Quaternius")
SIZE = 1024

NAVY_HUE = 222 / 360
BRASS_HUE = 40 / 360


def dye(rgb):
    r, g, b = (c / 255 for c in rgb)
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    if 0.17 < h < 0.48 and s > 0.22:
        # Green cloth: navy wool. Keep the folds (value) but pull it darker and calmer.
        h, s, v = NAVY_HUE, min(0.62, 0.35 + s * 0.4), v * 0.62 + 0.02
    elif s < 0.16 and v > 0.55:
        # Pale steel: polished brass.
        h, s, v = BRASS_HUE, 0.58, min(1.0, v * 0.92)
    r, g, b = colorsys.hsv_to_rgb(h, s, v)
    return int(r * 255 + 0.5), int(g * 255 + 0.5), int(b * 255 + 0.5)


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    pack = sys.argv[1]
    fbx = os.path.join(pack, "Exports", "FBX (Unity)", "Outfits", "Male_Ranger.fbx")
    tex = os.path.join(pack, "Textures", "Ranger")
    shutil.copyfile(fbx, os.path.join(Q, "Outfits", "Male_Ranger.fbx"))

    base = Image.open(os.path.join(tex, "T_Ranger_BaseColor.png")).convert("RGB").resize((SIZE, SIZE), Image.LANCZOS)
    lut = {}
    px = base.load()
    for y in range(SIZE):
        for x in range(SIZE):
            c = px[x, y]
            if c not in lut:
                lut[c] = dye(c)
            px[x, y] = lut[c]
    base.save(os.path.join(Q, "Textures", "T_Captain_BaseColor.png"), optimize=True)

    normal = Image.open(os.path.join(tex, "T_Ranger_Normal.png")).convert("RGB").resize((SIZE, SIZE), Image.LANCZOS)
    normal.save(os.path.join(Q, "Textures", "T_Ranger_Normal.png"), optimize=True)
    print("Captain outfit and textures written to", os.path.normpath(Q))


if __name__ == "__main__":
    main()
