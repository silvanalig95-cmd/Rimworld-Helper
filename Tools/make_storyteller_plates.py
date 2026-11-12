#!/usr/bin/env python3
"""
Draws the plates of Rimfolk's eight ways to play (Defs/StorytellerDefs/Rimfolk_Storytellers.xml): the game's storyteller
screen needs a picture for each storyteller (StorytellerDef.portraitLarge and portraitTiny, loaded with
ContentFinder<Texture2D>, which logs an error for a path with no texture), and the player asked for no character and no
portrait. So each is a plain plate in Rimfolk's own look (UI/SkinMath.cs): the window's dark ground, a raised card with a
faint edge, a thin amber rule, and the way's name lettered in the heading's colour, quiet and readable.

    <Way>.png       580x620  the large plate, drawn by the game at exactly that size beside the description
                             (Storyteller.PortraitSizeLarge)
    <Way>Tiny.png   228x244  the small plate in the list, twice the size the game draws it (the list's 122x130,
                             Storyteller.PortraitSizeTiny, less 4 on each side), so it stays sharp

Every plate is drawn at 4x and scaled down for smooth letters. Usage (from the repository root):
    python3 Tools/make_storyteller_plates.py      # writes Textures/ColonyCouncil/Storytellers/*.png

Needs Pillow (pip install pillow).
"""
import os

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIR = os.path.join(ROOT, "Textures", "ColonyCouncil", "Storytellers")
K = 4

# The defName's tail and the name lettered on the plate, in the storyteller list's order.
WAYS = [
    ("LongRoad", "The Long Road"),
    ("Neighbours", "Neighbours"),
    ("TurningYear", "The Turning Year"),
    ("WhatYouSow", "What You Sow"),
    ("OldPowers", "Old Powers"),
    ("LeanYears", "Lean Years"),
    ("RiseAndFall", "Rise and Fall"),
    ("Crossroads", "Crossroads"),
]


def hexrgb(v, a=255):
    return ((v >> 16) & 255, (v >> 8) & 255, v & 255, a)


# Rimfolk's own look (Source/ColonyCouncil/UI/SkinMath.cs).
GROUND = hexrgb(0x17191B)
PANEL = hexrgb(0x212427)
RAISED = hexrgb(0x2A2E32)
EDGE = hexrgb(0x3A3F44)
HEADING = hexrgb(0xE8D5A6)
ACCENT = hexrgb(0xE0A845)
FAINT = hexrgb(0xB0AA9C)

FONTS = [
    "/usr/share/fonts/truetype/freefont/FreeSerif.ttf",
    "/usr/share/fonts/truetype/liberation/LiberationSerif-Regular.ttf",
    "/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf",
]


def font(size):
    for f in FONTS:
        if os.path.exists(f):
            return ImageFont.truetype(f, size)
    return ImageFont.load_default()


def lines_for(name, width, size, draw):
    """The name in as few lines as fit the width at this size (words kept whole)."""
    words = name.split()
    lines, cur = [], ""
    f = font(size)
    for w in words:
        t = (cur + " " + w).strip()
        if draw.textlength(t, font=f) <= width or not cur:
            cur = t
        else:
            lines.append(cur)
            cur = w
    if cur:
        lines.append(cur)
    return lines


def fit(name, width, height, start, draw, max_lines):
    """The largest size at which the name fits the box in at most max_lines lines."""
    size = start
    while size > 8:
        ls = lines_for(name, width, size, draw)
        f = font(size)
        widest = max(draw.textlength(l, font=f) for l in ls)
        tall = len(ls) * size * 1.18
        if len(ls) <= max_lines and widest <= width and tall <= height:
            return size, ls
        size -= 2
    return size, lines_for(name, width, size, draw)


def plate(name, w, h, tiny):
    W, H = w * K, h * K
    img = Image.new("RGBA", (W, H), GROUND)
    d = ImageDraw.Draw(img)
    m = (10 if tiny else 34) * K
    r = (10 if tiny else 18) * K
    edge = (2 if tiny else 2) * K
    # The raised card, a little lighter than the ground, with a faint edge.
    d.rounded_rectangle((m, m, W - m, H - m), r, fill=RAISED, outline=EDGE, width=edge)
    # A quiet inner panel band for the name.
    pad = (14 if tiny else 60) * K
    box_w = W - 2 * (m + pad)
    box_h = (H - 2 * m) * (0.62 if tiny else 0.42)
    size, ls = fit(name, box_w, box_h, (64 if tiny else 120) * K // 1, d, 3 if tiny else 2)
    f = font(size)
    line_h = size * 1.18
    total = len(ls) * line_h
    cy = H / 2
    top = cy - total / 2
    for i, l in enumerate(ls):
        tw = d.textlength(l, font=f)
        d.text(((W - tw) / 2, top + i * line_h), l, font=f, fill=HEADING)
    # Thin amber rules above and below the name.
    rule_w = (36 if tiny else 120) * K
    gap = (12 if tiny else 34) * K
    th = max(K, (2 if tiny else 3) * K)
    for y in (top - gap, top + total + gap * 0.6):
        d.rounded_rectangle(((W - rule_w) / 2, y - th / 2, (W + rule_w) / 2, y + th / 2), th // 2, fill=ACCENT)
    if not tiny:
        # A small mark of Rimfolk's in the card's foot, faint.
        sf = font(22 * K)
        t = "a way to play"
        tw = d.textlength(t, font=sf)
        d.text(((W - tw) / 2, H - m - 70 * K), t, font=sf, fill=FAINT)
    return img.resize((w, h), Image.LANCZOS)


def main():
    os.makedirs(DIR, exist_ok=True)
    for key, name in WAYS:
        plate(name, 580, 620, False).save(os.path.join(DIR, key + ".png"))
        plate(name, 228, 244, True).save(os.path.join(DIR, key + "Tiny.png"))
        print("wrote", key)


if __name__ == "__main__":
    main()
