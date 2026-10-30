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
    /// A year's letter at each anniversary of the landing (with the annals' setting): what the year brought on the land
    /// and from beyond it since the last landing day. The herd (its head, its leader and line, the young born, a new
    /// leader, one in its last season), the beast that stays, the neighbours settled for good and the loads that went
    /// down the road, the camps that came, the regulars who came back, the world events that ended and how, the
    /// allies, and a few lines of the year's chronicle about strangers and the world. Nothing the annals say (people,
    /// raids, wealth, the council): those write the calendar year, and the founding day's own event marks the day.
    /// Written by the colony's annalist, or whoever reads and talks best.
    /// </summary>
    public static class YearLetter
    {
        /// <summary>The first three days of a new year since the landing, as the founding day counts them; once a year.</summary>
        public static void Daily(CouncilGameComponent comp)
        {
            if (comp == null || ColonyCouncilMod.Settings == null || !ColonyCouncilMod.Settings.annals) return;
            int days = GenDate.DaysPassed;
            if (days < GenDate.DaysPerYear || days % GenDate.DaysPerYear > 2) return;
            int year = days / GenDate.DaysPerYear;
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
            int sinceTick = (year - 1) * GenDate.TicksPerYear;
            var colonists = map.mapPawns.FreeColonistsSpawned.Where(p => !p.DevelopmentalStage.Juvenile()).ToList();
            // The colony's annalist keeps the pen, as for the annals; else whoever reads and talks best.
            var annalist = ColonyCouncilMod.Settings.epithets ? comp.epithets.HolderOf("Council_Epithet_Annalist") : null;
            var author = annalist != null && colonists.Contains(annalist) ? annalist
                : colonists.OrderByDescending(p => (p.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0) + (p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0)).FirstOrDefault();
            var sb = new StringBuilder();
            void Para(string s)
            {
                if (s.NullOrEmpty()) return;
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append(s);
            }
            string ord = HerdStory.Ordinal(year);
            Para(author != null ? "CC_Year_Opening".Translate(ord.Named("ORD"), author.Named("PAWN")).Resolve() : "CC_Year_OpeningNobody".Translate(ord.Named("ORD")).Resolve());

            // The herd.
            var herd = AnimalFacts.Herd(map);
            if (herd.Count > 0)
            {
                var hs = comp.herd;
                var leader = hs?.leader;
                bool leads = leader != null && !leader.Dead && AnimalFacts.Ours(leader);
                int born = herd.Count(a => a.ageTracker != null && a.ageTracker.AgeBiologicalTicks < GenDate.TicksPerYear);
                string lead = leads
                    ? hs.generation > 1 && hs.lineName != null
                        ? "CC_Year_HerdLeaderLine".Translate(leader.Named("PET"), HerdStory.Ordinal(hs.generation).Named("ORD"), hs.lineName.Named("LINE")).Resolve()
                        : "CC_Year_HerdLeader".Translate(leader.Named("PET")).Resolve()
                    : "CC_Year_HerdNoLeader".Translate().Resolve();
                Para("CC_Year_Herd".Translate(herd.Count.Named("COUNT"), born.Named("BORN"), lead.Named("LEAD")).Resolve());
                if (leads && hs.leaderSince >= sinceTick && hs.leaders > 1) Para("CC_Year_HerdNewLeader".Translate(leader.Named("PET")).Resolve());
                if (leads && hs.lastSeasonTick >= 0) Para("CC_Year_HerdOld".Translate(leader.Named("PET")).Resolve());
            }

            // The beast that stays.
            var rb = comp.beast;
            if (rb != null && rb.Here)
            {
                string stanceKey = "CC_Beast_Stance_" + rb.stance;
                string stance = (stanceKey.CanTranslate() ? stanceKey : "CC_Beast_Stance_watched").Translate();
                Para((rb.namedTick >= sinceTick ? "CC_Year_BeastNew" : "CC_Year_BeastStill").Translate(rb.animal.Named("PET"), AnimalFacts.KindLabel(rb.animal).Named("KIND"), stance.Named("STANCE")).Resolve());
                if (rb.stolen > 0) Para("CC_Year_BeastStole".Translate(rb.animal.Named("PET"), rb.stolen.Named("N")).Resolve());
            }

            // The neighbours: settled for good, the road, the camps that came, the faces that came back.
            var tracker = CampTracker.Instance;
            var holding = tracker?.holding;
            if (holding != null && holding.Here.Count > 0)
            {
                Para((holding.settledTick >= sinceTick ? "CC_Year_HoldingNew" : "CC_Year_Holding").Translate(holding.HoldingName.Named("NAME"), (holding.faction?.Name ?? "").Named("FACTION"),
                    holding.Here.Count.Named("COUNT"), TextUtil.DaysString(Mathf.Max(GenDate.TicksPerHour, now - Mathf.Max(0, holding.settledTick))).Named("DAYS")).Resolve());
                if (holding.roadLoads > 0) Para("CC_Year_Road".Translate(holding.roadLoads.Named("N")).Resolve());
            }
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
                var bits = ended.Take(6).Select(r => "CC_Year_WorldOne".Translate(r.Called.Named("EVENT"), EndingWord(r.ending).Named("ENDING")).Resolve()).ToList();
                Para("CC_Year_World".Translate(TextUtil.JoinAnd(bits).Named("LIST")).Resolve());
            }
            var ally = Allies.Of();
            if (ally != null) Para("CC_Year_Allies".Translate(ally.Name.Named("FACTION")).Resolve());

            // A few lines of the year's chronicle about strangers and the world, spread over the year.
            var lines = comp.chronicle.Where(e => e != null && e.tick >= sinceTick && (e.kind == ChronicleKind.Stranger || e.kind == ChronicleKind.World)).ToList();
            if (lines.Count > 0)
            {
                var picked = lines.Count <= 5 ? lines : Spread(lines, 5);
                Para("CC_Year_Chronicle".Translate(lines.Count.Named("N")).Resolve() + "\n" + string.Join("\n", picked.Select(e => "CC_DayN".Translate(e.tick / GenDate.TicksPerDay + 1).ToString() + ": " + e.text)));
            }
            Para("CC_Year_Close".Translate().Resolve());

            var look = author != null ? new LookTargets(author) : holding != null ? holding.Look : LookTargets.Invalid;
            Find.LetterStack.ReceiveLetter("CC_Year_Title".Translate(ord.Named("ORD")).Resolve(), sb.ToString(), LetterDefOf.NeutralEvent, look);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Chronicle_YearLetter".Translate(ord.Named("ORD")).Resolve(), author));
        }

        /// <summary>So many of the lines, evenly over the year: the first, the last, and some between.</summary>
        private static List<ChronicleEntry> Spread(List<ChronicleEntry> all, int n)
        {
            var picked = new List<ChronicleEntry>();
            for (int i = 0; i < n; i++) picked.Add(all[i * (all.Count - 1) / (n - 1)]);
            return picked;
        }

        /// <summary>A world event's ending as a word: its own text where there is one, else the ending's key.</summary>
        private static string EndingWord(string key)
        {
            if (key.NullOrEmpty()) return "CC_Year_EndingUnknown".Translate();
            string k = "CC_WS_Ending_" + key;
            return k.CanTranslate() ? k.Translate().ToString() : key;
        }
    }
}
