#!/usr/bin/env python3
"""Draws a keepsake of a god's moment (Textures/ColonyCouncil/Things/GodKeepsake.png, 128x128) in the game's manner: flat
fills with a little shading under a dark outline, seen from a little above. A smooth river stone with a small hole worn
through it, a feather and a short twist of cord tied through the hole, the kind of small thing people keep to remember a
day by; no holy sign of any real faith. Drawn at four times the size and scaled down for smooth edges.
Run: python3 Tools/make_god_keepsake.py"""
import os
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
S = 4
W = 128 * S
OUT = os.path.join(ROOT, "Textures", "ColonyCouncil", "Things", "GodKeepsake.png")

LINE = (34, 30, 28, 255)
STONE = (138, 136, 130, 255)
STONE_DARK = (104, 102, 98, 255)
STONE_LIGHT = (178, 176, 168, 255)
HOLE = (52, 48, 46, 255)
CORD = (164, 124, 70, 255)
CORD_DARK = (118, 86, 46, 255)
FEATHER = (226, 222, 208, 255)
FEATHER_DARK = (170, 164, 150, 255)
QUILL = (120, 104, 84, 255)
OL = 7


def S4(*v):
    return [x * S for x in v]


def main():
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # The feather, lying behind the stone: a soft vane, wider on one side, on a thin quill, a few barbs drawn in.
    import math
    ax, ay, bx, by = 22, 96, 50, 14
    length = math.hypot(bx - ax, by - ay)
    ux, uy = (bx - ax) / length, (by - ay) / length
    nx, ny = -uy, ux
    left, right = [], []
    for i in range(0, 21):
        t = i / 20
        w = math.sin(math.pi * min(1, t * 1.15)) if t > 0.08 else 0
        cx, cy = ax + ux * length * t, ay + uy * length * t
        left.append((cx + nx * 15 * w, cy + ny * 15 * w))
        right.append((cx - nx * 11 * w, cy - ny * 11 * w))
    vane = left + right[::-1]
    d.polygon([(x * S, y * S) for x, y in vane], fill=LINE)
    inner = [(ax + (x - ax) * 0.94 + ux * 1.5, ay + (y - ay) * 0.94 + uy * 1.5) for x, y in vane]
    d.polygon([(x * S, y * S) for x, y in inner], fill=FEATHER)
    shade = [(ax + (x - ax) * 0.94 + ux * 1.5, ay + (y - ay) * 0.94 + uy * 1.5) for x, y in right[::-1]] + [(ax + ux * length * t, ay + uy * length * t) for t in [i / 20 for i in range(1, 20)]]
    d.polygon([(x * S, y * S) for x, y in shade], fill=FEATHER_DARK)
    for t in (0.3, 0.45, 0.6, 0.75):
        cx, cy = ax + ux * length * t, ay + uy * length * t
        d.line([(cx * S, cy * S), ((cx + nx * 9 + ux * 4) * S, (cy + ny * 9 + uy * 4) * S)], fill=FEATHER_DARK, width=S)
    d.line(S4(ax, ay, bx, by), fill=QUILL, width=2 * S)
    # The stone: a flattened oval, darker below, light on its upper left.
    d.ellipse(S4(40, 52, 108, 104), fill=LINE)
    d.ellipse([40 * S + OL, 52 * S + OL, 108 * S - OL, 104 * S - OL], fill=STONE_DARK)
    d.ellipse(S4(46, 56, 104, 96), fill=STONE)
    d.ellipse(S4(52, 60, 82, 76), fill=STONE_LIGHT)
    # The hole worn through it.
    d.ellipse(S4(80, 70, 94, 82), fill=LINE)
    d.ellipse(S4(82, 72, 92, 80), fill=HOLE)
    # The cord through the hole, its ends hanging.
    d.line(S4(87, 76, 98, 92, 92, 112), fill=LINE, width=5 * S, joint="curve")
    d.line(S4(87, 76, 98, 92, 92, 112), fill=CORD, width=3 * S, joint="curve")
    d.line(S4(87, 76, 106, 88, 112, 106), fill=LINE, width=5 * S, joint="curve")
    d.line(S4(87, 76, 106, 88, 112, 106), fill=CORD_DARK, width=3 * S, joint="curve")
    d.ellipse(S4(89, 108, 95, 114), fill=LINE)
    d.ellipse(S4(109, 102, 115, 108), fill=LINE)
    out = img.resize((128, 128), Image.LANCZOS)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    out.save(OUT)
    print("Wrote", os.path.relpath(OUT, ROOT))


if __name__ == "__main__":
    main()
