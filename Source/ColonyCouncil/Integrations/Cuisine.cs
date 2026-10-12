using System;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Vanilla Cooking and Brewing Expanded, read by name. Their meals keep the game's meal grades, so stock checks
    /// already count them; what needs care is their own taste memories, their spoiled grills and their soft drinks.
    /// </summary>
    public static class Cuisine
    {
        /// <summary>Memories of a fine meal: the game's, and Vanilla Cooking Expanded's grills, which have their own.</summary>
        public static readonly string[] FineMealThoughts = { "AteFineMeal", "VCE_AteFineGrill" };

        /// <summary>Memories of a lavish meal: the game's, and Vanilla Cooking Expanded's lavish and gourmet dishes.</summary>
        public static readonly string[] LavishMealThoughts = { "AteLavishMeal", "VCE_AteLavishGrill", "VCE_AteGourmetGrill", "VCE_AteGourmetMeal" };

        /// <summary>
        /// A drink even a teetotaler may have: Vanilla Brewing Expanded's tea, coffee and soda are social drugs in the
        /// game's terms, but not the drink a brewer or a colonist with a taste for chemicals is asking for.
        /// </summary>
        public static bool IsSoftDrink(ThingDef d)
        {
            var drug = d?.GetCompProperties<CompProperties_Drug>();
            return drug != null && drug.teetotalerCanConsume;
        }

        /// <summary>
        /// A spoiled meal that keeps its grade: Vanilla Cooking Expanded's grills turn "ruined" when kept cold, and stay
        /// fine or lavish on paper.
        /// </summary>
        public static bool IsSpoiledMeal(ThingDef d) => d?.defName != null && d.defName.IndexOf("Ruined", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
