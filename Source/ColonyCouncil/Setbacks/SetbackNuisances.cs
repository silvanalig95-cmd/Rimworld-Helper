using System;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A nuisance's own check, for nuisances the built-in list doesn't cover (NuisanceDef.checkClass): what on the map it's
    /// about for this colonist (its label, "" when it's about no one thing), or null when it doesn't fit right now.
    /// </summary>
    public abstract class NuisanceCheckWorker
    {
        public abstract string Check(Pawn p, Map map);

        /// <summary>The closed room they're in, or null outdoors.</summary>
        protected static Room IndoorRoom(Pawn p)
        {
            var room = p.GetRoom();
            return room != null && room.ProperRoom && !room.PsychologicallyOutdoors ? room : null;
        }

        protected const int MaxThingsLooked = 400;
    }

    /// <summary>Food gone rotten in the colony's stores, noticed by someone who cooks.</summary>
    public class NuisanceCheck_SpoiltStores : NuisanceCheckWorker
    {
        public override string Check(Pawn p, Map map)
        {
            var cooking = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking");
            if (cooking == null || p.WorkTypeIsDisabled(cooking) || p.workSettings == null || !p.workSettings.WorkIsActive(cooking)) return null;
            var food = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);
            for (int i = 0; i < food.Count && i < MaxThingsLooked; i++)
            {
                var t = food[i];
                if (t is Corpse || t.def.category != ThingCategory.Item || !t.Spawned || !StoreUtility.IsInAnyStorage(t)) continue;
                var rot = t.TryGetComp<CompRottable>();
                if (rot != null && rot.Stage == RotStage.Rotting) return t.def.label;
            }
            return null;
        }
    }

    /// <summary>Working out in the rain with no coat, when the colony could make one.</summary>
    public class NuisanceCheck_NoCoat : NuisanceCheckWorker
    {
        private static int lookedDay = -1;
        private static bool canMake;

        public override string Check(Pawn p, Map map)
        {
            var weather = map.weatherManager;
            if (weather == null || weather.RainRate < 0.3f || map.mapTemperature.OutdoorTemp < 0f) return null;
            if (p.CurJob?.workGiverDef == null || IndoorRoom(p) != null || p.Position.Roofed(map)) return null;
            var worn = p.apparel?.WornApparel;
            if (worn == null) return null;
            var shell = ApparelLayerDefOf.Shell;
            foreach (var ap in worn)
                if (ap.def.apparel?.layers != null && ap.def.apparel.layers.Contains(shell)) return null;
            return CanMakeCoat() ? "" : null;
        }

        /// <summary>The colony knows how to make an outer layer (a duster, a parka, a cape): worked out once a day.</summary>
        private static bool CanMakeCoat()
        {
            int day = Find.TickManager.TicksGame / GenDate.TicksPerDay;
            if (day == lookedDay) return canMake;
            lookedDay = day;
            canMake = false;
            var shell = ApparelLayerDefOf.Shell;
            foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                ThingDef made;
                try
                {
                    made = r.ProducedThingDef;
                }
                catch
                {
                    continue;
                }
                if (made?.apparel?.layers == null || !made.apparel.layers.Contains(shell) || !r.AvailableNow) continue;
                canMake = true;
                break;
            }
            return canMake;
        }
    }

    /// <summary>Something ugly left lying in their bedroom (a stone chunk, slag, junk), while they're in it.</summary>
    public class NuisanceCheck_UglyBedroom : NuisanceCheckWorker
    {
        public override string Check(Pawn p, Map map)
        {
            var room = RoomUtil.BedRoom(p);
            if (room == null || !room.ProperRoom || room.PsychologicallyOutdoors || p.GetRoom() != room) return null;
            var things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count && i < MaxThingsLooked; i++)
            {
                var t = things[i];
                if (t.def.category != ThingCategory.Item || t is Corpse || t.GetRoom() != room) continue;
                if (t.GetStatValue(StatDefOf.Beauty) < -1f) return t.def.label;
            }
            return null;
        }
    }
}
