#!/usr/bin/env python3
"""
Draws the emblems and world map town icons of Rimfolk's own peoples (Defs/PeopleDefs/): white glyphs on transparent,
128x128 RGBA, tinted in game by the people's colour as the game tints its own faction icons and settlements.

Each glyph is drawn as a mask at 4x (512x512) and scaled down, with the shape helpers of Tools/make_icons.py, so the
emblems match the interface icons' clean, bold edges. Shapes are bold and their cutouts wide, so they still read at the
faction list's size (about 40 px) and on the world map.

  - the road league: a pair of scales hung from a waystone (emblem); a walled waystation with its arched gate and two
    towers (town);
  - the hospitaller order: an open hand with a lamp's flame in its palm (emblem); a long sickhouse under a gabled roof
    with a lantern tower (town);
  - the herding folk: a shepherd's crook across a herder's horn (emblem); a round felt house between two tents (town).

Release B (peoples that arise during play) adds a people's glyphs here: a function for each and a line in GLYPHS.

Usage (from the repository root):
    python3 Tools/make_emblems.py            # writes Textures/ColonyCouncil/Peoples/*.png and Tools/emblems_preview.png
    python3 Tools/make_emblems.py --preview  # only the contact sheet

Needs Pillow (pip install pillow).
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_icons import arc, bezier, circle, ellipse, grow, line, minus, over, poly, rect, union  # noqa: E402

S = 512  # drawing size (4x)
OUT = 128  # texture size
ON, OFF = 255, 0
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Textures", "ColonyCouncil", "Peoples")
PREVIEW = os.path.join(ROOT, "Tools", "emblems_preview.png")


def new():
    return Image.new("L", (S, S), 0)


def teardrop(cx, top, bottom, half_width):
    """A flame: pointed at the top, round at the bottom."""
    r = half_width
    cy = bottom - r
    left = bezier((cx, top), (cx - r * 0.25, top + (cy - top) * 0.45), (cx - r * 1.05, cy - r * 0.55), (cx - r, cy))
    right = bezier((cx + r, cy), (cx + r * 1.05, cy - r * 0.55), (cx + r * 0.25, top + (cy - top) * 0.45), (cx, top))
    bottom_arc = [(cx + r * math.cos(math.radians(a)), cy + r * math.sin(math.radians(a))) for a in range(180, -1, -10)]
    return left + bottom_arc[1:] + right[1:]


# ---------------------------------------------------------------- the road league

def g_league():
    """A pair of scales hung from a waystone: the road and the fair price."""
    stone = new()
    # The waystone: a tapered post on a broad footing stone, a round head on top.
    poly(stone, [(232, 128), (280, 128), (290, 410), (222, 410)])
    circle(stone, 256, 108, 34)
    poly(stone, [(176, 404), (336, 404), (372, 470), (140, 470)])
    # A notch cut across the footing stone, the road's mark.
    rect(stone, 236, 430, 276, 470, fill=OFF)
    scales = new()
    # The beam, a little bowed, with its ends turned down.
    beam = bezier((84, 176), (180, 150), (332, 150), (428, 176))
    line(scales, beam, 26)
    for x in (100, 412):
        # Each pan hangs by two cords that meet at the beam's end.
        line(scales, [(x, 182), (x - 62, 316)], 18)
        line(scales, [(x, 182), (x + 62, 316)], 18)
        # The pan: a shallow bowl.
        bowl = new()
        ellipse(bowl, x - 84, 282, x + 84, 372)
        rect(bowl, x - 90, 270, x + 90, 318, fill=OFF)
        scales = union(scales, bowl)
    return over(scales, stone, 12)


def g_league_town():
    """A waystation: a walled yard with an arched gate between two towers, a pennant on one."""
    m = new()
    rect(m, 120, 250, 392, 440)  # the yard wall
    for x0 in (52, 372):  # the towers
        rect(m, x0, 168, x0 + 88, 440)
        for k in range(3):  # their battlements
            rect(m, x0 + k * 34, 140, x0 + k * 34 + 20, 172)
    # Battlements along the wall.
    for x in range(140, 380, 44):
        rect(m, x, 226, x + 24, 252)
    # The arched gate, wide enough for a cart.
    rect(m, 206, 330, 306, 440, fill=OFF)
    circle(m, 256, 330, 50, fill=OFF)
    # Arrow slits in the towers.
    for cx in (96, 416):
        rect(m, cx - 8, 228, cx + 8, 290, r=6, fill=OFF)
    # The pennant on the left tower.
    line(m, [(96, 140), (96, 52)], 14, caps=False)
    poly(m, [(102, 52), (178, 76), (102, 100)])
    return m


# ---------------------------------------------------------------- the hospitaller order

def g_order():
    """An open hand with a lamp's flame in its palm: the order's care, given freely."""
    m = new()
    # The palm.
    rect(m, 142, 236, 370, 462, r=86)
    # Four fingers, the middle ones longest.
    for cx, top in ((168, 128), (226, 84), (286, 96), (344, 150)):
        rect(m, cx - 27, top, cx + 27, 300, r=27)
    # The thumb, out to the side.
    line(m, [(176, 392), (92, 268)], 58)
    # The gaps between the fingers, so they read as fingers at a small size.
    for x in (197, 256, 315):
        line(m, [(x, 150), (x, 238)], 10, fill=OFF, caps=False)
    # The lamp's flame cut out of the palm, a tongue of it left standing inside, and the lamp's bowl under it.
    poly(m, teardrop(256, 250, 394, 46), fill=OFF)
    poly(m, teardrop(256, 314, 380, 18))
    rect(m, 200, 404, 312, 430, r=12, fill=OFF)
    return m


def g_order_town():
    """A sickhouse: a long hall under a gabled roof, a lantern in its little tower, a wide door."""
    m = new()
    rect(m, 78, 270, 434, 444)  # the hall
    poly(m, [(44, 284), (256, 168), (468, 284)])  # the roof
    rect(m, 220, 82, 292, 214)  # the tower
    poly(m, [(206, 92), (256, 28), (306, 92)])  # its cap
    # The lantern's light in the tower.
    poly(m, teardrop(256, 112, 192, 20), fill=OFF)
    # The door and the windows of the ward.
    rect(m, 224, 352, 288, 444, fill=OFF)
    circle(m, 256, 352, 32, fill=OFF)
    for x in (112, 168, 324, 380):
        rect(m, x, 318, x + 28, 384, r=8, fill=OFF)
    return m


# ---------------------------------------------------------------- the herding folk

def g_herders():
    """A shepherd's crook across a herder's horn: the herd led, and called home."""
    horn = new()
    # A curved horn, narrow at the tip on the upper left and swelling to a wide bell on the right: discs along a curve,
    # each a little wider than the last.
    path = bezier((72, 220), (110, 400), (300, 450), (420, 360), 60)
    for i, (x, y) in enumerate(path):
        t = i / (len(path) - 1)
        circle(horn, x, y, 9 + 46 * t ** 1.4)
    # The bell's mouth, open.
    ellipse(horn, 396, 286, 474, 420)
    ellipse(horn, 424, 318, 462, 390, fill=OFF)
    # Two bands round the horn, cut in.
    line(horn, [(196, 346), (228, 410)], 14, fill=OFF, caps=False)
    line(horn, [(300, 360), (320, 438)], 14, fill=OFF, caps=False)
    crook = new()
    # The staff, and the hook curled over at its top.
    line(crook, [(316, 476), (316, 152)], 34)
    arc(crook, 252, 152, 64, 180, 360, 34)
    line(crook, [(188, 152), (188, 206)], 34)
    return over(crook, horn, 14)


def g_herders_town():
    """A herders' camp: a round felt house between two tents, smoke from its roof ring."""
    m = new()
    # The round house: a dome on a low wall, its door cut in.
    ellipse(m, 152, 214, 360, 380)
    rect(m, 152, 296, 360, 440)
    rect(m, 228, 352, 284, 440, r=10, fill=OFF)
    rect(m, 244, 196, 268, 226)  # the roof ring
    # The tents either side, each with its door flap open.
    for cx in (84, 428):
        poly(m, [(cx - 76, 440), (cx, 286), (cx + 76, 440)])
        poly(m, [(cx - 20, 440), (cx, 392), (cx + 20, 440)], fill=OFF)
    # A thread of smoke.
    line(m, bezier((256, 186), (232, 150), (282, 120), (258, 72)), 14)
    return m


