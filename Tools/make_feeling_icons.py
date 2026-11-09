#!/usr/bin/env python3
"""
Draws the signs over a colonist's head for a strong feeling (Source/ColonyCouncil/Heart/HeartFelt.cs, FeelingIcons):
a handful of shapes shared by feelings alike, a white glyph with a dark outline on transparent, 64x64 RGBA. The game
tints the white part in the feeling's own colour (MoteBubble's icon colour) and the outline stays dark, so the sign
reads on a thought's bubble.

Usage (from the repository root):
    python3 Tools/make_feeling_icons.py      # writes Textures/ColonyCouncil/Icons/Feelings/*.png

Needs Pillow (pip install pillow). The shapes are drawn with Tools/make_icons.py's primitives, at 4x and scaled down.
"""
import math
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_icons as mk  # noqa: E402

OUT_DIR = os.path.join(mk.ROOT, "Textures", "ColonyCouncil", "Icons", "Feelings")
S = mk.S


def g_tear():
    """Grief, despair: a tear."""
    m = mk.new()
    mk.circle(m, 128, 158, 66)
    mk.poly(m, [(128, 24), (70, 128), (186, 128)])
    mk.circle(m, 104, 162, 16, mk.OFF)
    return m


def g_fear():
    """Fear, dread, worry: a bold exclamation mark."""
    m = mk.new()
    mk.poly(m, [(96, 24), (160, 24), (146, 166), (110, 166)])
    mk.circle(m, 128, 210, 26)
    return m


def g_bolt():
    """Anger, frustration, envy: a bolt."""
    m = mk.new()
    mk.poly(m, [(150, 16), (64, 140), (122, 140), (96, 240), (194, 104), (134, 104), (170, 16)])
    return m


def g_love():
    """Love, tenderness, gratitude: a heart."""
    return mk.g_heart()


def g_sun():
    """Joy, contentment, relief: a sun."""
    m = mk.new()
    mk.circle(m, 128, 128, 46)
    for i in range(8):
        a = math.radians(i * 45 + 22.5)
        x0, y0 = 128 + math.cos(a) * 82, 128 + math.sin(a) * 82
        x1, y1 = 128 + math.cos(a) * 114, 128 + math.sin(a) * 114
        mk.line(m, [(x0, y0), (x1, y1)], 22)
    return m


def g_home():
    """Homesick, nostalgia: a small house."""
    m = mk.new()
    mk.poly(m, [(128, 26), (236, 122), (20, 122)])
    mk.rect(m, 52, 114, 204, 230)
    mk.rect(m, 108, 160, 148, 230, fill=mk.OFF)
    return m


def g_alone():
    """Loneliness: one figure on its own."""
    m = mk.new()
    mk.bust(m, 128, 82, 44, 132, 90)
    mk.clip_below(m, 222)
    return m


def g_together():
    """Fellowship: two figures side by side."""
    left, right = mk.new(), mk.new()
    mk.bust(left, 82, 96, 36, 100, 74)
    mk.bust(right, 174, 96, 36, 100, 74)
    m = mk.over(right, left, 10)
    mk.clip_below(m, 214)
    return m


def g_star():
    """Any other bright feeling (pride, hope, awe...): a star."""
    m = mk.new()
    mk.poly(m, mk.star(128, 134, 116, 48))
    return m


def g_cloud():
    """Any other hard feeling (shame, disgust, restlessness...): a cloud."""
    m = mk.new()
    mk.circle(m, 92, 140, 50)
    mk.circle(m, 150, 112, 66)
    mk.circle(m, 196, 152, 42)
    mk.rect(m, 52, 140, 218, 192, r=26)
    return m


GLYPHS = [("Tear", g_tear), ("Fear", g_fear), ("Bolt", g_bolt), ("Love", g_love), ("Sun", g_sun), ("Home", g_home),
          ("Alone", g_alone), ("Together", g_together), ("Star", g_star), ("Cloud", g_cloud)]


def to_texture(mask):
    """White glyph with a dark outline: the outline is the glyph grown a little, drawn dark under it."""
    outline = mk.grow(mask, 14)
    small_out = outline.resize((mk.OUT, mk.OUT), Image.LANCZOS)
    small_in = mask.resize((mk.OUT, mk.OUT), Image.LANCZOS)
    img = Image.new("RGBA", (mk.OUT, mk.OUT), (0, 0, 0, 0))
    dark = Image.new("RGBA", (mk.OUT, mk.OUT), (24, 22, 20, 255))
    white = Image.new("RGBA", (mk.OUT, mk.OUT), (255, 255, 255, 255))
    img = Image.composite(dark, img, small_out)
    img = Image.composite(white, img, small_in)
    return img


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for name, fn in GLYPHS:
        to_texture(fn()).save(os.path.join(OUT_DIR, name + ".png"), optimize=True)
    print("%d signs in %s" % (len(GLYPHS), OUT_DIR))


if __name__ == "__main__":
    main()
