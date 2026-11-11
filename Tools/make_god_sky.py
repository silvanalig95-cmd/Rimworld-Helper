#!/usr/bin/env python3
"""
Draws the gods' signs on the sky and at the edge of the land (Source/ColonyCouncil/Gods/OnMap/GodSky.cs): soft
white-on-transparent shapes, darkened or tinted in game, never a hard edge, so they read as a shadow or a light passing
rather than a picture.

    Wings.png   512x512  a vast winged shape, blurred: Smoulder's shadow crossing at noon (drawn dark, very faint)
    Vast.png    512x512  a great soft oval with a long trailing edge: Cirrus's shadow too large to have a source
    Figure.png  128x512  a tall thin standing shape, soft all round: Hush at the edge of the night (drawn pale)
    Dark.png    256x256  a soft round blot, darkest in the middle: Undertow's dark that a light doesn't reach
    Threads.png 256x256  a fine branching web of pale threads, thinning toward the edge: Morel's mycelium on a floor
    Rising.png  256x512  roots drawn up into a shape taller than a tree, a crown of branches and a knot for a face:
                         Hollin rising from the land for an hour (drawn bark-dark)

Every shape is drawn at 4x and scaled down, then blurred, so it has no edge at all.

Usage (from the repository root):
    python3 Tools/make_god_sky.py      # writes Textures/ColonyCouncil/Gods/Sky/*.png

Needs Pillow and numpy (pip install pillow numpy).
"""
import math
import os

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIR = os.path.join(ROOT, "Textures", "ColonyCouncil", "Gods", "Sky")
K = 4


def grid(w, h):
    ys, xs = np.mgrid[0:h * K, 0:w * K].astype(np.float64)
    return (xs + 0.5) / (w * K) * 2 - 1, (ys + 0.5) / (h * K) * 2 - 1


def save(alpha, w, h, name, blur):
    a = np.clip(alpha, 0, 1)
    img = Image.fromarray((a * 255).astype(np.uint8), "L").resize((w, h), Image.LANCZOS)
    img = img.filter(ImageFilter.GaussianBlur(blur))
    rgba = Image.merge("RGBA", (Image.new("L", (w, h), 255),) * 3 + (img,))
    os.makedirs(DIR, exist_ok=True)
    rgba.save(os.path.join(DIR, name + ".png"))


