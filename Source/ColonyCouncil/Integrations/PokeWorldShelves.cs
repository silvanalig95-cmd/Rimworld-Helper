using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// PokéWorld's items on the shelves that carry everything else (the player's request, 20 November 2026: "there's a
    /// bunch of items from the Poke World mod, for example, the Dawn Stone ... that I cannot put into large storage
    /// shelves ... It's just not in the list of available items"). PokéWorld lists its items under categories of its own
    /// (stones, fossils, balls, TMs, healing items, eggs, leathers, belts), and a shelf whose filter names the game's own
    /// categories (Deep Storage's, Deep Storage Plus's, RimFridge's and the like) doesn't take them or list them.
    ///
    /// Once every def has loaded (queued, so after every mod's startup classes), each of PokéWorld's items that can be
    /// stored and sits in none of the game's categories is also placed in the game's own category of the same kind, read
    /// from its fields (<see cref="PokeWorldShelvesMath.KindOf"/>): the belts with utility apparel, the leather with
    /// leathers, the eggs with fertilized eggs, the stones, fossils, balls, TMs and potions with the miscellaneous items.
    /// Every storage building whose fixed filter takes the whole of that category then takes them too, and its default
    /// settings where they take the whole of it, so a new shelf has them ticked; a fridge takes only those that spoil and
    /// eggs. The filters' display roots are worked out again, so the filter window lists them. A storage building's own
    /// limits (Deep Storage's size, mass and stack counts) are its mod's, checked when something is stored, and still
    /// apply. PokéWorld's own buildings, its Pokémon and anything not an item are left alone.
    ///
    /// Shelves already built in a save take them once, at load (<see cref="AllowOnBuilt"/>, CouncilGameComponent's
    /// pokeShelvesVersion), where the shelf's own settings take the whole of that category; never where they leave any of
    /// it out. The setting pokemonShelves (Your other mods, PokéWorld; on by default) undoes the placing when switched off
    /// (<see cref="Apply"/>, also from the settings window). Nothing happens without PokéWorld.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PokeWorldShelves
    {
        /// <summary>Each item placed, with the game's category it was placed in.</summary>
        private static readonly List<(ThingDef item, ThingCategoryDef kind)> placed = new List<(ThingDef, ThingCategoryDef)>();
        /// <summary>Each item a filter was made to allow, to undo it.</summary>
        private static readonly List<(ThingFilter filter, ThingDef item)> allowed = new List<(ThingFilter, ThingDef)>();
        /// <summary>The game's own things in each category used (PokéWorld's left out), as they were before the placing.</summary>
        private static readonly Dictionary<ThingCategoryDef, List<ThingDef>> kindsOf = new Dictionary<ThingCategoryDef, List<ThingDef>>();
        /// <summary>The storage buildings that take PokéWorld's items now, with the categories they take (the log and the self-check).</summary>
        private static readonly Dictionary<ThingDef, List<string>> takers = new Dictionary<ThingDef, List<string>>();

        private static readonly FieldInfo CategoriesField = AccessTools.Field(typeof(ThingFilter), "categories");
        private static readonly FieldInfo DisallowedCategoriesField = AccessTools.Field(typeof(ThingFilter), "disallowedCategories");

        private static string failed;

        /// <summary>The items are placed and the storage filters take them, this session.</summary>
        public static bool Applied { get; private set; }

        public static bool Wanted => ColonyCouncilMod.Settings?.pokemonShelves ?? true;

        static PokeWorldShelves()
        {
            // Queued: a storage mod may still set its shelves' filters in its own startup class.
            LongEventHandler.ExecuteWhenFinished(Apply);
        }

        /// <summary>Why it does nothing ("" when it works): no PokéWorld, the setting off, or an error this session.</summary>
        public static string StandsDownWhy() =>
            !PokeWorldLink.Active ? "PokéWorld isn't loaded" : failed != null ? "it failed this session (" + failed + ")" : !Wanted ? "the setting is off" : "";

        /// <summary>The setting into the defs: placed when on, undone when off. Safe to call any time.</summary>
        public static void Apply()
        {
            if (!PokeWorldLink.Active || failed != null) return;
            try
            {
                if (Wanted == Applied) return;
                if (Applied) Undo();
                else Place();
            }
            catch (Exception e)
            {
                failed = e.GetType().Name;
                CouncilLog.ErrorOnce("PokéWorld's items on the shelves", e);
                try
                {
                    Undo();
                }
                catch (Exception e2)
                {
                    CouncilLog.ErrorOnce("PokéWorld's items on the shelves, undoing", e2);
                }
            }
        }

        // ===================== The placing (once a session) =====================

        private static void Place()
        {
            var pack = PokeWorldLink.ModPack;
            if (pack == null) return;
            var plan = new List<(ThingDef item, ThingCategoryDef kind)>();
            foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (d?.modContentPack != pack || d.thingCategories == null) continue;
                bool item = d.category == ThingCategory.Item && d.race == null && !d.IsCorpse && (d.thingClass == null || !typeof(MinifiedThing).IsAssignableFrom(d.thingClass));
                bool others = d.thingCategories.Any(c => c != null && c.modContentPack != pack);
                if (!PokeWorldShelvesMath.ToPlace(item, d.EverStorable(false), d.isUnfinishedThing, others)) continue;
                var kind = CategoryFor(PokeWorldShelvesMath.KindOf(Facts(d)));
                if (kind == null || kind.ContainedInThisOrDescendant(d)) continue;
                plan.Add((d, kind));
            }
            if (plan.Count == 0) return;

            // The game's own things of each category, before PokéWorld's join them: what "the whole of it" means.
            foreach (var kind in plan.Select(p => p.kind).Distinct())
                kindsOf[kind] = kind.DescendantThingDefs.Where(k => k != null && k.modContentPack != pack && k.EverStorable(false)).Distinct().ToList();

            foreach (var (item, kind) in plan)
            {
                item.thingCategories.Add(kind);
                kind.childThingDefs.Add(item);
                placed.Add((item, kind));
            }
            Refresh(placed.Select(p => p.kind));
            Applied = true;

            var byKind = placed.GroupBy(p => p.kind).ToList();
            foreach (var b in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                var own = b?.building?.fixedStorageSettings?.filter;
                var defaults = b?.building?.defaultStorageSettings?.filter;
                if ((own == null && defaults == null) || b.modContentPack == pack) continue;
                bool fridge = IsFridge(b);
                var took = new List<string>();
                foreach (var g in byKind)
                {
                    var kinds = kindsOf[g.Key];
                    // No fixed filter: the building takes everything that can be stored, PokéWorld's items with it.
                    bool fixedTakes = own == null || PokeWorldShelvesMath.TakesWhole(Listed(own, g.Key), ListedOut(own, g.Key), kinds.Count, kinds.Count(own.Allows));
                    if (!fixedTakes) continue;
                    var within = own == null ? kinds : kinds.Where(own.Allows).ToList();
                    bool defaultTakes = defaults != null
                                        && PokeWorldShelvesMath.TakesWhole(Listed(defaults, g.Key), ListedOut(defaults, g.Key), within.Count, within.Count(defaults.Allows));
                    bool any = false;
                    foreach (var (item, _) in g)
                    {
                        if (!PokeWorldShelvesMath.Allow(true, fridge, Rots(item), g.Key.defName)) continue;
                        if (own != null) Allow(own, item);
                        if (defaultTakes) Allow(defaults, item);
                        any |= own != null || defaultTakes;
                    }
                    if (any) took.Add(g.Key.defName);
                }
                if (took.Count == 0) continue;
                own?.RecalculateDisplayRootCategory();
                defaults?.RecalculateDisplayRootCategory();
                takers[b] = took;
            }
            if (Prefs.DevMode)
                Log.Message("[Rimfolk] PokéWorld's items on the shelves: " + placed.Count + " placed beside the game's own (" + PokeWorldShelvesMath.Summary(placed.Select(p => p.kind.defName))
                            + "); " + takers.Count + " storage buildings take some: "
                            + string.Join("; ", takers.OrderBy(kv => kv.Key.defName).Select(kv => kv.Key.defName + " (" + string.Join(", ", kv.Value) + ")")) + ".");
        }

        private static void Undo()
        {
            foreach (var (filter, item) in allowed) filter.SetAllow(item, false);
            foreach (var filter in allowed.Select(a => a.filter).Distinct()) filter.RecalculateDisplayRootCategory();
            foreach (var (item, kind) in placed)
            {
                item.thingCategories?.Remove(kind);
                kind.childThingDefs.Remove(item);
            }
            Refresh(placed.Select(p => p.kind));
            placed.Clear();
            allowed.Clear();
            kindsOf.Clear();
            takers.Clear();
            Applied = false;
        }

        private static void Allow(ThingFilter filter, ThingDef item)
        {
            if (filter.Allows(item)) return;
            filter.SetAllow(item, true);
            allowed.Add((filter, item));
        }

        /// <summary>The categories' cached lists (every thing below each, the sorted list the filter window shows), again for each changed one and those above it.</summary>
        private static void Refresh(IEnumerable<ThingCategoryDef> kinds)
        {
            var all = new HashSet<ThingCategoryDef>();
            foreach (var k in kinds)
                for (var c = k; c != null && all.Add(c); c = c.parent) { }
            foreach (var c in all) c.ResolveReferences();
        }

        // ===================== Shelves already built (once a save) =====================

        /// <summary>
        /// Every storage building on the maps whose own settings (its storage group's, when linked) take the whole of a
        /// category, of what the building itself can take, now takes PokéWorld's items of it too: once a save, at load
        /// (CouncilGameComponent.pokeShelvesVersion). Returns how many were let in.
        /// </summary>
        public static int AllowOnBuilt()
        {
            if (!Applied || Find.Maps == null) return 0;
            var pack = PokeWorldLink.ModPack;
            var byKind = placed.GroupBy(p => p.kind).ToList();
            var seen = new HashSet<StorageSettings>();
            int n = 0;
            foreach (var map in Find.Maps)
            {
                var places = map?.haulDestinationManager?.AllHaulDestinationsListForReading;
                if (places == null) continue;
                foreach (var place in places.ToList())
                {
                    if (!(place is Building b) || b.def?.building == null || b.def.modContentPack == pack) continue;
                    var own = place.GetStoreSettings();
                    if (own?.filter == null || !seen.Add(own)) continue;
                    var parent = place.GetParentStoreSettings()?.filter;
                    bool fridge = IsFridge(b.def);
                    foreach (var g in byKind)
                    {
                        var within = parent == null ? kindsOf[g.Key] : kindsOf[g.Key].Where(parent.Allows).ToList();
                        if (!PokeWorldShelvesMath.TakesWhole(false, false, within.Count, within.Count(own.filter.Allows))) continue;
                        foreach (var (item, _) in g)
                        {
                            if ((parent != null && !parent.Allows(item)) || own.filter.Allows(item) || !PokeWorldShelvesMath.Allow(true, fridge, Rots(item), g.Key.defName)) continue;
                            own.filter.SetAllow(item, true);
                            n++;
                        }
                    }
                }
            }
            if (Prefs.DevMode && n > 0) Log.Message("[Rimfolk] PokéWorld's items on the shelves: " + n + " let in on the shelves already built.");
            return n;
        }

        // ===================== Reading the defs =====================

        /// <summary>What an item is, from its own fields.</summary>
        private static ShelfItemFacts Facts(ThingDef d)
        {
            var ing = d.ingestible;
            var stuff = d.IsStuff ? d.stuffProps?.categories : null;
            return new ShelfItemFacts
            {
                apparel = d.IsApparel,
                beltLayer = d.apparel?.layers != null && d.apparel.layers.Contains(ApparelLayerDefOf.Belt),
                onHead = d.apparel?.bodyPartGroups != null && d.apparel.bodyPartGroups.Any(g => g == BodyPartGroupDefOf.UpperHead || g == BodyPartGroupDefOf.FullHead),
                weapon = d.IsWeapon,
                ranged = d.IsRangedWeapon,
                tool = d.equipmentType == EquipmentType.Primary,
                hatches = d.comps != null && d.comps.Any(c => c is CompProperties_Hatcher || PokeWorldShelvesMath.IsHatcherType(c?.GetType().Name) || PokeWorldShelvesMath.IsHatcherType(c?.compClass?.Name)),
                medicine = d.IsMedicine,
                drug = d.IsDrug,
                food = d.IsNutritionGivingIngestible,
                meal = ing != null && ing.preferability >= FoodPreferability.MealAwful,
                meat = ing != null && (ing.foodType & FoodTypeFlags.Meat) != 0,
                plantFood = ing != null && (ing.foodType & (FoodTypeFlags.VegetableOrFruit | FoodTypeFlags.Seed)) != 0,
                animalProduct = ing != null && (ing.foodType & FoodTypeFlags.AnimalProduct) != 0,
                leathery = stuff != null && stuff.Contains(StuffCategoryDefOf.Leathery),
                fabric = stuff != null && stuff.Contains(StuffCategoryDefOf.Fabric),
                otherStuff = stuff != null && stuff.Count > 0
            };
        }

        /// <summary>The game's category of that name, else the nearest one above it that is loaded.</summary>
        private static ThingCategoryDef CategoryFor(string kind)
        {
            foreach (var name in PokeWorldShelvesMath.WithParents(kind))
            {
                var c = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(name);
                if (c != null) return c;
            }
            return null;
        }

        private static bool Rots(ThingDef d) => d.GetCompProperties<CompProperties_Rottable>() != null;

        /// <summary>A storage building that keeps things cold: its class or one of its comps is a fridge's (RimFridge's and the like).</summary>
        private static bool IsFridge(ThingDef b) =>
            PokeWorldShelvesMath.IsFridgeType(b.thingClass?.FullName)
            || (b.comps != null && b.comps.Any(c => c != null && (PokeWorldShelvesMath.IsFridgeType(c.GetType().FullName) || PokeWorldShelvesMath.IsFridgeType(c.compClass?.FullName))));

        /// <summary>The filter's own list (its def's XML) names the category or one above it.</summary>
        private static bool Listed(ThingFilter filter, ThingCategoryDef kind)
        {
            if (!(CategoriesField?.GetValue(filter) is List<string> names) || names.Count == 0) return false;
            for (var c = kind; c != null; c = c.parent)
                if (names.Contains(c.defName)) return true;
            return false;
        }

        /// <summary>The filter's own list leaves out the category, one above it or one below it.</summary>
        private static bool ListedOut(ThingFilter filter, ThingCategoryDef kind)
        {
            if (!(DisallowedCategoriesField?.GetValue(filter) is List<string> names) || names.Count == 0) return false;
            foreach (var name in names)
            {
                var c = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(name);
                if (c == null) continue;
                if (c == kind || kind.Parents.Contains(c) || c.Parents.Contains(kind)) return true;
            }
            return false;
        }

        // ===================== The self-check =====================

        /// <summary>The self-check's line (null without PokéWorld), and whether it is a warning.</summary>
        public static (string line, bool warn) CheckLine()
        {
            if (!PokeWorldLink.Active) return (null, false);
            string why = StandsDownWhy();
            if (why.Length > 0) return ("PokéWorld's items on the shelves: standing down (" + why + ").", failed != null);
            if (!Applied) return ("PokéWorld's items on the shelves: none of PokéWorld's items needed placing.", false);
            return ("PokéWorld's items on the shelves: " + placed.Count + " placed beside the game's own (" + PokeWorldShelvesMath.Summary(placed.Select(p => p.kind.defName)) + "); "
                    + takers.Count + " storage buildings take some ("
                    + string.Join("; ", takers.OrderBy(kv => kv.Key.label).Select(kv => kv.Key.LabelCap.ToString() + ": " + string.Join(", ", kv.Value))) + ").", false);
        }
    }
}
