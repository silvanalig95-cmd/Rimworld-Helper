using System;

namespace ColonyCouncil
{
    /// <summary>How a feast turns out with the meals in the stores (see <see cref="PlayOutMath.PlanFeast"/>).</summary>
    public enum FeastPlan
    {
        /// <summary>A cooked meal for everyone who comes.</summary>
        Feast,
        /// <summary>Meals for some: the first to the table eat, the rest make do with talk.</summary>
        Partial,
        /// <summary>No cooked meals at all: it's a party instead.</summary>
        Party
    }

    /// <summary>
    /// The numbers behind answers that play out on the map (PlayOut/): when a gathering may start, how long it may wait,
    /// who counts as having been there, and what a feast can offer. No game types, so Tools/Tests can check them.
    /// </summary>
    public static class PlayOutMath
    {
        public const int TicksPerHour = 2500;

        /// <summary>A gathering's usual hours to start in (from six in the morning until ten at night).</summary>
        public const int DayFrom = 6;
        public const int DayTo = 22;

        /// <summary>How long a plan waits by default for danger to pass and people to be free, after its hours open.</summary>
        public const float DefaultWaitHours = 6f;

        /// <summary>The longest any plan waits, whatever its hours and wait say (so nothing hangs about for days).</summary>
        public const float MaxWaitHours = 30f;

        public static int WrapHour(int h) => ((h % 24) + 24) % 24;

        /// <summary>Within the hours from <paramref name="from"/> (inclusive) to <paramref name="to"/> (exclusive), wrapping past midnight (20 to 2). Equal ends: any hour.</summary>
        public static bool InHours(int hour, int from, int to)
        {
            hour = WrapHour(hour);
            from = WrapHour(from);
            to = WrapHour(to);
            if (from == to) return true;
            return from < to ? hour >= from && hour < to : hour >= from || hour < to;
        }

        /// <summary>Whole hours until the window opens: 0 when it's open now.</summary>
        public static int HoursUntil(int hour, int from, int to)
        {
            if (InHours(hour, from, to)) return 0;
            return WrapHour(WrapHour(from) - WrapHour(hour));
        }

        /// <summary>When a plan gives up and leaves only its moods: its hours, then the wait it allows (never more than <see cref="MaxWaitHours"/>).</summary>
        public static int Deadline(int now, int hour, int from, int to, float waitHours)
        {
            float wait = Math.Max(1f, waitHours);
            float total = Math.Min(MaxWaitHours, HoursUntil(hour, from, to) + wait);
            return now + (int)Math.Round(total * TicksPerHour);
        }

        /// <summary>
        /// There long enough to count as having taken part: a quarter of the gathering, but never more than half an hour
        /// and never less than a few minutes.
        /// </summary>
        public static bool Attended(int presentTicks, int durationTicks)
        {
            int need = Math.Max(250, Math.Min(TicksPerHour / 2, durationTicks / 4));
            return presentTicks >= need;
        }

        /// <summary>A visit or a lesson counts once half of it has passed (cut short later, it still happened).</summary>
        public static bool VisitCounts(int stayedTicks, int durationTicks) => stayedTicks >= Math.Max(250, durationTicks / 2);

        /// <summary>What the stores allow: a meal each, a meal for some, or none at all.</summary>
        public static FeastPlan PlanFeast(int meals, int guests)
        {
            if (meals <= 0) return FeastPlan.Party;
            return meals >= guests ? FeastPlan.Feast : FeastPlan.Partial;
        }

        /// <summary>How far into what an answer set going its risk comes (a fight at the supper): a third of the way, an hour at most.</summary>
        public static int RiskAfter(int durationTicks) => Math.Max(0, Math.Min(durationTicks / 3, TicksPerHour));

        /// <summary>How many hours before a feast its cook puts the pot on (PlayOut/PlayCook.cs).</summary>
        public const int CookLead = 3;

        /// <summary>The most meals a feast's cook makes for it, however much food the answer spent.</summary>
        public const int MaxCooked = 12;

        /// <summary>
        /// The meals cooked for a feast from the raw food the answer spent on it (<paramref name="units"/>, of which a meal
        /// takes <paramref name="perMeal"/>): a meal for each of those asked at most, never more than the food pays for,
        /// and at least one when any was spent.
        /// </summary>
        public static int FeastMeals(int units, int perMeal, int guests)
        {
            if (units <= 0) return 0;
            int meals = units / Math.Max(1, perMeal);
            return Math.Max(1, Math.Min(meals, Math.Min(MaxCooked, Math.Max(1, guests))));
        }

