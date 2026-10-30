using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The council's automations (Building/Automations.cs) as plain rules: how often each may act, how long a bill the
    /// player removed stays unset, how many bills an hour, how many trees to mark for firewood. Pure, so Tools/Tests can check them.
    /// </summary>
    public static class AutomationMath
    {
        public const int TicksPerHour = 2500, TicksPerDay = 60000;
        /// <summary>A bill the player removed isn't set again for this long.</summary>
        public const int DroppedDays = 15;
        /// <summary>At most this many new bills an hour: the player can see each one go up.</summary>
        public const int BillsAnHour = 2;
        /// <summary>A proposed step at this level or above (3: high) goes straight into the plan.</summary>
        public const int PlanLevel = 3;
        /// <summary>How long the council waits before doing the same thing again: a stockpile, a take-down, lamps, firewood, a research pick.</summary>
        public const int StockpileDays = 5, TakeDownDays = 2, LampsDays = 1, FirewoodDays = 1, ResearchDays = 1;
        /// <summary>The log kept for the check-myself page, and the actions kept to undo.</summary>
        public const int LogLines = 20, ActionsKept = 12;
        /// <summary>A thing a plant yields is sown for once every so many days.</summary>
        public const int GrowDays = 10;
        /// <summary>The usual day's budget of materials (market worth) for the blueprints the automations place, and the usual daily caps.</summary>
        public const float BudgetDefault = 200f;
        public const int TakeDownsADayDefault = 8, ChopsADayDefault = 10;

        /// <summary>How many more may be done today under a cap, of the number wanted.</summary>
        public static int Allowed(int doneToday, int cap, int wanted) => Math.Max(0, Math.Min(wanted, cap - Math.Max(0, doneToday)));

        /// <summary>The day's budget still has something in it.</summary>
        public static bool BudgetLeft(float spentToday, float budget) => spentToday < budget;

        /// <summary>Whether something last done at <paramref name="lastTick"/> (negative: never) may be done again.</summary>
        public static bool Due(int lastTick, int now, int everyTicks) => lastTick < 0 || now - lastTick >= everyTicks;

        /// <summary>A bill the player removed at <paramref name="droppedTick"/> is still left alone.</summary>
        public static bool StillDropped(int droppedTick, int now) => now - droppedTick < DroppedDays * TicksPerDay;

        /// <summary>How many trees to mark for chopping when firewood is short: by how loud the advice is (0 idea to 4 critical).</summary>
        public static int TreesToMark(int level) => level >= 4 ? 12 : level >= 3 ? 10 : level >= 2 ? 7 : 5;

        /// <summary>Only a staple is set on its own; anything else only when the building or a bill wants it.</summary>
        public static bool AutoKind(string kind, bool wanted) => wanted || kind == "meals" || kind == "medicine" || kind == "components";
    }
}
