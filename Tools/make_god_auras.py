#!/usr/bin/env python3
"""
Draws the gods' presence on the map (Source/ColonyCouncil/Gods/Presence/GodAura.cs, GodPresence.cs, GodCard.cs): soft
white-on-transparent shapes, tinted in game in each god's colours, as the thermometer and the mailbag were drawn.

    Halo.png      256x256  a soft round light, brightest in the middle: the glow on the ground under a god
    Glow.png      128x256  a tall soft light: the glow behind a god's body
    Ring.png      256x256  a bright thin ring in a soft haze: the ring round a god, pulsing
    RingRunes.png 256x256  a ring of marks (dashes and small diamonds, as a circle carved in stone): turns slowly under a god
    Rays.png      256x256  a sunburst of soft rays: the flash of a god's coming, opening out from where it stands
    Mote.png       64x64   a soft dot with a bright core: what rises off a god
    Spark.png      64x64   a four-pointed twinkle: the motes of the bright gods, and the shimmer of one in disguise

Every shape is drawn at 4x and scaled down for clean edges. Alphas are kept soft, so the shapes add up without hard
edges when several are drawn over each other (a god's halo, its glow and its ring).

Usage (from the repository root):
    python3 Tools/make_god_auras.py      # writes Textures/ColonyCouncil/Gods/Aura/*.png

Needs Pillow and numpy (pip install pillow numpy).
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIR = os.path.join(ROOT, "Textures", "ColonyCouncil", "Gods", "Aura")
K = 4  # drawn at 4x, then scaled down


def grid(w, h):
    """Coordinates from -1 to 1 across the image (x right, y down), at K times the size."""
    ys, xs = np.mgrid[0:h * K, 0:w * K].astype(np.float64)
    return (xs + 0.5) / (w * K) * 2 - 1, (ys + 0.5) / (h * K) * 2 - 1


def save(alpha, w, h, name):
    """An alpha mask (0..1, at K times the size) as a white texture of w x h."""
    a = np.clip(alpha, 0, 1)
    img = Image.fromarray((a * 255).astype(np.uint8), "L").resize((w, h), Image.LANCZOS)
    white = Image.new("L", (w, h), 255)
    Image.merge("RGBA", (white, white, white, img)).save(os.path.join(DIR, name + ".png"), optimize=True)


def halo():
    x, y = grid(256, 256)
    r = np.sqrt(x * x + y * y)
    # A gaussian light that has faded to nothing at the edge.
    a = np.exp(-(r / 0.48) ** 2) * 0.95
    a *= np.clip((1 - r) / 0.12, 0, 1)
    save(a, 256, 256, "Halo")


def glow():
    x, y = grid(128, 256)
    r = np.sqrt((x / 0.85) ** 2 + (y / 0.95) ** 2)
    a = np.exp(-(r / 0.5) ** 2) * 0.9
    a *= np.clip((1 - r) / 0.1, 0, 1)
    save(a, 128, 256, "Glow")


def ring():
    x, y = grid(256, 256)
    r = np.sqrt(x * x + y * y)
    line = np.exp(-((r - 0.78) / 0.035) ** 2)
    haze = np.exp(-((r - 0.74) / 0.16) ** 2) * 0.35
    inner = np.exp(-(r / 0.55) ** 2) * 0.08
    a = np.maximum(line, haze) + inner
    a *= np.clip((1 - r) / 0.08, 0, 1)
    save(a, 256, 256, "Ring")


def ring_runes():
    S = 256 * K
    m = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(m)
    c = S / 2

    def at(rr, ang):
        return c + math.cos(ang) * rr * c, c + math.sin(ang) * rr * c

    # Two thin circles.
    for rr, wd in ((0.86, 0.018), (0.66, 0.014)):
        R = rr * c
        d.ellipse([c - R, c - R, c + R, c + R], outline=255, width=max(1, int(wd * c)))
    # Marks between them: dashes, and a small diamond every fourth, as a circle carved in stone.
    n = 32
    for i in range(n):
        ang = 2 * math.pi * i / n
        if i % 4 == 0:
            p = [at(0.76, ang - 0.05), at(0.82, ang), at(0.76, ang + 0.05), at(0.70, ang)]
            d.polygon(p, fill=255)
        else:
            a0, a1 = at(0.72, ang), at(0.80, ang)
            d.line([a0, a1], fill=255, width=max(1, int(0.02 * c)))
    a = np.asarray(m.filter(ImageFilter.GaussianBlur(K * 0.8)), dtype=np.float64) / 255.0
    # A soft haze under the marks, so it reads as light and not as paint.
    x, y = grid(256, 256)
    r = np.sqrt(x * x + y * y)
    a = np.maximum(a * 0.95, np.exp(-((r - 0.76) / 0.14) ** 2) * 0.22)
    a *= np.clip((1 - r) / 0.06, 0, 1)
    save(a, 256, 256, "RingRunes")


def rays():
    x, y = grid(256, 256)
    r = np.sqrt(x * x + y * y)
    th = np.arctan2(y, x)
    # Sixteen rays of two lengths, soft at their sides, fading outwards.
    beams = np.maximum(np.cos(th * 8) ** 24, 0.6 * np.cos(th * 8 + math.pi / 8) ** 24)
    a = beams * np.exp(-(r / 0.62) ** 2) + np.exp(-(r / 0.22) ** 2) * 0.8
    a *= np.clip((1 - r) / 0.1, 0, 1)
    save(a, 256, 256, "Rays")


def mote():
    x, y = grid(64, 64)
    r = np.sqrt(x * x + y * y)
    a = np.exp(-(r / 0.45) ** 2) * 0.75 + np.exp(-(r / 0.14) ** 2) * 0.5
    a *= np.clip((1 - r) / 0.15, 0, 1)
    save(a, 64, 64, "Mote")


def spark():
    x, y = grid(64, 64)
    r = np.sqrt(x * x + y * y)
    # A four-pointed twinkle: two thin crossed beams and a small bright core.
    beams = np.exp(-(np.abs(x) / 0.05) ** 2) * np.exp(-(np.abs(y) / 0.8) ** 2) + np.exp(-(np.abs(y) / 0.05) ** 2) * np.exp(-(np.abs(x) / 0.8) ** 2)
    a = beams * 0.85 + np.exp(-(r / 0.16) ** 2)
    a *= np.clip((1 - r) / 0.1, 0, 1)
    save(a, 64, 64, "Spark")


def main():
    os.makedirs(DIR, exist_ok=True)
    for fn in (halo, glow, ring, ring_runes, rays, mote, spark):
        fn()
    print("8 shapes in %s" % DIR)


if __name__ == "__main__":
    main()
