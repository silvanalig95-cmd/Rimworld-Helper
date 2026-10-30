using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// The numbers of the other settlers' year (World/Workers_World.cs, Council_WS_RivalColony): how their steading
    /// grows, how the colony stands with them by what it answered, and when they are few enough to take in. Pure, so
    /// Tools/Tests can check it.
    /// </summary>
    public static class RivalMath
    {
        /// <summary>Households at the start, and the most the steading grows to in its first year.</summary>
        public const int StartMin = 3, StartMax = 5, Most = 9;
        /// <summary>A household more every so many days; one more at <see cref="WarmDay"/> when the colony has been warm to them.</summary>
        public const float GrowDays = 9f, WarmDay = 20f;
        /// <summary>Few enough to take into the colony when asked.</summary>
        public const int AbsorbMost = 5;

        /// <summary>Answers that warmed the settlers to the colony, and answers that slighted them (the situation's flags).</summary>
        public static readonly string[] WarmFlags = { "gift", "trade", "shared", "fed", "sold", "peace" };
        public static readonly string[] SlightFlags = { "cold", "warned", "refused", "keepOff" };

        public static int Households(int start, float daysIn, bool warm)
        {
            int n = start + (int)Math.Floor(Math.Max(0f, daysIn) / GrowDays);
            if (warm && daysIn >= WarmDay) n++;
            return Math.Min(Most, Math.Max(1, n));
        }

        public static int Warmth(IEnumerable<string> flags) => flags == null ? 0 : flags.Count(WarmFlags.Contains);

        public static int Slights(IEnumerable<string> flags) => flags == null ? 0 : flags.Count(SlightFlags.Contains);

        /// <summary>The settlers have soured on the colony: slighted at least once, and not made up for by as many warm answers.</summary>
        public static bool Sour(IEnumerable<string> flags)
        {
            var list = flags?.ToList() ?? new List<string>();
            int slights = Slights(list);
            return slights > 0 && slights >= Warmth(list);
        }

        /// <summary>Warm to the colony: more warm answers than slights, and at least one.</summary>
        public static bool Warm(IEnumerable<string> flags)
        {
            var list = flags?.ToList() ?? new List<string>();
            return Warmth(list) > 0 && Warmth(list) > Slights(list);
        }

        public static bool CanAbsorb(int households) => households <= AbsorbMost;
    }
}
