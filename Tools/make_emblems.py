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

Release C:
  - the pilgrim fellowship: a scallop shell over a walking staff (emblem); a shrine under its pointed roof beside the
    long hostel (town);
  - the learned society: an open book with a quill across it (emblem); an archive's front of four columns under a
    pediment, on its steps (town);
  - the spacer enclave: a trader's coin with a square hole, ringed by an orbit and its ship (emblem); a lander on its
    pad beside the control mast (town);
  - the river folk: a fish leaping over two waves (emblem); a house on posts over the water with a boat drawn up
    beside it (town).

A new people's glyphs: a function for each and a line in GLYPHS.

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


def rot_ellipse(cx, cy, rx, ry, deg, n=48):
    """An ellipse's outline turned by deg (rx along the turned x axis), as points for a polygon."""
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    pts = []
    for i in range(n):
        t = 2 * math.pi * i / n
        x, y = rx * math.cos(t), ry * math.sin(t)
        pts.append((cx + x * c - y * s, cy + x * s + y * c))
    return pts


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
    # A curved horn, narrow at the tip on the upper left and swelling evenly to its mouth on the right: discs along a
    # curve, each a little wider than the last, so the horn is one smooth cone.
    path = bezier((60, 196), (84, 420), (330, 476), (444, 316), 80)
    radii = [10 + 52 * (i / (len(path) - 1)) ** 1.15 for i in range(len(path))]
    for (x, y), r in zip(path, radii):
        circle(horn, x, y, r)
    # The mouth, open: an ellipse across the horn's end, its hollow cut in.
    (x1, y1), (x0, y0) = path[-1], path[-4]
    angle = math.degrees(math.atan2(y1 - y0, x1 - x0))
    poly(horn, rot_ellipse(x1, y1, 22, radii[-1] + 4, angle))
    poly(horn, rot_ellipse(x1 + 6, y1 - 4, 10, radii[-1] - 16, angle), fill=OFF)
    # Two bands round the horn, cut in across it.
    for at in (0.42, 0.66):
        i = int(at * (len(path) - 1))
        (bx, by), (ax, ay) = path[i], path[i - 2]
        nx, ny = -(by - ay), bx - ax
        length = math.hypot(nx, ny)
        nx, ny = nx / length, ny / length
        r = radii[i] + 4
        line(horn, [(bx - nx * r, by - ny * r), (bx + nx * r, by + ny * r)], 12, fill=OFF, caps=False)
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


# ---------------------------------------------------------------- the pilgrim fellowship

def g_pilgrims():
    """A scallop shell over a walking staff: the pilgrim's badge and the long road."""
    staff = new()
    line(staff, [(256, 40), (256, 480)], 30)
    # The gourd tied below the staff's head.
    circle(staff, 256, 92, 30)
    shell = new()
    # The shell: a fan of ribs spreading up from its hinge, its top edge scalloped.
    cx, cy = 256, 420
    ribs = 7
    for i in range(ribs):
        a0 = math.radians(200 + i * (140 / ribs))
        a1 = math.radians(200 + (i + 1) * (140 / ribs))
        r = 250
        pts = [(cx, cy), (cx + r * math.cos(a0), cy + r * math.sin(a0)), (cx + r * math.cos(a1), cy + r * math.sin(a1))]
        poly(shell, pts)
        mid = (a0 + a1) / 2
        circle(shell, cx + (r - 6) * math.cos(mid), cy + (r - 6) * math.sin(mid), r * math.sin((a1 - a0) / 2) * 0.98)
    # The grooves between the ribs, cut from near the hinge to the edge.
    for i in range(1, ribs):
        a = math.radians(200 + i * (140 / ribs))
        line(shell, [(cx + 70 * math.cos(a), cy + 70 * math.sin(a)), (cx + 232 * math.cos(a), cy + 232 * math.sin(a))], 12, fill=OFF, caps=False)
    # The hinge's two ears.
    poly(shell, [(cx - 96, cy + 6), (cx - 30, cy - 40), (cx - 20, cy + 40)])
    poly(shell, [(cx + 96, cy + 6), (cx + 30, cy - 40), (cx + 20, cy + 40)])
    return over(shell, staff, 14)