GLYPHS = [
    ("League", g_league), ("LeagueTown", g_league_town),
    ("Order", g_order), ("OrderTown", g_order_town),
    ("Herders", g_herders), ("HerdersTown", g_herders_town),
]

# The peoples' colours (the middle of each colorSpectrum in Defs/PeopleDefs/), for the preview.
TINTS = {"League": (66, 158, 166), "Order": (234, 228, 210), "Herders": (196, 118, 66)}


def to_texture(mask):
    small = mask.resize((OUT, OUT), Image.LANCZOS)
    white = Image.new("L", (OUT, OUT), 255)
    return Image.merge("RGBA", (white, white, white, small))


def tinted(tex, size, tint):
    small = tex.resize((size, size), Image.LANCZOS)
    r, g, b, a = small.split()
    return Image.merge("RGBA", (r.point(lambda v: v * tint[0] // 255), g.point(lambda v: v * tint[1] // 255),
                                b.point(lambda v: v * tint[2] // 255), a))


def contact_sheet(textures):
    cols = 2
    cell_w, cell_h = 330, 190
    rows = (len(textures) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cell_w + 20, rows * cell_h + 56), (33, 36, 40, 255))
    d = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.load_default(size=13)
        title = ImageFont.load_default(size=16)
    except TypeError:
        font = title = ImageFont.load_default()
    d.text((12, 12), "Rimfolk's peoples: emblems and town icons (128 px white, then tinted at 64, 40 and 24 px)", fill=(230, 230, 230), font=title)
    for i, (name, tex) in enumerate(textures):
        cx = 10 + (i % cols) * cell_w
        cy = 44 + (i // cols) * cell_h
        d.rounded_rectangle([cx + 2, cy + 2, cx + cell_w - 6, cy + cell_h - 6], radius=6, fill=(44, 48, 53, 255))
        sheet.alpha_composite(tex, (cx + 10, cy + 12))
        tint = TINTS[name.replace("Town", "")]
        x = cx + 148
        for size in (64, 40, 24):
            sheet.alpha_composite(tinted(tex, size, tint), (x, cy + 76 - size // 2))
            x += size + 12
        d.text((cx + 10, cy + 150), name, fill=(220, 220, 220), font=font)
    return sheet


def main():
    only_preview = "--preview" in sys.argv
    os.makedirs(OUT_DIR, exist_ok=True)
    textures = []
    for name, fn in GLYPHS:
        tex = to_texture(fn())
        textures.append((name, tex))
        if not only_preview:
            tex.save(os.path.join(OUT_DIR, name + ".png"), optimize=True)
    contact_sheet(textures).save(PREVIEW, optimize=True)
    print("%d glyphs%s; preview: %s" % (len(textures), "" if only_preview else " in " + OUT_DIR, PREVIEW))


if __name__ == "__main__":
    main()