        /// <summary>
        /// No more meals after the one in the pot: the feast can't wait much longer (under an hour before it gives up), or
        /// it's due and the cook has been at it for as long as they'd have had before it.
        /// </summary>
        public static bool StopCooking(bool due, int cookingTicks, int ticksLeft) =>
            ticksLeft < TicksPerHour || (due && cookingTicks >= CookLead * TicksPerHour);

        /// <summary>
        /// The cook waits for a feast that's due: true while nothing is cooked yet and the feast's hours opened less than
        /// two hours ago (they may only be eating or finishing a job); after that it goes ahead with what there is.
        /// </summary>
        public static bool WaitForCook(int hour, int from, int cooked) => cooked <= 0 && WrapHour(hour - from) < 2;

        /// <summary>A gathering's length in ticks from hours, never under a quarter of an hour.</summary>
        public static int DurationTicks(float hours) => Math.Max(TicksPerHour / 4, (int)Math.Round(hours * TicksPerHour));

        /// <summary>Enough people for it to be a gathering: two, or everyone invited when fewer were.</summary>
        public static bool EnoughToGather(int able, int invited) => able >= Math.Min(2, Math.Max(1, invited));

        // ---------------- Groups: a work party, a walk together, a vigil, a search, an errand, a hunt ----------------

        /// <summary>The most people any one of them takes along, whatever the answer asks.</summary>
        public const int MaxGroup = 8;

        /// <summary>How many of them may be under way at once on a map; more wait their turn (they share the same people).</summary>
        public const int MaxGroupsRunning = 2;

        /// <summary>The longest a vigil lasts (a night), and the longest anything else of the kind does.</summary>
        public const float MaxVigilHours = 8f;
        public const float MaxGroupHours = 4f;

        /// <summary>A length in hours, held to a kind's longest.</summary>
        public static float CapHours(float hours, bool vigil) => Math.Max(0.25f, Math.Min(hours, vigil ? MaxVigilHours : MaxGroupHours));

        /// <summary>How many go: those free, never more than the answer asks for (none given: the cap) and never more than the cap.</summary>
        public static int GroupSize(int able, int wanted, int cap)
        {
            cap = Math.Max(1, Math.Min(cap, MaxGroup));
            int want = wanted <= 0 ? cap : Math.Min(wanted, cap);
            return Math.Max(0, Math.Min(able, want));
        }

        /// <summary>Enough free to set out: one for an errand of one, both of a pair, and two for anything bigger.</summary>
        public static bool GroupReady(int able, int wanted) => wanted > 0 && able >= Math.Min(wanted, 2);

        /// <summary>Kept a vigil: there a third of it, at least a few minutes and at most two hours.</summary>
        public static bool VigilCounts(int stayedTicks, int durationTicks) =>
            stayedTicks >= Math.Max(250, Math.Min(TicksPerHour * 2, durationTicks / 3));

        /// <summary>Pitched in at a work party: a job done, or a quarter of it spent at work (at most an hour).</summary>
        public static bool WorkCounts(int jobsDone, int presentTicks, int durationTicks) =>
            jobsDone > 0 || presentTicks >= Math.Max(250, Math.Min(TicksPerHour, durationTicks / 4));

        /// <summary>Went the whole way on a walk together: at the far end for half the stay.</summary>
        public static bool WalkCounts(int stayedThere, int stayTicks) => stayedThere >= Math.Max(250, stayTicks / 2);

        /// <summary>Searched: half the places on the way looked over (rounded up).</summary>
        public static bool SearchCounts(int stopsDone, int stops) => stops > 0 && stopsDone * 2 >= stops;

