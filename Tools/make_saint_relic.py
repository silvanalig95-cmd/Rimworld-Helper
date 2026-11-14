#!/usr/bin/env python3
"""Draws a saint's relic (Textures/ColonyCouncil/Things/SaintRelic.png, 128x128) in the game's manner: flat fills with a
little shading under a dark outline, seen from a little above. A small carved wooden casket with a brass band and a round
window of glass in its lid, a lock of hair coiled behind the glass, and a knotted cord tied round it; no holy sign of any
real faith. Drawn at four times the size and scaled down for smooth edges. Run: python3 Tools/make_saint_relic.py"""
import os
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
S = 4
W = 128 * S
OUT = os.path.join(ROOT, "Textures", "ColonyCouncil", "Things", "SaintRelic.png")

LINE = (38, 26, 17, 255)
WOOD = (128, 82, 48, 255)
WOOD_DARK = (92, 58, 33, 255)
WOOD_LIGHT = (160, 110, 68, 255)
BRASS = (214, 170, 72, 255)
BRASS_DARK = (150, 112, 40, 255)
GLASS = (196, 214, 210, 255)
GLASS_LIGHT = (232, 242, 238, 255)
HAIR = (120, 78, 42, 255)
HAIR_LIGHT = (164, 112, 64, 255)
CORD = (176, 52, 44, 255)
CORD_DARK = (126, 34, 30, 255)
OL = 7


def S4(*v):
    return [x * S for x in v]


def poly(d, pts, fill, width=OL):
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=LINE, width=width, joint="curve")


def main():
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # The casket's front and lid, seen from a little above.
    front = [(22 * S, 62 * S), (106 * S, 62 * S), (102 * S, 104 * S), (26 * S, 104 * S)]
    poly(d, front, WOOD)
    d.polygon([(26 * S, 92 * S), (102 * S, 92 * S), (102 * S, 104 * S), (26 * S, 104 * S)], fill=WOOD_DARK)
    d.line(front + [front[0]], fill=LINE, width=OL, joint="curve")
    lid = [(18 * S, 40 * S), (110 * S, 40 * S), (106 * S, 62 * S), (22 * S, 62 * S)]
    poly(d, lid, WOOD_LIGHT)
    # Carved lines on the front.
    for x in (40, 64, 88):
        d.line([(x * S, 70 * S), (x * S, 98 * S)], fill=WOOD_DARK, width=4 * S)
    # The brass band across the lid's edge and a clasp.
    d.rectangle(S4(22, 58, 106, 66), fill=BRASS, outline=LINE, width=OL)
    d.line([(24 * S, 64 * S), (104 * S, 64 * S)], fill=BRASS_DARK, width=2 * S)
    d.rounded_rectangle(S4(58, 62, 70, 78), 2 * S, fill=BRASS, outline=LINE, width=OL)
    # The round window in the lid, a lock of hair coiled behind it.
    d.ellipse(S4(44, 36, 84, 60), fill=BRASS, outline=LINE, width=OL)
    d.ellipse(S4(49, 39, 79, 57), fill=GLASS, outline=BRASS_DARK, width=2 * S)
    for i, (a, b) in enumerate([(200, 520), (230, 560), (260, 600)]):
        r = 3 + i * 3
        d.arc(S4(64 - r, 48 - r * 0.6, 64 + r, 48 + r * 0.6), a, b, fill=HAIR if i % 2 == 0 else HAIR_LIGHT, width=3 * S)
    d.line([(54 * S, 43 * S), (60 * S, 41 * S)], fill=GLASS_LIGHT, width=2 * S)
    # A knotted cord tied round the casket, its knot at the clasp and two ends hanging down the front.
    for y in (82,):
        d.line([(24 * S, y * S), (104 * S, y * S)], fill=LINE, width=7 * S)
        d.line([(24 * S, y * S), (104 * S, y * S)], fill=CORD, width=4 * S)
    d.ellipse(S4(58, 76, 70, 88), fill=CORD_DARK, outline=LINE, width=OL)
    for x1 in (54, 74):
        d.line([(64 * S, 86 * S), (x1 * S, 112 * S)], fill=LINE, width=7 * S)
        d.line([(64 * S, 86 * S), (x1 * S, 112 * S)], fill=CORD, width=4 * S)
        d.ellipse(S4(x1 - 4, 110, x1 + 4, 118), fill=CORD_DARK, outline=LINE, width=4)
    img = img.resize((128, 128), Image.LANCZOS)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img.save(OUT)
    print("wrote", os.path.relpath(OUT, ROOT))


if __name__ == "__main__":
    main()
