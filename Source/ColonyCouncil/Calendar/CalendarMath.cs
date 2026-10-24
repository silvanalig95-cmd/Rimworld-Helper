using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony calendar's arithmetic, free of game types so the tests can cover it (Tools/Tests): where a day falls in
    /// the sixty-day year and when it is due, how much each kind of day means to the colony, which day makes way when the
    /// calendar is full, how a day kept year after year grows into a tradition (and how one let pass lapses), and how to
    /// read a chronicle line from an older save back into its parts.
    /// </summary>
    public static class CalendarMath
    {
        public const int DaysPerYear = 60;
        public const int DaysPerQuadrum = 15;

        /// <summary>Days the calendar keeps on its own, besides the founding day and the days the player names.</summary>
        public const int MaxAutoDays = 12;

        /// <summary>Days the player can name.</summary>
        public const int MaxNamedDays = 8;

        /// <summary>Kept this many years running, a day becomes a tradition.</summary>
        public const int TraditionAfter = 3;

        /// <summary>A tradition let pass this many years running lapses.</summary>
        public const int LapseAfter = 2;

        /// <summary>A day that couldn't come round on its date (another day had it, or a fight) may still come the day after.</summary>
        public const int GraceDays = 1;

        // ---------------- Dates ----------------

        public static int Wrap(int day) => (day % DaysPerYear + DaysPerYear) % DaysPerYear;

        /// <summary>Days from today until the day next falls (0 when it's today).</summary>
        public static int DaysUntil(int today, int day) => Wrap(day - today);

        /// <summary>Days since the day last fell (0 when it's today).</summary>
        public static int DaysSince(int today, int day) => Wrap(today - day);

        /// <summary>The year in which the day last fell, seen from today.</summary>
        public static int LastOccurrence(int today, int year, int day) => today - DaysSince(today, day) >= 0 ? year : year - 1;

        /// <summary>
        /// The day fell today, or within the grace days, and hasn't come round for that year yet. <paramref name="occurrence"/>
        /// is the year it fell in (a day at the very end of a year may come round on the first day of the next).
        /// </summary>
        public static bool Due(int today, int year, int day, int lastYear, out int occurrence)
        {
            occurrence = LastOccurrence(today, year, day);
            return DaysSince(today, day) <= GraceDays && lastYear < occurrence;
        }

        /// <summary>
        /// The year a new day counts as last come round in. A day that happened just now, or whose date has passed this
        /// year, waits for next year; a later date comes this year; today's date comes today only when allowed (a day the
        /// player names for today, not one an old save remembers).
        /// </summary>
        public static int InitialLastYear(int today, int year, int day, bool allowToday)
        {
            day = Wrap(day);
            if (day > today) return year - 1;
            if (day == today && allowToday) return year - 1;
            return year;
        }

        /// <summary>Quadrum (0 to 3) and day of the quadrum (1 to 15) of a day of the year.</summary>
        public static void Split(int day, out int quadrum, out int dayOfQuadrum)
        {
            day = Wrap(day);
            quadrum = day / DaysPerQuadrum;
            dayOfQuadrum = day % DaysPerQuadrum + 1;
        }

        public static int Join(int quadrum, int dayOfQuadrum) =>
            Wrap(Math.Max(0, Math.Min(3, quadrum)) * DaysPerQuadrum + Math.Max(1, Math.Min(DaysPerQuadrum, dayOfQuadrum)) - 1);

        // ---------------- What a day means ----------------

        public const float FoundingWeight = 100f;
        public const float HarvestWeight = 40f;
        public const float SnowWeight = 30f;
        public const float WeddingWeight = 35f;
        public const float NemesisWeight = 45f;
        /// <summary>The day a nemesis first came back and the colony held: less than the day they fell.</summary>
        public const float NemesisReturnWeight = 34f;

        /// <summary>A battle: a little more for a bigger fight, and more than any clean win for each of ours who fell.</summary>
        public static float BattleWeight(int kills, int fallen) => 28f + 0.5f * Math.Min(Math.Max(kills, 0), 20) + 16f * Math.Min(Math.Max(fallen, 0), 4);

        /// <summary>A death: more for each who loved them, for their years with the colony, and for family left behind.</summary>
        public static float DeathWeight(int lovedBy, float years, bool family) =>
            22f + 8f * Math.Min(Math.Max(lovedBy, 0), 5) + 4f * Math.Min(Math.Max(years, 0f), 5f) + (family ? 10f : 0f);

        /// <summary>The day a founding member arrived, more for each year since.</summary>
        public static float ArrivalWeight(float years) => 18f + 3f * Math.Min(Math.Max(years, 0f), 5f);

        /// <summary>A long-serving colonist's birthday, more for each year with the colony.</summary>
        public static float BirthdayWeight(float years) => 14f + 3f * Math.Min(Math.Max(years, 0f), 6f);

        public const float FirstChildWeight = 45f;
        public const float WallsWeight = 32f;
        public const float NewCropWeight = 20f;
        public const float FirstTradeWeight = 24f;

        /// <summary>A caravan home safe: more for a longer journey (counted to twenty days) and for more of ours on it.</summary>
        public static float HomecomingWeight(float days, int travellers) =>
            18f + 0.8f * Math.Min(Math.Max(days, 0f), 20f) + 2f * Math.Min(Math.Max(travellers, 0), 5);

        /// <summary>A great hunt: more for a bigger bag (counted to fifteen).</summary>
        public static float HuntWeight(int kills) => 18f + 1.2f * Math.Min(Math.Max(kills, 0), 15);

        /// <summary>
        /// The day a camp left a gift: more for the time the colony spent at their fire (warmth, counted to twenty), for
        /// their good turns and the colony's gifts to them (to five), and for a longer stay (to ten days).
        /// </summary>
        public static float CampGiftWeight(int warmth, int kindnesses, float days) =>
            16f + 0.6f * Math.Min(Math.Max(warmth, 0), 20) + 2f * Math.Min(Math.Max(kindnesses, 0), 5) + 0.6f * Math.Min(Math.Max(days, 0f), 10f);

        /// <summary>How many things of a gift a day keeps track of (a ring of saplings is several).</summary>
        public const int MaxDayThings = 8;

        /// <summary>The colony took part in a world event: its answers counted as help at least once, or it helped see it through.</summary>
        public static bool TookPart(int help, bool helped) => help > 0 || helped;

        /// <summary>A world event the colony took part in: more for each answer that helped (to five), and for seeing it through.</summary>
        public static float SituationWeight(int help, bool helped) => 20f + 3f * Math.Min(Math.Max(help, 0), 5) + (helped ? 8f : 0f);

        /// <summary>A holiday of the colony's own faith (a rite held on a date).</summary>
        public const float FaithDayWeight = 30f;

        /// <summary>One of ours brought home from captivity: more the longer they were gone (counted to thirty days).</summary>
        public static float RescueWeight(float daysGone) => 30f + 0.5f * Math.Min(Math.Max(daysGone, 0f), 30f);

        /// <summary>A caravan counts as a journey once its people are away this many days.</summary>
        public const float HomecomingMinDays = 5f;

        /// <summary>A great hunt: this many animals brought down in a day.</summary>
        public const int GreatHuntKills = 5;

        /// <summary>The walls are up once the colony has this many wall sections.</summary>
        public const int WallsUp = 80;

        /// <summary>What a day means now: a tradition, and every year it was kept, count for more.</summary>
        public static float Standing(float weight, bool tradition, int keptYears) => weight + (tradition ? 40f : 0f) + 4f * Math.Min(Math.Max(keptYears, 0), 5);

        /// <summary>
        /// Room for one more day with this weight, among days with these weights (fixed ones never make way): -1 when
        /// there's room, the index of the weakest day to drop when the new one means more, or -2 when it doesn't.
        /// </summary>
        public static int MakeRoom(IList<float> weights, IList<bool> fixedDays, float candidate, int cap)
        {
            if (weights == null || weights.Count < cap) return -1;
            int weakest = -2;
            float least = float.MaxValue;
            for (int i = 0; i < weights.Count; i++)
            {
                if (fixedDays != null && i < fixedDays.Count && fixedDays[i]) continue;
                if (weights[i] < least)
                {
                    least = weights[i];
                    weakest = i;
                }
            }
            return weakest >= 0 && candidate > least ? weakest : -2;
        }

        // ---------------- Traditions ----------------

        public enum KeepResult
        {
            Kept,
            /// <summary>Kept long enough: it's a tradition now.</summary>
            Born,
            /// <summary>An established tradition, kept again.</summary>
            KeptTradition
        }

        public enum PassResult
        {
            /// <summary>Let pass; nobody expected it yet.</summary>
            Passed,
            /// <summary>A tradition broken once: the colony is disappointed.</summary>
            Broken,
            /// <summary>Broken again: it's no longer a tradition.</summary>
            Lapsed
        }

        public static KeepResult Keep(ref int keptYears, ref bool tradition, ref int breaks)
        {
            keptYears++;
            breaks = 0;
            if (tradition) return KeepResult.KeptTradition;
            if (keptYears < TraditionAfter) return KeepResult.Kept;
            tradition = true;
            return KeepResult.Born;
        }

        public static PassResult Pass(ref int keptYears, ref bool tradition, ref int breaks)
        {
            keptYears = 0;
            if (!tradition) return PassResult.Passed;
            breaks++;
            if (breaks < LapseAfter) return PassResult.Broken;
            tradition = false;
            breaks = 0;
            return PassResult.Lapsed;
        }

        /// <summary>Years still to keep before the day becomes a tradition (0 once it is one).</summary>
        public static int YearsToTradition(int keptYears, bool tradition) => tradition ? 0 : Math.Max(1, TraditionAfter - Math.Max(keptYears, 0));

        // ---------------- Reading old chronicle lines ----------------

        private static readonly Regex RichTag = new Regex("<[^>]*>", RegexOptions.Compiled);
        private static readonly Regex Placeholder = new Regex(@"\{[^{}]*\}", RegexOptions.Compiled);

        /// <summary>Text without the color tags names are shown with.</summary>
        public static string StripTags(string s) => s == null ? "" : RichTag.Replace(s, "");

        /// <summary>
        /// A pattern matching what a translation template became once filled in: each placeholder ({0}, {A_nameDef})
        /// becomes a group. <paramref name="whole"/> off lets more text follow (a line the game added after it).
        /// </summary>
        public static Regex TemplateRegex(string template, bool whole)
        {
            string t = StripTags(template ?? "");
            var sb = new StringBuilder("^");
            int at = 0;
            foreach (Match m in Placeholder.Matches(t))
            {
                sb.Append(Regex.Escape(t.Substring(at, m.Index - at)));
                sb.Append("(.+?)");
                at = m.Index + m.Length;
            }
            sb.Append(Regex.Escape(t.Substring(at)));
            if (whole) sb.Append("$");
            return new Regex(sb.ToString(), RegexOptions.Singleline);
        }

        /// <summary>The filled-in parts of a chronicle line written from this template, or null when it wasn't.</summary>
        public static List<string> Parse(Regex pattern, string text)
        {
            if (pattern == null || text == null) return null;
            var m = pattern.Match(StripTags(text));
            if (!m.Success) return null;
            var parts = new List<string>(m.Groups.Count - 1);
            for (int i = 1; i < m.Groups.Count; i++) parts.Add(m.Groups[i].Value.Trim());
            return parts;
        }
    }
}
