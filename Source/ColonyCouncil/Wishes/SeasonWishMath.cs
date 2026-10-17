using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The arithmetic behind the wishes of the land's year, kept free of game types so the tests can check it: clothes for
    /// the coming season (Wishes/WishWorkers_Seasons.cs), a sauna hot enough to be one, a cold store in hot country, and the
    /// day the fields open after a cold season (the planting day, Dilemmas/PlantingDay.cs).
    /// </summary>
    public static class SeasonWishMath
    {
        // ---------------- Clothes for the season ----------------

        /// <summary>A cool evening, or the warm part of the afternoon: how far the day strays from a twelfth's average.</summary>
        public const float DayDrift = 4f;
        /// <summary>How far short of the season someone's clothes must fall before they ask.</summary>
        public const float AskGap = 4f;
        /// <summary>Close enough: clothes that leave them this far from the season's worst still do.</summary>
        public const float Leeway = 2f;
        /// <summary>The least one new piece of clothing must add to be worth asking for.</summary>
        public const float MinGain = 3f;
        /// <summary>How many twelfths ahead the coming season reaches (now and the next three: about a season).</summary>
        public const int SeasonTwelfths = 3;

        /// <summary>
        /// The temperature to dress for: the coldest (or, with <paramref name="heat"/>, the warmest) twelfth's average from
        /// <paramref name="now"/> through the next <see cref="SeasonTwelfths"/>, a cool evening (a warm afternoon) beyond it.
        /// <paramref name="averages"/> holds the twelve twelfths of the year.
        /// </summary>
        public static float SeasonTemp(float[] averages, int now, bool heat)
        {
            if (averages == null || averages.Length == 0) return heat ? float.MinValue : float.MaxValue;
            int n = averages.Length;
            float best = heat ? float.MinValue : float.MaxValue;
            for (int i = 0; i <= SeasonTwelfths; i++)
            {
                float t = averages[((now + i) % n + n) % n];
                best = heat ? Math.Max(best, t) : Math.Min(best, t);
            }
            return heat ? best + DayDrift : best - DayDrift;
        }

        /// <summary>
        /// What their comfortable limit (<paramref name="comfy"/>: the coldest they bear in what they wear, or with
        /// <paramref name="heat"/> the hottest) should come to: the season's worst, give or take <see cref="Leeway"/>, but no
        /// further than one new piece of clothing can take them (<paramref name="gain"/>: the most the best one they could
        /// have would add over what it replaces). NaN when it isn't worth asking: they're dressed for it already, or nothing
        /// they could have helps enough.
        /// </summary>
        public static float ClothesTarget(float comfy, float season, float gain, bool heat)
        {
            if (float.IsNaN(comfy) || float.IsNaN(season) || float.IsNaN(gain)) return float.NaN;
            float gap = heat ? season - comfy : comfy - season;
            if (gap < AskGap || gain < MinGain) return float.NaN;
            float want = heat ? season - Leeway : season + Leeway;
            float reach = heat ? comfy + gain : comfy - gain;
            float target = heat ? Math.Min(want, reach) : Math.Max(want, reach);
            target = (float)Math.Round(target);
            if ((heat ? target - comfy : comfy - target) < MinGain) return float.NaN;
            return target;
        }

        /// <summary>What they wear now covers the season: comfortable down to (or up to) the target, within half a degree.</summary>
        public static bool Covered(float comfy, float target, bool heat) => heat ? comfy >= target - 0.5f : comfy <= target + 0.5f;

        /// <summary>The season it was asked for is over: it's no longer cold (or hot) enough out there for their clothes to fall short.</summary>
        public static bool SeasonPassed(float comfyNow, float seasonNow, bool heat) => heat ? seasonNow <= comfyNow : seasonNow >= comfyNow;

        // ---------------- A sauna ----------------

        /// <summary>How hot a sauna is kept (°C), when its heat can do it.</summary>
        public const float SaunaHeat = 28f;
        /// <summary>A sauna is small: at most this many cells.</summary>
        public const int SaunaMaxCells = 30;

        /// <summary>
        /// Hot enough to be a sauna: <see cref="SaunaHeat"/>, or, when its heat can't push that far (a campfire stops at about
        /// 23 °C), as hot as that heat can make a room, give or take two degrees. <paramref name="ceiling"/> is the highest
        /// temperature its heat source warms a room to.
        /// </summary>
        public static bool SaunaHotEnough(float roomTemp, float ceiling)
        {
            if (float.IsNaN(roomTemp)) return false;
            float wanted = Math.Min(SaunaHeat, ceiling - 2f);
            return roomTemp >= Math.Max(wanted, 18f);
        }

        // ---------------- Hot country ----------------

        /// <summary>A twelfth is hot when its average is at least this (°C): food left out rots at full speed, and fast.</summary>
        public const float HotTwelfth = 20f;
        /// <summary>A summer is hot when its hottest twelfth averages at least this (°C).</summary>
        public const float HotSummer = 26f;

        /// <summary>Hot country: hot most of the year (seven twelfths of twelve or more), or summer on a map whose summers are hot.</summary>
        public static bool HotLand(float[] averages, bool summerNow)
        {
            if (averages == null || averages.Length == 0) return false;
            int hot = 0;
            float max = float.MinValue;
            foreach (var t in averages)
            {
                if (t >= HotTwelfth) hot++;
                max = Math.Max(max, t);
            }
            return hot * 12 >= averages.Length * 7 || (summerNow && max >= HotSummer);
        }

        // ---------------- The fields opening ----------------

        /// <summary>The land has a cold season: some twelfths can grow and some can't.</summary>
        public static bool HasColdSeason(bool[] growable)
        {
            if (growable == null || growable.Length == 0) return false;
            bool any = false, none = false;
            foreach (var g in growable)
            {
                if (g) any = true;
                else none = true;
            }
            return any && none;
        }

        /// <summary>
        /// Twelfth <paramref name="now"/> opens the growing season: growing is possible in it, and wasn't in the one before.
        /// Never on land that grows all year or not at all.
        /// </summary>
        public static bool OpensGrowing(bool[] growable, int now)
        {
            if (!HasColdSeason(growable)) return false;
            int n = growable.Length;
            int i = ((now % n) + n) % n;
            return growable[i] && !growable[(i + n - 1) % n];
        }

        /// <summary>
        /// The planting day's window: the first twelfth of the growing season, or the first two days of the next when the
        /// opening twelfth was too short a spring to catch.
        /// </summary>
        public static bool PlantingWindow(bool[] growable, int now, int dayOfTwelfth)
        {
            if (OpensGrowing(growable, now)) return true;
            return dayOfTwelfth <= 1 && OpensGrowing(growable, now - 1);
        }

        /// <summary>The fields count as sown when nearly every cell meant for a crop has one (nine in ten).</summary>
        public static bool FullySown(int sown, int cells) => cells > 0 && sown * 10 >= cells * 9;
    }
}
