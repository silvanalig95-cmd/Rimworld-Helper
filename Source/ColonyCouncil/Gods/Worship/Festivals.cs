using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods' festivals on the colony calendar (docs/design/gods.md, "Worship": festivals; brainstorm 14), a part of the
    /// calendar like the faith's days: once a day the colony's gods (the one the council honours this year, its patron, then
    /// the most widely held, WorshipMath.MaxFestivals at most) each have a day of their own (CalendarKind.GodFestival), on the
    /// date their domain keeps (WorshipMath.FestivalWhen: the harvest's thanksgiving late in the fall, the longest night at
    /// midwinter, the dead's day as the fall turns, in the season as it falls in this land), kept by default with a feast. A
    /// god no longer the colony's has its day taken off quietly, unless it has become a tradition.
    /// </summary>
    public partial class ColonyCalendar
    {
        public const string GodDayPrefix = "god|";

        private void WatchGodFestivals(Map map, int today, int year)
        {
            if (!Worship.On || map == null) return;
            var gods = Festivals.GodsFor(map);
            var live = new HashSet<string>();
            float latitude = 0f;
            try
            {
                latitude = Find.WorldGrid.LongLatOf(map.Tile).y;
            }
            catch
            {
            }
            foreach (var g in gods)
            {
                string id = GodDayPrefix + g.id;
                live.Add(id);
                var have = Get(id);
                string label = Festivals.Name(g);
                if (have != null)
                {
                    have.subject = Worship.Called(g);
                    have.otherName = g.epithet;
                    if (!have.byPlayer) have.label = label;
                    continue;
                }
                var (season, inSeason) = Festivals.When(g);
                int q = Festivals.QuadrumOf(season, latitude);
                int clashes = days.Count(x => x.kind == CalendarKind.GodFestival && x.day == WorshipMath.FestivalDay(q, inSeason, 0));
                int day = WorshipMath.FestivalDay(q, inSeason, clashes);
                var d = new CalendarDay
                {
                    id = id, kind = CalendarKind.GodFestival, way = DayWay.Feast, label = label, subject = Worship.Called(g), otherName = g.epithet,
                    about = g.domain.ToString().ToLowerInvariant(), day = day, sinceYear = year, tick = Find.TickManager.TicksGame, weight = CalendarMath.FaithDayWeight,
                    lastYear = CalendarMath.InitialLastYear(today, year, day, false)
                };
                TryAdd(d, true);
            }
            foreach (var d in days.Where(x => x.kind == CalendarKind.GodFestival && !x.tradition && !x.byPlayer && !live.Contains(x.id)).ToList()) days.Remove(d);
        }

        /// <summary>Dev: the gods' festivals looked at now, as the calendar's day does.</summary>
        public void DevGodFestivals(Map map)
        {
            int now = Find.TickManager.TicksGame;
            WatchGodFestivals(map, CalendarMath.Wrap(DayOf(now, map)), YearOf(now, map));
        }

        /// <summary>The god a festival day is for, or null.</summary>
        public static God GodOfDay(CalendarDay d)
        {
            if (d?.id == null || d.kind != CalendarKind.GodFestival || !d.id.StartsWith(GodDayPrefix)) return null;
            return int.TryParse(d.id.Substring(GodDayPrefix.Length), out int id) ? Gods.ById(id) : null;
        }
    }

    /// <summary>
    /// A festival kept and let pass (from the calendar's keeping, Calendar/CalendarKeeping.cs). Kept, the colony's feast is
    /// held as any feast day is (the one feast engine, with the day's words of the god's own dishes), and besides it the god's
    /// rite: the priest (else the keeper of the day, else one who holds the god) walks to the god's place and keeps the rite
    /// there, with Alpha Memes' or Ideology's chant for the god's kind where those are loaded and a RimWorld of Magic priest's
    /// light; an offering of the god's liking is carried to its place; and a people that holds the god may send a few of its
    /// own for the day (the game's visitors). The god is pleased (a rite, more for the god honoured this year); let pass, it
    /// is displeased (neglect).
    /// </summary>
    public static class Festivals
    {
        /// <summary>The gods whose festivals the colony keeps: the one honoured this year, then those its people pray to, the most first (never a dead god, nor one that keeps none).</summary>
        public static List<God> GodsFor(Map map)
        {
            var list = new List<God>();
            void Add(God g)
            {
                if (GodWorshipDef.KeepsFestival(g) && !list.Contains(g) && list.Count < WorshipMath.MaxFestivals) list.Add(g);
            }
            Add(Worship.Honoured(map));
            foreach (var g in Worship.PrayedTo(map)) Add(g);
            return list;
        }

        /// <summary>The festival's name: "the thanksgiving of Ceren", "the longest night of the Moon-Hare".</summary>
        public static string Name(God g)
        {
            var own = GodWorshipDef.For(g)?.festivalName;
            return (own.NullOrEmpty() ? ("CC_Wsh_Festival_" + g.domain).Translate() : (TaggedString)own).Formatted(Gods.Args(g)).Resolve();
        }

        /// <summary>The dishes and rite a god's festival is kept with, in a phrase: its own, else its domain's ("bread broken and the first sheaf burned").</summary>
        public static string Dishes(God g)
        {
            var own = GodWorshipDef.For(g)?.festivalDishes;
            return own.NullOrEmpty() ? ("CC_Wsh_Dishes_" + (g?.domain ?? GodDomain.None)).Translate().Resolve() : own;
        }

        /// <summary>When a god's festival comes: its own day where its worship def gives one, else its domain's (WorshipMath.FestivalWhen).</summary>
        public static (int season, int day) When(God g)
        {
            var own = GodWorshipDef.For(g);
            var dom = WorshipMath.FestivalWhen(g.domain);
            return (own != null && own.festivalSeason >= 0 ? own.festivalSeason : dom.season, own != null && own.festivalDay >= 0 ? own.festivalDay : dom.day);
        }

        /// <summary>The quadrum (0 to 3) where a season (0 spring to 3 winter) falls at this latitude; the northern order where it never comes.</summary>
        public static int QuadrumOf(int season, float latitude)
        {
            var want = season switch { 0 => Season.Spring, 1 => Season.Summer, 2 => Season.Fall, _ => Season.Winter };
            for (int q = 0; q < 4; q++)
                if (((Quadrum)q).GetSeason(latitude) == want) return q;
            return Mathf.Clamp(season, 0, 3);
        }

        // ================= Kept, and let pass =================

        public static void Kept(CalendarDay d, Map map, List<Pawn> home)
        {
            var g = ColonyCalendar.GodOfDay(d);
            if (g == null || map == null || !Worship.On) return;
            string why = "CC_Wsh_Why_Festival".Translate(d.label.Named("NAME")).Resolve();
            Worship.Favour(g, map, WorshipMath.FestivalKept, why, FavourSource.Rite);
            string kept = "CC_Wsh_Book_Festival".Translate(d.label.Named("NAME"), Dishes(g).Named("DISHES")).Resolve();
            Worship.Witness(g, map, GodActKind.Favour, kept);
            Worship.Booked(g, map, IntVec3.Invalid, kept);
            var b = Worship.Book;
            var shrine = Shrines.Of(map, g).Select(s => s.thing).OrderByDescending(Shrines.IsTemple).FirstOrDefault();
            if (shrine != null && b != null)
            {
                // The rite at its place, and an offering carried there.
                b.SetMark(Gods.HomeKey(map), "rite", g.id, Worship.Now, Worship.Now + GenDate.TicksPerDay / 2);
                if (GodWorshipDef.TakesOfferings(g) && !b.offerings.Any(o => o != null && o.shrine == shrine && o.fate == OfferingFate.Waiting))
                    b.offerings.Add(new OfferingRecord { shrine = shrine, god = g.id, home = Gods.HomeKey(map), chosen = Worship.Now });
            }
            Visitors(g, map);
        }

        public static void Passed(CalendarDay d, Map map)
        {
            var g = ColonyCalendar.GodOfDay(d);
            if (g == null || map == null || !Worship.On) return;
            Worship.Favour(g, map, WorshipMath.FestivalPassed, "CC_Wsh_Why_FestivalPassed".Translate(d.label.Named("NAME")).Resolve(), FavourSource.Neglect, GodOffence.Neglect);
            Worship.Witness(g, map, GodActKind.Favour, "CC_Wsh_Book_FestivalPassed".Translate(d.label.Named("NAME")).Resolve());
        }

        /// <summary>The region's hour (WorshipCouncil.Hourly): a festival's rite waiting is held by whoever leads it, once free.</summary>
        public static void Hourly(WorshipBook b, Map map, int now)
        {
            var m = b.Mark(Gods.HomeKey(map), "rite");
            if (m == null) return;
            if (now > m.until)
            {
                b.homeMarks.Remove(m);
                return;
            }
            var g = Gods.ById(m.god);
            var shrine = g == null ? null : Shrines.Of(map, g).Select(s => s.thing).OrderByDescending(Shrines.IsTemple).FirstOrDefault();
            if (shrine == null)
            {
                b.homeMarks.Remove(m);
                return;
            }
            if (map.mapPawns.FreeColonistsSpawned.Any(p => p.CurJobDef?.defName == WorshipJobs.RiteJob)) return;
            var lead = new List<Pawn>();
            var priest = Worship.Priest(map);
            if (priest != null) lead.Add(priest);
            lead.AddRange(map.mapPawns.FreeColonistsSpawned.Where(p => Worship.GodOf(p) == g).OrderByDescending(Worship.Devout));
            foreach (var p in lead.Distinct())
            {
                if (p.Map != map || !Worship.Ours(p) || p.DevelopmentalStage.Juvenile() || Worship.Scoffer(p) || !WorshipJobs.Free(p, false)) continue;
                if (!p.CanReach(shrine, PathEndMode.Touch, Danger.Some)) continue;
                if (WorshipJobs.Start(p, WorshipJobs.At(WorshipJobs.RiteJob, shrine, Rand.Range(1500, 2200))))
                {
                    b.homeMarks.Remove(m);
                    return;
                }
            }
        }

        public static void RiteArrive(Pawn p, Thing shrine)
        {
            var g = Shrines.GodAt(shrine);
            if (g == null) return;
            Worship.Say(p, "Rimfolk_Pool_Worship_Rite", g, null, "about:" + g.domain.ToString().ToLowerInvariant());
            Worship.PriestLight(p, shrine.Position);
            Worship.Glow(shrine.Map, shrine.Position, g);
        }

        public static void RiteDone(Pawn p, Thing shrine)
        {
            var g = Shrines.GodAt(shrine);
            if (g == null) return;
            var map = shrine.Map;
            float more = 2f * (Worship.IsPriest(p) ? WorshipMath.PriestRiteFactor : 1f) * (Worship.MagicPriest(p) ? 1.25f : 1f);
            string text = "CC_Wsh_RiteDone".Translate(p.LabelShort.Named("PAWN"), Worship.ShrineOf(g).Named("SHRINE"), Worship.Called(g).Named("GOD"), Dishes(g).Named("DISHES")).Resolve();
            Worship.Favour(g, map, more, text, FavourSource.Rite);
            Tell.Minor(NewsKind.Colony, Importance.Flavor, map, text, shrine);
            if (Worship.IsPriest(p)) CouncilRecords.Add(p, "Council_RitualsLed");
            FaithHeart.Witnessed(map, g, shrine.Position);
        }

        /// <summary>
        /// The chant a rite of this god is kept with: Alpha Memes' by the god's kind and domain (the Neolithic for spirits of
        /// the wild and the hunt, the Kemetic for the dead, the waters and the harvest's old gods, the Mesoamerican for storm
        /// and war, the maddening chant for the void, steampunk for the machine and the forge, the cubic for an archotech),
        /// else Ideology's own for the like; none without either.
        /// </summary>
        public static SoundDef ChantFor(God g)
        {
            if (g == null) return null;
            var names = new List<string>();
            // The god's own chant first, where its worship def names one.
            var own = GodWorshipDef.For(g)?.chants;
            if (own != null) names.AddRange(own);
            if (g.kind == GodKind.Hunger || g.domain == GodDomain.Void) names.AddRange(new[] { "AM_RitualSustainer_MaddeningChant", "RitualSustainer_Morbid" });
            else if (g.kind == GodKind.Archotech) names.AddRange(new[] { "AM_RitualSustainer_Cubicism", "AM_RitualSustainer_SteampunkRevival" });
            else
                switch (g.domain)
                {
                    case GodDomain.Hunt:
                    case GodDomain.Wild:
                        names.AddRange(new[] { "AM_RitualSustainer_Neolithic", "RitualSustainer_Animalist" });
                        break;
                    case GodDomain.Dead:
                    case GodDomain.Waters:
                    case GodDomain.Night:
                        names.AddRange(new[] { "AM_RitualSustainer_Kemetic", "RitualSustainer_Morbid" });
                        break;
                    case GodDomain.Storm:
                    case GodDomain.War:
                        names.AddRange(new[] { "AM_RitualSustainer_Mesoamerican", "RitualSustainer_Rustic" });
                        break;
                    case GodDomain.Machine:
                    case GodDomain.Craft:
                        names.AddRange(new[] { "AM_RitualSustainer_SteampunkRevival", "RitualSustainer_Rustic" });
                        break;
                    case GodDomain.Healing:
                        names.AddRange(new[] { "RitualSustainer_Christian", "AM_RitualSustainer_Kemetic" });
                        break;
                    case GodDomain.Way:
                        names.AddRange(new[] { "RitualSustainer_Islamic", "AM_RitualSustainer_Mesoamerican" });
                        break;
                    case GodDomain.Hearth:
                        names.AddRange(new[] { "AM_RitualSustainer_TeaCeremony", "RitualSustainer_Rustic" });
                        break;
                    default:
                        names.AddRange(new[] { g.form == GodForm.Spirit ? "AM_RitualSustainer_Neolithic" : "AM_RitualSustainer_Kemetic", "RitualSustainer_Rustic" });
                        break;
                }
            foreach (var n in names)
            {
                var s = DefDatabase<SoundDef>.GetNamedSilentFail(n);
                if (s != null) return s;
            }
            return null;
        }

        /// <summary>A people that holds the god sends a few of its own to the festival (the game's own visitors), now and then.</summary>
        private static void Visitors(God g, Map map)
        {
            if (!Rand.Chance(WorshipMath.FestivalVisitorsChance)) return;
            try
            {
                var f = g.factions.Where(x => x != null && !x.IsPlayer && !x.defeated && !x.Hidden && !x.HostileTo(Faction.OfPlayer)).InRandomOrder().FirstOrDefault();
                var def = IncidentDefOf.VisitorGroup;
                if (f == null || def == null) return;
                var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                parms.faction = f;
                parms.forced = true;
                if (!def.Worker.CanFireNow(parms)) return;
                if (def.Worker.TryExecute(parms))
                    Tell.Minor(NewsKind.World, Importance.Notable, map, "CC_Wsh_FestivalVisitors".Translate(f.Name.Named("PEOPLE"), Worship.Called(g).Named("GOD")).Resolve(), null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: festival visitors", e);
            }
        }
    }
}