def g_pilgrims_town():
    """A shrine under its pointed roof beside the long hostel, a flame in the shrine's door."""
    m = new()
    # The hostel: a long low house with its door and windows.
    rect(m, 220, 300, 470, 440)
    poly(m, [(196, 312), (345, 230), (494, 312)])
    rect(m, 318, 370, 372, 440, fill=OFF)
    for x in (246, 412):
        rect(m, x, 336, x + 30, 384, r=8, fill=OFF)
    # The shrine: a tall house with a pointed roof and a finial, standing in front at the left.
    m2 = new()
    rect(m2, 60, 230, 220, 440)
    poly(m2, [(40, 244), (140, 92), (240, 244)])
    line(m2, [(140, 96), (140, 40)], 16)
    circle(m2, 140, 40, 16)
    # Its open door with the flame inside.
    rect(m2, 108, 330, 172, 440, fill=OFF)
    circle(m2, 140, 330, 32, fill=OFF)
    poly(m2, teardrop(140, 336, 420, 20))
    return over(m2, m, 12)


# ---------------------------------------------------------------- the learned society

def g_scholars():
    """An open book with a quill laid across it: the archive and the copied page."""
    book = new()
    # The two pages, lifted at their outer corners, meeting at the spine.
    poly(book, [(44, 206), (150, 180), (246, 214), (246, 430), (150, 398), (44, 420)])
    poly(book, [(468, 206), (362, 180), (266, 214), (266, 430), (362, 398), (468, 420)])
    # The written lines on each page.
    for i in range(4):
        y = 250 + i * 40
        line(book, [(78, y + 2), (150, y - 14), (214, y + 6)], 10, fill=OFF, caps=False)
        line(book, [(298, y + 6), (362, y - 14), (434, y + 2)], 10, fill=OFF, caps=False)
    quill = new()
    # The quill: a long feather from the upper right down to its nib on the right page.
    vane = bezier((470, 40), (430, 70), (360, 170), (316, 300), 40)
    for i, (x, y) in enumerate(vane):
        circle(quill, x, y, 8 + 30 * math.sin(math.pi * min(1.0, i / (len(vane) - 1) * 1.1)))
    line(quill, bezier((476, 34), (430, 80), (350, 200), (300, 352), 40), 12)
    return over(quill, book, 14)


def g_scholars_town():
    """An archive: four columns under a pediment, on three steps, its door between the middle columns."""
    m = new()
    poly(m, [(40, 196), (256, 70), (472, 196)])   # the pediment
    rect(m, 56, 196, 456, 232)                      # the beam
    poly(m, [(140, 182), (256, 114), (372, 182)], fill=OFF)
    poly(m, [(176, 174), (256, 128), (336, 174)])
    for x in (84, 172, 306, 394):                   # the columns
        rect(m, x, 240, x + 34, 400)
        rect(m, x - 8, 240, x + 42, 256)
    rect(m, 214, 290, 298, 400)                     # the door
    rect(m, 230, 306, 282, 400, fill=OFF)
    for k in range(3):                              # the steps
        rect(m, 40 - k * 14, 400 + k * 26, 472 + k * 14, 422 + k * 26)
    return m


# ---------------------------------------------------------------- the spacer enclave

def g_enclave():
    """A trader's coin with a square hole, ringed by an orbit, a ship on its way round."""
    coin = new()
    circle(coin, 256, 256, 150)
    rect(coin, 222, 222, 290, 290, fill=OFF)
    # The coin's rim, cut in.
    ImageDraw.Draw(coin).ellipse([126, 126, 386, 386], outline=OFF, width=14)
    orbit = new()
    poly(orbit, rot_ellipse(256, 256, 236, 92, -24, 96))
    poly(orbit, rot_ellipse(256, 256, 208, 66, -24, 96), fill=OFF)
    # The coin passes in front of the orbit's lower half, the orbit in front of its upper half.
    upper = new()
    rect(upper, 0, 0, S, 256)
    back = minus(orbit, upper)
    front = minus(orbit, minus(orbit, upper))
    m = over(coin, back, 12)
    m = over(front, m, 12)
    # The ship on the orbit: a small diamond.
    ship = new()
    a = math.radians(-24)
    sx, sy = 256 + 222 * math.cos(math.radians(200)) * math.cos(a) - 79 * math.sin(math.radians(200)) * math.sin(a), 256 + 222 * math.cos(math.radians(200)) * math.sin(a) + 79 * math.sin(math.radians(200)) * math.cos(a)
    poly(ship, [(sx, sy - 34), (sx + 24, sy), (sx, sy + 34), (sx - 24, sy)])
    return over(ship, m, 12)


