using System;

namespace ColonyCouncil
{
    /// <summary>The levels of build priority and the one rule they follow (BuildPriorities, Building/BuildPriority.cs).</summary>
    public static class BuildPriorityMath
    {
        public const int Low = -1, Normal = 0, High = 1, Urgent = 2;

        /// <summary>The levels from the top, as the menu lists them.</summary>
        public static readonly int[] Levels = { Urgent, High, Normal, Low };

        /// <summary>What a click on the button sets: normal, high, urgent, low, and round again.</summary>
        public static int Next(int level) => level >= Urgent ? Low : level + 1;

        public static int Clamp(int level) => Math.Max(Low, Math.Min(Urgent, level));

        /// <summary>
        /// Whether work at this level waits: something at a higher level can be built now (<paramref name="top"/>, the
        /// highest level with work that can go ahead; int.MinValue for none).
        /// </summary>
        public static bool Waits(int level, int top) => level < top;
    }
}
