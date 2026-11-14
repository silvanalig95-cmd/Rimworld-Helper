#!/usr/bin/env python3
"""
Draws the gods' signs, the colony's own holy sign for each of the fourteen (docs/gods-next-proposals.md, section A):
what its people draw for the god they believe in, never a portrait of the god. In the game (Gods/GodSigns.cs) each
shows in the god's chapter of the colony's book and on its card when the colony is sure, in the form of the colony's
age: scratched on a stone (tribal), painted on a shield (medieval), stamped on a badge (industrial), lit in a ring
(spacer); the plain sign, in the god's colours, beside the chapter's name in the book's list.

    Hollin     a tree whose roots are as wide as its crown      Pip       a spark inside a small cog
    Smoulder   a mountain with a coil asleep under it           Cirrus    an eye over drifting cloud, a star apart
    Bellwether a single spiralled horn over grass               Hush      a frost star on a tall thin standing body
    Morel      a mushroom whose threads run down into the earth Undertow  a whirl pulling down into a hole
    Ennu       an open door with a light left in it             Helios    a sun whose rays all point outward
    Selene     a crescent holding a small full disc             Mog       a seated cat facing you, eyes cut bright
    Murmur     a toad over ripples, rain falling                Ducat     a holed coin, moving

Each is drawn as a mask at 4x (512x512, in design units of 128) and scaled down, which gives clean edges; the shapes are
bold, with cutouts wide enough to read at 24 px.

Usage (from the repository root):
    python3 Tools/make_god_signs.py                  # writes the sheet, docs/gods-symbols-draft.png
    python3 Tools/make_god_signs.py --textures [DIR] # writes the game's textures instead, 128x128 RGBA, to DIR
                                                     # (default Textures/ColonyCouncil/Gods/Signs): <Key>.png, the plain
                                                     # sign in the god's colours, and <Key>_<Age>.png for Tribal,
                                                     # Medieval, Industrial and Spacer, each age's form
    python3 Tools/make_god_signs.py --textures [DIR] --only Mog   # one god's five

Needs Pillow (pip install pillow).
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

K = 4  # drawn at 4x
U = 128  # design units
S = U * K
OUT = 128
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHEET = os.path.join(ROOT, "docs", "gods-symbols-draft.png")
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
        for x, y in (pts[0], pts[-1]):
            circle(m, x, y, w / 2.0, fill)


def curve(m, pts, w, fill=ON, steps=24):
    """A smooth line through the points (Catmull-Rom), round at both ends."""
    if len(pts) < 3:
        line(m, pts, w, fill)
        return
    out = []
    ext = [pts[0]] + list(pts) + [pts[-1]]
    for i in range(1, len(ext) - 2):
        p0, p1, p2, p3 = ext[i - 1], ext[i], ext[i + 1], ext[i + 2]
        for s in range(steps):
            t = s / steps
            t2, t3 = t * t, t * t * t
            x = 0.5 * ((2 * p1[0]) + (-p0[0] + p2[0]) * t + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2 + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3)
            y = 0.5 * ((2 * p1[1]) + (-p0[1] + p2[1]) * t + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2 + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3)
            out.append((x, y))
    out.append(pts[-1])
    line(m, out, w, fill)
    for x, y in out[::4]:
        circle(m, x, y, w / 2.0, fill)


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


def spiral(m, cx, cy, r0, r1, turns, w, squash=1.0, start_deg=0.0, fill=ON):
    pts = []
    n = int(90 * turns)
    for i in range(n + 1):
        t = i / n
        r = r0 + (r1 - r0) * t
        a = math.radians(start_deg) + t * turns * 2 * math.pi
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a) * squash))
    line(m, pts, w, fill)


def almond(m, cx, cy, w, h, fill=ON):
    """An eye's almond: two arcs meeting at points left and right."""
    pts = []
    n = 32
    for i in range(n + 1):
        t = -1 + 2 * i / n
        pts.append((cx + t * w / 2, cy - (1 - t * t) * h / 2))
    for i in range(n, -1, -1):
        t = -1 + 2 * i / n
        pts.append((cx + t * w / 2, cy + (1 - t * t) * h / 2))
    poly(m, pts, fill)


