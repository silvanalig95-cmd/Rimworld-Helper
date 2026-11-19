#!/usr/bin/env python3
"""Draws the sprites of the quests at home (Textures/ColonyCouncil/Things/, 128x128 each) in the game's manner: flat fills
with a little shading under a dark outline, seen from a little above. The messenger's letter by their people's age (a
carved message stick, a letter sealed in red wax, a small locked dispatch case) and the lost keepsake by its owner's (a
painted hide, a small family book with a cord tie, a photo album), and the pilgrims' relic of the storylines (a small
carved reliquary box with a peaked lid and brass corners), and round 3's vault case, old cache and founder's grave marker.
Drawn at four times the size and scaled down for smooth edges.
Run: python3 Tools/make_quest_items.py (every sprite), or name the ones to draw: python3 Tools/make_quest_items.py relic"""
import sys
import os
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
S = 4
W = 128 * S
OUT = os.path.join(ROOT, "Textures", "ColonyCouncil", "Things")

LINE = (38, 26, 17, 255)
WOOD = (150, 104, 62, 255)
WOOD_DARK = (106, 70, 40, 255)
WOOD_LIGHT = (184, 136, 88, 255)
PAPER = (238, 228, 200, 255)
PAPER_DARK = (205, 192, 160, 255)
WAX = (170, 48, 40, 255)
WAX_DARK = (120, 30, 26, 255)
LEATHER = (120, 78, 46, 255)
LEATHER_DARK = (86, 54, 30, 255)
LEATHER_LIGHT = (150, 104, 66, 255)
BRASS = (214, 170, 72, 255)
BRASS_DARK = (150, 112, 40, 255)
STEEL = (120, 128, 134, 255)
STEEL_DARK = (82, 88, 94, 255)
STEEL_LIGHT = (164, 172, 178, 255)
HIDE = (202, 168, 118, 255)
HIDE_DARK = (160, 124, 80, 255)
OCHRE = (176, 74, 40, 255)
CHAR = (52, 40, 32, 255)
CLOTH = (58, 84, 120, 255)
CLOTH_DARK = (40, 58, 86, 255)
CORD = (176, 52, 44, 255)
PHOTO = (226, 220, 206, 255)
PHOTO_DARK = (120, 112, 100, 255)
OL = 7


def poly(d, pts, fill, width=OL):
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=LINE, width=width, joint="curve")


def rrect(d, box, r, fill, width=OL):
    d.rounded_rectangle(box, r, fill=fill, outline=LINE, width=width)


def canvas():
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    shadow = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse((110, 370, 410, 440), fill=(0, 0, 0, 80))
    img = Image.alpha_composite(img, shadow.filter(ImageFilter.GaussianBlur(10)))
    return img, ImageDraw.Draw(img)


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".png")
    img.resize((128, 128), Image.LANCZOS).save(path)
    print("wrote", path)


def message_stick():
    img, d = canvas()
    # A short stick lying across, a little tilted, notched along its length, a cord bound round one end.
    poly(d, [(110, 300), (380, 170), (406, 214), (136, 344)], WOOD)
    poly(d, [(120, 316), (388, 186), (396, 200), (128, 330)], WOOD_LIGHT, width=0)
    for i in range(7):
        x = 170 + i * 30
        y = 300 - i * 15
        d.line((x, y, x + 10, y + 22), fill=WOOD_DARK, width=7)
    for i in range(3):
        x = 300 + i * 22
        d.line((x, 220 - i * 10, x + 6, 236 - i * 10), fill=CHAR, width=6)
    for k in range(3):
        d.line((128 + k * 12, 306 - k * 6, 150 + k * 12, 350 - k * 6), fill=CORD, width=8)
    d.ellipse((96, 290, 136, 352), fill=WOOD_DARK, outline=LINE, width=OL)
    save(img, "QuestMessageStick")


def letter():
    img, d = canvas()
    # A folded letter, the flap closed under a round wax seal.
    poly(d, [(120, 170), (392, 150), (404, 350), (132, 370)], PAPER)
    d.line((126, 176, 262, 270), fill=PAPER_DARK, width=6)
    d.line((262, 270, 396, 156), fill=PAPER_DARK, width=6)
    d.line((140, 356, 400, 338), fill=PAPER_DARK, width=4)
    d.ellipse((228, 236, 300, 306), fill=WAX, outline=LINE, width=OL)
    d.ellipse((246, 254, 282, 288), fill=WAX_DARK)
    save(img, "QuestLetter")


