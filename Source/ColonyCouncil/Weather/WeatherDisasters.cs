using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Seasonal Weather's disasters, met by the colony (only with that mod; nothing here makes one happen). On a dry,
    /// windy day in summer or fall, with grass and brush right up to the home area, the advisor suggests a firebreak, and
    /// one button marks the brush in a strip round the home area for cutting (the game's own designation). When an
    /// earthquake is over, the best builder and a few others go round the roofs and walls: a work party repairing,
    /// cleaning and hauling what it left. The chronicle remembers it, and the year's annals count the quakes.
    /// </summary>
    public static class WeatherDisasters
    {
        public const string SeasonalWeather = "mlie.seasonalweather";

        /// <summary>The width of the firebreak outside the home area, in cells, and how much brush in it is worth the advice.</summary>
        public const int BreakWidth = 3, BrushWorthIt = 30;

        /// <summary>Days with nothing falling before the advice comes.</summary>
        public const int DryDays = 4;

        public static bool Active => ModsConfig.IsActive(SeasonalWeather);

        /// <summary>A strong wind: Seasonal Weather's windy weather, or the wind blowing hard.</summary>
        public static bool Windy(Map map) =>
            map?.weatherManager?.curWeather?.defName == "Windy" || (map?.windManager?.WindSpeed ?? 0f) >= 0.9f;

        /// <summary>The season when a wildfire would run: summer or fall.</summary>
        public static bool FireSeason(Map map)
        {
            var s = GenLocalDate.Season(map);
            return s == Season.Summer || s == Season.Fall || s == Season.PermanentSummer;
        }

        private static readonly Dictionary<int, (int hour, List<Plant> brush)> brushKept = new Dictionary<int, (int, List<Plant>)>();

        /// <summary>
        /// The wild grass and brush (not trees, not crops) within <see cref="BreakWidth"/> cells outside the home area:
        /// what a firebreak would cut. Worked out at most once an hour per map.
        /// </summary>
        public static List<Plant> Brush(Map map)
        {
            int hour = Find.TickManager.TicksGame / GenDate.TicksPerHour;
            if (brushKept.TryGetValue(map.uniqueID, out var kept) && kept.hour == hour) return kept.brush;
            var list = new List<Plant>();
            var home = map.areaManager?.Home;
            if (home != null && home.TrueCount > 0)
            {
                var seen = new HashSet<IntVec3>();
                foreach (var c in home.ActiveCells)
                {
                    if (!Edge(c, home, map)) continue;
                    foreach (var r in GenRadial.RadialCellsAround(c, BreakWidth, false))
                    {
                        if (!r.InBounds(map) || home[r] || !seen.Add(r)) continue;
                        var p = r.GetPlant(map);
                        if (p == null || p.def.plant == null || p.def.plant.IsTree || p.sown || p.Map.zoneManager.ZoneAt(r) is Zone_Growing) continue;
                        list.Add(p);
                    }
                    if (list.Count > 2000) break;
                }
            }
            if (brushKept.Count > 20) brushKept.Clear();
            brushKept[map.uniqueID] = (hour, list);
            return list;
        }

        private static bool Edge(IntVec3 c, Area home, Map map)
        {
            for (int i = 0; i < 8; i++)
            {
                var n = c + GenAdj.AdjacentCells[i];
                if (n.InBounds(map) && !home[n]) return true;
            }
            return false;
        }

        /// <summary>Marks the brush in the firebreak for cutting; how many were marked.</summary>
        public static int MarkFirebreak(Map map)
        {
            int marked = 0;
            foreach (var p in Brush(map))
            {
                if (p == null || !p.Spawned || map.designationManager.DesignationOn(p, DesignationDefOf.CutPlant) != null) continue;
                map.designationManager.AddDesignation(new Designation(p, DesignationDefOf.CutPlant));
                marked++;
            }
            brushKept.Remove(map.uniqueID);
            return marked;
        }

        /// <summary>
        /// An earthquake is over on a home map: the best builder and a few others go round the roofs and walls, and the
        /// chronicle remembers it.
        /// </summary>
        public static void QuakeOver(Map map)
        {
            var builder = map.mapPawns.FreeColonistsSpawned.Where(p => !p.DevelopmentalStage.Juvenile() && p.skills != null && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction))
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Construction)?.Level ?? 0).FirstOrDefault();
            if (builder == null) return;
            var party = new DilemmaEffect_WorkParty
            {
                label = "CC_Weather_QuakeParty".Translate().Resolve(),
                count = 4,
                tasks = new List<WorkTask> { WorkTask.Repair, WorkTask.Clean, WorkTask.Haul },
                hours = new IntRange(0, 23),
                waitHours = 3f,
                duration = new FloatRange(2f, 3f)
            };
            party.Apply(new DilemmaContext { map = map, a = builder });
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Weather_QuakeChronicle".Translate(builder.Named("PAWN")).Resolve(), builder));
        }
    }

    /// <summary>
    /// A dry, windy day in the fire season with brush up to the home area, while Seasonal Weather can send a wildfire:
    /// cut a firebreak (one button marks it).
    /// </summary>
    public class AdvisorWorker_Firebreak : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!WeatherDisasters.Active || !map.IsPlayerHome || !WeatherDisasters.FireSeason(map) || !WeatherDisasters.Windy(map)) yield break;
            var days = WeatherBook.Of(map)?.Days;
            if (days == null || !WeatherMath.Fits("dry:" + WeatherDisasters.DryDays, days, "summer")) yield break;
            if (map.gameConditionManager.ActiveConditions.Any(c => c.def.defName == "Wildfire")) yield break;
            var brush = WeatherDisasters.Brush(map);
            int open = brush.Count(p => p.Spawned && map.designationManager.DesignationOn(p, DesignationDefOf.CutPlant) == null);
            if (open < WeatherDisasters.BrushWorthIt) yield break;
            int dry = WeatherMath.Run(days, d => d.Dry);
            var s = Signal(0.35f, "CC_Adv_Firebreak_Title".Translate(), "CC_Adv_Firebreak_Detail".Translate(dry, open, WeatherDisasters.BreakWidth), 0.6f, 0.8f);
            s.key = "firebreak";
            s.act = () =>
            {
                int n = WeatherDisasters.MarkFirebreak(map);
                Messages.Message("CC_Adv_Firebreak_Marked".Translate(n), MessageTypeDefOf.TaskCompletion, false);
            };
            s.actLabel = "CC_Adv_Firebreak_Act".Translate();
            yield return s;
        }
    }
}
