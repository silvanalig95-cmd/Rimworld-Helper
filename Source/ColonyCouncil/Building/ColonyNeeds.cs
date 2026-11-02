using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony as the planning helper reads it (<see cref="For"/>, once an hour per home map, or when asked), shared
    /// by every part of planning ahead. Who eats here: colonists and slaves, prisoners, Hospitality's guests (they eat
    /// the colony's meals), and the colony's animals fed from the stores (those that can't graze, or all of them while
    /// nothing grows), each by their own hunger (Need_Food's fall at "fed" times a day's ticks, so genes, pregnancy and
    /// age count). What there is: Making's stock reading, by kind (meals, raw food, animal feed, medicine, clothes, the
    /// blueprints' materials), each with its nutrition, the storage cells it takes, its rot days and the share kept
    /// frozen. What is used: the colony's record (ColonyUse) when it covers a day. What the land gives: each growing
    /// zone's crop at its own soil and the year's growing days from the land's temperatures. The benches and bills:
    /// each meal, medicine and feed bill as the player set it, which mod sets it (Everybody Gets One's are only read),
    /// what its bench can make a day, and a free bench of the same kind. The arithmetic is in <see cref="AheadMath"/>.
    /// </summary>
    public class ColonyNeeds
    {
        public Map map;
        public int tick = -999999;

        // Who eats here.
        public int colonists, slaves, prisoners, guests, feedAnimals, grazers;
        /// <summary>Nutrition a day the people who eat meals here need; and the animals fed from the stores.</summary>
        public float humanNeed, feedNeed;

        // What is used.
        public ColonyUse.Read record;
        public bool measured;
        public float measuredDays;
        public float plantShare = 1f;
        public float medicineEstimate;

        // What there is.
        public Dictionary<ThingDef, int> stock = new Dictionary<ThingDef, int>();
        public List<AheadGood> goods = new List<AheadGood>();
        public Dictionary<string, ThingDef> defs = new Dictionary<string, ThingDef>();
        public bool freezer, dispenser;
        public float mealRotDays = AheadMath.Far, mealRoom = AheadMath.Far;

        // What the land gives.
        public List<AheadField> fields = new List<AheadField>();
        public Dictionary<string, Zone_Growing> zones = new Dictionary<string, Zone_Growing>();
        public List<AheadCrop> crops = new List<AheadCrop>();
        public Dictionary<string, ThingDef> plants = new Dictionary<string, ThingDef>();
        public float[] dayTemps = new float[0], growFactors = new float[0];
        public float growingLeftThisYear;
        public int dayOfYear, newGround;

        // The benches and the bills.
        public List<AheadBill> bills = new List<AheadBill>();
        public Dictionary<string, (Building_WorkTable bench, Bill_Production bill)> billOf = new Dictionary<string, (Building_WorkTable, Bill_Production)>();
        public Dictionary<string, Building_WorkTable> freeBenchOf = new Dictionary<string, Building_WorkTable>();
        public List<AheadAdd> canAdd = new List<AheadAdd>();
        public Dictionary<string, (Building_WorkTable bench, RecipeDef recipe, ThingDef product)> addOf = new Dictionary<string, (Building_WorkTable, RecipeDef, ThingDef)>();

        private static readonly Dictionary<int, ColonyNeeds> cache = new Dictionary<int, ColonyNeeds>();

        /// <summary>The colony's needs on a home map, read once an hour (or now, forced); an empty reading elsewhere.</summary>
        public static ColonyNeeds For(Map map, bool force = false)
        {
            if (map == null || !map.IsPlayerHome) return new ColonyNeeds { map = map };
            if (!cache.TryGetValue(map.uniqueID, out var n) || n.map != map) cache[map.uniqueID] = n = new ColonyNeeds { map = map };
            int now = Find.TickManager.TicksGame;
            if (force || now - n.tick >= Sched.HourTicks || now < n.tick)
            {
                var fresh = new ColonyNeeds { map = map, tick = now };
                try
                {
                    fresh.Read();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("colony needs", e);
                }
                cache[map.uniqueID] = n = fresh;
            }
            return n;
        }

        /// <summary>What the colony has of a kind: nutrition for food, pieces else.</summary>
        public float HaveOf(string kind)
        {
            float sum = 0f;
            foreach (var g in goods)
                if (g.kind == kind) sum += g.nutrition > 0f ? g.Nutrition : g.count;
            return sum;
        }

        /// <summary>The planner's input: these numbers, the player's days and what they set aside.</summary>
        public AheadInput Input(float mealDays, float reserveDays, HashSet<string> declined) => new AheadInput
        {
            colonists = colonists, slaves = slaves, prisoners = prisoners, guests = guests, feedAnimals = feedAnimals, humanNeed = humanNeed, feedNeed = feedNeed,
            medicineEstimate = medicineEstimate, measured = measured, goods = goods, bills = bills, canAdd = canAdd, fields = fields, crops = crops,
            growFactors = growFactors, growingLeftThisYear = growingLeftThisYear, plantShare = plantShare, mealDays = mealDays, reserveDays = reserveDays,
            freezer = freezer, dispenser = dispenser, mealRotDays = mealRotDays, mealRoom = mealRoom, newGround = newGround, declined = declined ?? new HashSet<string>()
        };

        // ---------------- Reading ----------------

        private void Read()
        {
            Eaters();
            record = ColonyUse.ReadFor(map);
            measured = record != null && record.measured;
            measuredDays = record?.days ?? 0f;
            stock = Making.Stock(map);
            Goods();
            Land();
            Benches();
        }

        /// <summary>A good's kind for the rows: meal, raw, feed, medicine or clothes; null for anything else.</summary>
        public static string KindOf(ThingDef d)
        {
            if (d == null) return null;
            if (d.IsMedicine) return AheadMath.Medicine;
            if (d.IsApparel) return AheadMath.Clothes;
            if (d.IsCorpse || d.IsDrug || !d.IsNutritionGivingIngestible || d.ingestible == null) return null;
            string making = Making.KindOf(d);
            if (making == "meals") return AheadMath.Meal;
            if (making == "feed" || !d.ingestible.HumanEdible) return AheadMath.Feed;
            var pref = d.ingestible.preferability;
            if (pref == FoodPreferability.RawBad || pref == FoodPreferability.RawTasty) return AheadMath.Raw;
            if (pref == FoodPreferability.NeverForNutrition && d.thingCategories != null && d.thingCategories.Any(c => c == ThingCategoryDefOf.PlantFoodRaw || c == ThingCategoryDefOf.MeatRaw)) return AheadMath.Raw;
            return null;
        }

        private static float NutritionOf(ThingDef d)
        {
            try
            {
                return d.GetStatValueAbstract(StatDefOf.Nutrition);
            }
            catch
            {
                return d.ingestible?.CachedNutrition ?? 0f;
            }
        }

        private static float RotDaysOf(ThingDef d)
        {
            var rot = d.GetCompProperties<CompProperties_Rottable>();
            return rot == null || rot.daysToRotStart <= 0f ? AheadMath.Far : rot.daysToRotStart;
        }

        /// <summary>The colony's animals that eat from the stores: those that can't graze, and all of them while nothing grows.</summary>
        private bool FedFromStores(Pawn p)
        {
            if (!p.RaceProps.Eats(FoodTypeFlags.Plant)) return true;
            try
            {
                return ThingDefOf.Plant_Grass != null && !PlantUtility.GrowthSeasonNow(map, ThingDefOf.Plant_Grass);
            }
            catch
            {
                return false;
            }
        }

        private void Eaters()
        {
            var player = Faction.OfPlayerSilentFail;
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                if (p == null || p.Dead || p.needs?.food == null) continue;
                float need;
                try
                {
                    need = AheadMath.NutritionADay(p.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed, false));
                }
                catch
                {
                    continue;
                }
                if (p.Faction != null && p.Faction == player || p.IsSlaveOfColony)
                {
                    if (p.RaceProps.Humanlike)
                    {
                        if (p.IsSlave) slaves++;
                        else colonists++;
                        humanNeed += need;
                    }
                    else if (p.RaceProps.Animal)
                    {
                        if (FedFromStores(p))
                        {
                            feedAnimals++;
                            feedNeed += need;
                        }
                        else grazers++;
                    }
                }
                else if (p.IsPrisonerOfColony)
                {
                    prisoners++;
                    humanNeed += need;
                }
                else if (p.RaceProps.Humanlike && Strangers.IsHospitalityGuest(p))
                {
                    guests++;
                    humanNeed += need;
                }
            }
            int tending = map.mapPawns.FreeColonistsSpawned.Count(p => p.health?.hediffSet != null && p.health.hediffSet.HasTendableHediff());
            medicineEstimate = tending + 0.1f * (colonists + slaves);
        }

        private void Goods()
        {
            var player = Faction.OfPlayerSilentFail;
            AheadGood Add(ThingDef d, string kind, float count)
            {
                var g = new AheadGood
                {
                    key = d.defName, label = d.label, kind = kind, count = count, stackLimit = Mathf.Max(1, d.stackLimit),
                    nutrition = kind == AheadMath.Meal || kind == AheadMath.Raw || kind == AheadMath.Feed ? NutritionOf(d) : 0f, rotDays = RotDaysOf(d)
                };
                if (measured && record.used.TryGetValue(d, out float used)) g.usedADay = used;
                // The cells it takes and the share kept frozen.
                var cells = new HashSet<IntVec3>();
                float frozen = 0f, all = 0f;
                foreach (var t in map.listerThings.ThingsOfDef(d))
                {
                    if (t == null || !t.Spawned || player != null && t.IsForbidden(player)) continue;
                    cells.Add(t.Position);
                    all += t.stackCount;
                    if (g.rotDays < AheadMath.Far && (t.AmbientTemperature <= 0f || InFridge(t.Position))) frozen += t.stackCount;
                }
                g.cells = cells.Count;
                g.frozenShare = all > 0f ? frozen / all : 0f;
                goods.Add(g);
                defs[d.defName] = d;
                return g;
            }
            foreach (var kv in stock)
            {
                if (kv.Value <= 0) continue;
                string kind = KindOf(kv.Key);
                if (kind == null || kind == AheadMath.Clothes) continue;
                Add(kv.Key, kind, kv.Value);
            }
            // Clothes in store worth wearing: whole enough, not worn by the dead.
            var wearable = new Dictionary<ThingDef, int>();
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel))
            {
                if (!(t is Apparel ap) || !ap.Spawned || ap.WornByCorpse || player != null && ap.IsForbidden(player)) continue;
                if (ap.def.useHitPoints && ap.HitPoints < ap.MaxHitPoints * 0.5f) continue;
                wearable.TryGetValue(ap.def, out int n);
                wearable[ap.def] = n + 1;
            }
            foreach (var kv in wearable) Add(kv.Key, AheadMath.Clothes, kv.Value);
            if (measured)
                foreach (var kv in record.used)
                    if (kv.Key.IsApparel && !wearable.ContainsKey(kv.Key) && kv.Value > 0f)
                        goods.Add(new AheadGood { key = kv.Key.defName, label = kv.Key.label, kind = AheadMath.Clothes, usedADay = kv.Value });
            // What the blueprints and frames are short of.
            int Have(ThingDef d) => d != null && stock.TryGetValue(d, out int n) ? n : 0;
            foreach (var kv in Making.BuildingNeeds(map, Have))
                Add(kv.Key, AheadMath.Material, Have(kv.Key)).shortForBuilding = kv.Value;
            // Of the raw food used, the share that grew in the fields.
            if (measured)
            {
                float plant = 0f, other = 0f;
                foreach (var kv in record.used)
                {
                    if (KindOf(kv.Key) != AheadMath.Raw) continue;
                    float n = kv.Value * NutritionOf(kv.Key);
                    if (kv.Key.ingestible != null && (kv.Key.ingestible.foodType & (FoodTypeFlags.VegetableOrFruit | FoodTypeFlags.Seed | FoodTypeFlags.Fungus)) != 0) plant += n;
                    else other += n;
                }
                if (plant + other > 0f) plantShare = plant / (plant + other);
            }
            Meals();
        }

        /// <summary>The meal the colony makes most, how long it keeps, whether a freezer takes it, and the room the stores have for it.</summary>
        private void Meals()
        {
            var meal = goods.Where(g => g.kind == AheadMath.Meal).OrderByDescending(g => g.count).Select(g => defs[g.key]).FirstOrDefault() ?? ThingDefOf.MealSimple;
            if (meal == null) return;
            mealRotDays = RotDaysOf(meal);
            float nutrition = NutritionOf(meal);
            int stack = Mathf.Max(1, meal.stackLimit);
            float room = 0f;
            bool cold = false;
            foreach (var group in map.haulDestinationManager.AllGroupsListForReading)
            {
                if (group?.Settings == null || !group.Settings.AllowedToAccept(meal)) continue;
                var cells = group.CellsList;
                if (cells == null || cells.Count == 0) continue;
                if (!cold && mealRotDays < AheadMath.Far)
                {
                    var r = cells[0].GetRoom(map);
                    if (r != null && !r.UsesOutdoorTemperature && r.Temperature <= 0f || group.parent is Building_Storage store && Fridge(store)) cold = true;
                }
                foreach (var c in cells)
                {
                    int items = c.GetItemCount(map), max = c.GetMaxItemsAllowedInCell(map);
                    room += Mathf.Max(0, max - items) * stack;
                    foreach (var t in c.GetThingList(map))
                        if (t.def == meal) room += Mathf.Max(0, stack - t.stackCount);
                }
            }
            freezer = cold;
            mealRoom = room * nutrition;
            dispenser = map.listerBuildings.allBuildingsColonist.OfType<Building_NutrientPasteDispenser>().Any(b => b.GetComp<CompPowerTrader>()?.PowerOn ?? true);
        }

        // ---------------- The land ----------------

        private static float[] FactorsFor(ThingDef plant, float[] temps)
        {
            var pp = plant.plant;
            float min = pp.minGrowthTemperature, minOpt = pp.minOptimalGrowthTemperature, maxOpt = pp.maxOptimalGrowthTemperature, max = pp.maxGrowthTemperature;
            if (maxOpt <= minOpt || max <= min)
            {
                min = Plant.DefaultMinGrowthTemperature;
                minOpt = Plant.DefaultMinOptimalGrowthTemperature;
                maxOpt = Plant.DefaultMaxOptimalGrowthTemperature;
                max = Plant.DefaultMaxGrowthTemperature;
            }
            return AheadMath.GrowFactors(temps, min, minOpt, maxOpt, max);
        }

        private static float YieldFactor(ThingDef plant) =>
            plant.plant.harvestYieldAffectedByDifficulty ? Find.Storyteller?.difficulty?.cropYieldFactor ?? 1f : 1f;

        /// <summary>A plant the colony can sow in a field whose harvest people eat: unlocked, not a tree, not a drug.</summary>
        /// <summary>
        /// A storage building that keeps its contents cold by itself, powered: a fridge of RimFridge or a mod like it (a
        /// comp whose type is named a refrigerator), so food in it keeps however warm the room is. Found by name, so
        /// without such a mod nothing counts.
        /// </summary>
        public static bool Fridge(Building_Storage b)
        {
            if (b?.AllComps == null) return false;
            bool fridge = false;
            foreach (var c in b.AllComps)
                if (c != null && c.GetType().Name.IndexOf("Refrigerator", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fridge = true;
                    break;
                }
            return fridge && (b.GetComp<CompPowerTrader>()?.PowerOn ?? true);
        }

        /// <summary>The cell holds such a fridge (what lies in it keeps).</summary>
        private bool InFridge(IntVec3 c) => c.GetEdifice(map) is Building_Storage b && Fridge(b);

        private static bool FoodCrop(ThingDef d)
        {
            var pp = d?.plant;
            if (pp == null || !pp.Sowable || pp.IsTree || pp.mustBeWildToSow || pp.mustBePermanentDarknessToSow || pp.harvestedThingDef == null || pp.harvestYield <= 0f
                || pp.growDays <= 0f) return false;
            if (pp.sowTags == null || !pp.sowTags.Contains("Ground")) return false;
            if (pp.sowResearchPrerequisites != null && pp.sowResearchPrerequisites.Any(r => r != null && !r.IsFinished)) return false;
            var h = pp.harvestedThingDef;
            return KindOf(h) == AheadMath.Raw;
        }

        /// <summary>The food fields whose free ground beside them is looked for, the best yield first.</summary>
        private const int FreeLooked = 6;

        private void Land()
        {
            var twelfths = new float[12];
            for (int i = 0; i < 12; i++) twelfths[i] = GenTemperature.AverageTemperatureAtTileForTwelfth(map.Tile, (Twelfth)i);
            dayOfYear = GenLocalDate.DayOfYear(map);
            dayTemps = AheadMath.DayTemperatures(twelfths, dayOfYear, AheadMath.DaysAYear);
            growFactors = AheadMath.GrowFactors(dayTemps, Plant.DefaultMinGrowthTemperature, Plant.DefaultMinOptimalGrowthTemperature, Plant.DefaultMaxOptimalGrowthTemperature,
                Plant.DefaultMaxGrowthTemperature);
            growingLeftThisYear = growFactors.Take(Math.Max(0, AheadMath.DaysAYear - dayOfYear)).Sum();

            var fertility = map.fertilityGrid;
            foreach (var zone in map.zoneManager.AllZones.OfType<Zone_Growing>())
            {
                var plant = zone.GetPlantDefToGrow();
                var pp = plant?.plant;
                var product = pp?.harvestedThingDef;
                if (product == null || zone.Cells.Count == 0) continue;
                bool food = KindOf(product) == AheadMath.Raw || KindOf(product) == AheadMath.Feed;
                float fert = 0f;
                int sown = 0;
                foreach (var c in zone.Cells)
                {
                    fert += fertility.FertilityAt(c);
                    if (c.GetPlant(map)?.def == plant) sown++;
                }
                fert /= zone.Cells.Count;
                float factor = AheadMath.FertilityFactor(fert, pp.fertilitySensitivity);
                float worth = food ? NutritionOf(product) : 1f;
                // A zone under a roof grows by its room's warmth, the same every day.
                float[] own;
                var room = zone.Cells[0].GetRoom(map);
                if (room != null && !room.UsesOutdoorTemperature)
                {
                    var temps = new float[AheadMath.DaysAYear];
                    for (int i = 0; i < temps.Length; i++) temps[i] = room.Temperature;
                    own = FactorsFor(plant, temps);
                }
                else own = FactorsFor(plant, dayTemps);
                stock.TryGetValue(product, out int have);
                float used = 0f;
                if (measured) record.used.TryGetValue(product, out used);
                var f = new AheadField
                {
                    key = zone.ID.ToString(), label = zone.label, crop = plant.defName, cropLabel = plant.label, product = product.defName, productLabel = product.label,
                    food = food, cells = zone.Cells.Count, sown = sown, perCellADay = AheadMath.PerCellADay(pp.harvestYield, worth, pp.growDays, factor, YieldFactor(plant)),
                    growFactors = own, stock = have, stockNutrition = food ? have * worth : 0f, useADay = used, measured = measured,
                    harvest = zone.Cells.Count * pp.harvestYield * YieldFactor(plant), stockCells = goods.FirstOrDefault(g => g.key == product.defName)?.cells ?? 0
                };
                fields.Add(f);
                zones[f.key] = zone;
                plants[plant.defName] = plant;
            }

            // The free fertile ground beside the best few food fields (the planner grows two at most a time).
            foreach (var f in fields.Where(f => f.food && zones[f.key].allowSow).OrderByDescending(f => f.perCellADay * AheadMath.GrowingDays(f.growFactors)).ThenBy(f => f.key).Take(FreeLooked))
                f.freeNext = Sowing.Around(map, plants[f.crop], zones[f.key], AheadMath.MaxCellsAStep).Count;

            // The food crops the colony can sow, on its usual soil.
            foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!FoodCrop(d)) continue;
                var pp = d.plant;
                var crop = new AheadCrop
                {
                    key = d.defName, label = d.label, growFactors = FactorsFor(d, dayTemps),
                    perCellADay = AheadMath.PerCellADay(pp.harvestYield, NutritionOf(pp.harvestedThingDef), pp.growDays, AheadMath.FertilityFactor(1f, pp.fertilitySensitivity), YieldFactor(d))
                };
                if (crop.perCellADay <= 0f) continue;
                crops.Add(crop);
                plants[d.defName] = d;
            }
            // Free ground near the fields for a new one, for the best crop.
            var best = crops.OrderByDescending(c => c.perCellADay * AheadMath.GrowingDays(c.growFactors)).FirstOrDefault();
            newGround = best != null ? Sowing.NewGround(map, plants[best.key], AheadMath.MaxCellsAStep).Count : 0;
        }

        // ---------------- The benches and the bills ----------------

        /// <summary>A bench's kind of work for a recipe: the recipe's own, else the work type whose bill-giver lists the bench.</summary>
        public static WorkTypeDef WorkTypeFor(Building_WorkTable bench, RecipeDef recipe)
        {
            if (recipe?.requiredGiverWorkType != null) return recipe.requiredGiverWorkType;
            if (bench == null) return null;
            foreach (var wg in DefDatabase<WorkGiverDef>.AllDefsListForReading)
                if (wg.fixedBillGiverDefs != null && wg.fixedBillGiverDefs.Contains(bench.def) && wg.workType != null) return wg.workType;
            return null;
        }

        /// <summary>Who works here: the colonists and the colony's slaves on the map.</summary>
        public static IEnumerable<Pawn> Hands(Map map) => map.mapPawns.FreeColonistsSpawned.Concat(map.mapPawns.SlavesOfColonySpawned).Distinct();

        /// <summary>Whether a colonist could work the bill: the work open to them, the skill the recipe needs, the bill's own limits.</summary>
        public static bool Able(Pawn p, Bill_Production bill, WorkTypeDef work)
        {
            if (p == null || p.Dead || p.Downed || p.workSettings == null) return false;
            if (work != null && p.WorkTypeIsDisabled(work)) return false;
            var recipe = bill.recipe;
            if (recipe.workSkill != null)
            {
                var skill = p.skills?.GetSkill(recipe.workSkill);
                if (skill == null || skill.TotallyDisabled) return false;
                if (skill.Level < bill.allowedSkillRange.min || skill.Level > bill.allowedSkillRange.max) return false;
            }
            if (recipe.skillRequirements != null && recipe.skillRequirements.Any(r => r != null && !r.PawnSatisfies(p))) return false;
            if (bill.PawnRestriction != null && bill.PawnRestriction != p) return false;
            if (bill.SlavesOnly && !p.IsSlave) return false;
            return true;
        }

        private static float WorkOf(RecipeDef recipe)
        {
            try
            {
                return recipe.WorkAmountTotal(null);
            }
            catch
            {
                return recipe.workAmount;
            }
        }

        private void Benches()
        {
            var benches = map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().Where(b => b.Spawned && b.BillStack != null).ToList();
            var workers = Hands(map).Where(p => !p.Dead && p.workSettings != null && !p.DevelopmentalStage.Juvenile()).ToList();
            // How many benches with work to do there are for each kind of work: a hand's day is shared among them.
            var busy = new Dictionary<WorkTypeDef, int>();
            foreach (var b in benches)
            {
                var active = b.BillStack.Bills.OfType<Bill_Production>().FirstOrDefault(x => !x.suspended && x.recipe != null);
                var w = active == null ? null : WorkTypeFor(b, active.recipe);
                if (w == null) continue;
                busy.TryGetValue(w, out int n);
                busy[w] = n + 1;
            }
            var kinds = new HashSet<string> { AheadMath.Meal, AheadMath.Medicine, AheadMath.Feed };
            var keys = new HashSet<string>();
            foreach (var bench in benches)
                foreach (var bill in bench.BillStack.Bills.OfType<Bill_Production>())
                {
                    if (bill.recipe == null || bill.DeletedOrDereferenced) continue;
                    var product = bill.recipe.ProducedThingDef ?? (bill.recipe.products != null && bill.recipe.products.Count > 0 ? bill.recipe.products[0].thingDef : null);
                    string kind = KindOf(product);
                    if (kind == null || !kinds.Contains(kind)) continue;
                    string key = bench.thingIDNumber + ":" + bill.recipe.defName;
                    for (int i = 2; keys.Contains(key); i++) key = bench.thingIDNumber + ":" + bill.recipe.defName + ":" + i;
                    keys.Add(key);
                    var b = new AheadBill
                    {
                        key = key, product = product.defName, label = product.label, bench = bench.LabelCap.ToString(), kind = kind,
                        unit = kind == AheadMath.Medicine ? 1f : Mathf.Max(0.01f, NutritionOf(product)),
                        suspended = bill.suspended, managed = !BillMods.GameMode(bill)
                    };
                    if (b.managed) b.mode = AheadMath.Other;
                    else if (bill.repeatMode == BillRepeatModeDefOf.Forever) b.mode = AheadMath.Forever;
                    else if (bill.repeatMode == BillRepeatModeDefOf.RepeatCount)
                    {
                        b.mode = AheadMath.Times;
                        b.target = bill.repeatCount * (int)Chains.PerBatch(bill.recipe, product);
                    }
                    else
                    {
                        b.mode = AheadMath.Until;
                        b.target = bill.targetCount;
                    }
                    b.linked = BillMods.Linked(bill).Count;
                    // What its bench can turn out a day: the best able hand's speed there, their day shared among the benches with work of the kind.
                    var work = WorkTypeFor(bench, bill.recipe);
                    var hand = workers.Where(p => Able(p, bill, work)).OrderByDescending(p => HandSpeed(p, bill.recipe)).FirstOrDefault();
                    if (hand != null)
                    {
                        int shared = work != null && busy.TryGetValue(work, out int n) ? n : 1;
                        float speed = ChainMath.BenchSpeed(HandSpeed(hand, bill.recipe), Chains.BenchFactor(bench, bill.recipe));
                        b.canMakeADay = ChainMath.BatchesADay(WorkOf(bill.recipe), speed, ChainMath.HandHours(shared)) * Chains.PerBatch(bill.recipe, product);
                    }
                    else b.canMakeADay = 0f;
                    // Another bench of a kind that could take the bill, with no bill of the recipe on it.
                    var free = benches.FirstOrDefault(o => o != bench && o.def.AllRecipes != null && o.def.AllRecipes.Contains(bill.recipe) && Chains.BenchFactor(o, bill.recipe) > 0f
                                                           && !o.BillStack.Bills.Any(x => x.recipe == bill.recipe) && AvailableOn(bill.recipe, o));
                    if (free != null)
                    {
                        b.freeBench = free.LabelCap.ToString();
                        freeBenchOf[key] = free;
                    }
                    bills.Add(b);
                    billOf[key] = (bench, bill);
                }
            // For a kind with no bill at all, the bill that could be added: the recipe at a bench here, its parts in stock first, the quickest for what it gives.
            foreach (var kind in kinds)
            {
                if (bills.Any(x => x.kind == kind)) continue;
                AheadAdd pick = null;
                float pickScore = float.MinValue;
                foreach (var bench in benches)
                    foreach (var recipe in bench.def.AllRecipes ?? new List<RecipeDef>())
                    {
                        if (recipe == null || !recipe.AvailableNow || recipe.products == null || recipe.products.Count == 0 || !AvailableOn(recipe, bench)) continue;
                        var product = recipe.ProducedThingDef ?? recipe.products[0].thingDef;
                        if (KindOf(product) != kind) continue;
                        float unit = kind == AheadMath.Medicine ? 1f : Mathf.Max(0.01f, NutritionOf(product));
                        float score = (PartsInStock(recipe) ? 1000f : 0f) + unit * Chains.PerBatch(recipe, product) * 2500f / Mathf.Max(1f, WorkOf(recipe));
                        if (score <= pickScore) continue;
                        pickScore = score;
                        string key = bench.thingIDNumber + ":" + recipe.defName;
                        pick = new AheadAdd { key = key, kind = kind, product = product.defName, label = product.label, bench = bench.LabelCap.ToString(), unit = unit };
                        addOf[key] = (bench, recipe, product);
                    }
                if (pick != null) canAdd.Add(pick);
            }
        }

        private static bool AvailableOn(RecipeDef recipe, Building_WorkTable bench)
        {
            try
            {
                return recipe.AvailableOnNow(bench, null);
            }
            catch
            {
                return true;
            }
        }

        private static float HandSpeed(Pawn p, RecipeDef recipe)
        {
            if (recipe.workSpeedStat == null) return 1f;
            try
            {
                return p.GetStatValue(recipe.workSpeedStat);
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>A batch's parts are in stock.</summary>
        private bool PartsInStock(RecipeDef recipe)
        {
            if (recipe.ingredients == null) return true;
            foreach (var ing in recipe.ingredients)
            {
                if (ing?.filter == null) continue;
                bool any = false;
                foreach (var d in ing.filter.AllowedThingDefs)
                    if (recipe.IsIngredient(d) && stock.TryGetValue(d, out int n) && n >= ing.CountRequiredOfFor(d, recipe, null))
                    {
                        any = true;
                        break;
                    }
                if (!any) return false;
            }
            return true;
        }
    }
}
