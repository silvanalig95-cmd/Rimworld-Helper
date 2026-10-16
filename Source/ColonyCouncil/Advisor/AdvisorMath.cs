using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The thresholds behind the advisor's newer checks (AdvisorSignals5.cs and AdvisorSignals_Rooms2.cs), with no game
    /// types, so the tests run them as the game does. Each answers one question: is this worth the player's attention yet?
    /// The checks themselves read the game and pass the numbers in.
    /// </summary>
    public static class AdvisorMath
    {
        public const int TicksPerDay = 60000;

        // ---------------- Missions ----------------

        /// <summary>
        /// A mission whose goods aren't all there yet is pressing in its last two days, or in its last quarter when that
        /// is under four days. Met goals, or no deadline at all, are never pressing.
        /// </summary>
        public static bool MissionPressing(int ticksLeft, int totalTicks, bool goalsMet)
        {
            if (goalsMet || ticksLeft <= 0) return false;
            if (ticksLeft <= 2 * TicksPerDay) return true;
            return totalTicks > 0 && ticksLeft * 4 <= totalTicks && ticksLeft <= 4 * TicksPerDay;
        }

        // ---------------- Decrees ----------------

        /// <summary>
        /// A decree costs more than it gives when what it was enacted for has passed (<paramref name="reasonHolds"/> false:
        /// the food is no longer low, the threat is days gone) while the colony's moods about it still add up to a loss of
        /// three or more, with more people against it than for it. A decree is meant to cost some mood while it does its
        /// work; only one that no longer does is worth a word. <paramref name="good"/> is the sum of the good moods,
        /// <paramref name="bad"/> of the bad ones (a negative number).
        /// </summary>
        public static bool DecreeCostsMore(float good, float bad, int against, int forIt, bool reasonHolds) =>
            !reasonHolds && good + bad <= -3f && against > forIt;

        // ---------------- People ----------------

        /// <summary>One doctor for three people or more: nobody to tend the doctor, or the second patient.</summary>
        public static bool NeedsBackupDoctor(int doctors, int colonists) => doctors == 1 && colonists >= 3;

        // ---------------- Winter ----------------

        /// <summary>Wood the colony's fires burn over the days ahead (at most two months are counted).</summary>
        public static int FirewoodNeeded(float perDay, float days) => perDay <= 0f || days <= 0f ? 0 : (int)Math.Ceiling(perDay * Math.Min(days, 60f));

        /// <summary>Short of wood: less than two thirds of what the fires will burn.</summary>
        public static bool FirewoodShort(int have, int needed) => needed >= 20 && have * 3 < needed * 2;

        /// <summary>
        /// Food that won't see the winter out: the stores hold at least five days of food that spoils, but less than half
        /// the winter's worth (at most a month counted) of food that keeps. Days are days of food for the colony.
        /// </summary>
        public static bool StoresWontKeep(float keepsDays, float spoilsDays, float winterDays)
        {
            if (winterDays < 10f || spoilsDays < 5f) return false;
            return keepsDays < Math.Min(winterDays, 30f) * 0.5f;
        }

        // ---------------- The calendar ----------------

        /// <summary>
        /// What a calendar day kept this way still lacks, or null: a feast needs a meal in store for everyone, a hunt
        /// someone who hunts, a bonfire a fire or wood to lay one. The other ways need nothing laid by.
        /// </summary>
        public static string CalendarLacks(string way, int meals, int people, int hunters, bool fire, int wood)
        {
            switch (way)
            {
                case "Feast": return meals < people ? "Feast" : null;
                case "Hunt": return hunters <= 0 ? "Hunt" : null;
                case "Bonfire": return !fire && wood < 20 ? "Bonfire" : null;
                default: return null;
            }
        }

        // ---------------- Rooms ----------------

        /// <summary>Cooking stations the colony wants for its size: one up to seven people, two up to fifteen, three beyond.</summary>
        public static int StovesFor(int people) => people <= 7 ? 1 : people <= 15 ? 2 : 3;

        /// <summary>
        /// The kitchen has outgrown its stove when the colony wants more stations than it has, it has the cooks to use
        /// another, and the meals run short (fewer in store than people).
        /// </summary>
        public static bool KitchenOutgrown(int people, int stoves, int cooks, int meals) =>
            stoves > 0 && stoves < StovesFor(people) && cooks > stoves && meals < people;

        /// <summary>The freezer is full (most of its storage taken) and food that spoils is left out in the warm.</summary>
        public static bool FreezerTooSmall(int storageCells, int usedCells, int spoilingOutside) =>
            storageCells > 0 && usedCells * 20 >= storageCells * 17 && spoilingOutside >= 30;

        /// <summary>
        /// A dining room too small for the colony: at least five people, and its open space rather tight (under 29, the
        /// game's own threshold) or under two and a half for each of them.
        /// </summary>
        public static bool DiningCramped(float space, int people) => people >= 5 && (space < 29f || space < people * 2.5f);

        /// <summary>What a prison cell lacks for its prisoners' mood: a light, a table, a seat, a cell that isn't awful.</summary>
        public static int PrisonLacks(bool light, bool table, bool seat, float impressiveness)
        {
            int n = 0;
            if (!light) n++;
            if (!table) n++;
            if (!seat) n++;
            if (impressiveness < 20f) n++;
            return n;
        }

        /// <summary>Worth a word when two or more things are missing.</summary>
        public static bool PrisonUncomfortable(int lacks) => lacks >= 2;

        /// <summary>
        /// A workshop that would work better: some bench with no light reaching its work spot, or half its benches or more
        /// with no chair at the work spot.
        /// </summary>
        public static bool WorkshopWanting(int benches, int unlit, int noChair) => benches > 0 && (unlit > 0 || noChair * 2 >= benches && noChair > 0);

        /// <summary>
        /// The smallest size of a room whose floor is larger than <paramref name="cells"/>, as an index into
        /// <paramref name="areas"/> (each size's floor cells); the largest if none is larger; -1 for none at all.
        /// </summary>
        public static int LargerSize(IList<int> areas, int cells)
        {
            int best = -1, largest = -1;
            for (int i = 0; i < areas.Count; i++)
            {
                if (areas[i] > cells && (best < 0 || areas[i] < areas[best])) best = i;
                if (largest < 0 || areas[i] > areas[largest]) largest = i;
            }
            return best >= 0 ? best : largest;
        }

        // ---------------- Light ----------------

        /// <summary>A cell lies within a light's reach (LightMath.Reach of its glow radius): straight-line distance.</summary>
        public static bool Lit(int x, int z, IList<(int x, int z, float reach)> lights)
        {
            if (lights == null) return false;
            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i];
                float dx = x - l.x, dz = z - l.z;
                if (l.reach > 0f && dx * dx + dz * dz <= l.reach * l.reach) return true;
            }
            return false;
        }
    }
}
