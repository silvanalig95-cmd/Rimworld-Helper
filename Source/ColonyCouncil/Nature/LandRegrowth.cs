using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// B1, land that recovers (docs/design/living-land-land.md): ground a fire burned, a wood the colony felled, trees a wilt
    /// killed and grass a rust killed come back in stages over the land's growing days. The plants lost are gathered into
    /// patches as they go (a fire's, Patch_LandBurnt; the colony's felling, Patch_LandFelled; the sicknesses',
    /// LandSickness); once a day the pass counts each patch's growing day and, as it comes to each stage, sets the land's own
    /// wild plants on the bare ground of it: grass first, then brush, then young trees of the kind that stood there (after a
    /// wilt, of other kinds), until it is woods again (or grass and brush, where no wood stood). The game's own wild plants
    /// come up there as well, as always. Nothing is set in the home area, a zone, on a floor, under a roof or next to a
    /// building; a patch the colony builds over keeps only its open ground. The news says each stage as it comes; a felled
    /// wood grown back is told to the Mossborn, and a believer may take a wood come back for Hollin's doing.
    /// </summary>
    public static class LandRegrowth
    {
        public static bool On => (ColonyCouncilMod.Settings?.landRecovers ?? true);

        // ================= Plants lost (the game's hooks) =================

        /// <summary>A plant burned to death (Patch_LandBurnt): a burn's patch.</summary>
        public static void Burnt(Plant p)
        {
            if (!On || p == null || !p.Spawned || p.def?.plant == null || p.def.plant.isStump) return;
            var map = p.Map;
            if (!map.IsPlayerHome) return;
            LandBook.Of(map)?.Lost(LandCause.Burn, p.Position, p.def.plant.IsTree, p.def);
        }

        /// <summary>A tree felled by one of the colony's people (Patch_LandFelled): a felled wood's patch, outside the colony's fields.</summary>
        public static void Felled(Plant p)
        {
            if (!On || p == null || !p.Spawned || p.def?.plant == null || !p.def.plant.IsTree || p.sown) return;
            var map = p.Map;
            if (!map.IsPlayerHome || p.Position.GetZone(map) is Zone_Growing) return;
            LandBook.Of(map)?.Lost(LandCause.Felled, p.Position, true, p.def);
        }

        // ================= The day's pass =================

        internal static void Pass(Map map, LandBook b)
        {
            int today = b.Today;
            bool growing = LandBook.GrowingToday(map);
            int counting = b.Patches.Count(p => p.Counts);
            int each = LandMath.CellsEach(counting);
            foreach (var p in b.Patches.ToList())
            {
                if (LandMath.Stale(p, today))
                {
                    b.Patches.Remove(p);
                    continue;
                }
                if (!p.Counts) continue;
                if (!LandMath.CountDay(p, today, growing) || !On) continue;
                Advance(map, b, p, each);
            }
        }

        /// <summary>
        /// A patch's growing day: done once it is (woods again, or grass and brush), else the stage's plants set; a new stage
        /// is told the first day it sets something (a patch built over and set with nothing is never told of).
        /// </summary>
        private static void Advance(Map map, LandBook b, LandPatch p, int cells)
        {
            var stage = LandMath.StageFor(p.growDays, p.Wood);
            p.stage = stage;
            if (LandMath.Closed(p.growDays, p.Wood))
            {
                Close(map, p);
                b.Patches.Remove(p);
                return;
            }
            Seed(map, p, stage, cells, out int own);
            if (own > 0 && (int)stage > p.told)
            {
                p.told = (int)stage;
                Stage(map, p);
            }
        }

        /// <summary>
        /// The bare ground of a patch: in the map, seen, open to the sky, natural ground that grows things, nothing on it but
        /// filth, no zone, no mark, outside the home area.
        /// </summary>
        public static bool Bare(Map map, IntVec3 c)
        {
            if (!c.InBounds(map) || c.Fogged(map) || c.Roofed(map) || map.areaManager.Home[c] || c.GetZone(map) != null) return false;
            var t = c.GetTerrain(map);
            if (t == null || !t.natural || t.IsWater || t.fertility <= 0f || map.terrainGrid.FoundationAt(c) != null) return false;
            var things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i].def.category != ThingCategory.Filth)
                    return false;
            return map.designationManager.AllDesignationsAt(c).Count == 0 && PlantUtility.SnowAllowsPlanting(c, map);
        }

        /// <summary>A young tree here would stand too close to a building or another tree.</summary>
        private static bool Crowded(Map map, IntVec3 c)
        {
            foreach (var x in GenRadial.RadialCellsAround(c, 2f, false))
            {
                if (!x.InBounds(map)) continue;
                if (x.GetEdifice(map) != null) return true;
                if (x.GetPlant(map) is Plant p && p.def.plant.IsTree) return true;
            }
            return false;
        }

        /// <summary>The pass's plants set in a patch at its stage, on bare ground sampled in it; <paramref name="own"/> of them the stage's own (grass, brush or young trees).</summary>
        internal static int Seed(Map map, LandPatch p, LandStage stage, int cells, out int own)
        {
            int want = LandMath.Seeds(stage), set = 0;
            own = 0;
            if (want <= 0) return 0;
            int ownClass = stage == LandStage.Saplings ? LandPlants.Tree : stage == LandStage.Brush ? LandPlants.Brush : LandPlants.Grass;
            var tree = p.tree.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(p.tree);
            for (int i = 0; i < cells && set < want; i++)
            {
                var c = new IntVec3(Rand.RangeInclusive(p.x0, p.x1), 0, Rand.RangeInclusive(p.z0, p.z1));
                if (!Bare(map, c)) continue;
                int cls = LandMath.Pick(stage, Rand.Value);
                ThingDef def;
                if (cls == LandPlants.Tree)
                {
                    // The kind that stood there comes back, where the land grows it; after a wilt, the others take its place.
                    def = p.cause != LandCause.Wilt && tree != null && LandPlants.WildHere(map, tree) && Rand.Chance(0.7f) ? tree : LandPlants.Pick(map, LandPlants.Tree, p.cause == LandCause.Wilt ? tree : null);
                    if (def != null && Crowded(map, c)) continue;
                }
                else def = LandPlants.Pick(map, cls) ?? LandPlants.Pick(map, LandPlants.Grass);
                if (def == null || !def.CanEverPlantAt(c, map, false, true)) continue;
                if (!Spawn(map, def, c, cls)) continue;
                set++;
                if (cls == ownClass) own++;
            }
            return set;
        }

        private static bool Spawn(Map map, ThingDef def, IntVec3 c, int cls)
        {
            try
            {
                var plant = (Plant)ThingMaker.MakeThing(def);
                plant.Growth = cls == LandPlants.Tree ? Rand.Range(LandMath.SaplingGrowthMin, LandMath.SaplingGrowthMax)
                    : cls == LandPlants.Brush ? Rand.Range(LandMath.BrushGrowthMin, LandMath.BrushGrowthMax) : Rand.Range(LandMath.GrassGrowthMin, LandMath.GrassGrowthMax);
                GenSpawn.Spawn(plant, c, map);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the land: a plant set", e);
                return false;
            }
        }

        // ================= What is told =================

        private static string Place(Map map, LandPatch p) => StorytellerEvents.Direction(map, new IntVec3(p.CenterX, 0, p.CenterZ));

        private static string What(LandPatch p) => ("CC_Land_Patch_" + p.cause).Translate().Resolve();

        /// <summary>A patch came to a stage: grass on the burn, brush, young trees (each told once, as it comes).</summary>
        private static void Stage(Map map, LandPatch p)
        {
            string key = p.stage == LandStage.Grass ? "CC_Land_Stage_Grass" : p.stage == LandStage.Saplings ? "CC_Land_Stage_Saplings" : p.stage == LandStage.Brush ? "CC_Land_Stage_Brush" : null;
            if (key == null) return;
            var tree = p.tree.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(p.tree);
            string trees = tree != null && p.cause != LandCause.Wilt ? Find.ActiveLanguageWorker.Pluralize(tree.label) : "CC_Land_YoungTrees".Translate().Resolve();
            Tell.Minor(NewsKind.Land, Importance.Flavor, map, key.Translate(What(p).Named("PATCH"), Place(map, p).Named("PLACE"), trees.Named("TREES")).Resolve(),
                new LookTargets(new IntVec3(p.CenterX, 0, p.CenterZ), map), MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>Cells looked at in a patch when it is done, to tell only what stands there.</summary>
        private const int CloseLooks = 60;

        /// <summary>
        /// A patch is done: woods again where trees stand in it again (in the chronicle; a felled wood told to the Mossborn; a
        /// believer may take it for Hollin's), or grass and brush again where plants cover it; nothing told of a patch built over.
        /// </summary>
        private static void Close(Map map, LandPatch p)
        {
            var at = new IntVec3(p.CenterX, 0, p.CenterZ);
            string what = What(p), place = Place(map, p);
            int trees = 0, plants = 0;
            for (int i = 0; i < CloseLooks; i++)
            {
                var c = new IntVec3(Rand.RangeInclusive(p.x0, p.x1), 0, Rand.RangeInclusive(p.z0, p.z1));
                if (!c.InBounds(map) || !(c.GetPlant(map) is Plant plant) || plant.def.plant.isStump) continue;
                plants++;
                if (plant.def.plant.IsTree) trees++;
            }
            if (!p.Wood || trees < LandMath.WoodTrees)
            {
                if (plants < CloseLooks / 4) return;
                Tell.Minor(NewsKind.Land, Importance.Flavor, map, "CC_Land_Closed_Open".Translate(what.Named("PATCH"), place.Named("PLACE")).Resolve(), new LookTargets(at, map),
                    MessageTypeDefOf.PositiveEvent);
                return;
            }
            string text = "CC_Land_Closed_Wood".Translate(what.Named("PATCH"), place.Named("PLACE")).Resolve();
            Tell.Message(NewsKind.Land, Importance.Notable, map, text, new LookTargets(at, map), MessageTypeDefOf.PositiveEvent);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Land_Closed_Chronicle".Translate(what.Named("PATCH")).Resolve()));
            if (p.cause == LandCause.Felled) Mossborn(map, p);
            LandGods.Said(map, "woods", LandGods.Trees, at);
        }

        /// <summary>A felled wood let grow back: each Mossborn people hears of it, and thinks a little better of the colony.</summary>
        private static void Mossborn(Map map, LandPatch p)
        {
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(f => Peoples.CallingOf(f) == PeoplesMath.Grovekeepers && !f.defeated && f.HasGoodwill && !f.HostileTo(Faction.OfPlayer)))
            {
                WorldSituationWorker.Goodwill(f, LandMath.MossbornGoodwill, GoodwillWhy.Ledger);
                Tell.Message(NewsKind.World, Importance.Notable, map, "CC_Land_Mossborn".Translate(f.Name.Named("FACTION"), p.trees.Named("COUNT")).Resolve(),
                    new LookTargets(new IntVec3(p.CenterX, 0, p.CenterZ), map), MessageTypeDefOf.PositiveEvent);
            }
        }

        /// <summary>Dev: every patch here a stage on (its growing days brought to the next stage, and that stage's plants set at once).</summary>
        internal static string DevStage(Map map)
        {
            var b = LandBook.Of(map);
            if (b == null || b.Patches.Count == 0) return "no patches here (burn some ground, or fell six trees close together, first).";
            foreach (var p in b.Patches.ToList())
            {
                if (!p.Counts)
                {
                    p.lost = Math.Max(p.lost, 12);
                    if (p.cause != LandCause.Burn && p.cause != LandCause.Rust) p.trees = Math.Max(p.trees, 6);
                }
                p.growDays = p.stage == LandStage.Bare ? LandMath.GrassDays : p.stage == LandStage.Grass ? LandMath.BrushDays : p.stage == LandStage.Brush ? LandMath.SaplingDays : LandMath.WoodsDays;
                Advance(map, b, p, LandMath.RegrowCells);
            }
            return b.Describe().Split('\n')[0];
        }
    }
}
