using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The planting day (Defs/DilemmaDefs/Council_Dilemmas_Customs.xml, the custom once called the planting blessing): on
    // land with a cold season, when the fields open again and the colony has growing zones waiting for seed, the council is
    // asked whether to make a day of it. Held, the colony sows together as a work party, and the grower who asked says a
    // few words at the field's edge in the evening. Kept two years running, it becomes a tradition. The player can also put
    // a planting day on the calendar (Calendar → a new day, kept as "a planting day": Calendar/CalendarPlanting.cs). If the
    // fields are all sown by the next evening, the colony notes it, and notes it again when that crop comes ready.

    /// <summary>The land's growing year, the fields waiting for seed, and the calendar's planting days.</summary>
    public static class PlantingDay
    {
        /// <summary>Each twelfth of the year: warm enough on average for crops to grow.</summary>
        public static bool[] Growable(Map map)
        {
            var temps = WishWorker_SeasonClothes.Averages(map);
            var g = new bool[temps.Length];
            for (int i = 0; i < temps.Length; i++) g[i] = temps[i] >= SeasonUtil.GrowMin;
            return g;
        }

        /// <summary>The fields have just opened after the cold season (SeasonWishMath.PlantingWindow), and it's growing weather today.</summary>
        public static bool Window(Map map)
        {
            if (map == null || !SeasonUtil.HasSeasons(map)) return false;
            if (!SeasonWishMath.PlantingWindow(Growable(map), (int)GenLocalDate.Twelfth(map), GenLocalDate.DayOfTwelfth(map))) return false;
            return (map.mapTemperature?.OutdoorTemp ?? 0f) > 4f;
        }

        /// <summary>The growing zones where sowing is allowed and a crop is chosen.</summary>
        public static IEnumerable<Zone_Growing> Fields(Map map) =>
            map?.zoneManager == null ? Enumerable.Empty<Zone_Growing>() : map.zoneManager.AllZones.OfType<Zone_Growing>().Where(z => z.allowSow && z.GetPlantDefToGrow() != null);

        /// <summary>Of the fields' cells, how many have their crop in them, and how many there are in all (a thousand at most looked at).</summary>
        public static void Sown(Map map, out int sown, out int cells)
        {
            sown = 0;
            cells = 0;
            if (map == null) return;
            foreach (var z in Fields(map))
            {
                var crop = z.GetPlantDefToGrow();
                foreach (var c in z.Cells)
                {
                    if (cells >= 1000) return;
                    cells++;
                    var plant = c.GetPlant(map);
                    if (plant != null && plant.def == crop) sown++;
                }
            }
        }

        /// <summary>Cells waiting for seed in the colony's fields.</summary>
        public static int ToSow(Map map)
        {
            Sown(map, out int sown, out int cells);
            return cells - sown;
        }

        /// <summary>The calendar already holds a planting day this season (one of the player's, kept or coming within ten days).</summary>
        public static bool OnCalendar(Map map)
        {
            var cal = CalendarTracker.Instance;
            if (cal?.days == null || map == null) return false;
            int today = GenLocalDate.DayOfYear(map);
            int year = GenLocalDate.Year(map);
            foreach (var d in cal.days)
            {
                if (d == null || d.way != DayWay.Planting) continue;
                if (d.lastKeptYear == year || CalendarMath.DaysUntil(today, d.day) <= 10) return true;
            }
            return false;
        }

        // ---------------- After the day ----------------

        private static ThoughtDef goodStart;
        private static bool looked;

        /// <summary>
        /// The planting day's fields came ready: a good start, for those who sowed them (the colony's growers), as a
        /// memory that gives hope rather than mood, and a line in the news.
        /// </summary>
        public static void GoodStart(Map map)
        {
            if (map == null) return;
            if (!looked)
            {
                looked = true;
                goodStart = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Planting_GoodStart");
            }
            // apart: the crop coming ready is seen by everyone who works the fields, wherever they are that hour.
            foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                var plants = p.skills?.GetSkill(SkillDefOf.Plants);
                if (plants == null || plants.TotallyDisabled || p.DevelopmentalStage.Baby()) continue;
                if (!People.Passionate(plants) && !(p.workSettings?.WorkIsActive(WorkTypeDefOf.Growing) ?? false)) continue;
                if (goodStart != null) CouncilGameComponent.GiveThought(p, goodStart);
            }
            News.Post("CC_Planting_GoodStart".Translate().Resolve(), null, NewsKind.Colony, MessageTypeDefOf.PositiveEvent, Importance.Flavor);
        }
    }

    /// <summary>
    /// The planting day: the fields have just opened after a cold season (the first twelfth that can grow, after one that
    /// couldn't), it's growing weather today, and the colony's fields wait for seed (ten cells or more). Not when the
    /// calendar already holds a planting day this season. Asked in its moment (DilemmaDef.timely).
    /// </summary>
    public class DilemmaWorker_PlantingDay : DilemmaWorker_Custom
    {
        public override bool CanHappenNow(Map map) =>
            base.CanHappenNow(map) && PlantingDay.Window(map) && PlantingDay.ToSow(map) >= 10 && !PlantingDay.OnCalendar(map);
    }

    /// <summary>
    /// The planting day let go: a small, honest cost for those who care about the fields (grown colonists with a passion
    /// for growing) and, with Ideology, for the faithful of a colony whose faith puts nature first or is bound to its trees.
    /// </summary>
    public class DilemmaEffect_PlantingSkipped : DilemmaEffect
    {
        public ThoughtDef thought;
        public List<string> memes = new List<string> { "NaturePrimacy", "TreeConnection" };

        public DilemmaEffect_PlantingSkipped()
        {
            target = DilemmaTarget.Colony;
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (thought == null) return;
            // apart: an answer of the council's, felt by those who care wherever they are.
            using var _cause = Causes.Push(ctx.cause ?? ctx.why);
            bool faith = !memes.NullOrEmpty() && DecreeWork.ColonyFaith != null && DecreeWork.FaithHas(memes);
            foreach (var p in Targets(ctx))
            {
                if (p.DevelopmentalStage.Juvenile()) continue;
                bool grower = People.Passionate(p.skills?.GetSkill(SkillDefOf.Plants));
                if (grower || (faith && DecreeWork.Believer(p))) CouncilGameComponent.GiveThought(p, thought);
            }
        }
    }

    /// <summary>
    /// Watches the planting day's fields: the next evening, if nine cells in ten are sown, the colony notes it; and when
    /// half of them stand ripe within the season, the growers have a good start to remember. One planting day is
    /// watched at a time; a new one takes its place.
    /// </summary>
    public class DilemmaEffect_PlantingWatch : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.map == null) return;
            PlantingWatch.Instance?.Start(ctx.map);
        }
    }

    /// <summary>The watch on the planting day's fields (saved with the game, checked every few hours).</summary>
    public class PlantingWatch : GameComponent
    {
        public static PlantingWatch Instance => Current.Game?.GetComponent<PlantingWatch>();

        private int mapId = -1;
        private int startTick = -1;
        /// <summary>0 waiting for the evening after, 1 all sown (waiting for the crop), 2 done.</summary>
        private int stage = 2;
        private int nextCheck;

        public PlantingWatch(Game game)
        {
        }

        public void Start(Map map)
        {
            mapId = map.uniqueID;
            startTick = Find.TickManager.TicksGame;
            stage = 0;
            nextCheck = startTick + GenDate.TicksPerDay * 3 / 4;
        }

        private Map MapOf()
        {
            foreach (var m in Find.Maps)
                if (m.uniqueID == mapId) return m;
            return null;
        }

        public override void GameComponentTick()
        {
            if (stage >= 2) return;
            int now = Find.TickManager.TicksGame;
            if (now < nextCheck) return;
            nextCheck = now + GenDate.TicksPerHour * 3;
            try
            {
                var map = MapOf();
                if (map == null || now - startTick > GenDate.TicksPerDay * 40)
                {
                    stage = 2;
                    return;
                }
                if (stage == 0)
                {
                    PlantingDay.Sown(map, out int sown, out int cells);
                    if (SeasonWishMath.FullySown(sown, cells))
                    {
                        stage = 1;
                        News.Post("CC_Planting_AllSown".Translate().Resolve(), null, NewsKind.Colony, MessageTypeDefOf.PositiveEvent, Importance.Flavor);
                        CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_Planting_Chronicle".Translate().Resolve(), null));
                    }
                    else stage = 2;
                    return;
                }
                // Waiting for the crop: half of the fields ripe.
                int ripe = 0, all = 0;
                foreach (var z in PlantingDay.Fields(map))
                {
                    var crop = z.GetPlantDefToGrow();
                    foreach (var c in z.Cells)
                    {
                        if (all >= 1000) break;
                        all++;
                        var plant = c.GetPlant(map);
                        if (plant != null && plant.def == crop && plant.HarvestableNow) ripe++;
                    }
                }
                if (all > 0 && ripe * 2 >= all)
                {
                    stage = 2;
                    PlantingDay.GoodStart(map);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("planting watch", e);
                stage = 2;
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref startTick, "startTick", -1);
            Scribe_Values.Look(ref stage, "stage", 2);
            Scribe_Values.Look(ref nextCheck, "nextCheck");
        }
    }
}
