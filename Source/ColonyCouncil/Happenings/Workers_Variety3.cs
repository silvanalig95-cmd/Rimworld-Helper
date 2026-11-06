using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Rimfolk events of the third round that grow out of what is on the map (Defs/IncidentDefs/Rimfolk_Events_Variety3.xml):
    // a wild one on the colony's land (the game's wild man or woman), a stretch of the pen's fence down after a storm, the
    // wild ambrosia ripe, and a clutch of a wild bird's eggs found in the grass. Each answer has its way both ways, and
    // each plays out with the game's own: taming, a blueprint, harvest marks, an egg's own hatching.

    // ===================== The wild one =====================

    /// <summary>
    /// A wild man or woman wanders the colony's land (the game's own, who can be tamed by a handler), calm, nobody's. X is
    /// the wild one, A the colony's best handler, B someone else of the colony, the warier the better.
    /// </summary>
    public class HappeningWorker_WildOne : IncidentWorker_Happening
    {
        public static Pawn WildOne(Map map) =>
            map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.IsWildMan() && !p.Dead && !p.Downed && !p.InMentalState && p.Faction == null && !p.Position.Fogged(map)
                                                             && TameUtility.CanTame(p) && map.designationManager.DesignationOn(p) == null);

        private static Pawn Handler(Map map) =>
            StorytellerEvents.Adults(map).Where(p => VarietyThree.Does(p, WorkTypeDefOf.Handling)).OrderByDescending(p => VarietyThree.Level(p, SkillDefOf.Animals)).FirstOrDefault();

        protected override bool CanHappen(Map map, HappeningExtension ext) => !MapFacts.ThreatOn(map) && WildOne(map) != null && Handler(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.stranger = WildOne(ctx.map);
            ctx.a = Handler(ctx.map);
            if (ctx.stranger == null || ctx.a == null) return false;
            ctx.b = StorytellerEvents.Adults(ctx.map).Where(p => p != ctx.a).OrderBy(p => p.relations?.OpinionOf(ctx.a) ?? 0).FirstOrDefault();
            return ctx.b != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => Lands.AddTags(ctx.map, tags);
    }

    // ===================== The fence down =====================

    /// <summary>The colony's pens with animals in them, and the fence round them.</summary>
    public static class Pens
    {
        public static IEnumerable<CompAnimalPenMarker> Of(Map map) =>
            map.listerBuildings.allBuildingsColonist.Select(b => b.TryGetComp<CompAnimalPenMarker>()).Where(m => m != null && m.parent.Spawned);

        public static List<Pawn> Animals(Map map, CompAnimalPenMarker pen)
        {
            var regions = pen.PenState?.ConnectedRegions;
            if (regions == null) return new List<Pawn>();
            return map.mapPawns.SpawnedColonyAnimals.Where(a => !a.Dead && a.GetRegion() is Region r && regions.Contains(r)).ToList();
        }

        private static bool Inside(IntVec3 c, Map map, HashSet<Region> regions) => c.InBounds(map) && c.GetRegion(map) is Region r && regions.Contains(r);

        /// <summary>A piece of the pen's own fence with the pen on one side and open ground on the other.</summary>
        public static Building Edge(Map map, CompAnimalPenMarker pen)
        {
            var regions = pen.PenState?.ConnectedRegions;
            if (regions == null) return null;
            var fences = map.listerBuildings.allBuildingsColonist.Where(b => b.Spawned && b.def.IsFence && b.Position.DistanceTo(pen.parent.Position) <= 40f);
            foreach (var f in fences.InRandomOrder())
            {
                bool inner = false, outer = false;
                foreach (var d in GenAdj.CardinalDirections)
                {
                    var c = f.Position + d;
                    if (!c.InBounds(map)) continue;
                    if (Inside(c, map, regions)) inner = true;
                    else if (c.Standable(map) && c.GetEdifice(map) == null) outer = true;
                }
                if (inner && outer) return f;
            }
            return null;
        }

        /// <summary>The pen to break: closed, with two of the colony's animals in it at least, and a fence with open ground past it.</summary>
        public static bool TryFind(Map map, out CompAnimalPenMarker pen, out Building fence)
        {
            pen = null;
            fence = null;
            foreach (var p in Of(map).InRandomOrder())
            {
                if (p.PenState == null || !p.PenState.Enclosed || Animals(map, p).Count < 2) continue;
                var f = Edge(map, p);
                if (f == null) continue;
                pen = p;
                fence = f;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// After a storm, a stretch of the fence round one of the colony's pens is down, and the animals in it could walk out.
    /// A is the colony's best handler, B its best builder. {THING}: the fence's kind; the spot is where it stood.
    /// </summary>
    public class HappeningWorker_FenceDown : IncidentWorker_Happening
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => !MapFacts.ThreatOn(map) && Pens.TryFind(map, out _, out _);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!Pens.TryFind(ctx.map, out var pen, out var fence)) return false;
            var adults = StorytellerEvents.Adults(ctx.map).ToList();
            ctx.a = VarietyThree.Best(adults, SkillDefOf.Animals, WorkTypeDefOf.Handling) ?? adults.FirstOrDefault();
            ctx.b = VarietyThree.Best(adults, SkillDefOf.Construction, WorkTypeDefOf.Construction, ctx.a) ?? VarietyThree.Other(adults, ctx.a, SkillDefOf.Construction);
            if (ctx.a == null || ctx.b == null) return false;
            var herd = Pens.Animals(ctx.map, pen);
            ctx.count = herd.Count;
            ctx.pet = herd.OrderByDescending(x => x.BodySize).First();
            ctx.beast = ctx.pet.kindDef.GetLabelPlural();
            ctx.thing = fence.def;
            ctx.spot = fence.Position;
            ctx.note = fence.Stuff?.defName;
            ctx.variant = fence.Rotation.AsInt;
            return true;
        }

        /// <summary>The fence comes down: gone from where it stood, its splinters and some of its stuff on the ground.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var fence = ctx.spot.GetFirstThing(map, ctx.thing);
            if (fence == null) return false;
            var stuff = fence.Stuff;
            fence.Destroy(DestroyMode.Vanish);
            FilthMaker.TryMakeFilth(ctx.spot, map, ThingDefOf.Filth_RubbleBuilding, 2);
            FleckMaker.ThrowDustPuff(ctx.spot, map, 1.2f);
            if (stuff != null && Rand.Chance(0.6f)) StorytellerEvents.Hand(stuff, Rand.RangeInclusive(1, 2), map, ctx.spot);
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.count >= 6) tags.Add("big");
        }
    }

    /// <summary>
    /// The fence rebuilt where it stood: a blueprint of the same fence in the same stuff, for the builders, and the
    /// handlers bring the animals back once the pen is closed again (the game's own). Can't be chosen when something
    /// stands in the way.
    /// </summary>
    public class DilemmaEffect_MendFence : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private static ThingDef Stuff(HappeningContext h) => h?.note.NullOrEmpty() != false ? null : DefDatabase<ThingDef>.GetNamedSilentFail(h.note);

        private static bool CanPlace(HappeningContext h) =>
            h?.thing != null && h.map != null && h.spot.IsValid && GenConstruct.CanPlaceBlueprintAt(h.thing, h.spot, new Rot4(h.variant), h.map, false, null, null, Stuff(h)).Accepted;

        public override string Unavailable(DilemmaContext ctx) =>
            ctx is HappeningContext h && h.thing != null && h.map != null && !CanPlace(h) ? "CC_Evt_FenceBlocked".Translate().ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!(ctx is HappeningContext h) || !CanPlace(h)) return;
            sheet.Play("CC_Impact_MendFence".Translate(h.thing.label).Resolve(), "CC_Impact_MendFenceBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (!(ctx is HappeningContext h) || !CanPlace(h)) return;
            try
            {
                GenConstruct.PlaceBlueprintForBuild(h.thing, h.spot, h.map, new Rot4(h.variant), Faction.OfPlayer, Stuff(h));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fence mended", e);
            }
        }
    }

    // ===================== The ambrosia =====================

    /// <summary>
    /// Wild ambrosia ripe on the colony's land (the game's own, which springs up where it likes), three bushes at least.
    /// A is the colony's best grower, B someone who'd sooner it were left: one who never touches anything of the kind, else
    /// the colony's doctor. {COUNT}: the ripe bushes.
    /// </summary>
    public class HappeningWorker_Ambrosia : IncidentWorker_Happening
    {
        public static List<Plant> Ripe(Map map)
        {
            var def = ThingDefOf.Plant_Ambrosia;
            if (def == null) return new List<Plant>();
            return map.listerThings.ThingsOfDef(def).OfType<Plant>().Where(p => p.Spawned && p.HarvestableNow && !p.Position.Fogged(map) && map.designationManager.DesignationOn(p) == null).ToList();
        }

        private static bool Teetotal(Pawn p) => p.story?.traits?.GetTrait(TraitDefOf.DrugDesire) is Trait t && t.Degree < 0;

        protected override bool CanHappen(Map map, HappeningExtension ext) => VarietyThreeMath.AmbrosiaWorth(Ripe(map).Count) && StorytellerEvents.Adults(map).Count() >= 2;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var ripe = Ripe(ctx.map);
            if (!VarietyThreeMath.AmbrosiaWorth(ripe.Count)) return false;
            var adults = StorytellerEvents.Adults(ctx.map).ToList();
            ctx.a = VarietyThree.Best(adults, SkillDefOf.Plants, WorkTypeDefOf.PlantCutting) ?? adults.FirstOrDefault();
            ctx.b = adults.FirstOrDefault(p => p != ctx.a && Teetotal(p)) ?? VarietyThree.Best(adults, SkillDefOf.Medicine, WorkTypeDefOf.Doctor, ctx.a) ?? VarietyThree.Other(adults, ctx.a, SkillDefOf.Medicine);
            if (ctx.a == null || ctx.b == null) return false;
            ctx.count = ripe.Count;
            ctx.thing = ThingDefOf.Plant_Ambrosia;
            ctx.place = ripe.OrderBy(p => p.Position.DistanceToSquared(StorytellerEvents.Center(ctx.map))).First();
            ctx.placeName = StorytellerEvents.Direction(ctx.map, ctx.place.Position);
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.b != null && Teetotal(ctx.b)) tags.Add("teetotal");
        }
    }

    /// <summary>
    /// Ripe wild ambrosia marked for harvest (the game's own designation, picked by the plant cutters): all of it, or with
    /// <see cref="night"/> as many as the colony has people. Names the game's own pull of it before the answer.
    /// </summary>
    public class DilemmaEffect_PickAmbrosia : DilemmaEffect
    {
        public bool night;

        public override bool ChanceEach => false;

        private int Count(DilemmaContext ctx, int ripe) => night ? VarietyThreeMath.AmbrosiaForANight(ripe, ctx.Colonists.Count()) : ripe;

        public override string Unavailable(DilemmaContext ctx) =>
            ctx?.map != null && HappeningWorker_Ambrosia.Ripe(ctx.map).Count == 0 ? "CC_Evt_AmbrosiaGone".Translate().ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null || HappeningWorker_Ambrosia.Ripe(ctx.map).Count == 0) return;
            sheet.Play((night ? "CC_Impact_AmbrosiaNight" : "CC_Impact_AmbrosiaAll").Translate().Resolve(), (night ? "CC_Impact_AmbrosiaNightBrief" : "CC_Impact_AmbrosiaAllBrief").Translate().Resolve());
            sheet.Add(ImpactKind.Health, -1, "CC_Impact_AmbrosiaPull".Translate().Resolve(), "CC_Impact_AmbrosiaPullBrief".Translate().Resolve(), "ambrosiapull");
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.map == null) return;
            try
            {
                var ripe = HappeningWorker_Ambrosia.Ripe(ctx.map);
                var center = StorytellerEvents.Center(ctx.map);
                var marked = ripe.OrderBy(p => p.Position.DistanceToSquared(center)).Take(Count(ctx, ripe.Count)).ToList();
                foreach (var p in marked) FieldBlight.Mark(ctx.map, p, DesignationDefOf.HarvestPlant);
                if (marked.Count > 0)
                    Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map, "CC_Evt_AmbrosiaMarked".Translate(marked.Count.Named("N")).Resolve(), new LookTargets(marked.Cast<Thing>()), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ambrosia marked", e);
            }
        }
    }

    // ===================== A clutch in the grass =====================

    /// <summary>
    /// A clutch of a wild bird's eggs found in the grass (the game's own fertilized eggs, of a bird that lives on this land),
    /// set down where they were found. {BEAST}: the bird, {COUNT}: the eggs; A found them, B the colony's best handler.
    /// </summary>
    public class HappeningWorker_Clutch : IncidentWorker_Happening
    {
        /// <summary>A fertilized egg of a bird that lives wild on this land, by the game's own hatching.</summary>
        public static ThingDef EggOf(Map map)
        {
            var wild = new HashSet<PawnKindDef>(map.Biome?.AllWildAnimals ?? Enumerable.Empty<PawnKindDef>());
            return DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.GetCompProperties<CompProperties_Hatcher>()?.hatcherPawn is PawnKindDef k && wild.Contains(k))
                .RandomElementWithFallback();
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => EggOf(map) != null && StorytellerEvents.Adults(map).Count() >= 2 && StorytellerEvents.TryOpenSpot(map, 10f, 35f, out _);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var egg = EggOf(ctx.map);
            if (egg == null || !StorytellerEvents.TryOpenSpot(ctx.map, 10f, 35f, out var spot)) return false;
            var adults = StorytellerEvents.Adults(ctx.map).ToList();
            ctx.a = adults.Where(p => VarietyThree.Does(p, WorkTypeDefOf.Growing) || VarietyThree.Does(p, WorkTypeDefOf.Hauling)).InRandomOrder().FirstOrDefault() ?? adults.FirstOrDefault();
            ctx.b = VarietyThree.Best(adults, SkillDefOf.Animals, WorkTypeDefOf.Handling, ctx.a) ?? VarietyThree.Other(adults, ctx.a, SkillDefOf.Animals);
            if (ctx.a == null || ctx.b == null) return false;
            ctx.thing = egg;
            ctx.beast = egg.GetCompProperties<CompProperties_Hatcher>().hatcherPawn.label;
            ctx.count = Rand.RangeInclusive(VarietyThreeMath.EggsLow, VarietyThreeMath.EggsHigh);
            ctx.spot = spot;
            ctx.placeName = StorytellerEvents.Direction(ctx.map, spot);
            return true;
        }

        /// <summary>The eggs set down where they were found, nobody's yet: kept from the haulers until the colony says.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var eggs = ThingMaker.MakeThing(ctx.thing);
            eggs.stackCount = Mathf.Clamp(ctx.count, 1, ctx.thing.stackLimit);
            if (!GenPlace.TryPlaceThing(eggs, ctx.spot, ctx.map, ThingPlaceMode.Near, out var placed)) return false;
            placed.SetForbidden(true, false);
            var hatcher = placed.TryGetComp<CompHatcher>();
            if (hatcher != null) hatcher.hatcheeFaction = null;
            ctx.place = placed;
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => Lands.AddTags(ctx.map, tags);
    }

    /// <summary>
    /// What becomes of the clutch: <see cref="raise"/>, kept for the colony (its young the colony's when they hatch, the
    /// game's own hatching, carried in by the haulers); else for the kitchen (the same eggs, but none to hatch: the
    /// unfertilized kind of the same bird, for the cooks). Can't be chosen once the eggs are gone, or for the kitchen when the
    /// bird has no eggs of that kind.
    /// </summary>
    public class DilemmaEffect_Clutch : DilemmaEffect
    {
        public bool raise;

        public override bool ChanceEach => false;

        private static Thing Eggs(DilemmaContext ctx) => (ctx as HappeningContext)?.place is Thing t && t.Spawned && t.Map == ctx.map ? t : null;

        private static ThingDef Plain(ThingDef egg) => egg == null ? null : DefDatabase<ThingDef>.GetNamedSilentFail(egg.defName.Replace("Fertilized", "Unfertilized"));

        public override string Unavailable(DilemmaContext ctx)
        {
            if (!(ctx is HappeningContext h) || h.place == null) return null;
            var eggs = Eggs(ctx);
            if (eggs == null) return "CC_Evt_EggsGone".Translate().ToString();
            return !raise && Plain(eggs.def) == null ? "CC_Evt_EggsNotFood".Translate().ToString() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var eggs = Eggs(ctx);
            if (eggs == null) return;
            string bird = (ctx as HappeningContext)?.beast ?? "";
            sheet.Play((raise ? "CC_Impact_ClutchRaise" : "CC_Impact_ClutchKitchen").Translate(bird).Resolve(), (raise ? "CC_Impact_ClutchRaiseBrief" : "CC_Impact_ClutchKitchenBrief").Translate(bird).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var eggs = Eggs(ctx);
            if (eggs == null) return;
            try
            {
                if (raise)
                {
                    var hatcher = eggs.TryGetComp<CompHatcher>();
                    if (hatcher != null) hatcher.hatcheeFaction = Faction.OfPlayer;
                    eggs.SetForbidden(false, false);
                    return;
                }
                var plain = Plain(eggs.def);
                if (plain != null)
                {
                    var at = eggs.Position;
                    var map = eggs.Map;
                    int n = eggs.stackCount;
                    eggs.Destroy();
                    var food = ThingMaker.MakeThing(plain);
                    food.stackCount = Mathf.Clamp(n, 1, plain.stackLimit);
                    GenPlace.TryPlaceThing(food, at, map, ThingPlaceMode.Near);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("clutch", e);
            }
        }
    }
}
