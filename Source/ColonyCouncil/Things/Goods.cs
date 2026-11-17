using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Valuable goods and plain goods in the live game (the rule is <see cref="GoodsMath"/>): each def read once, by what it
    /// is (its categories, its stuff, its food and drug, its value, what Things/ThingRoles.cs knows of its comps and trade
    /// tags), so the game's goods and every mod's sort themselves the same way.
    /// </summary>
    public static class Goods
    {
        private static readonly Dictionary<ThingDef, GoodsKind> kinds = new Dictionary<ThingDef, GoodsKind>();

        private static ThingCategoryDef plantMatter, blocks, raw, textiles, medicine;
        private static bool looked;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            plantMatter = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("PlantMatter");
            blocks = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("StoneBlocks");
            raw = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("ResourcesRaw");
            textiles = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("Textiles");
            medicine = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("Medicine");
        }

        private static bool Within(ThingDef d, ThingCategoryDef c) => c != null && d.thingCategories != null && d.IsWithinCategory(c);

        private static bool Has(List<StuffCategoryDef> cats, StuffCategoryDef c) => cats != null && c != null && cats.Contains(c);

        /// <summary>What the rule reads of a def.</summary>
        public static GoodsFacts Facts(ThingDef d)
        {
            if (d == null) return null;
            Look();
            var t = ThingRoles.Facts(d);
            var cats = d.stuffProps?.categories;
            return new GoodsFacts
            {
                defName = d.defName,
                value = d.BaseMarketValue,
                tech = t?.tech ?? 0,
                money = d == ThingDefOf.Silver,
                medicine = d.IsMedicine || Within(d, medicine),
                drug = d.IsDrug,
                special = t != null && (t.special || t.exotic),
                humanlike = t?.humanlike ?? false,
                nutrition = t?.nutrition ?? 0f,
                preferability = t?.preferability ?? 0,
                plantMatter = Within(d, plantMatter),
                blocks = Within(d, blocks),
                raw = Within(d, raw),
                textile = Within(d, textiles),
                woody = Has(cats, StuffCategoryDefOf.Woody),
                stony = Has(cats, StuffCategoryDefOf.Stony),
                metallic = Has(cats, StuffCategoryDefOf.Metallic),
                fabric = Has(cats, StuffCategoryDefOf.Fabric),
                leathery = Has(cats, StuffCategoryDefOf.Leathery)
            };
        }

        /// <summary>What a good is (worked out once a def: the def database doesn't change in play).</summary>
        public static GoodsKind KindOf(ThingDef d)
        {
            if (d == null) return GoodsKind.Unfit;
            if (kinds.TryGetValue(d, out var k)) return k;
            try
            {
                k = GoodsMath.Kind(Facts(d));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("goods: what a good is", e);
                k = GoodsKind.Valuable;
            }
            kinds[d] = k;
            return k;
        }

        /// <summary>A crafting, building or precious input (components, plasteel, medicine, gold, chemfuel, drugs, silver): never taken without the player's choice.</summary>
        public static bool Valuable(ThingDef d) => KindOf(d) == GoodsKind.Valuable;

        /// <summary>Wood, cloth, leather, food, stone blocks or a common metal: what the colony can spare.</summary>
        public static bool Plain(ThingDef d) => GoodsMath.Plain(KindOf(d));
    }
}