# ---------------------------------------------------------------- the signs

def hollin():
    """A tree whose roots are as wide as its crown: what grows under the world is as large as what grows above it."""
    m = new()
    for cx, cy, r in ((64, 22, 15), (46, 32, 13), (82, 32, 13), (54, 18, 10), (74, 18, 10), (64, 38, 12)):
        circle(m, cx, cy, r)
    rect(m, 59, 36, 69, 74)
    # The ground, and the roots below it as wide as the crown above.
    rect(m, 18, 72, 110, 77, 2)
    for end in ((24, 112), (40, 120), (64, 124), (88, 120), (104, 112)):
        curve(m, [(64, 77), ((64 + end[0]) / 2, 86 + (end[1] - 86) * 0.35), end], 5)
    for a, b in (((40, 120), (30, 124)), ((88, 120), (98, 124)), ((24, 112), (16, 118)), ((104, 112), (112, 118))):
        line(m, [((a[0] * 2 + 64) / 3, (a[1] * 2 + 77) / 3), b], 3)
    return m


def smoulder():
    """A mountain, and under it a coil asleep, its one eye shut."""
    m = new()
    poly(m, [(64, 8), (112, 64), (16, 64)])
    # Snow on the peak, cut as a zigzag.
    poly(m, [(64, 16), (76, 30), (70, 27), (64, 33), (58, 27), (52, 30)], OFF)
    rect(m, 14, 66, 114, 70, 0, OFF)
    spiral(m, 64, 96, 46, 10, 1.75, 6, squash=0.48, start_deg=180)
    # The shut eye at the coil's heart.
    curve(m, [(54, 96), (64, 101), (74, 96)], 3)
    return m


def bellwether():
    """A single spiralled horn rising from grass."""
    m = new()
    pts = []
    n = 30
    for i in range(n + 1):
        t = i / n
        y = 108 - t * 96
        half = 13 * (1 - t) + 1
        x = 64 + 6 * math.sin(t * 2.2)
        pts.append((x - half, y))
    for i in range(n, -1, -1):
        t = i / n
        y = 108 - t * 96
        half = 13 * (1 - t) + 1
        x = 64 + 6 * math.sin(t * 2.2)
        pts.append((x + half, y))
    poly(m, pts)
    # The spiral's grooves.
    for k in range(6):
        t = 0.08 + k * 0.14
        y = 108 - t * 96
        half = 13 * (1 - t) + 1
        x = 64 + 6 * math.sin(t * 2.2)
        line(m, [(x - half - 1, y + 3), (x + half + 1, y - 5)], 2.4, OFF, round_caps=False)
    # Grass at its foot, in tufts.
    for x0 in (30, 44, 84, 98):
        for lean in (-7, 0, 7):
            curve(m, [(x0, 122), (x0 + lean * 0.4, 114), (x0 + lean, 106 + abs(lean) * 0.6)], 3)
    return m


def morel():
    """A mushroom whose threads run on down into the earth."""
    m = new()
    ellipse(m, 22, 14, 106, 78)
    rect(m, 18, 48, 110, 80, 0, OFF)
    for cx, cy, r in ((46, 32, 5), (66, 24, 6), (84, 36, 5)):
        circle(m, cx, cy, r, OFF)
    rect(m, 56, 46, 72, 84, 4)
    # The threads.
    for end in ((26, 108), (42, 120), (64, 124), (86, 120), (102, 108)):
        curve(m, [(64, 84), ((64 * 2 + end[0]) / 3, 92), end], 3)
    for a, b in (((42, 104), (32, 116)), ((86, 104), (96, 116)), ((64, 104), (56, 116)), ((64, 104), (72, 116))):
        line(m, [a, b], 2.2)
    return m


