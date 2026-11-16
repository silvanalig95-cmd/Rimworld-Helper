using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Each god's temper toward the colony as the keeper of its book reads it (the player, 16 November 2026: "the god's
    /// favour should be tracked in the book, but the wording should also be kept a bit ominous, so not with a numeric value
    /// or so"). Every few days (GodTemperMath.ReadNow) the keeper reads each god the colony gives to, has a place for or
    /// holds to: one step from their last reading toward the god's standing at the colony's seat, or a step beside it when
    /// they misread it (GodTemperMath.Read), as the pawns would read it from what they saw; a guess when nothing of the god
    /// was seen lately, said as "some say". The book shows the words only (<see cref="Line"/>: the god's own words for each
    /// band, GodWorshipDef.temper, "since the steel was given" after a gift that came before it warmed), in the god's
    /// chapter, never a number and nowhere else. Nobody keeps the book: nobody reads, and the last reading stands.
    /// </summary>
    public static class GodTemper
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The region's day: the keeper reads the gods whose turn it is.</summary>
        public static void Daily(int now)
        {
            var lore = GodLore.Instance;
            if (lore == null || !Gods.On) return;
            var keeper = GodLore.Keeper(out _);
            var seat = Homes.Seat;
            if (keeper == null || seat == null) return;
            foreach (var g in Gods.All.ToList())
            {
                if (g == null || lore.PageOf(g) == null || !Kept(lore, g)) continue;
                var t = lore.TemperOf(g);
                float since = t == null || t.tick < 0 ? 9999f : (now - t.tick) / (float)GenDate.TicksPerDay;
                if (!GodTemperMath.ReadNow(since, Rand.ValueSeeded(g.id * 7919 ^ (t?.tick ?? 0)))) continue;
                Read(lore, g, keeper, seat, now);
            }
        }

        /// <summary>A god the colony has to do with: gifts given it, a place of its, or someone here who holds to it.</summary>
        private static bool Kept(GodLore lore, God g)
        {
            if (lore.gifts.Any(x => x != null && x.god == g.id)) return true;
            if (Homes.All.Any(m => m != null && Shrines.Of(m, g).Count > 0)) return true;
            return PawnGods.HoldersOf(g).Count > 0;
        }

        /// <summary>Something of the god seen lately: a gift of its taken or found gone, or a doing written under its name.</summary>
        private static bool Seen(GodLore lore, God g, int now)
        {
            int since = now - (int)(GodTemperMath.SignDays * GenDate.TicksPerDay);
            if (lore.gifts.Any(x => x != null && x.god == g.id && x.tick >= since && GodLore.TakenFate(x.fate))) return true;
            var page = lore.PageOf(g)?.name;
            return page != null && lore.entries.Any(e => e != null && e.tick >= since && e.page != null && (lore.Root(lore.Named(e.page))?.name ?? e.page) == page);
        }

        /// <summary>The keeper reads one god now: a step toward how it stands with the colony's seat, or beside it.</summary>
        internal static void Read(GodLore lore, God g, Pawn keeper, Map seat, int now)
        {
            try
            {
                var t = lore.TemperFor(g);
                int truth = (int)Gods.Favour(g, seat);
                int before = t.step < 0 ? GodTemperMath.Unknown : t.step;
                var (step, unsure) = GodTemperMath.Read(t.step, truth, Seen(lore, g, now), Rand.Value, Rand.Value);
                string since = null;
                if (GodTemperMath.SaysSince(GodTemperMath.Band(step), before, step))
                {
                    int from = now - (int)(GodTemperMath.SinceDays * GenDate.TicksPerDay);
                    var last = lore.gifts.LastOrDefault(x => x != null && x.god == g.id && x.tick >= from && GodLore.TakenFate(x.fate) && !x.plain.NullOrEmpty());
                    since = last?.plain ?? (step == before ? t.since : null);
                }
                t.step = step;
                t.unsure = unsure;
                t.since = since;
                t.tick = now;
                t.keeper = keeper?.LabelShort;
                GodLore.Changed();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the keeper's reading", e);
            }
        }

        /// <summary>
        /// The keeper's reading in words, for the chapter the colony keeps the god under (<paramref name="name"/>): the god's own
        /// line for the band (its worship def's, else the shared ones), "since the steel was given" where a gift came before it
        /// warmed, said as some say when it was a guess. Null when the keeper never read it.
        /// </summary>
        public static string Line(God g, string name)
        {
            var t = GodLore.Instance?.TemperOf(g);
            if (t == null || t.step < 0 || name.NullOrEmpty()) return null;
            try
            {
                int band = GodTemperMath.Band(t.step);
                var own = GodWorshipDef.For(g)?.temper;
                string read = own != null && own.Count == GodTemperMath.Bands && !own[band].NullOrEmpty() ? own[band] : ("CC_GodTemper_Band_" + band).Translate().ToString();
                read = read.Formatted(name.Named("NAME")).Resolve();
                if (!t.since.NullOrEmpty() && band >= 3)
                    read += " " + "CC_GodTemper_Since".Translate(Find.ActiveLanguageWorker.WithDefiniteArticle(t.since).Named("WHAT")).Resolve();
                return (t.unsure ? "CC_GodTemper_SomeSay" : "CC_GodTemper_Said").Translate(read.Named("READ")).Resolve().CapitalizeFirst();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the keeper's reading in words", e);
                return null;
            }
        }

        /// <summary>Whose reading it is and when, for the line over it ("As Avila read it, 3rd of Aprimay, 5502:").</summary>
        public static string Head(God g)
        {
            var t = GodLore.Instance?.TemperOf(g);
            if (t == null || t.step < 0) return null;
            string when = GodLorePage.Date(t.tick);
            return t.keeper.NullOrEmpty()
                ? "CC_GodTemper_HeadNobody".Translate(when.Named("DATE")).Resolve()
                : "CC_GodTemper_Head".Translate(t.keeper.Named("KEEPER"), when.Named("DATE")).Resolve();
        }

        // ================= Dev mode =================

        /// <summary>Dev mode: the keeper reads every god the colony has to do with now, and the log says what they read (the true word beside it).</summary>
        internal static string DevReadAll()
        {
            var lore = GodLore.Instance;
            var keeper = GodLore.Keeper(out _);
            var seat = Homes.Seat;
            if (lore == null || seat == null) return "No book or no seat.";
            var sb = new System.Text.StringBuilder();
            foreach (var g in Gods.All.ToList())
            {
                if (g == null || lore.PageOf(g) == null || !Kept(lore, g)) continue;
                Read(lore, g, keeper, seat, Now);
                var t = lore.TemperOf(g);
                sb.AppendLine("  " + g.Key + ": read " + t?.step + " (true " + (int)Gods.Favour(g, seat) + ")" + (t?.unsure == true ? ", a guess" : "") + ": " + Line(g, lore.PageOf(g)?.name));
            }
            return sb.Length == 0 ? "No god the colony has to do with." : sb.ToString();
        }
    }
}
