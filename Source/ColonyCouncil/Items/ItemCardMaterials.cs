using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The item card's reading of a material (Rimfolk's look, stage 3, point 5, step 2): what the game files as raw and
    /// manufactured resources, stuff and chunks, read from the defs so the player's mods' materials come in on their own.
    /// Once a kind: its stuff numbers (the game's stuff powers, stat factors and offsets, damage multipliers), Rimfolk's roles
    /// for it, what is made of it (every building, floor and recipe looked through once), what yields it (rocks, trees,
    /// animals, recipes that turn it into something), what burns it. Every ten seconds while the card is open: what the
    /// colony can make of it now, its stock, what waits on it (blueprints and frames, bills, repairs, fuel) against the trade
    /// advice's reserve, and where it comes from on this map. Rules and words in Items/ItemCardMaterialMath.cs.
    /// </summary>
    public static partial class ItemCardLive
    {
        /// <summary>A thing made of the material, or with it: a building or floor (its def), or a recipe's product.</summary>
        private sealed class Made
        {
            public BuildableDef def;
            public RecipeDef recipe;
            /// <summary>0 a building or floor, 1 apparel, 2 a weapon, 3 anything else (an item, a work of art).</summary>
            public int kind;
            public float worth;
        }

        /// <summary>A material's own numbers, read once.</summary>
        private sealed class MatPart
        {
            public StuffNumbers stuff;
            public string noun = "material", filed = "";
            public bool chunk, precious, gift, money, explosive, ofPerson, sturdiest, deep;
            public readonly List<Made> made = new List<Made>();
            public readonly List<ThingDef> rocks = new List<ThingDef>(), trees = new List<ThingDef>();
            public readonly List<ThingDef> leather = new List<ThingDef>(), wool = new List<ThingDef>(), spawn = new List<ThingDef>();
            public readonly List<RecipeDef> into = new List<RecipeDef>();
            public readonly HashSet<ThingDef> burners = new HashSet<ThingDef>();
        }

        private static readonly Dictionary<ThingDef, StuffNumbers> stuffNumbers = new Dictionary<ThingDef, StuffNumbers>();
        private static List<ThingDef> allStuffs;
        private static float breakdownDays = -1f;

        // ---------------------------------------------------------------- once a kind

        private static void ReadMaterial(DefPart p)
        {
            var d = p.def;
            var mp = p.mat = new MatPart();
            var cats = d.stuffProps?.categories?.Where(c => c != null).Select(c => c.defName).ToList() ?? new List<string>();
            var filed = d.thingCategories?.Where(c => c != null).Select(c => c.defName).ToList() ?? new List<string>();
            mp.chunk = d.IsWithinCategory(ThingCategoryDefOf.Chunks) || d.IsWithinCategory(ThingCategoryDefOf.StoneChunks);
            mp.noun = ItemCardMath.NounFor(cats, filed, mp.chunk);
            mp.filed = d.FirstThingCategory?.LabelCap.ToString() ?? "";
            mp.stuff = d.IsStuff ? NumbersOf(d) : null;
            mp.precious = ThingRoles.Fits(ThingRole.Precious, d);
            mp.gift = ThingRoles.Fits(ThingRole.Gift, d);
            mp.money = d == ThingDefOf.Silver;
            mp.explosive = d.GetCompProperties<CompProperties_Explosive>() != null;
            mp.ofPerson = ThingRoles.Facts(d)?.humanlike == true;
            mp.deep = d.deepCommonality > 0f;
            if (mp.stuff != null) mp.sturdiest = Sturdiest(d, mp.stuff);
            MadeOf(d, mp);
            Yields(d, mp);
        }

        /// <summary>What a stuff does to what is made of it, by the game's own stats on it (once a stuff).</summary>
        private static StuffNumbers NumbersOf(ThingDef s)
        {
            if (stuffNumbers.TryGetValue(s, out var n)) return n;
            n = new StuffNumbers { Def = s.defName, Label = s.label ?? s.defName };
            try
            {
                n.Categories = s.stuffProps?.categories?.Where(c => c != null).Select(c => c.defName).ToList() ?? new List<string>();
                n.Hard = ItemCardMath.IsHard(n.Categories);
                n.ArmorSharp = Stat(s, StatDefOf.StuffPower_Armor_Sharp);
                n.ArmorBlunt = Stat(s, StatDefOf.StuffPower_Armor_Blunt);
                n.ArmorHeat = Stat(s, StatDefOf.StuffPower_Armor_Heat);
                n.InsulationCold = Stat(s, StatDefOf.StuffPower_Insulation_Cold);
                n.InsulationHeat = Stat(s, StatDefOf.StuffPower_Insulation_Heat);
                n.Apparel = s.statBases != null && s.statBases.Any(m => m?.stat != null && m.stat.defName.StartsWith("StuffPower_", StringComparison.Ordinal));
                var factors = s.stuffProps?.statFactors;
                var offsets = s.stuffProps?.statOffsets;
                n.HitPoints = Factor(factors, StatDefOf.MaxHitPoints);
                n.Beauty = Factor(factors, StatDefOf.Beauty);
                n.BeautyOffset = offsets != null ? offsets.GetStatOffsetFromList(StatDefOf.Beauty) : 0f;
                n.Flammability = Factor(factors, StatDefOf.Flammability);
                n.WorkToMake = Factor(factors, StatDefOf.WorkToMake);
                n.WorkToBuild = Factor(factors, StatDefOf.WorkToBuild);
                n.Weapons = s.statBases != null && s.statBases.Any(m => m?.stat == StatDefOf.SharpDamageMultiplier || m?.stat == StatDefOf.BluntDamageMultiplier);
                n.SharpDamage = Stat(s, StatDefOf.SharpDamageMultiplier);
                n.BluntDamage = Stat(s, StatDefOf.BluntDamageMultiplier);
                n.Value = s.BaseMarketValue;
                n.Mass = Stat(s, StatDefOf.Mass);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the item card reading a stuff's numbers: " + s.defName, e);
            }
            stuffNumbers[s] = n;
            return n;
        }

        private static float Factor(List<StatModifier> list, StatDef stat) => list == null || stat == null ? 1f : list.GetStatFactorFromList(stat);

        private static List<ThingDef> AllStuffs => allStuffs ??= DefDatabase<ThingDef>.AllDefsListForReading.Where(x => x != null && x.IsStuff && x.stuffProps?.categories != null).ToList();

        /// <summary>The most hit points of every stuff of its first category, the game's and the mods' (none sturdier, and sturdier than steel).</summary>
        private static bool Sturdiest(ThingDef d, StuffNumbers n)
        {
            var first = d.stuffProps?.categories?.FirstOrDefault();
            if (first == null || n.HitPoints <= 1f) return false;
            foreach (var s in AllStuffs)
            {
                if (s == d || !s.stuffProps.categories.Contains(first)) continue;
                if (Factor(s.stuffProps.statFactors, StatDefOf.MaxHitPoints) > n.HitPoints + 0.0001f) return false;
            }
            return true;
        }

        /// <summary>Every building and floor it is the stuff of or a part of, and every recipe that takes it, with what each is worth (the best first). Once a kind.</summary>
        private static void MadeOf(ThingDef d, MatPart mp)
        {
            float perWork = StatWorker_MarketValue.ValuePerWork;
            float CostValue(BuildableDef b) => b.costList?.Where(c => c?.thingDef != null && c.thingDef != d).Sum(c => c.count * c.thingDef.BaseMarketValue) ?? 0f;
            float Own(BuildableDef b) => b.statBases != null ? b.statBases.GetStatValueFromList(StatDefOf.MarketValue, 0f) : 0f;
            float WorkOf(BuildableDef b, StatDef s) => b.statBases != null ? b.statBases.GetStatValueFromList(s, 0f) : 0f;
            void Building(BuildableDef b)
            {
                try
                {
                    bool stuffOf = b.MadeFromStuff && d.stuffProps != null && d.stuffProps.CanMake(b);
                    int part = b.costList?.FirstOrDefault(c => c?.thingDef == d)?.count ?? 0;
                    if (!stuffOf && part <= 0) return;
                    float worth = ItemCardMath.MadeWorth(Own(b), stuffOf ? b.CostStuffCount : 0, d.BaseMarketValue, CostValue(b) + part * d.BaseMarketValue, WorkOf(b, StatDefOf.WorkToBuild), perWork);
                    mp.made.Add(new Made { def = b, kind = 0, worth = worth });
                }
                catch
                {
                    // A mod's building that can't be read: left out.
                }
            }
            foreach (var b in Avail.AllBuildings) Building(b);
            foreach (var t in DefDatabase<TerrainDef>.AllDefsListForReading)
                if (t != null && t.BuildableByPlayer)
                    Building(t);
            var seen = new HashSet<ThingDef>();
            foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                try
                {
                    var product = r?.ProducedThingDef ?? (r?.products != null && r.products.Count > 0 ? r.products[0].thingDef : null);
                    if (product == null || product == d || r.ingredients == null || r.ingredients.Count == 0 || seen.Contains(product)) continue;
                    if (!r.IsIngredient(d)) continue;
                    seen.Add(product);
                    int kind = product.IsApparel ? 1 : product.IsWeapon ? 2 : 3;
                    float worth = ItemCardMath.MadeWorth(Own(product), product.MadeFromStuff ? product.CostStuffCount : 0, d.BaseMarketValue, CostValue(product),
                        WorkOf(product, StatDefOf.WorkToMake), perWork);
                    mp.made.Add(new Made { def = product, recipe = r, kind = kind, worth = worth });
                    // A raw material (not stuff itself) a recipe turns into stuff: chunks into blocks, slag into steel, a mod's ore into its metal.
                    if (!d.IsStuff && product.IsStuff && mp.into.Count < 4) mp.into.Add(r);
                }
                catch
                {
                    // A mod's recipe that can't be read: left out.
                }
            }
            mp.made.Sort((a, b) => b.worth.CompareTo(a.worth));
        }

        /// <summary>What yields it: rocks mined, trees cut, animals butchered, shorn or that make it, and the buildings that burn it. Once a kind.</summary>
        private static void Yields(ThingDef d, MatPart mp)
        {
            foreach (var t in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (t == null) continue;
                try
                {
                    if (t.building?.mineableThing == d && t.building.mineableYield > 0) mp.rocks.Add(t);
                    if (t.plant != null && t.plant.harvestedThingDef == d && t.plant.IsTree) mp.trees.Add(t);
                    if (t.race != null && t.race.Animal)
                    {
                        if (t.race.leatherDef == d) mp.leather.Add(t);
                        if (t.GetCompProperties<CompProperties_Shearable>()?.woolDef == d) mp.wool.Add(t);
                        if (t.comps != null && t.comps.Any(c => c is CompProperties_Spawner sp && sp.thingToSpawn == d)) mp.spawn.Add(t);
                    }
                    var fuel = t.category == ThingCategory.Building ? t.GetCompProperties<CompProperties_Refuelable>() : null;
                    if (fuel?.fuelFilter != null && fuel.fuelFilter.Allows(d)) mp.burners.Add(t);
                }
                catch
                {
                    // A mod's def that can't be read: left out.
                }
            }
        }

        /// <summary>The game's mean days between one machine's breakdowns (CompBreakdownable's own number; 228 days when it can't be read).</summary>
        private static float BreakdownDays()
        {
            if (breakdownDays > 0f) return breakdownDays;
            breakdownDays = 228f;
            try
            {
                var field = AccessTools.Field(typeof(CompBreakdownable), "BreakdownMTBTicks");
                if (field?.GetValue(null) is int ticks && ticks > 0) breakdownDays = ticks / (float)GenDate.TicksPerDay;
            }
            catch
            {
                // The base game's number stays.
            }
            return breakdownDays;
        }

        // ---------------------------------------------------------------- the live parts

        /// <summary>A material's own facts on the card (kept between the colony's readings).</summary>
        private static void LiveMaterial(ItemCardModel m, DefPart p)
        {
            var mp = p.mat;
            if (mp == null) return;
            var mf = m.facts.Material ??= new MaterialFacts();
            mf.Stuff = mp.stuff;
            mf.Noun = mp.noun;
            mf.Chunk = mp.chunk;
            mf.SturdiestOfKind = mp.sturdiest;
            mf.Precious = mp.precious;
            mf.Gift = mp.gift;
            mf.Money = mp.money;
            mf.Explosive = mp.explosive;
            mf.OfPerson = mp.ofPerson;
            mf.FuelAnywhere = mp.burners.Count > 0;
            mf.BreakdownDays = BreakdownDays();
        }

        /// <summary>What kind of material it is: "Metal, things are made of it", or the game's own filing ("Manufactured", "Stone chunks").</summary>
        private static string MaterialKindWords(ItemCardModel m, DefPart p)
        {
            var mp = p.mat;
            if (mp == null) return "";
            if (mp.stuff != null) return W("CC_ItemCard_Kind_Stuff", ItemCardMath.Cap(mp.noun).Named("NOUN"));
            return mp.filed.Length > 0 ? mp.filed : ItemCardMath.Cap(mp.noun);
        }

        // ---------------------------------------------------------------- the colony

        private static void ColonyMaterial(ItemCardModel m, DefPart p)
        {
            var mp = p.mat;
            var f = m.facts;
            if (mp == null) return;
            var mf = f.Material ??= new MaterialFacts();
            var d = m.def;
            var map = m.map;
            mf.Refs.Clear();
            mf.Buildings = mf.Apparel = mf.Weapons = mf.Others = mf.Later = 0;
            mf.LaterResearch = mf.Bench = mf.MakeResearch = mf.RawInto = mf.RawBench = mf.BurnerWords = "";
            mf.Makeable = mf.BenchBuilt = mf.RawBenchBuilt = mf.Counted = false;
            mf.Held = -1;
            mf.BuildWants = mf.BillWants = mf.BillsStarved = mf.Bills = mf.Need = mf.Keep = mf.Machines = mf.Burners = mf.BurnersWhileUsed = 0;
            mf.BurnADay = mf.UseADay = 0f;
            m.madeOf.Clear();
            m.madeLater = null;
            m.demand.Clear();
            m.bills.Clear();
            m.sources.Clear();

            // What it is compared with: the colony's most held of its kind, then the base game's usual.
            if (mp.stuff != null)
            {
                string mostHeld = map != null ? MostHeldOfKind(map, d) : null;
                foreach (var name in ItemCardMath.RefsFor(d.defName, mp.stuff.Hard, mostHeld))
                {
                    var rd = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                    if (rd != null && rd.IsStuff) mf.Refs.Add(NumbersOf(rd));
                }
            }

            MadeNow(m, mp, mf);
            Makes(p, mp, mf, map);
            if (map == null) return;

            var player = Faction.OfPlayer;
            int held = 0;
            foreach (var t in map.listerThings.ThingsOfDef(d))
                if (t != null && t.Spawned && !t.IsForbidden(player)) held += t.stackCount;
            foreach (var x in map.mapPawns.FreeColonistsSpawned)
            {
                var inv = x.inventory?.innerContainer;
                if (inv == null) continue;
                foreach (var t in inv)
                    if (t.def == d) held += t.stackCount;
            }
            mf.Held = held;

            // The trade advice's reserve for it, by its kind and the colony's record of use.
            var needs = ColonyNeeds.For(map);
            int people = Math.Max(1, ColonyNeeds.Hands(map).Count());
            float use = needs?.record != null && needs.measured && needs.record.used.TryGetValue(d, out float u) ? u : 0f;
            var good = TradeAdvice.KindOf(d);
            mf.UseADay = use;
            mf.Counted = TradeAdviceMath.Counted(good, use);
            mf.Need = TradeAdviceMath.Need(good, people, use);
            mf.Keep = TradeAdviceMath.Keep(good, people, use, 0);

            // What waits on it: the blueprints and frames, and the bills.
            try
            {
                mf.BuildWants = ColonyCouncil.Making.BuildingWants(map).TryGetValue(d, out int want) ? want : 0;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the item card reading the blueprints", e);
            }
            Bills(m, mf, map);
            Buildings(m, mp, mf, map);
            Demand(m, mf);
            Sources(m, mp, mf, map);
        }

        /// <summary>The stuff of its kind the colony holds most of, besides itself (by the plan ahead's hourly stock), or null.</summary>
        private static string MostHeldOfKind(Map map, ThingDef d)
        {
            var cats = d.stuffProps?.categories;
            if (cats == null) return null;
            ThingDef best = null;
            int most = 0;
            try
            {
                foreach (var kv in ColonyNeeds.For(map).stock)
                {
                    var s = kv.Key;
                    if (s == null || s == d || !s.IsStuff || s.stuffProps?.categories == null || kv.Value <= most) continue;
                    if (!s.stuffProps.categories.Any(c => cats.Contains(c))) continue;
                    best = s;
                    most = kv.Value;
                }
            }
            catch
            {
                return null;
            }
            return best?.defName;
        }

        /// <summary>What the colony can make of it now (research done), by kind, the best first; and what research would open more.</summary>
        private static void MadeNow(ItemCardModel m, MatPart mp, MaterialFacts mf)
        {
            var now = new List<Made>[4];
            for (int i = 0; i < 4; i++) now[i] = new List<Made>();
            var later = new Dictionary<ResearchProjectDef, int>();
            foreach (var x in mp.made)
            {
                ResearchProjectDef waits = null;
                bool open;
                try
                {
                    if (x.recipe == null)
                    {
                        open = x.def.BuildableByPlayer && x.def.IsResearchFinished;
                        if (!open) waits = x.def.researchPrerequisites?.FirstOrDefault(r => r != null && !r.IsFinished);
                    }
                    else
                    {
                        open = x.recipe.AvailableNow && Avail.RecipeUsers(x.recipe).Any(u => u.IsResearchFinished);
                        if (!open)
                            waits = x.recipe.researchPrerequisite != null && !x.recipe.researchPrerequisite.IsFinished ? x.recipe.researchPrerequisite
                                : x.recipe.researchPrerequisites?.FirstOrDefault(r => r != null && !r.IsFinished)
                                  ?? Avail.RecipeUsers(x.recipe).SelectMany(u => u.researchPrerequisites ?? new List<ResearchProjectDef>()).FirstOrDefault(r => r != null && !r.IsFinished);
                    }
                }
                catch
                {
                    open = false;
                }
                if (open) now[x.kind].Add(x);
                else
                {
                    mf.Later++;
                    if (waits != null) later[waits] = later.TryGetValue(waits, out int n) ? n + 1 : 1;
                }
            }
            mf.Buildings = now[0].Count;
            mf.Apparel = now[1].Count;
            mf.Weapons = now[2].Count;
            mf.Others = now[3].Count;
            var opens = later.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key.defName).FirstOrDefault();
            if (opens.Key != null) mf.LaterResearch = opens.Key.LabelCap.ToString();
            string[] keys = { "CC_ItemCard_Made_Buildings", "CC_ItemCard_Made_Apparel", "CC_ItemCard_Made_Weapons", "CC_ItemCard_Made_Others" };
            for (int i = 0; i < 4; i++)
            {
                if (now[i].Count == 0) continue;
                var names = now[i].Select(x => x.def.label ?? x.def.defName).Distinct().Take(3).ToList();
                string list = string.Join(", ", names);
                if (now[i].Count > names.Count) list += ", " + W("CC_ItemCard_AndMore", (now[i].Count - names.Count).Named("N"));
                m.madeOf.Add((W(keys[i]), W("CC_ItemCard_MadeNow", now[i].Count.Named("N"), list.Named("LIST"))));
            }
            if (mf.Later > 0)
                m.madeLater = opens.Key != null
                    ? W("CC_ItemCard_MadeLater", mf.Later.Named("N"), mf.LaterResearch.Named("RESEARCH"), opens.Value.Named("OPENS"))
                    : W("CC_ItemCard_MadeLaterNone", mf.Later.Named("N"));
        }

        /// <summary>The recipes that make it (research done, their bench and whether one is built) and, for a raw material, what a recipe turns it into.</summary>
        private static void Makes(DefPart p, MatPart mp, MaterialFacts mf, Map map)
        {
            bool Built(ThingDef bench) => map != null && bench != null && map.listerBuildings.AllBuildingsColonistOfDef(bench).Any();
            foreach (var r in p.makes)
            {
                bool open;
                try
                {
                    open = r.AvailableNow;
                }
                catch
                {
                    open = false;
                }
                var users = Avail.RecipeUsers(r);
                if (!open)
                {
                    if (mf.MakeResearch.Length == 0)
                        mf.MakeResearch = (r.researchPrerequisite ?? r.researchPrerequisites?.FirstOrDefault(x => x != null && !x.IsFinished))?.LabelCap.ToString() ?? "";
                    continue;
                }
                mf.Makeable = true;
                var built = users.FirstOrDefault(Built);
                if (built != null)
                {
                    mf.BenchBuilt = true;
                    mf.Bench = built.label;
                    break;
                }
                if (mf.Bench.Length == 0) mf.Bench = (users.FirstOrDefault(u => u.IsResearchFinished) ?? users.FirstOrDefault())?.label ?? "";
            }
            foreach (var r in mp.into)
            {
                var product = r.ProducedThingDef ?? r.products?.FirstOrDefault()?.thingDef;
                var users = Avail.RecipeUsers(r);
                var built = users.FirstOrDefault(Built);
                if (mf.RawInto.Length > 0 && built == null) continue;
                mf.RawInto = product?.label ?? "";
                mf.RawBench = (built ?? users.FirstOrDefault(u => u.IsResearchFinished) ?? users.FirstOrDefault())?.label ?? "";
                mf.RawBenchBuilt = built != null;
                if (built != null) break;
            }
        }

        /// <summary>The colony's bills that take it: each a line going to its bench; what finite bills need in all; those that can't start for want of it.</summary>
        private static void Bills(ItemCardModel m, MaterialFacts mf, Map map)
        {
            var d = m.def;
            int Have(ThingDef x) => MaterialCounts.Have(map, x);
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (!(b is Building_WorkTable wt) || wt.BillStack == null) continue;
                foreach (var bill in wt.BillStack)
                {
                    try
                    {
                        if (!(bill is Bill_Production bp) || bp.suspended || bp.recipe?.ingredients == null || !bp.recipe.IsIngredient(d)) continue;
                        if (bp.ingredientFilter != null && !bp.ingredientFilter.Allows(d)) continue;
                        var ing = bp.recipe.ingredients.FirstOrDefault(i => i?.filter != null && i.filter.Allows(d));
                        if (ing == null || !ColonyCouncil.Making.StillWanted(bp, Have)) continue;
                        int batch = Mathf.Max(1, Mathf.CeilToInt(ing.CountRequiredOfFor(d, bp.recipe)));
                        mf.Bills++;
                        // What a finite bill needs in all: so many to do, or up to its target.
                        int batches = 0;
                        if (bp.repeatMode == BillRepeatModeDefOf.RepeatCount) batches = bp.repeatCount;
                        else if (bp.repeatMode == BillRepeatModeDefOf.TargetCount)
                        {
                            var product = bp.recipe.ProducedThingDef;
                            int each = Mathf.Max(1, bp.recipe.products?.FirstOrDefault()?.count ?? 1);
                            batches = product == null ? 1 : Mathf.CeilToInt(Mathf.Max(0, bp.targetCount - Have(product)) / (float)each);
                        }
                        mf.BillWants += batches * batch;
                        // It can't start: not enough of it, and nothing else its ingredient allows is in store.
                        bool starved = mf.Held >= 0 && mf.Held < batch && !ing.filter.AllowedThingDefs.Where(x => x != d).Take(40).Any(x => (bp.ingredientFilter == null || bp.ingredientFilter.Allows(x)) && Have(x) >= ing.CountRequiredOfFor(x, bp.recipe));
                        if (starved) mf.BillsStarved++;
                        if (m.bills.Count >= 6) continue;
                        var bench = wt;
                        m.bills.Add(new ItemGoLine
                        {
                            text = W(starved ? "CC_ItemCard_BillStarved" : "CC_ItemCard_BillTakes", bp.recipe.LabelCap.Named("RECIPE"), bench.LabelCap.Named("BENCH"), batch.Named("BATCH"),
                                bp.RepeatInfoText.Named("REPEAT")),
                            tip = W("CC_ItemCard_GoBench_Tip"), go = () => GoToBench(bench), tone = starved ? -1 : 0
                        });
                    }
                    catch
                    {
                        // A mod's bill that can't be read: left out.
                    }
                }
            }
        }

        /// <summary>The colony's buildings: the machines that break down (components), what burns it, what is built of it (deconstruction).</summary>
        private static void Buildings(ItemCardModel m, MatPart mp, MaterialFacts mf, Map map)
        {
            var d = m.def;
            bool repairs = d == ThingDefOf.ComponentIndustrial;
            var burning = new Dictionary<ThingDef, int>();
            int builtOf = 0;
            float back = 0f;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                try
                {
                    if (repairs && b.GetComp<CompBreakdownable>() != null) mf.Machines++;
                    if (mp.burners.Contains(b.def))
                    {
                        var fuel = b.GetComp<CompRefuelable>();
                        var flick = b.GetComp<CompFlickable>();
                        if (fuel != null && (flick == null || flick.SwitchIsOn))
                        {
                            float aDay = fuel.Props.fuelConsumptionRate / Mathf.Max(0.01f, fuel.Props.FuelMultiplierCurrentDifficulty);
                            if (fuel.Props.consumeFuelOnlyWhenUsed) mf.BurnersWhileUsed++;
                            else
                            {
                                mf.Burners++;
                                mf.BurnADay += aDay;
                            }
                            burning[b.def] = burning.TryGetValue(b.def, out int n) ? n + 1 : 1;
                        }
                    }
                    if (b.Stuff == d || b.def.costList != null && b.def.costList.Any(c => c?.thingDef == d))
                    {
                        builtOf++;
                        foreach (var c in b.def.CostListAdjusted(b.Stuff, false))
                            if (c.thingDef == d) back += c.count * b.def.resourcesFractionWhenDeconstructed;
                    }
                }
                catch
                {
                    // A mod's building that can't be read: left out.
                }
            }
            var words = burning.OrderByDescending(kv => kv.Value).Take(3).Select(kv => kv.Value + " " + Find.ActiveLanguageWorker.Pluralize(kv.Key.label ?? kv.Key.defName, kv.Value)).ToList();
            if (burning.Count > 3) words.Add(W("CC_ItemCard_AndMore", (burning.Count - 3).Named("N")));
            mf.BurnerWords = ItemCardMath.AndList(words);
            if (builtOf > 0 && back >= 1f)
                m.sources.Add(new ItemGoLine { text = W("CC_ItemCard_FromDeconstruct", builtOf.Named("N"), Mathf.RoundToInt(back).Named("BACK")), tone = 0 });
        }

        /// <summary>The colony's stock and what waits on it, in rows.</summary>
        private static void Demand(ItemCardModel m, MaterialFacts mf)
        {
            m.demand.Add((W("CC_ItemCard_Row_Held"), W("CC_ItemCard_Held", mf.Held.Named("N"))));
            if (mf.BuildWants > 0) m.demand.Add((W("CC_ItemCard_Row_Blueprints"), W("CC_ItemCard_Blueprints", mf.BuildWants.Named("N"))));
            if (mf.Bills > 0)
                m.demand.Add((W("CC_ItemCard_Row_Bills"), mf.BillWants > 0
                    ? W("CC_ItemCard_BillsNeed", mf.Bills.Named("BILLS"), mf.BillWants.Named("N"))
                    : W("CC_ItemCard_BillsForever", mf.Bills.Named("BILLS"))));
            if (mf.Machines > 0)
                m.demand.Add((W("CC_ItemCard_Row_Repairs"), W("CC_ItemCard_Repairs", mf.Machines.Named("N"), ItemCardMath.N((float)Math.Round(ItemCardMath.RepairsASeason(mf.Machines, mf.BreakdownDays), 1)).Named("SEASON"),
                    ItemCardMath.Days(mf.BreakdownDays).Named("DAYS"))));
            if (mf.Burners + mf.BurnersWhileUsed > 0)
            {
                float days = ItemCardMath.DaysOf(mf.Held, ItemCardMath.UseADay(mf));
                string burn = mf.Burners > 0 ? W("CC_ItemCard_FuelBurns", mf.BurnerWords.Named("WHAT"), ItemCardMath.N((float)Math.Round(mf.BurnADay, 1)).Named("ADAY")) : W("CC_ItemCard_FuelWhileUsed", mf.BurnerWords.Named("WHAT"));
                if (mf.UseADay > 0f) burn += " " + W("CC_ItemCard_FuelRecord", ItemCardMath.N((float)Math.Round(mf.UseADay, 1)).Named("ADAY"));
                if (days >= 0f) burn += " " + W("CC_ItemCard_FuelLasts", ItemCardMath.Days(days).Named("DAYS"));
                m.demand.Add((W("CC_ItemCard_Row_Fuel"), burn));
            }
            int shortBy = ItemCardMath.ShortBy(mf);
            string verdict = shortBy > 0 ? W(mf.BuildWants + mf.BillWants > mf.Held ? "CC_ItemCard_MatShortPlans" : "CC_ItemCard_MatShortNeed", shortBy.Named("N"), mf.Need.Named("NEED"))
                : ItemCardMath.Spare(mf) ? W("CC_ItemCard_MatSpare", (mf.Keep + mf.BuildWants + mf.BillWants).Named("KEEP"))
                : mf.Counted ? W("CC_ItemCard_MatEnough", mf.Need.Named("NEED"))
                : W("CC_ItemCard_MatNoReserve");
            m.demand.Add((W("CC_ItemCard_Row_Verdict"), verdict));
        }

        /// <summary>Where it comes from on this map: deposits (and a jump to one), a deep drill, trees, animals; the recipes and fields come from the shared making lines.</summary>
        private static void Sources(ItemCardModel m, MatPart mp, MaterialFacts mf, Map map)
        {
            float diff = Find.Storyteller?.difficulty?.mineYieldFactor ?? 1f;
            foreach (var rock in mp.rocks)
            {
                var all = map.listerThings.ThingsOfDef(rock);
                int seen = 0;
                Thing first = null;
                foreach (var t in all)
                {
                    if (t == null || !t.Spawned || t.Position.Fogged(map)) continue;
                    seen++;
                    first ??= t;
                }
                if (seen == 0) continue;
                float chance = rock.building.mineableDropChance > 0f ? rock.building.mineableDropChance : 1f;
                int yield = Mathf.RoundToInt(seen * rock.building.mineableYield * diff * chance);
                var go = first;
                m.sources.Add(new ItemGoLine
                {
                    text = W("CC_ItemCard_FromRock", seen.Named("N"), (rock.label ?? rock.defName).Named("ROCK"), yield.Named("YIELD")),
                    tip = W("CC_ItemCard_GoThing_Tip"), go = () => GoTo(go), tone = 1
                });
            }
            if (mp.deep)
            {
                bool scanning = false;
                try
                {
                    scanning = map.deepResourceGrid.AnyActiveDeepScannersOnMap();
                }
                catch
                {
                    // Read as none.
                }
                if (scanning)
                {
                    int spots = 0, count = 0;
                    foreach (var c in map.AllCells)
                    {
                        if (map.deepResourceGrid.ThingDefAt(c) != m.def) continue;
                        spots++;
                        count += map.deepResourceGrid.CountAt(c);
                    }
                    m.sources.Add(new ItemGoLine { text = spots > 0 ? W("CC_ItemCard_FromDeepFound", spots.Named("N"), count.Named("COUNT")) : W("CC_ItemCard_FromDeepNone"), tone = spots > 0 ? 1 : 0 });
                }
                else m.sources.Add(new ItemGoLine { text = W("CC_ItemCard_FromDeep"), tone = 0 });
            }
            foreach (var tree in mp.trees)
            {
                int n = 0;
                float yield = 0f;
                foreach (var t in map.listerThings.ThingsOfDef(tree))
                {
                    if (!(t is Plant plant) || !t.Spawned || t.Position.Fogged(map)) continue;
                    n++;
                    yield += plant.YieldNow();
                }
                if (n > 0) m.sources.Add(new ItemGoLine { text = W("CC_ItemCard_FromTrees", n.Named("N"), (tree.label ?? tree.defName).Named("TREE"), Mathf.RoundToInt(yield).Named("YIELD")), tone = 1 });
            }
            if (mp.leather.Count + mp.wool.Count + mp.spawn.Count > 0)
            {
                int ownLeather = 0, wildLeather = 0, shorn = 0, makes = 0;
                float leather = 0f;
                foreach (var x in map.mapPawns.AllPawnsSpawned)
                {
                    if (x?.RaceProps == null || !x.RaceProps.Animal || x.Dead) continue;
                    bool ours = x.Faction == Faction.OfPlayer;
                    if (!ours && x.Faction != null) continue;
                    if (mp.leather.Contains(x.def))
                    {
                        if (ours) ownLeather++;
                        else wildLeather++;
                        if (ours) leather += x.GetStatValue(StatDefOf.LeatherAmount);
                    }
                    if (ours && mp.wool.Contains(x.def)) shorn++;
                    if (ours && mp.spawn.Contains(x.def)) makes++;
                }
                if (shorn > 0) m.sources.Add(new ItemGoLine { text = W("CC_ItemCard_FromShorn", shorn.Named("N")), tone = 1 });
                if (makes > 0) m.sources.Add(new ItemGoLine { text = W("CC_ItemCard_FromAnimalsMake", makes.Named("N")), tone = 1 });
                if (ownLeather > 0) m.sources.Add(new ItemGoLine { text = W("CC_ItemCard_FromOwnLeather", ownLeather.Named("N"), Mathf.RoundToInt(leather).Named("YIELD")), tone = 0 });
                if (wildLeather > 0) m.sources.Add(new ItemGoLine { text = W("CC_ItemCard_FromWildLeather", wildLeather.Named("N")), tone = 0 });
                if (ownLeather + wildLeather + shorn + makes == 0)
                    m.sources.Add(new ItemGoLine { text = W("CC_ItemCard_FromAnimalsNone", (mp.leather.Concat(mp.wool).Concat(mp.spawn).FirstOrDefault()?.label ?? "?").Named("ANIMAL")), tone = 0 });
            }
        }

        private static void GoTo(Thing t)
        {
            if (t == null || !t.Spawned) return;
            try
            {
                Pages.LeaveForMap();
                CameraJumper.TryJumpAndSelect(t);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the item card going to a thing", e);
            }
        }

        // ---------------------------------------------------------------- the lines

        /// <summary>The comparison (from the facts, so it follows the colony's most held of its kind) and the recipes and fields among the sources.</summary>
        private static void RowsMaterial(ItemCardModel m, DefPart p)
        {
            var mf = m.facts.Material;
            m.stuffHeads.Clear();
            m.stuffTable.Clear();
            m.stuffLabels.Clear();
            m.stuffKinds = "";
            // The shared making lines' "nothing makes it" gives way where the map itself yields it.
            if (m.sources.Count > 0 && m.making.Count == 1 && m.making[0].go == null && m.making[0].text == W("CC_ItemCard_MakeNone")) m.making.Clear();
            if (mf?.Stuff == null) return;
            m.stuffHeads.Add(ItemCardMath.Cap(mf.Stuff.Label));
            foreach (var r in mf.Refs) m.stuffHeads.Add(ItemCardMath.Cap(r.Label));
            foreach (var row in ItemCardMath.StuffTable(mf.Stuff, mf.Refs))
            {
                m.stuffTable.Add(row);
                m.stuffLabels.Add(W("CC_ItemCard_Stuff_" + row.Id));
            }
            var cats = m.def.stuffProps?.categories?.Where(c => c != null).Select(c => c.label ?? c.defName).ToList();
            if (cats != null && cats.Count > 0) m.stuffKinds = W("CC_ItemCard_StuffKinds", string.Join(", ", cats).Named("KINDS"));
        }
    }
}
