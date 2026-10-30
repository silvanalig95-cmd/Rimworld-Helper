using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// Production chains followed to the end (Building/Chains.cs) as plain rules: how many batches a bench turns out in
    /// a day, how long a stock lasts at its net use, what a parent's want asks of each part, a node's bill count, how
    /// long a batch takes, which recipe and bench to prefer. Pure, so Tools/Tests can check them.
    /// </summary>
    public static class ChainMath
    {
        /// <summary>A chain is followed this many links down at most.</summary>
        public const int MaxDepth = 4;
        /// <summary>Hours a worker spends at a bench in a day, for the make rate.</summary>
        public const float WorkHoursADay = 8f;
        /// <summary>A node's bill count covers this many days of its use beyond the want.</summary>
        public const float BufferDays = 3f;
        /// <summary>Nutrition a person eats in a day.</summary>
        public const float NutritionADay = 1.6f;
        /// <summary>A stock that is never run down: its make keeps up with its use.</summary>
        public const float Far = 999f;
        public const float TicksPerHour = 2500f;

        /// <summary>Batches a bench turns out in a day at this work speed (1 is plain), by the recipe's work in ticks.</summary>
        public static float BatchesADay(float workTicks, float speed, float hours = WorkHoursADay) =>
            workTicks <= 0f || speed <= 0f ? 0f : hours * TicksPerHour * speed / workTicks;

        /// <summary>Hours one batch takes at this speed.</summary>
        public static float HoursABatch(float workTicks, float speed) => workTicks <= 0f ? 0f : speed <= 0f ? Far : workTicks / (TicksPerHour * speed);

        /// <summary>Days the stock lasts at its net use (use less make); <see cref="Far"/> when the make keeps up.</summary>
        public static float DaysLeft(int stock, float useADay, float makeADay)
        {
            float net = useADay - makeADay;
            if (net <= 0.0001f) return Far;
            return Math.Max(0, stock) / net;
        }

        /// <summary>What a parent's want asks of one part beyond the stock: the batches the want takes, times the part per batch.</summary>
        public static int Need(int parentWant, float parentPerBatch, float partPerBatch, int stock)
        {
            if (parentWant <= 0 || parentPerBatch <= 0f || partPerBatch <= 0f) return 0;
            int batches = (int)Math.Ceiling(parentWant / parentPerBatch);
            return Math.Max(0, (int)Math.Ceiling(batches * partPerBatch) - Math.Max(0, stock));
        }

        /// <summary>A node's bill count: the want plus <see cref="BufferDays"/> of its use, on the stock, never under the usual (MakingMath.DemandTarget).</summary>
        public static int Target(int stock, int shortBy, float useADay, int usual) =>
            MakingMath.DemandTarget(stock, shortBy + (int)Math.Ceiling(Math.Max(0f, useADay) * BufferDays), usual);

        /// <summary>Meals eaten in a day by so many people, by the meal's nutrition.</summary>
        public static float MealsADay(int people, float nutritionEach) => people <= 0 || nutritionEach <= 0f ? 0f : people * NutritionADay / nutritionEach;

        /// <summary>A recipe's worth for choosing: product per hour of work.</summary>
        public static float Efficiency(float perBatch, float workTicks) => workTicks <= 0f ? 0f : perBatch * TicksPerHour / workTicks;

        /// <summary>A bench's worth for choosing: the best able worker's skill, less a point for every twenty cells from the materials' store.</summary>
        public static float BenchScore(int skill, float distance) => skill - Math.Max(0f, distance) / 20f;

        public static bool TooDeep(int depth) => depth >= MaxDepth;

        /// <summary>The chain's lead time: the hours along its longest path, as days.</summary>
        public static float LeadDays(IEnumerable<float> hoursAlongPath) => hoursAlongPath == null ? 0f : hoursAlongPath.Sum() / 24f;

        /// <summary>A plant's yield in a day from one cell: the harvest over the days it takes to grow, at the land's and the growers' share of it.</summary>
        public static float YieldADay(float harvestYield, float growDays, float share = 0.6f) => growDays <= 0f ? 0f : harvestYield * Math.Max(0f, share) / growDays;

        /// <summary>Cells to sow for a want of a plant's harvest, at least four, at most sixty.</summary>
        public static int CellsToSow(int want, float harvestYield) => harvestYield <= 0f ? 0 : Math.Max(4, Math.Min(60, (int)Math.Ceiling(want / harvestYield)));
    }
}