def g_enclave_town():
    """A lander on its pad beside the control mast, its legs out."""
    m = new()
    rect(m, 40, 420, 472, 452)                      # the pad
    rect(m, 70, 452, 442, 470)
    # The lander: a rounded hull on three legs.
    lander = new()
    ellipse(lander, 150, 196, 362, 380)
    rect(lander, 150, 290, 362, 360, r=20)
    rect(lander, 210, 236, 302, 270, r=14, fill=OFF)   # its window
    for x0, x1 in ((170, 128), (342, 384)):
        line(lander, [(x0, 350), (x1, 420)], 18)
    line(lander, [(256, 370), (256, 420)], 18)
    m = union(m, lander)
    # The control mast with its dish and its light.
    line(m, [(436, 420), (436, 150)], 22, caps=False)
    rect(m, 410, 300, 462, 330)
    arc(m, 436, 150, 46, 200, 340, 16)
    circle(m, 436, 92, 16)
    return m


# ---------------------------------------------------------------- the river folk

def g_riverfolk():
    """A fish leaping over two waves: the river that feeds them."""
    fish = new()
    poly(fish, rot_ellipse(250, 210, 150, 68, -24, 64))
    # The tail, forked.
    poly(fish, [(364, 168), (468, 70), (446, 170), (482, 252)])
    # The eye and the gill's line.
    circle(fish, 156, 236, 16, fill=OFF)
    arc(fish, 196, 226, 50, 290, 420, 12, fill=OFF)
    # The fins.
    poly(fish, [(232, 160), (290, 104), (306, 158)])
    poly(fish, [(214, 270), (244, 330), (270, 262)])
    waves = new()
    for y in (370, 440):
        line(waves, bezier((40, y), (130, y - 60), (190, y + 60), (256, y), 30) + bezier((256, y), (322, y - 60), (382, y + 60), (472, y), 30)[1:], 26)
    return over(fish, waves, 14)


def g_riverfolk_town():
    """A house on posts over the water, a boat drawn up beside it."""
    m = new()
    rect(m, 150, 190, 380, 320)                     # the house
    poly(m, [(120, 204), (265, 92), (410, 204)])    # its roof
    rect(m, 236, 236, 294, 320, fill=OFF)           # its door
    rect(m, 120, 320, 410, 342)                     # the deck
    for x in (150, 230, 300, 372):                  # the posts
        rect(m, x, 342, x + 18, 410)
    # The water under it.
    line(m, bezier((30, 420), (120, 390), (200, 450), (270, 420), 30) + bezier((270, 420), (340, 390), (420, 450), (490, 420), 30)[1:], 22)
    # A boat drawn up on the right.
    boat = new()
    poly(boat, [(376, 460), (492, 460), (472, 494), (396, 494)])
    line(boat, [(430, 460), (470, 372)], 12)
    return over(boat, m, 10)


GLYPHS = [
    ("League", g_league), ("LeagueTown", g_league_town),
    ("Order", g_order), ("OrderTown", g_order_town),
    ("Herders", g_herders), ("HerdersTown", g_herders_town),
    ("Pilgrims", g_pilgrims), ("PilgrimsTown", g_pilgrims_town),
    ("Scholars", g_scholars), ("ScholarsTown", g_scholars_town),
    ("Enclave", g_enclave), ("EnclaveTown", g_enclave_town),
    ("RiverFolk", g_riverfolk), ("RiverFolkTown", g_riverfolk_town),
]

# The peoples' colours (the middle of each colorSpectrum in Defs/PeopleDefs/), for the preview.
TINTS = {"League": (66, 158, 166), "Order": (234, 228, 210), "Herders": (196, 118, 66), "Pilgrims": (145, 161, 130),
         "Scholars": (143, 46, 84), "Enclave": (161, 212, 227), "RiverFolk": (28, 115, 179)}


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
