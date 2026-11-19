#!/usr/bin/env python3
"""Draws the Quiet Bell's bell-post for a colony's grave-garden (Textures/ColonyCouncil/Things/BellPost.png, 128x256, for
Defs/Misc/Rimfolk_PeopleEvents5_Misc.xml) in the game's manner: flat fills with a little shading under a dark outline, seen
from a little above. A post of dark oak on a squared stone footing, a crosspiece near its head with a small bronze bell hung
from it, and a grey cord down from the bell's clapper. Drawn at four times the size and scaled down for smooth edges.
Run: python3 Tools/make_bell_post.py"""
import os
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
S = 4
W, H = 128 * S, 256 * S
OUT = os.path.join(ROOT, "Textures", "ColonyCouncil", "Things")

LINE = (34, 26, 20, 255)
OAK = (92, 64, 42, 255)
OAK_DARK = (66, 45, 30, 255)
OAK_LIGHT = (124, 90, 60, 255)
STONE = (128, 124, 116, 255)
STONE_DARK = (98, 94, 88, 255)
BRONZE = (176, 128, 58, 255)
BRONZE_DARK = (128, 88, 36, 255)
BRONZE_LIGHT = (214, 172, 96, 255)
CORD = (150, 150, 156, 255)
OL = 8


def poly(d, pts, fill, width=OL):
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=LINE, width=width, joint="curve")


def draw():
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    shadow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse((90, 900, W - 90, 1000), fill=(0, 0, 0, 80))
    img = Image.alpha_composite(img, shadow.filter(ImageFilter.GaussianBlur(14)))
    d = ImageDraw.Draw(img)
    # The stone footing, a little wider than the post.
    poly(d, [(150, 880), (362, 880), (362, 960), (150, 960)], STONE)
    poly(d, [(150, 880), (362, 880), (340, 850), (172, 850)], STONE_DARK, 6)
    # The post, its lit side and its shaded side.
    poly(d, [(222, 190), (290, 190), (296, 870), (216, 870)], OAK)
    d.polygon([(258, 196), (284, 196), (290, 864), (258, 864)], fill=OAK_DARK)
    d.line([(234, 230), (232, 820)], fill=OAK_LIGHT, width=6)
    # The cap at its head.
    poly(d, [(206, 160), (306, 160), (296, 196), (216, 196)], OAK_DARK, 6)
    # The crosspiece, reaching out to the right, and its brace.
    poly(d, [(250, 250), (440, 250), (440, 290), (250, 290)], OAK)
    poly(d, [(288, 290), (316, 290), (380, 290), (296, 370), (284, 350)], OAK_DARK, 6)
    # The bell hung from the crosspiece's end: its yoke, its body, its lip.
    d.line([(404, 290), (404, 318)], fill=LINE, width=10)
    bell = [(380, 318), (428, 318), (446, 360), (458, 420), (350, 420), (362, 360)]
    poly(d, bell, BRONZE)
    d.polygon([(410, 324), (426, 324), (440, 362), (450, 414), (424, 414)], fill=BRONZE_DARK)
    d.line([(374, 344), (366, 404)], fill=BRONZE_LIGHT, width=8)
    poly(d, [(340, 418), (468, 418), (468, 438), (340, 438)], BRONZE_DARK, 6)
    # The clapper and the cord hanging down from it.
    d.ellipse((394, 436, 414, 456), fill=BRONZE_DARK, outline=LINE, width=5)
    d.line([(404, 456), (410, 560), (402, 640)], fill=LINE, width=9, joint="curve")
    d.line([(404, 456), (410, 560), (402, 640)], fill=CORD, width=4, joint="curve")
    return img


def save(img, name):
    img = img.resize((W // S, H // S), Image.LANCZOS)
    img.save(os.path.join(OUT, name))
    print("wrote", name)


if __name__ == "__main__":
    save(draw(), "BellPost.png")
