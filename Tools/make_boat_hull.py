#!/usr/bin/env python3
"""Draws the river folk boat-builders' hull on the bank in its three stages (Textures/ColonyCouncil/Things/BoatHull_1..3.png,
256x128 each, for Defs/Misc/Rimfolk_PeopleEvents4_Misc.xml) in the game's manner: flat fills with a little shading under a
dark outline, seen from a little above. 1: the keel on blocks with its stem and stern posts; 2: the ribs pegged along it and
the first planks at the bottom; 3: the planked, pitched hull. Drawn at four times the size and scaled down for smooth edges.
Run: python3 Tools/make_boat_hull.py"""
import os
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
S = 4
W, H = 256 * S, 128 * S
OUT = os.path.join(ROOT, "Textures", "ColonyCouncil", "Things")

LINE = (38, 26, 17, 255)
WOOD = (150, 104, 62, 255)
WOOD_DARK = (106, 70, 40, 255)
WOOD_LIGHT = (184, 136, 88, 255)
PITCH = (58, 44, 34, 255)
BLOCK = (118, 112, 104, 255)
OL = 8


def poly(d, pts, fill, width=OL):
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=LINE, width=width, joint="curve")


def canvas():
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    shadow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse((90, 300, W - 90, 430), fill=(0, 0, 0, 70))
    img = Image.alpha_composite(img, shadow.filter(ImageFilter.GaussianBlur(12)))
    return img, ImageDraw.Draw(img)


def blocks(d):
    for x in (260, 512, 764):
        poly(d, [(x - 34, 330), (x + 34, 330), (x + 34, 380), (x - 34, 380)], BLOCK, 6)


def keel(d):
    # The keel along the middle, and the stem (bow, right) and stern (left) posts rising from its ends.
    poly(d, [(140, 300), (884, 300), (884, 330), (140, 330)], WOOD_DARK)
    poly(d, [(860, 300), (900, 300), (960, 150), (930, 140)], WOOD)
    poly(d, [(130, 300), (170, 300), (120, 165), (92, 175)], WOOD)


def ribs(d):
    for i in range(9):
        x = 220 + i * 74
        top = 175 if 2 <= i <= 6 else 195
        d.line([(x - 26, top), (x - 10, 300), (x + 10, 300), (x + 26, top)], fill=LINE, width=OL + 6, joint="curve")
        d.line([(x - 26, top), (x - 10, 300), (x + 10, 300), (x + 26, top)], fill=WOOD_LIGHT, width=OL - 2, joint="curve")


def planks(d, rows):
    # Strakes from the keel up, each a curved band from the stern post to the stem.
    for r in range(rows):
        y0 = 300 - r * 26
        y1 = y0 - 26
        sag = 18 + r * 6
        top = [(150 + r * 10, y1 + sag // 2), (512, y1 + 6), (874 - r * 8, y1 + sag // 3)]
        bottom = [(874 - r * 8, y0 + sag // 3), (512, y0 + 6), (150 + r * 10, y0 + sag // 2)]
        poly(d, top + bottom, WOOD if r % 2 == 0 else WOOD_LIGHT, 6)


def stage1():
    img, d = canvas()
    blocks(d)
    keel(d)
    return img


def stage2():
    img, d = canvas()
    blocks(d)
    keel(d)
    ribs(d)
    planks(d, 2)
    return img


def stage3():
    img, d = canvas()
    blocks(d)
    # The planked hull: a long boat shape seen from above and a little to the side, pitched dark at the waterline.
    outer = [(100, 180), (260, 140), (512, 128), (800, 140), (960, 150), (890, 300), (512, 340), (150, 300)]
    poly(d, outer, WOOD)
    poly(d, [(150, 300), (512, 340), (890, 300), (870, 275), (512, 310), (165, 275)], PITCH, 6)
    inner = [(170, 190), (300, 160), (512, 152), (760, 160), (900, 168), (850, 262), (512, 290), (190, 258)]
    poly(d, inner, WOOD_DARK, 6)
    for i in range(1, 5):
        y = 165 + i * 24
        d.line([(200 + i * 4, y + 10), (512, y), (840 - i * 4, y + 8)], fill=WOOD, width=5, joint="curve")
    for x in (330, 512, 694):
        poly(d, [(x - 16, 175), (x + 16, 175), (x + 16, 270), (x - 16, 270)], WOOD_LIGHT, 5)
    return img


def save(img, name):
    img = img.resize((W // S, H // S), Image.LANCZOS)
    img.save(os.path.join(OUT, name))
    print("wrote", name)


if __name__ == "__main__":
    save(stage1(), "BoatHull_1.png")
    save(stage2(), "BoatHull_2.png")
    save(stage3(), "BoatHull_3.png")
