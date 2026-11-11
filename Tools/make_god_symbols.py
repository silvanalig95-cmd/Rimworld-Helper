#!/usr/bin/env python3
"""
Draws the gods' symbols (Source/ColonyCouncil/Gods/GodSymbols.cs): one per domain, and the pantheon's own, white on
transparent, 128x128 RGBA, tinted in a god's colours in game.

    Harvest  a sheaf of grain, bound at the waist        Way      a road to the horizon under a star
    Storm    a lightning bolt                            Dead     a skull, a lantern hung above it
    Hunt     a pair of antlers                           Night    a crescent moon and a star
    Healing  an open hand with a leaf on its palm        Hearth   a flame
    War      two crossed blades                          Wild     a tree with its roots
    Craft    a hammer                                    Waters   three waves
    Machine  a cog                                       Void     an eye in a ring
    Pantheon a small temple (the Gods page)

Each is drawn as a mask at 4x (512x512, in design units of 128) and scaled down, which gives clean edges; the shapes are
bold with cutouts wide enough to read at 24 px on screen, the size of a row on the Gods page.

Usage (from the repository root):
    python3 Tools/make_god_symbols.py                 # writes Textures/ColonyCouncil/GodSymbols/*.png
    python3 Tools/make_god_symbols.py --preview PATH  # also a contact sheet at 128, 48 and 24 px, tinted

Needs Pillow (pip install pillow).
"""
import math
import os
import sys

from PIL import Image, ImageDraw

K = 4  # drawn at 4x
U = 128  # design units
S = U * K
OUT = 128
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIR = os.path.join(ROOT, "Textures", "ColonyCouncil", "GodSymbols")
ON, OFF = 255, 0


# ---------------------------------------------------------------- primitives (design units)

def new():
    return Image.new("L", (S, S), 0)


def _p(pts):
    return [(float(x) * K, float(y) * K) for x, y in pts]


def poly(m, pts, fill=ON):
    ImageDraw.Draw(m).polygon(_p(pts), fill=fill)


def circle(m, cx, cy, r, fill=ON):
    ImageDraw.Draw(m).ellipse([(cx - r) * K, (cy - r) * K, (cx + r) * K, (cy + r) * K], fill=fill)


def ellipse(m, x0, y0, x1, y1, fill=ON):
    ImageDraw.Draw(m).ellipse([x0 * K, y0 * K, x1 * K, y1 * K], fill=fill)


def ring(m, cx, cy, r, w, fill=ON):
    ImageDraw.Draw(m).ellipse([(cx - r) * K, (cy - r) * K, (cx + r) * K, (cy + r) * K], outline=fill, width=int(w * K))


def rect(m, x0, y0, x1, y1, r=0, fill=ON):
    d = ImageDraw.Draw(m)
    box = [x0 * K, y0 * K, x1 * K, y1 * K]
    if r > 0:
        d.rounded_rectangle(box, radius=r * K, fill=fill)
    else:
        d.rectangle(box, fill=fill)


def line(m, pts, w, fill=ON, round_caps=True):
    d = ImageDraw.Draw(m)
    d.line(_p(pts), fill=fill, width=int(w * K), joint="curve")
    if round_caps:
        for x, y in pts:
            circle(m, x, y, w / 2.0, fill)


def rotated(m, deg):
    return m.rotate(deg, resample=Image.BICUBIC, center=(S / 2, S / 2))


def union(*ms):
    out = new()
    for m in ms:
        out = Image.composite(Image.new("L", (S, S), 255), out, m)
    return out


def leaf(m, cx, cy, length, width, angle_deg, fill=ON):
    """A pointed leaf (two arcs) centred on cx, cy, turned by angle."""
    a = math.radians(angle_deg)
    ux, uy = math.cos(a), math.sin(a)
    vx, vy = -uy, ux
    pts = []
    n = 24
    for i in range(n + 1):
        t = -1 + 2 * i / n
        w = width / 2 * (1 - t * t)
        pts.append((cx + ux * t * length / 2 + vx * w, cy + uy * t * length / 2 + vy * w))
    for i in range(n, -1, -1):
        t = -1 + 2 * i / n
        w = width / 2 * (1 - t * t)
        pts.append((cx + ux * t * length / 2 - vx * w, cy + uy * t * length / 2 - vy * w))
    poly(m, pts, fill)


def star(m, cx, cy, r_out, r_in, points=4, rot=-90, fill=ON):
    pts = []
    for i in range(points * 2):
        r = r_out if i % 2 == 0 else r_in
        a = math.radians(rot + i * 180 / points)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    poly(m, pts, fill)


# ---------------------------------------------------------------- the symbols

def harvest():
    m = new()
    # The stalks fan out above and below the band.
    for dx_top, dx_bot in ((-30, -26), (-15, -12), (0, 0), (15, 12), (30, 26)):
        line(m, [(64 + dx_bot, 116), (64, 72), (64 + dx_top * 0.75, 38)], 6)
    # The ears of grain, a leaf shape at each stalk's top.
    for dx, ang in ((-30, -112), (-15, -100), (0, -90), (15, -80), (30, -68)):
        x = 64 + dx * 0.75
        a = math.radians(ang)
        leaf(m, x + math.cos(a) * 14, 38 + math.sin(a) * 14, 34, 14, ang)
    # The band at the waist.
    rect(m, 48, 66, 80, 80, 4)
    rect(m, 52, 69, 76, 77, 3, OFF)
    rect(m, 52, 69, 76, 77, 0, ON)
    return m


