using System;

namespace ColonyCouncil
{
    /// <summary>
    /// What a good is to the colony when someone else would take it: a plain good it can spare (wood, cloth, leather, food,
    /// stone blocks, a common metal), a valuable one it keeps unless the player chooses otherwise, or one never handed over
    /// (of a person).
    /// </summary>
    public enum GoodsKind
    {
        /// <summary>A crafting, building or precious input: components, plasteel, medicine, gold, jade, chemfuel, drugs, silver.</summary>
        Valuable,
        Wood,
        /// <summary>Cloth and wool.</summary>
        Cloth,
        Leather,
        /// <summary>Food eaten for its nourishment: raw food, meals, kibble, hay.</summary>
        Food,
        /// <summary>Stone blocks and other building stone.</summary>
        Blocks,
        /// <summary>A common metal: steel, bronze.</summary>
        Metal,
        /// <summary>Of a person (human leather, human meat): never in a cart or a thief's pack.</summary>
        Unfit
    }

    /// <summary>What <see cref="GoodsMath"/> reads of a def (Things/Goods.cs fills it from the live game, Tools/Tests from the player's catalogue).</summary>
    public sealed class GoodsFacts
    {
        public string defName;
        /// <summary>Its market value, one of it.</summary>
        public float value;
        /// <summary>Its tech level, in ThingRolesMath.Techs's order.</summary>
        public int tech;
        /// <summary>Silver: money, not a good.</summary>
        public bool money;
        /// <summary>Medicine: a medical potency, or within the game's medicine category.</summary>
        public bool medicine;
        /// <summary>A drug of any kind (beer and tea too).</summary>
        public bool drug;
        /// <summary>Something to use rather than have, or marked by a trade tag as special (ThingFacts.special and exotic: a hatching egg, a mod's arcane good).</summary>
        public bool special;
        /// <summary>Of a person.</summary>
        public bool humanlike;
        public float nutrition;
        /// <summary>Its food preferability, in ThingRolesMath.Preferabilities's order.</summary>
        public int preferability;
        /// <summary>Within the game's categories: plant matter (hops, smokeleaf, psychoid leaves, dye, a mod's flowers), stone blocks, raw resources, textiles.</summary>
        public bool plantMatter, blocks, raw, textile;
        /// <summary>As a material (its stuff categories).</summary>
        public bool woody, stony, metallic, fabric, leathery;
    }

    /// <summary>
    /// Valuable goods and plain goods, one rule for everything that hands the colony's goods to someone else or takes them
    /// (the player, 17 November 2026: "it just shouldn't auto take valuable materials ... wood and cloth and leather are
    /// more, I guess, okay"; and of the thefts: "they shouldn't steal components again and other valuable things"): a trade
    /// pact's cart and a supply contract's load (Diplomacy/Pacts.cs) take a valuable good only when the player picks it, and
    /// a thief of a scheme (Diplomacy/Schemes.cs) never does. By what a def is, never by a mod's name, so a mod's goods sort
    /// themselves. Pure: Tools/Tests checks it on clear cases and on the player's own catalogue.
    /// </summary>
    public static class GoodsMath
    {
        /// <summary>
        /// A material worth this much a unit or more is precious (about twice steel): gold, plasteel, uranium, jade, devilstrand,
        /// synthread, a thrumbo's fur. Food is food whatever it is worth.
        /// </summary>
        public const float PreciousValue = 4f;

        /// <summary>Spacer goods and later (hyperweave, a mod's spacer fabric) are valuable whatever they are.</summary>
        public const int SpacerTech = 5;

        /// <summary>The preferability "NeverForNutrition": a condiment or an ingredient, not eaten for itself.</summary>
        public const int NeverForNutrition = 1;