def ennu():
    """An open door with a light left in it for whoever comes."""
    m = new()
    circle(m, 58, 46, 30)
    rect(m, 28, 46, 88, 120)
    circle(m, 58, 48, 22, OFF)
    rect(m, 36, 48, 80, 120, 0, OFF)
    # The door leaf, swung open.
    poly(m, [(90, 26), (110, 36), (110, 118), (90, 120)])
    circle(m, 96, 76, 2.5, OFF)
    # The light on the floor inside.
    leaf(m, 58, 92, 26, 13, -90)
    rect(m, 52, 104, 64, 110, 1)
    return m


def pip():
    """A spark inside a small cog."""
    m = new()
    circle(m, 64, 64, 40)
    for i in range(8):
        a = i * 45
        t = Image.new("L", (S, S), 0)
        rect(t, 56, 10, 72, 30, 2)
        t = t.rotate(-a, resample=Image.BICUBIC, center=(S / 2, S / 2))
        m = Image.composite(Image.new("L", (S, S), 255), m, t)
    circle(m, 64, 64, 28, OFF)
    poly(m, [(70, 38), (50, 68), (62, 68), (54, 92), (78, 58), (66, 58), (76, 38)])
    return m


def cirrus():
    """An eye over drifting cloud, and a star apart from it."""
    m = new()
    almond(m, 60, 46, 84, 40)
    almond(m, 60, 46, 70, 28, OFF)
    circle(m, 60, 46, 11)
    circle(m, 63, 43, 3, OFF)
    for y, x0, x1 in ((84, 22, 102), (100, 32, 112), (116, 18, 86)):
        pts = [(x0 + (x1 - x0) * i / 8, y + 3 * math.sin(i * 1.4)) for i in range(9)]
        curve(m, pts, 5)
    star(m, 110, 14, 10, 3, 4)
    return m


def hush():
    """A frost star on a tall thin standing body."""
    m = new()
    cx, cy, r = 64, 34, 26
    for i in range(6):
        a = math.radians(-90 + i * 60)
        ex, ey = cx + r * math.cos(a), cy + r * math.sin(a)
        if i == 3:
            continue
        line(m, [(cx, cy), (ex, ey)], 5)
        for s in (-1, 1):
            b = a + s * math.radians(40)
            mx, my = cx + r * 0.6 * math.cos(a), cy + r * 0.6 * math.sin(a)
            line(m, [(mx, my), (mx + 9 * math.cos(b), my + 9 * math.sin(b))], 3)
    circle(m, cx, cy, 7)
    # The body: the lower arm drawn on into a tall, thin figure in a long robe, standing still.
    poly(m, [(60, 42), (68, 42), (72, 64), (80, 122), (48, 122), (56, 64)])
    line(m, [(64, 70), (64, 118)], 2, OFF, round_caps=False)
    return m


def undertow():
    """A whirl pulling down into a hole."""
    m = new()
    spiral(m, 64, 64, 50, 6, 2.6, 6, squash=1.0, start_deg=-90)
    circle(m, 64, 64, 9)
    circle(m, 64, 64, 5, OFF)
    return m


def helios():
    """A sun whose rays all point outward: it gives, and never looks back."""
    m = new()
    circle(m, 64, 64, 22)
    for i in range(12):
        a = math.radians(i * 30)
        r0, r1 = 29, 58 if i % 2 == 0 else 46
        w = 7 if i % 2 == 0 else 5
        ux, uy = math.cos(a), math.sin(a)
        vx, vy = -uy, ux
        poly(m, [(64 + ux * r0 + vx * w, 64 + uy * r0 + vy * w), (64 + ux * r1, 64 + uy * r1),
                 (64 + ux * r0 - vx * w, 64 + uy * r0 - vy * w)])
    return m


