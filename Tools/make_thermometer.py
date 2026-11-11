#!/usr/bin/env python3
"""
Draws the thermometer beside the game's temperature line and on the vitals panel (Source/ColonyCouncil/Hud/Thermometer.cs):
three white-on-transparent layers, 48x96 RGBA, tinted in game.

    Back.png   the whole shape a little larger: drawn dark under the rest, so the glass and the colour read over snow
    Fill.png   the inside of the tube and the bulb: drawn in the temperature's colour, cut off at the temperature's height
    Glass.png  the glass round it: drawn light on top

The shapes are drawn as masks at 8x (384x768) in design units of 128x256 and scaled down, which gives clean edges. The
glass is thick and the tube wide, so the colour inside still shows at the game's smallest interface scale (about
12x24 on screen). TemperatureScaleMath.ThermoBulbTop and ThermoTubeTop are where BULB_INNER_TOP and TUBE_INNER_TOP
fall (Tools/Tests/TemperatureScaleTests.cs checks they agree).

Usage (from the repository root):
    python3 Tools/make_thermometer.py      # writes Textures/ColonyCouncil/Thermometer/*.png

Needs Pillow (pip install pillow).
"""
import os

from PIL import Image, ImageDraw, ImageFilter

W, H = 128, 256  # design units
K = 3  # drawn at 3x the design (384x768), then scaled down
OUT = (48, 96)  # texture size
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIR = os.path.join(ROOT, "Textures", "ColonyCouncil", "Thermometer")

# The tube (outer): its sides, top and corner radius; the bulb (outer): its centre and radius; the glass's thickness.
TUBE_X0, TUBE_X1, TUBE_TOP, TUBE_R = 36, 92, 8, 28
BULB_CX, BULB_CY, BULB_R = 64, 194, 52
GLASS = 15
# Where the inside begins at the top of the tube, and the top of the bulb's inside (design units from the top).
TUBE_INNER_TOP = TUBE_TOP + GLASS
BULB_INNER_TOP = BULB_CY - (BULB_R - GLASS)
# How far the dark back reaches beyond the glass.
RIM = 7


def new():
    return Image.new("L", (W * K, H * K), 0)


def shape(m, grow, fill=255):
    """The thermometer's outline grown by `grow` design units (negative: shrunk), filled."""
    d = ImageDraw.Draw(m)
    x0, x1, top, r = TUBE_X0 - grow, TUBE_X1 + grow, TUBE_TOP - grow, max(0, TUBE_R + grow)
    d.rounded_rectangle([x0 * K, top * K, x1 * K, BULB_CY * K], radius=r * K, fill=fill)
    br = BULB_R + grow
    d.ellipse([(BULB_CX - br) * K, (BULB_CY - br) * K, (BULB_CX + br) * K, (BULB_CY + br) * K], fill=fill)


def back():
    m = new()
    shape(m, RIM)
    return m


def fill():
    m = new()
    shape(m, -GLASS)
    return m


def glass():
    m = new()
    shape(m, 0)
    shape(m, -GLASS, 0)
    return m


def texture(mask):
    small = mask.filter(ImageFilter.GaussianBlur(K * 0.3)).resize(OUT, Image.LANCZOS)
    white = Image.new("L", OUT, 255)
    return Image.merge("RGBA", (white, white, white, small))


def main():
    os.makedirs(DIR, exist_ok=True)
    for name, fn in (("Back", back), ("Fill", fill), ("Glass", glass)):
        texture(fn()).save(os.path.join(DIR, name + ".png"), optimize=True)
    print("3 layers in %s; the bulb's inside from %d, the tube's from %d (of %d)" % (DIR, BULB_INNER_TOP, TUBE_INNER_TOP, H))


if __name__ == "__main__":
    main()
