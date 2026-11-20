using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// Each god's festival kept as its words say (docs/content-update-proposals.md, C1): the eight festivals that kept only
    /// the shared feast, their hours, how many take part and what each does on the map (Gods/OnMap/OwnFeasts.cs). No game
    /// types, so Tools/Tests reads the same numbers the game uses. Nobody is hurt by any of it; Selene's watch on a cold night
    /// is the night watch's own (Harm/ColdHurts.cs, the shared budget), as every watch on the walls is.
    /// </summary>
    public static class OwnFeastsMath
    {
        /// <summary>The eight gods whose festival plays out here (the other six play out in their own files: Ennu, Cirrus, Helios, Murmur, Ducat, Revel).</summary>
        public static readonly string[] Gods = { "Hollin", "Smoulder", "Bellwether", "Morel", "Pip", "Hush", "Undertow", "Selene" };

        /// <summary>The festival plays out here.</summary>
        public static bool Keeps(string god) => god != null && Array.IndexOf(Gods, god) >= 0;

        /// <summary>The hour each festival's custom begins (its words' time of day): a morning's work, a supper, dusk, the night.</summary>
        public static int StartHour(string god)
        {
            switch (god)
            {
                case "Hollin": return 8;
                case "Smoulder": return 10;
                case "Bellwether": return 16;
                case "Morel": return 9;
                case "Pip": return 9;
                case "Hush": return 18;
                case "Undertow": return 18;
                case "Selene": return 20;
                default: return 12;
            }
        }

        /// <summary>
        /// The last hour its custom may still begin that day: the day's work needs the daylight after it (the morning ones
        /// till mid-afternoon), the evening ones begin by the late evening. A festival kept later than this (its letter
        /// answered late) is held the next day at its hour.
        /// </summary>
        public static int LatestHour(string god)
        {
            switch (god)
            {
                case "Hollin": return 15;
                case "Smoulder": return 17;
                case "Bellwether": return 19;
                case "Morel": return 15;
                case "Pip": return 15;
                case "Hush": return 21;
                case "Undertow": return 21;
                case "Selene": return 23;
                default: return 18;
            }
        }

        /// <summary>The custom may begin now: from its hour to its latest.</summary>
        public static bool BeginNow(string god, int hour) => hour >= StartHour(god) && hour <= LatestHour(god);

        /// <summary>A festival kept on <paramref name="keptDay"/> waits for its hour that day or the next; after that it is let be.</summary>
        public static bool StillWaiting(int keptDay, int today) => today >= keptDay && today <= keptDay + 1;

        // ================= Hollin: the Greening =================

        /// <summary>At most this many saplings set in one Greening, and this many planters.</summary>
        public const int SaplingsMost = 20, PlantersMost = 4;

        /// <summary>A sapling for each tree the colony felled this year, <see cref="SaplingsMost"/> at most.</summary>
        public static int Saplings(int felled) => Math.Max(0, Math.Min(SaplingsMost, felled));

        /// <summary>The saplings shared among the planters, the first taking any left over.</summary>
        public static List<int> Share(int saplings, int planters)
        {
            var list = new List<int>();
            if (planters <= 0 || saplings <= 0) return list;
            int each = saplings / planters, extra = saplings % planters;
            for (int i = 0; i < planters; i++) list.Add(each + (i < extra ? 1 : 0));
            return list.Where(n => n > 0).ToList();
        }

        /// <summary>The planting ends this many hours after it began, done or not; the meal under the oldest tree is at noon.</summary>
        public const int PlantingHours = 4, MealHour = 12;

        /// <summary>The year's count of trees felled: a new year starts it again.</summary>
        public static int FelledThisYear(int keptYear, int year, int felled) => keptYear == year ? Math.Max(0, felled) : 0;

        // ================= Smoulder: the Deep Silver =================

        /// <summary>The silver dropped: a twentieth of what the stores hold, 5 to 40; none with fewer than 5.</summary>
        public static int Silver(int inStores) => inStores < 5 ? 0 : Math.Max(5, Math.Min(40, inStores / 20));

        // ================= Bellwether: the Letting Pass =================

        /// <summary>At most this many of the herd walked out, and the story told at this hour.</summary>
        public const int HerdMost = 12, StoryHour = 18;

        /// <summary>The long grass is looked for this far out from the colony's middle.</summary>
        public const float EdgeFrom = 18f, EdgeTo = 40f;

        // ================= Morel: the Turning Under =================

        /// <summary>At most this much hay or this many raw vegetables carried out, and this many squares of the field turned rich.</summary>
        public const int HayMost = 20, ScrapsMost = 10, RichCells = 9;

        /// <summary>The stew: one pot of fungus meals for the feast, a meal for each this many units of fungus, at most this many meals.</summary>
        public const int FungusPerMeal = 10, StewMost = 6;

        /// <summary>The meals the fungus in the stores makes, <see cref="StewMost"/> at most.</summary>
        public static int StewMeals(int fungus) => Math.Max(0, Math.Min(StewMost, fungus / FungusPerMeal));

        // ================= Pip: the First Spark =================

        /// <summary>At most this many machines walked round; the lights lit at dusk and put out at dawn.</summary>
        public const int MachinesMost = 6, DuskHour = 18, DawnHour = 6;

        // ================= Lights and doors (Pip, Undertow) =================

        /// <summary>At most this many lamps lit and doors shut on one round.</summary>
        public const int LampsMost = 12, DoorsMost = 8;

        /// <summary>Lit or shut by the festival, put back at dawn (a lamp switched off again, a door held open again) only if nobody changed it since.</summary>
        public static bool PutBack(bool changedByFeast, bool stillAsLeft) => changedByFeast && stillAsLeft;

        // ================= Hush: the Stillness =================

        /// <summary>At most this many at the silent supper; the old from this age.</summary>
        public const int SupperMost = 12, OldFrom = 60;

        /// <summary>
        /// Who sits nearest the fire: the sick and the old first (the sick before the old, the oldest of each first), then
        /// everyone else; the order of seats from the fire outward. Each is (sick, age, id); returns the ids in seating order.
        /// </summary>
        public static List<int> SeatOrder(IList<(bool sick, int age, int id)> people)
        {
            return people.OrderByDescending(p => p.sick ? 2 : p.age >= OldFrom ? 1 : 0).ThenByDescending(p => p.age).ThenBy(p => p.id).Select(p => p.id).ToList();
        }

        // ================= Selene: the Longest Night =================

        /// <summary>At most this many on the watch, and this many dreams told at breakfast.</summary>
        public const int WatchersMost = 2, DreamsMost = 3;

        /// <summary>Breakfast's hours, for the dreams told.</summary>
        public const int BreakfastFrom = 6, BreakfastTo = 11;

        public static bool Breakfast(int hour) => hour >= BreakfastFrom && hour < BreakfastTo;

        // ================= The rite =================

        /// <summary>At most this many believers stand behind the priest at the festival's rite.</summary>
        public const int GatheringMost = 6;

        /// <summary>
        /// The places behind the one leading the rite, in rows, for <paramref name="n"/> people: offsets (across, back) from the
        /// leader, facing the shrine. Rows of three, one cell apart, starting two behind.
        /// </summary>
        public static List<(int across, int back)> Behind(int n)
        {
            var list = new List<(int, int)>();
            int[] across = { 0, -1, 1 };
            for (int i = 0; i < n; i++) list.Add((across[i % 3], 2 + i / 3));
            return list;
        }
    }
}