def storm():
    m = new()
    poly(m, [(78, 6), (34, 70), (60, 70), (44, 122), (96, 50), (68, 50), (88, 6)])
    return m


def hunt():
    m = new()
    for side in (-1, 1):
        cx = 64
        base = (cx + side * 10, 92)
        beam = [(cx + side * 10, 92), (cx + side * 26, 70), (cx + side * 36, 46), (cx + side * 40, 18)]
        line(m, beam, 9)
        line(m, [(cx + side * 27, 69), (cx + side * 50, 62)], 7)
        line(m, [(cx + side * 34, 50), (cx + side * 56, 36)], 7)
        line(m, [(cx + side * 38, 32), (cx + side * 24, 14)], 7)
        line(m, [base, (cx + side * 4, 104)], 9)
    # The brow between them.
    ellipse(m, 50, 94, 78, 120)
    return m


def healing():
    m = new()
    # The palm and wrist.
    rect(m, 38, 58, 92, 104, 16)
    rect(m, 48, 98, 82, 124, 6)
    # Four fingers and the thumb.
    for x, top in ((40, 26), (54, 16), (68, 18), (82, 30)):
        rect(m, x - 6, top, x + 6, 70, 6)
    line(m, [(40, 86), (22, 64), (16, 50)], 12)
    # A leaf on the palm, cut out, with its vein left.
    leaf(m, 65, 76, 40, 20, -45, OFF)
    line(m, [(52, 89), (76, 65)], 3, ON)
    return m


def war():
    def blade():
        b = new()
        poly(b, [(64, 4), (73, 18), (73, 84), (55, 84), (55, 18)])
        rect(b, 36, 84, 92, 94, 4)
        rect(b, 59, 94, 69, 112, 3)
        circle(b, 64, 117, 8)
        return b
    return union(rotated(blade(), 40), rotated(blade(), -40))


def craft():
    # A hammer upright, its head square across the handle's end, then leaned.
    m = new()
    rect(m, 57, 44, 71, 124, 6)
    rect(m, 28, 14, 100, 46, 6)
    rect(m, 22, 18, 34, 42, 3)
    rect(m, 94, 18, 106, 42, 3)
    return rotated(m, -32)


def way():
    m = new()
    # The road to the horizon.
    poly(m, [(18, 124), (110, 124), (72, 52), (56, 52)])
    for y0, y1 in ((112, 124), (88, 100), (68, 77)):
        f0 = (124 - y0) / 72
        f1 = (124 - y1) / 72
        w0 = 4 * (1 - f0) + 1.5 * f0
        w1 = 4 * (1 - f1) + 1.5 * f1
        poly(m, [(64 - w1, y0), (64 + w1, y0), (64 + w0, y1), (64 - w0, y1)], OFF)
    # The star over it.
    star(m, 64, 22, 20, 6, 4)
    return m


def dead():
    m = new()
    # The lantern hung above, on its hook.
    line(m, [(92, 6), (92, 14)], 4)
    ring(m, 92, 16, 5, 3)
    rect(m, 82, 20, 102, 24, 2)
    rect(m, 84, 24, 100, 46, 3)
    rect(m, 88, 28, 96, 42, 2, OFF)
    circle(m, 92, 35, 3)
    rect(m, 82, 46, 102, 50, 2)
    # The skull.
    circle(m, 56, 78, 34)
    rect(m, 38, 92, 74, 122, 8)
    circle(m, 43, 76, 9, OFF)
    circle(m, 69, 76, 9, OFF)
    poly(m, [(56, 86), (51, 98), (61, 98)], OFF)
    for x in (44, 52, 60, 68):
        rect(m, x - 1.5, 108, x + 1.5, 122, 0, OFF)
    return m


def night():
    m = new()
    circle(m, 58, 66, 50)
    circle(m, 80, 54, 44, OFF)
    star(m, 98, 92, 15, 5, 4)
    return m


def spline(pts, steps=10):
    """A closed Catmull-Rom curve through the points, for smooth outlines."""
    out = []
    n = len(pts)
    for i in range(n):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[(i + 1) % n], pts[(i + 2) % n]
        for s in range(steps):
            u = s / steps
            u2, u3 = u * u, u * u * u
            x = 0.5 * (2 * p1[0] + (-p0[0] + p2[0]) * u + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * u2 + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * u3)
            y = 0.5 * (2 * p1[1] + (-p0[1] + p2[1]) * u + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * u2 + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * u3)
            out.append((x, y))
    return out


