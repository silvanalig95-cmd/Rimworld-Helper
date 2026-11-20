using System;

namespace ColonyCouncil
{
    // The rules of colony life's variety, part 1 (the content update, the player's go of 20 November 2026), without game
    // types, so Tools/Tests can check them: a seat holder asks to step down (A3), two wishes and one pile of steel (A4), the
    // night watch and a welcome for newcomers (A7), after the vote (A18), the one sent away (A19), worn thin (A20), and the
    // thresholds of the twenty-five everyday questions.

    /// <summary>A seat holder who asks to step down (CouncilLife/StepDown.cs).</summary>
    public static class StepDownMath
    {
        /// <summary>Worn down or near breaking this many days running (the heart's bands, strain 55 or more).</summary>
        public const float WornDays = 5f;

        /// <summary>In a sickbed with an illness or a wound that has kept them abed this many days (the game's own age of it).</summary>
        public const float LaidUpDays = 10f;

        /// <summary>A deputy does the seat's daily work this many days.</summary>
        public const float DeputyDays = 10f;

        /// <summary>The heart's weight eased when the seat is put down (strain points).</summary>
        public const float Relief = 12f;

        /// <summary>Trust for the colony's devoted when the holder is asked to stay on (the seat kept by someone they trust).</summary>
        public const float DevotedTrust = 2f;

        /// <summary>Whether a holder asks: worn down long enough, or laid up long enough; never while an election is under way.</summary>
        public static bool Asks(float wornDays, float laidUpDays, bool electionOn) =>
            !electionOn && (wornDays >= WornDays || laidUpDays >= LaidUpDays);

        /// <summary>Days someone has stood in a state since <paramref name="since"/> (-1: not now), at <paramref name="now"/>.</summary>
        public static float DaysSince(int since, int now, int ticksPerDay) => since < 0 || now < since ? 0f : (now - since) / (float)ticksPerDay;

        /// <summary>
        /// Since when someone has been in a state, looked at again: the old mark while it holds, now when it begins, -1 once
        /// it is over.
        /// </summary>
        public static int Since(int since, bool holds, int now) => holds ? (since < 0 ? now : since) : -1;

        /// <summary>A deputy's term is still running.</summary>
        public static bool DeputyOn(int until, int now) => until > now;
    }

    /// <summary>Two colonists' wishes that each need the same stuff, and the stores hold enough for only one (CouncilLife/OneSteel.cs).</summary>
    public static class OneSteelMath
    {
        /// <summary>The second wish's deadline moves on this many days, and its asker is told why.</summary>
        public const float WaitDays = 3f;

        /// <summary>At least this much of the stuff is wanted by each, so a handful of components doesn't make a question.</summary>
        public const int MinNeed = 10;

        /// <summary>Enough for either one alone, not for both.</summary>
        public static bool OneOnly(int have, int needA, int needB) =>
            needA >= MinNeed && needB >= MinNeed && have >= needA && have >= needB && have < needA + needB;

        /// <summary>Who asked first: true when A's wish is the older (ties go to A).</summary>
        public static bool AFirst(int createdA, int createdB) => createdA <= createdB;
    }

    /// <summary>The night watch decree (CouncilLife/WatchAndWelcome.cs).</summary>
    public static class NightWatchMath
    {
        /// <summary>The watch walks the walls from nine at night until one.</summary>
        public const int From = 21, To = 1;

        /// <summary>The hours of the watch.</summary>
        public const float Hours = 4f;

        /// <summary>
        /// Tonight's watchers in the rota, by the decree's day (0 the first night): two of the colony's fighters, taking it in
        /// turn so each walks as often as the others; the indexes into the fighters in their fixed order, the second -1 with
        /// only one fighter.
        /// </summary>
        public static (int first, int second) Pair(int day, int fighters)
        {
            if (fighters <= 0) return (-1, -1);
            if (fighters == 1) return (0, -1);
            int a = (int)((2L * Math.Max(0, day)) % fighters);
            int b = (a + 1) % fighters;
            return (a, b);
        }
    }

    /// <summary>A welcome for newcomers (CouncilLife/WatchAndWelcome.cs).</summary>
    public static class WelcomeMath
    {
        /// <summary>Someone here fewer than this many days is a newcomer to the decree.</summary>
        public const float NewcomerDays = 20f;

        /// <summary>The one who welcomes them has been here at least this long.</summary>
        public const float SettledDays = 20f;

        /// <summary>The evening's hour with them: from six to nine.</summary>
        public const int From = 18, To = 21;

