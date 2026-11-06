using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's pace (the player's decision of 6 November 2026): one count of gatherings per colony, as letters and
    /// messages have theirs (PlayOut/Stage.cs keeps it). What the player chose always happens but counts (an answer, a
    /// calendar day kept, a vote, a wedding, a decree, a meeting); the colony's own doings (a group's evening, a family
    /// supper, a camp's show, a birthday) come only while the last seven days hold fewer gatherings than the week's
    /// allowance and none in the last day and a half, wait up to three days for that room and are then let go quietly.
    /// Calendar days within three days of each other are kept together, one within two days of another gathering at
    /// supper; one seasonal custom a season; a floor where the quiet-days setback starts. No game types, so Tools/Tests and
    /// Tools/Balance use these very rules.
    /// </summary>
    public static class StageMath
    {
        public const int TicksPerHour = 2500;
        public const int TicksPerDay = 60000;

        /// <summary>The colony's own doings come while the last seven days hold fewer gatherings than this (the setting's default).</summary>
        public const int DefaultAWeek = 2;

        /// <summary>The setting's range.</summary>
        public const int MinAWeek = 1, MaxAWeek = 7;

        /// <summary>The days the allowance is counted over.</summary>
        public const int WeekDays = 7;

        /// <summary>Never within this many hours of another gathering (a day and a half).</summary>
        public const int GapHours = 36;

        /// <summary>How long one of the colony's own doings waits for room before it is let go.</summary>
        public const int WaitDays = 3;

        /// <summary>Calendar days within this many days of each other are kept together, as one.</summary>
        public const int TogetherDays = 3;

        /// <summary>A calendar day within this many days of another gathering is kept at supper instead.</summary>
        public const int SupperDays = 2;

        /// <summary>The 60-day year's seasons, fifteen days each.</summary>
        public const int SeasonDays = 15;

        /// <summary>Never this many days without a gathering: where the quiet-days setback starts.</summary>
        public const float FloorDays = SetbackMath.QuietDays;

        /// <summary>From this many days without one, the colony's own doings come at once, whatever the week holds.</summary>
        public const float FloorOpenDays = FloorDays - 3f;

        /// <summary>
        /// From this many, with nothing of the colony's own on the way, the colony keeps an evening together itself: a day
        /// for it to find an evening when people are free, and still under the ten.
        /// </summary>
        public const float FloorEveningDays = FloorDays - 2f;

        /// <summary>How long a life goal's toast waits for the next gathering to be raised at, before it is raised at supper.</summary>
        public const int CupWaitDays = 2;

        /// <summary>Gatherings older than this are forgotten (the week and a margin; the floor reads the last one apart).</summary>
        public const int KeepDays = 10;

        public static int GapTicks => GapHours * TicksPerHour;

        public static int WaitTicks => WaitDays * TicksPerDay;

        /// <summary>The setting, kept in its range.</summary>
        public static int AWeek(int setting) => Math.Max(MinAWeek, Math.Min(MaxAWeek, setting));

        /// <summary>The gatherings held in the seven days up to <paramref name="now"/> (one held now counts).</summary>
        public static int InWeek(IReadOnlyList<int> held, int now)
        {
            if (held == null) return 0;
            int from = now - WeekDays * TicksPerDay, n = 0;
            foreach (int t in held)
                if (t > from && t <= now) n++;
            return n;
        }

        /// <summary>Ticks since the latest gathering at or before <paramref name="now"/>; <see cref="int.MaxValue"/> when there's none.</summary>
        public static int Since(IReadOnlyList<int> held, int now)
        {
            int best = int.MaxValue;
            if (held == null) return best;
            foreach (int t in held)
                if (t <= now && now - t < best) best = now - t;
            return best;
        }

        /// <summary>
        /// Room for one of the colony's own doings now: fewer gatherings in the last seven days than the week allows, and
        /// none in the last day and a half; or the floor is open (<paramref name="sinceLast"/>, ticks since the colony's last
        /// gathering of any kind, the game's own too), when it may come at once.
        /// </summary>
        public static bool Room(IReadOnlyList<int> held, int now, int aWeek, int sinceLast)
        {
            if (FloorOpen(sinceLast)) return true;
            return InWeek(held, now) < AWeek(aWeek) && Since(held, now) >= GapTicks;
        }

        /// <summary>So long without a gathering that the colony's own doings come at once.</summary>
        public static bool FloorOpen(int sinceLastTicks) => sinceLastTicks != int.MaxValue && sinceLastTicks >= FloorOpenDays * TicksPerDay;

        /// <summary>So long without a gathering that, with nothing of its own on the way, the colony keeps an evening together.</summary>
        public static bool FloorEvening(int sinceLastTicks) => sinceLastTicks != int.MaxValue && sinceLastTicks >= FloorEveningDays * TicksPerDay;

        /// <summary>How long one of the colony's own doings may wait from when it was planned.</summary>
        public static int RoutineDeadline(int created) => created + WaitTicks;

        /// <summary>The ticks kept: the last <see cref="KeepDays"/> days (a clock gone back forgets what's ahead of it).</summary>
        public static void Prune(List<int> held, int now)
        {
            held?.RemoveAll(t => t > now || now - t > KeepDays * TicksPerDay);
        }

        // ---------------- The calendar ----------------

        /// <summary>Days of the year apart, the short way round the year (day 58 and day 1 are 3 apart in a 60-day year).</summary>
        public static int DaysApart(int dayA, int dayB, int daysPerYear)
        {
            if (daysPerYear <= 0) return Math.Abs(dayA - dayB);
            int d = Math.Abs(dayA - dayB) % daysPerYear;
            return Math.Min(d, daysPerYear - d);
        }

        /// <summary>
        /// A calendar day still to come round this year (<paramref name="daysAhead"/> after today, 1 or more) is kept
        /// together with the one coming round today.
        /// </summary>
        public static bool KeptTogether(int daysAhead) => daysAhead >= 1 && daysAhead <= TogetherDays;

        /// <summary>A calendar day's gathering, with the colony's last gathering this many ticks ago, is kept at supper instead.</summary>
        public static bool AtSupper(int sinceLastGatheringTicks) => sinceLastGatheringTicks != int.MaxValue && sinceLastGatheringTicks < SupperDays * TicksPerDay;

        // ---------------- The customs ----------------

        /// <summary>The season of the year a day falls in (0 to 3), and a number for each season of the game (year * 4 + season).</summary>
        public static int SeasonOf(int dayOfYear) => Math.Max(0, Math.Min(3, dayOfYear / SeasonDays));

        public static int SeasonKey(int year, int dayOfYear) => year * 4 + SeasonOf(dayOfYear);

        /// <summary>A seasonal custom may be asked: none was asked yet this season (<paramref name="lastSeason"/> -1: never).</summary>
        public static bool CustomMayCome(int lastSeason, int season) => lastSeason != season;

        // ---------------- A life goal's toast ----------------

        /// <summary>A toast waiting for the next gathering is raised at supper instead, this long after it was promised.</summary>
        public static bool CupAtSupper(int promised, int now) => now - promised >= CupWaitDays * TicksPerDay;

        /// <summary>A toast nobody could raise within the colony's own wait is let go.</summary>
        public static bool CupLetGo(int promised, int now) => now - promised >= (CupWaitDays + WaitDays) * TicksPerDay;
    }
}
