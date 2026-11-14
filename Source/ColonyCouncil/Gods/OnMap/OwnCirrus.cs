using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Cirrus, Who Drifts Above, on the map (docs/gods-next-proposals.md, C 7): the sky, clear or storm, stars and falling
    /// things. It has no body and never comes down; what it does comes out of the sky.
    /// <list type="bullet">
    /// <item>The star at the door: its kindness (the game's own meteorite, GodHealth) comes only to a colony that watches the
    /// sky (looked up lately with a telescope or lying out skygazing, or its festival kept), and the one who watched says they
    /// saw it fall.</item>
    /// <item>The wrong-way star (its sign) and the dream: whoever saw it dreams that night where something fell, and the next
    /// day cargo pods come down there.</item>
    /// <item>The aurora on the coldest night: the sky fills with light (the game's own aurora) for those out of doors on a night
    /// colder than any for days.</item>
    /// <item>Dry thunder: after a long clear spell with nothing of the sky's, thunder and lightning roll over the far hills for
    /// an hour or two, with no rain and never on the colony.</item>
    /// </list>
    /// </summary>
    public static class OwnCirrus
    {
        public const string Key = "Cirrus";

        /// <summary>What counts as watching the sky: the game's skygazing and telescope, and Vanilla Social Interactions Expanded's skygazing.</summary>
        private static readonly string[] Watching = { "Skygaze", "UseTelescope", "VSIE_Skygaze" };

        private static God It => OwnHappenings.God(Key);

        private static int Now => OwnHappenings.Now;

        // ================= Watching the sky =================

        /// <summary>The colony watches the sky (OwnHappeningsMath.Watches): looked up lately, or its festival kept.</summary>
        public static bool Watches(Map map)
        {
            var b = OwnHappenings.Book;
            if (b == null) return false;
            int now = Now;
            int window = Mathf.RoundToInt(OwnHappeningsMath.WatchDays * GenDate.TicksPerDay);
            return OwnHappeningsMath.Watches(b.watches.Count(t => now - t <= window), GodPace.DaysSince(b.festivalKept, now));
        }

        /// <summary>The hour: anyone of the colony looking up (a telescope, skygazing) is a watch of the sky; one an hour is kept.</summary>
        private static void Look(OwnHappeningsBook b, int now)
        {
            foreach (var map in OwnHappenings.HomeMaps)
            {
                var p = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(x => x.CurJobDef != null && Array.IndexOf(Watching, x.CurJobDef.defName) >= 0);
                if (p == null) continue;
                b.watches.Add(now);
                while (b.watches.Count > OwnHappeningsBook.MaxWatches) b.watches.RemoveAt(0);
                b.watcher = p;
                return;
            }
        }

        /// <summary>Its festival kept (Festivals.Kept): a watch of the sky.</summary>
        public static void FestivalKept()
        {
            var b = OwnHappenings.Book;
            if (b != null) b.festivalKept = Now;
        }

        /// <summary>The star fell (its kindness, the game's meteorite, GodHealth.Bless): the one who watched the sky says they saw it come. Always true.</summary>
        public static bool StarFell(God g, Map map)
        {
            var b = OwnHappenings.Book;
            if (b == null || map == null) return true;
            int now = Now;
            OwnHappenings.Came(b, OwnHappening.CirrusStar, now);
            var p = b.watcher != null && b.watcher.Spawned && b.watcher.Map == map && !b.watcher.Dead ? b.watcher : map.mapPawns.FreeColonistsSpawned.FirstOrDefault(GodOmens.Awake);
            if (p == null) return true;
            OwnHappenings.Say(p, "starfell", g);
            OwnHappenings.Journal(p, "CC_GodO_Journal_Star", p.Named("PAWN"));
            OwnHappenings.Saw(g, map, p.Position, GodActKind.Blessing, p, "CC_GodO_Saw_Star");
            return true;
        }

        // ================= The wrong-way star, and the dream =================

        /// <summary>Its sign, the star that moves the wrong way (GodOmens.Seen), seen by <paramref name="seer"/>: they may dream tonight where something fell.</summary>
        public static void WrongStar(Map map, Pawn seer)
        {
            var b = OwnHappenings.Book;
            int now = Now;
            if (b == null || map == null || seer == null || b.fallStage != 0) return;
            if (OwnHappenings.forced != OwnHappening.CirrusFall
                && (!OwnHappeningsMath.Free(OwnHappening.CirrusFall, OwnHappenings.DaysSince(b, OwnHappening.CirrusFall, now)) || !Rand.Chance(OwnHappeningsMath.FallChance)))
                return;
            var at = Fall(map);
            if (!at.IsValid) return;
            b.fallSeer = seer;
            b.fallMap = map;
            b.fallAt = at;
            b.fallStage = 1;
            OwnHappenings.Came(b, OwnHappening.CirrusFall, now);
        }

        /// <summary>Open ground out from the colony where something could come down, or Invalid.</summary>
        private static IntVec3 Fall(Map map)
        {
            var centre = CampSite.ColonyCenter(map);
            if (!centre.IsValid) centre = map.Center;
            for (int i = 0; i < 50; i++)
            {
                var c = CellFinder.RandomClosewalkCellNear(centre, map, 40);
                if (c.InHorDistOf(centre, 15f) || c.Roofed(map) || c.Fogged(map) || !c.Standable(map) || c.GetEdifice(map) != null) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>Which way a place lies from the colony, in words ("to the north").</summary>
        internal static string Way(Map map, IntVec3 at)
        {
            var centre = CampSite.ColonyCenter(map);
            if (!centre.IsValid) centre = map.Center;
            var d = at - centre;
            string k = Math.Abs(d.x) > Math.Abs(d.z) * 2 ? (d.x > 0 ? "East" : "West") : Math.Abs(d.z) > Math.Abs(d.x) * 2 ? (d.z > 0 ? "North" : "South")
                : (d.z > 0 ? "North" : "South") + (d.x > 0 ? "East" : "West");
            return ("CC_GodO_Way_" + k).Translate().Resolve();
        }

        /// <summary>At dawn: the dream told, and the pods' hour set for today.</summary>
        internal static bool Dream(OwnHappeningsBook b, int now, bool anyway = false)
        {
            var p = b.fallSeer;
            var map = b.fallMap;
            var g = It;
            var comp = CouncilGameComponent.Instance;
            if (p == null || map == null || g == null || comp == null || p.Dead || !p.Spawned || p.Map != map)
            {
                b.fallStage = 0;
                return false;
            }
            if (!anyway && comp.dreams?.WokeFromNight(p) != true && OwnHappenings.Hour(map) < 9) return false;
            string way = Way(map, b.fallAt);
            GodOmens.Dreamt(comp, p, "CC_GodO_Fall_Dream".Translate(way.Named("WAY")).Resolve());
            OwnHappenings.Say(p, "falldream", g, null, way.Named("WAY"));
            OwnHappenings.Saw(g, map, p.Position, GodActKind.Omen, p, "CC_GodO_Saw_FallDream", way.Named("WAY"));
            b.fallStage = 2;
            int hour = OwnHappenings.Hour(map);
            int at = Rand.RangeInclusive(OwnHappeningsMath.PodFrom, OwnHappeningsMath.PodTo);
            b.fallDue = now + Math.Max(1, at - hour) * GenDate.TicksPerHour;
            return true;
        }

        /// <summary>The pods come down where the dream put them (the game's own cargo).</summary>
        internal static bool Pods(OwnHappeningsBook b, int now)
        {
            var map = b.fallMap;
            var g = It;
            b.fallStage = 0;
            if (map == null || g == null || !b.fallAt.IsValid || !Homes.IsHome(map)) return false;
            List<Thing> things;
            try
            {
                things = ThingSetMakerDefOf.ResourcePod.root.Generate();
                if (things.NullOrEmpty()) return false;
                DropPodUtility.DropThingsNear(b.fallAt, map, things);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: pods where the dream said", e);
                return false;
            }
            string way = Way(map, b.fallAt);
            string who = b.fallSeer?.LabelShort ?? "";
            OwnHappenings.Card(g, map, "Pods", true, new LookTargets(b.fallAt, map), LetterDefOf.PositiveEvent, Importance.Notable, way.Named("WAY"), who.Named("PAWN"));
            var seer = GodsOnMapWords.Seer(map, b.fallAt, 50f) ?? b.fallSeer;
            OwnHappenings.Say(b.fallSeer, "pods", g);
            OwnHappenings.Saw(g, map, b.fallAt, GodActKind.Blessing, seer, "CC_GodO_Saw_Pods", who.Named("PAWN"));
            return true;
        }

        // ================= The aurora on the coldest night =================

        /// <summary>A night colder than any for days, someone of the colony out of doors: the sky may fill with light (once a night at most).</summary>
        private static void Aurora(OwnHappeningsBook b, God g, Map map, int now)
        {
            int h = OwnHappenings.Hour(map);
            if (h < 21 && h > 3) return;
            int night = GenDate.DaysPassed;
            if (b.auroraNight == night && OwnHappenings.forced != OwnHappening.CirrusAurora) return;
            var book = WeatherBook.Of(map);
            var days = book?.DaysBefore?.Where(d => d != null && d.Counted).ToList();
            if (days == null) return;
            float tonight = map.mapTemperature.OutdoorTemp;
            float coldest = days.Count == 0 ? 999f : days.Min(d => d.min);
            var outside = map.mapPawns.FreeColonistsSpawned.Where(p => GodOmens.Awake(p) && !p.Position.Roofed(map)).ToList();
            if (OwnHappenings.forced != OwnHappening.CirrusAurora && (!OwnHappeningsMath.Coldest(tonight, coldest, days.Count) || outside.Count == 0)) return;
            b.auroraNight = night;
            if (OwnHappenings.forced != OwnHappening.CirrusAurora
                && (!OwnHappeningsMath.Free(OwnHappening.CirrusAurora, OwnHappenings.DaysSince(b, OwnHappening.CirrusAurora, now)) || !Rand.Chance(OwnHappeningsMath.AuroraChance)))
                return;
            Light(b, g, map, outside, now);
        }

        internal static bool Light(OwnHappeningsBook b, God g, Map map, List<Pawn> outside, int now)
        {
            var def = DefDatabase<GameConditionDef>.GetNamedSilentFail("Aurora");
            if (def == null || map.gameConditionManager.ConditionIsActive(def)) return false;
            try
            {
                map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(def, OwnHappeningsMath.AuroraHours * GenDate.TicksPerHour));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the aurora on the coldest night", e);
                return false;
            }
            OwnHappenings.Came(b, OwnHappening.CirrusAurora, now);
            var p = outside.RandomElementWithFallback() ?? map.mapPawns.FreeColonistsSpawned.FirstOrDefault(GodOmens.Awake);
            OwnHappenings.Card(g, map, "Aurora", false, p != null ? new LookTargets(p) : null);
            OwnHappenings.Say(p, "aurora", g);
            OwnHappenings.Saw(g, map, IntVec3.Invalid, GodActKind.Blessing, p, "CC_GodO_Saw_Aurora");
            return true;
        }

        // ================= Dry thunder =================

        /// <summary>Days of no rain or snow at all on a map, back from yesterday.</summary>
        private static int DryDays(Map map)
        {
            var days = WeatherBook.Of(map)?.DaysBefore;
            int n = 0;
            if (days == null) return 0;
            for (int i = days.Count - 1; i >= 0 && days[i] != null && days[i].Counted && days[i].Dry; i--) n++;
            return n;
        }

        /// <summary>Days since anything of the sky's at a home: its own happenings, or a god answering in the weather.</summary>
        private static float SkyQuiet(OwnHappeningsBook b, Map map, int now)
        {
            int last = OwnHappeningsMath.Of(Key).Max(h => b.Last(h));
            var mb = GodsOnMapBook.Instance;
            if (mb != null) last = Math.Max(last, mb.LastWeather(Gods.HomeKey(map)));
            return GodPace.DaysSince(last, now);
        }

        /// <summary>Thunder begins over the far hills: its flashes an hour or so apart on a clear sky.</summary>
        internal static bool Thunder(OwnHappeningsBook b, Map map, int now)
        {
            if (map == null || b.thunderLeft > 0) return false;
            b.thunderMap = map;
            b.thunderLeft = OwnHappeningsMath.ThunderFlashes;
            b.thunderNext = now + 60;
            b.Due(b.thunderNext);
            OwnHappenings.Came(b, OwnHappening.CirrusThunder, now);
            return true;
        }

        /// <summary>A flash and the roll of it, far off at the land's edge; the first one remarked on.</summary>
        private static void ThunderStep(OwnHappeningsBook b, int now)
        {
            if (b.thunderLeft <= 0) return;
            if (now < b.thunderNext)
            {
                b.Due(b.thunderNext);
                return;
            }
            var map = b.thunderMap;
            var g = It;
            if (map == null || g == null || !Homes.IsHome(map) || map.weatherManager.curWeather?.rainRate > 0.1f)
            {
                b.thunderLeft = 0;
                return;
            }
            bool first = b.thunderLeft == OwnHappeningsMath.ThunderFlashes;
            b.thunderLeft--;
            b.thunderNext = now + OwnHappeningsMath.ThunderTicks / OwnHappeningsMath.ThunderFlashes + Rand.Range(-200, 200);
            if (b.thunderLeft > 0) b.Due(b.thunderNext);
            try
            {
                map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningFlash(map));
                var edge = CellFinder.RandomEdgeCell(map);
                FleckMaker.ThrowLightningGlow(edge.ToVector3Shifted(), map, 4f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: dry thunder", e);
            }
            if (!first) return;
            var p = map.mapPawns.FreeColonistsSpawned.Where(x => GodOmens.Awake(x) && !x.Position.Roofed(map)).RandomElementWithFallback();
            OwnHappenings.Card(g, map, "Thunder", false, null);
            OwnHappenings.Say(p, "drythunder", g);
            OwnHappenings.Saw(g, map, IntVec3.Invalid, GodActKind.Omen, p, "CC_GodO_Saw_Thunder");
        }

        // ================= The day, the hour, the steps =================

        internal static void Daily(OwnHappeningsBook b, int now)
        {
            var g = It;
            if (g == null || b.thunderLeft > 0) return;
            foreach (var map in OwnHappenings.HomeMaps)
            {
                if (!OwnHappenings.Acts(g, map) || !OwnHappenings.Quiet(map)) continue;
                var why = OwnHappeningsMath.ThunderTrigger(DryDays(map), SkyQuiet(b, map, now));
                if (OwnHappenings.Due(b, OwnHappening.CirrusThunder, g, map, why, now))
                {
                    // Some hour of the day ahead.
                    Thunder(b, map, now);
                    b.thunderNext = now + Rand.Range(1, 10) * GenDate.TicksPerHour;
                    b.Due(b.thunderNext);
                    return;
                }
            }
        }

        internal static void Hourly(OwnHappeningsBook b, int now)
        {
            Look(b, now);
            var g = It;
            if (g == null) return;
            if (b.fallStage == 1 && b.fallMap != null)
            {
                int h = OwnHappenings.Hour(b.fallMap);
                if (h >= 5 && h <= 11) Dream(b, now);
                else if (now - b.Last(OwnHappening.CirrusFall) > GenDate.TicksPerDay * 2) b.fallStage = 0;
            }
            else if (b.fallStage == 2 && now >= b.fallDue) Pods(b, now);
            foreach (var map in OwnHappenings.HomeMaps)
                if (OwnHappenings.Acts(g, map) && OwnHappenings.Quiet(map))
                    Aurora(b, g, map, now);
        }

        internal static void Step(OwnHappeningsBook b, int now) => ThunderStep(b, now);
    }
}
