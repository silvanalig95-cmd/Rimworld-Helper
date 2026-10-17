using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    [DefOf]
    public static class CalendarPlantingDefOf
    {
        public static ThoughtDef Council_Cal_PlantingDay;

        static CalendarPlantingDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(CalendarPlantingDefOf));
    }

    /// <summary>
    /// A planting day the player put on the calendar (Calendar → a new day, kept as "a planting day"). When it comes round
    /// and is kept: with seed to sow and growing weather, the colony sows together as a work party (PlayOut/, with answers
    /// playing out on the map) and its grower says a few words at the field's edge in the evening; with nothing to sow,
    /// they walk out to the fields together instead. Those who took part come away with a planting day to remember
    /// (Council_Cal_PlantingDay: hope and a little practice, no mood). The fields are watched afterwards, as for the
    /// council's own planting day (Dilemmas/PlantingDay.cs).
    /// </summary>
    public partial class CalendarTracker
    {
        private static void Planting(CalendarDay d, Map map, List<Pawn> home)
        {
            var thought = CalendarPlantingDefOf.Council_Cal_PlantingDay;
            var keeper = home.Where(p => !p.DevelopmentalStage.Juvenile() && !(p.skills?.GetSkill(SkillDefOf.Plants)?.TotallyDisabled ?? true))
                             .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Plants).Level).FirstOrDefault() ?? Keeper(d, home);
            var ctx = new DilemmaContext
            {
                map = map,
                a = keeper,
                why = "CC_Cal_Cause_Day".Translate(d.label).Resolve(),
                cause = "CC_Cal_Cause_Day".Translate(d.label).Resolve()
            };
            bool sow = PlantingDay.ToSow(map) >= 10 && PlantGrowsNow(map);
            var effects = new List<DilemmaEffect>();
            if (sow)
            {
                effects.Add(new DilemmaEffect_WorkParty
                {
                    label = d.label, count = 6, tasks = new List<WorkTask> { WorkTask.Sow }, near = new List<PlaySpot> { PlaySpot.Fields },
                    hours = new IntRange(6, 13), duration = new FloatRange(3f, 4f), pool = "Council_Pool_PlantingWork"
                });
                effects.Add(new DilemmaEffect_Words
                {
                    label = "CC_Planting_WordsLabel".Translate().Resolve(), spots = new List<PlaySpot> { PlaySpot.Fields, PlaySpot.Outdoors },
                    hours = new IntRange(17, 20), duration = new FloatRange(0.75f, 1f), pool = "Council_Pool_PlantingWords"
                });
                effects.Add(new DilemmaEffect_PlantingWatch());
            }
            else
            {
                effects.Add(new DilemmaEffect_Procession
                {
                    label = d.label, to = PlaySpot.Fields, hours = new IntRange(7, 18), duration = new FloatRange(1f, 1.5f), pool = "Council_Pool_PlantingWords"
                });
            }
            if (thought != null) effects.Add(new DilemmaEffect_Thought { target = DilemmaTarget.Colony, thought = thought });
            PlayOuts.Run(effects, ctx, "calendar planting day");
        }

        /// <summary>Growing weather at the colony's fields today.</summary>
        private static bool PlantGrowsNow(Map map)
        {
            var field = PlantingDay.Fields(map).FirstOrDefault();
            if (field == null || field.Cells.Count == 0) return false;
            return PlantUtility.GrowthSeasonNow(field.Cells[0], map, field.GetPlantDefToGrow());
        }
    }
}