def dispatch():
    img, d = canvas()
    # A small steel-cornered leather case with a handle and a brass lock.
    rrect(d, (196, 140, 316, 182), 14, LEATHER_DARK)
    d.rounded_rectangle((216, 154, 296, 176), 8, fill=(0, 0, 0, 0))
    rrect(d, (110, 170, 402, 368), 22, LEATHER)
    d.rounded_rectangle((126, 186, 190, 352), 16, fill=LEATHER_LIGHT)
    d.line((118, 228, 394, 228), fill=LEATHER_DARK, width=6)
    for (x, y) in ((110, 170), (366, 170), (110, 332), (366, 332)):
        rrect(d, (x, y, x + 36, y + 36), 8, STEEL, width=5)
    rrect(d, (236, 214, 276, 262), 6, BRASS, width=5)
    d.rectangle((250, 232, 262, 252), fill=BRASS_DARK)
    save(img, "QuestDispatch")


def hide():
    img, d = canvas()
    # A small tanned hide, its edges uneven, painted with a family's marks in ochre and charcoal.
    pts = [(120, 200), (170, 160), (250, 176), (330, 150), (392, 196), (380, 270), (400, 340), (320, 372), (240, 352), (160, 376), (112, 320), (132, 262)]
    poly(d, pts, HIDE)
    poly(d, [(150, 214), (190, 190), (250, 204), (320, 182), (360, 214), (352, 270)], HIDE_DARK, width=0)
    d.ellipse((216, 216, 296, 296), outline=OCHRE, width=14)
    d.ellipse((244, 244, 268, 268), fill=CHAR)
    for i in range(4):
        d.line((176 + i * 52, 322, 196 + i * 52, 342), fill=OCHRE, width=10)
    d.line((176, 238, 204, 280), fill=CHAR, width=9)
    d.line((336, 236, 308, 280), fill=CHAR, width=9)
    save(img, "KeepsakeHide")


def book():
    img, d = canvas()
    # A small cloth-bound book, closed, a cord tied round it, the page edges showing.
    poly(d, [(140, 160), (360, 150), (374, 352), (152, 366)], PAPER_DARK)
    poly(d, [(126, 150), (350, 138), (362, 346), (138, 360)], CLOTH)
    poly(d, [(126, 150), (160, 148), (172, 358), (138, 360)], CLOTH_DARK)
    d.line((350, 160, 368, 344), fill=PAPER, width=10)
    d.line((132, 250, 360, 238), fill=CORD, width=12)
    d.ellipse((230, 226, 262, 258), fill=CORD, outline=LINE, width=5)
    rrect(d, (210, 176, 312, 214), 6, PAPER, width=5)
    save(img, "KeepsakeBook")


def photos():
    img, d = canvas()
    # A leather photo album, open a little, a photograph half out of it.
    poly(d, [(118, 170), (380, 160), (392, 350), (130, 362)], LEATHER)
    poly(d, [(118, 170), (152, 168), (162, 360), (130, 362)], LEATHER_DARK)
    d.rectangle((176, 186, 362, 334), fill=LEATHER_LIGHT)
    poly(d, [(214, 120), (330, 132), (318, 262), (202, 250)], PHOTO)
    poly(d, [(228, 138), (316, 148), (306, 244), (218, 234)], PHOTO_DARK, width=0)
    d.ellipse((246, 162, 286, 202), fill=PHOTO)
    d.rectangle((240, 204, 292, 238), fill=PHOTO)
    rrect(d, (196, 288, 244, 318), 6, BRASS, width=5)
    save(img, "KeepsakePhotos")