        /// <summary>
        /// The stages of a walk together: gathering (0), walking out (1), at the far end (2), walking back (3), over (4).
        /// Each moves on when everyone has come, or when most have and they've waited a while, or after a long wait
        /// whoever has come; the stay at the far end lasts its time.
        /// </summary>
        public static int NextWalkPhase(int phase, int arrived, int going, int ticksInPhase, int stayTicks)
        {
            if (going <= 0) return 4;
            bool all = arrived >= going;
            bool most = arrived * 2 > going;
            switch (phase)
            {
                case 0: return all || (most && ticksInPhase >= TicksPerHour / 4) || ticksInPhase >= TicksPerHour / 2 ? 1 : 0;
                case 1: return all || (most && ticksInPhase >= TicksPerHour / 2) || ticksInPhase >= TicksPerHour * 2 ? 2 : 1;
                case 2: return ticksInPhase >= stayTicks ? 3 : 2;
                case 3: return all || ticksInPhase >= TicksPerHour ? 4 : 3;
                default: return 4;
            }
        }

        /// <summary>How long a walk together stays at the far end: what's left of its length after the walking, at least half an hour.</summary>
        public static int WalkStay(int durationTicks) => Math.Max(TicksPerHour / 2, durationTicks / 2);

        /// <summary>The places each searcher looks over: three for one alone, two each for more.</summary>
        public static int SearchStops(int searchers) => searchers <= 1 ? 3 : 2;

        /// <summary>
        /// Which way a searcher heads for one of their places, in degrees: the searchers fan out, each over their own arc
        /// of the compass (turned by <paramref name="seed"/>), and their places step along it.
        /// </summary>
        public static float SearchBearing(int searcher, int searchers, int stop, int stops, int seed)
        {
            searchers = Math.Max(1, searchers);
            float arc = 360f / searchers;
            float start = ((seed % 360) + 360) % 360;
            float within = stops <= 1 ? arc * 0.5f : arc * (0.15f + 0.7f * stop / (stops - 1));
            float b = (start + arc * searcher + within) % 360f;
            return b < 0f ? b + 360f : b;
        }

        /// <summary>How far out a searcher's place is: the first close in, the last farthest out.</summary>
        public static float SearchRadius(int stop, int stops, float near, float far) =>
            stops <= 1 ? (near + far) * 0.5f : near + (far - near) * stop / (stops - 1);

        /// <summary>How long a searcher looks about at each place: a little over a quarter of an hour, more with more time to spare.</summary>
        public static int SearchWait(int durationTicks, int stops) => Math.Max(400, Math.Min(TicksPerHour / 2, durationTicks / Math.Max(1, stops * 3)));

        // Places to stand round something (a grave, a sickbed, the gate), in the order they're taken: the four sides,
        // then the corners, then the ring beyond; nobody on the thing itself.
        private static readonly int[] RingX = { 0, 0, 2, -2, 2, -2, -2, 2, 1, -1, 2, -2, -1, 1, -2, 2, 0, 0, 3, -3 };
        private static readonly int[] RingZ = { 2, -2, 0, 0, 2, -2, 2, -2, 2, -2, -1, 1, 2, -2, -1, 1, 3, -3, 0, 0 };

        public static int RingCount => RingX.Length;

        /// <summary>The <paramref name="i"/>th place round a spot (wrapping round once they're all taken).</summary>
        public static void RingOffset(int i, out int dx, out int dz)
        {
            int k = ((i % RingX.Length) + RingX.Length) % RingX.Length;
            dx = RingX[k];
            dz = RingZ[k];
        }

        /// <summary>
        /// How much an errand takes from the stores: the answer's own number, or the letter's ({COUNT}), times its share,
        /// less the quartermaster's saving; a sum named in the letter is rounded to tens, at least ten.
        /// </summary>
        public static int CarryAmount(int count, int letterCount, float factor, float costFactor, bool tens)
        {
            int n = count > 0 ? count : letterCount;
            if (tens) return Math.Max(10, (int)Math.Round(n * factor / 10f) * 10);
            return Math.Max(1, (int)Math.Round(n * factor * costFactor));
        }

        /// <summary>What one carrier takes in hand: the amount, as much as the stack holds and as much as they can carry.</summary>
        public static int CarryCount(int amount, int stack, int space) => Math.Max(0, Math.Min(amount, Math.Min(stack, space)));

        /// <summary>A hunt is over when the game is down, or its time has run out.</summary>
        public static bool HuntOver(int alive, int elapsedTicks, int durationTicks) => alive <= 0 || elapsedTicks >= durationTicks;

        /// <summary>How long one of them may run past its length before it's let go, whatever is still going on.</summary>
        public static int RunCap(int durationTicks) => durationTicks + TicksPerHour * 3;
    }
}
