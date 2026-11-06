using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ---------------- The herd, the power, the plans, the walls, the snow and the faith ----------------
    //
    // Checks that go with the council's fourth round of questions and decrees (Dilemmas/DilemmaWorkers_YearFour.cs): hay
    // and kibble short of the herd's winter, animals out in a cold they can't bear, a generator running dry with no fuel in
    // the stores, plans waiting on stuff the stores don't hold, trees giving cover close outside the walls, deep snow with
    // nowhere set to clear, and the colony's faith without a moral guide. Each says why and what helps; the trees and the
    // snow have a button that does what it says (the same marks as the council's answers). They run once an hour with the
    // rest of the advisor; the thresholds are in YearFourMath.

    /// <summary>The hay and kibble in store won't see the herd through the winter ahead.</summary>
    public class AdvisorWorker_HerdFeed : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!Homes.IsHome(map) || MapFacts.InVacuum(map)) yield break;
            var herd = YearFour.Grazers(map);
            if (herd.Count < 3) yield break;
            float winter = YearFour.WinterAhead(map);
            if (winter <= 0f) yield break;
            float days = YearFourMath.FeedDays(YearFour.Feed(map), YearFour.HerdADay(herd));
            if (days >= winter) yield break;
            var s = Signal(days < winter * YearFourMath.FeedShort ? 0.5f : 0.32f, "CC_Adv_HerdFeed_Title".Translate().Resolve(),
                "CC_Adv_HerdFeed_Detail".Translate(herd.Count, Mathf.RoundToInt(days), Mathf.RoundToInt(winter)).Resolve(), 0.7f, 0.7f);
            s.look = new LookTargets(herd.Take(10));
            s.suggestThing = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Haygrass");
            s.key = "herdfeed";
            yield return s;
        }
    }

    /// <summary>Animals of the colony's in a cold they can't bear, outside or in an unheated barn.</summary>
    public class AdvisorWorker_HerdCold : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!Homes.IsHome(map) || MapFacts.InVacuum(map)) yield break;
            var cold = map.mapPawns.SpawnedColonyAnimals.Where(a => !a.Dead && a.RaceProps.Animal && !a.InContainerEnclosed
                                                                  && YearFourMath.TooCold(a.AmbientTemperature, a.GetStatValue(StatDefOf.ComfyTemperatureMin))).ToList();
            if (cold.Count < 2) yield break;
            var s = Signal(cold.Count >= 5 ? 0.45f : 0.3f, "CC_Adv_HerdCold_Title".Translate(cold.Count).Resolve(),
                "CC_Adv_HerdCold_Detail".Translate(TextUtil.JoinAnd(cold.Select(a => a.LabelShort).Take(4).ToList()), Mathf.RoundToInt(cold[0].AmbientTemperature)).Resolve(), 0.8f);
            s.look = new LookTargets(cold);
            s.key = "herdcold";
            yield return s;
        }
    }

    /// <summary>A generator of the colony's will run dry within half a day, and there's none of its fuel in the stores.</summary>
    public class AdvisorWorker_GeneratorFuel : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!Homes.IsHome(map)) yield break;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (b.def.IsFrame || b.TryGetComp<CompPowerPlant>() == null) continue;
                var fuel = b.TryGetComp<CompRefuelable>();
                var props = fuel?.Props;
                if (props?.fuelFilter == null || props.fuelConsumptionRate <= 0f || props.consumeFuelOnlyWhenUsed || !(b.TryGetComp<CompFlickable>()?.SwitchIsOn ?? true)) continue;
                float perHour = props.fuelConsumptionRate / GenDate.HoursPerDay;
                int inStores = 0;
                foreach (var kv in map.resourceCounter.AllCountedAmounts)
                    if (props.fuelFilter.Allows(kv.Key)) inStores += kv.Value;
                if (!YearFourMath.FuelLow(fuel.Fuel, perHour, inStores)) continue;
                var s = Signal(0.45f, "CC_Adv_GenFuel_Title".Translate(b.LabelNoCount).Resolve(),
                    "CC_Adv_GenFuel_Detail".Translate(b.LabelNoCount, Mathf.Max(0, Mathf.RoundToInt(fuel.Fuel / perHour))).Resolve(), 0.9f);
                s.look = new LookTargets(b);
                s.key = "genfuel:" + b.ThingID;
                yield return s;
                yield break;
            }
        }
    }

    /// <summary>Plans of the colony's that wait on stuff the stores don't hold: they stand until it's found or the plans are taken off.</summary>
    public class AdvisorWorker_UnpaidPlans : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!Homes.IsHome(map)) yield break;
            var plans = YearFour.UnpaidPlans(map);
            if (plans.Count < YearFourMath.Waiting) yield break;
            var missing = new Dictionary<ThingDef, int>();
            foreach (var b in plans)
            {
                try
                {
                    foreach (var cost in b.TotalMaterialCost())
                        if (cost?.thingDef != null && map.resourceCounter.GetCount(cost.thingDef) < cost.count)
                            missing[cost.thingDef] = (missing.TryGetValue(cost.thingDef, out int n) ? n : 0) + cost.count;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("advisor unpaid plans", e);
                }
            }
            if (missing.Count == 0) yield break;
            string what = TextUtil.JoinAnd(missing.OrderByDescending(kv => kv.Value).Take(3).Select(kv => kv.Key.label).ToList());
            var s = Signal(0.25f, "CC_Adv_Unpaid_Title".Translate(plans.Count).Resolve(), "CC_Adv_Unpaid_Detail".Translate(plans.Count, what).Resolve(), 0.4f);
            s.look = new LookTargets(plans.Take(20));
            s.suggestThing = missing.OrderByDescending(kv => kv.Value).First().Key;
            s.key = "unpaid";
            yield return s;
        }
    }

    /// <summary>Grown trees close outside the colony's walls: cover for anyone coming at them. One click marks them for the cutters.</summary>
    public class AdvisorWorker_TreeCover : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!Homes.IsHome(map)) yield break;
            // A colony whose faith holds trees sacred is never told to cut them down.
            if (Colonists(map).Any(p => Beliefs.Forbids(p, Belief.NoTreeCutting))) yield break;
            var dm = map.designationManager;
            var trees = YearFour.TreesByWallsLately(map).Where(p => dm.DesignationOn(p) == null).ToList();
            if (!YearFourMath.TreeLine(trees.Count)) yield break;
            var s = Signal(0.3f, "CC_Adv_TreeCover_Title".Translate(trees.Count).Resolve(), "CC_Adv_TreeCover_Detail".Translate(trees.Count).Resolve(), 0.5f, 0.7f);
            s.look = new LookTargets(trees.Cast<Thing>());
            s.act = () =>
            {
                foreach (var p in YearFour.TreesByWalls(map))
                    if (dm.DesignationOn(p) == null) FieldBlight.Mark(map, p, DesignationDefOf.CutPlant);
            };
            s.actLabel = "CC_Adv_TreeCover_Act".Translate();
            s.actTip = "CC_Adv_TreeCover_ActTip".Translate();
            s.key = "treecover";
            yield return s;
        }
    }

    /// <summary>Deep snow over the home area and nowhere set to clear it. One click marks the paths by the doors.</summary>
    public class AdvisorWorker_SnowPaths : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            var area = YearFour.SnowArea(map);
            if (!Homes.IsHome(map) || map.snowGrid == null || area == null || map.mapTemperature.OutdoorTemp > YearFourMath.SnowMelts) yield break;
            YearFour.Snow(map, out int open, out int deep);
            if (!YearFourMath.Snowed(deep, open, area.TrueCount)) yield break;
            var s = Signal(0.22f, "CC_Adv_Snow_Title".Translate().Resolve(), "CC_Adv_Snow_Detail".Translate(Mathf.RoundToInt(deep * 100f / Mathf.Max(1, open))).Resolve(), 0.5f);
            s.act = () =>
            {
                foreach (var c in DilemmaEffect_SnowArea.Cells(map, true)) area[c] = true;
            };
            s.actLabel = "CC_Adv_Snow_Act".Translate();
            s.actTip = "CC_Adv_Snow_ActTip".Translate();
            s.key = "snowpaths";
            yield return s;
        }
    }

    /// <summary>Ideology: nobody holds the moral guide's role in the colony's faith, though someone could.</summary>
    public class AdvisorWorker_NoGuide : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!Faiths.On || !Homes.IsHome(map)) yield break;
            var faith = DecreeWork.ColonyFaith;
            var role = YearFour.EmptyGuide(faith);
            if (role == null) yield break;
            var can = Colonists(map).Where(p => YearFour.CanGuide(role, p)).OrderByDescending(Faiths.Certainty).ToList();
            if (can.Count == 0) yield break;
            var s = Signal(0.22f, "CC_Adv_NoGuide_Title".Translate(role.LabelCap).Resolve(),
                "CC_Adv_NoGuide_Detail".Translate(role.LabelCap, faith.name, TextUtil.JoinAnd(can.Take(3).Select(p => p.LabelShort).ToList())).Resolve(), 0.3f);
            s.look = new LookTargets(can.Take(3));
            s.key = "noguide";
            yield return s;
        }
    }
}
