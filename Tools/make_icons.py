#!/usr/bin/env python3
"""
Draws Rimfolk's interface icons: white glyphs on transparent, 64x64 RGBA, tinted in game like vanilla icons.

Each glyph is drawn as a mask at 4x (256x256) and scaled down, which gives clean antialiased edges. Shapes are
bold, with cutouts at least ~20 units wide at 4x, so they stay readable at 16 to 24 px.

Usage (from the repository root):
    python3 Tools/make_icons.py            # writes Textures/ColonyCouncil/Icons/*.png and Tools/icons_preview.png
    python3 Tools/make_icons.py --preview  # only the contact sheet

Needs Pillow (pip install pillow).
"""
import math
import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

S = 256  # drawing size (4x)
OUT = 64  # texture size
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ICON_DIR = os.path.join(ROOT, "Textures", "ColonyCouncil", "Icons")
PREVIEW = os.path.join(ROOT, "Tools", "icons_preview.png")
ON, OFF = 255, 0


# ---------------------------------------------------------------- primitives

def new():
    return Image.new("L", (S, S), 0)


def circle(m, cx, cy, r, fill=ON):
    ImageDraw.Draw(m).ellipse([cx - r, cy - r, cx + r, cy + r], fill=fill)


def ring(m, cx, cy, r, w, fill=ON):
    ImageDraw.Draw(m).ellipse([cx - r, cy - r, cx + r, cy + r], outline=fill, width=w)


def ellipse(m, x0, y0, x1, y1, fill=ON):
    ImageDraw.Draw(m).ellipse([x0, y0, x1, y1], fill=fill)


def rect(m, x0, y0, x1, y1, r=0, fill=ON):
    d = ImageDraw.Draw(m)
    if r > 0:
        d.rounded_rectangle([x0, y0, x1, y1], radius=r, fill=fill)
    else:
        d.rectangle([x0, y0, x1, y1], fill=fill)


def poly(m, pts, fill=ON):
    ImageDraw.Draw(m).polygon([(float(x), float(y)) for x, y in pts], fill=fill)


def line(m, pts, w, fill=ON, caps=True):
    d = ImageDraw.Draw(m)
    d.line([(float(x), float(y)) for x, y in pts], fill=fill, width=int(w), joint="curve")
    if caps:
        for x, y in (pts[0], pts[-1]):
            circle(m, x, y, w / 2.0, fill)


def arc(m, cx, cy, r, start, end, w, fill=ON):
    """Angles in degrees, 0 at three o'clock, clockwise (screen y points down)."""
    ImageDraw.Draw(m).arc([cx - r, cy - r, cx + r, cy + r], start, end, fill=fill, width=int(w))


