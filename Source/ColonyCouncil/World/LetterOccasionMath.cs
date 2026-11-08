namespace ColonyCouncil
{
    /// <summary>Why a letter of kind words can be written now (docs/design/post-and-letters.md, step 2, point 10), or that there is no reason.</summary>
    public enum LetterOccasion : byte
    {
        /// <summary>No reason to write it now: it is greyed with what would bring one.</summary>
        None,
        /// <summary>Asking for news: always open.</summary>
        Always,
        /// <summary>Condolences: a loss of theirs the colony has heard of.</summary>
        Loss,
        /// <summary>A warning: a threat to them the colony knows of.</summary>
        Threat,
        /// <summary>An invitation: the colony's feast with guests being planned or set.</summary>
        Feast,
        /// <summary>An invitation: a day on the colony's calendar kept with a gathering, within a week.</summary>
        Gathering,
        /// <summary>An invitation: guest beds standing empty, nobody from outside staying (the colony would like visitors).</summary>
        Visitors,
        /// <summary>Asking for help: the stores short of food.</summary>
        ShortFood,
        /// <summary>Asking for help: short of medicine.</summary>
        ShortMedicine,
        /// <summary>Greetings: the turn of the year.</summary>
        NewYear,
        /// <summary>Greetings: to go with a gift the colony gave them lately.</summary>
        Gift,
        /// <summary>Not one of the kind letters: its own checks say when it can go (a pact, a truce, a debt called in).</summary>
        Own
    }

    /// <summary>What the occasions are read from, plain data (World/LetterOccasions.cs gathers it from what Rimfolk tracks).</summary>
    public struct OccasionFacts
    {
        /// <summary>A loss of theirs heard of and not yet written of (NeighbourLetters.Topics for condolences).</summary>
        public bool lossHeard;
        /// <summary>A threat to them known and not yet written of (NeighbourLetters.Topics for a warning).</summary>
        public bool threatKnown;
        /// <summary>The home's feast with guests is being planned or set (Diplomacy/Feasts.cs).</summary>
        public bool feastComing;
        /// <summary>A day on the colony calendar kept with a gathering comes within a week (Calendar/).</summary>
        public bool gatheringComing;
        /// <summary>The home has guest beds (Hospitality) and nobody from outside is staying.</summary>
        public bool guestBedsEmpty;
        /// <summary>The colony gave them a gift lately (their memory's gift deeds, World/FactionMemory.cs).</summary>
        public bool giftLately;
        /// <summary>The home's days of food (AdvisorWorker_Food.DaysOfFood) and its medicine, and how many colonists it feeds.</summary>
        public float foodDays;
        public int medicine;
        public int colonists;
        /// <summary>Today's day of the year, 0 to 59.</summary>
        public int dayOfYear;
    }

    /// <summary>
    /// Letters by occasion (docs/design/post-and-letters.md, step 2, point 10): condolences only after a loss heard of, a
    /// warning when a threat to them is known, an invitation when a feast or a gathering day is coming or the guest beds
    /// stand empty, asking for help when the colony is short of food or medicine, greetings at the turn of the year or with a
    /// gift, and asking for news always. With the setting "Letters by occasion" off, only the old checks hold. No game
    /// types, so Tools/Tests checks it.
    /// </summary>
    public static class LetterOccasionMath
    {
        /// <summary>Short of food: under six days in the stores, the line under which allies send food (World/Allies.cs).</summary>
        public const float ShortFoodDays = 6f;

        /// <summary>The year's days, and the turn of the year: its last three days and its first seven.</summary>
        public const int DaysPerYear = 60, YearEndDays = 3, YearStartDays = 7;

        /// <summary>A gathering day counts as coming within this many days.</summary>
        public const int GatheringDays = 7;

        /// <summary>A gift counts as lately given for this many days.</summary>
        public const int GiftDays = 10;

        public static bool ShortOfFood(float foodDays, int colonists) => colonists > 0 && foodDays < ShortFoodDays;

        /// <summary>Short of medicine: less than one dose for each colonist.</summary>
        public static bool ShortOfMedicine(int medicine, int colonists) => colonists > 0 && medicine < colonists;

        public static bool TurnOfYear(int dayOfYear)
        {
            int d = (dayOfYear % DaysPerYear + DaysPerYear) % DaysPerYear;
            return d < YearStartDays || d >= DaysPerYear - YearEndDays;
        }

        /// <summary>A day so many days off comes within the week (today counts).</summary>
        public static bool Coming(int daysUntil) => daysUntil >= 0 && daysUntil <= GatheringDays;

        /// <summary>A gift given so many days ago is lately given.</summary>
        public static bool Lately(float daysAgo) => daysAgo >= 0f && daysAgo <= GiftDays;

        /// <summary>The letters that go by occasion; every other purpose keeps its own checks only.</summary>
        public static bool ByOccasion(LetterPurpose p) =>
            p == LetterPurpose.Greetings || p == LetterPurpose.Condolences || p == LetterPurpose.Warning || p == LetterPurpose.Invitation || p == LetterPurpose.Help
            || p == LetterPurpose.News;

        /// <summary>The reason a letter can be written now, the best first, or <see cref="LetterOccasion.None"/>.</summary>
        public static LetterOccasion Occasion(LetterPurpose p, OccasionFacts f)
        {
            switch (p)
            {
                case LetterPurpose.News: return LetterOccasion.Always;
                case LetterPurpose.Condolences: return f.lossHeard ? LetterOccasion.Loss : LetterOccasion.None;
                case LetterPurpose.Warning: return f.threatKnown ? LetterOccasion.Threat : LetterOccasion.None;
                case LetterPurpose.Invitation:
                    return f.feastComing ? LetterOccasion.Feast : f.gatheringComing ? LetterOccasion.Gathering : f.guestBedsEmpty ? LetterOccasion.Visitors : LetterOccasion.None;
                case LetterPurpose.Help:
                    return ShortOfFood(f.foodDays, f.colonists) ? LetterOccasion.ShortFood
                        : ShortOfMedicine(f.medicine, f.colonists) ? LetterOccasion.ShortMedicine : LetterOccasion.None;
                case LetterPurpose.Greetings:
                    return f.giftLately ? LetterOccasion.Gift : TurnOfYear(f.dayOfYear) ? LetterOccasion.NewYear : LetterOccasion.None;
                default:
                    return LetterOccasion.Own;
            }
        }

        /// <summary>
        /// Whether the occasion keeps the letter from being written: only a kind letter with no reason, and only with the
        /// setting on (condolences and a warning had their reasons before the setting: the old checks still hold them).
        /// </summary>
        public static bool Holds(LetterPurpose p, LetterOccasion o, bool settingOn) =>
            o == LetterOccasion.None && (settingOn || p == LetterPurpose.Condolences || p == LetterPurpose.Warning);

        /// <summary>The reason's words: CC_Occ_ and the occasion; with no reason, CC_Occ_None_ and the purpose (what would bring one).</summary>
        public static string Key(LetterPurpose p, LetterOccasion o) => o == LetterOccasion.None ? "CC_Occ_None_" + p : "CC_Occ_" + o;
    }
}