def selene():
    """A crescent holding a small full disc: she has no light but what she gives back."""
    m = new()
    circle(m, 60, 64, 48)
    circle(m, 80, 56, 42, OFF)
    circle(m, 84, 60, 13)
    return m


def mog():
    """A seated cat facing you, its eyes cut bright."""
    m = new()
    ellipse(m, 34, 54, 94, 124)
    rect(m, 30, 112, 98, 124, 0, OFF)
    rect(m, 36, 108, 92, 120, 5)
    circle(m, 64, 40, 21)
    poly(m, [(45, 34), (44, 10), (60, 22)])
    poly(m, [(83, 34), (84, 10), (68, 22)])
    almond(m, 55, 40, 12, 7, OFF)
    almond(m, 73, 40, 12, 7, OFF)
    rect(m, 54, 38, 56, 42, 0, ON)
    rect(m, 72, 38, 74, 42, 0, ON)
    poly(m, [(61, 49), (67, 49), (64, 53)], OFF)
    # The paws, and the tail round the front.
    line(m, [(64, 104), (64, 120)], 2.4, OFF, round_caps=False)
    curve(m, [(92, 112), (106, 104), (110, 86), (102, 76)], 6)
    return m


def murmur():
    """A toad over ripples, with the rain it sings up falling."""
    m = new()
    ellipse(m, 26, 50, 102, 88)
    circle(m, 44, 50, 11)
    circle(m, 84, 50, 11)
    circle(m, 44, 49, 4.5, OFF)
    circle(m, 84, 49, 4.5, OFF)
    curve(m, [(40, 72), (64, 78), (88, 72)], 3, OFF)
    for r, y in ((44, 104), (30, 116)):
        d = ImageDraw.Draw(m)
        d.arc([(64 - r) * K, (y - r * 0.3) * K, (64 + r) * K, (y + r * 0.3) * K], 0, 180, fill=ON, width=int(4 * K))
    for x, y in ((30, 18), (64, 10), (98, 18), (47, 30), (81, 30)):
        circle(m, x, y + 4, 4)
        poly(m, [(x - 3.6, y + 3), (x, y - 5), (x + 3.6, y + 3)])
    return m


def ducat():
    """A holed coin, moving: gold that will not stay."""
    m = new()
    circle(m, 74, 64, 34)
    ring(m, 74, 64, 27, 2.5, OFF)
    rect(m, 66, 56, 82, 72, 1, OFF)
    for i, (x0, y0, y1) in enumerate(((26, 40, 88), (16, 50, 78), (34, 30, 98))):
        d = ImageDraw.Draw(m)
        r = (y1 - y0) / 2
        d.arc([(x0 - r * 0.5) * K, y0 * K, (x0 + r * 0.5) * K, y1 * K], 110, 250, fill=ON, width=int(4 * K))
    return m


# ---------------------------------------------------------------- the fourteen, in the defs' order

