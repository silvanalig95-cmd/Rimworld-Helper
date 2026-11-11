using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The temperature in colour, on one scale by the colonists' comfort (the player's request, 10 November 2026: "can the
    /// temperature meter be clearly color coded from blue to red, so i can at immediate glance see how warm or cold it
    /// is"): deep blue in killing cold, lighter blues through chilly, white where people are comfortable, yellow and orange
    /// through hot, deep red in killing heat; then, at the player's word ("this should not just be colored text, but some
    /// symbol/field that's colored"), a thermometer in that colour, filled to the temperature's height, with the word for
    /// it. The game's own readout at the bottom right (Hud/Patch_TemperatureReadout.cs) and the vitals' outdoor temperature
    /// (Hud/HudVitals.cs) both take it. Each step sits so far outside the comfort range (the game's human one, 16 to 26 °C,
    /// unless a mod moves it): around the human range the lines fall where the vitals' row turns orange and red (HudRank),
    /// and between two steps the colour goes over smoothly. Every colour is light enough to read on Rimfolk's dark panels in
    /// both its looks, and inside the thermometer's dark back over the game's map. Free of game types, so Tools/Tests can
    /// check it.
    /// </summary>
    public static class TemperatureScaleMath
    {
        // ---------------- The colours (0xRRGGBB) ----------------

        /// <summary>Killing cold: as deep a blue as still reads on a dark panel.</summary>
        public const uint DeepBlue = 0x5e87ff;
        /// <summary>Hard frost.</summary>
        public const uint Blue = 0x74a2ff;
        /// <summary>Freezing.</summary>
        public const uint LightBlue = 0x98c4ff;
        /// <summary>Chilly: at the line where the cold starts to harm someone not dressed for it.</summary>
        public const uint PaleBlue = 0xc4e0ff;
        /// <summary>Comfortable.</summary>
        public const uint Neutral = 0xf2f2f2;
        /// <summary>Warm.</summary>
        public const uint PaleYellow = 0xffec9e;
        /// <summary>Hot: at the line where the heat starts to harm.</summary>
        public const uint Yellow = 0xffcf40;
        /// <summary>Fierce heat.</summary>
        public const uint Orange = 0xff9a2e;
        /// <summary>Killing heat.</summary>
        public const uint DeepRed = 0xff5248;

        // ---------------- Where each colour sits ----------------

        /// <summary>How far outside the comfort range is still safe, as the game counts it (GenTemperature.SafeTemperatureRange).</summary>
        public const float SafeMargin = HudRank.SafeMargin;

        /// <summary>Below the comfort range: freezing, hard frost (the vitals' orange line) and killing cold (its red line), with the human range.</summary>
        public const float FreezingBelow = 20f, FrostBelow = HudRank.HumanComfyMin - HudRank.HardFrost, DeadlyBelow = HudRank.HumanComfyMin - HudRank.DeadlyCold;

        /// <summary>Above the comfort range: warm, fierce heat (the vitals' orange line) and killing heat (its red line), with the human range.</summary>
        public const float WarmAbove = 6f, FierceAbove = HudRank.FierceHeat - HudRank.HumanComfyMax, DeadlyAbove = HudRank.DeadlyHeat - HudRank.HumanComfyMax;

        /// <summary>The cold side, from the bottom of the comfort range down (degrees under it, colour).</summary>
        public static readonly (float under, uint rgb)[] Cold =
        {
            (0f, Neutral), (SafeMargin, PaleBlue), (FreezingBelow, LightBlue), (FrostBelow, Blue), (DeadlyBelow, DeepBlue)
        };

        /// <summary>The warm side, from the top of the comfort range up (degrees over it, colour).</summary>
        public static readonly (float over, uint rgb)[] Hot =
        {
            (0f, Neutral), (WarmAbove, PaleYellow), (SafeMargin, Yellow), (FierceAbove, Orange), (DeadlyAbove, DeepRed)
        };

        /// <summary>
        /// The colour of <paramref name="celsius"/> around a comfort range: white inside it, then step by step out to deep blue
        /// and deep red, no further beyond those. An unknown temperature is white; an unknown or reversed range is the
        /// human one (a range the wrong way round is taken the right way).
        /// </summary>
        public static uint Rgb(float celsius, float comfyMin, float comfyMax)
        {
            if (float.IsNaN(celsius) || float.IsInfinity(celsius)) return Neutral;
            Range(ref comfyMin, ref comfyMax);
            if (celsius < comfyMin) return Along(Cold, comfyMin - celsius);
            if (celsius > comfyMax) return Along(Hot, celsius - comfyMax);
            return Neutral;
        }

        /// <summary>The colour <paramref name="d"/> degrees out along one side, going over smoothly between its steps.</summary>
        private static uint Along((float at, uint rgb)[] side, float d)
        {
            if (d <= side[0].at) return side[0].rgb;
            for (int i = 1; i < side.Length; i++)
            {
                if (d > side[i].at) continue;
                float t = (d - side[i - 1].at) / (side[i].at - side[i - 1].at);
                return Mix(side[i - 1].rgb, side[i].rgb, t);
            }
            return side[side.Length - 1].rgb;
        }

        /// <summary>Between two colours, channel by channel: <paramref name="t"/> 0 the first, 1 the second.</summary>
        public static uint Mix(uint a, uint b, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            uint Ch(int shift)
            {
                int ca = (int)((a >> shift) & 0xff), cb = (int)((b >> shift) & 0xff);
                int x = (int)Math.Round(ca + (cb - ca) * t);
                return (uint)Math.Max(0, Math.Min(255, x)) << shift;
            }
            return Ch(16) | Ch(8) | Ch(0);
        }

        /// <summary>The colour as rich text's hex ("5e87ff"), for a line the game draws itself.</summary>
        public static string Hex(uint rgb) => (rgb & 0xffffff).ToString("x6");

        /// <summary>A line of text wrapped in its colour as the game's labels read it ("&lt;color=#5e87ff&gt;-31C&lt;/color&gt;").</summary>
        public static string Wrap(string text, uint rgb) => string.IsNullOrEmpty(text) ? text : "<color=#" + Hex(rgb) + ">" + text + "</color>";

        // ---------------- The words ----------------

        /// <summary>
        /// The temperature in a word, for the colonists: killing cold at deep blue and under, cold under the game's safe
        /// line, chilly under the comfort range, comfortable in it, warm up to the safe line above it, hot under killing
        /// heat, killing heat at deep red and over. An unknown temperature is comfortable.
        /// </summary>
        public static TemperatureFeel Feel(float celsius, float comfyMin, float comfyMax)
        {
            if (float.IsNaN(celsius) || float.IsInfinity(celsius)) return TemperatureFeel.Comfortable;
            Range(ref comfyMin, ref comfyMax);
            if (celsius <= comfyMin - DeadlyBelow) return TemperatureFeel.KillingCold;
            if (celsius < comfyMin - SafeMargin) return TemperatureFeel.Cold;
            if (celsius < comfyMin) return TemperatureFeel.Chilly;
            if (celsius <= comfyMax) return TemperatureFeel.Comfortable;
            if (celsius <= comfyMax + SafeMargin) return TemperatureFeel.Warm;
            if (celsius < comfyMax + DeadlyAbove) return TemperatureFeel.Hot;
            return TemperatureFeel.KillingHeat;
        }

        /// <summary>An unknown range is the human one; one given the wrong way round is taken the right way.</summary>
        private static void Range(ref float comfyMin, ref float comfyMax)
        {
            if (float.IsNaN(comfyMin) || float.IsNaN(comfyMax) || float.IsInfinity(comfyMin) || float.IsInfinity(comfyMax))
            {
                comfyMin = HudRank.HumanComfyMin;
                comfyMax = HudRank.HumanComfyMax;
            }
            if (comfyMin > comfyMax) (comfyMin, comfyMax) = (comfyMax, comfyMin);
        }

        // ---------------- The thermometer ----------------

        /// <summary>
        /// How full the thermometer is, 0 to 1: empty (the bulb alone) at killing cold, full at killing heat, in a straight
        /// line between (with the human range: -30 °C empty, 21 °C about two thirds, 50 °C full). Unknown: the middle of the
        /// comfort range's.
        /// </summary>
        public static float Level(float celsius, float comfyMin, float comfyMax)
        {
            Range(ref comfyMin, ref comfyMax);
            if (float.IsNaN(celsius) || float.IsInfinity(celsius)) celsius = (comfyMin + comfyMax) / 2f;
            float lo = comfyMin - DeadlyBelow, hi = comfyMax + DeadlyAbove;
            float t = (celsius - lo) / (hi - lo);
            return t < 0f ? 0f : t > 1f ? 1f : t;
        }

        /// <summary>
        /// Where the inside of the bulb and of the tube's top fall in the thermometer's picture, as shares of its height from
        /// the bottom (Tools/make_thermometer.py: BULB_INNER_TOP 157 and TUBE_INNER_TOP 23 of 256 from the top).
        /// </summary>
        public const float ThermoBulbTop = (256f - 157f) / 256f, ThermoTubeTop = (256f - 23f) / 256f;

        /// <summary>How much of the fill's picture is shown, from the bottom: the bulb always, the tube up to the level.</summary>
        public static float Shown(float level)
        {
            if (float.IsNaN(level)) level = 0f;
            level = level < 0f ? 0f : level > 1f ? 1f : level;
            return ThermoBulbTop + level * (ThermoTubeTop - ThermoBulbTop);
        }

        /// <summary>How dark the thermometer's back is drawn (black, its alpha): enough for the glass and the colour to read over snow.</summary>
        public const float ThermoBackAlpha = 0.6f;

        /// <summary>The thermometer's size on screen beside the game's line: half as wide as tall, a little under the line's height.</summary>
        public const float ThermoHeight = 22f, ThermoWidth = 11f;

        /// <summary>How far the game's own words are tinted toward the temperature's colour (lightly: they stay near the game's white).</summary>
        public const float TextTintShare = 0.35f;

        /// <summary>The game's words beside the thermometer: white, a little toward the temperature's colour.</summary>
        public static uint TextTint(uint rgb) => Mix(0xffffff, rgb, TextTintShare);

        /// <summary>
        /// The coloured field behind the game's temperature line (the player, 11 November 2026: "a larger surface that's colored
        /// so it's more clearly visible, perhaps behind the text could be a colored background around the size of the
        /// field"): the scale's colour this strong, so it reads at a glance over the map.
        /// </summary>
        public const float FieldAlpha = 0.9f;

        /// <summary>The field's room left of the thermometer and right of the words, and its inset from the line's top and bottom.</summary>
        public const float FieldPadX = 5f, FieldPadY = 1f;

        /// <summary>Dark words, for the field's light colours.</summary>
        public const uint FieldInk = 0x161616;

        /// <summary>The words on the field: dark or white, whichever reads better on its colour (dark on every colour of the scale).</summary>
        public static uint FieldText(uint fill) => SkinMath.Contrast(fill, FieldInk) >= SkinMath.Contrast(fill, 0xffffff) ? FieldInk : 0xffffffu;

        /// <summary>How far the thermometer's fill is darkened on the field, so it stands out from a field of its own colour.</summary>
        public const float FieldThermoDark = 0.5f;

        /// <summary>The thermometer's fill on the field: the temperature's colour, darker.</summary>
        public static uint FieldThermo(uint fill) => Mix(fill, 0x000000, FieldThermoDark);
    }

    /// <summary>The temperature in a word, for the colonists (TemperatureScaleMath.Feel), coldest first.</summary>
    public enum TemperatureFeel
    {
        KillingCold,
        Cold,
        Chilly,
        Comfortable,
        Warm,
        Hot,
        KillingHeat
    }
}