def hearth():
    m = new()
    # A flame: round at the foot, two licks at its sides, a tall tip leaning a little.
    outer = [(64, 124), (40, 119), (25, 102), (23, 80), (31, 60), (42, 46), (44, 28), (55, 40), (60, 26), (68, 4), (76, 24), (82, 38),
             (90, 26), (99, 48), (105, 74), (102, 100), (88, 118)]
    poly(m, spline(outer))
    inner = [(64, 120), (51, 115), (45, 101), (49, 86), (57, 74), (63, 58), (70, 72), (79, 86), (83, 101), (77, 115)]
    poly(m, spline(inner), OFF)
    return m


def wild():
    m = new()
    # The crown: three rounds.
    circle(m, 64, 38, 30)
    circle(m, 40, 56, 24)
    circle(m, 88, 56, 24)
    circle(m, 64, 62, 24)
    # The trunk and its roots.
    rect(m, 56, 62, 72, 108, 3)
    line(m, [(60, 104), (40, 122)], 9)
    line(m, [(68, 104), (88, 122)], 9)
    line(m, [(64, 106), (64, 124)], 9)
    # A notch of light in the crown.
    circle(m, 52, 40, 6, OFF)
    return m


def waters():
    m = new()
    for y in (36, 64, 92):
        pts = []
        for i in range(41):
            x = 12 + i * 104 / 40
            pts.append((x, y + 9 * math.sin((x - 12) / 104 * math.pi * 2.0)))
        line(m, pts, 12)
    return m


def machine():
    m = new()
    cx, cy = 64, 64
    teeth = 10
    for i in range(teeth):
        a = math.radians(i * 360 / teeth)
        b = new()
        rect(b, 56, 4, 72, 30, 3)
        m = union(m, b.rotate(-math.degrees(a), resample=Image.BICUBIC, center=(S / 2, S / 2)))
    circle(m, cx, cy, 44)
    circle(m, cx, cy, 18, OFF)
    return m


def void():
    m = new()
    ring(m, 64, 64, 56, 10)
    # The eye: an almond, its iris, the pupil cut out.
    pts = []
    for i in range(41):
        t = -1 + 2 * i / 40
        pts.append((64 + t * 42, 64 - 22 * (1 - t * t)))
    for i in range(40, -1, -1):
        t = -1 + 2 * i / 40
        pts.append((64 + t * 42, 64 + 22 * (1 - t * t)))
    poly(m, pts)
    circle(m, 64, 64, 15, OFF)
    circle(m, 64, 64, 8)
    return m


def pantheon():
    m = new()
    # The pediment, the columns, the steps.
    poly(m, [(10, 40), (64, 8), (118, 40)])
    rect(m, 14, 42, 114, 50, 2)
    for x in (26, 50, 78, 102):
        rect(m, x - 7, 54, x + 7, 104, 2)
    rect(m, 12, 106, 116, 114, 2)
    rect(m, 6, 116, 122, 124, 2)
    return m


SYMBOLS = {
    "Harvest": harvest, "Storm": storm, "Hunt": hunt, "Healing": healing, "War": war, "Craft": craft, "Way": way,
    "Dead": dead, "Night": night, "Hearth": hearth, "Wild": wild, "Waters": waters, "Machine": machine, "Void": void,
    "Pantheon": pantheon,
}


def texture(mask):
    small = mask.resize((OUT, OUT), Image.LANCZOS)
    white = Image.new("L", (OUT, OUT), 255)
    return Image.merge("RGBA", (white, white, white, small))


def preview(path, made):
    colours = {
        "Harvest": (224, 184, 82), "Storm": (122, 148, 204), "Hunt": (92, 143, 82), "Healing": (235, 240, 230), "War": (199, 56, 51),
        "Craft": (209, 133, 66), "Way": (204, 179, 133), "Dead": (219, 217, 199), "Night": (82, 97, 179), "Hearth": (245, 148, 71),
        "Wild": (107, 184, 82), "Waters": (71, 158, 184), "Machine": (117, 199, 224), "Void": (102, 51, 133), "Pantheon": (230, 200, 120),
    }
    sizes = (128, 48, 24)
    cell = 140
    sheet = Image.new("RGBA", (cell * len(made), cell + 60 + 30), (34, 34, 38, 255))
    for i, (name, tex) in enumerate(made):
        tint = Image.new("RGBA", tex.size, colours[name] + (255,))
        tinted = Image.composite(tint, Image.new("RGBA", tex.size, (0, 0, 0, 0)), tex.split()[3])
        x = i * cell + 6
        sheet.alpha_composite(tinted, (x, 6))
        sheet.alpha_composite(tinted.resize((48, 48), Image.LANCZOS), (x, cell + 4))
        sheet.alpha_composite(tinted.resize((24, 24), Image.LANCZOS), (x + 60, cell + 16))
    sheet.save(path)
    print("wrote", path)


def main():
    os.makedirs(DIR, exist_ok=True)
    made = []
    for name, fn in SYMBOLS.items():
        tex = texture(fn())
        tex.save(os.path.join(DIR, name + ".png"))
        made.append((name, tex))
    print("wrote", len(made), "symbols to", DIR)
    if "--preview" in sys.argv:
        i = sys.argv.index("--preview")
        preview(sys.argv[i + 1], made)


if __name__ == "__main__":
    main()
