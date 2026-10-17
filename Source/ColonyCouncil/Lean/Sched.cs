namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's clock: the tick of the hour (or of the 250-tick cycle) on which each periodic pass runs. Every system
    /// still keeps its own clock (they are not merged into one scheduler); the offsets are only listed here, so that no
    /// two of them land on the same tick and the hour's work is spread over the hour instead of piling onto one frame.
    /// DESIGN.md ("Ticking") has the whole table with what each pass does. Pure (no game types): Tools/Tests checks the
    /// table and the arithmetic.
    /// </summary>
    public static class Sched
    {
        public const int HourTicks = 2500;
        public const int DayTicks = 60000;
        /// <summary>The game's "rare" tick: the short passes run this often.</summary>
        public const int RareTicks = 250;

        // ---------------- Once an hour, at this tick of the hour ----------------

        /// <summary>The council: fights and losses, decrees, wish upkeep, promises kept, event wishes, new wishes.</summary>
        public const int CouncilWishes = 157;
        /// <summary>A camp's hour (only while one is there), inside the camp's own 250-tick pass.</summary>
        public const int Camp = 327;
        /// <summary>The council's day, first half: trust, standards, decrees, anniversaries, names, journals, offices.</summary>
        public const int CouncilDayFirst = 502;
        /// <summary>Health stories, the colony's part: the cases of dangerous illness, and the day's pruning (each colonist's part runs in <see cref="RareHealth"/>'s slices).</summary>
        public const int Health = 677;
        /// <summary>The council's day, second half: the social web, life goals, secrets, vendettas, quirks, legacy, character.</summary>
        public const int CouncilDaySecond = 852;
        /// <summary>The council: ambitions, personal touches, dilemmas, annals, dreams, quirks.</summary>
        public const int CouncilLife = 1027;
        /// <summary>World situations.</summary>
        public const int WorldSituations = 1203;
        /// <summary>The colony calendar.</summary>
        public const int Calendar = 1311;
        /// <summary>Setbacks: the lasting ones, crops lost, noisy nights, long hauls, rain on gatherings; the day's in its first hour.</summary>
        public const int Setbacks = 1427;
        /// <summary>The council: nemeses, pets, PokeWorld, autonomy, who is home, everyday pressures.</summary>
        public const int CouncilPeople = 1544;
        /// <summary>Reputation and gossip: the fight's rescues and the brave's company, idling and tending watched; the day's deeds and names in its first hour.</summary>
        public const int Reputation = 1660;
        /// <summary>Personal stories.</summary>
        public const int Arcs = 1777;
        /// <summary>Groups: what everyone's work is, and habits kept together (Social/Groups/GroupTracker.cs).</summary>
        public const int Groups = 1903;
        /// <summary>Story chains and missions, inside the missions' own 250-tick pass.</summary>
        public const int Stories = 2089;
        /// <summary>The council: encounters and nuisances, Rimfolk's own events.</summary>
        public const int CouncilEvents = 2280;
        /// <summary>Conversations: what people went through together (night shifts, disasters, gatherings, mishaps); views drift in the first hour of the day.</summary>
        public const int Talk = 2375;
        /// <summary>Ailments, the colony's part: the catching ailments' clock and the day's pruning, inside the slice that lands here (each colonist's part runs in <see cref="RareAilments"/>'s slices).</summary>
        public const int Ailments = 2471;

        /// <summary>Every hourly pass above, for the tests and the self-check.</summary>
        public static readonly int[] HourlyOffsets =
        {
            CouncilWishes, Camp, CouncilDayFirst, Health, CouncilDaySecond, CouncilLife, WorldSituations, Calendar, Setbacks, CouncilPeople,
            Reputation, Arcs, Groups, Stories, CouncilEvents, Talk, Ailments
        };

        /// <summary>The hourly passes that run inside a 250-tick pass of their own system on purpose (same tick, same work).</summary>
        public static readonly int[] HourlyInsideRare = { Camp, Stories, Ailments };

        // ---------------- Every 250 ticks, at this tick of the cycle ----------------

        /// <summary>World situations keep their people from the world's clean-up.</summary>
        public const int RareWorldKeep = 37;
        /// <summary>The room planner's queue of blueprints (only while something is queued).</summary>
        public const int RarePlanQueue = 41;
        /// <summary>One tenth of the colony's hearts (see <see cref="SliceTurn"/>).</summary>
        public const int RareHeart = 71;
        /// <summary>A camp's quarter hour of life (only while one is there).</summary>
        public const int RareCamp = 77;
        /// <summary>Missions: a quest accepted, a goal met.</summary>
        public const int RareMissions = 89;
        /// <summary>The first-run welcome letter, and the letter after an update (nothing once they have been sent).</summary>
        public const int RareWelcome = 113;
        /// <summary>One tenth of the colonists' ailments (see <see cref="SliceTurn"/>); the slice at tick 2471 also carries the colony's part (<see cref="Ailments"/>).</summary>
        public const int RareAilments = 221;
        /// <summary>One tenth of the colonists' health stories (see <see cref="SliceTurn"/>).</summary>
        public const int RareHealth = 139;

        /// <summary>How many slices the per-colonist passes spread the colony over: each colonist comes round once an hour.</summary>
        public const int Slices = 10;

        /// <summary>Defense lines watch how a raid meets them (only while raiders are on a home map that has lines).</summary>
        public const int RareDefenseLines = 233;

        public static readonly int[] RareOffsets = { RareWorldKeep, RarePlanQueue, RareHeart, RareCamp, RareMissions, RareWelcome, RareAilments, RareHealth, RareDefenseLines };

        /// <summary>Passes on the 250-tick cycle that only run while something is waiting (the news of the evening, an answer playing out, the council window's memory).</summary>
        public const int RareNews = 167;
        public const int RarePlayOut = 113;
        public const int RareWeekMemory = 199;

        // ---------------- Other rhythms ----------------

        /// <summary>Every 1000 ticks: other mods' people read the play log and the tales.</summary>
        public const int PeopleLogPeriod = 1000;
        public const int PeopleLog = 433;
        /// <summary>Once a day, at this tick of the day: the advisor's wealth sample, and best friends.</summary>
        public const int DailyWealth = 433;
        public const int DailyBestFriends = 1789;

        // ---------------- Arithmetic ----------------

        /// <summary>a mod b, never negative (b &gt; 0).</summary>
        public static int Mod(int a, int b)
        {
            int r = a % b;
            return r < 0 ? r + b : r;
        }

        /// <summary>This tick is the pass's tick of the hour.</summary>
        public static bool Hourly(int now, int offset) => Mod(now, HourTicks) == offset;

        /// <summary>This tick is the pass's tick of a <paramref name="period"/>-tick cycle.</summary>
        public static bool Every(int now, int period, int offset) => Mod(now, period) == offset;

        /// <summary>Within the first hour of the game day, where daily work runs (on whichever tick of the hour a pass has).</summary>
        public static bool FirstHourOfDay(int now) => Mod(now, DayTicks) < HourTicks;

        /// <summary>Which of <paramref name="slices"/> slices an id (a colonist's thingIDNumber) belongs to.</summary>
        public static int SliceOf(int id, int slices) => Mod(id, slices);

        /// <summary>
        /// Spreads a per-colonist pass over a period: the pass runs every period/slices ticks at <paramref name="offset"/>,
        /// and each time it is one slice's turn, so each slice comes round once per period. Returns that slice, or -1 when
        /// this tick isn't a turn. (The heart: period an hour, ten slices, offset 71.)
        /// </summary>
        public static int SliceTurn(int now, int period, int slices, int offset)
        {
            if (slices <= 0 || period < slices) return -1;
            int step = period / slices;
            if (Mod(now, step) != offset) return -1;
            return Mod(now / step, slices);
        }
    }
}
