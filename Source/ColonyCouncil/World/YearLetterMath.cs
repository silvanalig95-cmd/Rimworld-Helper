namespace ColonyCouncil
{
    /// <summary>
    /// The rules of the one year letter (World/YearLetter.cs): its day (the anniversary of the landing, the colony's
    /// founding day), which year it tells, whether its figures cover the whole year, its parts in order, and how the
    /// founding event's lines are shared between the letter (the colony's founding facts) and the day's own letter (who
    /// keeps the day tonight), so nothing is told twice. Pure: Tools/Tests checks it.
    /// </summary>
    public static class YearLetterMath
    {
        public const int DaysPerYear = 60;
        public const int TicksPerYear = DaysPerYear * 60000;
        /// <summary>The letter comes in the first days of a new year since the landing, as the founding event does.</summary>
        public const int DayWindow = 2;
        /// <summary>A year whose counting began later than this after its landing day is told as from that day only.</summary>
        public const int PartialDays = 3;

        /// <summary>A day for the year's letter: a year or more since the landing, within the first days of a new one.</summary>
        public static bool IsYearDay(int daysPassed) => daysPassed >= DaysPerYear && daysPassed % DaysPerYear <= DayWindow;

        /// <summary>Which year since the landing ended (1 for the first), on a year's day.</summary>
        public static int YearOf(int daysPassed) => daysPassed / DaysPerYear;

        /// <summary>The landing day the told year began on, as a game tick.</summary>
        public static int LandingTick(int year) => (year - 1) * TicksPerYear;

        /// <summary>The figures cover only part of the year: counted from a mod added midway, or (an older save) from its calendar new year.</summary>
        public static bool Partial(bool midway, int countedFrom, int landingTick) => midway || countedFrom > landingTick + PartialDays * 60000;

        /// <summary>The letter's parts in order: the opening, the people (the annals' figures), the land and the world, the founding, the author's last word.</summary>
        public static readonly string[] Parts = { "opening", "people", "land", "founding", "lastWord" };

        /// <summary>The founding event's lines (their tags) that belong to the year's letter: the colony's founding facts.</summary>
        public static readonly string[] YearTags = { "founders", "lost", "known", "customs" };

        /// <summary>The founding event's lines that stay in the day's own letter: who keeps the day tonight.</summary>
        public static readonly string[] DayTags = { "lastfounder", "founder", "firstborn" };
    }
}
