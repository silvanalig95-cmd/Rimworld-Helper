#!/usr/bin/env python3
"""Draws the mailbag's sprite (Textures/ColonyCouncil/Things/Mailbag.png, 128x128) in the game's manner: flat fills with
a little shading under a dark outline, seen from a little above. A wooden post with a peg, a leather satchel hung from
it with its flap buckled, and two letters standing out of it, one sealed in red wax. Drawn at four times the size and
scaled down for smooth edges. Run: python3 Tools/make_mailbag.py"""
import os
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
S = 4
W = 128 * S
OUT = os.path.join(ROOT, "Textures", "ColonyCouncil", "Things", "Mailbag.png")

LINE = (38, 26, 17, 255)
WOOD = (139, 94, 56, 255)
WOOD_DARK = (104, 68, 40, 255)
WOOD_LIGHT = (168, 120, 76, 255)
LEATHER = (158, 101, 56, 255)
LEATHER_DARK = (118, 72, 38, 255)
LEATHER_LIGHT = (184, 128, 78, 255)
PAPER = (238, 228, 200, 255)
PAPER_DARK = (205, 192, 160, 255)
WAX = (170, 48, 40, 255)
BRASS = (214, 170, 72, 255)
BRASS_DARK = (150, 112, 40, 255)
OL = 7  # outline width at the drawn scale


def poly(d, pts, fill, width=OL):
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=LINE, width=width, joint="curve")


def rrect(d, box, r, fill, width=OL):
    d.rounded_rectangle(box, r, fill=fill, outline=LINE, width=width)


def main():
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))

    # A soft shadow on the ground under the post and the bag.
    shadow = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    ds = ImageDraw.Draw(shadow)
    ds.ellipse((120, 400, 420, 470), fill=(0, 0, 0, 90))
    img = Image.alpha_composite(img, shadow.filter(ImageFilter.GaussianBlur(10)))
    d = ImageDraw.Draw(img)

    # The post, its cap and the peg.
    rrect(d, (176, 96, 228, 452), 8, WOOD)
    d.rectangle((184, 104, 196, 444), fill=WOOD_LIGHT)
    for y in (170, 248, 330, 400):
        d.line((206, y, 220, y + 18), fill=WOOD_DARK, width=4)
    rrect(d, (164, 80, 240, 112), 10, WOOD_DARK)
    rrect(d, (220, 150, 330, 172), 8, WOOD)
    d.ellipse((316, 140, 346, 182), fill=WOOD_DARK, outline=LINE, width=OL)

    # The satchel's strap over the peg.
    d.line((318, 166, 262, 236), fill=LINE, width=16)
    d.line((318, 166, 262, 236), fill=LEATHER_DARK, width=8)
    d.line((334, 166, 392, 236), fill=LINE, width=16)
    d.line((334, 166, 392, 236), fill=LEATHER_DARK, width=8)

    # Two letters standing out of the bag, behind its flap.
    poly(d, [(258, 168), (326, 150), (342, 250), (274, 268)], PAPER)
    d.line((264, 174, 296, 204), fill=PAPER_DARK, width=4)
    d.line((296, 204, 330, 160), fill=PAPER_DARK, width=4)
    poly(d, [(318, 176), (388, 172), (392, 270), (322, 274)], PAPER)
    d.line((324, 182, 356, 214), fill=PAPER_DARK, width=4)
    d.line((356, 214, 386, 178), fill=PAPER_DARK, width=4)
    d.ellipse((342, 198, 370, 226), fill=WAX, outline=LINE, width=5)

    # The bag: its body, a lighter side, a seam and the flap with a brass buckle.
    rrect(d, (240, 240, 414, 410), 26, LEATHER)
    d.rounded_rectangle((252, 252, 300, 398), 18, fill=LEATHER_LIGHT)
    d.line((250, 384, 404, 384), fill=LEATHER_DARK, width=5)
    poly(d, [(236, 236), (418, 236), (404, 318), (327, 338), (250, 318)], LEATHER_DARK)
    d.line((246, 250, 408, 250), fill=LEATHER, width=5)
    rrect(d, (312, 318, 344, 356), 6, BRASS, width=5)
    d.rectangle((322, 328, 334, 346), fill=BRASS_DARK)

    # A small brass post-horn plate on the post.
    rrect(d, (182, 300, 222, 340), 8, BRASS, width=5)
    d.arc((189, 308, 215, 334), 200, 520, fill=BRASS_DARK, width=5)

    out = img.resize((128, 128), Image.LANCZOS)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    out.save(OUT)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