        /// <summary>
        /// What a good is: of a person, unfit; money, medicine, a drug or something special, valuable; spacer goods,
        /// valuable; food eaten for itself, food; a material worth <see cref="PreciousValue"/> or more, valuable; then wood
        /// (logs), cloth, leather, stone (blocks, building stone) and a common metal; anything else (components, chemfuel,
        /// neutroamine, shells, a mod's parts and fuels, plant matter for brewing or dyeing) is a crafting input and valuable.
        /// </summary>
        public static GoodsKind Kind(GoodsFacts f)
        {
            if (f == null || f.humanlike) return GoodsKind.Unfit;
            if (f.money || f.medicine || f.drug || f.special) return GoodsKind.Valuable;
            if (f.tech >= SpacerTech) return GoodsKind.Valuable;
            if (Food(f)) return GoodsKind.Food;
            if (f.value >= PreciousValue) return GoodsKind.Valuable;
            if (f.woody && f.raw) return GoodsKind.Wood;
            if (f.fabric || f.textile && !f.leathery) return GoodsKind.Cloth;
            if (f.leathery) return GoodsKind.Leather;
            if (f.stony && (f.blocks || f.raw)) return GoodsKind.Blocks;
            if (f.metallic && f.raw) return GoodsKind.Metal;
            return GoodsKind.Valuable;
        }

        /// <summary>Eaten for its nourishment: some nutrition, not a condiment or an ingredient, not plant matter grown for brewing or dye.</summary>
        public static bool Food(GoodsFacts f) => f != null && f.nutrition > 0f && f.preferability != NeverForNutrition && !f.plantMatter;

        public static bool Plain(GoodsKind k) => k != GoodsKind.Valuable && k != GoodsKind.Unfit;

        public static bool Valuable(GoodsFacts f) => Kind(f) == GoodsKind.Valuable;

        // ================= A cart of plain goods =================

        /// <summary>
        /// The plain goods a people's age uses, first to last (DealingMath.RequestGoods asks the tribal and medieval for wood,
        /// cloth and medicine, later ages for steel): a tribe's wood, leather and food; a medieval people's wood, cloth and
        /// leather; an industrial or spacer people's steel and cloth. Every plain kind is in each list.
        /// </summary>
        public static GoodsKind[] CartOrder(WorldAge age)
        {
            switch (age)
            {
                case WorldAge.Tribal: return new[] { GoodsKind.Wood, GoodsKind.Leather, GoodsKind.Food, GoodsKind.Cloth, GoodsKind.Blocks, GoodsKind.Metal };
                case WorldAge.Medieval: return new[] { GoodsKind.Wood, GoodsKind.Cloth, GoodsKind.Leather, GoodsKind.Food, GoodsKind.Blocks, GoodsKind.Metal };
                default: return new[] { GoodsKind.Metal, GoodsKind.Cloth, GoodsKind.Food, GoodsKind.Wood, GoodsKind.Leather, GoodsKind.Blocks };
            }
        }

        /// <summary>Where a kind stands in the age's order (0 first); a valuable or unfit good after every plain one.</summary>
        public static int Rank(GoodsKind k, WorldAge age)
        {
            int i = Array.IndexOf(CartOrder(age), k);
            return i < 0 ? 99 : i;
        }

        /// <summary>A colonist eats about this much nutrition a day (the game's 1.6 for a grown person, rounded).</summary>
        public const float DailyNutrition = 1.6f;

        /// <summary>The colony keeps this many days of each food for everyone before any of it goes in a cart.</summary>
        public const float FoodDays = 5f;

        /// <summary>
        /// How many of a plain good the colony keeps before any goes in a cart: of a food, five days of it for everyone; of
        /// wood 150 and 20 a colonist (fuel and building); cloth or leather 50 and 15 a colonist (clothes); stone blocks 100
        /// and 30 a colonist; a common metal 200 and 30 a colonist. A valuable good chosen on purpose keeps none (a third of
        /// the stores at most goes, as the cart has always counted).
        /// </summary>
        public static int Reserve(GoodsKind k, int colonists, float nutrition = 0f)
        {
            int c = Math.Max(1, colonists);
            switch (k)
            {
                case GoodsKind.Food: return nutrition > 0f ? (int)Math.Ceiling(c * DailyNutrition * FoodDays / nutrition - 0.01f) : 0;
                case GoodsKind.Wood: return 150 + 20 * c;
                case GoodsKind.Cloth:
                case GoodsKind.Leather:
                    return 50 + 15 * c;
                case GoodsKind.Blocks: return 100 + 30 * c;
                case GoodsKind.Metal: return 200 + 30 * c;
                default: return 0;
            }
        }
    }
}
