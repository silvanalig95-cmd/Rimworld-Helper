namespace ColonyCouncil
{
    /// <summary>
    /// Every periodic pass in <see cref="Sched"/>'s table, by number, for Rimfolk's clock (<c>PassClock</c>): the hourly
    /// ones, the 250-tick ones, those that only run while something waits, and the daily ones. Where one tick of Sched
    /// carries two systems' work (the things answers keep on the map, the visitors' services, the callers and the answers
    /// that change someone for a while all run at <see cref="Sched.RareTimed"/>), each has a pass of its own.
    /// </summary>
    public enum Pass
    {
        // Once an hour, in the order they come in the hour.
        Neighbours,
        CouncilWishes,
        Camp,
        CouncilDayFirst,
        Health,
        CouncilDaySecond,
        CouncilLife,
        WorldSituations,
        Calendar,
        Setbacks,
        CouncilPeople,
        Reputation,
        Arcs,
        Groups,
        Fields,
        Stories,
        Weather,
        CouncilEvents,
        Talk,
        Ailments,
        /// <summary>The region's hour, on a tick of the hour's first 250-tick stretch (Sched.HourlyOnCycle).</summary>
        Region,

        // Every 250 ticks, in the order they come in the cycle.
        RareSmoke,
        RareBuildPriority,
        RareWorldKeep,
        RarePlanQueue,
        RareHeart,
        RareCamp,
        RareMissions,
        RareWeather,
        RareWelcome,
        RareHealth,
        RareFightSides,
        RareAilments,
        RareDefenseLines,

        // Every 250 ticks, only while something waits, in the same order.
        RareRearrange,
        RareCouriers,
        RareOutsiderTalk,
        RarePlayOut,
        RareVisitors,
        RareTimed,
        RareVisitorServices,
        RareCallers,
        RareAnswerShifts,
        RareNews,
        RareJourneys,
        RoadFood,
        RareWeekMemory,
        RareNeighbours,
        RareOutsiders,

        // Other rhythms.
        PeopleLog,
        DailyWealth,
        DailyMagicNeeds,
        DailyBestFriends,
        DailyHistory,
        DailyRegion,
    }

    /// <summary>One pass in the clock's table: its name (a key in the keyed file), the slot in Sched it runs on and how often.</summary>
    public readonly struct PassInfo
    {
        public readonly Pass pass;
        /// <summary>Its name, as DESIGN.md's ticking table has it (Languages/English/Keyed/Rimfolk_Clock.xml).</summary>
        public readonly string key;
        /// <summary>The name of the constant in <see cref="Sched"/> that gives its tick.</summary>
        public readonly string slot;
        /// <summary>Ticks between its turns.</summary>
        public readonly int period;
        /// <summary>The tick of that period it runs on.</summary>
        public readonly int offset;
        /// <summary>It only runs while something waits for it (a visit, a letter, an answer playing out).</summary>
        public readonly bool waiting;

        public PassInfo(Pass pass, string key, string slot, int period, int offset, bool waiting = false)
        {
            this.pass = pass;
            this.key = key;
            this.slot = slot;
            this.period = period;
            this.offset = offset;
            this.waiting = waiting;
        }
    }

    /// <summary>The clock's table of passes, in the order of <see cref="Pass"/>. Pure (no game types): Tools/Tests checks it against Sched.</summary>
    public static class PassTable
    {
        private const int H = Sched.HourTicks, R = Sched.RareTicks, D = Sched.DayTicks;

        public static readonly PassInfo[] All =
        {
            new PassInfo(Pass.Neighbours, "CC_Clock_Pass_Neighbours", nameof(Sched.Neighbours), H, Sched.Neighbours),
            new PassInfo(Pass.CouncilWishes, "CC_Clock_Pass_CouncilWishes", nameof(Sched.CouncilWishes), H, Sched.CouncilWishes),
            new PassInfo(Pass.Camp, "CC_Clock_Pass_Camp", nameof(Sched.Camp), H, Sched.Camp),
            new PassInfo(Pass.CouncilDayFirst, "CC_Clock_Pass_CouncilDayFirst", nameof(Sched.CouncilDayFirst), H, Sched.CouncilDayFirst),
            new PassInfo(Pass.Health, "CC_Clock_Pass_Health", nameof(Sched.Health), H, Sched.Health),
            new PassInfo(Pass.CouncilDaySecond, "CC_Clock_Pass_CouncilDaySecond", nameof(Sched.CouncilDaySecond), H, Sched.CouncilDaySecond),
            new PassInfo(Pass.CouncilLife, "CC_Clock_Pass_CouncilLife", nameof(Sched.CouncilLife), H, Sched.CouncilLife),
            new PassInfo(Pass.WorldSituations, "CC_Clock_Pass_WorldSituations", nameof(Sched.WorldSituations), H, Sched.WorldSituations),
            new PassInfo(Pass.Calendar, "CC_Clock_Pass_Calendar", nameof(Sched.Calendar), H, Sched.Calendar),
            new PassInfo(Pass.Setbacks, "CC_Clock_Pass_Setbacks", nameof(Sched.Setbacks), H, Sched.Setbacks),
            new PassInfo(Pass.CouncilPeople, "CC_Clock_Pass_CouncilPeople", nameof(Sched.CouncilPeople), H, Sched.CouncilPeople),
            new PassInfo(Pass.Reputation, "CC_Clock_Pass_Reputation", nameof(Sched.Reputation), H, Sched.Reputation),
            new PassInfo(Pass.Arcs, "CC_Clock_Pass_Arcs", nameof(Sched.Arcs), H, Sched.Arcs),
            new PassInfo(Pass.Groups, "CC_Clock_Pass_Groups", nameof(Sched.Groups), H, Sched.Groups),
            new PassInfo(Pass.Fields, "CC_Clock_Pass_Fields", nameof(Sched.Fields), H, Sched.Fields),
            new PassInfo(Pass.Stories, "CC_Clock_Pass_Stories", nameof(Sched.Stories), H, Sched.Stories),
            new PassInfo(Pass.Weather, "CC_Clock_Pass_Weather", nameof(Sched.Weather), H, Sched.Weather),
            new PassInfo(Pass.CouncilEvents, "CC_Clock_Pass_CouncilEvents", nameof(Sched.CouncilEvents), H, Sched.CouncilEvents),
            new PassInfo(Pass.Talk, "CC_Clock_Pass_Talk", nameof(Sched.Talk), H, Sched.Talk),
            new PassInfo(Pass.Ailments, "CC_Clock_Pass_Ailments", nameof(Sched.Ailments), H, Sched.Ailments),
            new PassInfo(Pass.Region, "CC_Clock_Pass_Region", nameof(Sched.Region), H, Sched.Region),

            new PassInfo(Pass.RareSmoke, "CC_Clock_Pass_RareSmoke", nameof(Sched.RareSmoke), R, Sched.RareSmoke),
            new PassInfo(Pass.RareBuildPriority, "CC_Clock_Pass_RareBuildPriority", nameof(Sched.RareBuildPriority), R, Sched.RareBuildPriority),
            new PassInfo(Pass.RareWorldKeep, "CC_Clock_Pass_RareWorldKeep", nameof(Sched.RareWorldKeep), R, Sched.RareWorldKeep),
            new PassInfo(Pass.RarePlanQueue, "CC_Clock_Pass_RarePlanQueue", nameof(Sched.RarePlanQueue), R, Sched.RarePlanQueue),
            new PassInfo(Pass.RareHeart, "CC_Clock_Pass_RareHeart", nameof(Sched.RareHeart), R, Sched.RareHeart),
            new PassInfo(Pass.RareCamp, "CC_Clock_Pass_RareCamp", nameof(Sched.RareCamp), R, Sched.RareCamp),
            new PassInfo(Pass.RareMissions, "CC_Clock_Pass_RareMissions", nameof(Sched.RareMissions), R, Sched.RareMissions),
            new PassInfo(Pass.RareWeather, "CC_Clock_Pass_RareWeather", nameof(Sched.RareWeather), R, Sched.RareWeather),
            new PassInfo(Pass.RareWelcome, "CC_Clock_Pass_RareWelcome", nameof(Sched.RareWelcome), R, Sched.RareWelcome),
            new PassInfo(Pass.RareHealth, "CC_Clock_Pass_RareHealth", nameof(Sched.RareHealth), R, Sched.RareHealth),
            new PassInfo(Pass.RareFightSides, "CC_Clock_Pass_RareFightSides", nameof(Sched.RareFightSides), R, Sched.RareFightSides),
            new PassInfo(Pass.RareAilments, "CC_Clock_Pass_RareAilments", nameof(Sched.RareAilments), R, Sched.RareAilments),
            new PassInfo(Pass.RareDefenseLines, "CC_Clock_Pass_RareDefenseLines", nameof(Sched.RareDefenseLines), R, Sched.RareDefenseLines),

            new PassInfo(Pass.RareRearrange, "CC_Clock_Pass_RareRearrange", nameof(Sched.RareRearrange), R, Sched.RareRearrange, true),
            new PassInfo(Pass.RareCouriers, "CC_Clock_Pass_RareCouriers", nameof(Sched.RareCouriers), R, Sched.RareCouriers, true),
            new PassInfo(Pass.RareOutsiderTalk, "CC_Clock_Pass_RareOutsiderTalk", nameof(Sched.RareOutsiderTalk), R, Sched.RareOutsiderTalk, true),
            new PassInfo(Pass.RarePlayOut, "CC_Clock_Pass_RarePlayOut", nameof(Sched.RarePlayOut), R, Sched.RarePlayOut, true),
            new PassInfo(Pass.RareVisitors, "CC_Clock_Pass_RareVisitors", nameof(Sched.RareVisitors), R, Sched.RareVisitors, true),
            new PassInfo(Pass.RareTimed, "CC_Clock_Pass_RareTimed", nameof(Sched.RareTimed), R, Sched.RareTimed, true),
            new PassInfo(Pass.RareVisitorServices, "CC_Clock_Pass_RareVisitorServices", nameof(Sched.RareTimed), R, Sched.RareTimed, true),
            new PassInfo(Pass.RareCallers, "CC_Clock_Pass_RareCallers", nameof(Sched.RareTimed), R, Sched.RareTimed, true),
            new PassInfo(Pass.RareAnswerShifts, "CC_Clock_Pass_RareAnswerShifts", nameof(Sched.RareTimed), R, Sched.RareTimed, true),
            new PassInfo(Pass.RareNews, "CC_Clock_Pass_RareNews", nameof(Sched.RareNews), R, Sched.RareNews, true),
            new PassInfo(Pass.RareJourneys, "CC_Clock_Pass_RareJourneys", nameof(Sched.RareJourneys), R, Sched.RareJourneys, true),
            new PassInfo(Pass.RoadFood, "CC_Clock_Pass_RoadFood", nameof(Sched.RareJourneys), H, Sched.RareJourneys, true),
            new PassInfo(Pass.RareWeekMemory, "CC_Clock_Pass_RareWeekMemory", nameof(Sched.RareWeekMemory), R, Sched.RareWeekMemory, true),
            new PassInfo(Pass.RareNeighbours, "CC_Clock_Pass_RareNeighbours", nameof(Sched.RareNeighbours), R, Sched.RareNeighbours, true),
            new PassInfo(Pass.RareOutsiders, "CC_Clock_Pass_RareOutsiders", nameof(Sched.RareOutsiders), R, Sched.RareOutsiders, true),

            new PassInfo(Pass.PeopleLog, "CC_Clock_Pass_PeopleLog", nameof(Sched.PeopleLog), Sched.PeopleLogPeriod, Sched.PeopleLog),
            new PassInfo(Pass.DailyWealth, "CC_Clock_Pass_DailyWealth", nameof(Sched.DailyWealth), D, Sched.DailyWealth),
            new PassInfo(Pass.DailyMagicNeeds, "CC_Clock_Pass_DailyMagicNeeds", nameof(Sched.DailyMagicNeeds), D, Sched.DailyMagicNeeds),
            new PassInfo(Pass.DailyBestFriends, "CC_Clock_Pass_DailyBestFriends", nameof(Sched.DailyBestFriends), D, Sched.DailyBestFriends),
            new PassInfo(Pass.DailyHistory, "CC_Clock_Pass_DailyHistory", nameof(Sched.DailyHistory), D, Sched.DailyHistory),
            new PassInfo(Pass.DailyRegion, "CC_Clock_Pass_DailyRegion", nameof(Sched.DailyRegion), D, Sched.DailyRegion),
        };

        public static int Count => All.Length;
    }

    /// <summary>
    /// A stretch of game time a pass's runs are added up in: which in-game hour (or day) it is, counted from the start of
    /// the game, how many runs and their time (in the stopwatch's ticks).
    /// </summary>
    public struct PassWindow
    {
        /// <summary>No window yet.</summary>
        public const int None = int.MinValue;

        public int index;
        public int runs;
        public long time;

        public static PassWindow Empty(int index) => new PassWindow { index = index };
    }

    /// <summary>
    /// One pass's figures since the game was loaded: this hour and day and the ones before, every run, and the worst
    /// single run with the tick it came on. Rolled on by game ticks, never by real time. Pure (no game types).
    /// </summary>
    public sealed class PassFigures
    {
        public PassWindow hour, lastHour, day, lastDay;
        public int runs;
        public long time;
        public long worst;
        public int worstTick;

        public PassFigures() => Clear();

        public void Clear()
        {
            hour = lastHour = day = lastDay = PassWindow.Empty(PassWindow.None);
            runs = 0;
            time = worst = 0;
            worstTick = PassWindow.None;
        }

        /// <summary>One run that took <paramref name="elapsed"/> stopwatch ticks, at game tick <paramref name="now"/>.</summary>
        public void Add(int now, long elapsed)
        {
            if (elapsed < 0) elapsed = 0;
            PassClockMath.Add(ref hour, ref lastHour, PassClockMath.Index(now, Sched.HourTicks), elapsed);
            PassClockMath.Add(ref day, ref lastDay, PassClockMath.Index(now, Sched.DayTicks), elapsed);
            runs++;
            time += elapsed;
            if (runs == 1 || elapsed > worst)
            {
                worst = elapsed;
                worstTick = now;
            }
        }

        /// <summary>The in-game hour <paramref name="now"/> is in, so far.</summary>
        public PassWindow HourAt(int now) => PassClockMath.Current(hour, PassClockMath.Index(now, Sched.HourTicks));

        /// <summary>The whole in-game hour before the one <paramref name="now"/> is in.</summary>
        public PassWindow LastHourAt(int now) => PassClockMath.Previous(hour, lastHour, PassClockMath.Index(now, Sched.HourTicks));

        /// <summary>The in-game day <paramref name="now"/> is in, so far.</summary>
        public PassWindow DayAt(int now) => PassClockMath.Current(day, PassClockMath.Index(now, Sched.DayTicks));

        /// <summary>The whole in-game day before the one <paramref name="now"/> is in.</summary>
        public PassWindow LastDayAt(int now) => PassClockMath.Previous(day, lastDay, PassClockMath.Index(now, Sched.DayTicks));
    }

    /// <summary>The clock's arithmetic. Pure (no game types): Tools/Tests checks it.</summary>
    public static class PassClockMath
    {
        /// <summary>Which window of <paramref name="length"/> ticks a game tick is in (an hour, a day), counted from the start of the game.</summary>
        public static int Index(int tick, int length) => Fresh.Bucket(tick, length);

        /// <summary>One run in the window <paramref name="index"/>, rolling the windows on first.</summary>
        public static void Add(ref PassWindow cur, ref PassWindow prev, int index, long elapsed)
        {
            Roll(ref cur, ref prev, index);
            cur.runs++;
            cur.time += elapsed;
        }

        /// <summary>
        /// Moves the windows on to <paramref name="index"/>: the current one becomes the previous one when it was the window
        /// just before; after a gap (no runs in the window before) the previous one is empty; the clock going back (an older
        /// save) starts both again.
        /// </summary>
        public static void Roll(ref PassWindow cur, ref PassWindow prev, int index)
        {
            if (cur.index == index) return;
            prev = cur.index != PassWindow.None && cur.index == index - 1 ? cur : PassWindow.Empty(index - 1);
            cur = PassWindow.Empty(index);
        }

        /// <summary>The window <paramref name="index"/> as kept: what ran in it so far, or nothing.</summary>
        public static PassWindow Current(PassWindow cur, int index) => cur.index == index ? cur : PassWindow.Empty(index);

        /// <summary>The window before <paramref name="index"/> as kept: the previous one, or the current one if no run has come since it ended, or nothing.</summary>
        public static PassWindow Previous(PassWindow cur, PassWindow prev, int index)
        {
            if (cur.index == index) return prev.index == index - 1 ? prev : PassWindow.Empty(index - 1);
            if (cur.index == index - 1) return cur;
            return PassWindow.Empty(index - 1);
        }

        /// <summary>
        /// The clock, running since <paramref name="since"/>, saw the whole window before the one <paramref name="now"/> is
        /// in; until then only the window so far can be shown.
        /// </summary>
        public static bool SawWholePrevious(int since, int now, int length)
        {
            if (since == PassWindow.None || length <= 0) return false;
            long previousStart = ((long)Index(now, length) - 1) * length;
            return since <= previousStart;
        }

        /// <summary>Stopwatch ticks as milliseconds.</summary>
        public static double Ms(long time, long frequency) => frequency <= 0 ? 0.0 : time * 1000.0 / frequency;

        /// <summary>The average run, in stopwatch ticks.</summary>
        public static double PerRun(long time, int runs) => runs <= 0 ? 0.0 : (double)time / runs;
    }
}
