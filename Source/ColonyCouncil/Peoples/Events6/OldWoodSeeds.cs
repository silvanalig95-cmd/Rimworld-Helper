using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 53. Seeds of the old wood (the Mossborn) =====================

    /// <summary>
    /// Seeds of the old wood (item 53; the egg-trader's shape): an herb-trader of the Mossborn comes with seeds of a plant the
    /// colony doesn't grow, one it can sow in the ground now (its research done, its season here, its grower able to sow it),
    /// the player's plant mods first (Vanilla Plants Expanded and its add-ons, Alpha Biomes), the game's own after. Bought for
    /// silver or swapped for a share of the colony's own harvest, the trader sows a plot of four by four on open soil near the
    /// colony's fields with the colony's best grower beside them (round 1's visitor who works at a place), and the plot is the
    /// colony's growing zone of that plant from then on; the grower learns from the work.
    /// </summary>
    public static class OldWoodSeeds
    {
        public static JobDef SeedsJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv6_Seeds");

        private static ThingCategoryDef rawPlants;

        private static ThingCategoryDef RawPlants => rawPlants ??= DefDatabase<ThingCategoryDef>.GetNamedSilentFail("PlantFoodRaw");

        /// <summary>What the colony grows now: every growing zone's and plant pot's plant.</summary>
        private static HashSet<ThingDef> Grown(Map map)
        {
            var set = new HashSet<ThingDef>();
            foreach (var z in map.zoneManager.AllZones.OfType<IPlantToGrowSettable>())
                if (z.GetPlantDefToGrow() is ThingDef d) set.Add(d);
            foreach (var b in map.listerBuildings.allBuildingsColonist.OfType<IPlantToGrowSettable>())
                if (b.GetPlantDefToGrow() is ThingDef d) set.Add(d);
            return set;
        }

        private static string Package(ThingDef d) => d?.modContentPack?.PackageId?.ToLowerInvariant() ?? "";

        /// <summary>
        /// A plant the colony could take up: sown in the ground, not a tree or a cave plant, its research done and its season
        /// here, within the grower's skill, and not grown by the colony now.
        /// </summary>
        public static bool Fits(ThingDef d, Map map, Pawn grower, HashSet<ThingDef> grown)
        {
            var p = d?.plant;
            if (p == null || p.IsTree || p.cavePlant || !p.Sowable || p.sowTags == null || !p.sowTags.Contains("Ground") || grown.Contains(d)) return false;
            if (p.sowResearchPrerequisites != null && p.sowResearchPrerequisites.Any(r => r != null && !r.IsFinished)) return false;
            if (grower?.skills?.GetSkill(SkillDefOf.Plants) is SkillRecord s && s.Level < p.sowMinSkill) return false;
            try
            {
                return PlantUtility.GrowthSeasonNow(map, d);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>The seeds they bring: the player's plant mods' first, else the game's own; null when nothing fits.</summary>
        public static ThingDef Pick(Map map, Pawn grower)
        {
            if (map == null || grower == null) return null;
            var grown = Grown(map);
            var all = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => Fits(d, map, grower, grown)).ToList();
            if (all.Count == 0) return null;
            var mods = all.Where(d => PeopleEvents6Math.SeedPackages.Contains(Package(d))).ToList();
            return (mods.Count > 0 ? mods : all).RandomElement();
        }

        /// <summary>What one cell's first harvest is worth, roughly.</summary>
        public static float YieldValue(ThingDef plant) => plant?.plant?.harvestedThingDef == null ? 0f : plant.plant.harvestYield * plant.plant.harvestedThingDef.BaseMarketValue;

        private static bool PlotCell(Map map, IntVec3 c, ThingDef plant)
        {
            if (!c.InBounds(map) || c.Fogged(map) || c.Roofed(map) || c.GetZone(map) != null || c.GetEdifice(map) != null || c.GetFirstBuilding(map) != null || c.GetFirstItem(map) != null) return false;
            var t = c.GetTerrain(map);
            if (t == null || t.IsWater || t.fertility < Mathf.Max(0.7f, plant.plant.fertilityMin)) return false;
            if (c.GetPlant(map) is Plant p && p.def.plant.IsTree) return false;
            return plant.CanEverPlantAt(c, map, true, true);
        }

        /// <summary>
        /// The plot: four by four on open soil (three by three where that won't fit), near the colony's fields, else its middle,
        /// out of every zone and building; its corner and side, or Invalid.
        /// </summary>
        public static IntVec3 Plot(Map map, ThingDef plant, Pawn grower, out int side)
        {
            side = 0;
            if (map == null || plant?.plant == null) return IntVec3.Invalid;
            var mid = CampSite.ColonyCenter(map);
            var field = map.zoneManager.AllZones.OfType<Zone_Growing>().SelectMany(z => z.Cells).OrderBy(c => c.DistanceToSquared(mid)).FirstOrDefault();
            var anchor = field.IsValid && field != IntVec3.Zero ? field : mid;
            if (!anchor.IsValid) return IntVec3.Invalid;
            foreach (int s in new[] { PeopleEvents6Math.PlotSide, PeopleEvents6Math.PlotSmall })
                foreach (var corner in GenRadial.RadialCellsAround(anchor, 30f, true))
                {
                    var rect = new CellRect(corner.x, corner.z, s, s);
                    if (!rect.Cells.All(c => PlotCell(map, c, plant))) continue;
                    if (grower != null && !grower.CanReach(rect.CenterCell, PathEndMode.Touch, Danger.Some)) continue;
                    side = s;
                    return corner;
                }
            return IntVec3.Invalid;
        }

        /// <summary>The colony's own harvest to swap: the raw plant food it has most of in its stores, and how much of it.</summary>
        public static ThingDef Harvest(Map map, int price, out int count)
        {
            count = 0;
            if (map == null || RawPlants == null) return null;
            var best = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                .Where(t => t.Spawned && t.def.thingCategories != null && t.def.thingCategories.Contains(RawPlants) && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage())
                .GroupBy(t => t.def).Select(g => (def: g.Key, have: g.Sum(t => t.stackCount))).OrderByDescending(g => g.have).FirstOrDefault();
            if (best.def == null) return null;
            count = PeopleEvents6Math.SwapCount(price, best.def.BaseMarketValue);
            return count > 0 && best.have >= count ? best.def : null;
        }

        private static string Note(ThingDef plant, IntVec3 corner, int side) =>
            plant.defName + "|" + corner.x.ToString(CultureInfo.InvariantCulture) + "," + corner.z.ToString(CultureInfo.InvariantCulture) + "|" + side.ToString(CultureInfo.InvariantCulture);

        private static bool Parse(string note, out ThingDef plant, out CellRect rect)
        {
            plant = null;
            rect = CellRect.Empty;
            var bits = (note ?? "").Split('|');
            if (bits.Length < 3) return false;
            plant = DefDatabase<ThingDef>.GetNamedSilentFail(bits[0]);
            var xz = bits[1].Split(',');
            if (plant == null || xz.Length < 2 || !int.TryParse(xz[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) || !int.TryParse(xz[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int z)
                || !int.TryParse(bits[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) || s <= 0) return false;
            rect = new CellRect(x, z, s, s);
            return true;
        }

        /// <summary>The bargain made: the trader and the grower walk to the plot, the trader sows it with the grower beside them.</summary>
        public static void Begin(HappeningContext ctx, bool swap)
        {
            var map = ctx?.map;
            var x = ctx?.stranger;
            if (map == null || x == null || ctx.a == null || ctx.thing == null) return;
            var corner = Plot(map, ctx.thing, ctx.a, out int side);
            if (!corner.IsValid)
            {
                Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_PEv6_SeedsNoPlot".Translate(x.LabelShort.Named("X")).Resolve(), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                // Their price given back: the silver by their courier (a swap's harvest stays theirs, as a trader's goods do).
                if (!swap) Couriers.PaySilver(map, x.Spawned ? x.Position : CampSite.ColonyCenter(map), Mathf.Max(0, ctx.count), ctx.faction, x);
                PeoplesG.Home(PeoplesG.Party(ctx));
                return;
            }
            var rect = new CellRect(corner.x, corner.z, side, side);
            var job = SeedsJob;
            if (job == null || !PeopleWorkOn.Begin(ctx, x, ctx.a, job, rect.CenterCell, Note(ctx.thing, corner, side), 0, PeopleEvents6Math.SowTicks, 8f))
                Sown(x, ctx.a, ctx.incident, Note(ctx.thing, corner, side), map);
        }

        /// <summary>
        /// The plot sown: each of its cells planted (what grew wild there cut), the cells made the colony's growing zone of that
        /// plant; the grower's lesson; told.
        /// </summary>
        public static void Sown(Pawn trader, Pawn grower, IncidentDef incident, string note, Map map)
        {
            if (map == null || !Parse(note, out var plant, out var rect)) return;
            int n = 0;
            Zone_Growing zone = null;
            try
            {
                foreach (var c in rect.Cells)
                {
                    if (!c.InBounds(map) || c.GetZone(map) != null || !plant.CanEverPlantAt(c, map, true, true)) continue;
                    if (c.GetPlant(map) is Plant old)
                    {
                        if (old.def.plant.IsTree) continue;
                        old.Destroy();
                    }
                    var p = (Plant)ThingMaker.MakeThing(plant);
                    p.Growth = 0.05f;
                    p.sown = true;
                    if (!GenPlace.TryPlaceThing(p, c, map, ThingPlaceMode.Direct)) continue;
                    if (zone == null)
                    {
                        zone = new Zone_Growing(map.zoneManager);
                        map.zoneManager.RegisterZone(zone);
                    }
                    zone.AddCell(c);
                    n++;
                }
                zone?.SetPlantDefToGrow(plant);
                grower?.skills?.Learn(SkillDefOf.Plants, PeopleEvents6Math.GrowerXp, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 6: the old wood's plot", e);
            }
            if (n == 0) return;
            var f = trader?.Faction;
            if (PeopleEvents6.Thanks(f)) WorldSituationWorker.Goodwill(f, PeopleEvents6Math.SeedGoodwill, GoodwillWhy.TheirWork);
            if (trader != null) PeoplesG.SayLine(trader, incident, "sown");
            FleckMaker.ThrowDustPuff(rect.CenterCell, map, 1f);
            Tell.Message(NewsKind.Land, Importance.Notable, map,
                "CC_PEv6_SeedsSown".Translate((trader?.LabelShort ?? "?").Named("X"), (grower?.LabelShort ?? "?").Named("PAWN"), plant.label.Named("PLANT"), n.Named("COUNT")).Resolve(),
                new LookTargets(rect.CenterCell, map), MessageTypeDefOf.PositiveEvent);
            if (trader != null) PeoplesG.Home(new[] { trader });
        }
    }

    /// <summary>
    /// An herb-trader of {FACTION} ({X}) at the gate with seeds of {TALE}, which the colony doesn't grow: for {COUNT} silver, or
    /// {RELATION} of its harvest, they would sow a plot of it with {A}, the colony's best grower.
    /// </summary>
    public class HappeningWorker_OldWoodSeeds : HappeningWorker_Visitors
    {
        private static Pawn Grower(Map map) =>
            PeoplesD.Best(map, SkillDefOf.Plants, p => !p.DevelopmentalStage.Juvenile() && p.skills?.GetSkill(SkillDefOf.Plants)?.TotallyDisabled == false && !p.WorkTypeIsDisabled(WorkTypeDefOf.Growing));

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (PeoplesD.Friendly(PeoplesMath.Grovekeepers) == null || OldWoodSeeds.SeedsJob == null || map.dangerWatcher.DangerRating != StoryDanger.None) return false;
            var g = Grower(map);
            var plant = OldWoodSeeds.Pick(map, g);
            return g != null && plant != null && OldWoodSeeds.Plot(map, plant, g, out _).IsValid;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Grovekeepers);
            ctx.a = Grower(ctx.map);
            ctx.thing = OldWoodSeeds.Pick(ctx.map, ctx.a);
            if (ctx.faction == null || ctx.a == null || ctx.thing == null || !OldWoodSeeds.Plot(ctx.map, ctx.thing, ctx.a, out int side).IsValid) return false;
            ctx.tale = ctx.thing.label;
            ctx.variant = side * side;
            ctx.count = PeopleEvents6Math.SeedPrice(ctx.variant, OldWoodSeeds.YieldValue(ctx.thing));
            var harvest = OldWoodSeeds.Harvest(ctx.map, ctx.count, out int n);
            ctx.relation = harvest != null ? VarietyUtil.Bundle(harvest, n) : "";
            ctx.note = harvest != null ? harvest.defName + "|" + n.ToString(CultureInfo.InvariantCulture) : null;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Mossborn_HerbTrader"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Plants, new IntRange(10, 16));
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            var purpose = ctx.thing?.plant?.purpose;
            tags.Add(purpose == PlantPurpose.Food ? "food" : purpose == PlantPurpose.Health ? "health" : purpose == PlantPurpose.Beauty ? "beauty" : "other");
            if (ctx.thing != null && PeopleEvents6Math.SeedPackages.Contains(ctx.thing.modContentPack?.PackageId?.ToLowerInvariant() ?? "")) tags.Add("mod");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.a == null || ctx.a.Dead || !ctx.a.Spawned || ctx.a.Map != ctx.map) return "CC_PEv6_GrowerGone".Translate().Resolve();
            if (option == 1 && ctx.note.NullOrEmpty()) return "CC_PEv6_NoHarvest".Translate().Resolve();
            if (!OldWoodSeeds.Plot(ctx.map, ctx.thing, ctx.a, out _).IsValid) return "CC_PEv6_NoPlot".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "seeds" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The plot sown with the grower beside them (OldWoodSeeds.Begin); with <see cref="swap"/>, the harvest share carried out first.</summary>
    public class DilemmaEffect_OldWoodSeeds : DilemmaEffect
    {
        public bool swap;

        public override bool ChanceEach => false;

        private static DilemmaEffect_CarryOut Carry(DilemmaContext ctx)
        {
            var bits = (ctx as HappeningContext)?.note?.Split('|');
            if (bits == null || bits.Length < 2 || !int.TryParse(bits[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) return null;
            return new DilemmaEffect_CarryOut { what = bits[0], count = n, steward = false, label = "CC_PEv6_HarvestCarried".Translate().Resolve() };
        }

        public override string Unavailable(DilemmaContext ctx) => swap ? Carry(ctx) == null ? "CC_PEv6_NoHarvest".Translate().Resolve() : Carry(ctx).Unavailable(ctx) : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null || hc.thing == null) return;
            if (swap) Carry(ctx)?.Describe(ctx, sheet);
            sheet.Play("CC_PEv6_Impact_Seeds".Translate(hc.stranger?.LabelShort ?? "?", hc.variant, hc.thing.label, hc.a.LabelShort).Resolve(), "CC_PEv6_Impact_SeedsBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Skill, 1, "CC_PEv6_Impact_SeedsLesson".Translate(hc.a.LabelShort).Resolve(), "CC_PEv6_Impact_SeedsLessonBrief".Translate().Resolve(), "pev6seeds");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents6Math.SeedGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            if (swap) Carry(ctx)?.Apply(ctx);
            OldWoodSeeds.Begin(hc, swap);
        }
    }

    /// <summary>The plot sown (OldWoodSeeds.Sown).</summary>
    public class OldWoodSeedsDone : PeopleWorkDone
    {
        public override void Done(Visit v, Pawn worker, Pawn on) => OldWoodSeeds.Sown(worker, on, v?.incident, PeopleWorkOn.Note(v), v?.map ?? worker?.Map);

        public override void GaveUp(Visit v)
        {
            if (v?.map == null) return;
            Tell.Message(NewsKind.Land, Importance.Notable, v.map, "CC_PEv_WorkGivenUp".Translate((v.x?.LabelShort ?? "?").Named("WORKER"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }
    }
}
