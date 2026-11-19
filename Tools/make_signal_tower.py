#!/usr/bin/env python3
"""Draws the Signal Line's flag-and-lamp tower (Textures/ColonyCouncil/Things/SignalTower.png, 128x256, for
Defs/Misc/Rimfolk_PeopleEvents7_Misc.xml), the tower a Line of the tribal or medieval age raises instead of a radio mast, in the
game's manner: flat fills with a little shading under a dark outline, seen from a little above. Two wooden legs lean in toward
the top, braced with crosspieces; a plank platform near the top carries a lantern in a small iron frame, and two arms hold a red
flag and a white one. Drawn at four times the size and scaled down for smooth edges.
Run: python3 Tools/make_signal_tower.py"""
import os
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
S = 4
OUT = os.path.join(ROOT, "Textures", "ColonyCouncil", "Things")

LINE = (34, 28, 22, 255)
WOOD = (150, 108, 66, 255)
WOOD_DARK = (110, 76, 44, 255)
WOOD_LIGHT = (186, 142, 92, 255)
IRON = (70, 70, 74, 255)
GLOW = (250, 210, 110, 255)
GLOW_LIGHT = (255, 240, 190, 255)
RED = (196, 64, 48, 255)
RED_DARK = (150, 44, 34, 255)
WHITE = (232, 228, 214, 255)
WHITE_DARK = (196, 190, 172, 255)
OL = 8


def poly(d, pts, fill, width=OL):
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=LINE, width=width, joint="curve")


def beam(d, a, b, w, fill):
    """A wooden beam from a to b, w wide, with its outline."""
    (x1, y1), (x2, y2) = a, b
    dx, dy = x2 - x1, y2 - y1
    n = max(1.0, (dx * dx + dy * dy) ** 0.5)
    ox, oy = -dy / n * w / 2, dx / n * w / 2
    poly(d, [(x1 + ox, y1 + oy), (x2 + ox, y2 + oy), (x2 - ox, y2 - oy), (x1 - ox, y1 - oy)], fill)


def shadow(img, box):
    sh = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse(box, fill=(0, 0, 0, 80))
    return Image.alpha_composite(img, sh.filter(ImageFilter.GaussianBlur(14)))


def flag(d, x, y, w, h, fill, dark, left):
    """A flag hung from an arm's end at (x, y), a fold shaded."""
    if left:
        pts = [(x, y), (x - w, y + 10), (x - w + 8, y + h // 2), (x - w, y + h - 10), (x, y + h)]
    else:
        pts = [(x, y), (x + w, y + 10), (x + w - 8, y + h // 2), (x + w, y + h - 10), (x, y + h)]
    poly(d, pts, fill, 6)
    fx = x - w // 2 if left else x + w // 2
    d.line([(fx, y + 14), (fx, y + h - 14)], fill=dark, width=8)


def tower():
    W, H = 128 * S, 256 * S
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    img = shadow(img, (W // 2 - 150, H - 150, W // 2 + 150, H - 50))
    d = ImageDraw.Draw(img)
    cx = W // 2
    base_y, top_y = H - 110, 300
    # The legs, leaning in toward the platform.
    lb, rb = (cx - 130, base_y), (cx + 130, base_y)
    lt, rt = (cx - 56, top_y + 70), (cx + 56, top_y + 70)
    beam(d, lb, lt, 34, WOOD)
    beam(d, rb, rt, 34, WOOD)
    # The bracing: crosspieces and an X between them.
    for f in (0.25, 0.55, 0.82):
        yl = base_y + (top_y + 70 - base_y) * f
        xl = lb[0] + (lt[0] - lb[0]) * f
        xr = rb[0] + (rt[0] - rb[0]) * f
        beam(d, (xl, yl), (xr, yl), 20, WOOD_DARK)
    beam(d, (lb[0] + 20, base_y - 120), (rt[0] - 10, top_y + 200), 14, WOOD_DARK)
    beam(d, (rb[0] - 20, base_y - 120), (lt[0] + 10, top_y + 200), 14, WOOD_DARK)
    # The platform, its planks shaded.
    poly(d, [(cx - 100, top_y + 40), (cx + 100, top_y + 40), (cx + 90, top_y + 84), (cx - 90, top_y + 84)], WOOD_LIGHT)
    for x in range(cx - 70, cx + 80, 34):
        d.line([(x, top_y + 46), (x - 2, top_y + 80)], fill=WOOD_DARK, width=5)
    # The post above the platform and the arms for the flags.
    beam(d, (cx, top_y + 44), (cx, top_y - 200), 22, WOOD)
    beam(d, (cx - 150, top_y - 150), (cx + 150, top_y - 150), 16, WOOD_DARK)
    flag(d, cx - 146, top_y - 146, 92, 120, RED, RED_DARK, True)
    flag(d, cx + 146, top_y - 146, 92, 120, WHITE, WHITE_DARK, False)
    # The lantern on the platform: an iron frame round a lit glass.
    poly(d, [(cx - 34, top_y - 40), (cx + 34, top_y - 40), (cx + 34, top_y + 40), (cx - 34, top_y + 40)], IRON, 6)
    poly(d, [(cx - 24, top_y - 30), (cx + 24, top_y - 30), (cx + 24, top_y + 32), (cx - 24, top_y + 32)], GLOW, 5)
    d.rectangle((cx - 10, top_y - 16, cx + 6, top_y + 18), fill=GLOW_LIGHT)
    poly(d, [(cx - 40, top_y - 40), (cx, top_y - 72), (cx + 40, top_y - 40)], IRON, 6)
    return img.resize((128, 256), Image.LANCZOS)


def main():
    os.makedirs(OUT, exist_ok=True)
    tower().save(os.path.join(OUT, "SignalTower.png"))
    print("wrote", os.path.join(OUT, "SignalTower.png"))


if __name__ == "__main__":
    main()
