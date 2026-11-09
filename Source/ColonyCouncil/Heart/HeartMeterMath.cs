using System;
using System.Collections.Generic;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>
    /// The heart's meters, free of game types so the tests can check them (the player, 9 November 2026: "the strain level
    /// etc. should be graphically more easier to see, perhaps also like a level bar or so. the "give day off" should also be
    /// in this menu and not in the pawn bar"). Where the ticks sit and which of their small labels fit, how the meters and
    /// the day off's place share a width, how strain moved today, when time off ends in the day's hours, the day off's
    /// reason in a few words, and RimHUD's strain line cut to its cell (Heart/HeartMeter.cs and Integrations/RimHud.cs draw
    /// them).
    /// </summary>
    public static class HeartMeterMath
    {
        /// <summary>Strain's ticks: where each band starts (strained, worn down, near breaking) and where a day to themselves may come (85).</summary>
        public static readonly float[] StrainTicks =
        {
            HeartMath.BandStart(StrainBand.Strained), HeartMath.BandStart(StrainBand.Worn), HeartMath.BandStart(StrainBand.Breaking), HeartMath.EpisodeAt
        };

        /// <summary>The good side's ticks: steady, in good heart, in high spirits.</summary>
        public static readonly float[] GoodTicks = { HeartMath.SteadyAt, HeartMath.GoodHeartAt, HeartMath.HighSpiritsAt };

        /// <summary>A meter's bar (as tall as the mood bar above it) and the row of small labels under it.</summary>
        public const float BarHeight = 18f, LabelRow = 14f;

        /// <summary>A meter in all, and the space between the two.</summary>
        public const float MeterHeight = BarHeight + LabelRow, MeterGap = 4f;

        /// <summary>Beside a bar: the trend ("-7 today" and its arrow).</summary>
        public const float TrendWidth = 72f;

        /// <summary>Between the meters and the day off's place at their right, and the least a bar is drawn at beside it.</summary>
        public const float SlotGap = 8f, MinBar = 170f;

        /// <summary>The day off's place: its button and the line under it.</summary>
        public const float SlotMin = 104f, SlotMax = 150f;

        /// <summary>How much strain must move since the day began before the trend says so.</summary>
        public const int TrendFrom = 2;

        /// <summary>The lasting weight is marked from this (as the heart's page says it).</summary>
        public const float FloorFrom = 5f;

        /// <summary>How strain moved since the day began, to the point: 0 while it moved less than <see cref="TrendFrom"/>.</summary>
        public static int Moved(float now, float dayAgo)
        {
            int m = (int)Math.Round(now - dayAgo, MidpointRounding.AwayFromZero);
            return Math.Abs(m) >= TrendFrom ? m : 0;
        }

        /// <summary>A value's place on a bar <paramref name="width"/> wide, out of 100, kept on the bar.</summary>
        public static float At(float value, float width) => width * Clamp01(value / HeartMath.Max);

        /// <summary>
        /// The small labels under the ticks: each centred on its tick and kept on the bar, the left x of each, or NaN where it
        /// would touch the one before it (the first ones are kept: a band's start before the day to themselves).
        /// </summary>
        public static float[] LabelLefts(float[] ticks, float[] widths, float barWidth, float gap = 3f)
        {
            var lefts = new float[ticks.Length];
            LabelLefts(ticks, widths, barWidth, lefts, gap);
            return lefts;
        }

        /// <summary>The same into <paramref name="lefts"/> (as long as the ticks at least), so a meter drawn every frame makes no array.</summary>
        public static void LabelLefts(float[] ticks, float[] widths, float barWidth, float[] lefts, float gap = 3f)
        {
            float lastRight = float.NegativeInfinity;
            for (int i = 0; i < ticks.Length; i++)
            {
                float w = i < widths.Length ? widths[i] : 0f;
                float left = At(ticks[i], barWidth) - w / 2f;
                if (left < 0f) left = 0f;
                if (left + w > barWidth) left = barWidth - w;
                if (left < lastRight + gap || left < 0f)
                {
                    lefts[i] = float.NaN;
                    continue;
                }
                lefts[i] = left;
                lastRight = left + w;
            }
        }

        /// <summary>How the meters and the day off's place share a width.</summary>
        public struct Layout
        {
            /// <summary>Each bar's width (the trend beside it).</summary>
            public float bar;
            /// <summary>The day off's place: its left and width, beside the meters or under them (<see cref="under"/>); 0 wide with none.</summary>
            public float slotX, slotW;
            public bool under;
        }

        /// <summary>
        /// The meters across <paramref name="width"/>, with the day off's place at their right (<paramref name="slotWanted"/>
        /// wide, 0 for none) while a bar still gets <see cref="MinBar"/>; else the place goes under the meters, as wide as it
        /// wants. The place keeps one spot whatever the colonist's state, so it's easy to find.
        /// </summary>
        public static Layout Lay(float width, float slotWanted)
        {
            var l = new Layout();
            float slot = slotWanted <= 0f ? 0f : Math.Max(SlotMin, Math.Min(SlotMax, slotWanted));
            float beside = width - TrendWidth - (slot > 0f ? slot + SlotGap : 0f);
            if (slot <= 0f || beside >= MinBar)
            {
                l.bar = Math.Max(0f, beside);
                l.slotW = slot;
                l.slotX = slot > 0f ? width - slot : width;
                return l;
            }
            l.bar = Math.Max(0f, width - TrendWidth);
            l.under = true;
            l.slotW = Math.Min(width, slot);
            l.slotX = 0f;
            return l;
        }

        /// <summary>
        /// When time off ends, in the day's hours: the hour it ends at (rounded) and how many days on (0 today, 1 tomorrow), from
        /// the hour now (with its fraction) and the hours left.
        /// </summary>
        public static void Until(float hourNow, float hoursLeft, out int hour, out int dayOffset)
        {
            int end = (int)Math.Round(Math.Max(0f, hourNow) + Math.Max(0f, hoursLeft), MidpointRounding.AwayFromZero);
            hour = end % 24;
            dayOffset = end / 24;
        }

        private const string WhyNotPrefix = "CC_Count_WhyNot_";

        /// <summary>
        /// The day off's reason it can't be given, in a few words under its button ("again in 3 days"): the short key for a
        /// reason key of <c>HeartTimeOff.WhyNotDayOffKey</c>, or null where the day off's place doesn't show at all (days off
        /// switched off, no heart, not one of the colony's free people).
        /// </summary>
        public static string ShortWhy(string key)
        {
            if (key == null || !key.StartsWith(WhyNotPrefix, StringComparison.Ordinal)) return null;
            string why = key.Substring(WhyNotPrefix.Length);
            switch (why)
            {
                case "Off":
                case "NoHeart":
                case "NotFree":
                    return null;
                default:
                    return "CC_Meter_Why_" + why;
            }
        }

        /// <summary>The reasons the day off's place has a few words for (each has its key in Rimfolk_HeartMeters.xml).</summary>
        public static readonly string[] ShortWhys = { "Already", "NotStrained", "Cooldown", "Away", "Child", "Busy", "Danger", "Care", "Nursing", "TooFew" };

        /// <summary>The text without its colour tags ("&lt;color=#ff0000&gt;Worn 60&lt;/color&gt;" is "Worn 60"), for measuring.</summary>
        public static string StripTags(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return s ?? "";
            var sb = new StringBuilder(s.Length);
            int i = 0;
            while (i < s.Length)
            {
                if (s[i] == '<')
                {
                    int close = s.IndexOf('>', i + 1);
                    string tag = close > i ? s.Substring(i + 1, close - i - 1) : null;
                    if (tag != null && (tag.StartsWith("color", StringComparison.Ordinal) || tag == "/color" || tag == "b" || tag == "/b" || tag == "i" || tag == "/i"))
                    {
                        i = close + 1;
                        continue;
                    }
                }
                sb.Append(s[i]);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>
        /// A line of parts cut to a width without cutting a word: the first part always, then each after it in its order where
        /// it still fits (one too long is left out, a shorter one after it may still come), joined by <paramref name="sep"/>.
        /// Widths are measured on the text without its colour tags. RimHUD's strain line ("Worn 60 · fighting"): its value
        /// cell is cut from the left when too long, so it is kept short enough instead.
        /// </summary>
        public static string Fit(IList<string> parts, Func<string, float> width, float budget, string sep = " · ")
        {
            if (parts == null || parts.Count == 0) return "";
            var kept = new List<string>(parts.Count);
            string plain = "";
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                string next = kept.Count == 0 ? StripTags(part) : plain + sep + StripTags(part);
                if (kept.Count > 0 && width(next) > budget) continue;
                kept.Add(part);
                plain = next;
            }
            return string.Join(sep, kept);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
