using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The heart's good side (the player's request of 6 November 2026: "there should perhaps also be a counterweight/side
    /// to strain, right? If they have positive impacts over an extended period"). One heart with two sides, not a new
    /// system: strain on one side, the good side on the other (0 to 100 each), never both at once. Hardship spends the
    /// good side before strain builds (HeartMath.Spend, half a point a point: someone in good heart takes a knock in their
    /// stride); good things ease strain before they fill it (what a gathering, a talk or a small joy eases beyond the
    /// strain they have). It fills slowly, only while strain is at nothing, within daily caps, mostly from what can't be
    /// bought: friends close by and purpose (the heart's needs well met, by the hour), deeds the colony speaks well of and
    /// a life goal moving (once a day), gatherings, good talks and a run of small joys; the game's comforts (a fine room, a
    /// fine meal) only a little. It fades slowly.
    /// <para>
    /// Its bands are steady (the middle), in good heart and in high spirits: a small good mood (one situational thought
    /// reading the band, like strain's), brighter dreams, a quicker recovery from strain for someone in good heart lately,
    /// and in high spirits, now and then, a release (GoodRelease). No work speed. With its setting off nothing fills, the
    /// bands give nothing and hardship builds strain as before. Old saves start at nothing.
    /// </para>
    /// </summary>
    public static class GoodHeart
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The heart is on and so is its good side.</summary>
        public static bool On => Heart.On && (S?.heartGoodSide ?? true);

        public static GoodBand Band(PawnHeart h) => On && h != null ? HeartMath.GoodBandFor(h.good) : GoodBand.Steady;

        public static GoodBand BandOf(Pawn p) => Band(Heart.Of(p));

        // ---------------- Spending and filling ----------------

        /// <summary>
        /// Hardship from <paramref name="source"/> (after its caps): the good side covers it first, on the record by its
        /// source; what is left is returned, to build strain.
        /// </summary>
        public static float Spend(PawnHeart h, StrainSource source, float hardship)
        {
            if (!On || h == null || h.good <= 0f || hardship <= 0f) return hardship;
            h.EnsureLists();
            float rest = HeartMath.Spend(hardship, ref h.good, out float spent);
            if (spent > 0f && (int)source < h.goodSpent.Count) h.goodSpent[(int)source] += spent;
            return rest;
        }

        /// <summary>
        /// Fills the good side from <paramref name="source"/>, only while strain is at nothing, within the source's cap and
        /// the day's cap (on a day the hour hasn't reached yet, yesterday's counts don't hold it back). Returns what was added.
        /// </summary>
        public static float Fill(PawnHeart h, GoodSource source, float amount, int now)
        {
            if (!On || h == null || amount <= 0f || h.strain > 0f) return 0f;
            h.EnsureLists();
            int i = (int)source;
            bool today = h.day == now / GenDate.TicksPerDay;
            float sourceToday = today ? h.goodToday[i] : 0f, totalToday = today ? h.goodTodayTotal : 0f;
            float added = HeartMath.Fill(amount, ref h.good, ref sourceToday, HeartMath.GoodCap(source), ref totalToday, HeartMath.GoodDailyCap);
            if (added <= 0f) return 0f;
            if (today)
            {
                h.goodToday[i] = sourceToday;
                h.goodTodayTotal = totalToday;
            }
            h.goodRecent[i] += added;
            return added;
        }

        /// <summary>What a gathering, a talk or a joy eased beyond the strain they had: the good side takes its share of it.</summary>
        public static float Overflow(PawnHeart h, GoodSource source, float beyond, int now) => Fill(h, source, beyond * HeartMath.GoodShare(source), now);

        /// <summary>Off: the good side is gone at once (nothing builds, the bands give nothing).</summary>
        private static void Clear(PawnHeart h)
        {
            h.good = 0f;
            h.goodLately = 0f;
        }

        // ---------------- The hour and the day ----------------

        /// <summary>
        /// The hour (HeartLogic.Evaluate, after strain): belonging and purpose well met fill it, it fades, and how high it
        /// has stood lately is kept (for bouncing back after a hard spell).
        /// </summary>
        public static void Hour(Pawn p, PawnHeart h, float hours, int now)
        {
            if (!On)
            {
                if (h.good > 0f || h.goodLately > 0f) Clear(h);
                return;
            }
            if (h.needs != null && h.needs.Length == PawnHeart.NeedCount)
            {
                Fill(h, GoodSource.Belonging, HeartMath.NeedFillPerHour(h.needs[(int)HeartNeed.Belonging].value) * hours, now);
                Fill(h, GoodSource.Purpose, HeartMath.NeedFillPerHour(h.needs[(int)HeartNeed.Purpose].value) * hours, now);
            }
            h.good = HeartMath.Faded(h.good, hours);
            if (h.good < 0.05f) h.good = 0f;
            h.goodLately = HeartMath.Lately(h.goodLately, h.good, hours);
        }

        /// <summary>
        /// The day's turn (HeartLogic.Daily): a deed of theirs the colony speaks well of since the last look, and their life
        /// goal moving since yesterday. The first look after a load (or on an older save) only takes note.
        /// </summary>
        public static void Daily(Pawn p, PawnHeart h, int now)
        {
            if (!On) return;
            try
            {
                Deeds(p, h, now);
                Goal(p, h, now);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("the heart's good side, the day", e);
            }
        }

        private static readonly List<float> facetsTmp = new List<float>();

        /// <summary>A deed the colony speaks well of (Social/Gossip: its sides add up to good) that someone besides them knows of.</summary>
        private static void Deeds(Pawn p, PawnHeart h, int now)
        {
            int since = h.deedTick;
            h.deedTick = now;
            var tr = Reputations.On ? Reputations.Tracker : null;
            if (tr == null || since < 0) return;
            foreach (var d in tr.By(p.thingIDNumber))
            {
                if (d?.def == null || d.lastTick <= since || d.def.grave || d.def.facets == null) continue;
                if (RepMath.Valence(d.def.facets.ToArray()) <= 0) continue;
                bool known = false;
                foreach (var k in d.heard)
                    if (k != null && k.id != d.doer)
                    {
                        known = true;
                        break;
                    }
                if (known) Fill(h, GoodSource.Deeds, HeartMath.DeedFillFor(d.size), now);
            }
        }

        /// <summary>Their life goal came on since yesterday (the same goal, further along).</summary>
        private static void Goal(Pawn p, PawnHeart h, int now)
        {
            var comp = CouncilGameComponent.Instance;
            var goal = comp != null && S.aspirations ? comp.aspirations.Active(p) : null;
            if (goal == null)
            {
                h.goalTick = -1;
                return;
            }
            float progress;
            try
            {
                progress = goal.Progress;
            }
            catch
            {
                return;
            }
            if (goal.startedTick == h.goalTick && progress > h.goalProgress + 0.005f) Fill(h, GoodSource.Purpose, HeartMath.GoalMovedFill, now);
            h.goalTick = goal.startedTick;
            h.goalProgress = progress;
        }

        // ---------------- Words ----------------

        public static string BandLabel(GoodBand b) => ("CC_Good_Band_" + b).Translate();

        public static string BandDesc(GoodBand b) => ("CC_Good_Band_" + b + "_Desc").Translate();

        public static string SourceLabel(GoodSource s) => ("CC_Good_Source_" + s).Translate();

        /// <summary>A source as the heart's page says it: small joys with what they were ("small joys: a laugh with Ana").</summary>
        public static string SourceText(PawnHeart h, GoodSource s)
        {
            if (s == GoodSource.Joys && h != null && !h.joysWhy.NullOrEmpty()) return "CC_Heart_Relief_JoysWhat".Translate(h.joysWhy).Resolve();
            return SourceLabel(s);
        }

        /// <summary>What filled it lately, biggest first: "friends close by (+6), a purpose (+4)".</summary>
        public static List<(GoodSource source, float amount)> TopSources(PawnHeart h, int n)
        {
            var list = new List<(GoodSource, float)>();
            if (h?.goodRecent == null) return list;
            for (int i = 0; i < h.goodRecent.Count && i < HeartMath.GoodSourceCount; i++)
                if (h.goodRecent[i] >= 0.5f) list.Add(((GoodSource)i, h.goodRecent[i]));
            list.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            if (list.Count > n) list.RemoveRange(n, list.Count - n);
            return list;
        }

        /// <summary>The names alone ("friends close by and a purpose"), for the mood's line and the card.</summary>
        public static string BuiltText(PawnHeart h, int n)
        {
            var top = TopSources(h, n);
            return top.Count == 0 ? null : TextUtil.JoinAnd(top.Select(x => SourceText(h, x.source)).ToList());
        }

        public static string BuiltDetailed(PawnHeart h, int n)
        {
            var top = TopSources(h, n);
            return top.Count == 0 ? null : string.Join(", ", top.Select(x => SourceText(h, x.source) + " (+" + x.amount.ToString("0") + ")"));
        }

        /// <summary>What spent it lately (hardship by its source): "fighting (-6), losses (-4)".</summary>
        public static string SpentDetailed(PawnHeart h, int n)
        {
            if (h?.goodSpent == null) return null;
            var list = new List<(StrainSource s, float amount)>();
            for (int i = 0; i < h.goodSpent.Count && i < HeartMath.SourceCount; i++)
                if (h.goodSpent[i] >= 0.5f) list.Add(((StrainSource)i, h.goodSpent[i]));
            if (list.Count == 0) return null;
            list.Sort((a, b) => b.amount.CompareTo(a.amount));
            return string.Join(", ", list.Take(n).Select(x => HeartView.SourceText(h, x.s) + " (-" + x.amount.ToString("0") + ")"));
        }

        /// <summary>
        /// The good side in full, for its meter's tooltip: the band and what it does, what filled it, what spent it, the
        /// last release, and how it works.
        /// </summary>
        public static string Tip(Pawn p, PawnHeart h)
        {
            var band = Band(h);
            var sb = new System.Text.StringBuilder();
            sb.Append("CC_Good_TipHead".Translate(BandLabel(band).Named("BAND"), h.good.ToString("0").Named("VALUE")).Resolve());
            sb.Append("\n").Append(BandDesc(band));
            string built = BuiltDetailed(h, 4);
            sb.Append("\n\n").Append(built == null ? "CC_Good_NothingBuilt".Translate().ToString() : "CC_Good_BuiltBy".Translate(built.Named("WHAT")).Resolve());
            string spent = SpentDetailed(h, 3);
            if (spent != null) sb.Append("\n").Append("CC_Good_SpentBy".Translate(spent.Named("WHAT")).Resolve());
            if (h.strain > 0f) sb.Append("\n").Append("CC_Good_WaitsForStrain".Translate(p.Named("PAWN")).Resolve());
            if (!h.releaseWhy.NullOrEmpty() && Find.TickManager.TicksGame - h.lastReleaseTick < GenDate.TicksPerDay * 15)
                sb.Append("\n\n").Append("CC_Good_LastRelease".Translate(h.releaseWhy.Named("WHAT"), Ago(h.lastReleaseTick).Named("WHEN")).Resolve());
            sb.Append("\n\n").Append("CC_Good_Explain".Translate().ToString().Colorize(ColoredText.SubtleGrayColor));
            return sb.ToString();
        }

        /// <summary>"just now", "2 days ago".</summary>
        public static string Ago(int tick)
        {
            int ago = Find.TickManager.TicksGame - tick;
            return ago < GenDate.TicksPerHour ? "CC_Good_JustNow".Translate().ToString() : "CC_Good_Ago".Translate(TextUtil.DaysString(ago)).Resolve();
        }

        /// <summary>One line for the colonist's card and the Mood tab: "In good heart: friends close by and a purpose." Null while steady.</summary>
        public static string Line(Pawn p, PawnHeart h)
        {
            var band = Band(h);
            if (band == GoodBand.Steady) return null;
            string built = BuiltText(h, 2);
            return built == null
                ? "CC_Good_Line".Translate(BandLabel(band).CapitalizeFirst().Named("BAND"), p.Named("PAWN")).Resolve()
                : "CC_Good_LineWhy".Translate(BandLabel(band).CapitalizeFirst().Named("BAND"), built.Named("WHAT"), p.Named("PAWN")).Resolve();
        }
    }

    /// <summary>
    /// The good side as a mood: a situational thought that reads the colonist's band each time the game asks, so nothing
    /// is stored in their memories. In good heart and in high spirits; nothing while steady, with the good side or the
    /// heart off, or on "Advisor only" stakes.
    /// </summary>
    public class ThoughtWorker_HeartGood : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!ColonyCouncilMod.Settings.MoodEffects || !GoodHeart.On) return ThoughtState.Inactive;
            var h = Heart.Of(p);
            if (h == null || !Heart.Eligible(p)) return ThoughtState.Inactive;
            switch (HeartMath.GoodBandFor(h.good))
            {
                case GoodBand.GoodHeart: return ThoughtState.ActiveAtStage(0);
                case GoodBand.HighSpirits: return ThoughtState.ActiveAtStage(1);
                default: return ThoughtState.Inactive;
            }
        }
    }

    /// <summary>The good side's mood, saying what filled it lately.</summary>
    public class Thought_HeartGood : Thought_Situational, IExplainedThought
    {
        public string Why
        {
            get
            {
                var h = Heart.Of(pawn);
                if (h == null) return null;
                string built = GoodHeart.BuiltText(h, 3);
                return built.NullOrEmpty() ? null : "CC_Good_ThoughtWhy".Translate(built.Named("WHAT")).Resolve();
            }
        }

        public override string Description => Causes.Append(base.Description, Why);
    }
}
