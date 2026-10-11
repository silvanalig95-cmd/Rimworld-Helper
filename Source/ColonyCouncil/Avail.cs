using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "What can this colony actually do right now?"
    ///
    /// Every suggestion the mod makes goes through here, so that a medieval colony is never asked for
    /// a turret and a tribe is never told to build a heater. Buildings are recognized by what they do
    /// (they heat, they glow, they hold patients) rather than by vanilla defNames, so modded content
    /// such as Medieval Overhaul furniture counts automatically.
    /// </summary>
    public static class Avail
    {
        // ---------- Tech & research ----------

        public static TechLevel PlayerTech
        {
            get
            {
                var tech = Faction.OfPlayerSilentFail?.def?.techLevel ?? TechLevel.Industrial;
                return tech == TechLevel.Undefined ? TechLevel.Industrial : tech;
            }
        }

        /// <summary>The most advanced tech level the colony has actually researched something in.</summary>
        public static TechLevel ResearchedTech
        {
            get
            {
                RefreshCache();
                return researchedTech;
            }
        }

        /// <summary>The era the colony "lives in": its faction level or what it has researched, whichever is higher.</summary>
        public static TechLevel EffectiveTech => PlayerTech > ResearchedTech ? PlayerTech : ResearchedTech;

        /// <summary>No electricity, no gunpowder era: medieval or tribal. The player can also set the era by hand.</summary>
        public static bool IsPreIndustrial
        {
            get
            {
                switch (ColonyCouncilMod.Settings.era)
                {
                    case EraMode.PreIndustrial: return true;
                    case EraMode.Industrial: return false;
                    default: return EffectiveTech <= TechLevel.Medieval && !HasElectricity;
                }
            }
        }

        public static bool ResearchDone(string defName)
        {
            var r = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(defName);
            return r != null && r.IsFinished;
        }

        public static bool HasElectricity
        {
            get
            {
                if (ColonyCouncilMod.Settings.era == EraMode.PreIndustrial) return false;
                RefreshCache();
                return hasElectricity;
            }
        }

        // ---------- Buildability ----------

        /// <summary>
        /// Can the player place this right now? Research done, shows up in the build menu, building
        /// prerequisites met, and if it needs power the colony at least knows electricity.
        /// </summary>
        public static bool CanBuild(BuildableDef def, Map map = null)
        {
            if (def == null) return false;
            if (!def.BuildableByPlayer) return false;
            if (!def.IsResearchFinished) return false;
            if (map != null && def.buildingPrerequisites != null)
            {
                foreach (var pre in def.buildingPrerequisites)
                {
                    if (!map.listerBuildings.ColonistsHaveBuilding(pre)) return false;
                }
            }
            if (def is ThingDef td && NeedsPower(td) && !HasElectricity) return false;
            return true;
        }

        public static bool NeedsPower(ThingDef td)
        {
            if (td?.comps == null) return false;
            foreach (var c in td.comps)
            {
                if (c is CompProperties_Power p && p.compClass != null && typeof(CompPowerTrader).IsAssignableFrom(p.compClass)
                    && !typeof(CompPowerPlant).IsAssignableFrom(p.compClass) && p.PowerConsumption > 0f)
                    return true;
            }
            return false;
        }

        /// <summary>Picks the stuff the colony has the most of (and enough of, if possible) for a buildable.</summary>
        public static ThingDef BestStuff(BuildableDef def, Map map, int needed = -1)
        {
            if (def == null || !def.MadeFromStuff) return null;
            if (needed < 0) needed = def.CostStuffCount;
            ThingDef best = null;
            int bestCount = -1;
            ThingDef bestEnough = null;
            int bestEnoughCount = -1;
            foreach (var stuff in AllowedStuffs(def))
            {
                int count = map != null ? map.resourceCounter.GetCount(stuff) : 0;
                if (count > bestCount)
                {
                    best = stuff;
                    bestCount = count;
                }
                if (count >= needed && count > bestEnoughCount)
                {
                    bestEnough = stuff;
                    bestEnoughCount = count;
                }
            }
            if (bestEnough != null) return bestEnough;
            if (best != null && bestCount > 0) return best;
            return GenStuff.DefaultStuffFor(def);
        }

        /// <summary>
        /// Does the colony have (or can it easily get) the materials for one of these? Wood counts as
        /// obtainable when there are trees on the map; stone blocks count when there are chunks.
        /// </summary>
        public static bool MaterialsWithinReach(BuildableDef def, Map map, ThingDef stuff = null)
        {
            if (def == null || map == null) return false;
            if (def.MadeFromStuff)
            {
                stuff = stuff ?? BestStuff(def, map);
                if (stuff == null) return false;
                if (!HaveOrCanGet(stuff, def.CostStuffCount, map)) return false;
            }
            if (def.CostList != null)
            {
                foreach (var cost in def.CostList)
                {
                    if (!HaveOrCanGet(cost.thingDef, cost.count, map)) return false;
                }
            }
            return true;
        }

        private static bool HaveOrCanGet(ThingDef thing, int count, Map map)
        {
            if (thing == null) return true;
            int have = map.resourceCounter.GetCount(thing);
            if (have >= count) return true;
            if (thing == ThingDefOf.WoodLog && map.listerThings.ThingsInGroup(ThingRequestGroup.HarvestablePlant).Count > 0)
                return true;
            if (thing.stuffProps != null && thing.stuffProps.categories != null
                && thing.stuffProps.categories.Contains(StuffCategoryDefOf.Stony)
                && map.listerThings.ThingsInGroup(ThingRequestGroup.Chunk).Count > 0)
                return true;
            return false;
        }

        private static readonly Dictionary<RecipeDef, List<ThingDef>> recipeUsers = new Dictionary<RecipeDef, List<ThingDef>>();

        /// <summary>The workbenches (and the like) that can do a recipe. Vanilla walks every item in the game for this; it is remembered.</summary>
        public static List<ThingDef> RecipeUsers(RecipeDef r)
        {
            if (r == null) return new List<ThingDef>();
            if (!recipeUsers.TryGetValue(r, out var list)) recipeUsers[r] = list = r.AllRecipeUsers.Distinct().ToList();
            return list;
        }

        // ---------- Finding the right building for a job ----------

        // ---------- Materials ----------

        private static readonly Dictionary<BuildableDef, List<ThingDef>> allowedStuffs = new Dictionary<BuildableDef, List<ThingDef>>();
        private static readonly Dictionary<BuildableDef, HashSet<ThingDef>> allowedStuffSets = new Dictionary<BuildableDef, HashSet<ThingDef>>();

        /// <summary>
        /// Every material this can be made from. Vanilla works this out by walking every item in the game each time;
        /// defs never change after loading, so it is remembered.
        /// </summary>
        public static List<ThingDef> AllowedStuffs(BuildableDef def)
        {
            if (def == null) return new List<ThingDef>();
            if (!allowedStuffs.TryGetValue(def, out var list))
                allowedStuffs[def] = list = def.MadeFromStuff ? GenStuff.AllowedStuffsFor(def).ToList() : new List<ThingDef>();
            return list;
        }

        public static bool StuffAllowed(BuildableDef def, ThingDef stuff)
        {
            if (def == null || stuff == null) return false;
            if (!allowedStuffSets.TryGetValue(def, out var set)) allowedStuffSets[def] = set = new HashSet<ThingDef>(AllowedStuffs(def));
            return set.Contains(stuff);
        }

        // ---------- Finding the right building for a job ----------

        private static readonly Dictionary<ThingDef, float> workToBuild = new Dictionary<ThingDef, float>();

        private static float WorkToBuild(ThingDef d)
        {
            if (!workToBuild.TryGetValue(d, out float w)) workToBuild[d] = w = d.GetStatValueAbstract(StatDefOf.WorkToBuild);
            return w;
        }

        /// <summary>BestBuildable answers for this in-game hour (research and buildings don't change faster than that).</summary>
        private static readonly Dictionary<(Map, System.Reflection.MethodInfo, object, string), ThingDef> bestMemo = new Dictionary<(Map, System.Reflection.MethodInfo, object, string), ThingDef>();
        private static int bestMemoHour = -1;
        private static Game bestMemoGame;

        /// <summary>
        /// The best buildable for a purpose: preferred vanilla defNames first (if buildable), otherwise
        /// any buildable def matching the predicate, cheapest first. Null if the colony can't build one.
        /// </summary>
        public static ThingDef BestBuildable(Map map, Func<ThingDef, bool> pred, params string[] preferred)
        {
            if (pred == null) return null;
            // Only predicates that capture nothing (method groups and plain lambdas) can be remembered: a capturing
            // lambda is a new object on every call.
            var target = pred.Target;
            bool memoable = target == null || target.GetType().Name == "<>c";
            if (!memoable) return FindBestBuildable(map, pred, preferred);
            int hour = (Find.TickManager?.TicksGame ?? 0) / GenDate.TicksPerHour;
            if (hour != bestMemoHour || Current.Game != bestMemoGame || bestMemo.Count > 1000)
            {
                bestMemo.Clear();
                bestMemoHour = hour;
                bestMemoGame = Current.Game;
            }
            var key = (map, pred.Method, target, preferred == null || preferred.Length == 0 ? "" : string.Join(",", preferred));
            if (!bestMemo.TryGetValue(key, out var best)) bestMemo[key] = best = FindBestBuildable(map, pred, preferred);
            return best;
        }

        private static ThingDef FindBestBuildable(Map map, Func<ThingDef, bool> pred, string[] preferred)
        {
            if (preferred != null)
            {
                foreach (var name in preferred)
                {
                    var d = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                    if (d != null && pred(d) && CanBuild(d, map)) return d;
                }
            }
            ThingDef best = null;
            float bestCost = float.MaxValue;
            foreach (var d in AllBuildings)
            {
                if (!pred(d) || !CanBuild(d, map)) continue;
                float cost = WorkToBuild(d);
                if (cost < bestCost)
                {
                    best = d;
                    bestCost = cost;
                }
            }
            return best;
        }

        public static bool AnyBuildable(Map map, Func<ThingDef, bool> pred)
        {
            foreach (var d in AllBuildings)
            {
                if (pred(d) && CanBuild(d, map)) return true;
            }
            return false;
        }

        private static List<ThingDef> allBuildings;

        public static List<ThingDef> AllBuildings
        {
            get
            {
                if (allBuildings == null)
                {
                    allBuildings = DefDatabase<ThingDef>.AllDefsListForReading
                        .Where(d => d.category == ThingCategory.Building && d.BuildableByPlayer && !d.IsFrame && !d.IsBlueprint)
                        .ToList();
                }
                return allBuildings;
            }
        }

        // ---------- What a building is for ----------

        public static bool IsHeater(ThingDef d)
        {
            if (d == null) return false;
            if (d.thingClass != null && typeof(Building_Heater).IsAssignableFrom(d.thingClass)) return true;
            var hp = d.GetCompProperties<CompProperties_HeatPusher>();
            return hp != null && hp.heatPerSecond >= 10f && !IsCooler(d);
        }

        public static bool IsCooler(ThingDef d)
        {
            if (d == null) return false;
            if (d.thingClass != null && typeof(Building_Cooler).IsAssignableFrom(d.thingClass)) return true;
            var hp = d.GetCompProperties<CompProperties_HeatPusher>();
            return hp != null && hp.heatPerSecond < 0f;
        }

        public static bool IsLight(ThingDef d)
        {
            if (d == null || d.building == null) return false;
            var g = d.GetCompProperties<CompProperties_Glower>();
            return g != null && g.glowRadius >= 4f && !d.IsWorkTable;
        }

        public static bool IsSunLamp(ThingDef d)
        {
            var g = d?.GetCompProperties<CompProperties_Glower>();
            return g != null && g.overlightRadius > 0f;
        }

        public static bool IsHumanBed(ThingDef d) =>
            d != null && d.IsBed && d.building != null && d.building.bed_humanlike && !d.building.bed_slabBed
            && (d.building.bed_humanlike && d.size.z >= 2);

        public static bool IsDoubleBed(ThingDef d) => IsHumanBed(d) && d.size.x >= 2;

        public static bool IsSingleBed(ThingDef d) => IsHumanBed(d) && d.size.x == 1;

        public static bool IsAnimalBed(ThingDef d) => d != null && d.IsBed && d.building != null && !d.building.bed_humanlike;

        public static bool IsEatingTable(ThingDef d) => d != null && d.surfaceType == SurfaceType.Eat && d.building != null;

        public static bool IsChair(ThingDef d) => d != null && d.building != null && d.building.isSittable;

        public static bool IsJoyGiver(ThingDef d) => d?.building?.joyKind != null;

        public static bool IsResearchBench(ThingDef d) =>
            d?.thingClass != null && typeof(Building_ResearchBench).IsAssignableFrom(d.thingClass);

        public static bool IsAutoTurret(ThingDef d) =>
            d?.thingClass != null && typeof(Building_Turret).IsAssignableFrom(d.thingClass)
            && d.GetCompProperties<CompProperties_Mannable>() == null;

        public static bool IsTrap(ThingDef d) =>
            d?.thingClass != null && typeof(Building_Trap).IsAssignableFrom(d.thingClass);

        public static bool IsCover(ThingDef d) =>
            d?.building != null && d.passability == Traversability.PassThroughOnly && d.fillPercent >= 0.45f
            && !d.IsDoor && d.size.x == 1 && d.size.z == 1 && !d.IsBed && d.surfaceType == SurfaceType.None
            && !d.building.isSittable && !IsJoyGiver(d) && d.thingClass != null && !typeof(Building_Turret).IsAssignableFrom(d.thingClass);

        public static bool IsGrave(ThingDef d) =>
            d?.thingClass != null && typeof(Building_Grave).IsAssignableFrom(d.thingClass);

        /// <summary>
        /// A brewing barrel: vanilla's, or a Vanilla Expanded Framework item processor that brews (as used by
        /// Medieval Overhaul and Vanilla Brewing Expanded), recognized by what it's called.
        /// </summary>
        public static bool IsFermenter(ThingDef d)
        {
            if (d?.thingClass == null) return false;
            if (typeof(Building_FermentingBarrel).IsAssignableFrom(d.thingClass)) return true;
            // Vanilla Expanded's item processors and Processor Framework's processors: barrels by name.
            bool processor = d.thingClass.FullName.Contains("ItemProcessor")
                             || (d.comps != null && d.comps.Any(c => c?.compClass != null && c.compClass.FullName.Contains("Processor")));
            if (!processor) return false;
            string name = (d.defName + " " + d.label).ToLowerInvariant();
            // Sauces and pickles ferment too, but they aren't brewing.
            if (name.Contains("sauce") || name.Contains("soy") || name.Contains("vinegar") || name.Contains("pickl")) return false;
            return name.Contains("barrel") || name.Contains("ferment") || name.Contains("brew") || name.Contains("keg") || name.Contains("cask")
                   || name.Contains("still") || name.Contains("distill") || name.Contains("drum");
        }

        /// <summary>
        /// A hydroponics basin: a plant grower with the Hydroponic sow tag, or one named for it. Other growers
        /// (Medieval Overhaul's and Vanilla Expanded's planters, flower pots) aren't hydroponics.
        /// </summary>
        public static bool IsHydroponics(ThingDef d)
        {
            if (d?.thingClass == null || d.building == null || !typeof(Building_PlantGrower).IsAssignableFrom(d.thingClass)) return false;
            if (d.building.sowTag == "Hydroponic") return true;
            return d.defName.IndexOf("hydroponic", StringComparison.OrdinalIgnoreCase) >= 0
                   || (d.label != null && d.label.IndexOf("hydroponic", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static bool IsPlantPot(ThingDef d) =>
            d?.thingClass != null && typeof(Building_PlantGrower).IsAssignableFrom(d.thingClass)
            && d.building != null && d.building.sowTag == "DecorativePlant";

        public static bool IsWorkstationFor(ThingDef d, SkillDef skill)
        {
            if (d == null || skill == null || !d.IsWorkTable) return false;
            var recipes = d.AllRecipes;
            if (recipes == null) return false;
            for (int i = 0; i < recipes.Count; i++)
            {
                if (recipes[i].workSkill == skill) return true;
            }
            return false;
        }

        public static bool IsCookingStation(ThingDef d)
        {
            if (d == null || !d.IsWorkTable) return false;
            var recipes = d.AllRecipes;
            if (recipes == null) return false;
            foreach (var r in recipes)
            {
                if (r.workSkill == SkillDefOf.Cooking && r.ProducedThingDef != null && r.ProducedThingDef.IsNutritionGivingIngestible
                    && r.ProducedThingDef.ingestible != null && r.ProducedThingDef.ingestible.preferability >= FoodPreferability.MealSimple)
                    return true;
            }
            return false;
        }

        public static bool IsStorage(ThingDef d) =>
            d?.building != null && d.building.fixedStorageSettings == null && d.building.defaultStorageSettings != null
            && d.thingClass != null && typeof(Building_Storage).IsAssignableFrom(d.thingClass);

        /// <summary>Bedside furniture: a facility that must sit next to the bed's head (end tables and the like).</summary>
        public static bool IsEndTable(ThingDef d)
        {
            if (d?.building == null) return false;
            if (d.defName == "EndTable") return true;
            var f = d.GetCompProperties<CompProperties_Facility>();
            return f != null && (f.mustBePlacedAdjacentCardinalToBedHead || f.mustBePlacedAdjacentCardinalToAndFacingBedHead)
                   && d.size.x == 1 && d.size.z == 1 && d.surfaceType == SurfaceType.Item;
        }

        /// <summary>Bedroom storage furniture linked to beds from a distance (dressers and the like).</summary>
        public static bool IsDresser(ThingDef d)
        {
            if (d?.building == null) return false;
            if (d.defName == "Dresser") return true;
            var f = d.GetCompProperties<CompProperties_Facility>();
            return f != null && !f.mustBePlacedAdjacent && !f.mustBePlacedAdjacentCardinalToBedHead && !f.canLinkToMedBedsOnly
                   && f.maxDistance > 0f && d.size.x == 2 && d.size.z == 1 && !IsLight(d);
        }

        // ---------- Map state ----------

        public static int JoyKindsOnMap(Map map)
        {
            try
            {
                return JoyUtility.JoyKindsOnMapCount(map);
            }
            catch
            {
                return map.listerBuildings.allBuildingsColonist.Where(b => b.def.building?.joyKind != null)
                    .Select(b => b.def.building.joyKind).Distinct().Count();
            }
        }

        public static bool ColonyHas(Map map, Func<ThingDef, bool> pred)
        {
            var list = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < list.Count; i++)
            {
                if (pred(list[i].def)) return true;
            }
            return false;
        }

        public static int ColonyCount(Map map, Func<ThingDef, bool> pred)
        {
            int n = 0;
            var list = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < list.Count; i++)
            {
                if (pred(list[i].def)) n++;
            }
            return n;
        }

        public static IEnumerable<Building> ColonyBuildings(Map map, Func<ThingDef, bool> pred)
        {
            var list = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < list.Count; i++)
            {
                if (pred(list[i].def)) yield return list[i];
            }
        }

        /// <summary>Label for a def that reads naturally in a sentence ("a torch lamp", "a heater").</summary>
        public static string Indefinite(ThingDef d) => d == null ? "" : Find.ActiveLanguageWorker.WithIndefiniteArticle(d.label);

        // ---------- Cache ----------

        private static int cacheTick = -99999;
        private static bool hasElectricity;
        private static TechLevel researchedTech = TechLevel.Neolithic;

        private static void RefreshCache()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            // Also refresh when time went backwards: another save was loaded.
            if (cacheTick >= 0 && now >= cacheTick && now - cacheTick < 2500) return;
            cacheTick = now;
            // Electricity: the vanilla research, or a power plant the colony actually runs (mods rename things).
            hasElectricity = ResearchDone("Electricity")
                             || Find.Maps.Any(m => m.IsPlayerHome && m.listerBuildings.allBuildingsColonist.Any(IsRealGenerator));
            // The era: the most advanced level with at least two finished projects, so one stray modded project
            // doesn't drag a medieval colony into the industrial age.
            var counts = new Dictionary<TechLevel, int>();
            foreach (var r in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
                if (r.IsFinished) counts[r.techLevel] = (counts.TryGetValue(r.techLevel, out int n) ? n : 0) + 1;
            var best = TechLevel.Neolithic;
            foreach (var kv in counts)
                if (kv.Key > best && kv.Value >= 2) best = kv.Key;
            researchedTech = best;
        }

        /// <summary>
        /// A generator that feeds a power grid. Medieval Overhaul's watermill uses a power-plant part only to measure
        /// the river (it makes 1 W and connects to nothing), and must not make a medieval colony look industrial.
        /// </summary>
        private static bool IsRealGenerator(Building b)
        {
            var plant = b.TryGetComp<CompPowerPlant>();
            return plant?.Props != null && plant.Props.transmitsPower && plant.Props.PowerConsumption <= -50f;
        }

        public static void ResetCache()
        {
            cacheTick = -99999;
            bestMemo.Clear();
            bestMemoGame = null;
        }
    }
}
