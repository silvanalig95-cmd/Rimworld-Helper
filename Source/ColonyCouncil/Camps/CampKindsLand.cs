using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    [DefOf]
    public static class CampLandDefOf
    {
        public static JobDef Rimfolk_CampFish;
        public static JobDef Rimfolk_CampStrip;
        public static JobDef Rimfolk_CampReap;
        public static JobDef Rimfolk_CampTakeUp;
        public static JobDef Rimfolk_CampHull;
        public static JobDef Rimfolk_CampWatch;
        public static JobDef Council_CampVigil;
        public static ThoughtDef Rimfolk_Camp_SoldOurBeast;

        static CampLandDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CampLandDefOf));
        }
    }

    /// <summary>
    /// What the camps that come for something on the land look for (CampKindDef.needs) and where they pitch: water with
    /// fish (the game's own fish stock with Odyssey, or Vanilla Fishing Expanded's open water on its own), ancient pieces
    /// to strip outside the home area, a harvest the colony's growers can't bring in before the season ends, the map's
    /// edge (a craft come down there), Anomaly's woken monolith. Everything is read from the live map.
    /// </summary>
    public static class CampLand
    {
        public static bool Odyssey => ModsConfig.OdysseyActive;

        /// <summary>Vanilla Fishing Expanded on its own: its fish and open water, without Odyssey's fish stock.</summary>
        public static bool FishingMod => !ModsConfig.OdysseyActive && ModsConfig.IsActive("VanillaExpanded.VCEF");

        /// <summary>How far from their camp fishers go to the water.</summary>
        public const float FishRadius = 30f;

        /// <summary>How far stripping the ruin nearest the colony takes the salvagers from their camp.</summary>
        public const float ClearRadius = 70f;

        // ---------------- Can they come ----------------

        public static bool Fits(CampKindDef kind, Map map)
        {
            if (kind == null || map == null) return false;
            try
            {
                switch (kind.needs)
                {
                    case null:
                    case "": return true;
                    case "fish": return WishWorker_Fishing.CanFish(map) && FishTargets(map).Count > 0;
                    case "ruins": return RuinTargets(map).Count > 0;
                    case "harvest": return HarvestBehind(map);
                    case "edge": return true;
                    case "monolith": return Monolith(map) != null;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp land fits " + kind.defName, e);
            }
            return false;
        }

        /// <summary>
        /// Where a camp of this kind pitches: by the water, the ruins, the monolith or the map's edge it came for, else
        /// the spot the camp finder chose (<paramref name="usual"/>). Invalid when a kind that must pitch by something
        /// finds no room there.
        /// </summary>
        public static IntVec3 SiteFor(CampKindDef kind, Map map, IntVec3 usual, bool shade)
        {
            if (kind == null || map == null || kind.needs.NullOrEmpty() || kind.needs == "harvest") return usual;
            try
            {
                var targets = new List<IntVec3>();
                float min = 7f, max = 16f;
                switch (kind.needs)
                {
                    case "fish":
                        targets = FishTargets(map).Take(6).ToList();
                        break;
                    case "ruins":
                        targets = RuinTargets(map).Take(4).ToList();
                        min = 9f;
                        max = 20f;
                        break;
                    case "monolith":
                        var m = Monolith(map);
                        if (m != null) targets.Add(m.Position);
                        min = 16f;
                        max = 30f;
                        break;
                    case "edge":
                        targets = EdgeTargets(map);
                        min = 0f;
                        max = 12f;
                        break;
                }
                foreach (var t in targets)
                {
                    var c = TryFindNear(map, t, min, max, shade);
                    if (c.IsValid) return c;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp land site " + kind.defName, e);
            }
            return IntVec3.Invalid;
        }

        /// <summary>Their own work's target is theirs alone while they work on it (a piece of the ruins, a crop, their own stack).</summary>
        public static bool Reserves(JobDef def) =>
            def != null && (def == CampLandDefOf.Rimfolk_CampStrip || def == CampLandDefOf.Rimfolk_CampReap || def == CampLandDefOf.Rimfolk_CampTakeUp);

        /// <summary>
        /// A camp's own spot near <paramref name="target"/> (between <paramref name="min"/> and <paramref name="max"/>
        /// cells away), by the camp finder's own rules: open ground well clear of the colony and of neighbours settled for
        /// good, room for the whole camp, a way in from the edge and a way to the colony.
        /// </summary>
        public static IntVec3 TryFindNear(Map map, IntVec3 target, float min, float max, bool shade)
        {
            if (map == null || !target.IsValid) return IntVec3.Invalid;
            var pts = CampSite.ColonyPoints(map);
            int margin = CampSite.CoreRadius + 8;
            if (map.Size.x < margin * 2 + 20 || map.Size.z < margin * 2 + 20) return IntVec3.Invalid;
            var traverse = TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false);
            int minSq = CampSite.MinFromColony * CampSite.MinFromColony;
            int maxSq = (CampSite.MaxFromColony + 30) * (CampSite.MaxFromColony + 30);
            var best = IntVec3.Invalid;
            float bestScore = float.MinValue;
            int r = Mathf.CeilToInt(max);
            for (int i = 0; i < 140; i++)
            {
                var c = new IntVec3(target.x + Rand.RangeInclusive(-r, r), 0, target.z + Rand.RangeInclusive(-r, r));
                float d = c.DistanceTo(target);
                if (d < min || d > max) continue;
                if (c.x < margin || c.z < margin || c.x >= map.Size.x - margin || c.z >= map.Size.z - margin) continue;
                if (!CampSite.OpenGround(c, map)) continue;
                var h = CampTracker.HoldingAt(map);
                if (h != null && h.map == map && h.center.IsValid && c.InHorDistOf(h.center, CampSite.CoreRadius * 2 + 10)) continue;
                int dsq = pts.Count > 0 ? MinDistSquared(c, pts) : int.MaxValue;
                if (dsq < minSq || (pts.Count > 0 && dsq > maxSq)) continue;
                int open = 0, total = 0;
                bool blocked = false;
                foreach (var x in GenRadial.RadialCellsAround(c, CampSite.CoreRadius, true))
                {
                    total++;
                    if (CampSite.OpenGround(x, map)) open++;
                    else if (x.InBounds(map) && (map.zoneManager.ZoneAt(x) != null || (map.areaManager.Home != null && map.areaManager.Home[x])))
                    {
                        blocked = true;
                        break;
                    }
                }
                if (blocked || open < total * 0.8f) continue;
                if (!map.reachability.CanReachMapEdge(c, traverse) || !map.reachability.CanReachColony(c)) continue;
                int trees = 0;
                if (shade)
                    foreach (var x in GenRadial.RadialCellsAround(c, 10f, false))
                        if (x.InBounds(map) && x.GetPlant(map) is Plant p && p.def.plant.IsTree) trees++;
                float score = -d + Mathf.Min(trees, 20) * 0.4f + Rand.Range(0f, 4f);
                if (score <= bestScore) continue;
                bestScore = score;
                best = c;
            }
            return best;
        }

        private static int MinDistSquared(IntVec3 c, List<IntVec3> pts)
        {
            int best = int.MaxValue;
            foreach (var p in pts)
            {
                int dx = p.x - c.x, dz = p.z - c.z;
                int d = dx * dx + dz * dz;
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>Well clear of the colony: no zone, nothing planned or marked there, nowhere near the home area (as a camp keeps).</summary>
        public static bool ClearOfColony(Map map, IntVec3 c)
        {
            if (map == null || !c.InBounds(map)) return false;
            if (map.zoneManager.ZoneAt(c) != null || CampSite.Marked(c, map)) return false;
            var home = map.areaManager.Home;
            if (home != null)
                foreach (var x in GenRadial.RadialCellsAround(c, CampSite.HomeMargin, true))
                    if (x.InBounds(map) && home[x]) return false;
            return true;
        }

        // ---------------- Fish ----------------

        /// <summary>Water here holds fish to catch: with Odyssey the water's own fish stock (and not frozen over); with the fishing mod alone, any open water.</summary>
        public static bool Fishable(Map map, IntVec3 c)
        {
            if (map == null || !c.InBounds(map)) return false;
            var t = c.GetTerrain(map);
            if (t == null || !t.IsWater) return false;
            if (Odyssey) return map.waterBodyTracker != null && map.waterBodyTracker.AnyFishPopulationAt(c);
            return FishingMod;
        }

        /// <summary>How many fish the water at this cell holds now (Odyssey), for the richest water; 0 without the game's fish stock.</summary>
        public static float FishAt(Map map, IntVec3 c) => Odyssey && map?.waterBodyTracker != null ? map.waterBodyTracker.FishPopulationAt(c) : 0f;

        /// <summary>Water with fish well clear of the colony, sampled across the map, the richest first: where a fishing camp could pitch.</summary>
        public static List<IntVec3> FishTargets(Map map)
        {
            var list = new List<IntVec3>();
            if (map == null || (!Odyssey && !FishingMod)) return list;
            var pts = CampSite.ColonyPoints(map);
            int minSq = 30 * 30;
            for (int x = 4; x < map.Size.x - 4; x += 4)
                for (int z = 4; z < map.Size.z - 4; z += 4)
                {
                    var c = new IntVec3(x, 0, z);
                    if (c.Fogged(map) || !Fishable(map, c)) continue;
                    if (pts.Count > 0 && MinDistSquared(c, pts) < minSq) continue;
                    list.Add(c);
                }
            return list.OrderByDescending(c => FishAt(map, c)).ThenBy(c => Rand.Value).ToList();
        }

        /// <summary>The fish the fishing mod brings, on its own (its raw fish, by the category it keeps them in).</summary>
        public static List<ThingDef> ModFish()
        {
            if (modFish != null) return modFish;
            modFish = new List<ThingDef>();
            try
            {
                foreach (var cat in DefDatabase<ThingCategoryDef>.AllDefsListForReading.Where(c => c.defName.IndexOf("Fish", StringComparison.OrdinalIgnoreCase) >= 0))
                    foreach (var d in cat.DescendantThingDefs)
                    {
                        var ing = d?.ingestible;
                        if (ing == null || !d.IsNutritionGivingIngestible || !ing.HumanEdible) continue;
                        if (ing.preferability != FoodPreferability.RawBad && ing.preferability != FoodPreferability.RawTasty) continue;
                        if (!modFish.Contains(d)) modFish.Add(d);
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp fish of the fishing mod", e);
            }
            return modFish;
        }

        private static List<ThingDef> modFish;

        /// <summary>A fish: Odyssey's (its fish category), or the fishing mod's.</summary>
        public static bool IsFish(ThingDef d)
        {
            if (d == null || !d.IsIngestible) return false;
            var cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("Fish");
            if (cat != null && d.IsWithinCategory(cat)) return true;
            return ModFish().Contains(d);
        }

        /// <summary>A fishing zone of the colony's (the game's or a mod's), by its class name so a missing mod costs nothing.</summary>
        public static bool IsFishingZone(Zone z) => z != null && (z is Zone_Fishing || z.GetType().Name.IndexOf("Fishing", StringComparison.Ordinal) >= 0);

        /// <summary>Every water body (Odyssey) with fish in it and not frozen over.</summary>
        public static IEnumerable<WaterBody> Bodies(Map map)
        {
            var bodies = map?.waterBodyTracker?.Bodies;
            if (bodies == null || !Odyssey) yield break;
            foreach (var b in bodies)
                if (b != null && b.HasFish && !b.TotallyFrozen) yield return b;
        }

        /// <summary>The run: every water body with fish stocked to the full (the game's own stock, as much as the water holds).</summary>
        public static void FillWaters(Map map)
        {
            try
            {
                foreach (var b in Bodies(map).ToList())
                    if (b.Population < b.MaxPopulation) b.Population = b.MaxPopulation;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the run's fish", e);
            }
        }

        // ---------------- The ruins ----------------

        /// <summary>
        /// A piece of the ancient ruins a scrap crew may take apart: a building nobody owns (an ancient wall, junk, mech
        /// remains), made of something that comes back when it's taken down; never natural rock, anything that holds a
        /// thing or a sleeper, a vent, anything that explodes, can be hacked or studied, anything a quest or the colony
        /// has a claim on, nor anything that would bring a roof down or open a sealed room.
        /// </summary>
        public static bool Salvageable(Building b, Map map)
        {
            if (b == null || !b.Spawned || map == null || b.Map != map || b.Faction != null || b.def?.building == null || b.def.category != ThingCategory.Building) return false;
            var bp = b.def.building;
            if (bp.isNaturalRock || bp.isResourceRock || !b.def.destroyable || !b.def.useHitPoints || b.def.IsFrame) return false;
            if (b is IThingHolder || b is Building_Casket || b is Building_Grave || b is Building_VoidMonolith) return false;
            if (b.def.HasComp(typeof(CompExplosive)) || b.def.HasComp(typeof(CompHackable)) || b.def.HasComp(typeof(CompStudiable))) return false;
            if (!b.questTags.NullOrEmpty()) return false;
            string n = b.def.defName;
            if (n.IndexOf("Vent", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Monolith", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Shrine", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (map.designationManager.DesignationOn(b) != null) return false;
            if (!Yields(b)) return false;
            var rect = b.OccupiedRect().ExpandedBy(1);
            foreach (var c in rect)
                if (c.InBounds(map) && c.Fogged(map)) return false;
            if (b.def.holdsRoof)
                foreach (var c in GenRadial.RadialCellsAround(b.Position, 7f, true))
                    if (c.InBounds(map) && c.Roofed(map)) return false;
            return true;
        }

        /// <summary>Something comes back when it's taken down: what it was made of, or what it leaves when broken up.</summary>
        public static bool Yields(Building b) =>
            DeconstructYields(b) || !b.def.killedLeavings.NullOrEmpty();

        public static bool DeconstructYields(Building b) =>
            b.def.building.IsDeconstructible && (!b.def.CostList.NullOrEmpty() || (b.def.costStuffCount > 0 && b.Stuff != null));

        /// <summary>The ruins' pieces on the colony's land outside its home area (clear of its zones and plans).</summary>
        public static List<Building> Ruins(Map map)
        {
            var list = new List<Building>();
            if (map == null) return list;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
                if (t is Building b && Salvageable(b, map) && ClearOfColony(map, b.Position)) list.Add(b);
            return list;
        }

        /// <summary>Where the ruins lie thickest: pieces with at least three more within a dozen cells, the thickest first (six pieces at least in all).</summary>
        public static List<IntVec3> RuinTargets(Map map)
        {
            var ruins = Ruins(map);
            var list = new List<IntVec3>();
            if (ruins.Count < 6) return list;
            var scored = new List<(IntVec3 c, int n)>();
            foreach (var b in ruins.Take(400))
            {
                int near = ruins.Count(o => o != b && o.Position.InHorDistOf(b.Position, 12f));
                if (near >= 3) scored.Add((b.Position, near));
            }
            if (scored.Count < 6) return list;
            foreach (var s in scored.OrderByDescending(s => s.n))
            {
                if (list.Any(c => c.InHorDistOf(s.c, 20f))) continue;
                list.Add(s.c);
                if (list.Count >= 4) break;
            }
            return list;
        }

        /// <summary>Salvage: metal, stone blocks, slag and parts, as the ruins give them up (not a camp's own wood and cloth).</summary>
        public static bool IsSalvage(ThingDef d)
        {
            if (d == null || d.category != ThingCategory.Item) return false;
            if (d == ThingDefOf.ComponentIndustrial || d == ThingDefOf.ComponentSpacer || d == ThingDefOf.Plasteel || d == ThingDefOf.Steel) return true;
            if (d.stuffProps?.categories != null && d.stuffProps.categories.Contains(StuffCategoryDefOf.Metallic)) return true;
            return d.IsWithinCategory(ThingCategoryDefOf.Chunks) || d.IsWithinCategory(ThingCategoryDefOf.StoneBlocks);
        }

        // ---------------- The harvest ----------------

        /// <summary>The colony's crops in its growing zones: sown, a crop (not a tree) that gives something when harvested.</summary>
        public static IEnumerable<Plant> Crops(Map map)
        {
            if (map?.zoneManager == null) yield break;
            foreach (var z in map.zoneManager.AllZones.OfType<Zone_Growing>())
                foreach (var c in z.Cells)
                {
                    var p = c.GetPlant(map);
                    if (p == null || !p.sown || p.def.plant == null || p.def.plant.IsTree || !p.def.plant.Harvestable || p.def.plant.harvestedThingDef == null) continue;
                    yield return p;
                }
        }

        /// <summary>
        /// More crop than the growers can bring in before the season ends: the plants ripe or ripening (two thirds grown or
        /// more), the hours they take against the growers' hours (each colonist on growing work, at their own plant work
        /// speed), with the season's end within a fortnight or so.
        /// </summary>
        public static bool HarvestBehind(Map map)
        {
            if (map == null || !SeasonUtil.HasSeasons(map)) return false;
            SeasonUtil.Winter(map, out float daysLeft, out _, out bool inWinter);
            if (inWinter || daysLeft <= 0f || daysLeft > CampLandMath.MaxDaysLeft) return false;
            int plants = 0;
            float work = 0f;
            foreach (var p in Crops(map))
            {
                if (p.Growth < 0.65f) continue;
                plants++;
                work += p.def.plant.harvestWork;
            }
            float speed = 0f;
            foreach (var c in map.mapPawns.FreeColonistsSpawned)
                if (!c.WorkTypeIsDisabled(WorkTypeDefOf.Growing) && c.workSettings != null && c.workSettings.WorkIsActive(WorkTypeDefOf.Growing))
                    speed += c.GetStatValue(StatDefOf.PlantWorkSpeed);
            return CampLandMath.HarvestBehind(CampLandMath.RipeHours(work), plants, speed, daysLeft);
        }

        // ---------------- The edge, the monolith ----------------

        /// <summary>Spots a little in from the map's edges, the side farthest from the colony first: where a craft came down.</summary>
        public static List<IntVec3> EdgeTargets(Map map)
        {
            var colony = CampSite.ColonyCenter(map);
            int m = CampSite.CoreRadius + 10;
            var list = new List<IntVec3>
            {
                new IntVec3(m, 0, Rand.Range(m, map.Size.z - m)), new IntVec3(map.Size.x - m - 1, 0, Rand.Range(m, map.Size.z - m)),
                new IntVec3(Rand.Range(m, map.Size.x - m), 0, m), new IntVec3(Rand.Range(m, map.Size.x - m), 0, map.Size.z - m - 1)
            };
            return list.Where(c => c.InBounds(map)).OrderByDescending(c => c.DistanceToSquared(colony)).ToList();
        }

        /// <summary>Anomaly's monolith on this map, once it has woken (its level past the first).</summary>
        public static Building Monolith(Map map)
        {
            if (map == null || !ModsConfig.AnomalyActive || Find.Anomaly == null || Find.Anomaly.Level < 1) return null;
            var def = ThingDefOf.VoidMonolith;
            if (def == null) return null;
            return map.listerThings.ThingsOfDef(def).OfType<Building>().FirstOrDefault(b => b.Spawned);
        }

        /// <summary>Rain, snow or a storm heavy enough to want a roof.</summary>
        public static bool StormNow(Map map)
        {
            var w = map?.weatherManager?.curWeather;
            return w != null && (w.rainRate >= 0.5f || w.snowRate >= 0.5f);
        }

        // ---------------- The beast show ----------------

        /// <summary>A beast for a travelling show by its trade tags: tame-able, no predator, insect, dryad, thing of the void or anything thrumbo-sized.</summary>
        public static bool ShowBeast(PawnKindDef k, List<string> tags)
        {
            var race = k?.race;
            var rp = race?.race;
            if (rp == null || !rp.Animal || rp.predator || rp.Insect || rp.Dryad || rp.IsMechanoid || rp.IsAnomalyEntity || rp.Humanlike) return false;
            if (rp.baseBodySize < 0.3f || rp.baseBodySize > 4.1f || race.defName.IndexOf("Thrumbo", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return race.tradeTags != null && tags != null && race.tradeTags.Any(tags.Contains);
        }

        /// <summary>
        /// The colony's beast the show's keepers would buy: a grown, sound beast of the colony's out of any pen (they lead
        /// it away), worth something, never one bonded to anyone; the most valuable first.
        /// </summary>
        public static Pawn OurBeast(Map map)
        {
            if (map == null) return null;
            return map.mapPawns.SpawnedColonyAnimals
                .Where(a => a.RaceProps.Animal && !a.Downed && !a.InMentalState && !a.Dead && a.RaceProps.baseBodySize >= 0.3f && !a.RaceProps.Dryad && !a.RaceProps.Insect
                            && a.ageTracker?.CurLifeStage != null && a.ageTracker.CurLifeStage.reproductive && a.MarketValue >= 50f
                            && a.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond) == null && !a.IsQuestLodger()
                            && AnimalPenUtility.GetCurrentPenOf(a, false) == null && !(a.health?.hediffSet?.HasHediff(HediffDefOf.Pregnant) ?? false))
                .OrderByDescending(a => a.MarketValue).ThenBy(a => a.thingIDNumber).FirstOrDefault();
        }

        /// <summary>The colony's children old enough to help (Biotech): not babies, up and about.</summary>
        public static List<Pawn> Children(Map map) =>
            map == null ? new List<Pawn>() : IncidentWorker_Happening.Colonists(map).Where(p => p.DevelopmentalStage == DevelopmentalStage.Child && !p.Downed && p.Spawned).ToList();

        /// <summary>A study of the unnatural under way that the watchers' notes can add to (Anomaly).</summary>
        public static bool StudyOpen() =>
            ModsConfig.AnomalyActive && KnowledgeCategoryDefOf.Basic != null && Find.ResearchManager?.GetProject(KnowledgeCategoryDefOf.Basic) != null;
    }

    // The camps that come for something on the land (CampKindDef.needs and work): fishers at the water, a scrap crew at
    // the ancient ruins, harvest hands for the colony's fields, spacers by their wrecked craft, watchers of the monolith.
    // Their day's work, what the colony's asks set going, and what they leave.
    public partial class Camp
    {
        /// <summary>The fishers' catch the colony buys, carried to its gate; the salvagers' share of the haul (CampCarry).</summary>
        public const string CatchSale = "catch";
        public const string HaulShare = "haul";

        /// <summary>The day their own work was last counted, and how many times they did it that day.</summary>
        public int workDay = -1, workToday;
        /// <summary>What they took from the land this stay: fish caught, pieces of the ruins taken down, crops reaped (and the reapers' share).</summary>
        public int fishCaught, piecesStripped, reapedPlants, reapedUnits, reapShareUnits;
        /// <summary>Fishers keep to the far water; salvagers start on the ruin nearest the colony.</summary>
        public bool farWater, clearNear;
        /// <summary>How the harvest hands are paid ("sheaf", "silver", "meals"; null: not taken on yet), whether they offered, the sheaf owed, hours with nothing left to reap, and the day supper went over.</summary>
        public string reapPay;
        public bool offerMade;
        public float reapCarry;
        public int reapIdleHours, mealDay = -1;
        /// <summary>The spacers' wrecked craft, and the hours they worked at it.</summary>
        public Thing hull;
        public int hullWork;
        /// <summary>The night the watchers sleep in the colony's beds out of the storm (-1: none), and the day they last asked.</summary>
        public int shelterDay = -1, shelterAskDay = -1;

        [Unsaved] private List<(IntVec3 stand, IntVec3 water)> fishSpots;
        [Unsaved] private int fishSpotsTick = -999999;
        [Unsaved] private bool fishSpotsFar;
        [Unsaved] private int landNextSearch;

        private void ExposeLand()
        {
            if (Scribe.mode == LoadSaveMode.Saving && hull != null && hull.Destroyed) hull = null;
            Scribe_Values.Look(ref workDay, "workDay", -1);
            Scribe_Values.Look(ref workToday, "workToday");
            Scribe_Values.Look(ref fishCaught, "fishCaught");
            Scribe_Values.Look(ref piecesStripped, "piecesStripped");
            Scribe_Values.Look(ref reapedPlants, "reapedPlants");
            Scribe_Values.Look(ref reapedUnits, "reapedUnits");
            Scribe_Values.Look(ref reapShareUnits, "reapShareUnits");
            Scribe_Values.Look(ref farWater, "farWater");
            Scribe_Values.Look(ref clearNear, "clearNear");
            Scribe_Values.Look(ref reapPay, "reapPay");
            Scribe_Values.Look(ref offerMade, "offerMade");
            Scribe_Values.Look(ref reapCarry, "reapCarry");
            Scribe_Values.Look(ref reapIdleHours, "reapIdleHours");
            Scribe_Values.Look(ref mealDay, "mealDay", -1);
            Scribe_References.Look(ref hull, "hull");
            Scribe_Values.Look(ref hullWork, "hullWork");
            Scribe_Values.Look(ref shelterDay, "shelterDay", -1);
            Scribe_Values.Look(ref shelterAskDay, "shelterAskDay", -1);
        }

        private int Adults => Here.Count(p => !p.DevelopmentalStage.Juvenile() && !p.Downed);

        /// <summary>How far from their camp they fetch their own goods back to it: farther for salvagers paid to clear the ruin nearest the colony.</summary>
        public float FetchRadius => clearNear ? CampLand.ClearRadius : CampSite.WorkRadius;

        private bool Targeted(Thing t) => t != null && Here.Any(p => p.CurJob != null && p.CurJob.targetA.Thing == t);

        /// <summary>What the arrival letter says of their own work, if their kind has one.</summary>
        public string LandFacts()
        {
            string key = kind?.work.NullOrEmpty() ?? true ? null : "CC_CampLand_Facts_" + kind.work;
            return key != null && key.CanTranslate() ? Text(key) : null;
        }

        // ---------------- Arriving and going ----------------

        /// <summary>The spacers' craft lies where it came down, beside their camp: a ship's chunk of hull, theirs while they stay.</summary>
        public void LandStarted()
        {
            if (kind?.work != "hull" || map == null || hull != null) return;
            try
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail("ShipChunk");
                if (def == null) return;
                var taken = new HashSet<IntVec3>(plans.Where(p => p.def != null).SelectMany(p => p.Rect.ExpandedBy(1)));
                foreach (var c in GenRadial.RadialCellsAround(stash, 2.9f, true)) taken.Add(c);
                var near = (center + Away * 9).ClampInsideMap(map);
                foreach (var c in GenRadial.RadialCellsAround(near, 6f, true))
                {
                    var rect = GenAdj.OccupiedRect(c, Rot4.North, def.size);
                    if (!rect.All(x => x.InBounds(map) && CampSite.OpenGround(x, map) && !taken.Contains(x) && x.GetFirstItem(map) == null && x.GetFirstPawn(map) == null)) continue;
                    var t = ThingMaker.MakeThing(def);
                    var put = GenSpawn.Spawn(t, c, map, Rot4.North);
                    if (put.def.CanHaveFaction && faction != null) put.SetFaction(faction);
                    hull = put;
                    return;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp spacers' hull", e);
            }
        }

        /// <summary>They go: beds lent for the night given back, and the wrecked craft left for the colony to take apart.</summary>
        private void LandLeaving()
        {
            try
            {
                if (hull != null && !hull.Destroyed && hull.Spawned && faction != null && hull.Faction == faction)
                {
                    hull.SetFaction(null);
                    Note(Text("CC_CampLand_HullLeft"), MessageTypeDefOf.NeutralEvent, Importance.Notable, hull, true);
                }
                if (kind?.work == "watch")
                {
                    foreach (var p in members) Hosting.GiveBack(p, map);
                    shelterDay = -1;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp land leaving", e);
            }
        }

        // ---------------- Every hour ----------------

        private void LandHourly()
        {
            if (kind?.work == null || map == null || phase != CampPhase.Living || Hostile) return;
            try
            {
                int hour = GenLocalDate.HourOfDay(map);
                if (kind.work == "reap") ReapHourly(hour);
                else if (kind.work == "watch") WatchHourly(hour);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp land hourly " + kind.work, e);
            }
        }

        private bool LetterOpen(string key = null) =>
            Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_Camp c && c.campId == id && !c.resolved && (key == null || c.askKey == key));

        /// <summary>
        /// The harvest hands offer their work once the camp stands (a letter); turned down, they move on to the next farm.
        /// Taken on, they reap by day; supper is carried over each evening they worked, if that was the bargain; with
        /// nothing left ripe to reap, they finish within a few hours.
        /// </summary>
        private void ReapHourly(int hour)
        {
            if (!offerMade)
            {
                var ask = AskByKey("work");
                if (ask == null || LetterOpen()) return;
                offerMade = true;
                if (AskBlocked(ask) == null) Request(ask);
                return;
            }
            if (reapPay == null)
            {
                if (asksMade.Contains("work") && !LetterOpen("work")) Leave("declined");
                return;
            }
            if (hour >= 8 && hour < 17)
            {
                if (Crops(true).Count == 0)
                {
                    reapIdleHours++;
                    if (reapIdleHours >= 3 && reapedPlants > 0 && untilTick > Now + GenDate.TicksPerHour * 3) untilTick = Now + GenDate.TicksPerHour * 3;
                }
                else reapIdleHours = 0;
            }
            int day = GenDate.DaysPassed;
            if (reapPay == "meals" && hour >= 18 && hour <= 20 && mealDay != day && workDay == day && workToday > 0) Supper(day);
        }

        /// <summary>The harvest hands' hot supper, carried over to their camp by one of the colony; without meals in the stores, they go without and take it badly.</summary>
        private void Supper(int day)
        {
            mealDay = day;
            int n = Mathf.Max(1, Adults);
            var carry = new DilemmaEffect_CarryOut
            {
                what = "meals", count = n, to = PlaySpot.Camp, who = PlayWho.None, steward = false,
                label = "CC_CampReap_SupperLabel".Translate(Label.Named("NAME")).Resolve()
            };
            var ctx = new DilemmaContext { map = map, faction = faction, stranger = Speaker };
            if (carry.Unavailable(ctx) != null)
            {
                AddTension(8f);
                Note(Text("CC_CampReap_NoSupper"), MessageTypeDefOf.NegativeEvent, Importance.Notable);
                return;
            }
            // apart: a colonist carries the meals over to their camp, where one of them takes them.
            carry.Apply(ctx);
            AddTension(-2f);
        }

        /// <summary>
        /// The watchers: beds given back in the morning after a night in out of the storm; and on a stormy evening they
        /// ask to sleep under the colony's roof (a letter).
        /// </summary>
        private void WatchHourly(int hour)
        {
            int day = GenDate.DaysPassed;
            if (shelterDay >= 0 && (day > shelterDay + 1 || (day > shelterDay && hour >= 7)))
            {
                foreach (var p in members) Hosting.GiveBack(p, map);
                shelterDay = -1;
            }
            if (hour < 17 || hour > 21 || shelterDay >= 0 || shelterAskDay == day || !CampLand.StormNow(map) || LetterOpen()) return;
            shelterAskDay = day;
            var ask = AskByKey("shelter");
            if (ask != null && AskBlocked(ask) == null) Request(ask);
        }

        // ---------------- Their day's work ----------------

        /// <summary>Their own work on the land by day, or null (CampWork.Day asks, between the market and the hunt).</summary>
        public Job LandJob(Pawn p)
        {
            if (kind?.work == null || map == null || phase != CampPhase.Living || p == trader) return null;
            int day = GenDate.DaysPassed;
            if (workDay != day)
            {
                workDay = day;
                workToday = 0;
            }
            try
            {
                switch (kind.work)
                {
                    case "fish": return FishJob(p);
                    case "strip": return StripJob(p);
                    case "reap": return ReapJob(p);
                    case "hull":
                        // Their pilot is at the hull most of the day (where a colonist lent to them works beside them).
                        if (hull == null || hull.Destroyed || !hull.Spawned || !Rand.Chance(p == leader ? 0.85f : 0.6f) || !p.CanReach(hull, PathEndMode.Touch, Danger.Some)) return null;
                        SayMoment(p, "hull", 0.25f);
                        return JobMaker.MakeJob(CampLandDefOf.Rimfolk_CampHull, hull);
                    case "watch":
                    {
                        var m = CampLand.Monolith(map);
                        if (m == null || !Rand.Chance(0.25f)) return null;
                        var v = (m.Position - center).ToVector3();
                        v.Normalize();
                        var toward = center + new IntVec3(Mathf.RoundToInt(v.x * 5f), 0, Mathf.RoundToInt(v.z * 5f));
                        if (!CellFinder.TryFindRandomCellNear(toward.ClampInsideMap(map), map, 2, c => c.Standable(map) && c.GetEdifice(map) == null && c.GetFirstPawn(map) == null
                                                                                                      && p.CanReach(c, PathEndMode.OnCell, Danger.Some), out var cell)) return null;
                        return JobMaker.MakeJob(CampLandDefOf.Rimfolk_CampWatch, cell, m);
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp land work " + kind.work, e);
            }
            return null;
        }

        /// <summary>The night's own work: a bed in the colony out of the storm, else the watch at the monolith by turns.</summary>
        public Job LandNightJob(Pawn p, int hour)
        {
            if (kind?.work != "watch" || map == null || phase != CampPhase.Living || Hostile) return null;
            try
            {
                if (shelterDay >= 0 && (hour >= 20 || hour < 7))
                {
                    var bed = ShelterJob(p);
                    if (bed != null) return bed;
                }
                if ((hour >= 22 || hour < 5) && OnWatch(p))
                {
                    var m = CampLand.Monolith(map);
                    if (m == null) return null;
                    if (!CellFinder.TryFindRandomCellNear(m.Position, map, 13, c => !c.InHorDistOf(m.Position, 7f) && c.Standable(map) && !c.Roofed(map) && c.GetEdifice(map) == null
                                                                                 && c.GetFirstPawn(map) == null && p.CanReach(c, PathEndMode.OnCell, Danger.Some), out var cell)) return null;
                    SayMoment(p, "watch", 0.2f);
                    return JobMaker.MakeJob(CampLandDefOf.Council_CampVigil, cell, m);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp watch", e);
            }
            return null;
        }

        /// <summary>The one or two of them keeping watch tonight: the most rested, by turns as they tire.</summary>
        private bool OnWatch(Pawn p)
        {
            var rest = p.needs?.rest;
            if (rest != null && rest.CurLevelPercentage < 0.25f) return false;
            var here = Here.Where(x => !x.DevelopmentalStage.Juvenile() && !x.Downed && x != trader).ToList();
            int n = here.Count >= 4 ? 2 : 1;
            return here.OrderByDescending(x => x.needs?.rest?.CurLevelPercentage ?? 1f).ThenBy(x => x.thingIDNumber).Take(n).Contains(p);
        }

        /// <summary>A free bed of the colony's for the night (the one they already have, else one nobody else is using), lain in until morning.</summary>
        private Job ShelterJob(Pawn p)
        {
            if (p.ownership == null) return null;
            var bed = p.ownership.OwnedBed;
            if (bed == null || bed.Faction != Faction.OfPlayer || !bed.Spawned)
            {
                bed = Hosting.Beds(map, x => Has(x), p)
                    .Where(b => b.OwnersForReading.Count < b.SleepingSlotsCount && Hosting.CanShare(b, p) && p.CanReserveAndReach(b, PathEndMode.OnCell, Danger.Some, b.SleepingSlotsCount, 0))
                    .OrderBy(b => b.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                if (bed == null || !p.ownership.ClaimBedIfNonMedical(bed)) return null;
            }
            var job = JobMaker.MakeJob(HostingDefOf.Council_GuestSleep, bed);
            job.expiryInterval = Mathf.Max(GenDate.TicksPerHour, Mathf.RoundToInt(Hosting.HoursToMorning(map) * GenDate.TicksPerHour));
            return job;
        }

        // ---------------- Fishing ----------------

        /// <summary>Spots by the water with fish, near their camp: where to stand and the water to fish. Never in the colony's own fishing zones; on the far water, never on the colony's side.</summary>
        private List<(IntVec3 stand, IntVec3 water)> FishSpots(bool far)
        {
            if (fishSpots != null && fishSpotsFar == far && Now - fishSpotsTick < GenDate.TicksPerDay / 2) return fishSpots;
            var list = new List<(IntVec3, IntVec3)>();
            var zoneCells = new List<IntVec3>();
            foreach (var z in map.zoneManager.AllZones.Where(CampLand.IsFishingZone))
                zoneCells.AddRange(z.Cells.Where(c => (c.x + c.z) % 3 == 0));
            var seen = new HashSet<IntVec3>();
            foreach (var w in GenRadial.RadialCellsAround(center, CampLand.FishRadius, true))
            {
                if (!w.InBounds(map) || map.zoneManager.ZoneAt(w) != null || !CampLand.Fishable(map, w)) continue;
                if (far && zoneCells.Any(z => z.InHorDistOf(w, 25f))) continue;
                foreach (var s in GenAdj.CardinalDirections.Select(d => w + d))
                {
                    if (!s.InBounds(map) || seen.Contains(s) || !s.Standable(map) || (s.GetTerrain(map)?.IsWater ?? true) || s.GetEdifice(map) != null) continue;
                    if (!ClearOfColony(s) || (far && ColonySide(s)) || (termBoundary && !bendBoundary && ColonySide(s))) continue;
                    seen.Add(s);
                    list.Add((s, w));
                    break;
                }
                if (list.Count >= 60) break;
            }
            var tp = TraverseParms.For(TraverseMode.PassDoors, Danger.Some, false);
            fishSpots = list.Where(x => map.reachability.CanReach(center, x.Item1, PathEndMode.OnCell, tp)).Take(20).ToList();
            fishSpotsTick = Now;
            fishSpotsFar = far;
            return fishSpots;
        }

        private Job FishJob(Pawn p)
        {
            if (workToday >= CampLandMath.FishTrips(Adults) || !Rand.Chance(0.6f) || Now < landNextSearch) return null;
            var spots = FishSpots(farWater).Where(x => x.stand.GetFirstPawn(map) == null && !Here.Any(o => o != p && o.CurJob?.def == CampLandDefOf.Rimfolk_CampFish && o.CurJob.targetA.Cell == x.stand)
                                                       && p.CanReach(x.stand, PathEndMode.OnCell, Danger.Some)).Take(5).ToList();
            if (spots.Count == 0)
            {
                landNextSearch = Now + GenDate.TicksPerHour;
                return null;
            }
            var spot = spots.RandomElement();
            return JobMaker.MakeJob(CampLandDefOf.Rimfolk_CampFish, spot.stand, spot.water);
        }

        /// <summary>
        /// A fishing trip done at the water's edge: the game's own catch (with Odyssey: as the colony's fishers catch, and the
        /// water's fish stock goes down by it), or a few of the fishing mod's fish; into their stash, theirs to cook and keep.
        /// </summary>
        private void Fished(Pawn p, IntVec3 water)
        {
            if (!p.Spawned || !water.IsValid || !p.Position.InHorDistOf(water, 2.5f) || !CampLand.Fishable(map, water)) return;
            var catches = new List<Thing>();
            if (CampLand.Odyssey)
            {
                var tracker = map.waterBodyTracker;
                float before = tracker.FishPopulationAt(water);
                catches.AddRange(FishingUtility.GetCatchesFor(p, water, false, out _));
                int fish = catches.Where(t => t.def.IsIngestible).Sum(t => t.stackCount);
                if (fish > 0 && Mathf.Approximately(before, tracker.FishPopulationAt(water))) tracker.Notify_Fished(water, fish);
            }
            else
            {
                var def = CampLand.ModFish().RandomElementWithFallback();
                if (def == null) return;
                var t = ThingMaker.MakeThing(def);
                t.stackCount = Mathf.Max(1, Mathf.RoundToInt(Rand.Range(3f, 7f) * (0.6f + (p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 4) * 0.05f)));
                catches.Add(t);
            }
            workToday++;
            int got = 0;
            foreach (var t in catches)
            {
                if (t == null || t.Destroyed) continue;
                int n = t.stackCount;
                if (GenPlace.TryPlaceThing(t, stash, map, ThingPlaceMode.Near, out var put, null, AtStash) && put != null)
                {
                    Own(put);
                    if (CampLand.IsFish(put.def)) got += n;
                }
            }
            if (got <= 0) return;
            fishCaught += got;
            AddTension(0.3f);
            SayMoment(p, "fish", 0.4f);
        }

        // ---------------- The ruins ----------------

        [Unsaved] private List<Building> ruinCache;
        [Unsaved] private int ruinTick = -999999;

        /// <summary>The ruins' pieces within reach of their camp that could be taken down, looked for again every hour.</summary>
        private List<Building> RuinPieces()
        {
            if (ruinCache != null && Now - ruinTick < GenDate.TicksPerHour)
            {
                ruinCache.RemoveAll(b => b == null || b.Destroyed || !b.Spawned);
                return ruinCache;
            }
            ruinCache = new List<Building>();
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
                if (t is Building b && t.Position.InHorDistOf(center, CampLand.ClearRadius) && CampLand.Salvageable(b, map) && ClearOfColony(b.Position)) ruinCache.Add(b);
            ruinTick = Now;
            return ruinCache;
        }

        /// <summary>The ruins' pieces they may take down now: near their camp, or (paid to) the ruin nearest the colony first.</summary>
        private List<Building> StripTargets(Pawn p)
        {
            var list = RuinPieces().Where(b => clearNear || MayWork(b.Position)).ToList();
            var colony = colonyCenter.IsValid ? colonyCenter : map.Center;
            return (clearNear ? list.OrderBy(b => b.Position.DistanceToSquared(colony)) : list.OrderBy(b => b.Position.DistanceToSquared(p.Position))).ToList();
        }

        private Job StripJob(Pawn p)
        {
            if (workToday >= CampLandMath.StripsADay(Adults) || !Rand.Chance(0.6f) || Now < landNextSearch || p.WorkTypeIsDisabled(WorkTypeDefOf.Construction)) return null;
            foreach (var b in StripTargets(p).Take(12))
            {
                if (Targeted(b) || !p.CanReserveAndReach(b, PathEndMode.Touch, Danger.Some)) continue;
                SayMoment(p, "strip", 0.3f);
                return JobMaker.MakeJob(CampLandDefOf.Rimfolk_CampStrip, b);
            }
            landNextSearch = Now + GenDate.TicksPerHour;
            return null;
        }

        /// <summary>
        /// A piece of the ruins taken down, as the game takes one apart (what it was made of comes back; junk that can't be
        /// taken apart is broken up for what it leaves): what it gives is theirs, carried to their stash by their own porters.
        /// </summary>
        private void Stripped(Pawn p, Building b)
        {
            if (b == null || !CampLand.Salvageable(b, map) || !p.Spawned || !b.OccupiedRect().ExpandedBy(2).Contains(p.Position)) return;
            var rect = b.OccupiedRect().ExpandedBy(1);
            var before = new HashSet<int>();
            foreach (var c in rect)
                if (c.InBounds(map))
                    foreach (var t in c.GetThingList(map))
                        before.Add(t.thingIDNumber);
            var mode = CampLand.DeconstructYields(b) ? DestroyMode.Deconstruct : DestroyMode.KillFinalize;
            b.Destroy(mode);
            int n = 0;
            foreach (var c in rect)
            {
                if (!c.InBounds(map)) continue;
                foreach (var t in c.GetThingList(map).ToList())
                {
                    if (before.Contains(t.thingIDNumber) || t.def.category != ThingCategory.Item || !t.def.EverHaulable) continue;
                    Own(t);
                    n += t.stackCount;
                }
            }
            workToday++;
            piecesStripped++;
            AddTension(0.6f);
        }

        // ---------------- The harvest ----------------

        [Unsaved] private List<Plant> ripeCache;
        [Unsaved] private int ripeTick = -999999;

        /// <summary>The colony's ripe crops (cached for an hour, or looked again when <paramref name="fresh"/>).</summary>
        private List<Plant> Crops(bool fresh = false)
        {
            if (!fresh && ripeCache != null && Now - ripeTick < GenDate.TicksPerHour) return ripeCache;
            ripeCache = CampLand.Crops(map).Where(x => x.HarvestableNow && map.designationManager.DesignationOn(x, DesignationDefOf.CutPlant) == null).ToList();
            ripeTick = Now;
            return ripeCache;
        }

        private Job ReapJob(Pawn p)
        {
            if (reapPay == null || p.WorkTypeIsDisabled(WorkTypeDefOf.Growing) || !Rand.Chance(0.85f)) return null;
            foreach (var plant in Crops().Where(x => x.Spawned && x.HarvestableNow).OrderBy(x => x.Position.DistanceToSquared(p.Position)).Take(15))
            {
                if (Targeted(plant) || !p.CanReserveAndReach(plant, PathEndMode.Touch, Danger.Some)) continue;
                return JobMaker.MakeJob(CampLandDefOf.Rimfolk_CampReap, plant);
            }
            return null;
        }

        /// <summary>
        /// A crop of the colony's reaped as the game harvests one: what it yields left where it grew for the colony's
        /// haulers, but for their sheaf in ten if that was the bargain, which goes in their own pack; the field is the
        /// colony's to sow again.
        /// </summary>
        private void Reaped(Pawn p, Plant plant)
        {
            if (plant == null || !plant.Spawned || !plant.HarvestableNow || p.inventory == null || !p.Position.InHorDistOf(plant.Position, 2f)) return;
            var def = plant.def.plant.harvestedThingDef;
            int n = plant.YieldNow();
            var cell = plant.Position;
            int share = reapPay == "sheaf" ? CampLandMath.Share(n, CampLandMath.Sheaf, ref reapCarry) : 0;
            plant.PlantCollected(p, PlantDestructionMode.Cut);
            if (def != null && n - share > 0)
            {
                var t = ThingMaker.MakeThing(def);
                t.stackCount = n - share;
                if (GenPlace.TryPlaceThing(t, cell, map, ThingPlaceMode.Near, out var put)) put?.SetForbidden(false, false);
            }
            if (def != null && share > 0)
            {
                var s = ThingMaker.MakeThing(def);
                s.stackCount = share;
                p.inventory.TryAddItemNotForSale(s);
            }
            workToday++;
            reapedPlants++;
            reapedUnits += n;
            reapShareUnits += share;
            if (reapedPlants % 12 == 1) SayMoment(p, "reap", 0.5f);
        }

        // ---------------- When their work is done ----------------

        public void LandJobDone(Pawn p, Job job)
        {
            var def = job?.def;
            if (def == null) return;
            if (def == CampLandDefOf.Rimfolk_CampFish) Fished(p, job.targetB.Cell);
            else if (def == CampLandDefOf.Rimfolk_CampStrip) Stripped(p, job.targetA.Thing as Building);
            else if (def == CampLandDefOf.Rimfolk_CampReap) Reaped(p, job.targetA.Thing as Plant);
            else if (def == CampLandDefOf.Rimfolk_CampTakeUp) TookFromStash(p, job.targetA.Thing);
            else if (def == CampLandDefOf.Rimfolk_CampHull) hullWork++;
        }

        // ---------------- Errands and asks ----------------

        /// <summary>Where the land kinds' errands go: "richwater" (the water with the most fish), "hull" (their craft).</summary>
        private IntVec3 LandErrandCell(string to, Pawn walker)
        {
            if (to == "hull") return hull != null && hull.Spawned ? PlaySpots.FreeCellNear(hull.Position, map, walker) : IntVec3.Invalid;
            // What travellers came for: the ruins, the ore, the relic, the books, the tree (CampSeekers.cs).
            if (to == SeekTo) return SeekErrandCell(walker);
            if (to != "richwater") return IntVec3.Invalid;
            var colony = colonyCenter.IsValid ? colonyCenter : map.Center;
            IntVec3 best = IntVec3.Invalid;
            float bestFish = -1f;
            foreach (var w in GenRadial.RadialCellsAround(colony, 50f, true))
            {
                if ((w.x + w.z) % 2 != 0 || !CampLand.Fishable(map, w)) continue;
                float f = CampLand.Odyssey ? CampLand.FishAt(map, w) : -w.DistanceToSquared(colony);
                if (f <= bestFish) continue;
                IntVec3 stand = IntVec3.Invalid;
                foreach (var s in GenAdj.CardinalDirections.Select(d => w + d))
                    if (s.InBounds(map) && s.Standable(map) && !(s.GetTerrain(map)?.IsWater ?? true) && s.GetEdifice(map) == null && (walker == null || walker.CanReach(s, PathEndMode.OnCell, Danger.Some)))
                    {
                        stand = s;
                        break;
                    }
                if (!stand.IsValid) continue;
                bestFish = f;
                best = stand;
            }
            return best;
        }

        /// <summary>Why one of the land kinds' asks can't be made now (null: it can), by what it needs.</summary>
        private string LandAskBlocked(CampAsk a)
        {
            switch (a?.needs)
            {
                case "theirCatch": return CatchSaleBlocked();
                case "farWater":
                    if (farWater) return "CC_CampAsk_Done".Translate();
                    return FishSpots(true).Count == 0 ? "CC_CampLand_NoFarWater".Translate().ToString() : null;
                case "theirHaul": return HaulShareBlocked();
                case "nearRuin":
                    if (clearNear) return "CC_CampAsk_Done".Translate();
                    return RuinPieces().Count == 0 ? "CC_CampLand_NoRuin".Translate().ToString() : null;
                case "notHired": return reapPay != null ? "CC_CampLand_Hired".Translate().ToString() : null;
                case "ourBeast": return CampLand.OurBeast(map) == null ? "CC_CampLand_NoBeast".Translate().ToString() : null;
                case "children": return CampLand.Children(map).Count == 0 ? "CC_CampLand_NoChildren".Translate().ToString() : null;
                case "study": return CampLand.StudyOpen() ? null : "CC_CampLand_NoStudy".Translate().ToString();
                case "hull": return hull == null || hull.Destroyed || !hull.Spawned ? "CC_CampLand_NoHull".Translate().ToString() : null;
                case "storm":
                    if (!CampLand.StormNow(map)) return "CC_CampLand_NoStorm".Translate();
                    return Hosting.Beds(map, x => Has(x), Speaker).Count == 0 ? "CC_CampLand_NoBeds".Translate().ToString() : null;
            }
            // What travellers came for: only when it brought them, and while it's still there (CampSeekers.cs).
            return SeekAskBlocked(a);
        }

        /// <summary>What an ask of theirs is for, for {THING}: their catch and what it comes to, or the colony's beast they'd buy.</summary>
        private string LandAskThing(CampAsk a)
        {
            if (a?.price == CatchSale) return CatchWords();
            if (a?.price == "beastsale")
            {
                var beast = CampLand.OurBeast(map);
                return beast == null ? "CC_CampLand_NoBeastWord".Translate().Resolve()
                    : "CC_CampLand_BeastOffer".Translate(beast.LabelShort, beast.def.label, CampLandMath.BeastPrice(beast.MarketValue)).Resolve();
            }
            return null;
        }

        // ---------------- The catch and the haul, carried over from their stash ----------------

        private static bool FromStash(string help) => help == CatchSale || help == HaulShare;

        /// <summary>The most of one kind of fish in their stash now, and how many.</summary>
        public int CatchOffer(out ThingDef def)
        {
            def = null;
            int best = 0;
            foreach (var g in Goods().Where(t => CampLand.IsFish(t.def)).GroupBy(t => t.def))
            {
                int n = g.Sum(t => t.stackCount);
                if (n <= best) continue;
                best = n;
                def = g.Key;
            }
            return best;
        }

        public int CatchCost() => VisitorTradeMath.MilkPrice(CatchOffer(out var def), def?.BaseMarketValue ?? 0f);

        private string CatchWords()
        {
            int n = CatchOffer(out var def);
            return n > 0 && def != null
                ? "CC_CampMilk_Offer".Translate(n, def.label, VisitorTradeMath.MilkPrice(n, def.BaseMarketValue)).Resolve()
                : "CC_CampLand_NoCatchWord".Translate().Resolve();
        }

        private Pawn Porter() => Free.Where(p => !p.WorkTagIsDisabled(WorkTags.Hauling) && p != trader).OrderBy(p => p == leader ? 1 : 0).FirstOrDefault();

        public string CatchSaleBlocked()
        {
            if (Pending(CatchSale)) return "CC_CampLand_CatchGoing".Translate();
            if (CatchOffer(out _) < 10) return "CC_CampLand_NoCatch".Translate();
            if (Porter() == null) return "CC_CampLand_NoPorter".Translate();
            return CanBring ? null : "CC_CampErrand_No_gate".Translate().ToString();
        }

        /// <summary>The colony buys their catch: one of them takes it up from their stash and carries it to the colony's gate, to be paid for there.</summary>
        public bool StartCatchSale()
        {
            int n = CatchOffer(out var def);
            var porter = Porter();
            if (n <= 0 || def == null || porter == null) return false;
            var gate = ErrandCell("gate", null, porter);
            if (!gate.IsValid) return false;
            carries.Add(new CampCarry { by = porter, help = CatchSale, def = def, want = n, to = gate, from = Now, until = Now + GenDate.TicksPerHour * 10 });
            return true;
        }

        /// <summary>What lies in their stash from the ruins, by kind, the most first.</summary>
        private List<(ThingDef def, int count)> Haul() =>
            Goods().Where(t => CampLand.IsSalvage(t.def)).GroupBy(t => t.def).Select(g => (g.Key, g.Sum(t => t.stackCount))).OrderByDescending(x => x.Item2).ToList();

        public string HaulShareBlocked()
        {
            if (Pending(HaulShare)) return "CC_CampLand_HaulGoing".Translate();
            if (!Haul().Any(x => CampLandMath.Half(x.count) >= 2)) return "CC_CampLand_NoHaul".Translate();
            if (Porter() == null) return "CC_CampLand_NoPorter".Translate();
            return CanBring ? null : "CC_CampErrand_No_gate".Translate().ToString();
        }

        /// <summary>Half of the two biggest piles of their haul, each taken up by one of them and carried to the colony's gate.</summary>
        public bool StartHaulShare()
        {
            bool any = false;
            foreach (var (def, count) in Haul().Take(2))
            {
                int half = CampLandMath.Half(count);
                var porter = Porter();
                if (half < 2 || porter == null) break;
                var gate = ErrandCell("gate", null, porter);
                if (!gate.IsValid) break;
                carries.Add(new CampCarry { by = porter, help = HaulShare, def = def, want = half, to = gate, from = Now, until = Now + GenDate.TicksPerHour * 10 });
                any = true;
            }
            return any;
        }

        /// <summary>The next stack of theirs to take up for what they're bringing, while they have less than they're to bring.</summary>
        private Job StashJob(Pawn p, CampCarry c)
        {
            if (c.count >= c.want || c.def == null) return null;
            var stack = Goods().Where(t => t.def == c.def && t.Spawned && !Targeted(t) && p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some))
                .OrderBy(t => t.Position.DistanceToSquared(p.Position)).FirstOrDefault();
            return stack == null ? null : JobMaker.MakeJob(CampLandDefOf.Rimfolk_CampTakeUp, stack);
        }

        /// <summary>A stack taken up from their stash into their pack, as much of it as they're still to bring.</summary>
        private void TookFromStash(Pawn p, Thing t)
        {
            var c = CarryOf(p);
            if (c == null || c.carrying || t == null || t.Destroyed || !t.Spawned || !Owns(t) || t.def != c.def || p.inventory == null) return;
            int k = Mathf.Min(c.want - c.count, t.stackCount);
            if (k <= 0) return;
            var part = t.SplitOff(k);
            if (part.Spawned) part.DeSpawn();
            Disown(part);
            p.inventory.TryAddItemNotForSale(part);
            if (!p.inventory.innerContainer.Contains(part))
            {
                // No room in their pack: back on the pile, theirs as before.
                if (!part.Destroyed && !part.Spawned && GenPlace.TryPlaceThing(part, stash, map, ThingPlaceMode.Near, out var back)) Own(back);
                return;
            }
            c.count += k;
            c.stops++;
            c.taken = true;
        }

        /// <summary>The catch or the share set down by the colony's gate: the catch is paid for at its worth, the share is told.</summary>
        private void StashBrought(Pawn p, CampCarry c, int n, Thing look)
        {
            if (c.help == HaulShare)
            {
                Note(Text("CC_CampLand_HaulBrought", p, null, n, c.def.label), MessageTypeDefOf.PositiveEvent, Importance.Notable, look, true);
                return;
            }
            int price = VisitorTradeMath.MilkPrice(n, c.def.BaseMarketValue);
            int pay = VisitorTradeMath.PayWith(price, CampAnswers.Silver(map));
            if (pay > 0) CampAnswers.Pay(this, pay);
            string key = pay >= price ? "CC_CampLand_CatchSold" : pay > 0 ? "CC_CampLand_CatchSoldShort" : "CC_CampLand_CatchSoldUnpaid";
            if (pay < price) AddTension(pay > 0 ? 4f : 8f);
            string text = key.Translate(p.Named("X"), n.Named("COUNT"), c.def.label.Named("THING"), pay.Named("SILVER"), price.Named("PRICE"), Label.Named("NAME")).Resolve();
            Note(text, pay >= price ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent, Importance.Notable, look, true);
        }

        // ---------------- Their good turns ----------------

        /// <summary>
        /// The land kinds' good turns: "fish", a third of the fishers' catch set out at their camp's edge for the colony;
        /// "scrap", a quarter of the biggest pile of the salvagers' haul. False with too little to spare.
        /// </summary>
        private bool LandHelp(string h)
        {
            if (map == null || !share.IsValid) return false;
            List<Thing> from;
            int give;
            switch (h)
            {
                case "fish":
                {
                    int n = CatchOffer(out var def);
                    if (n < 15 || def == null) return false;
                    from = Goods().Where(t => t.def == def).ToList();
                    give = n / 3;
                    break;
                }
                case "scrap":
                {
                    var pile = Haul().FirstOrDefault();
                    if (pile.def == null || pile.count < 12) return false;
                    from = Goods().Where(t => t.def == pile.def).ToList();
                    give = pile.count / 4;
                    break;
                }
                default:
                    // The travellers' good turns: a book of theirs carried to the gate (CampSeekers.cs).
                    return SeekHelp(h);
            }
            var what = from.FirstOrDefault()?.def;
            int given = GiveAway(from, give);
            if (given <= 0 || what == null) return false;
            var by = Here.Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            Tell(Text("CC_Camp_HelpTitle"), HelpText(h, by, IncidentWorker_Happening.Colonists(map).RandomElementWithFallback(), given, what.label), LetterDefOf.PositiveEvent, false);
            return true;
        }

        // ---------------- What the colony's asks do ----------------

        /// <summary>One of the colony's beasts goes to the show's keepers: theirs now, it joins their beasts; one of them carries what they pay to the gate, and its keeper minds.</summary>
        public bool TakeBeast(Pawn beast)
        {
            if (beast == null || beast.Dead || !beast.Spawned || beast.Map != map || beast.Faction != Faction.OfPlayer || faction == null) return false;
            int price = CampLandMath.BeastPrice(beast.MarketValue);
            var keeper = beast.playerSettings?.Master;
            beast.SetFaction(faction);
            if (LordAlive && beast.GetLord() == null) lord.AddPawn(beast);
            if (!herd.Contains(beast)) herd.Add(beast);
            if (keeper != null && !keeper.Dead) CouncilGameComponent.GiveThought(keeper, CampLandDefOf.Rimfolk_Camp_SoldOurBeast);
            // apart: one of them carries the silver to the colony's gate.
            if (!StartBring(ThingDefOf.Silver, price)) GiveGoods("Silver", price, "Silver");
            Note(Text(keeper != null ? "CC_CampLand_BeastSold" : "CC_CampLand_BeastSoldNoKeeper", null, keeper, price, beast.LabelShort), MessageTypeDefOf.NeutralEvent, Importance.Notable, beast, true);
            return true;
        }
    }

    // ---------------- Effects the land kinds' asks and the run use ----------------

    /// <summary>The colony buys the fishers' catch (their ask): see <see cref="Camp.StartCatchSale"/>.</summary>
    public class DilemmaEffect_CampCatchSale : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampOnMap.For(ctx);
            if (c == null || c.CatchSaleBlocked() != null) return;
            int n = c.CatchOffer(out var def);
            sheet.Play("CC_Impact_CatchSale".Translate(c.Label).Resolve(), "CC_Impact_CatchSaleBrief".Translate().Resolve());
            sheet.Goods(1, def, n);
            sheet.Silver(-c.CatchCost(), "catch");
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            return c == null ? "CC_CampAsk_NotNow".Translate().ToString() : c.CatchSaleBlocked();
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null || (chance < 1f && !Rand.Chance(chance))) return;
            try
            {
                c.StartCatchSale();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp catch sale", e);
            }
        }
    }

    /// <summary>The salvagers carry half of their haul to the colony's gate (their ask): see <see cref="Camp.StartHaulShare"/>.</summary>
    public class DilemmaEffect_CampHaulShare : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampOnMap.For(ctx);
            if (c == null || c.HaulShareBlocked() != null) return;
            sheet.Play("CC_Impact_HaulShare".Translate(c.Label).Resolve(), "CC_Impact_HaulShareBrief".Translate().Resolve());
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            return c == null ? "CC_CampAsk_NotNow".Translate().ToString() : c.HaulShareBlocked();
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null || (chance < 1f && !Rand.Chance(chance))) return;
            try
            {
                c.StartHaulShare();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp haul share", e);
            }
        }
    }

    /// <summary>
    /// The fishing camp's water: with <see cref="far"/> they keep to the far water (clear of the colony's fishing and its
    /// side of the camp), else they may fish the water near the colony again.
    /// </summary>
    public class DilemmaEffect_CampFarWater : DilemmaEffect
    {
        public bool far = true;
        /// <summary>How much tenser the camp is for it (a world event's answer; a camp ask has its own).</summary>
        public float tension;

        public override bool ChanceEach => false;

        private static Camp Fishers(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            return c?.kind?.work == "fish" ? c : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = Fishers(ctx);
            if (c == null) return;
            sheet.Play((far ? "CC_Impact_FarWater" : "CC_Impact_NearWater").Translate(c.Label).Resolve(), (far ? "CC_Impact_FarWaterBrief" : "CC_Impact_NearWaterBrief").Translate().Resolve());
            if (tension != 0f) DilemmaImpact.Tension(sheet, tension);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = Fishers(ctx);
            if (c == null || (chance < 1f && !Rand.Chance(chance))) return;
            c.farWater = far;
            if (tension != 0f) c.AddTension(tension);
        }
    }

    /// <summary>The salvagers start on the ruin nearest the colony (paid for, in their ask), taking it down piece by piece before any other.</summary>
    public class DilemmaEffect_CampClearNear : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampOnMap.For(ctx);
            if (c?.kind?.work == "strip") sheet.Play("CC_Impact_ClearNear".Translate(c.Label).Resolve(), "CC_Impact_ClearNearBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c?.kind?.work != "strip" || (chance < 1f && !Rand.Chance(chance))) return;
            c.clearNear = true;
        }
    }

    /// <summary>
    /// "These ruins are ours": a proud crew (or a tense one) packs up and goes; the rest agree to pay a fee to finish,
    /// every five days, the first tomorrow.
    /// </summary>
    public class DilemmaEffect_CampRuinsOurs : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private static float LeaveChance(Camp c) => Mathf.Clamp01(c.kind.pride + c.tension / 200f);

        private static int FeeOf(Camp c) => CampAnswers.Round10((15f + 8f * Mathf.Max(1, c.Here.Count)) * LivingEvents.WealthFactor(c.map));

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampOnMap.For(ctx);
            if (c?.kind?.work != "strip") return;
            var leave = sheet.Play("CC_Impact_RuinsLeave".Translate(c.Label).Resolve(), "CC_Impact_RuinsLeaveBrief".Translate().Resolve());
            if (leave != null) leave.chance = LeaveChance(c);
            var fee = sheet.Play("CC_Impact_RuinsFee".Translate(c.Label, FeeOf(c)).Resolve(), "CC_Impact_RuinsFeeBrief".Translate().Resolve());
            if (fee != null) fee.chance = 1f - LeaveChance(c);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c?.kind?.work != "strip" || (chance < 1f && !Rand.Chance(chance))) return;
            if (Rand.Chance(LeaveChance(c)))
            {
                c.LoseGoodwill(3);
                c.Note(c.Text("CC_CampLand_RuinsLeave"), MessageTypeDefOf.NeutralEvent, Importance.Notable, null, true);
                c.Leave("asked");
                return;
            }
            c.termFee = true;
            c.fee = FeeOf(c);
            c.feeLate = false;
            c.nextFeeTick = Find.TickManager.TicksGame + GenDate.TicksPerDay;
            c.Note(c.Text("CC_CampLand_RuinsFee", null, null, c.fee), MessageTypeDefOf.NeutralEvent, Importance.Notable, null, true);
        }
    }

    /// <summary>The harvest hands are taken on: they reap the colony's ripe crops by day, for <see cref="pay"/> ("sheaf": one in ten of what they reap; "silver": paid when asked; "meals": a hot supper each evening they worked).</summary>
    public class DilemmaEffect_CampHireHands : DilemmaEffect
    {
        public string pay = "sheaf";

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampOnMap.For(ctx);
            if (c?.kind?.work != "reap" || c.reapPay != null) return;
            sheet.Play("CC_Impact_Reap".Translate(c.Label).Resolve(), "CC_Impact_ReapBrief".Translate().Resolve());
            if (pay == "sheaf") sheet.Add(ImpactKind.Goods, -1, "CC_Impact_ReapSheaf".Translate().Resolve(), "CC_Impact_ReapSheafBrief".Translate().Resolve(), "sheaf");
            else if (pay == "meals") sheet.Add(ImpactKind.Goods, -1, "CC_Impact_ReapMeals".Translate(Math.Max(1, c.Here.Count)).Resolve(), "CC_Impact_ReapMealsBrief".Translate().Resolve(), "suppers");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c?.kind?.work != "reap" || c.reapPay != null || (chance < 1f && !Rand.Chance(chance))) return;
            c.reapPay = pay.NullOrEmpty() ? "sheaf" : pay;
            c.AddTension(-5f);
        }
    }

    /// <summary>The watchers' notes on the monolith shared: the colony's study of the unnatural gains <see cref="knowledge"/> (the game's own), and they stay on <see cref="days"/> more to compare notes.</summary>
    public class DilemmaEffect_CampMonolithNotes : DilemmaEffect
    {
        public float knowledge = 3f;
        public float days = 2f;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampOnMap.For(ctx);
            if (c == null || !CampLand.StudyOpen()) return;
            sheet.Add(ImpactKind.Skill, 1, "CC_Impact_MonolithNotes".Translate(knowledge.ToString("0.#")).Resolve(), "CC_Impact_MonolithNotesBrief".Translate().Resolve(), "notes");
            if (days > 0f) sheet.Play("CC_Impact_StayLonger".Translate(c.Label, TextUtil.DaysString(Mathf.RoundToInt(days * GenDate.TicksPerDay))).Resolve(), "CC_Impact_StayLongerBrief".Translate().Resolve());
        }

        public override string Unavailable(DilemmaContext ctx) => CampLand.StudyOpen() ? null : "CC_CampLand_NoStudy".Translate().ToString();

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null || !CampLand.StudyOpen() || (chance < 1f && !Rand.Chance(chance))) return;
            try
            {
                Find.ResearchManager.ApplyKnowledge(KnowledgeCategoryDefOf.Basic, knowledge);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("watchers' notes", e);
            }
            if (days > 0f && c.untilTick != int.MaxValue) c.untilTick += Mathf.RoundToInt(days * GenDate.TicksPerDay);
        }
    }

    /// <summary>The watchers sleep in the colony's free beds tonight, out of the storm, and give them back in the morning.</summary>
    public class DilemmaEffect_CampShelter : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampOnMap.For(ctx);
            if (c?.kind?.work == "watch") sheet.Play("CC_Impact_Shelter".Translate(c.Label).Resolve(), "CC_Impact_ShelterBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c?.kind?.work != "watch" || (chance < 1f && !Rand.Chance(chance))) return;
            c.shelterDay = GenDate.DaysPassed;
        }
    }

    /// <summary>One of the colony's beasts sold to the show's keepers (see <see cref="CampLand.OurBeast"/> and <see cref="Camp.TakeBeast"/>).</summary>
    public class DilemmaEffect_CampTakeBeast : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampOnMap.For(ctx);
            var beast = CampLand.OurBeast(ctx?.map);
            if (c == null || beast == null) return;
            sheet.Add(ImpactKind.People, -1, "CC_Impact_BeastGoes".Translate(beast.LabelShort, c.Label).Resolve(), "CC_Impact_BeastGoesBrief".Translate(beast.LabelShort).Resolve(), "beast");
            sheet.Silver(CampLandMath.BeastPrice(beast.MarketValue), "beastsale");
            var keeper = beast.playerSettings?.Master;
            if (keeper != null) sheet.Thought(new List<Pawn> { keeper }, keeper.LabelShort, CampLandDefOf.Rimfolk_Camp_SoldOurBeast, _ => beast);
        }

        public override string Unavailable(DilemmaContext ctx) => CampLand.OurBeast(ctx?.map) == null ? "CC_CampLand_NoBeast".Translate().ToString() : null;

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null || (chance < 1f && !Rand.Chance(chance))) return;
            try
            {
                c.TakeBeast(CampLand.OurBeast(ctx.map));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp beast bought from the colony", e);
            }
        }
    }

    /// <summary>
    /// Up to two of the colony's children help the show's keepers feed their beasts: each a lesson at the side of their
    /// best keeper, among the beasts (Animals practice).
    /// </summary>
    public class DilemmaEffect_CampChildrenFeed : DilemmaEffect
    {
        public int count = 2;
        public FloatRange duration = new FloatRange(1.5f, 2f);

        public override bool ChanceEach => false;

        private static Pawn Keeper(Camp c) =>
            c.Here.Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0).FirstOrDefault();

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = CampOnMap.For(ctx);
            var kids = CampLand.Children(ctx?.map).Take(count).ToList();
            if (c == null || kids.Count == 0 || Keeper(c) == null) return;
            sheet.Play("CC_Impact_ChildrenFeed".Translate(TextUtil.JoinAnd(kids.Select(k => k.LabelShort).ToList()), c.Label).Resolve(), "CC_Impact_ChildrenFeedBrief".Translate().Resolve());
        }

        public override string Unavailable(DilemmaContext ctx) => CampLand.Children(ctx?.map).Count == 0 ? "CC_CampLand_NoChildren".Translate().ToString() : null;

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            var keeper = c == null ? null : Keeper(c);
            if (keeper == null || (chance < 1f && !Rand.Chance(chance))) return;
            foreach (var child in CampLand.Children(ctx.map).Take(count))
            {
                try
                {
                    // together: each child works at the keeper's side among the beasts (a lesson: the child follows the keeper).
                    var lesson = new DilemmaEffect_Lesson
                    {
                        teacher = PlayWho.X, student = PlayWho.A, skill = SkillDefOf.Animals, duration = duration, xpPerHour = 200f,
                        label = "CC_CampLand_FeedLabel".Translate(c.Label.Named("NAME")).Resolve()
                    };
                    lesson.Apply(new HappeningContext { map = ctx.map, a = child, stranger = keeper, faction = c.faction });
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp children feed the beasts", e);
                }
            }
        }
    }
}