def relic():
    img, d = canvas()
    # A small carved reliquary: a wooden box under a peaked lid, brass at its corners, a brass disc on its front.
    poly(d, [(140, 200), (256, 120), (372, 200)], WOOD_DARK)
    poly(d, [(170, 196), (256, 138), (342, 196)], WOOD, width=0)
    rrect(d, (132, 196, 380, 362), 10, WOOD)
    d.rectangle((150, 214, 362, 344), fill=WOOD_LIGHT)
    d.line((150, 300, 362, 300), fill=WOOD_DARK, width=6)
    for (x, y) in ((124, 188), (352, 188), (124, 334), (352, 334)):
        rrect(d, (x, y, x + 36, y + 36), 8, BRASS, width=5)
    d.ellipse((222, 222, 290, 290), fill=BRASS, outline=LINE, width=OL)
    d.ellipse((242, 242, 270, 270), fill=BRASS_DARK)
    rrect(d, (240, 106, 272, 132), 6, BRASS, width=5)
    save(img, "PilgrimRelic")


STONE = (150, 146, 136, 255)
STONE_DARK = (104, 100, 92, 255)
STONE_LIGHT = (184, 180, 168, 255)
MOSS = (96, 120, 64, 255)
IRON = (70, 72, 76, 255)


def vault_case():
    img, d = canvas()
    # The vault's sealed case (storylines, round 3): a low steel case with ribbed sides, a seam round its lid, two latches
    # and a small red seal light at the front.
    rrect(d, (104, 190, 408, 372), 18, STEEL)
    d.rectangle((122, 206, 390, 236), fill=STEEL_LIGHT)
    d.line((112, 250, 400, 250), fill=STEEL_DARK, width=8)
    for x in (150, 206, 306, 362):
        d.line((x, 262, x, 356), fill=STEEL_DARK, width=6)
    for x in (176, 304):
        rrect(d, (x, 232, x + 34, 272), 6, STEEL_DARK, width=5)
    d.ellipse((240, 286, 272, 318), fill=WAX, outline=LINE, width=5)
    d.ellipse((250, 294, 262, 306), fill=(232, 96, 80, 255))
    save(img, "VaultCase")


def cache():
    img, d = canvas()
    # The old cache in the ruins (quests, round 3): a small wooden chest with a rounded lid, iron bands and a heavy lock,
    # the wood greyed with age.
    old = (128, 104, 80, 255)
    old_dark = (92, 74, 56, 255)
    rrect(d, (128, 236, 384, 368), 8, old)
    d.pieslice((128, 168, 384, 304), 180, 360, fill=old_dark, outline=LINE, width=OL)
    d.line((128, 236, 384, 236), fill=LINE, width=OL)
    for x in (164, 324):
        d.rectangle((x, 176, x + 24, 366), fill=IRON, outline=LINE, width=4)
    rrect(d, (234, 240, 278, 296), 6, IRON, width=5)
    d.ellipse((248, 258, 264, 274), fill=LINE)
    save(img, "QuestCache")


def grave():
    img, d = canvas()
    # The founder's grave marker (storylines, round 3): an old upright stone with a rounded top, worn, moss at its foot,
    # two plain cut lines across its face.
    d.ellipse((130, 330, 382, 400), fill=(110, 92, 70, 255), outline=LINE, width=OL)
    poly(d, [(170, 360), (170, 170), (342, 170), (342, 360)], STONE, width=0)
    d.pieslice((170, 90, 342, 250), 180, 360, fill=STONE)
    d.arc((170, 90, 342, 250), 180, 360, fill=LINE, width=OL)
    d.line((170, 170, 170, 360), fill=LINE, width=OL)
    d.line((342, 170, 342, 360), fill=LINE, width=OL)
    d.line((170, 360, 342, 360), fill=LINE, width=OL)
    d.rectangle((186, 150, 222, 340), fill=STONE_LIGHT)
    d.line((206, 220, 306, 220), fill=STONE_DARK, width=7)
    d.line((206, 256, 306, 256), fill=STONE_DARK, width=7)
    d.ellipse((150, 330, 230, 372), fill=MOSS)
    d.ellipse((290, 338, 356, 374), fill=MOSS)
    save(img, "FounderGrave")


DRAW = {
    "stick": message_stick, "letter": letter, "dispatch": dispatch, "hide": hide, "book": book, "photos": photos, "relic": relic,
    "vaultcase": vault_case, "cache": cache, "grave": grave,
}


def main():
    names = sys.argv[1:] or list(DRAW)
    for n in names:
        DRAW[n]()


if __name__ == "__main__":
    main()
