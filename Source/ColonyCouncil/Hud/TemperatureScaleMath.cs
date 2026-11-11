using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The temperature in colour, on one scale by the colonists' comfort (the player's request, 10 November 2026: "can the
    /// temperature meter be clearly color coded from blue to red, so i can at immediate glance see how warm or cold it
    /// is"): deep blue in killing cold, lighter blues through chilly, white where people are comfortable, yellow and orange
    /// through hot, deep red in killing heat. The game's own readout at the bottom right (Hud/Patch_TemperatureReadout.cs)
    /// and the vitals' outdoor temperature (Hud/HudVitals.cs) both take it. Each step sits so far outside the comfort range
    /// (the game's human one, 16 to 26 °C, unless a mod moves it): around the human range the lines fall where the vitals'
    /// row turns orange and red (HudRank), and between two steps the colour goes over smoothly. Every colour is light enough
    /// to read on Rimfolk's dark panels in both its looks, and over the game's map behind a dark edge. Free of game types,
    /// so Tools/Tests can check it.
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
            if (float.IsNaN(comfyMin) || float.IsNaN(comfyMax) || float.IsInfinity(comfyMin) || float.IsInfinity(comfyMax))
            {
                comfyMin = HudRank.HumanComfyMin;
                comfyMax = HudRank.HumanComfyMax;
            }
            if (comfyMin > comfyMax) (comfyMin, comfyMax) = (comfyMax, comfyMin);
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

        /// <summary>How dark the edge round the game's readout is drawn (its alpha): enough for the colour to read over snow.</summary>
        public const float EdgeAlpha = 0.7f;
    }
}
