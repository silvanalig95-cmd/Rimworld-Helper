using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The year, told once: one letter at each anniversary of the landing (with the annals' setting), the colony's
    /// founding day, written by the colony's annalist or whoever reads and talks best. Its people first (the annals'
    /// figures, counted since the last landing day: <see cref="AnnalsTracker.PeoplePart"/>), then the land and the world (the
    /// herd, the beast that stays, the neighbours settled for good and their road, the camps that came, the regulars who
    /// came back, the world events that ended and how, the allies, a few lines of the year's chronicle about strangers
    /// and the world), then the founding (who has been here since the first days, the dead, the colony's name on the
    /// roads, its customs), and the author's last word. The founding day's event or the calendar's letter is the day's
    /// question, how to keep it, and no longer tells the year; the chronicle has one line for it. Kept for the Chronicle's
    /// header, by the colony's year.
    /// </summary>
    public static class YearLetter
    {
        /// <summary>The first three days of a new year since the landing, as the founding day counts them; once a year.</summary>
        public static void Daily(CouncilGameComponent comp)
        {
            if (comp == null || ColonyCouncilMod.Settings == null || !ColonyCouncilMod.Settings.annals) return;
            int days = GenDate.DaysPassed;
            if (!YearLetterMath.IsYearDay(days)) return;
            int year = YearLetterMath.YearOf(days);
            if (year <= comp.yearLetterLast) return;
            var map = MapFacts.BusiestHome;
            if (map == null) return;
            comp.yearLetterLast = year;
            try
            {
                Write(comp, map, year);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("year's letter", e);
            }
        }

        private static void Write(CouncilGameComponent comp, Map map, int year)
        {
            int now = Find.TickManager.TicksGame;
            int sinceTick = YearLetterMath.LandingTick(year);
            var annals = comp.annals;
            var author = annals.Author(comp, out bool keeps);
            var sb = new StringBuilder();
            void Para(string s)
            {
                if (s.NullOrEmpty()) return;
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append(s);
            }
            void Part(string key) => Para(key.Translate().Resolve().Colorize(ColoredText.TipSectionTitleColor));
            string ord = HerdStory.Ordinal(year);
            if (author == null) Para("CC_YearOne_OpeningNobody".Translate(ord.Named("ORD")).Resolve());
            else if (annals.Partial(sinceTick))
                Para("CC_YearOne_OpeningPartial".Translate(ord.Named("ORD"), author.Named("PAWN"), Since(annals.Since, map).Named("SINCE")).Resolve());
            else Para((keeps ? "CC_YearOne_OpeningAnnalist" : "CC_YearOne_Opening").Translate(ord.Named("ORD"), author.Named("PAWN")).Resolve());

            // The people: the annals' figures for the year.
            Part("CC_YearOne_PartPeople");
            annals.PeoplePart(comp, map, Para);

            // The land and the world.
            Part("CC_YearOne_PartLand");
            // Each colony's own year (its herd, its beast, its neighbours settled for good): with one colony as it always
            // was, with more than one a part for each, under its name.
            if (!ColonyState.Several) Local(comp, map, sinceTick, now, Para);
            else
                foreach (var home in Homes.All)
                {
                    Para("CC_Year_AtHome".Translate(Homes.Name(home).Named("PLACE")).Resolve());
                    Local(comp, home, sinceTick, now, Para);
                }

            // The camps that came, the faces that came back.
            var tracker = CampTracker.Instance;
            var holding = CampTracker.HoldingAt(map);
            var camps = tracker?.past?.Where(c => c?.kind != null && c.leftTick >= sinceTick).ToList() ?? new List<PastCamp>();
            if (camps.Count > 0)
            {
                var kinds = camps.GroupBy(c => c.kind.label).Select(g => g.Count() > 1 ? "CC_Year_Times".Translate(g.Key, g.Count()).Resolve() : g.Key).ToList();
                Para("CC_Year_Camps".Translate(TextUtil.JoinAnd(kinds).Named("LIST")).Resolve());
            }
            var regulars = Regulars.All.Where(r => r.Alive && r.lastTick >= sinceTick).OrderByDescending(r => r.visits).Take(6).ToList();
            if (regulars.Count > 0)
            {
                var bits = regulars.Select(r => "CC_Regular_One".Translate(r.pawn.Named("X"), ("CC_Regular_Kind_" + r.kind).Translate().Named("KIND"), r.visits.Named("COUNT")).Resolve()).ToList();
                Para("CC_Year_Regulars".Translate(TextUtil.JoinAnd(bits).Named("LIST")).Resolve());
            }

            // The world: the events that ended, and the allies.
            var ended = WorldSituations.Instance?.history?.Where(r => r != null && r.endTick >= sinceTick).ToList() ?? new List<SituationRecord>();
            if (ended.Count > 0)
            {
                var bits = ended.Take(6).Select(r => WorldOne(r)).ToList();
                Para("CC_Year_World".Translate(TextUtil.JoinAnd(bits).Named("LIST")).Resolve());
            }
            var ally = Allies.Of();
            if (ally != null) Para("CC_Year_Allies".Translate(ally.Name.Named("FACTION")).Resolve());

            // A few lines of the year's chronicle about strangers and the world, spread over the year (oldest first; not
            // the council's answers, nor a season's summary).
            var lines = Chronicle.Since(sinceTick).Where(e => !e.Answer && !e.Summary && (e.kind == ChronicleKind.Stranger || e.kind == ChronicleKind.World)).Reverse().ToList();
            if (lines.Count > 0)
            {
                var picked = lines.Count <= 5 ? lines : Spread(lines, 5);
                Para("CC_Year_ChronicleSome".Translate().Resolve() + "\n" + string.Join("\n", picked.Select(e => "CC_DayN".Translate(e.tick / GenDate.TicksPerDay + 1).ToString() + ": " + e.text)));
            }

            // The founding: who has been here since the first days, the dead, the colony's name, its customs.
            string founding = HappeningWorker_Founding.YearFacts(map);
            if (!founding.NullOrEmpty())
            {
                Part("CC_YearOne_PartFounding");
                Para(founding);
            }
            Para(annals.LastWord(author));

            string text = sb.ToString();
            var look = author != null ? new LookTargets(author) : holding != null ? holding.Look : LookTargets.Invalid;
            // With more than one colony the letter is about them all (each named in its part).
            Tell.Letter(NewsKind.Colony, Importance.Notable, ColonyState.Several ? null : map, "CC_Year_Title".Translate(ord.Named("ORD")).Resolve(), text, LetterDefOf.NeutralEvent, look);
            annals.Keep(map, year, author, text);
            // The chronicle has the year once.
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Note, author != null
                ? "CC_YearOne_Chronicle".Translate(ord.Named("ORD"), author.Named("PAWN")).Resolve()
                : "CC_Chronicle_YearLetter".Translate(ord.Named("ORD")).Resolve(), author));
        }

        /// <summary>The day the year's counting began, as a date ("the 5th of Aprimay"), for a year counted from part of the way through.</summary>
        private static string Since(int tick, Map map)
        {
            var at = map != null ? Find.WorldGrid.LongLatOf(map.Tile) : Vector2.zero;
            return GenDate.DateFullStringAt(GenDate.TickGameToAbs(tick), at);
        }

        /// <summary>One colony's own year: its herd, the beast on its land, its neighbours settled for good and their road.</summary>
        private static void Local(CouncilGameComponent comp, Map map, int sinceTick, int now, Action<string> Para)
        {
            // The herd.
            var herd = AnimalFacts.Herd(map);
            if (herd.Count > 0)
            {
                var hs = comp.HerdOf(map);
                var leader = hs?.leader;
                bool leads = leader != null && !leader.Dead && AnimalFacts.Ours(leader);
                int born = herd.Count(a => a.ageTracker != null && a.ageTracker.AgeBiologicalTicks < GenDate.TicksPerYear);
                string lead = leads
                    ? hs.generation > 1 && hs.lineName != null
                        ? "CC_Year_HerdLeaderLine".Translate(leader.Named("PET"), HerdStory.Ordinal(hs.generation).Named("ORD"), hs.lineName.Named("LINE")).Resolve()
                        : "CC_Year_HerdLeader".Translate(leader.Named("PET")).Resolve()
                    : "CC_Year_HerdNoLeader".Translate().Resolve();
                Para((born == 0 ? "CC_Year_HerdNoneBorn" : "CC_Year_Herd").Translate(herd.Count.Named("COUNT"), born.Named("BORN"), lead.Named("LEAD")).Resolve());
                if (leads && hs.leaderSince >= sinceTick && hs.leaders > 1) Para("CC_Year_HerdNewLeader".Translate(leader.Named("PET")).Resolve());
                if (leads && hs.lastSeasonTick >= 0) Para("CC_Year_HerdOld".Translate(leader.Named("PET")).Resolve());
            }

            // The beast that stays.
            var rb = comp.BeastOf(map);
            if (rb != null && rb.Here)
            {
                string stanceKey = "CC_Beast_Stance_" + rb.stance;
                string stance = (stanceKey.CanTranslate() ? stanceKey : "CC_Beast_Stance_watched").Translate();
                Para((rb.namedTick >= sinceTick ? "CC_Year_BeastNew" : "CC_Year_BeastStill").Translate(rb.animal.Named("PET"), AnimalFacts.KindLabel(rb.animal).Named("KIND"), stance.Named("STANCE")).Resolve());
                if (rb.stolen > 0) Para(Texts.CountKey("CC_Year_BeastStole", rb.stolen).Translate(rb.animal.Named("PET"), rb.stolen.Named("N")).Resolve());
            }

            // The neighbours settled for good, and the road.
            var holding = CampTracker.HoldingAt(map);
            if (holding != null && holding.Here.Count > 0)
            {
                Para((holding.settledTick >= sinceTick ? "CC_Year_HoldingNew" : "CC_Year_Holding").Translate(holding.HoldingName.Named("NAME"), (holding.faction?.Name ?? "").Named("FACTION"),
                    holding.Here.Count.Named("COUNT"), TextUtil.DaysString(Mathf.Max(GenDate.TicksPerHour, now - Mathf.Max(0, holding.settledTick))).Named("DAYS")).Resolve());
                if (holding.roadLoads > 0) Para(Texts.CountKey("CC_Year_Road", holding.roadLoads).Translate(holding.roadLoads.Named("N")).Resolve());
            }
        }

        /// <summary>So many of the lines, evenly over the year: the first, the last, and some between.</summary>
        private static List<ChronicleLine> Spread(List<ChronicleLine> all, int n)
        {
            var picked = new List<ChronicleLine>();
            for (int i = 0; i < n; i++) picked.Add(all[i * (all.Count - 1) / (n - 1)]);
            return picked;
        }

        /// <summary>
        /// A world event that ended this year: "ended in" its ending's own word where it has one (CC_WS_Ending_KEY), else
        /// that it is over. The ending's key itself is never a word ("A silver rush ended in over", the player's save).
        /// </summary>
        private static string WorldOne(SituationRecord r)
        {
            string k = r.ending.NullOrEmpty() ? null : "CC_WS_Ending_" + r.ending;
            if (k != null && k.CanTranslate()) return "CC_Year_WorldOne".Translate(r.Called.Named("EVENT"), k.Translate().Named("ENDING")).Resolve();
            return "CC_Year_WorldOver".Translate(r.Called.Named("EVENT")).Resolve();
        }
    }
}