        /// <summary>A newcomer: here at least a day and fewer than <see cref="NewcomerDays"/>.</summary>
        public static bool Newcomer(float daysHere) => daysHere >= 1f && daysHere < NewcomerDays;

        /// <summary>Settled enough to welcome someone: here long enough, and not thinking badly of them.</summary>
        public static bool CanWelcome(float daysHere, int opinionOfThem) => daysHere >= SettledDays && opinionOfThem >= 0;

        /// <summary>Welcomes the evening counts for the group the newcomer is finding their way into: two from one of its own, one from anyone else.</summary>
        public static int Welcomes(bool hostInTheGroup) => hostInTheGroup ? 2 : 1;
    }

    /// <summary>The three personal stories (CouncilLife/LifeArcs.cs).</summary>
    public static class LifeArcMath
    {
        /// <summary>After the vote: a seat held a year or more (sixty days, the game's year).</summary>
        public const float HeldDays = 60f;

        /// <summary>After the vote: the story begins within this many days of the count.</summary>
        public const float VotedOutWithin = 6f;

        /// <summary>The one sent away: within this many days of their going.</summary>
        public const float SentAwayWithin = 4f;

        /// <summary>The one sent away: close enough to them to feel it (opinion).</summary>
        public const int CloseOpinion = 10;

        /// <summary>Worn thin: within this many days of the heart's own episode, or this long near breaking.</summary>
        public const float EpisodeWithin = 3f, BreakingDays = 7f;

        /// <summary>Worn thin ends when strain stays at most this.</summary>
        public const float BackToSelf = 30f;

        /// <summary>The weight it lifts at its end (strain points).</summary>
        public const float EndRelief = 10f;

        public static bool HeldLongEnough(float heldDays) => heldDays >= HeldDays;

        /// <summary>After the vote: they help the winner when they don't think too badly of them and no deep rivalry stands between.</summary>
        public static bool Helps(int opinionOfWinner, float rivalry) => opinionOfWinner >= -10 && rivalry < 40f;

        /// <summary>Worn thin begins after the episode, or after a week near breaking.</summary>
        public static bool WornThin(float daysSinceEpisode, bool episodeOver, float daysBreaking) =>
            (episodeOver && daysSinceEpisode >= 0f && daysSinceEpisode <= EpisodeWithin) || daysBreaking >= BreakingDays;
    }

    /// <summary>The thresholds of the twenty-five everyday questions (CouncilLife/EverydayQuestions.cs).</summary>
    public static class EverydayMath
    {
        /// <summary>The only one who knows how: this good at it, and nobody else at home better than <see cref="TradeNextBest"/>.</summary>
        public const int TradeMaster = 12, TradeNextBest = 6;

        /// <summary>Every hand to the harvest: this many ripe plants waiting in the fields.</summary>
        public const int RipeWaiting = 40;

        /// <summary>A second helping: food for fewer than this many days.</summary>
        public const float FoodShortDays = 6f;

        /// <summary>Laid up and bored: in a sickbed this many days, their recreation under <see cref="BoredJoy"/>.</summary>
        public const float BedDays = 2f, BoredJoy = 0.3f;

        /// <summary>The sickroom: this much filth in a room with a patient abed.</summary>
        public const int SickroomFilth = 6;

        /// <summary>A chair for the old bones: from this age.</summary>
        public const float ElderAge = 60f;

        /// <summary>A young voice: from this age to that.</summary>
        public const float YoungFrom = 18f, YoungTo = 23f;

        /// <summary>A name for the newborn: born within this many days.</summary>
        public const float NewbornDays = 5f;

        /// <summary>A cold bedroom: under this many degrees, the other's room at least <see cref="WarmerBy"/> warmer.</summary>
        public const float ColdRoom = 12f, WarmerBy = 5f;

        /// <summary>The first warm day of spring: at least this warm outdoors.</summary>
        public const float WarmDay = 18f;

        /// <summary>Dawn to dark: strain at least this (strained).</summary>
        public const float LongDaysStrain = 30f;

        /// <summary>The good meals: at least one fine meal a colonist.</summary>
        public static bool EnoughFine(int fineMeals, int colonists) => colonists > 0 && fineMeals >= colonists;

        /// <summary>The only one who knows how: the master is far ahead of the next best.</summary>
        public static bool OnlyOne(int master, int nextBest) => master >= TradeMaster && nextBest < TradeNextBest;

        /// <summary>A cold bedroom: theirs cold, the other's clearly warmer.</summary>
        public static bool ColderRoom(float theirs, float other) => theirs < ColdRoom && other - theirs >= WarmerBy;
    }
}
