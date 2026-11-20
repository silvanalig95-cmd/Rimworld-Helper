using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// After the storm: a work party for every disaster (C19 of docs/content-update-proposals.md; the player's go of 20
    /// November 2026). The weather record looks once an hour at what is happening on a home map (Weather/WeatherBook.cs);
    /// when one of these is over, and the land is quiet again, the one best at the work and a few others set to it with the
    /// game's own jobs (a work party, PlayOut/PlayWork.cs), only where there is something to do:
    /// <list type="bullet">
    /// <item>an earthquake (Seasonal Weather's, or Vanilla Events Expanded's, which comes and goes in a moment): the
    /// builders go round the roofs and walls, mending, rebuilding what the game has marked for it, patching roofs,
    /// cleaning and hauling;</item>
    /// <item>a tornado (the game's own, as Vanilla Landmarks Expanded's tornado alley sends them): the same, the debris
    /// cleared;</item>
    /// <item>a flashstorm: once its fires are out in the home area, the growers mark the burnt trees there for cutting,
    /// clear them and the ash, and sow the fields again where the season allows;</item>
    /// <item>Rimfolk's own hail: the growers sow again where it flattened the young crops (the hail destroys those
    /// outright, so there is nothing left to cut) and bring in what is ripe;</item>
    /// <item>a flood (Odyssey's seasonal flood, Vanilla Landmarks Expanded's tidal flooding): what the water left lying about
    /// is carried back into storage, and the dirt is cleaned up.</item>
    /// </list>
    /// When the party has done its work the chronicle keeps it (PlayAfter, "afterstorm:"); the year's annals count each
    /// disaster. Nothing here makes a disaster happen, and no one is hurt by it (the fires are the game's). The rules
    /// without game types are in AfterStormMath.
    /// </summary>
    public static class AfterStorm
    {
        /// <summary>What a party's plan says it is (PlayOut.afterward): "afterstorm:tornado".</summary>
        public const string AfterPrefix = "afterstorm:";

        private static ThingDef flood, burnedTree;
        private static bool looked;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            flood = DefDatabase<ThingDef>.GetNamedSilentFail("SeasonalFlood");
            burnedTree = DefDatabase<ThingDef>.GetNamedSilentFail("BurnedTree");
        }

        private static readonly HashSet<string> FlashstormDefs = new HashSet<string> { "Flashstorm", "VESWW_FullMapFlashstorm" };

        /// <summary>The disasters going on now on the map.</summary>
        public static HashSet<string> OnNow(Map map)
        {
            Look();
            var on = new HashSet<string>();
            var conditions = map.gameConditionManager?.ActiveConditions;
            if (conditions != null)
                foreach (var c in conditions)
                {
                    string n = c?.def?.defName;
                    if (n == null) continue;
                    if (n == "Earthquake" && WeatherDisasters.Active) on.Add(AfterStormMath.Quake);
                    else if (FlashstormDefs.Contains(n)) on.Add(AfterStormMath.Flashstorm);
                    else if (n == "VEE_TidalFlooding") on.Add(AfterStormMath.Flood);
                }
            if (GameCondition_Hail.Falling(map)) on.Add(AfterStormMath.Hail);
            if (WeatherMods.TornadoOn(map)) on.Add(AfterStormMath.Tornado);
            if (flood != null && map.listerThings.ThingsOfDef(flood).Count > 0) on.Add(AfterStormMath.Flood);
            return on;
        }

        /// <summary>
        /// The weather record's hour (WeatherBook.Hour): what began is counted for the year's annals, what ended has its
        /// party waiting, and a party whose time has come sets out.
        /// </summary>
        public static void Hourly(Map map, WeatherBook book, int year)
        {
            int now = Find.TickManager.TicksGame;
            var on = OnNow(map);
            var was = book.StormsOn;
            foreach (var kind in on)
            {
                if (was.Contains(kind)) continue;
                book.CountDisaster(year, kind);
                // A tornado crossing the land is written into the chronicle when it comes (as before).
                if (kind == AfterStormMath.Tornado) CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Weather_TornadoChronicle".Translate().Resolve()));
            }
            foreach (var kind in was)
            {
                if (on.Contains(kind)) continue;
                book.afterWaiting = AfterStormMath.Add(book.afterWaiting, kind, now + AfterStormMath.DelayHours(kind) * GenDate.TicksPerHour);
                if (kind == AfterStormMath.Tornado)
                {
                    var home = ColonyCalendar.AtHome(map);
                    if (home.Count > 0) CouncilGameComponent.Instance?.journals.WriteTagged(home.RandomElement(), "Council_Journal_Tornado", new string[0]);
                }
            }
            book.StormsOn = on;
            SetOut(map, book, now, false);
        }

        /// <summary>
        /// A disaster that comes and goes in a moment (Vanilla Events Expanded's earthquake, noted from the game's event):
        /// counted, and its party waits as for one that has ended.
        /// </summary>
        public static void Noted(Map map, string kind)
        {
            var book = WeatherBook.Of(map);
            if (book == null || !map.IsPlayerHome || !AfterStormMath.IsKind(kind)) return;
            book.CountDisaster(GenLocalDate.Year(map), kind);
            book.afterWaiting = AfterStormMath.Add(book.afterWaiting, kind, Find.TickManager.TicksGame + (AfterStormMath.DelayHours(kind) + 1) * GenDate.TicksPerHour);
        }

        /// <summary>The parties whose time has come set out: on a quiet map, the fires out, someone able to lead it.</summary>
        private static void SetOut(Map map, WeatherBook book, int now, bool force)
        {
            var waiting = AfterStormMath.Unpack(book.afterWaiting);
            if (waiting.Count == 0) return;
            bool quiet = map.dangerWatcher == null || map.dangerWatcher.DangerRating == StoryDanger.None;
            foreach (var (kind, due) in waiting)
            {
                if (!force && !AfterStormMath.Due(due, now)) continue;
                if (!force && AfterStormMath.TooLate(due, now, GenDate.TicksPerHour))
                {
                    book.afterWaiting = AfterStormMath.Remove(book.afterWaiting, kind);
                    continue;
                }
                if (!force && (!quiet || kind == AfterStormMath.Flashstorm && FiresAtHome(map) > 0)) continue;
                var r = Start(map, kind);
                // Set going, or nothing to do: it waits no more. Nobody able now: it waits.
                if (r != Started.NoOne) book.afterWaiting = AfterStormMath.Remove(book.afterWaiting, kind);
            }
        }

        public enum Started
        {
            Going,
            NothingToDo,
            NoOne
        }

        private static int FiresAtHome(Map map)
        {
            var home = map.areaManager?.Home;
            if (home == null) return 0;
            return map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count(f => f.Spawned && home[f.Position]);
        }

        /// <summary>The one best at the work: building after a quake, a tornado or a flood, growing after a flashstorm or hail.</summary>
        private static Pawn Leader(Map map, string kind)
        {
            bool fields = kind == AfterStormMath.Flashstorm || kind == AfterStormMath.Hail;
            var skill = fields ? SkillDefOf.Plants : SkillDefOf.Construction;
            var work = fields ? WorkTypeDefOf.Growing : WorkTypeDefOf.Construction;
            return map.mapPawns.FreeColonistsSpawned.Where(p => !p.DevelopmentalStage.Juvenile() && p.skills != null && !p.WorkTypeIsDisabled(work) && !p.Downed && !p.InMentalState
                                                                && People.HasInnerLife(p) && !p.IsQuestLodger())
                .OrderByDescending(p => p.skills.GetSkill(skill)?.Level ?? 0).FirstOrDefault();
        }

        private static List<WorkTask> Tasks(string kind)
        {
            switch (kind)
            {
                case AfterStormMath.Flashstorm: return new List<WorkTask> { WorkTask.Cut, WorkTask.Clean, WorkTask.Sow };
                case AfterStormMath.Hail: return new List<WorkTask> { WorkTask.Sow, WorkTask.Harvest };
                case AfterStormMath.Flood: return new List<WorkTask> { WorkTask.Haul, WorkTask.Clean };
                default: return new List<WorkTask> { WorkTask.Repair, WorkTask.Build, WorkTask.Roof, WorkTask.Clean, WorkTask.Haul };
            }
        }

        /// <summary>Whether there's anything to do after it; for a flashstorm, the burnt trees in and round the home area are marked for cutting first.</summary>
        private static bool Work(Map map, string kind)
        {
            var home = map.areaManager?.Home;
            switch (kind)
            {
                case AfterStormMath.Flashstorm:
                {
                    int marked = MarkBurnt(map);
                    int ash = home == null ? 0 : map.listerFilthInHomeArea.FilthInHomeArea.Count(f => f.def.defName == "Filth_Ash");
                    return marked > 0 || ash >= 3;
                }
                case AfterStormMath.Hail:
                    return GrowingWeather(map) && PlantingDay.ToSow(map) > 0;
                case AfterStormMath.Flood:
                    return map.listerFilthInHomeArea.FilthInHomeArea.Count >= 5 || map.listerHaulables.ThingsPotentiallyNeedingHauling().Count > 0;
                default:
                    return map.listerBuildingsRepairable.RepairableBuildings(Faction.OfPlayer).Count > 0
                           || map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint).Any(b => b.Faction == Faction.OfPlayer);
            }
        }

        /// <summary>Warm enough to sow: a growing month, and above 4 °C out of doors now (as the planting day asks).</summary>
        private static bool GrowingWeather(Map map)
        {
            var g = PlantingDay.Growable(map);
            int t = (int)GenLocalDate.Twelfth(map);
            return t >= 0 && t < g.Length && g[t] && (map.mapTemperature?.OutdoorTemp ?? 0f) > 4f;
        }

        /// <summary>The burnt trees in the home area and the fields, and a few steps round the home area, marked for cutting; how many were marked.</summary>
        public static int MarkBurnt(Map map)
        {
            Look();
            var home = map.areaManager?.Home;
            if (home == null) return 0;
            int n = 0;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).ToList())
            {
                if (!(t is Plant p) || !p.Spawned || !Burnt(p.def)) continue;
                var c = p.Position;
                bool near = home[c] || c.GetZone(map) is Zone_Growing;
                if (!near)
                    for (int i = 0; i < 8 && !near; i++)
                    {
                        var x = c + GenAdj.AdjacentCells[i] * 2;
                        near = x.InBounds(map) && home[x];
                    }
                if (!near || p.IsForbidden(Faction.OfPlayer) || map.designationManager.DesignationOn(p) != null) continue;
                map.designationManager.AddDesignation(new Designation(p, DesignationDefOf.CutPlant));
                n++;
            }
            return n;
        }

        private static bool Burnt(ThingDef d) => d != null && (d == burnedTree || d.plant != null && d.defName.StartsWith("Burned", StringComparison.Ordinal));

        /// <summary>The party after one of them, set going now: who leads it, what they do, and the chronicle's line when it's done.</summary>
        public static Started Start(Map map, string kind)
        {
            if (map == null || !AfterStormMath.IsKind(kind)) return Started.NothingToDo;
            var lead = Leader(map, kind);
            if (lead == null) return Started.NoOne;
            if (!Work(map, kind)) return Started.NothingToDo;
            var party = new DilemmaEffect_WorkParty
            {
                label = (kind == AfterStormMath.Quake ? "CC_Weather_QuakeParty" : "CC_After_Party_" + kind).Translate().Resolve(),
                count = AfterStormMath.Hands,
                tasks = Tasks(kind),
                hours = new IntRange(6, 20),
                waitHours = 6f,
                duration = new FloatRange(2f, 3f)
            };
            var p = party.Begin(new DilemmaContext { map = map, a = lead });
            if (p == null) return Started.NothingToDo;
            p.afterward = AfterPrefix + kind;
            return Started.Going;
        }

        /// <summary>The party is over (PlayAfter.Run): if it did any of the work, the chronicle keeps it, with the one who led it.</summary>
        public static void Over(PlayOut p, string what, bool happened)
        {
            if (!happened || p == null || p.tally <= 0) return;
            string kind = what.Substring(AfterPrefix.Length);
            if (!AfterStormMath.IsKind(kind)) return;
            var lead = p.organizer ?? p.hands.FirstOrDefault()?.pawn;
            if (lead == null) return;
            string key = kind == AfterStormMath.Quake ? "CC_Weather_QuakeChronicle" : "CC_After_Chronicle_" + kind;
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, key.Translate(lead.Named("PAWN")).Resolve(), lead));
        }

        /// <summary>Dev: the party after one of them, now, as if it had just ended.</summary>
        public static string DevNow(Map map, string kind)
        {
            switch (Start(map, kind))
            {
                case Started.Going: return "the work party is set going (it starts in its hours, 6 to 20).";
                case Started.NoOne: return "nobody able to lead it here now.";
                default: return "nothing to do after it here (nothing damaged, burnt, flattened or left lying about).";
            }
        }
    }

    /// <summary>
    /// One of the game's events happened: Vanilla Events Expanded's earthquake comes and goes in a moment, so its work party
    /// is noted from the event itself (AfterStorm.Noted). The event and its letter are untouched. Off with the deeper game hooks.
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]
    public static class Patch_AfterStorm_TryExecute
    {
        public static void Postfix(IncidentWorker __instance, IncidentParms parms, bool __result)
        {
            if (!__result || !RimfolkPatches.On || __instance?.def?.defName != "VEE_Earthquake") return;
            try
            {
                if (parms?.target is Map map) AfterStorm.Noted(map, AfterStormMath.Quake);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("after the storm: an earthquake", e);
            }
        }
    }
}