GODS = [
    ("Hollin", "the Rootmother", hollin, (0.55, 0.78, 0.45), (0.62, 0.5, 0.34),
     "The mother of all roots and growing things. She guards the trees and the green, and remembers every tree cut."),
    ("Smoulder", "the Coil Under the Mountain", smoulder, (0.95, 0.56, 0.26), (0.8, 0.78, 0.82),
     "A dragon asleep under the mountains. His dreams push up ore and heat; he loves silver and hates being woken."),
    ("Bellwether", "the Old Horn", bellwether, (0.92, 0.9, 0.82), (0.7, 0.8, 0.62),
     "The first thrumbo, a great white beast that walks the whole world. Let him pass in peace; never hurt his kin."),
    ("Morel", "the Soft Mother", morel, (0.82, 0.72, 0.58), (0.9, 0.88, 0.84),
     "The mother of rot and mushrooms. Everything that dies goes back to her; she hates what is kept from rotting."),
    ("Ennu", "the Guest", ennu, (0.9, 0.78, 0.52), (0.6, 0.68, 0.8),
     "A god who comes to the door as a stranger asking for shelter, and remembers how it was treated."),
    ("Pip", "the Hum", pip, (0.48, 0.86, 0.95), (0.95, 0.85, 0.42),
     "A young spirit living in machines and wires. It mends them when it is fond of you and breaks them when it sulks."),
    ("Cirrus", "Who Drifts Above", cirrus, (0.72, 0.83, 0.98), (0.92, 0.94, 1.0),
     "Something vast in the high sky that hardly ever looks down. Storms, stars and falling rocks are its moods."),
    ("Hush", "the Long White", hush, (0.84, 0.92, 0.98), (0.6, 0.66, 0.8),
     "The spirit of winter, cold and silence. It takes the weak first and spares those who prepare."),
    ("Undertow", "the Hunger", undertow, (0.62, 0.44, 0.78), (0.32, 0.26, 0.4),
     "Something in the dark under the world. It grants the desperate whatever they ask, and always takes more back."),
    ("Helios", "the Giver", helios, (0.98, 0.78, 0.38), (0.95, 0.55, 0.28),
     "The sun. He gives light, warmth and harvest to everyone alike, and sometimes far too much."),
    ("Selene", "the Mirror", selene, (0.8, 0.84, 0.98), (0.92, 0.92, 1.0),
     "The moon. At night she gives back what people did by day, good or bad, as much as her face is full."),
    ("Mog", "the Cat", mog, (0.78, 0.8, 0.84), (0.95, 0.88, 0.45),
     "A gray cat who is a god. She chooses one person to bring luck to, and leaves if anyone harms a cat."),
    ("Murmur", "Old Croak", murmur, (0.48, 0.74, 0.6), (0.55, 0.62, 0.42),
     "A huge old toad in the wells and marshes. When it sings, it rains; when it falls silent, the land goes dry."),
    ("Ducat", "the Restless Gold", ducat, (0.96, 0.8, 0.32), (0.72, 0.56, 0.24),
     "The spirit in gold that wants to keep moving. It loves trade and giving, and hates a hoard."),
]


# ---------------------------------------------------------------- output

def small(mask, size):
    return mask.resize((size, size), Image.LANCZOS)


def tinted(mask, size, a, b):
    """The sign at a size in the god's colour, a soft glow of its second colour behind it (as on a dark card in game)."""
    m = small(mask, size)
    glow = m.filter(ImageFilter.GaussianBlur(size / 22))
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    gl = Image.new("RGBA", (size, size), tuple(int(c * 255) for c in b) + (0,))
    gl.putalpha(glow.point(lambda v: int(v * 0.55)))
    out = Image.alpha_composite(out, gl)
    fg = Image.new("RGBA", (size, size), tuple(int(c * 255) for c in a) + (0,))
    fg.putalpha(m)
    return Image.alpha_composite(out, fg)


def font(names, size):
    for n in names:
        for d in ("/usr/share/fonts/truetype/liberation", "/usr/share/fonts/truetype/dejavu", "/usr/share/fonts/TTF", "C:/Windows/Fonts"):
            p = os.path.join(d, n)
            if os.path.exists(p):
                return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def wrap(draw, text, f, width):
    words, lines, cur = text.split(), [], ""
    for w in words:
        t = (cur + " " + w).strip()
        if draw.textlength(t, font=f) <= width:
            cur = t
        else:
            lines.append(cur)
            cur = w
    if cur:
        lines.append(cur)
    return lines


PAPER = (24, 21, 18)
CARD = (36, 31, 26)
EDGE = (92, 76, 52)
INK = (226, 214, 190)
DIM = (160, 148, 128)


