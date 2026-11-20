using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// What one of PokéWorld's items is, read from its def's own fields (never its name) by <see cref="PokeWorldShelves"/>:
    /// the facts <see cref="PokeWorldShelvesMath.KindOf"/> needs to find the game's own kind of thing it belongs beside.
    /// </summary>
    public struct ShelfItemFacts
    {
        /// <summary>Worn (the def has apparel properties).</summary>
        public bool apparel;
        /// <summary>Worn on the belt layer, like the game's shield belt and packs.</summary>
        public bool beltLayer;
        /// <summary>Worn on the head (the upper head or the full head).</summary>
        public bool onHead;
        /// <summary>A weapon (the game's own test: verbs or melee tools, not apparel).</summary>
        public bool weapon;
        /// <summary>A weapon that shoots or throws.</summary>
        public bool ranged;
        /// <summary>Held in the hands like a weapon (equipment of the primary kind) without being one: a tool.</summary>
        public bool tool;
        /// <summary>A comp hatches it: a fertilized egg.</summary>
        public bool hatches;
        /// <summary>Medicine (it has a medical potency).</summary>
        public bool medicine;
        /// <summary>A drug.</summary>
        public bool drug;
        /// <summary>Eaten for nutrition.</summary>
        public bool food;
        /// <summary>Food of a meal's preferability (an awful meal or better).</summary>
        public bool meal;
        /// <summary>Food of these kinds (the game's food type flags).</summary>
        public bool meat, plantFood, animalProduct;
        /// <summary>A material things are made of: leathery, fabric, or another kind (metal, wood, stone).</summary>
        public bool leathery, fabric, otherStuff;
    }

    /// <summary>
    /// The pure rules of PokéWorld's items on the shelves (the player's request, 20 November 2026: "there's a bunch of
    /// items from the Poke World mod, for example, the Dawn Stone ... that I cannot put into large storage shelves").
    /// PokéWorld lists its items under categories of its own, which a shelf that names the game's categories doesn't take.
    /// <see cref="KindOf"/> finds the game's own category each item belongs beside; a storage filter takes the PokéWorld
    /// items of a category where it takes the whole of that category (<see cref="TakesWhole"/>); a fridge only takes those
    /// that spoil and eggs (<see cref="FridgeTakes"/>). <see cref="PokeWorldShelves"/> applies them to the loaded defs and,
    /// once a save, to the shelves already built.
    /// </summary>
    public static class PokeWorldShelvesMath
    {
        /// <summary>The game's own categories an item can be placed beside, each with the one above it (the fallback when it's missing).</summary>
        public static readonly Dictionary<string, string> Parents = new Dictionary<string, string>
        {
            ["ApparelUtility"] = "Apparel",
            ["Headgear"] = "Apparel",
            ["ApparelMisc"] = "Apparel",
            ["Apparel"] = null,
            ["WeaponsRanged"] = "Weapons",
            ["WeaponsMelee"] = "Weapons",
            ["Weapons"] = null,
            ["EggsFertilized"] = "AnimalProductRaw",
            ["AnimalProductRaw"] = "FoodRaw",
            ["MeatRaw"] = "FoodRaw",
            ["PlantFoodRaw"] = "FoodRaw",
            ["FoodRaw"] = "Foods",
            ["FoodMeals"] = "Foods",
            ["Foods"] = null,
            ["Medicine"] = "Manufactured",
            ["Drugs"] = "Manufactured",
            ["Manufactured"] = null,
            ["Leathers"] = "Textiles",
            ["Textiles"] = "Manufactured",
            ["ResourcesRaw"] = null,
            ["ItemsMisc"] = "Items",
            ["Items"] = null
        };

        /// <summary>
        /// The game's own category an item belongs beside, by what it is: apparel with apparel (a belt with the utility
        /// pieces, a hat with headgear, the rest with the other apparel), a weapon with weapons (ranged or melee) and a tool
        /// held like one with weapons, a fertilized egg with fertilized eggs, medicine and drugs with theirs, food with food
        /// (meals, meat, plant food, animal products, other raw food), a leathery material with leathers, a fabric with
        /// textiles, another material with the raw resources, and everything else (stones, fossils, balls, discs,
        /// potions) with the game's miscellaneous items. The first that fits, in that order.
        /// </summary>
        public static string KindOf(ShelfItemFacts f)
        {
            if (f.apparel) return f.beltLayer ? "ApparelUtility" : f.onHead ? "Headgear" : "ApparelMisc";
            if (f.weapon) return f.ranged ? "WeaponsRanged" : "WeaponsMelee";
            if (f.tool) return "Weapons";
            if (f.hatches) return "EggsFertilized";
            if (f.medicine) return "Medicine";
            if (f.drug) return "Drugs";
            if (f.food)
            {
                if (f.meal) return "FoodMeals";
                if (f.meat) return "MeatRaw";
                if (f.plantFood) return "PlantFoodRaw";
                if (f.animalProduct) return "AnimalProductRaw";
                return "FoodRaw";
            }
            if (f.leathery) return "Leathers";
            if (f.fabric) return "Textiles";
            if (f.otherStuff) return "ResourcesRaw";
            return "ItemsMisc";
        }

        /// <summary>The category and the ones above it, nearest first: where to place an item when its own is missing.</summary>
        public static IEnumerable<string> WithParents(string kind)
        {
            for (int i = 0; kind != null && i < 8; i++)
            {
                yield return kind;
                kind = Parents.TryGetValue(kind, out var up) ? up : null;
            }
        }

        /// <summary>
        /// Whether a storage filter takes the whole of a category: its own list names the category or one above it and
        /// leaves none of them out (<paramref name="listed"/>, <paramref name="listedOut"/>: what the shelf's def says, so
        /// one thing set apart by name doesn't count against it), or, counted, it takes every one of the category's
        /// <paramref name="kinds"/> it could hold (<paramref name="taken"/>). A category with nothing in it is never taken whole by count.
        /// </summary>
        public static bool TakesWhole(bool listed, bool listedOut, int kinds, int taken) => (listed && !listedOut) || (kinds > 0 && taken >= kinds);

        /// <summary>A fridge takes only what spoils, and eggs (they keep in the cold).</summary>
        public static bool FridgeTakes(bool rots, string kind) => rots || kind == "EggsFertilized" || kind == "EggsUnfertilized";

        /// <summary>Whether one of PokéWorld's items of <paramref name="kind"/> goes on a storage building that takes the whole of that kind.</summary>
        public static bool Allow(bool takesWhole, bool fridge, bool rots, string kind) => takesWhole && (!fridge || FridgeTakes(rots, kind));

        /// <summary>A storage building that keeps things cold, by its class's or a comp's type name (RimFridge's fridges and the like).</summary>
        public static bool IsFridgeType(string typeName) =>
            !string.IsNullOrEmpty(typeName) && (Has(typeName, "Fridge") || Has(typeName, "Refrigerat") || Has(typeName, "Freezer"));

        /// <summary>A comp that hatches what it is on, by its type name (the game's CompHatcher, PokéWorld's egg hatcher and the like).</summary>
        public static bool IsHatcherType(string typeName) => !string.IsNullOrEmpty(typeName) && Has(typeName, "Hatcher");

        /// <summary>
        /// One of PokéWorld's items to place: an item that can be stored (it is in some category) and isn't already in one
        /// of the game's own (or another mod's): an unfinished thing PokéWorld itself lists with the game's is left alone.
        /// </summary>
        public static bool ToPlace(bool item, bool storable, bool unfinished, bool inOthersCategory) => item && storable && !unfinished && !inOthersCategory;

        /// <summary>A plain count of what was placed, for the log and the self-check: "ItemsMisc 31, EggsFertilized 379".</summary>
        public static string Summary(IEnumerable<string> kinds) =>
            string.Join(", ", (kinds ?? Enumerable.Empty<string>()).GroupBy(k => k).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => g.Key + " " + g.Count()));

        private static bool Has(string s, string part) => s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
