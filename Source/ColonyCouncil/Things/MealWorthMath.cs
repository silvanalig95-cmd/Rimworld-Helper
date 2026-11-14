using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's food counted as the meals its stores would make (Things/MealWorth.cs; DESIGN.md, "The days of food as
    /// the kitchen would make them"), always in nutrition, never in pieces. A recipe's gain is the nutrition of the
    /// enjoyable meals it makes (each meal's own, times how many) over the nutrition its ingredient groups take: a group
    /// counted by nutrition takes its count, a group counted by pieces its count times a piece's nutrition. The meals'
    /// nutrition is shared over the groups by what each puts in, so a piece of any ingredient makes the recipe's gain times
    /// its own nutrition (a simple meal of 0.9 from 0.5 of raw food: 1.8 times). A gain is never counted above three, the
    /// game's own best (nutrient paste makes 0.9 of 0.3). An ingredient counts by its best recipe; food eaten as it is counts
    /// at least its own nutrition; a meal in store counts its own; animal feed and what isn't food count nothing.
    /// </summary>
    public static class MealWorthMath
    {
        /// <summary>The most a recipe gives back for the nutrition put in.</summary>
        public const float MaxGain = 3f;

        /// <summary>One serving: a simple meal's nutrition, what a person sits down to.</summary>
        public const float ServingNutrition = 0.9f;

        /// <summary>
        /// The nutrition an ingredient group takes: its count times the nutrition in one of its counted units
        /// (<paramref name="nutritionPerUnit"/>: 1 for a group counted by nutrition, a piece's nutrition for one counted by pieces).
        /// </summary>
        public static float GroupNutrition(float count, float nutritionPerUnit) => Math.Max(0f, count) * Math.Max(0f, nutritionPerUnit);

        /// <summary>The nutrition of the meals a recipe makes: each meal's times how many.</summary>
        public static float Made(float mealNutrition, float mealsMade) => Math.Max(0f, mealNutrition) * Math.Max(0f, mealsMade);

        /// <summary>What a recipe gives back for the nutrition its groups take, at most <see cref="MaxGain"/>; 0 when it makes nothing or takes no nutrition.</summary>
        public static float Gain(float made, IEnumerable<float> groupNutrition)
        {
            float put = 0f;
            if (groupNutrition != null)
                foreach (float g in groupNutrition)
                    put += Math.Max(0f, g);
            if (made <= 0f || put <= 0.0001f) return 0f;
            return Math.Min(MaxGain, made / put);
        }

        /// <summary>The meal nutrition one piece of an ingredient makes in a recipe of this gain: the gain times the piece's own nutrition.</summary>
        public static float WorthPerPiece(float gain, float nutrition) => Math.Max(0f, gain) * Math.Max(0f, nutrition);

        /// <summary>An ingredient's best recipe: the largest gain of those that take it, 0 for none.</summary>
        public static float BestGain(IEnumerable<float> gains)
        {
            float best = 0f;
            if (gains != null)
                foreach (float g in gains)
                    best = Math.Max(best, Math.Min(MaxGain, g));
            return best;
        }

        /// <summary>What a thing in store is, for the count.</summary>
        public enum Stored
        {
            /// <summary>Counts nothing: no nutrition, a drug, a corpse, or animal feed (kibble, hay, anything only animals eat).</summary>
            Nothing,
            /// <summary>A meal a person eats as it is (paste and awful ones too): its own nutrition.</summary>
            Meal,
            /// <summary>Raw food, flour and every other ingredient: the meals its best recipe makes of it.</summary>
            Food
        }

        /// <summary>A thing in store, sorted for the count. Feed is kibble and the like; what only animals eat and no recipe takes comes out at nothing on its own.</summary>
        public static Stored KindOf(bool nutritionGiving, bool drugOrCorpse, bool feed, bool meal) =>
            !nutritionGiving || drugOrCorpse || feed ? Stored.Nothing : meal ? Stored.Meal : Stored.Food;

        /// <summary>
        /// What one piece in store counts for: a meal its own nutrition; other food the meals its best recipe makes of it,
        /// or its own nutrition when a person eats it as it is and that is more (no stove: raw food at its raw worth); nothing else.
        /// </summary>
        public static float PerPiece(Stored kind, float nutrition, float bestGain, bool eatenAsIs)
        {
            switch (kind)
            {
                case Stored.Meal:
                    return Math.Max(0f, nutrition);
                case Stored.Food:
                    return Math.Max(WorthPerPiece(bestGain, nutrition), eatenAsIs ? Math.Max(0f, nutrition) : 0f);
                default:
                    return 0f;
            }
        }

        /// <summary>The stores' total: each kind's pieces times what one counts for.</summary>
        public static float Total(IEnumerable<(int pieces, float perPiece)> stores)
        {
            float sum = 0f;
            if (stores != null)
                foreach (var s in stores)
                    if (s.pieces > 0 && s.perPiece > 0f)
                        sum += s.pieces * s.perPiece;
            return sum;
        }

        /// <summary>How many servings a meal's nutrition makes: whole simple meals' worth, so a stack of pemmican is a few meals, not one a piece.</summary>
        public static int Servings(float nutrition) => nutrition <= 0f ? 0 : (int)Math.Floor(nutrition / ServingNutrition + 0.001f);
    }
}
