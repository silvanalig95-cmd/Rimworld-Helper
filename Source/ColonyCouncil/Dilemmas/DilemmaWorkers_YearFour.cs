using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The council's questions about the colony's own systems, fourth round (Defs/DilemmaDefs/Rimfolk_Dilemmas_YearFour.xml):
    // the empty field and what a field grows (the fields), snow on the paths (the weather), the unfinished work and the
    // trees by the wall (building and defence), too many in the pens and what to teach an animal (the herd), home from the
    // road (journeys), and a doubter of the faith and the faith without a guide (the faith council). Each comes only while
    // the colony is as its letter says, and each answer's words happen in the game: a sowing party or the field given up,
    // the field's crop changed, the snow-clearing area marked and cleared, a building party or the plans that can't be paid
    // taken off, trees marked for the cutters, animals marked for slaughter, an animal's training set, a day off for the
    // travellers or their road told, an hour with the faith's guide or alone at its altar, the faith's moral guide named.
    // The effects that mark trees and wild plants are the decrees' too (Decrees/DecreeWork.cs). The rules without game
    // types are in YearFourMath.

    /// <summary>Shared look-ups for this round's questions.</summary>
    public static class YearFour
    {
        public static List<Pawn> Grown(Map map) => VarietyThree.Grown(map);

        /// <summary>A colonist of this colony has the wish open (the question about the same thing waits for it).</summary>
        public static bool WishOpen(Map map, string wishDef)
        {
            var wishes = CouncilGameComponent.Instance?.wishes;
            if (wishes == null || map == null) return false;
            foreach (var w in wishes)
                if (w?.def != null && w.def.defName == wishDef && w.state == WishState.Open && w.pawn != null && Homes.Of(w.pawn) == map) return true;
            return false;
        }

        /// <summary>The question was asked in this colony within <see cref="YearFourMath.QuietAfterAskedDays"/> (the wish about the same thing waits).</summary>
        public static bool AskedLately(Map map, string dilemmaDef) =>
            YearFourMath.AskedLately(DilemmaTracker.For(map)?.LastAsked(dilemmaDef) ?? -1, Find.TickManager.TicksGame);

        // ---------------- Fields ----------------

        /// <summary>The colony's outdoor growing zones, under the open sky for the most part, set to sow.</summary>
        public static IEnumerable<Zone_Growing> Fields(Map map) =>
            map.zoneManager.AllZones.OfType<Zone_Growing>().Where(z => z.Cells.Count > 0 && z.allowSow && z.GetPlantDefToGrow()?.plant != null
                                                                      && z.Cells.Count(c => !c.Roofed(map)) * 2 > z.Cells.Count);

        /// <summary>Cells of the zone with its own crop growing in them.</summary>
        public static int Sown(Zone_Growing z)
        {
            var def = z.GetPlantDefToGrow();
            int n = 0;
            foreach (var c in z.Cells)
                if (c.GetPlant(z.Map) is Plant p && p.def == def) n++;
            return n;
        }

        /// <summary>A field the colony grows food in (its crop gives something edible raw).</summary>
        public static bool FoodField(Zone_Growing z)
        {
            var h = z.GetPlantDefToGrow()?.plant?.harvestedThingDef;
            return h != null && h.IsNutritionGivingIngestible && !h.IsDrug;
        }

        /// <summary>A crop the colony can sow in the open ground now (its research done), or null.</summary>
        public static ThingDef Crop(string defName)
        {
            var d = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            var pp = d?.plant;
            if (pp == null || !pp.Sowable || pp.IsTree || pp.sowTags == null || !pp.sowTags.Contains("Ground")) return null;
            if (pp.sowResearchPrerequisites != null && pp.sowResearchPrerequisites.Any(r => r != null && !r.IsFinished)) return null;
            return d;
        }

        public static Zone_Growing ZoneOf(DilemmaContext ctx) =>
            ctx is DilemmaContextMore more && more.zone is Zone_Growing z && z.Map == ctx.map && ctx.map.zoneManager.AllZones.Contains(z) ? z : null;

        // ---------------- Snow ----------------

        /// <summary>The home area's open ground (sampled every other cell), and how much of it lies under deep snow.</summary>
        public static void Snow(Map map, out int open, out int deep)
        {
            open = deep = 0;
            if (map?.snowGrid == null) return;
            var home = map.areaManager.Home;
            foreach (var c in home.ActiveCells)
            {
                if ((c.x + c.z) % 2 != 0 || c.Roofed(map) || !c.Standable(map)) continue;
                open++;
                if (map.snowGrid.GetDepth(c) >= YearFourMath.DeepSnow) deep++;
            }
        }

        public static Area SnowArea(Map map) => map?.areaManager?.SnowOrSandClear;

        // ---------------- Plans ----------------

        /// <summary>The colony's blueprints and frames standing on the map.</summary>
        public static List<Thing> Plans(Map map) =>
            map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint).Concat(map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
                .Where(t => t.Spawned && t.Faction == Faction.OfPlayer).ToList();

        /// <summary>A blueprint whose materials the colony's stores don't hold enough of now.</summary>
        public static bool Unpaid(Blueprint_Build b, Map map)
        {
            try
            {
                foreach (var cost in b.TotalMaterialCost())
                    if (cost?.thingDef != null && map.resourceCounter.GetCount(cost.thingDef) < cost.count) return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("year four plan cost", e);
            }
            return false;
        }

        public static List<Blueprint_Build> UnpaidPlans(Map map) =>
            map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint).OfType<Blueprint_Build>().Where(b => b.Spawned && b.Faction == Faction.OfPlayer && Unpaid(b, map)).ToList();

        // ---------------- Trees ----------------

        /// <summary>A wall of the colony's that faces the open: an impassable building of ours (not a door) with open, unroofed ground beside it outside any room.</summary>
        private static bool OuterWall(IntVec3 c, Map map)
        {
            var e = c.GetEdifice(map);
            if (e == null || e.Faction != Faction.OfPlayer || e.def.passability != Traversability.Impassable || e is Building_Door) return false;
            foreach (var x in GenAdj.CellsAdjacentCardinal(c, Rot4.North, IntVec2.One))
                if (x.InBounds(map) && !x.Roofed(map) && x.Standable(map) && (x.GetRoom(map)?.PsychologicallyOutdoors ?? true)) return true;
            return false;
        }

        /// <summary>A wild grown tree (not sown in a field, not a tree a faith holds sacred) standing in the open.</summary>
        public static bool WildTree(Plant p, Map map) =>
            p != null && p.Spawned && p.def.plant != null && p.def.plant.IsTree && p.def.plant.treeCategory != TreeCategory.Super && p.Growth >= YearFourMath.TreeGrown
            && p.Position.GetZone(map) == null && !p.Position.Roofed(map) && !p.IsForbidden(Faction.OfPlayer);

        /// <summary>The wild grown trees that stand within reach of the colony's outer walls, nearest the walls first.</summary>
        public static List<Plant> TreesByWalls(Map map)
        {
            // From the walls out (a colony has far fewer wall cells facing the open than trees about it): each outer wall's
            // cells within reach, and the nearest wall each tree there stands from.
            var found = new Dictionary<Plant, float>();
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (!b.Spawned || b.def.passability != Traversability.Impassable || b is Building_Door || b.def.size.x != 1 || b.def.size.z != 1) continue;
                var at = b.Position;
                if (!OuterWall(at, map)) continue;
                foreach (var c in GenRadial.RadialCellsAround(at, YearFourMath.TreeReach, false))
                {
                    if (!c.InBounds(map) || !(c.GetPlant(map) is Plant p) || !WildTree(p, map)) continue;
                    float d = c.DistanceTo(at);
                    if (!found.TryGetValue(p, out float was) || d < was) found[p] = d;
                }
            }
            return found.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
        }

        private static readonly Dictionary<int, (int tick, List<Plant> trees)> treesSeen = new Dictionary<int, (int, List<Plant>)>();

        /// <summary>The same, worked out at most every six hours a map (the advisor's hourly look; trees grow slowly).</summary>
        public static List<Plant> TreesByWallsLately(Map map)
        {
            int now = Find.TickManager.TicksGame;
            if (treesSeen.TryGetValue(map.uniqueID, out var c) && now >= c.tick && now - c.tick < GenDate.TicksPerHour * 6)
                return c.trees.Where(p => p != null && p.Spawned && p.Map == map).ToList();
            var trees = TreesByWalls(map);
            treesSeen[map.uniqueID] = (now, trees);
            return trees;
        }

        /// <summary>The nearest wild grown trees outside the home area and the colony's zones, for wood.</summary>
        public static List<Plant> TreesForWood(Map map, int count)
        {
            var center = TemplatePlanner.ColonyCenter(map);
            var home = map.areaManager.Home;
            var dm = map.designationManager;
            return map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>()
                .Where(p => WildTree(p, map) && !home[p.Position] && dm.DesignationOn(p) == null && p.def.plant.harvestedThingDef == ThingDefOf.WoodLog && !p.Position.Fogged(map))
                .OrderBy(p => p.Position.DistanceToSquared(center)).Take(Mathf.Max(0, count)).ToList();
        }

        /// <summary>The stores hold less wood than the winter wants: 150 a colonist for a tribe or a village, 80 for any other.</summary>
        public static bool WoodShort(Map map)
        {
            if (map == null) return false;
            var age = Ages.Of(map);
            return map.resourceCounter.GetCount(ThingDefOf.WoodLog) < YearFourMath.WoodWanted(map.mapPawns.FreeColonistsSpawnedCount, age == WorldAge.Tribal || age == WorldAge.Medieval);
        }

        /// <summary>Wood a grown tree of this kind gives the cutter (the game's yield at full growth).</summary>
        public static float WoodOf(ThingDef tree) => tree?.plant == null ? 25f : Mathf.Max(5f, tree.plant.harvestYield);

        /// <summary>Trees already marked for cutting on the map.</summary>
        public static int MarkedTrees(Map map) =>
            map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.CutPlant).Count(d => d.target.Thing is Plant p && p.def.plant.IsTree);

        // ---------------- Wild plants ----------------

        /// <summary>
        /// Wild plants ripe for picking (berries, wild healroot, agave, mushrooms) within reach of the colony, outside its zones:
        /// only those whose harvest is food or medicine. The game counts a plant as a tree only when it yields wood, so Alpha
        /// Bees' trees with a wild hive (and any plant harvested for something not eaten or used to treat) were marked too;
        /// the player, 9 November 2026: "any bee related matters shouldn't be involved as these don't immedieatlly generate
        /// food".
        /// </summary>
        public static List<Plant> Forage(Map map, int count)
        {
            var center = TemplatePlanner.ColonyCenter(map);
            var dm = map.designationManager;
            float reach = YearFourMath.ForageReach * YearFourMath.ForageReach;
            return map.listerThings.ThingsInGroup(ThingRequestGroup.HarvestablePlant).OfType<Plant>()
                .Where(p => p.Spawned && !p.def.plant.IsTree && p.HarvestableNow && p.def.plant.harvestedThingDef != null && p.def.plant.harvestedThingDef != ThingDefOf.WoodLog
                            && p.Position.GetZone(map) == null && p.Position.DistanceToSquared(center) <= reach && dm.DesignationOn(p) == null && !p.Position.Fogged(map)
                            && !p.IsForbidden(Faction.OfPlayer) && !p.def.plant.harvestedThingDef.IsDrug
                            && (p.def.plant.harvestedThingDef.IsNutritionGivingIngestible || p.def.plant.harvestedThingDef.IsMedicine))
                .OrderBy(p => p.Position.DistanceToSquared(center)).Take(Mathf.Max(0, count)).ToList();
        }

        // ---------------- The herd ----------------

        public static readonly string[] FeedDefs = { "Hay", "Kibble" };

        /// <summary>The colony's grown grazers on the map (they eat grass and hay), not bonded ones' worth of pets.</summary>
        public static List<Pawn> Grazers(Map map) =>
            map.mapPawns.SpawnedColonyAnimals.Where(a => !a.Dead && a.RaceProps.Animal && a.RaceProps.Eats(FoodTypeFlags.Plant) && a.RaceProps.Roamer).ToList();

        /// <summary>What the herd eats in a day (the game's own hunger), and the hay and kibble in store.</summary>
        public static float HerdADay(List<Pawn> herd) =>
            herd.Sum(a => a.needs?.food == null ? 0f : a.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed, false) * GenDate.TicksPerDay);

        public static float Feed(Map map)
        {
            float n = 0f;
            foreach (var name in FeedDefs)
            {
                var d = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (d == null) continue;
                foreach (var t in map.listerThings.ThingsOfDef(d))
                    if (t.Spawned && !t.IsForbidden(Faction.OfPlayer)) n += t.stackCount * t.GetStatValue(StatDefOf.Nutrition);
            }
            return n;
        }

        /// <summary>
        /// The cold that stops the grass (the advisor's reckoning, SeasonUtil.Winter): its days still ahead when it's here or
        /// within <see cref="YearFourMath.WinterWithinDays"/> days, else 0.
        /// </summary>
        public static float WinterAhead(Map map)
        {
            if (!SeasonUtil.HasSeasons(map)) return 0f;
            SeasonUtil.Winter(map, out float until, out float length, out bool now);
            if (until < 0f || (!now && until > YearFourMath.WinterWithinDays)) return 0f;
            return length;
        }

        /// <summary>
        /// Who of the herd to mark first: never someone's own, bonded, named or carrying young; the spare males first, then the
        /// oldest, always leaving a male and a female of each kind.
        /// </summary>
        public static List<Pawn> ToCull(List<Pawn> herd, int count)
        {
            var chosen = new List<Pawn>();
            if (count <= 0) return chosen;
            // As the herd's cull spares them (Animals/AnimalDilemmas.cs): nobody's own animal, nothing bonded or named.
            var may = herd.Where(a => !a.Downed && a.ageTracker.CurLifeStage?.reproductive != false && !Bonded(a) && !Pregnant(a) && !a.IsQuestLodger()
                                      && PetTracker.PersonOf(a) == null && (a.Name == null || a.Name.Numerical)
                                      && a.Map.designationManager.DesignationOn(a, DesignationDefOf.Slaughter) == null)
                .OrderByDescending(a => a.ageTracker.AgeBiologicalYearsFloat / Mathf.Max(1f, a.RaceProps.lifeExpectancy)).ToList();
            // The spare males first, oldest first, always leaving one male of each kind (and one female) for the spring.
            var males = herd.Where(a => a.gender == Gender.Male).GroupBy(a => a.def).ToDictionary(g => g.Key, g => g.Count());
            var females = herd.Where(a => a.gender == Gender.Female).GroupBy(a => a.def).ToDictionary(g => g.Key, g => g.Count());
            foreach (var a in may.Where(a => a.gender == Gender.Male))
            {
                if (chosen.Count >= count) break;
                if (males.TryGetValue(a.def, out int n) && n > 1)
                {
                    chosen.Add(a);
                    males[a.def] = n - 1;
                }
            }
            foreach (var a in may)
            {
                if (chosen.Count >= count) break;
                if (chosen.Contains(a)) continue;
                var left = a.gender == Gender.Male ? males : a.gender == Gender.Female ? females : null;
                if (left != null)
                {
                    if (!left.TryGetValue(a.def, out int n) || n <= 1) continue;
                    left[a.def] = n - 1;
                }
                chosen.Add(a);
            }
            return chosen;
        }

        private static bool Bonded(Pawn a) => a.relations?.DirectRelations?.Any(r => r.def == PawnRelationDefOf.Bond) ?? false;

        private static bool Pregnant(Pawn a) => a.health?.hediffSet?.HasHediff(HediffDefOf.Pregnant) ?? false;

        // ---------------- Training ----------------

        public static TrainableDef Trainable(string name) => DefDatabase<TrainableDef>.GetNamedSilentFail(name);

        /// <summary>It can be taught this now (the game's own check).</summary>
        public static bool CanLearn(Pawn a, TrainableDef td)
        {
            if (a?.training == null || td == null) return false;
            try
            {
                return a.training.CanAssignToTrain(td, out bool visible).Accepted && visible;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("year four training " + td.defName, e);
                return false;
            }
        }

        public static bool Set(Pawn a, TrainableDef td) => a?.training != null && td != null && (a.training.HasLearned(td) || a.training.GetWanted(td));

        // ---------------- The road ----------------

        /// <summary>Those back from the road at the colony of this map, lately, by the calendar's homecoming look; empty when none.</summary>
        public static List<Pawn> HomeFromRoad(Map map, out float daysAway)
        {
            daysAway = 0f;
            var cal = ColonyCalendar.For(map);
            if (cal == null || !cal.LastHome(out int tick, out var ids, out float away)) return new List<Pawn>();
            float home = (Find.TickManager.TicksGame - tick) / (float)GenDate.TicksPerDay;
            if (!YearFourMath.FreshHome(away, home)) return new List<Pawn>();
            daysAway = away;
            return Grown(map).Where(p => ids.Contains(p.ThingID)).ToList();
        }

        // ---------------- The faith ----------------

        /// <summary>The moral guide's role in the faith, while nobody holds it.</summary>
        public static Precept_Role EmptyGuide(Ideo faith)
        {
            if (faith == null) return null;
            try
            {
                var moralist = PreceptDefOf.IdeoRole_Moralist;
                foreach (var r in faith.RolesListForReading)
                    if (r != null && moralist != null && r.def == moralist && r.ChosenPawnSingle() == null)
                        return r;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("year four moral guide", e);
            }
            return null;
        }

        /// <summary>Who could take the role: of the faith, grown, holding no other role of it, and meeting what the role asks.</summary>
        public static bool CanGuide(Precept_Role role, Pawn p)
        {
            if (role == null || p == null || p.Ideo != role.ideo || p.DevelopmentalStage.Juvenile() || p.IsSlave || p.IsQuestLodger()) return false;
            try
            {
                return role.ideo.GetRole(p) == null && role.RequirementsMet(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("year four guide check", e);
                return false;
            }
        }
    }

    // ===================================== The empty field =====================================

    /// <summary>
    /// A field of the colony's lies mostly empty in the growing season ({THING}: its crop, {COUNT}: the empty cells). A is the
    /// colony's best grower, who wants it sown; B someone the work elsewhere needs, who would let it go.
    /// </summary>
    public class DilemmaWorker_EmptyField : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var pawns = YearFour.Grown(map);
            if (pawns.Count < def.minColonists) return false;
            var a = VarietyThree.Best(pawns.Where(p => def.roleA.Allows(p, null)), SkillDefOf.Plants, WorkTypeDefOf.Growing);
            if (a == null) return false;
            Zone_Growing field = null;
            int empty = 0;
            foreach (var z in YearFour.Fields(map).OrderByDescending(z => z.Cells.Count))
            {
                var crop = z.GetPlantDefToGrow();
                if (!PlantUtility.GrowthSeasonNow(z.Cells[0], map, crop)) continue;
                if (FieldWeather.SowOn && FieldWeather.Paused(a, z.Cells[0])) continue;
                int sown = YearFour.Sown(z);
                if (!YearFourMath.FieldEmpty(sown, z.Cells.Count)) continue;
                field = z;
                empty = z.Cells.Count - sown;
                break;
            }
            if (field == null) return false;
            var b = VarietyThree.Other(pawns, a, SkillDefOf.Construction);
            if (b == null) return false;
            ctx = new DilemmaContextMore { def = def, map = map, a = a, b = b, thing = field.GetPlantDefToGrow(), zone = field, count = empty };
            return true;
        }
    }

    /// <summary>The field given up: the growing zone taken off the map (the game's own), its ground left as it is.</summary>
    public class DilemmaEffect_GiveUpField : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) => ctx is DilemmaContextMore && YearFour.ZoneOf(ctx) == null ? "CC_Y4_FieldGone".Translate().ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var z = YearFour.ZoneOf(ctx);
            if (z == null) return;
            sheet.Play("CC_Impact_GiveUpField".Translate(z.label, ctx.thing?.label ?? "").Resolve(), "CC_Impact_GiveUpFieldBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var z = YearFour.ZoneOf(ctx);
            if (z == null) return;
            try
            {
                var at = z.Cells[0];
                string label = z.label;
                z.Delete();
                Tell.Message(NewsKind.Land, Importance.Notable, ctx.map, "CC_Y4_FieldGivenUp".Translate(label.Named("FIELD")).Resolve(), new LookTargets(at, ctx.map), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("field given up", e);
            }
        }
    }

    // ===================================== What the field grows =====================================

    /// <summary>
    /// The colony is short of medicine (or of cloth) while its food is plenty, and a crop that gives it can be sown ({THING});
    /// a food field ({PLAIN}: its crop) could grow it instead. A is the doctor (or the tailor), B the colony's best grower.
    /// </summary>
    public class DilemmaWorker_WhichCrop : DilemmaWorker
    {
        private static ThingDef Wanted(Map map, int colonists, out string why)
        {
            why = null;
            var healroot = YearFour.Crop("Plant_Healroot");
            if (healroot != null && YearFourMath.MedicineShort(LifeChecks.Medicine(map), colonists))
            {
                why = "medicine";
                return healroot;
            }
            var cloth = YearFour.Crop("Plant_Devilstrand") ?? YearFour.Crop("Plant_Cotton");
            int textiles = ThingCategoryDefOf.Textiles != null ? map.resourceCounter.GetCountIn(ThingCategoryDefOf.Textiles) : 0;
            if (cloth != null && YearFourMath.ClothShort(textiles, colonists))
            {
                why = "cloth";
                return cloth;
            }
            return null;
        }

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var pawns = YearFour.Grown(map);
            if (pawns.Count < def.minColonists || AdvisorWorker_Food.DaysOfFood(map) < YearFourMath.FoodPlentyDays) return false;
            var want = Wanted(map, pawns.Count, out string why);
            if (want == null) return false;
            var field = YearFour.Fields(map).Where(z => YearFour.FoodField(z) && z.GetPlantDefToGrow() != want && z.Cells.Count >= YearFourMath.FieldCells
                                                         && PlantUtility.CanSowOnGrower(want, z) && PlantUtility.GrowthSeasonNow(z.Cells[0], map, want))
                .OrderByDescending(z => z.Cells.Count).FirstOrDefault();
            if (field == null) return false;
            var a = why == "medicine"
                ? VarietyThree.Best(pawns.Where(p => def.roleA.Allows(p, null)), SkillDefOf.Medicine, WorkTypeDefOf.Doctor)
                : VarietyThree.Best(pawns.Where(p => def.roleA.Allows(p, null)), SkillDefOf.Crafting, VarietyThree.Work("Tailoring"));
            if (a == null) return false;
            var b = VarietyThree.Best(pawns, SkillDefOf.Plants, WorkTypeDefOf.Growing, a);
            if (b == null) return false;
            ctx = new DilemmaContextMore { def = def, map = map, a = a, b = b, thing = want, other = field.GetPlantDefToGrow(), zone = field, count = field.Cells.Count };
            return true;
        }

        /// <summary>"medicine" or "cloth": what the colony is short of, for the letter's lines.</summary>
        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx?.thing == null) return;
            tags.Add(ctx.thing.defName == "Plant_Healroot" ? "medicine" : "cloth");
        }
    }

    /// <summary>The field (or the half of it farther from the colony, with <see cref="half"/>) set to grow the question's crop ({THING}).</summary>
    public class DilemmaEffect_SetCrop : DilemmaEffect
    {
        public bool half;

        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            if (!(ctx is DilemmaContextMore)) return null;
            var z = YearFour.ZoneOf(ctx);
            if (z == null) return "CC_Y4_FieldGone".Translate().ToString();
            return ctx.thing == null || z.GetPlantDefToGrow() == ctx.thing ? "CC_Y4_FieldAlready".Translate(ctx.thing?.label ?? "").ToString() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var z = YearFour.ZoneOf(ctx);
            if (z == null || ctx.thing == null) return;
            string was = (ctx as DilemmaContextMore)?.other?.label ?? z.GetPlantDefToGrow()?.label ?? "";
            sheet.Play((half ? "CC_Impact_SetCropHalf" : "CC_Impact_SetCrop").Translate(z.label, ctx.thing.label, was).Resolve(),
                "CC_Impact_SetCropBrief".Translate(ctx.thing.label).Resolve());
            sheet.Add(ImpactKind.Goods, -1, "CC_Impact_SetCropFood".Translate(was).Resolve(), "CC_Impact_SetCropFoodBrief".Translate(was).Resolve(), "cropfood");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var z = YearFour.ZoneOf(ctx);
            if (z == null || ctx.thing == null) return;
            try
            {
                Zone_Growing set = z;
                if (half && z.Cells.Count >= 2)
                {
                    var center = TemplatePlanner.ColonyCenter(ctx.map);
                    var far = z.Cells.OrderByDescending(c => c.DistanceToSquared(center)).Take(YearFourMath.Half(z.Cells.Count) - (z.Cells.Count % 2)).ToList();
                    if (far.Count == 0) return;
                    foreach (var c in far) z.RemoveCell(c);
                    set = Sowing.MakeZone(ctx.map, ctx.thing, far);
                    if (set == null)
                    {
                        foreach (var c in far) z.AddCell(c);
                        return;
                    }
                }
                else set.SetPlantDefToGrow(ctx.thing);
                Tell.Message(NewsKind.Land, Importance.Notable, ctx.map, "CC_Y4_CropSet".Translate(set.label.Named("FIELD"), ctx.thing.label.Named("CROP")).Resolve(),
                    new LookTargets(set.Cells[0], ctx.map), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("crop set", e);
            }
        }
    }

    // ===================================== Snow on the paths =====================================

    /// <summary>
    /// Deep snow lies over the home area's open ground and the colony has next to no snow-clearing area. A is someone who
    /// clears and hauls, B the colony's best grower, who says it will melt.
    /// </summary>
    public class DilemmaWorker_SnowPaths : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map) || map.snowGrid == null) return false;
            var area = YearFour.SnowArea(map);
            if (area == null || map.mapTemperature.OutdoorTemp > YearFourMath.SnowMelts) return false;
            if (YearFour.WishOpen(map, "Rimfolk_Wish_SnowWays")) return false;
            YearFour.Snow(map, out int open, out int deep);
            if (!YearFourMath.Snowed(deep, open, area.TrueCount)) return false;
            var pawns = YearFour.Grown(map);
            if (pawns.Count < def.minColonists) return false;
            var a = pawns.Where(p => def.roleA.Allows(p, null) && VarietyThree.Does(p, VarietyThree.Work("Cleaning"))).InRandomOrder().FirstOrDefault()
                    ?? pawns.Where(p => def.roleA.Allows(p, null)).InRandomOrder().FirstOrDefault();
            if (a == null) return false;
            var b = VarietyThree.Other(pawns, a, SkillDefOf.Plants);
            if (b == null) return false;
            ctx = new DilemmaContextMore { def = def, map = map, a = a, b = b, count = deep * 2 };
            return true;
        }
    }

    /// <summary>
    /// The snow-clearing area marked (the game's own area): all the home area's open ground under snow, or with
    /// <see cref="paths"/> only the open ground by the colony's doors to the outside. The cleaners clear it as the game has them.
    /// </summary>
    public class DilemmaEffect_SnowArea : DilemmaEffect
    {
        public bool paths;

        public override bool ChanceEach => false;

        public static List<IntVec3> Cells(Map map, bool paths)
        {
            var cells = new List<IntVec3>();
            var area = YearFour.SnowArea(map);
            if (area == null || map.snowGrid == null) return cells;
            var home = map.areaManager.Home;
            IEnumerable<IntVec3> from;
            if (paths)
            {
                var near = new HashSet<IntVec3>();
                foreach (var door in map.listerBuildings.allBuildingsColonist.OfType<Building_Door>())
                    if (door.Spawned && home[door.Position])
                        foreach (var c in GenRadial.RadialCellsAround(door.Position, YearFourMath.PathReach, false))
                            near.Add(c);
                from = near;
            }
            else from = home.ActiveCells;
            var center = TemplatePlanner.ColonyCenter(map);
            foreach (var c in from.OrderBy(c => c.DistanceToSquared(center)))
            {
                if (cells.Count >= YearFourMath.SnowCellsMost) break;
                if (!c.InBounds(map) || !home[c] || area[c] || c.Roofed(map) || !c.Standable(map) || map.snowGrid.GetDepth(c) < 0.1f) continue;
                cells.Add(c);
            }
            return cells;
        }

        public override string Unavailable(DilemmaContext ctx) => ctx?.map != null && Cells(ctx.map, paths).Count == 0 ? "CC_Y4_SnowGone".Translate().ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null) return;
            int n = Cells(ctx.map, paths).Count;
            if (n == 0) return;
            sheet.Play((paths ? "CC_Impact_SnowPaths" : "CC_Impact_SnowAll").Translate(n).Resolve(), "CC_Impact_SnowAreaBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            var area = YearFour.SnowArea(map);
            if (area == null) return;
            try
            {
                var cells = Cells(map, paths);
                foreach (var c in cells) area[c] = true;
                if (cells.Count > 0)
                    Tell.Message(NewsKind.Colony, Importance.Notable, map, (paths ? "CC_Y4_SnowPathsMarked" : "CC_Y4_SnowAllMarked").Translate(cells.Count.Named("N")).Resolve(),
                        new LookTargets(cells[0], map), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("snow area", e);
            }
        }
    }

    // ===================================== The unfinished work =====================================

    /// <summary>
    /// Many of the colony's plans stand unbuilt, some of them waiting on stuff the stores don't hold ({COUNT}: the plans).
    /// A is the colony's best builder, who wants a building party; B the one who keeps the stores, who would take off what
    /// can't be paid for.
    /// </summary>
    public class DilemmaWorker_Unfinished : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var plans = YearFour.Plans(map);
            if (!YearFourMath.ManyPlans(plans.Count)) return false;
            int unpaid = YearFour.UnpaidPlans(map).Count;
            if (unpaid < 2 || plans.Count - unpaid < YearFourMath.Plans / 2) return false;
            var pawns = YearFour.Grown(map);
            if (pawns.Count < def.minColonists) return false;
            var a = VarietyThree.Best(pawns.Where(p => def.roleA.Allows(p, null)), SkillDefOf.Construction, WorkTypeDefOf.Construction);
            if (a == null) return false;
            var b = pawns.FirstOrDefault(p => p != a && Interplay.Holds(p, "Council_Office_Quartermaster")) ?? VarietyThree.Other(pawns, a, SkillDefOf.Social);
            if (b == null) return false;
            ctx = new DilemmaContextMore { def = def, map = map, a = a, b = b, count = plans.Count };
            return true;
        }
    }

    /// <summary>The colony's blueprints whose materials the stores don't hold taken off the map (the game's own cancel).</summary>
    public class DilemmaEffect_CancelPlans : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) => ctx?.map != null && YearFour.UnpaidPlans(ctx.map).Count == 0 ? "CC_Y4_NoUnpaid".Translate().ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null) return;
            int n = YearFour.UnpaidPlans(ctx.map).Count;
            if (n == 0) return;
            sheet.Play("CC_Impact_CancelPlans".Translate(n).Resolve(), "CC_Impact_CancelPlansBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.map == null) return;
            try
            {
                var plans = YearFour.UnpaidPlans(ctx.map);
                var at = plans.Select(p => p.Position).ToList();
                foreach (var b in plans)
                    if (!b.Destroyed) b.Destroy(DestroyMode.Cancel);
                if (plans.Count > 0)
                    Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map, "CC_Y4_PlansCancelled".Translate(plans.Count.Named("N")).Resolve(), new LookTargets(at[0], ctx.map),
                        MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("plans cancelled", e);
            }
        }
    }

    // ===================================== The trees by the wall =====================================

    /// <summary>
    /// Grown wild trees stand close outside the colony's walls ({COUNT}), cover for anyone coming at them. A is the one who
    /// keeps the colony's watch (the marshal, else its best shot), B someone who would keep the trees.
    /// </summary>
    public class DilemmaWorker_TreeLine : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            if (YearFour.WishOpen(map, "Rimfolk_Wish_TreesByWalls")) return false;
            var trees = YearFour.TreesByWalls(map);
            if (!YearFourMath.TreeLine(trees.Count)) return false;
            var pawns = YearFour.Grown(map);
            if (pawns.Count < def.minColonists) return false;
            var a = pawns.FirstOrDefault(p => def.roleA.Allows(p, null) && Interplay.Holds(p, "Council_Office_Marshal"))
                    ?? VarietyThree.Best(pawns.Where(p => def.roleA.Allows(p, null) && !p.WorkTagIsDisabled(WorkTags.Violent)), SkillDefOf.Shooting);
            if (a == null) return false;
            var b = pawns.FirstOrDefault(p => p != a && Beliefs.Forbids(p, Belief.NoTreeCutting)) ?? VarietyThree.Best(pawns, SkillDefOf.Plants, null, a);
            if (b == null) return false;
            ctx = new DilemmaContextMore { def = def, map = map, a = a, b = b, count = trees.Count, thing = trees[0].def };
            return true;
        }

        /// <summary>"sacred" when B's faith holds tree cutting wrong, for the letter's lines.</summary>
        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx?.b != null && Beliefs.Forbids(ctx.b, Belief.NoTreeCutting)) tags.Add("sacred");
        }
    }

    /// <summary>
    /// Trees marked for the cutters (the game's own cut): those by the colony's walls (<see cref="half"/>: the nearest half),
    /// or with <see cref="forWood"/> the nearest wild trees outside the home area, a morning's worth until the stores hold
    /// the wood the winter wants (wood for the winter, a decree).
    /// </summary>
    public class DilemmaEffect_MarkTrees : DilemmaEffect
    {
        public bool half;
        public bool forWood;

        public override bool ChanceEach => false;

        private List<Plant> Trees(Map map)
        {
            if (forWood)
            {
                int colonists = map.mapPawns.FreeColonistsSpawnedCount;
                var age = Ages.Of(map);
                int wanted = YearFourMath.WoodWanted(colonists, age == WorldAge.Tribal || age == WorldAge.Medieval);
                var first = YearFour.TreesForWood(map, 1).FirstOrDefault();
                int n = YearFourMath.TreesToday(map.resourceCounter.GetCount(ThingDefOf.WoodLog), wanted, YearFour.WoodOf(first?.def), YearFour.MarkedTrees(map));
                return n <= 0 ? new List<Plant>() : YearFour.TreesForWood(map, n);
            }
            var dm = map.designationManager;
            var trees = YearFour.TreesByWalls(map).Where(p => dm.DesignationOn(p) == null).ToList();
            return half ? trees.Take(YearFourMath.Half(trees.Count)).ToList() : trees;
        }

        public override string Unavailable(DilemmaContext ctx) => !forWood && ctx?.map != null && Trees(ctx.map).Count == 0 ? "CC_Y4_TreesGone".Translate().ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null) return;
            if (forWood)
            {
                sheet.Play("CC_Impact_TreesForWood".Translate(YearFourMath.TreesADay).Resolve(), "CC_Impact_TreesForWoodBrief".Translate().Resolve());
                return;
            }
            int n = Trees(ctx.map).Count;
            if (n == 0) return;
            sheet.Play((half ? "CC_Impact_TreesHalf" : "CC_Impact_TreesAll").Translate(n).Resolve(), "CC_Impact_TreesBrief".Translate().Resolve());
            if (ctx.map.mapPawns.FreeColonistsSpawned.Any(p => Beliefs.Forbids(p, Belief.NoTreeCutting)))
                sheet.Add(ImpactKind.Mood, -1, "CC_Impact_TreesFaith".Translate().Resolve(), "CC_Impact_TreesFaithBrief".Translate().Resolve(), "treesfaith");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null) return;
            try
            {
                var trees = Trees(map);
                foreach (var p in trees) FieldBlight.Mark(map, p, DesignationDefOf.CutPlant);
                if (trees.Count > 0)
                    Tell.Message(NewsKind.Land, forWood ? Importance.Flavor : Importance.Notable, map,
                        (forWood ? "CC_Y4_TreesForWood" : "CC_Y4_TreesMarked").Translate(trees.Count.Named("N")).Resolve(), new LookTargets(trees.Cast<Thing>()), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trees marked", e);
            }
        }
    }

    /// <summary>Wild plants ripe for picking within reach of the colony marked for the game's harvest (foraging days, a decree), a morning's worth.</summary>
    public class DilemmaEffect_Forage : DilemmaEffect
    {
        public int count = YearFourMath.ForageADay;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null) return;
            sheet.Play("CC_Impact_Forage".Translate(count).Resolve(), "CC_Impact_ForageBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null) return;
            try
            {
                var plants = YearFour.Forage(map, count);
                foreach (var p in plants) FieldBlight.Mark(map, p, DesignationDefOf.HarvestPlant);
                if (plants.Count > 0)
                    Tell.Message(NewsKind.Land, Importance.Flavor, map, "CC_Y4_Foraged".Translate(plants.Count.Named("N"), TextUtil.JoinAnd(plants.Select(p => p.def.label).Distinct().Take(3).ToList()).Named("KINDS")).Resolve(),
                        new LookTargets(plants.Cast<Thing>()), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("forage marked", e);
            }
        }
    }

    // ===================================== Too many in the pens =====================================

    /// <summary>
    /// The cold is coming, and the hay and kibble in store won't see the colony's herd through it ({COUNT}: how many to
    /// mark so it would). A is the colony's herdmaster or best handler, B the one who'd rather keep them all.
    /// </summary>
    public class DilemmaWorker_HerdTooBig : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var herd = YearFour.Grazers(map);
            if (herd.Count < YearFourMath.Herd) return false;
            float winter = YearFour.WinterAhead(map);
            float days = YearFourMath.FeedDays(YearFour.Feed(map), YearFour.HerdADay(herd));
            if (!YearFourMath.HerdTooBig(herd.Count, days, winter)) return false;
            int n = YearFourMath.ToCull(herd.Count, days, winter);
            if (YearFour.ToCull(herd, n).Count < 1) return false;
            var pawns = YearFour.Grown(map);
            if (pawns.Count < def.minColonists) return false;
            var a = pawns.FirstOrDefault(p => def.roleA.Allows(p, null) && Interplay.Holds(p, "Council_Office_Herdmaster"))
                    ?? VarietyThree.Best(pawns.Where(p => def.roleA.Allows(p, null)), SkillDefOf.Animals, WorkTypeDefOf.Handling);
            if (a == null) return false;
            var b = pawns.Where(p => p != a && map.mapPawns.SpawnedColonyAnimals.Any(x => x.playerSettings?.Master == p)).InRandomOrder().FirstOrDefault()
                    ?? VarietyThree.Other(pawns, a, SkillDefOf.Animals);
            if (b == null) return false;
            ctx = new DilemmaContextMore { def = def, map = map, a = a, b = b, count = n, thing = herd.GroupBy(x => x.def).OrderByDescending(g => g.Count()).First().Key };
            return true;
        }
    }

    /// <summary>Animals of the herd marked for slaughter (the game's own): the question's count, or one with <see cref="one"/>.</summary>
    public class DilemmaEffect_MarkSlaughter : DilemmaEffect
    {
        public bool one;

        public override bool ChanceEach => false;

        private List<Pawn> Chosen(DilemmaContext ctx) => YearFour.ToCull(YearFour.Grazers(ctx.map), one ? 1 : Mathf.Max(1, ctx.count));

        public override string Unavailable(DilemmaContext ctx) => ctx?.map != null && Chosen(ctx).Count == 0 ? "CC_Y4_HerdNone".Translate().ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null) return;
            var who = Chosen(ctx);
            if (who.Count == 0) return;
            sheet.Play("CC_Impact_Slaughter".Translate(TextUtil.JoinAnd(who.Select(a => a.LabelShort).ToList())).Resolve(), "CC_Impact_SlaughterBrief".Translate(who.Count).Resolve());
            sheet.Add(ImpactKind.Goods, 1, "CC_Impact_SlaughterMeat".Translate().Resolve(), "CC_Impact_SlaughterMeatBrief".Translate().Resolve(), "slaughtermeat");
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.map == null) return;
            try
            {
                var who = Chosen(ctx);
                var dm = ctx.map.designationManager;
                foreach (var a in who)
                {
                    dm.RemoveAllDesignationsOn(a);
                    dm.AddDesignation(new Designation(a, DesignationDefOf.Slaughter));
                }
                if (who.Count > 0)
                    Tell.Message(NewsKind.Land, Importance.Notable, ctx.map, "CC_Y4_HerdMarked".Translate(TextUtil.JoinAnd(who.Select(a => a.LabelShort).ToList()).Named("NAMES"), who.Count.Named("N")).Resolve(),
                        new LookTargets(who), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("herd marked", e);
            }
        }
    }

    // ===================================== What to teach it =====================================

    /// <summary>
    /// An animal of the colony's could be taught to guard or to haul and is set to learn neither ({PET}). A is the colony's
    /// best handler, who wants it hauling; B the marshal or the colony's best fighter, who wants it at their side.
    /// </summary>
    public class DilemmaWorker_TrainBeast : DilemmaWorker
    {
        public static bool Untaught(Pawn a)
        {
            var guard = YearFour.Trainable("Release");
            var haul = YearFour.Trainable("Haul");
            if (guard == null || haul == null) return false;
            if (YearFour.Set(a, guard) || YearFour.Set(a, haul)) return false;
            return YearFour.CanLearn(a, guard) && YearFour.CanLearn(a, haul);
        }

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var animal = map.mapPawns.SpawnedColonyAnimals.Where(a => !a.Dead && !a.Downed && a.training != null && a.ageTracker.CurLifeStage?.reproductive != false && Untaught(a))
                .InRandomOrder().FirstOrDefault();
            if (animal == null) return false;
            var pawns = YearFour.Grown(map);
            if (pawns.Count < def.minColonists) return false;
            var a = VarietyThree.Best(pawns.Where(p => def.roleA.Allows(p, null)), SkillDefOf.Animals, WorkTypeDefOf.Handling);
            if (a == null) return false;
            var b = pawns.FirstOrDefault(p => p != a && Interplay.Holds(p, "Council_Office_Marshal"))
                    ?? VarietyThree.Best(pawns.Where(p => !p.WorkTagIsDisabled(WorkTags.Violent)), SkillDefOf.Melee, null, a);
            if (b == null) return false;
            ctx = new DilemmaContextMore { def = def, map = map, a = a, b = b, pet = animal, thing = animal.def };
            return true;
        }
    }

    /// <summary>The animal's training set (the game's own, its steps before it too): to guard and go for an enemy, or with <see cref="haul"/> to haul.</summary>
    public class DilemmaEffect_SetTraining : DilemmaEffect
    {
        public bool haul;

        public override bool ChanceEach => false;

        private TrainableDef Td => YearFour.Trainable(haul ? "Haul" : "Release");

        public override string Unavailable(DilemmaContext ctx) =>
            ctx?.pet != null && (ctx.pet.Dead || !YearFour.CanLearn(ctx.pet, Td)) ? "CC_Y4_CantTrain".Translate(ctx.pet.LabelShort).ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.pet == null || !YearFour.CanLearn(ctx.pet, Td)) return;
            sheet.Play((haul ? "CC_Impact_TrainHaul" : "CC_Impact_TrainGuard").Translate(ctx.pet.LabelShort).Resolve(), "CC_Impact_TrainBrief".Translate(ctx.pet.LabelShort).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var pet = ctx?.pet;
            var td = Td;
            if (pet?.training == null || pet.Dead || td == null) return;
            try
            {
                pet.training.SetWantedRecursive(td, true);
                if (!haul && YearFour.Trainable("Obedience") is TrainableDef ob && YearFour.CanLearn(pet, ob)) pet.training.SetWantedRecursive(ob, true);
                Tell.Message(NewsKind.Land, Importance.Notable, ctx.map, (haul ? "CC_Y4_TrainHaul" : "CC_Y4_TrainGuard").Translate(pet.Named("PET")).Resolve(), new LookTargets(pet),
                    MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("training set", e);
            }
        }
    }

    // ===================================== Home from the road =====================================

    /// <summary>
    /// Some of the colony's people came home from five days or more on the road, yesterday or today ({COUNT}: the days
    /// away). A is the one of them who speaks best, B someone who stayed and did their work while they were gone.
    /// </summary>
    public class DilemmaWorker_HomeFromRoad : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var back = YearFour.HomeFromRoad(map, out float away);
            if (back.Count == 0) return false;
            var pawns = YearFour.Grown(map);
            if (pawns.Count < def.minColonists) return false;
            var a = back.Where(p => def.roleA.Allows(p, null)).OrderByDescending(p => VarietyThree.Level(p, SkillDefOf.Social)).FirstOrDefault();
            if (a == null) return false;
            var b = pawns.Where(p => !back.Contains(p)).InRandomOrder().FirstOrDefault();
            if (b == null) return false;
            ctx = new DilemmaContextMore { def = def, map = map, a = a, b = b, count = Mathf.RoundToInt(away) };
            return true;
        }
    }

    /// <summary>
    /// A day off for everyone back from the road (the calendar's rest timetable for today, put back tomorrow), or with
    /// <see cref="DilemmaEffect_Rest.sleep"/> their beds. Those back are read again as it's given.
    /// </summary>
    public class DilemmaEffect_RestTravellers : DilemmaEffect_Rest
    {
        private static List<Pawn> Back(DilemmaContext ctx) =>
            ctx?.map == null ? new List<Pawn>() : YearFour.HomeFromRoad(ctx.map, out _).Where(p => PlayOuts.OurPerson(p) && p.Spawned && p.Map == ctx.map).ToList();

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!PlayOuts.On) return;
            var who = Back(ctx);
            if (who.Count == 0) return;
            string w = TextUtil.JoinAnd(who.Select(p => p.LabelShort).ToList());
            sheet.Play((sleep ? "CC_Impact_Sleep" : "CC_Impact_Rest").Translate(w).Resolve(), (sleep ? "CC_Impact_SleepBrief" : "CC_Impact_RestBrief").Translate(w).Resolve());
        }

        public override string Unavailable(DilemmaContext ctx) => ctx?.map != null && Back(ctx).Count == 0 ? "CC_Y4_NobodyBack".Translate().ToString() : null;

        public override void Apply(DilemmaContext ctx)
        {
            if (!PlayOuts.On || ctx?.map == null) return;
            try
            {
                var who = Back(ctx);
                var cal = ColonyCalendar.For(ctx.map);
                if (who.Count == 0 || cal == null) return;
                var given = cal.RestFor(ctx.map, who, sleep);
                if (given.Count == 0) return;
                string text = (sleep ? "CC_Play_Sleep" : "CC_Play_Rest").Translate(TextUtil.JoinAnd(given.Select(p => p.LabelShort).ToList()), given.Count).Resolve();
                Tell.Message(NewsKind.People, Importance.Notable, null, text, new LookTargets(given), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("travellers' rest", e);
            }
        }
    }

    // ===================================== A doubter of the faith =====================================

    /// <summary>
    /// Ideology: someone of the colony's faith doubts it ({FAITH}). A is the doubter, B the faith's moral guide at home, else
    /// its most devout speaker. The faith's altar, if it has one here, is the question's place.
    /// </summary>
    public class DilemmaWorker_Doubter : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!Faiths.On || !CanHappenNow(map)) return false;
            var faith = DecreeWork.ColonyFaith;
            if (faith == null) return false;
            var pawns = YearFour.Grown(map);
            if (pawns.Count < def.minColonists) return false;
            var a = pawns.Where(p => def.roleA.Allows(p, null) && Faiths.Of(p) == faith && YearFourMath.Doubts(Faiths.Certainty(p))).InRandomOrder().FirstOrDefault();
            if (a == null) return false;
            var guide = FaithEvenings.MoralGuide(faith);
            var b = guide != null && guide != a && guide.Spawned && guide.Map == map && !guide.Downed ? guide : FaithEvenings.Speaker(map, faith);
            if (b == null || b == a) return false;
            ctx = new FaithDilemmaContext { def = def, map = map, a = a, b = b, faithName = faith.name };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            var faith = DecreeWork.ColonyFaith;
            if (faith != null && FaithEvenings.MoralGuide(faith) == ctx?.b) tags.Add("guide");
            if (ctx?.map != null && faith != null && FaithEvenings.Place(ctx.map, faith) != null) tags.Add("altar");
        }
    }

    /// <summary>Certainty in their own faith steadied for those it's for (A unless given), through the shared core.</summary>
    public class DilemmaEffect_Steady : DilemmaEffect
    {
        public float amount = YearFourMath.GuideSteadies;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!Faiths.On) return;
            var who = Reached(ctx).Where(Faiths.Holds).ToList();
            if (who.Count > 0) DilemmaImpact.Certainty(sheet, WhoOf(ctx, who), amount);
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (!Faiths.On) return;
            foreach (var p in Targets(ctx).Distinct().ToList())
                if (Faiths.Holds(p)) Faiths.Steady(p, amount);
        }
    }

    /// <summary>A alone at the faith's altar or ideogram for an hour (a visit to the place), when the colony's faith has one here.</summary>
    public class DilemmaEffect_VisitAltar : DilemmaEffect_Visit
    {
        public DilemmaEffect_VisitAltar()
        {
            to = PlaySpot.Place;
            sit = true;
            duration = new FloatRange(0.75f, 1.25f);
        }

        private static Thing Altar(DilemmaContext ctx)
        {
            var faith = DecreeWork.ColonyFaith;
            var t = ctx?.map != null && faith != null ? FaithEvenings.Place(ctx.map, faith) : null;
            return t != null && t.Spawned && t.Map == ctx.map ? t : null;
        }

        public override string Unavailable(DilemmaContext ctx) => ctx?.map != null && Altar(ctx) == null ? "CC_Y4_NoAltar".Translate().ToString() : base.Unavailable(ctx);

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            var altar = Altar(ctx);
            if (altar == null || !base.Fill(p, ctx)) return false;
            p.place = altar;
            p.to = PlaySpot.Place;
            return true;
        }
    }

    // ===================================== The faith without a guide =====================================

    /// <summary>
    /// Ideology: nobody holds the moral guide's role in the colony's faith, and two of its believers could. A is the most
    /// devout of them with the best way with words, B the next.
    /// </summary>
    public class DilemmaWorker_NoGuide : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!Faiths.On || !CanHappenNow(map)) return false;
            var faith = DecreeWork.ColonyFaith;
            var role = YearFour.EmptyGuide(faith);
            if (role == null) return false;
            var pawns = YearFour.Grown(map);
            if (pawns.Count < def.minColonists) return false;
            var can = pawns.Where(p => def.roleA.Allows(p, null) && YearFour.CanGuide(role, p))
                .OrderByDescending(p => Faiths.Certainty(p) + VarietyThree.Level(p, SkillDefOf.Social) * 0.05f).ToList();
            if (can.Count < 2) return false;
            ctx = new FaithDilemmaContext { def = def, map = map, a = can[0], b = can[1], faithName = faith.name };
            return true;
        }
    }

    /// <summary>The faith's moral guide named (the game's own role): A, or B with <see cref="b"/>.</summary>
    public class DilemmaEffect_NameGuide : DilemmaEffect
    {
        public bool b;

        public override bool ChanceEach => false;

        private Pawn Who(DilemmaContext ctx) => b ? ctx?.b : ctx?.a;

        public override string Unavailable(DilemmaContext ctx)
        {
            var role = YearFour.EmptyGuide(DecreeWork.ColonyFaith);
            var p = Who(ctx);
            if (p == null) return null;
            return role == null ? "CC_Y4_GuideTaken".Translate().ToString() : !YearFour.CanGuide(role, p) ? "CC_Y4_CantGuide".Translate(p.LabelShort).ToString() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var p = Who(ctx);
            var role = YearFour.EmptyGuide(DecreeWork.ColonyFaith);
            if (p == null || role == null) return;
            sheet.Play("CC_Impact_NameGuide".Translate(p.LabelShort, role.LabelCap).Resolve(), "CC_Impact_NameGuideBrief".Translate(p.LabelShort).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var p = Who(ctx);
            var role = YearFour.EmptyGuide(DecreeWork.ColonyFaith);
            if (p == null || role == null || !YearFour.CanGuide(role, p)) return;
            try
            {
                role.Assign(p, true);
                Tell.Message(NewsKind.People, Importance.Notable, ctx.map, "CC_Y4_GuideNamed".Translate(p.Named("PAWN"), role.LabelCap.Named("ROLE")).Resolve(), new LookTargets(p),
                    MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("guide named", e);
            }
        }
    }
}
