#!/usr/bin/env python3
"""Draws the Stormglass Conclave's glass mast and a sealed storm vial (Textures/ColonyCouncil/Things/StormglassMast.png,
128x256, and StormVial.png, 128x128, for Defs/Misc/Rimfolk_PeopleEvents6_Misc.xml) in the game's manner, to go with
their rod (StormglassRod.png): flat fills with a little shading under a dark outline, seen from a little above. The mast
is a brass pole on a three-legged footing with a pointed tip, two crossbars, and four glass vessels of grey-blue storm
water hung from them on brass caps. The vial is a small stoppered glass of the same water with a brass cap and a paper
label tied round its neck. Drawn at four times the size and scaled down for smooth edges.
Run: python3 Tools/make_storm_glass.py"""
import os
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
S = 4
OUT = os.path.join(ROOT, "Textures", "ColonyCouncil", "Things")

LINE = (34, 28, 22, 255)
BRASS = (186, 146, 64, 255)
BRASS_DARK = (138, 102, 38, 255)
BRASS_LIGHT = (226, 192, 112, 255)
GLASS = (156, 188, 206, 255)
GLASS_DARK = (110, 140, 162, 255)
GLASS_LIGHT = (222, 238, 246, 255)
WATER = (92, 118, 140, 255)
PAPER = (222, 210, 178, 255)
CORD = (120, 96, 70, 255)
OL = 8


def poly(d, pts, fill, width=OL):
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=LINE, width=width, joint="curve")


def shadow(img, box):
    sh = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse(box, fill=(0, 0, 0, 80))
    return Image.alpha_composite(img, sh.filter(ImageFilter.GaussianBlur(14)))


def vessel(d, cx, top, w, h):
    """A glass vessel hung from a brass cap: the cap, the glass, the water in its lower half, a highlight."""
    poly(d, [(cx - w // 2 - 6, top), (cx + w // 2 + 6, top), (cx + w // 2 + 6, top + 22), (cx - w // 2 - 6, top + 22)], BRASS, 6)
    glass = [(cx - w // 2, top + 22), (cx + w // 2, top + 22), (cx + w // 2, top + h), (cx - w // 2, top + h)]
    poly(d, glass, GLASS, 6)
    d.rectangle((cx - w // 2 + 4, top + 22 + h // 2, cx + w // 2 - 4, top + h - 4), fill=WATER)
    d.line([(cx - w // 2 + 4, top + h - 4), (cx + w // 2 - 4, top + h - 4)], fill=GLASS_DARK, width=4)
    d.line([(cx - w // 2 + 10, top + 34), (cx - w // 2 + 10, top + h - 20)], fill=GLASS_LIGHT, width=6)
    d.line(glass + [glass[0]], fill=LINE, width=6, joint="curve")


def mast():
    W, H = 128 * S, 256 * S
    img = shadow(Image.new("RGBA", (W, H), (0, 0, 0, 0)), (80, 920, W - 80, 1010))
    d = ImageDraw.Draw(img)
    # The three-legged footing.
    for foot in [(120, 980), (392, 980), (256, 1000)]:
        d.line([(256, 860), foot], fill=LINE, width=22)
        d.line([(256, 860), foot], fill=BRASS_DARK, width=12)
    # The pole, its lit and shaded sides, and the pointed tip.
    poly(d, [(240, 130), (272, 130), (276, 900), (236, 900)], BRASS)
    d.polygon([(258, 136), (268, 136), (272, 894), (258, 894)], fill=BRASS_DARK)
    d.line([(246, 150), (244, 880)], fill=BRASS_LIGHT, width=5)
    poly(d, [(256, 40), (276, 132), (236, 132)], BRASS_LIGHT, 6)
    # A coil wound round the pole under the tip.
    for y in range(160, 260, 18):
        d.arc((226, y, 286, y + 22), 0, 180, fill=LINE, width=8)
        d.arc((226, y, 286, y + 22), 0, 180, fill=BRASS_LIGHT, width=3)
    # Two crossbars, and the vessels hung from them.
    for y, half in [(320, 150), (560, 120)]:
        poly(d, [(256 - half, y), (256 + half, y), (256 + half, y + 22), (256 - half, y + 22)], BRASS)
        d.line([(256 - half + 6, y + 6), (256 + half - 6, y + 6)], fill=BRASS_LIGHT, width=4)
    for cx, top, w, h in [(126, 342, 64, 150), (386, 342, 64, 150), (156, 582, 56, 130), (356, 582, 56, 130)]:
        d.line([(cx, top - 20), (cx, top)], fill=LINE, width=6)
        vessel(d, cx, top, w, h)
    return img


def vial():
    W = H = 128 * S
    img = shadow(Image.new("RGBA", (W, H), (0, 0, 0, 0)), (150, 400, W - 150, 470))
    d = ImageDraw.Draw(img)
    # The glass body, the water in it, a highlight.
    body = [(196, 190), (316, 190), (330, 230), (330, 420), (316, 440), (196, 440), (182, 420), (182, 230)]
    poly(d, body, GLASS)
    d.polygon([(190, 300), (322, 300), (322, 420), (310, 434), (202, 434), (190, 420)], fill=WATER)
    d.line([(204, 236), (204, 400)], fill=GLASS_LIGHT, width=10)
    d.line(body + [body[0]], fill=LINE, width=OL, joint="curve")
    # The neck and the brass cap.
    poly(d, [(222, 140), (290, 140), (290, 192), (222, 192)], GLASS, 6)
    poly(d, [(208, 92), (304, 92), (304, 146), (208, 146)], BRASS)
    d.line([(218, 104), (294, 104)], fill=BRASS_LIGHT, width=6)
    d.line([(208, 132), (304, 132)], fill=BRASS_DARK, width=6)
    # A paper label on a cord round the neck.
    d.line([(222, 168), (290, 168)], fill=CORD, width=6)
    poly(d, [(286, 170), (356, 200), (338, 256), (268, 226)], PAPER, 6)
    d.line([(290, 206), (334, 224)], fill=CORD, width=4)
    d.line([(284, 222), (318, 236)], fill=CORD, width=4)
    return img


def save(img, name):
    img = img.resize((img.width // S, img.height // S), Image.LANCZOS)
    img.save(os.path.join(OUT, name))
    print("wrote", name)


if __name__ == "__main__":
    save(mast(), "StormglassMast.png")
    save(vial(), "StormVial.png")
