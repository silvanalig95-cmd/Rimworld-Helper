using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// A brewery at home in the kitchen (Building/BreweryKitchen.cs; the player, 18 November 2026: "i think the brewery
    /// should also be able to be placed in the kitchen"). The pure rules, linked in the tests: which workshop benches are
    /// cooking benches, and a room's workshop score with their share taken out where the room is a kitchen.
    /// </summary>
    public static class BreweryKitchenMath
    {
        /// <summary>
        /// A workshop bench whose work is cooking, as the brewery's (brewing wort is Cooking work, done by the cooks): it
        /// has recipes, every recipe that names a skill names Cooking, and at least one does. <paramref name="recipeSkills"/>
        /// holds each recipe's skill (null for none).
        /// </summary>
        public static bool CookingBench(IEnumerable<string> recipeSkills)
        {
            if (recipeSkills == null) return false;
            bool cooking = false;
            foreach (var s in recipeSkills)
            {
                if (s == null) continue;
                if (s != "Cooking") return false;
                cooking = true;
            }
            return cooking;
        }

        /// <summary>
        /// A room's workshop score with its cooking benches left out, where the room has a kitchen bench too (a stove), so
        /// a brewery beside the stoves counts toward the kitchen and never turns it into a workshop. The game's own score
        /// is so much a bench; the share of the <paramref name="cookingBenches"/> among the <paramref name="workshopBenches"/>
        /// comes off. Unchanged without a kitchen bench or a cooking bench.
        /// </summary>
        public static float WorkshopScore(float score, int workshopBenches, int cookingBenches, bool kitchenBench)
        {
            if (!kitchenBench || score <= 0f || workshopBenches <= 0 || cookingBenches <= 0) return score;
            float per = score / workshopBenches;
            return Math.Max(0f, score - per * Math.Min(cookingBenches, workshopBenches));
        }
    }
}