def rotate(pts, cx, cy, deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    return [(cx + (x - cx) * c - (y - cy) * s, cy + (x - cx) * s + (y - cy) * c) for x, y in pts]


def bezier(p0, p1, p2, p3, n=24):
    out = []
    for i in range(n + 1):
        t = i / n
        u = 1 - t
        out.append((u ** 3 * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t ** 3 * p3[0],
                    u ** 3 * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t ** 3 * p3[1]))
    return out


def star(cx, cy, ro, ri, points=5, rot=-90):
    pts = []
    for i in range(points * 2):
        r = ro if i % 2 == 0 else ri
        a = math.radians(rot + i * 180.0 / points)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def union(*ms):
    out = ms[0]
    for m in ms[1:]:
        out = ImageChops.lighter(out, m)
    return out


def minus(a, b):
    return ImageChops.subtract(a, b)


def grow(m, px):
    """Dilate a mask by about px units (for the gap where one shape passes over another)."""
    size = int(px) * 2 + 1
    return m.filter(ImageFilter.MaxFilter(size if size % 2 == 1 else size + 1))


def over(top, below, gap=12):
    """`top` drawn over `below`, with a transparent gap between them."""
    return union(minus(below, grow(top, gap)), top)


def clip_below(m, y):
    rect(m, 0, y, S, S, fill=OFF)


# ---------------------------------------------------------------- shared pieces

def bubble(m):
    rect(m, 20, 28, 236, 182, r=44)
    poly(m, [(60, 168), (118, 168), (50, 236)])


def bust(m, cx, cy, head, body_w, body_h, fill=ON):
    """Head and shoulders, cut flat at the bottom by the caller."""
    circle(m, cx, cy, head, fill)
    top = cy + head + 10
    ellipse(m, cx - body_w / 2, top, cx + body_w / 2, top + body_h * 2, fill)


def shield(m, fill=ON, x0=36, y0=20, x1=220, y1=240):
    cx = (x0 + x1) / 2
    right = bezier((x1, y0 + 34), (x1, y0 + 140), (cx + 40, y1 - 30), (cx, y1))
    left = bezier((cx, y1), (cx - 40, y1 - 30), (x0, y0 + 140), (x0, y0 + 34))
    pts = [(cx, y0), (x1, y0 + 34)] + right[1:] + left[1:] + [(x0, y0 + 34)]
    poly(m, pts, fill)


def leaf(p_start, p_end, width, bend=0.0):
    """A pointed leaf from p_start to p_end, `width` across at the widest."""
    (x0, y0), (x1, y1) = p_start, p_end
    dx, dy = x1 - x0, y1 - y0
    length = math.hypot(dx, dy)
    nx, ny = -dy / length, dx / length
    mx, my = x0 + dx * 0.5 + nx * bend, y0 + dy * 0.5 + ny * bend
    a = bezier((x0, y0), (x0 + dx * 0.15 + nx * width * 0.75, y0 + dy * 0.15 + ny * width * 0.75),
               (mx + dx * 0.3 + nx * width * 0.55, my + dy * 0.3 + ny * width * 0.55), (x1, y1))
    b = bezier((x1, y1), (mx + dx * 0.3 - nx * width * 0.55, my + dy * 0.3 - ny * width * 0.55),
               (x0 + dx * 0.15 - nx * width * 0.75, y0 + dy * 0.15 - ny * width * 0.75), (x0, y0))
    return a + b[1:]


def rrect_pts(cx, cy, w, h, deg):
    pts = [(cx - w / 2, cy - h / 2), (cx + w / 2, cy - h / 2), (cx + w / 2, cy + h / 2), (cx - w / 2, cy + h / 2)]
    return rotate(pts, cx, cy, deg)


def check(m, pts, w, fill):
    line(m, pts, w, fill)


def sword(deg):
    """A sword pointing up-right along the diagonal, rotated by deg."""
    m = new()
    c = (128, 128)
    poly(m, [(114, 22), (128, -2), (142, 22), (142, 168), (114, 168)])
    rect(m, 76, 166, 180, 190, r=10)
    rect(m, 115, 188, 141, 230)
    circle(m, 128, 238, 18)
    return m.rotate(-deg, resample=Image.BICUBIC, center=c)


# ---------------------------------------------------------------- the glyphs

def g_colony():
    m = new()
    rect(m, 170, 40, 198, 110)  # chimney
    poly(m, [(128, 22), (240, 118), (210, 118), (210, 234), (46, 234), (46, 118), (16, 118)])
    rect(m, 106, 156, 150, 234, r=6, fill=OFF)  # door
    return m


def g_people():
    back = new()
    bust(back, 172, 76, 38, 128, 70)
    clip_below(back, 196)
    front = new()
    bust(front, 98, 100, 44, 150, 84)
    clip_below(front, 236)
    return over(front, back, 12)


def g_council():
    m = new()
    poly(m, [(18, 92), (128, 22), (238, 92)])
    rect(m, 34, 100, 222, 122)
    for x in (52, 113, 174):
        rect(m, x, 132, x + 30, 202)
    rect(m, 24, 210, 232, 236, r=4)
    circle(m, 128, 72, 14, OFF)
    return m


def g_history():
    m = new()
    rect(m, 46, 18, 210, 46, r=10)
    rect(m, 46, 210, 210, 238, r=10)
    glass = [(70, 46), (186, 46), (186, 70), (142, 126), (142, 130), (186, 186), (186, 210), (70, 210), (70, 186),
             (114, 130), (114, 126), (70, 70)]
    poly(m, glass)
    # The top bulb is mostly empty; the sand has run to the bottom.
    poly(m, [(92, 66), (164, 66), (164, 70), (132, 108), (124, 108), (92, 70)], OFF)
    poly(m, [(92, 190), (164, 190), (164, 186), (140, 158), (116, 158), (92, 186)], OFF)
    poly(m, [(100, 190), (156, 190), (128, 160)])
    return m


def g_almanac():
    m = new()
    rect(m, 40, 20, 216, 228, r=16)
    rect(m, 72, 20, 86, 228, fill=OFF)  # spine
    rect(m, 106, 62, 192, 98, r=6, fill=OFF)  # title plate
    poly(m, [(156, 180), (196, 180), (196, 248), (176, 230), (156, 248)], OFF)  # room for the ribbon
    poly(m, [(166, 170), (186, 170), (186, 240), (176, 228), (166, 240)])  # ribbon
    return m


def g_overview():
    m = new()
    rect(m, 24, 24, 118, 118, r=18)
    rect(m, 138, 24, 232, 118, r=18)
    rect(m, 24, 138, 118, 232, r=18)
    rect(m, 138, 138, 232, 232, r=18)
    return m


def g_priorities():
    m = new()
    rect(m, 40, 36, 216, 238, r=18)  # board
    rect(m, 88, 16, 168, 54, r=10, fill=OFF)
    rect(m, 96, 22, 160, 48, r=8)  # clip
    for y in (102, 168):
        check(m, [(66, 0 + y), (82, 16 + y), (108, -10 + y)], 16, OFF)
        rect(m, 122, y - 6, 190, y + 8, r=6, fill=OFF)
    return m


def g_moods():
    m = new()
    circle(m, 128, 128, 108)
    ellipse(m, 78, 74, 106, 118, OFF)
    ellipse(m, 150, 74, 178, 118, OFF)
    arc(m, 128, 128, 66, 25, 155, 22, OFF)
    return m


def g_world():
    m = new()
    circle(m, 128, 128, 108)
    ImageDraw.Draw(m).ellipse([82, 20, 174, 236], outline=OFF, width=16)
    rect(m, 20, 120, 236, 136, fill=OFF)
    rect(m, 120, 20, 136, 236, fill=OFF)
    return m


def g_rooms():
    m = new()
    ImageDraw.Draw(m).rectangle([22, 22, 234, 234], outline=ON, width=26)
    rect(m, 106, 22, 132, 136)  # wall from the top
    rect(m, 106, 110, 234, 136)  # wall to the right
    rect(m, 22, 156, 48, 204, fill=OFF)  # front door
    rect(m, 156, 208, 200, 234, fill=OFF)  # back door
    return m


def g_roster():
    m = new()
    for y in (52, 128, 204):
        circle(m, 52, y, 26)
        rect(m, 96, y - 16, 232, y + 16, r=16)
    return m


def g_bonds():
    m = new()
    nodes = [(62, 66), (198, 84), (108, 198)]
    for i in range(3):
        a, b = nodes[i], nodes[(i + 1) % 3]
        line(m, [a, b], 22)
    for x, y in nodes:
        circle(m, x, y, 52, OFF)
        circle(m, x, y, 40)
    return m


def g_wishes():
    m = new()
    bubble(m)
    poly(m, star(128, 108, 54, 22), OFF)
    return m


def g_decrees():
    m = new()
    rect(m, 60, 40, 196, 206)  # sheet
    rect(m, 30, 20, 226, 56, r=18)  # top roll
    rect(m, 30, 196, 226, 232, r=18)  # bottom roll
    rect(m, 46, 30, 62, 46, r=8, fill=OFF)
    for y in (82, 118, 154):
        rect(m, 86, y, 170, y + 14, r=6, fill=OFF)
    return m


def g_ambition():
    m = new()
    poly(m, [(12, 232), (104, 100), (136, 146), (166, 112), (244, 232)])
    poly(m, [(104, 100), (124, 128), (112, 124), (104, 136), (96, 124), (84, 128)], OFF)  # snow line
    rect(m, 96, 18, 110, 104)  # pole
    poly(m, [(110, 20), (176, 42), (110, 66)])  # flag
    return m


def g_ways():
    m = new()
    ring(m, 128, 128, 112, 24)
    needle = [(128, 40), (154, 128), (128, 216), (102, 128)]
    poly(m, rotate(needle, 128, 128, 40))
    circle(m, 128, 128, 14, OFF)
    return m


def g_chronicle():
    m = new()
    poly(m, [(14, 58), (116, 74), (116, 222), (14, 206)])
    poly(m, [(140, 74), (242, 58), (242, 206), (140, 222)])
    for i, y in enumerate((106, 144)):
        line(m, [(40, y - 4 + i * 2), (94, y + 4 + i * 2)], 12, OFF)
        line(m, [(162, y + 4 + i * 2), (216, y - 4 + i * 2)], 12, OFF)
    return m


def g_dreams():
    m = new()
    circle(m, 112, 140, 96)
    circle(m, 160, 104, 84, OFF)
    poly(m, star(190, 56, 34, 9, 4, -90))
    poly(m, star(216, 146, 22, 6, 4, -90))
    return m


def g_calendar():
    """A wall calendar: two rings on top, a header band, a grid of days with one marked."""
    m = new()
    rect(m, 22, 46, 234, 234, r=24)
    rect(m, 40, 98, 216, 108, fill=OFF)  # under the header band
    cell, gap = 28, 12
    for row in range(3):
        for col in range(4):
            x = 52 + col * (cell + gap)
            y = 122 + row * (cell + gap)
            if (row, col) != (1, 2):
                rect(m, x, y, x + cell, y + cell, r=6, fill=OFF)
    rings = new()
    for cx in (82, 174):
        rect(rings, cx - 13, 14, cx + 13, 76, r=13)
    return over(rings, m, 10)


def g_week():
    """This week: a page with a header band and a bold check: what needs doing."""
    m = new()
    rect(m, 22, 30, 234, 230, r=24)
    rect(m, 42, 84, 214, 210, r=10, fill=OFF)  # the page under the header band
    check(m, [(78, 148), (114, 182), (180, 112)], 28, ON)
    return m


def g_table():
    """Everyone: a table with a header row and a grid of cells."""
    m = new()
    rect(m, 20, 34, 236, 222, r=18)
    for x0, x1 in ((36, 92), (106, 162), (176, 220)):
        for y0, y1 in ((94, 142), (156, 206)):
            rect(m, x0, y0, x1, y1, r=6, fill=OFF)
    return m


def g_dilemmas():
    """Dilemmas: a signpost, one arm each way."""
    m = new()
    rect(m, 116, 30, 140, 238, r=8)  # the post
    rect(m, 84, 222, 172, 240, r=8)  # its foot
    poly(m, [(30, 82), (62, 50), (150, 50), (150, 114), (62, 114)])  # pointing left
    poly(m, [(106, 128), (194, 128), (226, 160), (194, 192), (106, 192)])  # pointing right
    return m


def g_pin():
    """The pin: a push pin, head up."""
    m = new()
    rect(m, 80, 18, 176, 54, r=14)  # the cap
    poly(m, [(100, 50), (156, 50), (168, 132), (88, 132)])  # the body
    rect(m, 62, 128, 194, 158, r=12)  # the collar
    line(m, [(128, 158), (128, 240)], 16)  # the needle
    return m


def g_more():
    """More (the bottom bar's button for the buttons put away): three bold dots in a row."""
    m = new()
    for cx in (52, 128, 204):
        circle(m, cx, 128, 30)
    return m


def g_plan():
    """Next steps: three lines of a list, the first two ticked, the third still open."""
    m = new()
    for i, (y, done) in enumerate(((56, True), (128, True), (200, False))):
        if done:
            check(m, [(30, y + 4), (54, y + 28), (92, y - 24)], 18, ON)
        else:
            ring(m, 58, y, 22, 12)
        rect(m, 118, y - 12, 232, y + 12, r=10)
    return m


def g_base():
    """The base looked over: a ground plan of three rooms, a magnifying glass over the corner."""
    m = new()
    ImageDraw.Draw(m).rectangle([22, 40, 200, 234], outline=ON, width=22)
    rect(m, 100, 40, 122, 150)  # wall down the middle
    rect(m, 22, 128, 200, 150)  # wall across
    rect(m, 22, 72, 44, 108, fill=OFF)  # the door in
    rect(m, 100, 92, 122, 118, fill=OFF)  # a door through
    ring(m, 184, 70, 46, 18)
    line(m, [(216, 102), (246, 132)], 22)
    return m


def g_making():
    """What you could make: a chain of three links, raw to made, with an arrowhead at the end."""
    m = new()
    for cx in (52, 128, 204):
        ring(m, cx, 128, 34, 16)
    rect(m, 86, 118, 94, 138)
    rect(m, 162, 118, 170, 138)
    poly(m, [(196, 60), (248, 128), (196, 196), (196, 170), (222, 128), (196, 86)])
    return m


def g_journals():
    m = new()
    rect(m, 52, 20, 214, 236, r=16)
    for y in (58, 104, 150, 196):
        rect(m, 30, y - 9, 70, y + 9, r=9, fill=OFF)
        rect(m, 34, y - 6, 66, y + 6, r=6)
    for y in (78, 118, 158):
        rect(m, 98, y, 188, y + 14, r=6, fill=OFF)
    return m


# Markers

def g_trust():
    m = new()
    shield(m)
    check(m, [(78, 128), (114, 164), (180, 94)], 26, OFF)
    return m


def g_office():
    m = new()
    poly(m, [(62, 14), (110, 14), (150, 108), (112, 122)])
    poly(m, [(194, 14), (146, 14), (106, 108), (144, 122)])
    circle(m, 128, 168, 76, OFF)
    circle(m, 128, 168, 64)
    poly(m, star(128, 172, 38, 16), OFF)
    return m


def g_lifegoal():
    m = new()
    circle(m, 128, 128, 110)
    circle(m, 128, 128, 84, OFF)
    circle(m, 128, 128, 60)
    circle(m, 128, 128, 36, OFF)
    circle(m, 128, 128, 16)
    return m


def g_secret():
    m = new()
    arc(m, 128, 104, 62, 180, 360, 26)
    rect(m, 66, 100, 92, 124)
    rect(m, 164, 100, 190, 124)
    rect(m, 40, 114, 216, 238, r=22)
    circle(m, 128, 162, 20, OFF)
    poly(m, [(118, 168), (138, 168), (144, 206), (112, 206)], OFF)
    return m


def g_vendetta():
    dagger = new()
    circle(dagger, 128, 22, 20)
    rect(dagger, 114, 34, 142, 86, r=4)
    rect(dagger, 70, 84, 186, 112, r=12)
    poly(dagger, [(104, 112), (152, 112), (152, 190), (128, 246), (104, 190)])
    dagger = dagger.rotate(-30, resample=Image.BICUBIC, center=(128, 128))
    drop = new()
    circle(drop, 58, 196, 32)
    poly(drop, [(28, 190), (58, 120), (88, 190)])
    return over(dagger, drop, 12)


def g_nemesis():
    m = new()
    circle(m, 128, 108, 94)
    rect(m, 78, 150, 178, 230, r=18)
    ellipse(m, 64, 92, 116, 146, OFF)
    ellipse(m, 140, 92, 192, 146, OFF)
    poly(m, [(128, 150), (112, 180), (144, 180)], OFF)
    for x in (104, 128, 152):
        rect(m, x - 6, 196, x + 6, 232, fill=OFF)
    return m


def g_dream():
    m = new()
    circle(m, 120, 134, 104)
    circle(m, 172, 96, 92, OFF)
    return m


def g_journal():
    m = new()
    feather = leaf((220, 22), (70, 186), 74, bend=-6)
    poly(m, feather)
    line(m, [(206, 40), (66, 190)], 12, OFF, caps=False)
    for t in (0.38, 0.62):
        x, y = 206 + (66 - 206) * t, 40 + (190 - 40) * t
        line(m, [(x, y), (x + 34, y + 10)], 10, OFF, caps=False)
    line(m, [(78, 178), (30, 232)], 18)
    return m


def g_worldevent():
    planet = new()
    circle(planet, 128, 128, 70)
    ringm = new()
    ImageDraw.Draw(ringm).ellipse([10, 96, 246, 160], outline=ON, width=20)
    # The ring passes in front of the planet's lower half and behind its upper half.
    front = ringm.copy()
    rect(front, 0, 0, S, 128, fill=OFF)
    back = minus(ringm, front)
    front = front.rotate(22, resample=Image.BICUBIC, center=(128, 128))
    back = back.rotate(22, resample=Image.BICUBIC, center=(128, 128))
    return over(front, union(minus(back, grow(planet, 12)), planet), 12)


def g_raid():
    a = sword(45)
    b = sword(-45)
    return over(a, b, 12)


def g_trader():
    m = new()
    poly(m, [(78, 22), (178, 22), (156, 66), (100, 66)])  # top
    rect(m, 92, 72, 164, 96, r=10)  # tie
    ellipse(m, 30, 90, 226, 242)  # sack
    rect(m, 84, 70, 172, 98, r=12, fill=OFF)
    rect(m, 94, 76, 162, 92, r=8)
    ring(m, 128, 168, 40, 16, OFF)
    rect(m, 121, 146, 135, 190, fill=OFF)
    return m


def g_visitor():
    m = new()

    def foot(cx, cy, deg):
        f = new()
        ellipse(f, cx - 30, cy - 62, cx + 30, cy + 28)
        ellipse(f, cx - 24, cy + 40, cx + 24, cy + 84)
        return f.rotate(deg, resample=Image.BICUBIC, center=(cx, cy))

    return union(foot(84, 140, 12), foot(172, 86, -12))


def g_death():
    m = new()
    circle(m, 128, 96, 76)
    rect(m, 52, 96, 204, 214)
    rect(m, 28, 208, 228, 238, r=8)
    rect(m, 117, 70, 139, 178, fill=OFF)
    rect(m, 92, 96, 164, 116, fill=OFF)
    return m


def g_birth():
    m = new()
    stem = bezier((128, 236), (124, 196), (132, 160), (128, 116))
    line(m, stem, 20)
    poly(m, leaf((124, 150), (24, 92), 62, bend=-10))
    poly(m, leaf((132, 122), (232, 40), 70, bend=10))
    rect(m, 60, 226, 196, 244, r=9)
    return m


def g_wedding():
    a = new()
    ring(a, 90, 128, 72, 24)
    b = new()
    ring(b, 166, 128, 72, 24)
    # The left ring passes over the right one at the top crossing, under it at the bottom one.
    m = over(a, b, 10)
    region = new()
    rect(region, 0, 128, 256, 256)
    b_low = ImageChops.multiply(b, region)
    return over(b_low, m, 10)


def g_decree():
    m = new()
    poly(m, rrect_pts(150, 84, 136, 58, 45))  # head
    poly(m, rrect_pts(196, 130, 24, 80, 45))  # bands at both ends
    poly(m, rrect_pts(104, 38, 24, 80, 45))
    line(m, [(150, 84), (44, 190)], 24)  # handle
    rect(m, 128, 206, 238, 236, r=10)  # block
    return m


def g_promise():
    m = new()
    bubble(m)
    check(m, [(78, 104), (114, 138), (178, 72)], 26, OFF)
    return m


def g_warning():
    m = new()
    tri = [(128, 22), (236, 222), (20, 222)]
    poly(m, tri)
    line(m, tri + [tri[0]], 20, ON, caps=False)
    for x, y in tri:
        circle(m, x, y, 10)
    rect(m, 116, 82, 140, 160, r=8, fill=OFF)
    circle(m, 128, 190, 15, OFF)
    return m


def g_heart():
    m = new()
    circle(m, 80, 92, 60)
    circle(m, 176, 92, 60)
    poly(m, [(26, 116), (230, 116), (128, 230)])
    poly(m, [(30, 100), (128, 104), (226, 100), (128, 232)])
    return m


def g_strain():
    """Strain near breaking (the colonist bar's badge): the heart with a crack down it."""
    m = g_heart()
    line(m, [(128, 52), (104, 104), (150, 140), (116, 182), (130, 236)], 26, OFF, caps=False)
    return m


def g_leaving():
    """About to leave the colony (the colonist bar's badge): a doorway, and an arrow heading out of it."""
    m = new()
    rect(m, 24, 26, 136, 230, r=14)
    rect(m, 52, 54, S, 202, fill=OFF)  # hollow it, open on the right
    rect(m, 84, 110, 186, 146, r=8)  # the arrow's shaft
    poly(m, [(170, 66), (242, 128), (170, 190)])  # its head
    return m


def g_bar_dot():
    """A plain round dot: the badges' dark backing on the colonist bar, and the break dot."""
    m = new()
    circle(m, 128, 128, 122)
    return m


def g_bar_ring():
    """The colonist bar's break ring: a soft band along a portrait's edge, strongest at the edge, clear in the middle."""
    outer = new()
    rect(outer, 0, 0, S - 1, S - 1, r=36)
    hard_inner = new()
    rect(hard_inner, 11, 11, S - 12, S - 12, r=28)
    soft_inner = new()
    rect(soft_inner, 20, 20, S - 21, S - 21, r=24)
    soft = minus(outer, soft_inner).filter(ImageFilter.GaussianBlur(8)).point(lambda v: v * 3 // 4)
    return ImageChops.multiply(union(minus(outer, hard_inner), soft), outer)


def g_trendup():
    m = new()
    poly(m, [(128, 40), (224, 176), (32, 176)])
    return m


def g_trenddown():
    m = new()
    poly(m, [(128, 216), (224, 80), (32, 80)])
    return m


# On-screen panels (Source/ColonyCouncil/Hud/)

def g_panels():
    """The panels' switch: a screen frame with a tall panel on the left and two small ones on the right."""
    m = new()
    rect(m, 14, 30, 242, 226, r=22)
    rect(m, 36, 52, 220, 204, r=10, fill=OFF)
    rect(m, 52, 68, 108, 188, r=8)
    rect(m, 150, 68, 204, 110, r=8)
    rect(m, 150, 128, 204, 170, r=8)
    return m


def g_power():
    """Power: a lightning bolt."""
    m = new()
    poly(m, [(150, 14), (52, 146), (118, 146), (96, 242), (204, 100), (136, 100), (172, 14)])
    return m


def g_temperature():
    """Temperature: a thermometer, its bulb filled."""
    m = new()
    rect(m, 98, 18, 158, 170, r=30)
    circle(m, 128, 192, 50)
    rect(m, 116, 40, 140, 160, r=12, fill=OFF)
    circle(m, 128, 192, 30, OFF)
    circle(m, 128, 192, 22)
    rect(m, 120, 96, 136, 190, r=6)
    for y in (64, 96, 128):
        rect(m, 168, y - 6, 206, y + 6, r=5)
    return m


def g_research():
    """Research: a flask with liquid in it."""
    m = new()
    rect(m, 100, 18, 156, 40, r=8)
    poly(m, [(108, 36), (148, 36), (148, 96), (226, 220), (30, 220), (108, 96)])
    line(m, [(30, 220), (226, 220)], 24, ON)
    poly(m, [(124, 52), (132, 52), (132, 104), (196, 202), (60, 202), (124, 104)], OFF)
    poly(m, [(100, 150), (156, 150), (190, 196), (66, 196)])
    return m


# Almanac categories

def g_cat_basics():
    m = new()
    circle(m, 128, 100, 82)
    poly(m, [(76, 150), (180, 150), (162, 184), (94, 184)])
    rect(m, 94, 190, 162, 206, r=6)
    rect(m, 102, 214, 154, 236, r=10)
    arc(m, 128, 100, 52, 200, 270, 16, OFF)
    return m


def g_cat_food():
    m = new()
    # A bowl with steam.
    ImageDraw.Draw(m).chord([22, 76, 234, 236], 0, 180, fill=ON)
    rect(m, 14, 148, 242, 166, r=8)
    rect(m, 90, 222, 166, 240, r=8)
    for x in (88, 128, 168):
        pts = [(x + 12 * math.sin(i / 10 * math.pi * 2), 120 - i * 9) for i in range(11)]
        line(m, pts, 14)
    return m


def g_cat_defense():
    m = new()
    shield(m)
    rect(m, 118, 20, 138, 240, fill=OFF)
    rect(m, 30, 100, 226, 120, fill=OFF)
    return m


def g_defenses():
    """Defenses: a stretch of castle wall with battlements and an arched gate."""
    m = new()
    rect(m, 24, 100, 232, 236, r=6)
    for x0 in (24, 106, 188):
        rect(m, x0, 52, x0 + 44, 108, r=4)
    rect(m, 100, 156, 156, 240, fill=OFF)
    circle(m, 128, 158, 28, OFF)
    return m


def g_cat_health():
    m = new()
    rect(m, 92, 24, 164, 232, r=16)
    rect(m, 24, 92, 232, 164, r=16)
    return m


def g_cat_management():
    m = new()
    for i in range(4):
        poly(m, rrect_pts(128, 128, 42, 236, i * 45))
    circle(m, 128, 128, 88)
    circle(m, 128, 128, 40, OFF)
    return m


def g_cat_lowtech():
    m = new()
    flame = bezier((128, 196), (40, 196), (80, 110), (120, 20)) + bezier((120, 20), (150, 90), (230, 150), (128, 196))[1:]
    poly(m, flame)
    inner = bezier((128, 184), (90, 184), (100, 140), (124, 104)) + bezier((124, 104), (136, 140), (170, 170), (128, 184))[1:]
    poly(m, inner, OFF)
    line(m, [(40, 238), (216, 206)], 22)
    line(m, [(40, 206), (216, 238)], 22)
    return m


def g_cat_roomdesign():
    m = new()
    poly(m, [(24, 24), (24, 232), (232, 232)])
    poly(m, [(56, 104), (56, 200), (152, 200)], OFF)
    for i, y in enumerate(range(60, 200, 28)):
        rect(m, 24, y, 44 if i % 2 else 52, y + 8, fill=OFF)
    return m


def g_cat_royalty():
    m = new()
    poly(m, [(30, 200), (20, 74), (82, 136), (128, 52), (174, 136), (236, 74), (226, 200)])
    for x, y in ((20, 70), (128, 46), (236, 70)):
        circle(m, x, y, 18)
    rect(m, 30, 210, 226, 236, r=8)
    circle(m, 128, 160, 16, OFF)
    return m


def g_cat_ideology():
    m = new()
    circle(m, 128, 128, 58)
    for i in range(8):
        poly(m, rotate([(118, 50), (138, 50), (128, 14)], 128, 128, i * 45))
        poly(m, rotate([(116, 58), (140, 58), (128, 48)], 128, 128, i * 45))
    circle(m, 128, 128, 30, OFF)
    return m


def g_cat_biotech():
    m = new()
    n = 40
    a = [(128 + 72 * math.sin(i / n * math.pi * 2.0), 18 + i * (220 / n)) for i in range(n + 1)]
    b = [(128 - 72 * math.sin(i / n * math.pi * 2.0), 18 + i * (220 / n)) for i in range(n + 1)]
    for i in (5, 15, 25, 35):
        line(m, [a[i], b[i]], 14, caps=False)
    line(m, a, 24)
    line(m, b, 24)
    return m


def g_cat_anomaly():
    m = new()
    top = bezier((14, 128), (70, 40), (186, 40), (242, 128))
    bottom = bezier((242, 128), (186, 216), (70, 216), (14, 128))
    poly(m, top + bottom[1:])
    circle(m, 128, 128, 58, OFF)
    circle(m, 128, 128, 40)
    ellipse(m, 120, 96, 136, 160, OFF)
    return m


def g_cat_odyssey():
    m = new()
    body = bezier((128, 14), (196, 60), (176, 160), (164, 196)) + [(92, 196)] + bezier((92, 196), (80, 160), (60, 60), (128, 14))[1:]
    poly(m, body)
    poly(m, [(84, 140), (40, 214), (96, 190)])
    poly(m, [(172, 140), (216, 214), (160, 190)])
    circle(m, 128, 96, 22, OFF)
    poly(m, [(104, 206), (152, 206), (128, 246)])
    return m


def g_cat_numbers():
    m = new()
    rect(m, 32, 138, 76, 222, r=6)
    rect(m, 106, 92, 150, 222, r=6)
    rect(m, 180, 34, 224, 222, r=6)
    rect(m, 18, 228, 238, 242, r=6)
    return m


def g_cat_mods():
    m = new()
    rect(m, 36, 64, 200, 228, r=14)
    circle(m, 118, 52, 28)  # tab on top
    circle(m, 212, 146, 28)  # tab on the right
    circle(m, 36, 146, 26, OFF)  # notch on the left
    circle(m, 118, 228, 26, OFF)  # notch at the bottom
    return m


GLYPHS = [
    # sections
    ("Colony", g_colony), ("People", g_people), ("Council", g_council), ("History", g_history), ("Almanac", g_almanac),
    # sub-tabs
    ("Overview", g_overview), ("Priorities", g_priorities), ("Moods", g_moods), ("World", g_world), ("Rooms", g_rooms),
    ("Roster", g_roster), ("Bonds", g_bonds), ("Wishes", g_wishes), ("Decrees", g_decrees), ("Ambition", g_ambition),
    ("Ways", g_ways), ("Chronicle", g_chronicle), ("Calendar", g_calendar), ("Dreams", g_dreams), ("Journals", g_journals),
    ("Week", g_week), ("Table", g_table), ("Dilemmas", g_dilemmas), ("Pin", g_pin), ("Defenses", g_defenses),
    ("More", g_more), ("Plan", g_plan), ("Base", g_base), ("Making", g_making),
    # markers
    ("Trust", g_trust), ("Office", g_office), ("LifeGoal", g_lifegoal), ("Secret", g_secret), ("Vendetta", g_vendetta),
    ("Nemesis", g_nemesis), ("Dream", g_dream), ("Journal", g_journal), ("WorldEvent", g_worldevent), ("Raid", g_raid),
    ("Trader", g_trader), ("Visitor", g_visitor), ("Death", g_death), ("Birth", g_birth), ("Wedding", g_wedding),
    ("Decree", g_decree), ("Promise", g_promise), ("Warning", g_warning), ("Heart", g_heart),
    ("TrendUp", g_trendup), ("TrendDown", g_trenddown),
    # the colonist bar's badges (Source/ColonyCouncil/ColonistBar/BarBadges.cs)
    ("Strain", g_strain), ("Leaving", g_leaving), ("BarDot", g_bar_dot), ("BarRing", g_bar_ring),
    # on-screen panels
    ("Panels", g_panels), ("Power", g_power), ("Temperature", g_temperature), ("Research", g_research),
    # almanac categories
    ("CatBasics", g_cat_basics), ("CatFood", g_cat_food), ("CatDefense", g_cat_defense), ("CatHealth", g_cat_health),
    ("CatManagement", g_cat_management), ("CatLowTech", g_cat_lowtech), ("CatRoomDesign", g_cat_roomdesign),
    ("CatRoyalty", g_cat_royalty), ("CatIdeology", g_cat_ideology), ("CatBiotech", g_cat_biotech),
    ("CatAnomaly", g_cat_anomaly), ("CatOdyssey", g_cat_odyssey), ("CatNumbers", g_cat_numbers), ("CatMods", g_cat_mods),
]


def to_texture(mask):
    small = mask.resize((OUT, OUT), Image.LANCZOS)
    white = Image.new("L", (OUT, OUT), 255)
    return Image.merge("RGBA", (white, white, white, small))


def contact_sheet(icons):
    cols = 7
    cell_w, cell_h = 150, 132
    rows = (len(icons) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cell_w + 20, rows * cell_h + 56), (33, 36, 40, 255))
    d = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.load_default(size=13)
        title = ImageFont.load_default(size=16)
    except TypeError:
        font = title = ImageFont.load_default()
    d.text((12, 12), "Rimfolk interface icons (64 px, then 24 px and 16 px as tinted in game)", fill=(230, 230, 230), font=title)
    tints = [(255, 255, 255), (255, 217, 128), (140, 230, 140)]
    for i, (name, tex) in enumerate(icons):
        cx = 10 + (i % cols) * cell_w
        cy = 44 + (i // cols) * cell_h
        d.rounded_rectangle([cx + 2, cy + 2, cx + cell_w - 6, cy + cell_h - 6], radius=6, fill=(44, 48, 53, 255))
        sheet.alpha_composite(tex, (cx + 10, cy + 12))
        for j, (size, tint) in enumerate(((24, tints[1]), (16, tints[2]))):
            small = tex.resize((size, size), Image.LANCZOS)
            r, g, b, a = small.split()
            colored = Image.merge("RGBA", (r.point(lambda v: v * tint[0] // 255), g.point(lambda v: v * tint[1] // 255),
                                           b.point(lambda v: v * tint[2] // 255), a))
            sheet.alpha_composite(colored, (cx + 86 + j * 30, cy + 34 - size // 2 + 10))
        d.text((cx + 10, cy + 86), name, fill=(220, 220, 220), font=font)
    return sheet


def main():
    only_preview = "--preview" in sys.argv
    os.makedirs(ICON_DIR, exist_ok=True)
    icons = []
    for name, fn in GLYPHS:
        tex = to_texture(fn())
        icons.append((name, tex))
        if not only_preview:
            tex.save(os.path.join(ICON_DIR, name + ".png"), optimize=True)
    contact_sheet(icons).save(PREVIEW, optimize=True)
    print("%d icons%s; preview: %s" % (len(icons), "" if only_preview else " in " + ICON_DIR, PREVIEW))


if __name__ == "__main__":
    main()
