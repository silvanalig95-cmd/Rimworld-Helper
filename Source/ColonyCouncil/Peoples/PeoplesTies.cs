using System;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The peoples and their ties' hour and day (the content update's B12, B17 and B22), run with the region's own
    /// (World/Region/RegionBook.cs): a visitor from a struggling town asks for a meal (FortuneShows), the word of a hard
    /// year reaches those with a tie to its people (KinNews), the neighbours for good come to the colony's gatherings and
    /// graves and keep their own days (HoldingDays). Each part on its own, so one that fails leaves the others.
    /// </summary>
    public static class PeoplesTies
    {
        public static void Hourly(int now)
        {
            Safe("a town's fortune at the gate", () => FortuneShows.Hourly(now));
            Safe("kin's news", () => KinNews.Hourly(now));
            if (CampTracker.Enabled) Safe("the neighbours' days", () => HoldingDays.Hourly(now));
        }

        public static void Daily(int now)
        {
            if (CampTracker.Enabled) Safe("the neighbours' own days", () => HoldingDays.Daily(now));
        }

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the peoples' ties: " + what, e);
            }
        }
    }
}
