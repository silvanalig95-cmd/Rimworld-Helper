using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The small rules of the travellers who seek out the remote (docs/design/stage1-region.md, item 5; DESIGN.md, "The
    /// region"): how much more often camps and Rimfolk's visitors come to a home by its remoteness, how much a kind of
    /// camp that seeks something weighs when the land near the home has it, which reason a traveller gives, when the land
    /// has each reason, the far towns their news is of, and the bounds of what they tell. Pure (no game types):
    /// Tools/Tests (TravelTests) checks them.
    /// </summary>
    public static class TravelMath
    {
        // ---------------- The rates, by remoteness ----------------

        /// <summary>A camp comes up to this much more often at full remoteness (decision 8: up to 60% more).</summary>
        public const float CampBoost = 0.6f;
        /// <summary>The rest after a camp is up to this much shorter at full remoteness.</summary>
        public const float GapCut = 0.3f;
        /// <summary>The rest after a camp is never shorter than this share of its usual length.</summary>
        public const float MinGap = 0.4f;
        /// <summary>A kind that seeks something the land near the home has weighs 1 + this × remoteness.</summary>
        public const float SeekBoost = 2f;
        /// <summary>Rimfolk's visitors come up to this much more often at full remoteness.</summary>
        public const float VisitorBoost = 0.6f;
        /// <summary>The setting ("Travellers to the remote") runs from 0 (as before) to this.</summary>
        public const float MaxRate = 2f;

        /// <summary>The setting within its range (0 to 2; 1 by default).</summary>
        public static float Rate(float setting) => float.IsNaN(setting) ? 1f : Math.Max(0f, Math.Min(MaxRate, setting));

        /// <summary>How much of the remoteness counts: the remoteness (0 to 1) times the setting.</summary>
        public static float Pull(float remote, float rate) => Clamp01(remote) * Rate(rate);

        /// <summary>A camp's daily chance at a home this remote: the base chance times 1 + 0.6 × remoteness (by the setting).</summary>
        public static float CampChance(float baseChance, float remote, float rate) => Math.Max(0f, baseChance) * (1f + CampBoost * Pull(remote, rate));

        /// <summary>The rest after a camp at a home this remote, as a share of its usual length: 1 − 0.3 × remoteness, never under 0.4.</summary>
        public static float GapFactor(float remote, float rate) => Math.Max(MinGap, 1f - GapCut * Pull(remote, rate));

        /// <summary>
        /// A kind's weight among the camps that may come: 1 + 2 × remoteness when it seeks something the land near the home
        /// has (<paramref name="hasReason"/>), else its base weight (1).
        /// </summary>
        public static float SeekWeight(float remote, float rate, bool hasReason) => hasReason ? 1f + SeekBoost * Pull(remote, rate) : 1f;

        /// <summary>One of Rimfolk's visitor events' weight at a home this remote: 1 + 0.6 × remoteness.</summary>
        public static float VisitorFactor(float remote, float rate) => 1f + VisitorBoost * Pull(remote, rate);

        /// <summary>
        /// The share of Rimfolk's events at a home that are visitors, by weight (0 to 1): the visitors' weight over all of
        /// it, 0 with none.
        /// </summary>
        public static float VisitorShare(float visitors, float total) => total <= 0f || visitors <= 0f ? 0f : Clamp01(visitors / total);

        /// <summary>
        /// The home's next wait between Rimfolk's own events, as a share of its usual length. With the visitors weighed
        /// 1 + 0.6 × remoteness among the events and the wait cut by this, the visitors come that much more often and the
        /// other events at the rate they always did: 1 / (1 + 0.6 × remoteness × the visitors' share before the boost).
        /// </summary>
        public static float IntervalFactor(float remote, float rate, float visitorShare) => 1f / (1f + VisitorBoost * Pull(remote, rate) * Clamp01(visitorShare));

        /// <summary>
        /// The visitors' share of the events once weighed by <paramref name="factor"/>: what <see cref="IntervalFactor"/>'s
        /// rule makes of the share <paramref name="share"/> they had (for the tests and the Almanac's numbers).
        /// </summary>
        public static float BoostedShare(float share, float factor)
        {
            share = Clamp01(share);
            float w = share * Math.Max(0f, factor);
            float total = w + (1f - share);
            return total <= 0f ? 0f : w / total;
        }

        // ---------------- Reasons the land gives ----------------

        /// <summary>A relic of the colony's faith kept on the map (Ideology).</summary>
        public const string Relic = "relic";
        /// <summary>A landmark of the colony's land (its tile's landmark).</summary>
        public const string Wonder = "wonder";
        /// <summary>A Gauranlen tree (Ideology) or an anima tree (Royalty) on the map.</summary>
        public const string Trees = "trees";
        /// <summary>Ancient ruins on the colony's land, outside its home area.</summary>
        public const string Ruins = "ruins";
        /// <summary>A ruin in the region: an abandoned town within the region map's reach.</summary>
        public const string RuinRegion = "ruinRegion";
        /// <summary>The colony's books: its bookcases hold <see cref="MinBooks"/> or more.</summary>
        public const string Library = "library";
        /// <summary>The colony's history: a year old or more, with <see cref="HistoryMilestones"/> milestones or more in its chronicle.</summary>
        public const string History = "history";
        /// <summary>Ore in the colony's rock, in sight.</summary>
        public const string Ore = "ore";
        /// <summary>A rush in the region (a world event of the kind "rush": a silver rush).</summary>
        public const string Rush = "rush";
        /// <summary>Wild grass in its growing season.</summary>
        public const string Grazing = "grazing";
        /// <summary>A road through the colony's land.</summary>
        public const string Road = "road";
        /// <summary>The colony is far from every market: rather remote or more.</summary>
        public const string Far = "far";

        /// <summary>Every reason a traveller may give, in the order the land is read.</summary>
        public static readonly string[] Reasons = { Relic, Wonder, Trees, Ruins, RuinRegion, Library, History, Ore, Rush, Grazing, Road, Far };

        /// <summary>The reasons whose thing is on the colony's map, to be looked at (the others lie out in the world, or are the land itself).</summary>
        public static readonly string[] OnTheMap = { Relic, Trees, Ruins, Library, Ore };

        public static bool Known(string reason) => reason != null && Array.IndexOf(Reasons, reason) >= 0;

        /// <summary>A kind that seeks one of <paramref name="seeks"/> finds one of them in <paramref name="holds"/>.</summary>
        public static bool AnyHolds(IList<string> seeks, ICollection<string> holds)
        {
            if (seeks == null || holds == null) return false;
            foreach (var s in seeks)
                if (s != null && holds.Contains(s))
                    return true;
            return false;
        }

        /// <summary>
        /// The reason a traveller gives, from those their kind seeks (<paramref name="seeks"/>, strongest first) that the land
        /// holds: the first of them when <paramref name="roll"/> is under 0.6, else one of the others by the roll; null when
        /// none holds (the kind came for no reason of the land's, at its base weight).
        /// </summary>
        public static string Pick(IList<string> seeks, ICollection<string> holds, float roll)
        {
            if (seeks == null || holds == null) return null;
            var have = new List<string>();
            foreach (var s in seeks)
                if (s != null && holds.Contains(s) && !have.Contains(s))
                    have.Add(s);
            if (have.Count == 0) return null;
            if (have.Count == 1 || roll < 0.6f) return have[0];
            int i = 1 + (int)Math.Floor((Clamp01(roll) - 0.6f) / 0.4f * (have.Count - 1));
            return have[Math.Min(have.Count - 1, Math.Max(1, i))];
        }

        /// <summary>The colony's books count as a library from this many.</summary>
        public const int MinBooks = 6;

        public static bool LibraryHolds(int books) => books >= MinBooks;

        /// <summary>The colony's history draws scholars once it is a year old (60 days) with this many milestones in its chronicle.</summary>
        public const int HistoryDays = 60, HistoryMilestones = 5;

        public static bool HistoryHolds(int daysOld, int milestones) => daysOld >= HistoryDays && milestones >= HistoryMilestones;

        /// <summary>
        /// Ore enough to draw prospectors: cells of a rich ore in sight (anything but plain steel), or a great deal of
        /// steel in sight.
        /// </summary>
        public const int RichOreCells = 6, SteelCells = 40;

        public static bool OreHolds(int rich, int steel) => rich >= RichOreCells || steel >= SteelCells;

        /// <summary>Wild grass enough for a herd: the land's plant density at least this, in its growing season.</summary>
        public const float GrazingDensity = 0.3f;

        public static bool GrazingHolds(float plantDensity, bool growingSeason) => growingSeason && plantDensity >= GrazingDensity;

        /// <summary>"Far from every market": rather remote or more (RegionMath.RemoteLevel 2 and up).</summary>
        public const int FarLevel = 2;

        public static bool FarHolds(float remote) => RegionMath.RemoteLevel(remote) >= FarLevel;

        // ---------------- News of far towns ----------------

        /// <summary>A camp of travellers tells at most this many pieces of news of far towns in its stay.</summary>
        public const int FarNewsPerCamp = 2;
        /// <summary>The far towns looked at for news: the nearest so many beyond the reach.</summary>
        public const int FarTowns = 12;
        /// <summary>Far towns lie beyond the reach and within this many times it.</summary>
        public const float FarOuter = 3f;
        /// <summary>Each hour a colonist sits with them, the chance one of them tells news of a far town.</summary>
        public const float FarNewsHourChance = 0.15f;
        /// <summary>The same far town's news is told again only after this many days.</summary>
        public const int FarSameDays = 10;

        /// <summary>
        /// The far towns, as indices into the lists, nearest first: those that <paramref name="fit"/>, farther than
        /// <paramref name="reach"/> tiles (beyond the region kept in detail) and no farther than <paramref name="outer"/>,
        /// the nearest <paramref name="max"/>.
        /// </summary>
        public static List<int> FarTownsOf(IList<float> distances, IList<bool> fit, float reach, float outer, int max = FarTowns)
        {
            var result = new List<int>();
            int n = distances?.Count ?? 0;
            if (n == 0 || max <= 0 || outer <= reach) return result;
            var order = new List<int>();
            for (int i = 0; i < n; i++)
                if ((fit == null || i < fit.Count && fit[i]) && distances[i] > reach && distances[i] <= outer)
                    order.Add(i);
            order.Sort((a, b) => distances[a] != distances[b] ? distances[a].CompareTo(distances[b]) : a.CompareTo(b));
            for (int i = 0; i < order.Count && result.Count < max; i++) result.Add(order[i]);
            return result;
        }

        /// <summary>What a far town's news can be of, the weightiest first: a world event about its people, a war of theirs, the town new, a site near it, a quest of theirs.</summary>
        public static readonly string[] NewsKinds = { "situation", "war", "new", "site", "quest" };

        /// <summary>How weighty a kind of news is (lower comes first); unknown kinds last.</summary>
        public static int NewsRank(string kind)
        {
            int i = kind == null ? -1 : Array.IndexOf(NewsKinds, kind);
            return i < 0 ? NewsKinds.Length : i;
        }

        /// <summary>A town counts as new (its news "a new town") for this many days after it was made.</summary>
        public const int NewTownDays = 60;

        /// <summary>A camp may still tell news: under <see cref="FarNewsPerCamp"/> told.</summary>
        public static bool MayTell(int told) => told < FarNewsPerCamp;

        /// <summary>The same news of a far town was told within <see cref="FarSameDays"/> (it is not told again).</summary>
        public static bool ToldLately(int lastTick, int now, int dayTicks = RegionMath.DayTicks) => lastTick >= 0 && now >= lastTick && now - lastTick < (long)FarSameDays * dayTicks;

        // ---------------- A quarrel over a claim ----------------

        /// <summary>
        /// Prospectors who came for the ore may fall out over a claim: once a stay at most, this chance a stay, as two of
        /// their own come to blows (the game's own social fight, between the two of them).
        /// </summary>
        public const float QuarrelChance = 0.12f;

        /// <summary>The chance on one hour of the day's working hours, so a stay of about a week comes to <see cref="QuarrelChance"/>.</summary>
        public static float QuarrelHourChance(float stayDays, int workHours = 8)
        {
            float hours = Math.Max(1f, stayDays) * Math.Max(1, workHours);
            return 1f - (float)Math.Pow(1f - QuarrelChance, 1f / hours);
        }

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