def frame_age(mask, age, size, colour):
    """One sign in an age's own form: scratched on a stone, painted on a shield, stamped on a badge, lit in a ring."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = tuple(int(x * 255) for x in colour)
    inner = int(size * 0.62)
    off = (size - inner) // 2
    if age == 0:
        pts = [(size * x, size * y) for x, y in ((0.18, 0.12), (0.62, 0.05), (0.9, 0.3), (0.94, 0.7), (0.66, 0.95), (0.22, 0.9), (0.06, 0.55))]
        d.polygon(pts, fill=(98, 92, 84, 255), outline=(60, 56, 50, 255))
        sign = small(mask, inner)
        scratched = Image.new("RGBA", (inner, inner), (214, 206, 190, 0))
        scratched.putalpha(sign)
        img.alpha_composite(scratched, (off, off))
    elif age == 1:
        w = size * 0.8
        x0 = (size - w) / 2
        d.polygon([(x0, size * 0.1), (x0 + w, size * 0.1), (x0 + w, size * 0.5), (size / 2, size * 0.95), (x0, size * 0.5)],
                  fill=(118, 36, 30, 255), outline=(200, 170, 90, 255), width=max(2, size // 40))
        img.alpha_composite(tinted(mask, inner, (0.93, 0.84, 0.55), (0.6, 0.4, 0.2)), (off, off - size // 16))
    elif age == 2:
        pts = [(size / 2 + size * 0.45 * math.cos(math.radians(22.5 + 45 * i)), size / 2 + size * 0.45 * math.sin(math.radians(22.5 + 45 * i))) for i in range(8)]
        d.polygon(pts, fill=(70, 72, 74, 255), outline=(150, 150, 146, 255), width=max(2, size // 36))
        img.alpha_composite(tinted(mask, inner, (0.86, 0.86, 0.82), (0.3, 0.3, 0.3)), (off, off))
    else:
        r = size * 0.44
        d.ellipse([size / 2 - r, size / 2 - r, size / 2 + r, size / 2 + r], outline=c + (255,), width=max(2, size // 30))
        d.ellipse([size / 2 - r * 0.86, size / 2 - r * 0.86, size / 2 + r * 0.86, size / 2 + r * 0.86], outline=c + (110,), width=1)
        glowing = tinted(mask, inner, (0.7, 0.95, 1.0), (0.3, 0.8, 1.0))
        img.alpha_composite(glowing, (off, off))
    return img


def sheet(path):
    cols, cw, ch, gap, margin, head, foot = 4, 300, 350, 18, 30, 150, 70
    rows = 4
    W = margin * 2 + cols * cw + (cols - 1) * gap
    H = head + rows * ch + (rows - 1) * gap + foot
    img = Image.new("RGBA", (W, H), PAPER + (255,))
    d = ImageDraw.Draw(img)
    f_title = font(["LiberationSerif-Bold.ttf", "DejaVuSerif-Bold.ttf"], 42)
    f_sub = font(["LiberationSerif-Italic.ttf", "DejaVuSerif.ttf"], 20)
    f_name = font(["LiberationSerif-Bold.ttf", "DejaVuSerif-Bold.ttf"], 30)
    f_epi = font(["LiberationSerif-Italic.ttf", "DejaVuSerif.ttf"], 18)
    f_text = font(["LiberationSans-Regular.ttf", "DejaVuSans.ttf"], 16)
    d.text((margin, 34), "The gods' signs (draft)", font=f_title, fill=INK)
    d.text((margin, 90), "Each god's holy sign as the colony's people draw it, and what the colony takes the god to be. "
           "Shown only for a god the colony has heard of.", font=f_sub, fill=DIM)
    cells = [(g, None) for g in GODS] + [(None, "ages")]
    for i, (g, extra) in enumerate(cells):
        r, c = divmod(i, cols)
        x = margin + c * (cw + gap)
        y = head + r * (ch + gap)
        if extra == "ages":
            w2 = cw * 2 + gap
            d.rounded_rectangle([x, y, x + w2, y + ch], radius=10, fill=CARD, outline=EDGE, width=2)
            d.text((x + 20, y + 16), "One sign, every age: Hollin's", font=f_name, fill=INK)
            d.text((x + 20, y + 56), "The same sign takes the colony's own means and the age's ways.", font=f_epi, fill=DIM)
            labels = ["scratched on a stone", "painted on a shield", "stamped on a badge", "lit in a ring"]
            ages = ["tribal", "medieval", "industrial", "spacer"]
            hm = hollin()
            fs = 120
            for k in range(4):
                fx = x + 22 + k * (fs + 24)
                fy = y + 104
                img.alpha_composite(frame_age(hm, k, fs, GODS[0][3]), (int(fx), int(fy)))
                tw = d.textlength(ages[k], font=f_epi)
                d.text((fx + (fs - tw) / 2, fy + fs + 12), ages[k], font=f_epi, fill=INK)
                for j, ln in enumerate(wrap(d, labels[k], f_text, fs + 16)):
                    tw = d.textlength(ln, font=f_text)
                    d.text((fx + (fs - tw) / 2, fy + fs + 36 + j * 19), ln, font=f_text, fill=DIM)
            continue
        key, epithet, fn, a, b, meaning = g
        d.rounded_rectangle([x, y, x + cw, y + ch], radius=10, fill=CARD, outline=EDGE, width=2)
        sign = tinted(fn(), 150, a, b)
        img.alpha_composite(sign, (x + (cw - 150) // 2, y + 16))
        tw = d.textlength(key, font=f_name)
        d.text((x + (cw - tw) / 2, y + 176), key, font=f_name, fill=tuple(int(min(1.0, v * 1.08) * 255) for v in a))
        tw = d.textlength(epithet, font=f_epi)
        d.text((x + (cw - tw) / 2, y + 212), epithet, font=f_epi, fill=DIM)
        for j, ln in enumerate(wrap(d, meaning, f_text, cw - 34)):
            tw = d.textlength(ln, font=f_text)
            d.text((x + (cw - tw) / 2, y + 246 + j * 20), ln, font=f_text, fill=INK)
    d.text((margin, H - foot + 22), "Drawn by Tools/make_god_signs.py. Not in the game yet: a proposal (docs/gods-next-proposals.md, section A).",
           font=f_text, fill=DIM)
    img.convert("RGB").save(path, optimize=True)


AGES = ["Tribal", "Medieval", "Industrial", "Spacer"]


def textures(out_dir, only=None):
    """Each god's five textures for the game (Gods/GodSigns.cs reads them by these names): the plain sign in its colours
    with a soft glow of its second, and its four age forms as the sheet shows them."""
    os.makedirs(out_dir, exist_ok=True)
    n = 0
    for key, _e, fn, a, b, _meaning in GODS:
        if only and key not in only:
            continue
        mask = fn()
        tinted(mask, OUT, a, b).save(os.path.join(out_dir, key + ".png"), optimize=True)
        for k, age in enumerate(AGES):
            frame_age(mask, k, OUT, a).save(os.path.join(out_dir, key + "_" + age + ".png"), optimize=True)
        n += 1
    return n


def main(argv):
    if "--textures" in argv:
        i = argv.index("--textures")
        nxt = argv[i + 1] if i + 1 < len(argv) else None
        out = nxt if nxt and not nxt.startswith("--") else os.path.join(ROOT, "Textures", "ColonyCouncil", "Gods", "Signs")
        only = None
        if "--only" in argv:
            j = argv.index("--only")
            only = set(argv[j + 1].split(",")) if j + 1 < len(argv) else None
        n = textures(out, only)
        print("wrote the signs of", n, "gods (", n * (1 + len(AGES)), "textures ) to", os.path.relpath(out, ROOT))
        return
    sheet(SHEET)
    print("wrote", os.path.relpath(SHEET, ROOT))


if __name__ == "__main__":
    main(sys.argv[1:])