def smooth(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def wings():
    w, h = 512, 512
    x, y = grid(w, h)
    span = np.abs(y)
    # The body: a long neck and head forward (to the right), a long tapering tail behind.
    body = np.exp(-((y / (0.04 + 0.08 * np.clip(0.35 - np.abs(x), 0, 1))) ** 2)) * smooth(x, -0.98, -0.75) * (1 - smooth(x, 0.62, 0.8))
    head = np.exp(-(((x - 0.66) / 0.07) ** 2 + (y / 0.05) ** 2))
    # The wings: spread wide across the body, the leading edge swept back toward the tips, the trailing edge scalloped
    # between the long fingers of the wing.
    lead = 0.3 + 0.1 * span - 0.5 * span ** 2
    chord = 0.12 + 0.55 * (1 - span) ** 1.3
    fingers = 0.05 * (1 - np.cos(span * 4 * 2 * math.pi)) * smooth(span, 0.25, 0.55)
    trail = lead - chord + fingers
    wing = smooth(lead - x, -0.04, 0.04) * smooth(x - trail, -0.04, 0.04) * (1 - smooth(span, 0.88, 0.98))
    a = np.maximum(np.maximum(body, wing), head)
    save(a * 0.95, w, h, "Wings", 5)


def vast():
    w = h = 512
    x, y = grid(w, h)
    r = np.sqrt((x / 0.85) ** 2 + (y / 0.6) ** 2)
    core = 1 - smooth(r, 0.35, 1.0)
    trail = np.exp(-((y / (0.35 + 0.3 * np.clip(-x, 0, 1))) ** 2)) * smooth(-x, -0.2, 1.0) * 0.5
    save(np.maximum(core, trail), w, h, "Vast", 12)


def figure():
    w, h = 128, 512
    x, y = grid(w, h)
    # Taller than wide, narrow at the head and spreading a little toward the ground, fading at the top and the feet.
    width = 0.22 + 0.25 * np.clip((y + 0.6) / 1.6, 0, 1)
    body = np.exp(-((x / width) ** 2)) * smooth(y, -0.85, -0.6) * (1 - smooth(y, 0.8, 1.0))
    head = np.exp(-((x / 0.2) ** 2 + ((y + 0.72) / 0.09) ** 2))
    save(np.maximum(body, head) * 0.9, w, h, "Figure", 5)


def dark():
    w = h = 256
    x, y = grid(w, h)
    r = np.sqrt(x * x + y * y)
    save(1 - smooth(r, 0.15, 1.0), w, h, "Dark", 8)


def threads():
    w = h = 256
    rng = np.random.default_rng(7)
    a = np.zeros((h * K, w * K))
    # Threads that wander out from a few knots and branch, as mycelium does.
    def walk(x, y, ang, steps, width):
        for _ in range(steps):
            ang += rng.normal(0, 0.35)
            x += math.cos(ang) * 2.0 * K
            y += math.sin(ang) * 2.0 * K
            if not (0 <= x < w * K and 0 <= y < h * K):
                return
            r = max(1, int(width))
            xi, yi = int(x), int(y)
            a[max(0, yi - r):yi + r, max(0, xi - r):xi + r] = 1.0
            if rng.random() < 0.04 and steps > 10:
                walk(x, y, ang + rng.choice([-1, 1]) * rng.uniform(0.5, 1.2), steps // 2, width * 0.8)
    for _ in range(9):
        cx, cy = rng.uniform(0.25, 0.75) * w * K, rng.uniform(0.25, 0.75) * h * K
        for _ in range(4):
            walk(cx, cy, rng.uniform(0, 2 * math.pi), 60, 1.6 * K / 2)
    x, y = grid(w, h)
    fade = 1 - smooth(np.sqrt(x * x + y * y), 0.5, 1.0)
    save(a * fade * 0.85, w, h, "Threads", 0.6)


def rising():
    w, h = 256, 512
    rng = np.random.default_rng(11)
    a = np.zeros((h * K, w * K))

    def line(x, y, ang, steps, width, step=2.0, wander=0.12, branch=0.0, pull=0.0):
        for i in range(steps):
            ang += rng.normal(0, wander) + pull
            x += math.cos(ang) * step * K
            y += math.sin(ang) * step * K
            if not (0 <= x < w * K and 0 <= y < h * K):
                return
            r = max(1, int(width * (1 - 0.6 * i / steps)))
            xi, yi = int(x), int(y)
            a[max(0, yi - r):yi + r, max(0, xi - r):xi + r] = 1.0
            if branch and rng.random() < branch and steps - i > 8:
                line(x, y, ang + rng.choice([-1, 1]) * rng.uniform(0.4, 0.9), (steps - i) // 2, width * 0.6, step, wander, 0.0, pull)

    x, y = grid(w, h)
    # The body: roots twisted together into a standing shape, shoulders and a neck and a head, wide again at the ground,
    # with faint bark streaks down it.
    width = np.interp(y, [-0.5, -0.44, -0.36, 0.05, 0.55, 0.85], [0.07, 0.2, 0.27, 0.17, 0.24, 0.36])
    trunk = smooth(width - np.abs(x + 0.03 * np.sin(y * 6)), -0.025, 0.025) * smooth(y, -0.58, -0.5) * (1 - smooth(y, 0.84, 0.92))
    trunk *= 0.9 + 0.1 * np.sin(x * 40 + np.sin(y * 9) * 3)
    head = np.exp(-((x / 0.13) ** 2 + ((y + 0.6) / 0.1) ** 2) ** 2)
    a = np.maximum(a, np.maximum(trunk, head))
    # The roots it rose on, spreading out over the ground; its arms, roots hanging from the shoulders; a crown of
    # branches over its head.
    cx, base = w * K / 2, h * K * 0.9
    for i in range(7):
        side = -1 if i % 2 == 0 else 1
        line(cx + side * rng.uniform(0, 14) * K, base - 6 * K, math.pi / 2 - side * rng.uniform(0.4, 1.3), 40, 3.2 * K / 2, 1.6, 0.18, 0.05)
    for side in (-1, 1):
        line(cx + side * 0.21 * w * K / 2, h * K * 0.31, math.pi / 2 - side * 0.35, 70, 3.4 * K / 2, 1.6, 0.06, 0.06, side * 0.004)
    for i in range(6):
        side = -1 if i % 2 == 0 else 1
        line(cx + side * rng.uniform(2, 10) * K, h * K * 0.17, -math.pi / 2 + side * rng.uniform(0.3, 1.0), 40, 2.4 * K / 2, 2.0, 0.14, 0.08)
    # A knot for a face: a darker hollow (less of the shape) where a face would be.
    knot = np.exp(-(((x - 0.02) / 0.05) ** 2 + ((y + 0.6) / 0.035) ** 2))
    a = a * (1 - 0.55 * knot)
    fade = 1 - smooth(np.abs(x), 0.85, 1.0)
    save(a * fade * 0.95, w, h, "Rising", 1.5)


if __name__ == "__main__":
    wings()
    vast()
    figure()
    dark()
    threads()
    rising()
    print("wrote", DIR)
